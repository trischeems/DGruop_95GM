-- =====================================================================================
-- App: ManagerPerfoment
-- File: V007__multi_product_order.sql
-- Muc dich: 1 DON SAN XUAT chua NHIEU MAT HANG, moi mat hang co SL rieng.
-- =====================================================================================
--
-- TRUOC DAY: production_orders = 1 ma hang + 1 so luong. Moi thu (BOM chot, giu cho NVL,
--   cong doan, nhap kho TP, hao hut, ke hoach) deu bam thang vao don.
--
-- BAY GIO: them bang production_order_items = DONG DON. Moi dong = 1 ma hang + SL +
--   BOM chot rieng + mau quy trinh rieng. Cac bang con gan them production_order_item_id
--   de tach so lieu theo tung dong (cong doan cua ao khac cong doan cua quan).
--
-- TUONG THICH NGUOC (quan trong): don cu duoc sinh dung 1 dong, moi ban ghi con duoc
--   tro ve dong do. Cac cot cu tren production_orders (product_id, bom_id, quantity)
--   GIU NGUYEN: product_id/bom_id = dong DAU TIEN (ma hang dai dien), quantity = TONG SL
--   cac dong — nho vay bao cao/man hinh cu khong vo trong khi chuyen doi.
-- =====================================================================================


-- -------------------------------------------------------------------------------------
-- 1. BANG DONG DON
-- -------------------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS production_order_items (
    id                  bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    production_order_id bigint    NOT NULL REFERENCES production_orders(id) ON DELETE CASCADE,
    line_no             integer   NOT NULL,                       -- so thu tu dong trong don (1..n)
    product_id          bigint    NOT NULL REFERENCES products(id),
    -- BOM chot cho RIENG dong nay (dong khac ma hang thi BOM khac).
    bom_id              bigint    REFERENCES bom(id),
    -- Mau quy trinh rieng cho dong nay (null = lay mau mac dinh khi khoi tao cong doan).
    routing_id          bigint    REFERENCES production_routings(id),
    quantity            numeric(18,4) NOT NULL CHECK (quantity > 0),
    note                text,
    created_at          timestamptz NOT NULL DEFAULT now(),
    updated_at          timestamptz NOT NULL DEFAULT now(),
    CONSTRAINT uq_poi_line UNIQUE (production_order_id, line_no)
);
COMMENT ON TABLE production_order_items IS
    'Dong don san xuat: 1 ma hang + so luong + BOM/quy trinh rieng. 1 don co nhieu dong.';
CREATE INDEX IF NOT EXISTS ix_poi_order   ON production_order_items (production_order_id);
CREATE INDEX IF NOT EXISTS ix_poi_product ON production_order_items (product_id);

-- Don cu -> sinh dung 1 dong (idempotent: chi sinh cho don chua co dong nao).
INSERT INTO production_order_items (production_order_id, line_no, product_id, bom_id, routing_id, quantity)
SELECT po.id, 1, po.product_id, po.bom_id, po.routing_id, po.quantity
FROM production_orders po
WHERE NOT EXISTS (SELECT 1 FROM production_order_items i WHERE i.production_order_id = po.id);


-- -------------------------------------------------------------------------------------
-- 2. GAN CAC BANG CON VE DONG DON
-- -------------------------------------------------------------------------------------
ALTER TABLE production_steps        ADD COLUMN IF NOT EXISTS production_order_item_id bigint REFERENCES production_order_items(id) ON DELETE CASCADE;
ALTER TABLE material_reservations   ADD COLUMN IF NOT EXISTS production_order_item_id bigint REFERENCES production_order_items(id) ON DELETE CASCADE;
ALTER TABLE production_plans        ADD COLUMN IF NOT EXISTS production_order_item_id bigint REFERENCES production_order_items(id) ON DELETE CASCADE;
ALTER TABLE finished_goods_receipts ADD COLUMN IF NOT EXISTS production_order_item_id bigint REFERENCES production_order_items(id);
ALTER TABLE loss_reports            ADD COLUMN IF NOT EXISTS production_order_item_id bigint REFERENCES production_order_items(id);
ALTER TABLE material_issue_items    ADD COLUMN IF NOT EXISTS production_order_item_id bigint REFERENCES production_order_items(id);

