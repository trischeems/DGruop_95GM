# CLAUDE.md — DGroup Monorepo (Manager_95GM)

Tài liệu định hướng cho Claude khi làm việc trên repo này. Đọc trước khi sửa code.

## Bối cảnh

Công ty xuất nhập / sản xuất hàng hoá, lưu lượng lớn (500+ người dùng đồng thời, mỗi bộ
phận vài nghìn đơn/tháng, dữ liệu cực nhiều). Đây là **monorepo nhiều app dùng chung 1 server**.
App đầu tiên: **Manager_Perfoment** (quản lý sản xuất may mặc). Sau này sẽ thêm **HRM** và các app khác.

## RANH GIỚI QUAN TRỌNG NHẤT — Server vs App client

Có hai loại "app", đừng nhầm:

| | Vị trí | Là gì |
|---|---|---|
| **Module SERVER** | `Server/Apps/<Ten>/` | Code API + schema DB chạy trên server Ubuntu, phục vụ 1 app. |
| **App CLIENT** | `App/<Ten>/` | Phần mềm .NET cài trên máy người dùng (UI, installer ISS). |

> Schema DB, controller, service, migration = **đồ server** → luôn ở `Server/Apps/…`, KHÔNG bao giờ để trong `App/…`.
> `App/Manager_Perfoment/` chỉ chứa app client (làm sau).

## Kiến trúc server (đã chốt)

- **.NET 8 SDK, ASP.NET Core, kiểu Controller (MVC).** Cross-platform (Windows dev / Ubuntu prod / macOS).
- **Modular monolith:** 1 process duy nhất (`DGroup.Server`), mỗi app = 1 folder module trong `Server/Apps/`.
  Thêm app mới = tạo folder + class `IAppModule` + thêm 1 dòng vào `ModuleRegistry.BuildModules()`.
- **Mỗi app 1 database Postgres riêng** (không lẫn). App đầu tiên dùng khối `dgroup_postgress` trong config.
- **Multi-tenant: schema-per-tenant.** Mỗi tenant (bộ phận/công ty con) = 1 PostgreSQL schema riêng
  trong DB của app. Row-Level Security ready.
- **Data access: Dapper only (raw SQL).** KHÔNG EF Core. Tự viết migration `.sql` versioned.
- **PostgreSQL 16.** Dev Windows: portable tự bung (`Server/start_pg.bat`). Prod Ubuntu: apt PGDG, không Docker.
- **API prefix:** app đầu tiên `/dgrpi/`, app sau `/dgrapp2/`, `/dgrapp3/`… KHÔNG dùng `/api/`.
- **Endpoint đặt tên tiếng Anh.**

## ĐIỂM DỄ SAI NHẤT — chọn schema tenant an toàn với connection pooling

**Luôn** truy cập DB theo tenant qua `ITenantConnection.RunAsync(tenant, scope => …)`. Cơ chế:
mở connection → `BEGIN` → `SET LOCAL search_path TO "<tenant>"` → chạy query → commit.
Vì dùng `SET LOCAL` (không phải `SET` trần), search_path tự reset khi transaction kết thúc →
**không rò rỉ schema sang request/tenant khác** khi connection quay lại pool.

Quy tắc bắt buộc:
- Repository nhận `TenantScope` (đã có tx đúng schema), **không tự mở connection**.
- SQL trong repo **không prefix schema** (dựa vào search_path). Migration V001 cũng vậy.
- Tên tenant luôn qua `SqlTenantValidator.Validate` (whitelist + regex) trước khi ghép vào SQL.
- Ghi tồn kho (nhập/xuất/giữ chỗ) làm trong **1** `RunAsync` + `SELECT … FOR UPDATE` (chống race 500+ user).

## Cấu hình — `Server/config.json` là NGUỒN SỰ THẬT

Mọi port, mật khẩu, tên DB… nằm trong `Server/config.json`. Sửa ở đó, không hard-code.
- `server.port` (string) → Kestrel bind. `https` (bool).
- `dgroup_postgress` (giữ nguyên typo): host/port/dbname/user/pass của app đầu tiên.
- `r2_backup`: biến đặt sẵn cho backup Cloudflare R2 (làm sau).
- `tenancy` (tuỳ chọn, có default): `default_tenant`, `allowed_tenants`, `auto_create_schema`, `tenant_header`.

Class map: `Server/Configuration/*.cs` (dùng `[JsonPropertyName]` cho tên có typo). Không phá cấu trúc JSON cũ.

## Chạy dev (Windows)

