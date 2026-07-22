-- Server sẽ sử dụng hệ thống DNS và Cloudflared tunnel chạy trên hệ điều hành linux ubuntu 2404, không sử dụng docker , chạy thuần mở port mở postgress , mở tunnel  thôi .
-- Nếu đây là máy window chắc chắn là máy của dev thôi , deploy sẽ làm trên ubuntu .
-- các endpoin hay api phải được đặt tên tiếng anh . api sẽ không được ghi là /api/ nữa phải ghi khác thành /dgrpi/ . tránh xung đột nếu dùng nhiều app . ví dụ /dgrpi/, /dgrapp1/, dgrapp2/ v...v
-- mỗi app hay một loại trang tjao được folder riêng lẻ trong các cấu trúc càng tốt sau này đọc code cho dễ còn nếu bị ảnh hưởng bởi kiến trúc thì thôi.
-- framework chuẩn cho doanh nghiệp .

---

# DGroup Server — Yêu cầu & Kiến trúc

## Mục tiêu (yêu cầu đặt ra)

- Phục vụ **≥ 500 người dùng đồng thời**, truy xuất **nhanh**. Công ty xuất nhập / sản xuất
  hàng hoá rất nhiều cùng lúc, **dữ liệu cực kỳ nhiều** (một bộ phận nhỏ có thể vài nghìn đơn/tháng).
- Kiến trúc **tối ưu cho truy xuất dữ liệu**, có khả năng **scale** (chuẩn bị sẵn hệ thống
  replica / multi-tenant). Sau này còn làm thêm **HRM** trên cùng server này.
- Là **server dùng chung cho nhiều app**. **Mỗi app một database Postgres riêng** — không để lẫn.
- App đầu tiên phục vụ: **Manager_Perfoment** (quản lý sản xuất may mặc), client dùng **.NET 8**
  (chạy Windows / macOS / Linux), yêu cầu tốc độ truy xuất về server nhanh.

## Công nghệ & quyết định kiến trúc (đã chốt)

- **.NET 8 SDK, ASP.NET Core, kiểu Controller (MVC).**
- **Modular monolith:** 1 process `DGroup.Server`; mỗi app = 1 folder module trong `Server/Apps/`.
  Thêm app = tạo folder + class `IAppModule` + 1 dòng trong `ModuleRegistry`.
- **Multi-tenant: schema-per-tenant.** Mỗi tenant (bộ phận / công ty con) = 1 PostgreSQL schema
  riêng trong DB của app. Cách ly dữ liệu tốt, backup/restore từng tenant, query nhanh vì mỗi
  schema nhỏ. Row-Level Security ready.
- **Data access: Dapper (raw SQL) + Npgsql.** KHÔNG EF Core. Migration `.sql` versioned tự viết.
- **PostgreSQL 16.** Connection pooling built-in của Npgsql (Max Pool 100); khi cần hơn dùng
  **PgBouncer** (transaction-mode) phía trước — đã tính đường nâng cấp.
- **API prefix:** app đầu tiên `/dgrpi/`, app sau `/dgrapp2/`… (không dùng `/api/`).

### RANH GIỚI: đồ SERVER vs APP CLIENT

- Code API + **schema DB** + migration = **đồ server** → nằm trong `Server/Apps/<Ten>/`.
- **App client** (.NET UI, installer ISS cài trên máy người dùng) → nằm trong `App/<Ten>/`.
- KHÔNG để schema/migration/controller trong thư mục `App/`.

## Cấu hình — `config.json` là nguồn sự thật

Mọi mật khẩu, port, tên DB… nằm trong `Server/config.json` để chỉnh sửa dễ (đã đặt sẵn nhiều biến
để dùng sau: `r2_backup`, `tenancy`). Không hard-code trong code.

## Chạy trên máy dev (Windows)

```bat
Server\start_pg.bat     :: bung + start PostgreSQL 16 portable (lần đầu tải ~338MB), initdb, tạo DB
run_server.bat          :: build & chạy server .NET 8 (tự gọi start_pg.bat), in URL http(s)://localhost:<port>
Server\stop_pg.bat      :: dừng PostgreSQL (pg_ctl stop -m fast)
```

Portable PG bung vào `Server/pgsql`, data ở `Server/pgdata` (đã .gitignore). Nếu `https:true`
mà chưa có dev-cert, chạy `dotnet dev-certs https --trust` một lần hoặc để dev chạy http.

## Deploy Ubuntu 24.04 (KHÔNG Docker) + Cloudflared tunnel

1. Cài **.NET 8 SDK** + **postgresql-16** (apt PGDG chính thức). Không dùng portable trên prod.
2. Tạo DB + user theo `config.json` (đặt **mật khẩu mạnh** khác dev). `pg_hba.conf` dùng `scram-sha-256`.
   PG chỉ `listen_addresses='localhost'` (không mở port ra ngoài).
3. `dotnet publish Server -c Release -o /opt/dgroup/server`. Đặt `config.json` prod cạnh binary.
4. Chạy bằng **systemd service** (`dgroup-server.service`), `ASPNETCORE_URLS=http://127.0.0.1:8765`.
5. **Cloudflared tunnel** trỏ hostname công khai → `127.0.0.1:8765` (TLS terminate ở Cloudflare).
6. Backup: cron `pg_dump` → upload Cloudflare R2 (khối `r2_backup` trong config, làm sau).

## Tuning PostgreSQL 16 cho 500+ user & dữ liệu lớn (gợi ý, máy 8–16GB RAM)

- `max_connections = 200` (app pool Max 100; cần hơn → PgBouncer transaction-mode).
- `shared_buffers = 25% RAM`, `effective_cache_size = 60–75% RAM`, `work_mem = 16–32MB`.
- `maintenance_work_mem = 512MB`, `wal_compression = on`, `max_wal_size = 4GB`, `checkpoint_timeout = 15min`.
- SSD: `random_page_cost = 1.1`, `effective_io_concurrency = 200`.
- Autovacuum tích cực cho bảng ghi nhiều (`stock_transactions`, `material_issues`):
  `autovacuum_vacuum_scale_factor = 0.05`.
- **Scale sau này:** partition `stock_transactions` theo thời gian; **streaming replication** để có
  replica đọc cho báo cáo; tách tenant lớn sang DB/replica riêng nếu cần.

## Tối ưu truy xuất (đã áp dụng trong schema V001)

- Khoá chính `bigint identity` (index B-tree nhỏ, monotonic, ít phân mảnh) thay uuid ngẫu nhiên.
- Index nóng có `INCLUDE` để index-only scan (vd `ix_stock_material`, `ix_bom_items_bom`).
- Partial index cho hàng đợi/lọc trạng thái (vd đơn `CONFIRMED`, cảnh báo `OPEN`, BOM `ACTIVE`).
- Công thức nghiệp vụ đóng gói trong **view** (`v_material_stock`, `v_max_output_*`,
  `v_order_material_requirement`) để query nhất quán, nhanh.
- Ghi tồn kho dùng `SELECT … FOR UPDATE` trong 1 transaction (chống race khi đồng thời cao).