-- Backfill: du lieu cu deu thuoc dong DUY NHAT cua don.
UPDATE production_steps s
   SET production_order_item_id = i.id
  FROM production_order_items i
 WHERE i.production_order_id = s.production_order_id
   AND s.production_order_item_id IS NULL;

UPDATE material_reservations r
   SET production_order_item_id = i.id
  FROM production_order_items i
 WHERE i.production_order_id = r.production_order_id
   AND r.production_order_item_id IS NULL;

UPDATE production_plans p
   SET production_order_item_id = i.id
  FROM production_order_items i
 WHERE i.production_order_id = p.production_order_id
   AND p.production_order_item_id IS NULL;

UPDATE finished_goods_receipts f
   SET production_order_item_id = i.id
  FROM production_order_items i
 WHERE i.production_order_id = f.production_order_id
   AND f.production_order_item_id IS NULL;

UPDATE loss_reports l
   SET production_order_item_id = i.id
  FROM production_order_items i
 WHERE i.production_order_id = l.production_order_id
   AND l.production_order_item_id IS NULL;

UPDATE material_issue_items ii
   SET production_order_item_id = i.id
  FROM material_issues mi
  JOIN production_order_items i ON i.production_order_id = mi.production_order_id
 WHERE mi.id = ii.material_issue_id
   AND ii.production_order_item_id IS NULL;

-- Cac bang BAT BUOC phai thuoc 1 dong (sau khi da backfill xong).
ALTER TABLE production_steps        ALTER COLUMN production_order_item_id SET NOT NULL;
ALTER TABLE material_reservations   ALTER COLUMN production_order_item_id SET NOT NULL;
ALTER TABLE production_plans        ALTER COLUMN production_order_item_id SET NOT NULL;
ALTER TABLE finished_goods_receipts ALTER COLUMN production_order_item_id SET NOT NULL;
-- loss_reports / material_issue_items: de NULL duoc (du lieu sinh tu quy trinh cu van doc duoc).

CREATE INDEX IF NOT EXISTS ix_steps_item ON production_steps        (production_order_item_id);
CREATE INDEX IF NOT EXISTS ix_resv_item  ON material_reservations   (production_order_item_id);
CREATE INDEX IF NOT EXISTS ix_plans_item ON production_plans        (production_order_item_id);
CREATE INDEX IF NOT EXISTS ix_fg_item    ON finished_goods_receipts (production_order_item_id);
CREATE INDEX IF NOT EXISTS ix_loss_item  ON loss_reports            (production_order_item_id);
CREATE INDEX IF NOT EXISTS ix_mii_item   ON material_issue_items    (production_order_item_id);


-- -------------------------------------------------------------------------------------
-- 3. DOI RANG BUOC DUY NHAT: tu "theo DON" sang "theo DONG DON"
--    (2 dong khac nhau cua cung 1 don deu co cong doan May rieng, giu cho rieng...)
-- -------------------------------------------------------------------------------------
ALTER TABLE production_steps      DROP CONSTRAINT IF EXISTS uq_step_order_stage;
ALTER TABLE production_steps      ADD  CONSTRAINT uq_step_item_stage UNIQUE (production_order_item_id, stage_id);

ALTER TABLE material_reservations DROP CONSTRAINT IF EXISTS uq_reservation;
ALTER TABLE material_reservations ADD  CONSTRAINT uq_reservation_item UNIQUE (production_order_item_id, material_id, warehouse_id);

ALTER TABLE loss_reports          DROP CONSTRAINT IF EXISTS uq_loss_order_material;
ALTER TABLE loss_reports          ADD  CONSTRAINT uq_loss_item_material UNIQUE (production_order_item_id, material_id);

-- 1 phieu xuat co the cap NVL cho nhieu dong -> khoa duy nhat gom ca dong don.
ALTER TABLE material_issue_items  DROP CONSTRAINT IF EXISTS uq_issue_item;
ALTER TABLE material_issue_items  ADD  CONSTRAINT uq_issue_item_line UNIQUE (material_issue_id, production_order_item_id, material_id);