```bat
Server\start_pg.bat     :: bung + start PostgreSQL 16 portable (lần đầu tải ~338MB), tạo DB
run_server.bat          :: build + chạy server (tự gọi start_pg.bat), in URL
Server\stop_pg.bat      :: dừng PostgreSQL
```

Kiểm tra nhanh:
- `GET /dgrpi/health` → server sống.
- `GET /dgrpi/health/db` → ping PostgreSQL.
- Chọn tenant qua header `X-Tenant: public` (mặc định `public` nếu không gửi).

## Cấu trúc thư mục

```
Server/
  Program.cs                    bootstrap: config, DI, Kestrel port, migration, pipeline
  config.json                   NGUỒN SỰ THẬT cấu hình
  Configuration/                map config.json -> POCO
  Infrastructure/
    Data/                       Dapper + tenant (ITenantConnection = cách DUY NHẤT truy cập DB)
    Tenancy/                    ITenantContext + middleware resolve tenant từ header
    Migrations/                 MigrationRunner (chạy V*.sql theo từng schema tenant)
    Web/                        IAppModule, ModuleRegistry, ApiResult, GlobalExceptionMiddleware
  Controllers/HealthController  /dgrpi/health, /dgrpi/health/db
  Apps/ManagerPerformance/      MODULE SERVER cho app Manager_Perfoment
    ManagerPerformanceModule.cs implements IAppModule (prefix "dgrpi")
    Database/Migrations/V001__init.sql   DDL per-tenant (authoritative, không prefix schema)
    Contracts/ Repositories/ Services/ Controllers/
App/
  Manager_Perfoment/            APP CLIENT (.NET UI + installer ISS) — làm sau
```

## App client Manager_Perfoment (Avalonia UI, .NET 8) — đã có khung + lát cắt chạy được

App client ở `App/Manager_Perfoment/` (project `DGroup.App.ManagerPerformance`). **Avalonia UI 11.2**,
MVVM (CommunityToolkit.Mvvm). App KHÔNG chứa database — chỉ gọi API server `/dgrpi/` qua `HttpClient`.

- **`config.json` của app** (mỗi app có config riêng): `app` (name/title/vendor), `server`
  (base_url/api_prefix/timeout), `tenant` (current/header). Người dùng ghi đè bằng `user-config.json`
  ở thư mục dữ liệu runtime. Đọc bởi `Services/AppConfig.cs`.
- **Cross-platform, một logic path duy nhất, tự chuyển hoá theo HĐH** (`Services/AppPaths.cs`, không hard-code):
  - Nguồn app (chỉ đọc): Windows `C:\Program Files\Dgroup\App\Manager_performent`;
    macOS `/Applications/Dgroup/…`; Linux `/opt/dgroup/…`. Installer Windows dùng Inno Setup (ISS).
  - Dữ liệu runtime (cache/logs/user-config): `Environment.SpecialFolder.LocalApplicationData` +
    `Dgroup/App/Manager_performent` → Windows `%LOCALAPPDATA%\…`, macOS `~/Library/Application Support/…`,
    Linux `~/.local/share/…`.
- **Lát cắt hiện có:** 3 tab — Nguyên vật liệu · Nhập kho · Cảnh báo tồn thấp (khớp 3 nhóm API server đã chạy).
  Đã build + chạy thử: app khởi động, kết nối server, hiển thị dữ liệu. Nghiệp vụ còn lại bổ sung sau.
- Chạy: server trước (`run_server.bat`), rồi mở app bằng **`run_app_quanly.bat`** (ở gốc dự án —
  tự đọc server URL từ config app, cảnh báo nếu server chưa chạy, rồi build + mở app).

## Nghiệp vụ app Manager_Perfoment (tóm tắt)

Kho NVL → Định mức BOM → Đơn sản xuất → Kế hoạch → Cắt vải → May → QC → Nhập kho thành phẩm.
Công thức chính đóng gói trong 4 view (query trực tiếp bằng Dapper):
- `v_material_stock` — tồn khả dụng = on_hand − reserved, cờ tồn thấp.
- `v_max_output_by_material` / `v_max_output_by_product` — sản lượng tối đa = min theo từng NVL, NVL nút thắt.
- `v_order_material_requirement` — nhu cầu NVL/đơn, thiếu hụt, đề xuất mua thêm.

## Quy ước khi mở rộng

- **Không seed dữ liệu** trừ khi được yêu cầu rõ.
- Migration cũ **bất biến** (checksum tracked). Thay đổi schema = tạo file `V{nnn}__*.sql` mới.
- Kiểu số dùng `numeric`, không `float`. Khoá chính `bigint identity`; id lộ ra ngoài dùng `public_code uuid`.
- Response chuẩn `{ ok, data, error }` (dùng `ApiResult<T>`).
