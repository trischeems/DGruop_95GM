yêu cầu làm  app :
Kho nguyên vật liệu
↓
Định mức BOM
↓
Đơn hàng sản xuất
↓
Kế hoạch sản xuất
↓
Cắt vải
↓
May
↓
QC
↓
Nhập kho thành phẩm
chủ yếu theo quy trình và các module này
Mai c sẽ gửi chi tiết cho em
Nhập nguyên vật liệu vào kho
↓
Cập nhật tồn kho khả dụng
↓
Chọn mã hàng cần sản xuất
↓
Lấy BOM/định mức nguyên vật liệu
↓
Tính sản lượng tối đa theo từng NVL
↓
Chọn NVL có sản lượng thấp nhất
↓
Hiển thị sản lượng tối đa có thể sản xuất
↓
Cảnh báo NVL thiếu
↓
Đề xuất số lượng cần mua thêm
Tính sản lượng tối đa theo từng mã hàng
Tính thiếu hụt NVL theo đơn hàng
Tính nguyên vật liệu cần mua thêm
Giữ chỗ nguyên vật liệu cho đơn hàng đã xác nhận
Trừ kho khi xuất cho sản xuất
Đối chiếu NVL cấp phát và thành phẩm đầu ra
Báo cáo hao hụt thực tế so với định mức
Cảnh báo tồn kho thấp
Cảnh báo đơn hàng có nguy cơ thiếu NVL
Lịch sử điều chỉnh định mức
Phê duyệt thay đổi BOM/định mức

---

# App client Manager_Perfoment (Avalonia UI, .NET 8)

Đây là **app client** (phần mềm cài trên máy người dùng), KHÔNG chứa database.
App chỉ gọi API tới **server** (`/dgrpi/...`); database nằm ở server (PostgreSQL).

```
App client (Avalonia UI)  ──HTTP /dgrpi/──►  Server (.NET 8)  ──►  PostgreSQL
   [App/Manager_Perfoment/]                   [Server/]              [dgroup_db]
```

## Công nghệ

- **.NET 8**, **Avalonia UI 11.2** (desktop native, chạy Windows / macOS / Linux từ 1 code).
- MVVM (CommunityToolkit.Mvvm), `HttpClient` gọi API server.

## Cấu hình — `config.json` của app

Mỗi app có `config.json` riêng chứa thông tin của app (tách khỏi config server):
- `app`: name / title / vendor.
- `server`: `base_url` (URL server), `api_prefix` (`dgrpi`), `timeout_seconds`.
- `tenant`: `current` (schema tenant), `header` (`X-Tenant`).

Người dùng có thể ghi đè cấu hình runtime bằng file `user-config.json` đặt ở thư mục dữ liệu app.

## Đường dẫn (tự chuyển hoá theo HĐH, không hard-code)

| | Windows | macOS | Linux |
|---|---|---|---|
| **Nguồn app** (installer ghi, chỉ đọc) | `C:\Program Files\Dgroup\App\Manager_performent` | `/Applications/Dgroup/…` | `/opt/dgroup/…` |
| **Dữ liệu runtime** (cache/logs/user-config) | `%LOCALAPPDATA%\Dgroup\App\Manager_performent` | `~/Library/Application Support/Dgroup/App/Manager_performent` | `~/.local/share/Dgroup/App/Manager_performent` |

Code dùng `Environment.SpecialFolder.LocalApplicationData` (xem `Services/AppPaths.cs`) → .NET tự
trả đúng thư mục mỗi HĐH. Installer Windows dùng **Inno Setup (ISS)**.

## Cấu trúc

```
Services/AppPaths.cs      duong dan data runtime cross-platform
Services/AppConfig.cs     doc config.json (+ user-config.json ghi de)
Services/ApiClient.cs     goi API server /dgrpi/ (tu gan header X-Tenant)
Models/ApiModels.cs       model khop DTO server
ViewModels/               MVVM (MainWindowViewModel)
Views/                    MainWindow.axaml (3 tab: NVL, Nhập kho, Cảnh báo tồn thấp)
config.json               cau hinh app
```

## Chạy thử

1. Chạy server trước: `run_server.bat` (ở gốc dự án) — tự bật PostgreSQL + server.
2. Mở app: **`run_app_quanly.bat`** (ở gốc dự án) — tự đọc server URL từ config app, kiểm tra server,
   rồi build + mở app. (Hoặc `dotnet run --project App/Manager_Perfoment`, hoặc mở solution.)
3. App tự kết nối server, hiển thị NVL / tồn kho / cảnh báo tồn thấp. Bấm **Làm mới** để tải lại.

Hiện app đã có lát cắt: **Nguyên vật liệu · Nhập kho · Cảnh báo tồn thấp** (khớp 3 nhóm API server
đã chạy). Các nghiệp vụ còn lại (BOM, đơn sản xuất, Cắt-May-QC…) bổ sung sau khi server có API tương ứng.