-- -------------------------------------------------------------------------------------
-- 4. GIU production_orders.quantity = TONG SL CAC DONG
--    (cot cu van dung cho man hinh/bao cao tong quan; dong bo bang trigger de khong
--     phu thuoc vao viec service co nho cap nhat hay khong).
-- -------------------------------------------------------------------------------------
CREATE OR REPLACE FUNCTION trg_sync_order_totals() RETURNS trigger AS $$
DECLARE
    v_order_id bigint := COALESCE(NEW.production_order_id, OLD.production_order_id);
BEGIN
    UPDATE production_orders po
       SET quantity   = COALESCE((SELECT SUM(quantity) FROM production_order_items WHERE production_order_id = v_order_id), po.quantity),
           product_id = COALESCE((SELECT product_id FROM production_order_items WHERE production_order_id = v_order_id ORDER BY line_no LIMIT 1), po.product_id),
           bom_id     =          (SELECT bom_id     FROM production_order_items WHERE production_order_id = v_order_id ORDER BY line_no LIMIT 1),
           updated_at = now()
     WHERE po.id = v_order_id;
    RETURN NULL;
END;
$$ LANGUAGE plpgsql;

DROP TRIGGER IF EXISTS trg_poi_sync_totals ON production_order_items;
CREATE TRIGGER trg_poi_sync_totals
    AFTER INSERT OR UPDATE OR DELETE ON production_order_items
    FOR EACH ROW EXECUTE FUNCTION trg_sync_order_totals();

-- updated_at tu cap nhat (dong bo voi cac bang khac o V001).
DROP TRIGGER IF EXISTS trg_poi_updated ON production_order_items;
CREATE TRIGGER trg_poi_updated
    BEFORE UPDATE ON production_order_items
    FOR EACH ROW EXECUTE FUNCTION set_updated_at();


-- -------------------------------------------------------------------------------------
-- 5. VIEW NHU CAU NVL: tinh theo TUNG DONG DON (truoc day theo don)
--    CREATE OR REPLACE chi cho phep THEM cot o CUOI -> giu nguyen thu tu cot cu.
-- -------------------------------------------------------------------------------------
CREATE OR REPLACE VIEW v_order_material_requirement AS
SELECT
    po.id                                            AS production_order_id,
    po.order_no,
    po.status                                        AS order_status,
    bi.material_id,
    ROUND(poi.quantity * bi.qty_per_unit * (1 + bi.waste_pct/100), 4) AS required_qty,
    ms.total_available,
    ROUND(GREATEST(0, (poi.quantity * bi.qty_per_unit * (1 + bi.waste_pct/100)) - ms.total_available), 4) AS shortage_qty,
    CASE
        WHEN m.reorder_quantity > 0 THEN
            CEIL( GREATEST(0, (poi.quantity * bi.qty_per_unit * (1 + bi.waste_pct/100)) - ms.total_available)
                  / m.reorder_quantity ) * m.reorder_quantity
        ELSE ROUND(GREATEST(0, (poi.quantity * bi.qty_per_unit * (1 + bi.waste_pct/100)) - ms.total_available), 4)
    END                                              AS suggested_purchase_qty,
    -- ----- cot MOI them o cuoi (khong pha consumer cu) -----
    poi.id                                           AS production_order_item_id,
    poi.line_no,
    poi.product_id,
    poi.quantity                                     AS line_quantity
FROM production_orders po
JOIN production_order_items poi ON poi.production_order_id = po.id
JOIN bom_items bi        ON bi.bom_id = poi.bom_id
JOIN materials m         ON m.id = bi.material_id
JOIN v_material_stock ms ON ms.material_id = bi.material_id
WHERE po.status IN ('DRAFT','CONFIRMED','IN_PROGRESS');

COMMENT ON VIEW v_order_material_requirement IS
    'Nhu cau NVL theo TUNG DONG cua don (BOM chot cua dong) vs ton kha dung => thieu hut & de xuat mua.';
