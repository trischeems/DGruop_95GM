-- =====================================================================================
-- clear_all_data.sql  —  XOA SACH du lieu cua tenant, dua DB ve dung trang thai
--   "vua cai xong, chua ai nhap gi" (nhu database moi chay migration lan dau).
--
-- GIU: cau truc bang/view/trigger + bang __migration_history (khong chay lai migration).
-- XOA: toan bo du lieu nghiep vu (NVL, ton kho, BOM, don, cong doan, phieu nhap/xuat...).
-- DUNG LAI: danh muc nen chuan do V001/V002/V006 seed (don vi tinh, nhom NVL, kho,
--   cong doan, mau quy trinh RT-MAY) — xoa het roi seed lai tu dau nen ID bat dau tu 1,
--   khop mac dinh uomId=1 / warehouseId=1 cua app client.
--
-- Xoa theo thu tu con -> cha (KHONG dung TRUNCATE CASCADE de kiem soat thu tu ro rang).
-- Dung boi run_all_trong.bat. Truyen -v tenant=<schema> (mac dinh public).
-- =====================================================================================
\if :{?tenant}
\else
  \set tenant public
\endif
SET search_path TO :"tenant";
\echo Xoa sach du lieu tenant: :tenant
BEGIN;

-- -------------------------------------------------------------------------------------
-- 1. DU LIEU NGHIEP VU (con -> cha)
-- -------------------------------------------------------------------------------------
DELETE FROM alerts;
DELETE FROM loss_reports;
DELETE FROM finished_goods_receipts;
DELETE FROM material_issue_items;
DELETE FROM material_issues;
DELETE FROM material_reservations;
DELETE FROM production_steps;
DELETE FROM production_plans;
DELETE FROM production_order_items;   -- V007: dong don (cha cua cac bang tren)
DELETE FROM production_orders;
DELETE FROM bom_approvals;
DELETE FROM bom_change_history;
DELETE FROM bom_items;
DELETE FROM bom;
DELETE FROM stock_receipt_items;      -- V004: chi tiet phieu nhap kho
DELETE FROM stock_receipts;           -- V004: header phieu nhap kho
DELETE FROM stock_transactions;
DELETE FROM stock;
DELETE FROM products;
DELETE FROM materials;
DELETE FROM users;

-- -------------------------------------------------------------------------------------
-- 2. DANH MUC NEN — xoa sach roi seed lai (bo cac muc nguoi dung tu them khi test)
-- -------------------------------------------------------------------------------------
DELETE FROM routing_steps;            -- V006: buoc cua mau quy trinh
DELETE FROM production_routings;      -- V006: mau quy trinh
DELETE FROM production_stages;
DELETE FROM warehouses;
DELETE FROM material_categories;
DELETE FROM units_of_measure;

-- -------------------------------------------------------------------------------------
-- 3. RESET IDENTITY — moi bang bat dau lai tu id = 1
-- -------------------------------------------------------------------------------------
ALTER TABLE users                   ALTER COLUMN id RESTART WITH 1;
ALTER TABLE units_of_measure        ALTER COLUMN id RESTART WITH 1;
ALTER TABLE material_categories     ALTER COLUMN id RESTART WITH 1;
ALTER TABLE warehouses              ALTER COLUMN id RESTART WITH 1;
ALTER TABLE materials               ALTER COLUMN id RESTART WITH 1;
ALTER TABLE stock                   ALTER COLUMN id RESTART WITH 1;
ALTER TABLE stock_transactions      ALTER COLUMN id RESTART WITH 1;
ALTER TABLE stock_receipts          ALTER COLUMN id RESTART WITH 1;
ALTER TABLE stock_receipt_items     ALTER COLUMN id RESTART WITH 1;
ALTER TABLE products                ALTER COLUMN id RESTART WITH 1;
ALTER TABLE bom                     ALTER COLUMN id RESTART WITH 1;
ALTER TABLE bom_items               ALTER COLUMN id RESTART WITH 1;
ALTER TABLE bom_approvals           ALTER COLUMN id RESTART WITH 1;
ALTER TABLE bom_change_history      ALTER COLUMN id RESTART WITH 1;
ALTER TABLE production_orders       ALTER COLUMN id RESTART WITH 1;
ALTER TABLE production_order_items  ALTER COLUMN id RESTART WITH 1;
ALTER TABLE production_plans        ALTER COLUMN id RESTART WITH 1;
ALTER TABLE production_stages       ALTER COLUMN id RESTART WITH 1;
ALTER TABLE production_steps        ALTER COLUMN id RESTART WITH 1;
ALTER TABLE production_routings     ALTER COLUMN id RESTART WITH 1;
ALTER TABLE routing_steps           ALTER COLUMN id RESTART WITH 1;
ALTER TABLE material_reservations   ALTER COLUMN id RESTART WITH 1;
ALTER TABLE material_issues         ALTER COLUMN id RESTART WITH 1;
ALTER TABLE material_issue_items    ALTER COLUMN id RESTART WITH 1;
ALTER TABLE finished_goods_receipts ALTER COLUMN id RESTART WITH 1;
ALTER TABLE loss_reports            ALTER COLUMN id RESTART WITH 1;
ALTER TABLE alerts                  ALTER COLUMN id RESTART WITH 1;

-- -------------------------------------------------------------------------------------
-- 4. SEED LAI DANH MUC NEN — copy dung noi dung V001 / V002 / V006
--    (khong chay lai migration duoc vi __migration_history da ghi nhan)
-- -------------------------------------------------------------------------------------
-- 4a. Don vi do (V002) — dong dau id=1 = 'M' (mac dinh cua app client).
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

-- 4b. Nhom NVL (V002).
INSERT INTO material_categories (code, name) VALUES
    ('VAI',      'Vải'),
    ('CHI',      'Chỉ may'),
    ('CUC',      'Cúc / Nút'),
    ('KHOAKEO',  'Khoá kéo'),
    ('NHAN',     'Nhãn mác'),
    ('PHULIEU',  'Phụ liệu khác')
ON CONFLICT (code) DO NOTHING;

-- 4c. Kho (V002) — dong dau id=1 = 'WH-NVL' (mac dinh cua app client).
INSERT INTO warehouses (code, name) VALUES
    ('WH-NVL', 'Kho nguyên vật liệu'),
    ('WH-TP',  'Kho thành phẩm')
ON CONFLICT (code) DO NOTHING;

-- 4d. Cong doan san xuat (V001).
INSERT INTO production_stages (code, name, seq) VALUES
    ('CUTTING',    'Cắt vải',              1),
    ('SEWING',     'May',                  2),
    ('QC',         'Kiểm tra chất lượng',  3),
    ('FG_RECEIPT', 'Nhập kho thành phẩm',  4)
ON CONFLICT (code) DO NOTHING;

-- 4e. Mau quy trinh mac dinh (V006) — don moi tao van co quy trinh de chon.
INSERT INTO production_routings (code, name, note, is_default)
VALUES ('RT-MAY', 'Quy trình may chuẩn', 'Cắt vải → May → QC → Nhập kho thành phẩm', true)
ON CONFLICT (code) DO NOTHING;

INSERT INTO routing_steps (routing_id, stage_id, seq, is_optional)
SELECT r.id, st.id, st.seq, false
FROM production_routings r
CROSS JOIN production_stages st
WHERE r.code = 'RT-MAY' AND st.code IN ('CUTTING','SEWING','QC','FG_RECEIPT')
ON CONFLICT (routing_id, stage_id) DO NOTHING;

COMMIT;

\echo Da xoa sach. DB trong nhu moi cai (chi con danh muc nen chuan).
