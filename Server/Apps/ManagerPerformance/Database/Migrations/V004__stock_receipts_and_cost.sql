-- =====================================================================================
-- App: ManagerPerfoment
-- File: V004__stock_receipts_and_cost.sql
-- Muc dich (2 phan, theo feedback khach hang M1):
--   PHAN 1: Phieu NHAP kho NHIEU DONG (stock_receipts + stock_receipt_items) —
--           nhap nhieu NVL trong 1 phieu, luu 1 lan (giong material_issues da co).
--   PHAN 2: Bo sung cot GIA vao view v_material_stock — hien thi don gia nhap gan nhat,
--           don gia binh quan gia quyen, va gia tri ton, ngay tren bang "Ton kho kha dung".
--
-- QUY UOC: khong prefix schema (chay theo search_path tenant). V001/V002/V003 BAT BIEN;
--   day la file moi. Kieu so numeric; boc ROUND(...) khi co phep chia (bai hoc V003).
-- =====================================================================================


-- =====================================================================================
-- PHAN 1: PHIEU NHAP KHO NHIEU DONG
-- =====================================================================================
-- Header phieu nhap. Nhap kho la DOC LAP (khong gan don san xuat) -> khong co
--   production_order_id. Moi item se ghi 1 stock_transactions RECEIPT (quantity duong),
--   ref_type='RECEIPT', ref_id=stock_receipt_id. Cong qty_on_hand, KHONG dung qty_reserved.
CREATE TABLE stock_receipts (
    id           bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    receipt_no   text        NOT NULL,                 -- so phieu (sinh 'PN-'+timestamp)
    warehouse_id bigint      NOT NULL REFERENCES warehouses(id),
    status       text        NOT NULL DEFAULT 'POSTED'
        CHECK (status IN ('DRAFT','POSTED','CANCELLED')),
    received_at  timestamptz NOT NULL DEFAULT now(),
    note         text,
    created_at   timestamptz NOT NULL DEFAULT now(),
    created_by   bigint      REFERENCES users(id),
    CONSTRAINT uq_stock_receipt_no UNIQUE (receipt_no)
);
COMMENT ON TABLE stock_receipts IS 'Phieu nhap kho NVL nhieu dong (header). Khi POSTED cong ton thuc.';
CREATE INDEX ix_stock_receipts_wh ON stock_receipts (warehouse_id);
CREATE INDEX ix_stock_receipts_time ON stock_receipts (received_at DESC);

-- Chi tiet dong phieu nhap: moi NVL 1 dong voi so luong + don gia.
CREATE TABLE stock_receipt_items (
    id                bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    stock_receipt_id  bigint   NOT NULL REFERENCES stock_receipts(id) ON DELETE CASCADE,
    material_id       bigint   NOT NULL REFERENCES materials(id),
    qty_received      numeric(18,4) NOT NULL CHECK (qty_received > 0),
    unit_cost         numeric(18,4) NOT NULL DEFAULT 0 CHECK (unit_cost >= 0),
    CONSTRAINT uq_stock_receipt_item UNIQUE (stock_receipt_id, material_id)
);
COMMENT ON TABLE stock_receipt_items IS 'Chi tiet NVL trong phieu nhap kho (so luong + don gia moi dong).';
CREATE INDEX ix_receipt_items_receipt ON stock_receipt_items (stock_receipt_id);
CREATE INDEX ix_receipt_items_material ON stock_receipt_items (material_id);


-- =====================================================================================
-- PHAN 2: BO SUNG COT GIA VAO VIEW v_material_stock
-- =====================================================================================
-- CREATE OR REPLACE VIEW: PHAI giu nguyen 9 cot cu (dung ten + thu tu), CHI append cot moi
--   o cuoi. Bo sung 3 cot gia tinh tu stock_transactions (txn_type='RECEIPT'):
--     last_unit_cost   : don gia lan nhap gan nhat (RECEIPT moi nhat theo created_at).
--     avg_unit_cost    : don gia binh quan gia quyen = SUM(qty*unit_cost)/SUM(qty) cac RECEIPT.
--     stock_value      : gia tri ton uoc tinh = total_on_hand * avg_unit_cost.
--   (materials.standard_cost la don gia THAM CHIEU nhap tay, khac gia nhap thuc te nay.)
CREATE OR REPLACE VIEW v_material_stock AS
SELECT
    m.id                                             AS material_id,
    m.sku,
    m.name,
    m.reorder_level,
    m.reorder_quantity,
    COALESCE(SUM(s.qty_on_hand), 0)                  AS total_on_hand,
    COALESCE(SUM(s.qty_reserved), 0)                 AS total_reserved,
    COALESCE(SUM(s.qty_on_hand - s.qty_reserved), 0) AS total_available,
    (COALESCE(SUM(s.qty_on_hand - s.qty_reserved), 0) < m.reorder_level) AS is_low_stock,
    -- ----- cot GIA moi (append cuoi) -----
    COALESCE(rc.last_unit_cost, 0)                   AS last_unit_cost,
    COALESCE(ROUND(rc.avg_unit_cost, 4), 0)          AS avg_unit_cost,
    ROUND(COALESCE(SUM(s.qty_on_hand), 0) * COALESCE(rc.avg_unit_cost, 0), 2) AS stock_value
FROM materials m
LEFT JOIN stock s ON s.material_id = m.id
-- Gia tu so cai: don gia RECEIPT gan nhat + binh quan gia quyen theo tung NVL.
LEFT JOIN (
    SELECT
        t.material_id,
        -- don gia lan nhap gan nhat (RECEIPT moi nhat).
        (SELECT t2.unit_cost FROM stock_transactions t2
         WHERE t2.material_id = t.material_id AND t2.txn_type = 'RECEIPT'
         ORDER BY t2.created_at DESC, t2.id DESC LIMIT 1)                       AS last_unit_cost,
        -- binh quan gia quyen tren toan bo lan nhap.
        SUM(t.quantity * t.unit_cost) / NULLIF(SUM(t.quantity), 0)              AS avg_unit_cost
    FROM stock_transactions t
    WHERE t.txn_type = 'RECEIPT'
    GROUP BY t.material_id
) rc ON rc.material_id = m.id
WHERE m.is_active
GROUP BY m.id, m.sku, m.name, m.reorder_level, m.reorder_quantity,
         rc.last_unit_cost, rc.avg_unit_cost;
COMMENT ON VIEW v_material_stock IS
    'Ton kha dung gop moi kho theo NVL + gia: last_unit_cost (nhap gan nhat), avg_unit_cost (binh quan gia quyen), stock_value (gia tri ton).';


-- =====================================================================================
-- HET V004__stock_receipts_and_cost.sql
-- =====================================================================================
