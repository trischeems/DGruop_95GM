-- =====================================================================================
-- App: ManagerPerfoment (Quản lý sản xuất may mặc)
-- File: V002__seed_reference_data.sql  -- Seed DỮ LIỆU NỀN (danh mục tra cứu) cho MỖI tenant
-- DBMS: PostgreSQL 16
-- =====================================================================================
--
-- TẠI SAO CÓ FILE NÀY:
--   Dữ liệu nằm trong pgdata (KHÔNG commit vào git). Máy dev A tự nhập tay vài đơn vị
--   tính / kho => chạy được; máy dev B pull code về, DB mới tinh, các bảng danh mục RỖNG
--   => tạo NVL dính lỗi khoá ngoại "materials_uom_id_fkey" (uom_id không tồn tại).
--   App client mặc định uomId=1 và warehouseId=1 (MaterialsViewModel), nên id=1 PHẢI có sẵn.
--   => Seed dữ liệu nền chuẩn qua migration để "pull phát chạy luôn", không phụ thuộc máy.
--   (Cùng cơ chế với production_stages đã seed sẵn trong V001.)
--
-- NGUYÊN TẮC:
--   - KHÔNG prefix schema (dựa vào search_path của tenant, giống V001).
--   - Idempotent: ON CONFLICT (code) DO NOTHING => chạy lại nhiều lần vẫn an toàn.
--   - Chỉ seed DANH MỤC NỀN mang tính CHUẨN/TRA CỨU (đơn vị tính, nhóm NVL, kho mặc định),
--     KHÔNG seed dữ liệu nghiệp vụ (NVL, đơn hàng, tồn kho...).
--   - Dựa vào IDENTITY tuần tự trên schema mới => dòng đầu mỗi bảng nhận id = 1
--     (khớp mặc định uomId=1 / warehouseId=1 của app client).
-- =====================================================================================


-- -------------------------------------------------------------------------------------
-- 1. ĐƠN VỊ ĐO (units_of_measure) — CHUẨN, dùng chung mọi tenant.
--    Dòng đầu (id=1) = 'M' (Mét): đơn vị mặc định của app client khi tạo NVL.
-- -------------------------------------------------------------------------------------
INSERT INTO units_of_measure (code, name) VALUES
    ('M',     'Mét'),
    ('KG',    'Kilogram'),
    ('PCS',   'Cái'),
    ('ROLL',  'Cuộn'),
    ('YARD',  'Yard'),
    ('CONE',  'Cuộn chỉ'),
    ('SET',   'Bộ'),
    ('PAIR',  'Đôi'),
    ('BOX',   'Hộp'),
    ('G',     'Gram')
ON CONFLICT (code) DO NOTHING;


-- -------------------------------------------------------------------------------------
-- 2. NHÓM NGUYÊN VẬT LIỆU (material_categories) — phân loại NVL ngành may.
--    category_id ở materials là NULL-able nên không bắt buộc, seed cho tiện báo cáo/BOM.
-- -------------------------------------------------------------------------------------
INSERT INTO material_categories (code, name) VALUES
    ('VAI',      'Vải'),
    ('CHI',      'Chỉ may'),
    ('CUC',      'Cúc / Nút'),
    ('KHOAKEO',  'Khoá kéo'),
    ('NHAN',     'Nhãn mác'),
    ('PHULIEU',  'Phụ liệu khác')
ON CONFLICT (code) DO NOTHING;


-- -------------------------------------------------------------------------------------
-- 3. KHO (warehouses) — kho mặc định để nhập NVL ngay sau khi cài.
--    Dòng đầu (id=1) = 'WH-NVL': kho mặc định của app client khi nhập kho.
-- -------------------------------------------------------------------------------------
INSERT INTO warehouses (code, name) VALUES
    ('WH-NVL', 'Kho nguyên vật liệu'),
    ('WH-TP',  'Kho thành phẩm')
ON CONFLICT (code) DO NOTHING;


-- =====================================================================================
-- HẾT V002__seed_reference_data.sql
-- =====================================================================================
