-- =====================================================================================
-- App: ManagerPerfoment
-- File: V003__fix_report_view_scale.sql
-- Muc dich: SUA scale (so chu so thap phan) cua cac cot tinh toan trong view bao cao.
-- =====================================================================================
--
-- LOI GOC: bieu thuc "(1 + waste_pct/100)" dung phep chia numeric. PostgreSQL cho ra
--   numeric voi SCALE rat lon (vd 5.0000/100 = 0.05000000000000000000, ~20 chu so thap phan).
--   Nhan don gia * san luong lam scale cong don len ~30 chu so. Khi Npgsql map sang
--   System.Decimal (toi da 28-29 chu so co nghia) -> OverflowException -> API 500.
--   (v_max_output_by_product khong dinh vi da co FLOOR() cat ve so nguyen.)
--
-- CACH SUA: CREATE OR REPLACE VIEW (khong doi ten/kieu cot -> hop le), boc ROUND(...)
--   quanh cac cot decimal lo ra ngoai de gioi han scale. Khong prefix schema (theo tenant).
--   V001 bat bien; day la file moi thay the hanh vi view.
-- =====================================================================================


-- Round effective_qty_per_unit (cot decimal duy nhat co scale lon lo ra o view nay).
-- max_output_for_material da la FLOOR() nen an toan; giu nguyen bieu thuc chia ben trong.
CREATE OR REPLACE VIEW v_max_output_by_material AS
SELECT
    b.product_id,
    b.id                                             AS bom_id,
    bi.material_id,
    bi.qty_per_unit,
    bi.waste_pct,
    ROUND(bi.qty_per_unit * (1 + bi.waste_pct/100), 6) AS effective_qty_per_unit,
    ms.total_available,
    FLOOR( ms.total_available / (bi.qty_per_unit * (1 + bi.waste_pct/100)) ) AS max_output_for_material
FROM bom b
JOIN bom_items bi        ON bi.bom_id = b.id
JOIN v_material_stock ms ON ms.material_id = bi.material_id
WHERE b.status = 'ACTIVE';


-- Round required_qty / shortage_qty / suggested_purchase_qty ve 4 chu so thap phan.
CREATE OR REPLACE VIEW v_order_material_requirement AS
SELECT
    po.id                                            AS production_order_id,
    po.order_no,
    po.status                                        AS order_status,
    bi.material_id,
    ROUND(po.quantity * bi.qty_per_unit * (1 + bi.waste_pct/100), 4) AS required_qty,
    ms.total_available,
    ROUND(GREATEST(0, (po.quantity * bi.qty_per_unit * (1 + bi.waste_pct/100)) - ms.total_available), 4) AS shortage_qty,
    CASE
        WHEN m.reorder_quantity > 0 THEN
            CEIL( GREATEST(0, (po.quantity * bi.qty_per_unit * (1 + bi.waste_pct/100)) - ms.total_available)
                  / m.reorder_quantity ) * m.reorder_quantity
        ELSE ROUND(GREATEST(0, (po.quantity * bi.qty_per_unit * (1 + bi.waste_pct/100)) - ms.total_available), 4)
    END                                              AS suggested_purchase_qty
FROM production_orders po
JOIN bom_items bi        ON bi.bom_id = po.bom_id
JOIN materials m         ON m.id = bi.material_id
JOIN v_material_stock ms ON ms.material_id = bi.material_id
WHERE po.status IN ('DRAFT','CONFIRMED','IN_PROGRESS');


-- =====================================================================================
-- HET V003__fix_report_view_scale.sql
-- =====================================================================================
