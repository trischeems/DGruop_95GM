-- =====================================================================================
-- App: ManagerPerfoment
-- File: V005__stock_uom.sql
-- Muc dich: bo sung MA/TEN DON VI TINH (uom_code/uom_name) vao view v_material_stock
--   de UI hien don vi (kg/m/cai...) canh so luong ton/kha dung/nguong.
-- CREATE OR REPLACE VIEW: giu nguyen thu tu cot cu, CHI append cot moi o cuoi.
-- V001-V004 bat bien; day la file moi. Khong prefix schema (theo tenant).
-- =====================================================================================

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
    COALESCE(rc.last_unit_cost, 0)                   AS last_unit_cost,
    COALESCE(ROUND(rc.avg_unit_cost, 4), 0)          AS avg_unit_cost,
    ROUND(COALESCE(SUM(s.qty_on_hand), 0) * COALESCE(rc.avg_unit_cost, 0), 2) AS stock_value,
    -- ----- cot DVT moi (append cuoi) -----
    u.code                                           AS uom_code,
    u.name                                           AS uom_name
FROM materials m
LEFT JOIN stock s ON s.material_id = m.id
LEFT JOIN units_of_measure u ON u.id = m.uom_id
LEFT JOIN (
    SELECT
        t.material_id,
        (SELECT t2.unit_cost FROM stock_transactions t2
         WHERE t2.material_id = t.material_id AND t2.txn_type = 'RECEIPT'
         ORDER BY t2.created_at DESC, t2.id DESC LIMIT 1)                       AS last_unit_cost,
        SUM(t.quantity * t.unit_cost) / NULLIF(SUM(t.quantity), 0)              AS avg_unit_cost
    FROM stock_transactions t
    WHERE t.txn_type = 'RECEIPT'
    GROUP BY t.material_id
) rc ON rc.material_id = m.id
WHERE m.is_active
GROUP BY m.id, m.sku, m.name, m.reorder_level, m.reorder_quantity,
         rc.last_unit_cost, rc.avg_unit_cost, u.code, u.name;
COMMENT ON VIEW v_material_stock IS
    'Ton kha dung gop moi kho theo NVL + gia + DVT (uom_code/uom_name).';

-- =====================================================================================
-- HET V005__stock_uom.sql
-- =====================================================================================
