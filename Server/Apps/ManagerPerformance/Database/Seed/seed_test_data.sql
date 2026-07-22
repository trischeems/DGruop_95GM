-- =====================================================================================
-- seed_test_data.sql  —  Dữ liệu TEST cho tenant `public` (app Manager_Perfoment)
-- Sinh vài trăm dòng mỗi loại, tôn trọng mọi FK / CHECK / bất biến tồn kho.
-- Chạy lại được nhiều lần: xoá sạch dữ liệu giao dịch trước rồi nạp mới
-- (KHÔNG đụng vào reference đã seed: units_of_measure, material_categories,
--  warehouses, production_stages).
-- Thứ tự chèn theo phụ thuộc: users -> materials -> stock(+txn) -> products
--  -> bom(+items) -> production_orders -> plans/steps/reservations/issues
--  -> finished_goods -> loss_reports -> alerts.
-- =====================================================================================
-- Tenant dich: mac dinh 'public' neu chay truc tiep (khong truyen -v tenant=...).
\if :{?tenant}
\else
  \set tenant public
\endif
SET search_path TO :"tenant";
\echo Seeding tenant: :tenant
BEGIN;

-- ------------------------------------------------------------------------------------
-- 0) DỌN dữ liệu giao dịch cũ (GIỮ reference: warehouses, units, categories, stages).
--    Dùng DELETE theo thứ tự con->cha (KHÔNG dùng TRUNCATE CASCADE vì sẽ xoá luôn
--    warehouses do stock.warehouse_id tham chiếu). Sau đó reset sequence identity.
-- ------------------------------------------------------------------------------------
DELETE FROM alerts;
DELETE FROM loss_reports;
DELETE FROM finished_goods_receipts;
DELETE FROM material_issue_items;
DELETE FROM material_issues;
DELETE FROM material_reservations;
DELETE FROM production_steps;
DELETE FROM production_plans;
DELETE FROM production_orders;
DELETE FROM bom_approvals;
DELETE FROM bom_change_history;
DELETE FROM bom_items;
DELETE FROM bom;
DELETE FROM stock_transactions;
DELETE FROM stock;
DELETE FROM products;
DELETE FROM materials;
DELETE FROM users;

-- Reset identity của các bảng vừa dọn (để id bắt đầu lại từ 1 mỗi lần seed).
ALTER TABLE users                   ALTER COLUMN id RESTART WITH 1;
ALTER TABLE materials               ALTER COLUMN id RESTART WITH 1;
ALTER TABLE stock                   ALTER COLUMN id RESTART WITH 1;
ALTER TABLE stock_transactions      ALTER COLUMN id RESTART WITH 1;
ALTER TABLE products                ALTER COLUMN id RESTART WITH 1;
ALTER TABLE bom                     ALTER COLUMN id RESTART WITH 1;
ALTER TABLE bom_items               ALTER COLUMN id RESTART WITH 1;
ALTER TABLE bom_approvals           ALTER COLUMN id RESTART WITH 1;
ALTER TABLE bom_change_history      ALTER COLUMN id RESTART WITH 1;
ALTER TABLE production_orders       ALTER COLUMN id RESTART WITH 1;
ALTER TABLE production_plans        ALTER COLUMN id RESTART WITH 1;
ALTER TABLE production_steps        ALTER COLUMN id RESTART WITH 1;
ALTER TABLE material_reservations   ALTER COLUMN id RESTART WITH 1;
ALTER TABLE material_issues         ALTER COLUMN id RESTART WITH 1;
ALTER TABLE material_issue_items    ALTER COLUMN id RESTART WITH 1;
ALTER TABLE finished_goods_receipts ALTER COLUMN id RESTART WITH 1;
ALTER TABLE loss_reports            ALTER COLUMN id RESTART WITH 1;
ALTER TABLE alerts                  ALTER COLUMN id RESTART WITH 1;

-- ------------------------------------------------------------------------------------
-- 1) USERS  (~30 người: quản lý kho, kế hoạch, QC, mua hàng...)
-- ------------------------------------------------------------------------------------
INSERT INTO users (username, full_name, email, is_active)
SELECT
    'user' || lpad(g::text, 3, '0'),
    (ARRAY['Nguyễn','Trần','Lê','Phạm','Hoàng','Vũ','Đặng','Bùi','Đỗ','Hồ'])[1 + (g % 10)]
        || ' ' ||
    (ARRAY['Văn','Thị','Minh','Hữu','Ngọc','Quang','Thanh','Đức','Xuân','Gia'])[1 + ((g/10) % 10)]
        || ' ' ||
    (ARRAY['An','Bình','Cường','Dũng','Em','Phúc','Giang','Hà','Khôi','Linh',
           'Mai','Nam','Oanh','Phong','Quân','Sơn','Tú','Uyên','Vân','Yến'])[1 + (g % 20)],
    'user' || lpad(g::text, 3, '0') || '@dgroup.local',
    (g % 20 <> 0)                       -- ~5% inactive
FROM generate_series(1, 30) AS g;

-- ------------------------------------------------------------------------------------
-- 2) MATERIALS  (300 NVL, rải đều 6 nhóm, gán uom hợp lý theo nhóm)
--    reorder_level / reorder_quantity / standard_cost sinh đa dạng để test cảnh báo.
-- ------------------------------------------------------------------------------------
INSERT INTO materials (sku, name, category_id, uom_id, reorder_level, reorder_quantity, standard_cost, is_active, created_by)
SELECT
    'MAT-' || lpad(g::text, 4, '0'),
    (ARRAY['Vải kaki','Vải thun','Vải jean','Vải lụa','Vải nỉ','Vải cotton'])[1 + (g % 6)]
        || ' ' || (ARRAY['xanh','đỏ','đen','trắng','vàng','xám','be','nâu'])[1 + (g % 8)]
        || ' loại ' || (1 + (g % 5)),
    cat.category_id,
    cat.uom_id,
    (ARRAY[50,100,150,200,300,500,20,80])[1 + (g % 8)]::numeric,       -- reorder_level
    (ARRAY[100,200,250,500,1000,50,300,400])[1 + (g % 8)]::numeric,    -- reorder_quantity (lô)
    round((5 + (g % 47) * 3.5 + (g % 13) * 0.25)::numeric, 4),         -- standard_cost 5..~170
    (g % 25 <> 0),                                                     -- ~4% inactive
    1 + (g % 30)
FROM generate_series(1, 300) AS g
CROSS JOIN LATERAL (
    SELECT
        (1 + (g % 6))::bigint AS category_id,     -- categories 1..6
        CASE (g % 6)                              -- uom theo nhóm
            WHEN 0 THEN 1   -- Vải     -> Mét
            WHEN 1 THEN 6   -- Chỉ     -> Cuộn chỉ (CONE)
            WHEN 2 THEN 3   -- Cúc     -> Cái (PCS)
            WHEN 3 THEN 3   -- Khoá kéo-> Cái (PCS)
            WHEN 4 THEN 3   -- Nhãn    -> Cái (PCS)
            ELSE 3          -- Phụ liệu-> Cái (PCS)
        END::bigint AS uom_id
) cat;

-- ------------------------------------------------------------------------------------
-- 3) STOCK  (tồn cho ~85% NVL, ở kho NVL id=1; một số cố tình dưới ngưỡng => low stock)
--    qty_reserved sẽ được cập nhật sau ở bước giữ chỗ (reservations). Ở đây = 0.
--    Đồng thời ghi 1 stock_transactions RECEIPT khớp balance_after = qty_on_hand.
-- ------------------------------------------------------------------------------------
WITH picked AS (
    SELECT
        m.id AS material_id,
        1::bigint AS warehouse_id,
        -- Phân bố tồn để MỌI màn hình có dữ liệu đa dạng (không all-zero, không all-dư):
        --   ~15% NVL tồn THẤP (dưới ngưỡng) -> bật cảnh báo tồn thấp & thiếu hụt đơn.
        --   còn lại tồn DƯ DẢ và LỚN (chục nghìn) -> "sản lượng tối đa" ra số đẹp.
        CASE WHEN (m.id % 20) < 3
             THEN round((m.reorder_level * (0.1 + (m.id % 5) * 0.12))::numeric, 4)   -- < ngưỡng
             ELSE round((20000 + (m.id % 40) * 2500 + m.reorder_level * 10)::numeric, 4) -- dư nhiều
        END AS qty_on_hand
    FROM materials m
    WHERE m.is_active AND (m.id % 40 <> 7)   -- ~2.5% NVL không có dòng tồn (test tồn = 0)
)
, ins_stock AS (
    INSERT INTO stock (warehouse_id, material_id, qty_on_hand, qty_reserved)
    SELECT warehouse_id, material_id, qty_on_hand, 0 FROM picked
    RETURNING material_id, warehouse_id, qty_on_hand
)
INSERT INTO stock_transactions
    (warehouse_id, material_id, txn_type, quantity, unit_cost, balance_after, ref_type, ref_id, note, created_by)
SELECT
    s.warehouse_id, s.material_id, 'RECEIPT', s.qty_on_hand,
    m.standard_cost, s.qty_on_hand, 'MANUAL', NULL, 'Tồn đầu kỳ (seed)', 1 + (s.material_id % 30)
FROM ins_stock s JOIN materials m ON m.id = s.material_id
WHERE s.qty_on_hand > 0;

-- Thêm lịch sử giao dịch phong phú: mỗi NVL có tồn thêm 1-3 RECEIPT/ADJUST nữa
-- (chỉ để audit history dày; balance_after cộng dồn cho đúng thứ tự thời gian).
WITH base AS (
    SELECT st.material_id, st.warehouse_id, st.balance_after AS bal
    FROM stock_transactions st
    WHERE st.note = 'Tồn đầu kỳ (seed)'
)
INSERT INTO stock_transactions
    (warehouse_id, material_id, txn_type, quantity, unit_cost, balance_after, ref_type, ref_id, note, created_by, created_at)
SELECT
    b.warehouse_id, b.material_id,
    (ARRAY['RECEIPT','ADJUST','RECEIPT'])[gs] AS txn_type,
    d.q AS quantity,
    m.standard_cost,
    b.bal + d.running AS balance_after,
    'MANUAL', NULL,
    'Giao dịch bổ sung ' || gs, 1 + (b.material_id % 30),
    now() - ((4 - gs) || ' days')::interval
FROM base b
JOIN materials m ON m.id = b.material_id
CROSS JOIN LATERAL generate_series(1, 1 + (b.material_id % 3)) AS gs
CROSS JOIN LATERAL (
    SELECT
        (5 + (b.material_id % 7) * 4)::numeric AS q,
        (gs * (5 + (b.material_id % 7) * 4))::numeric AS running
) d
WHERE b.material_id % 3 = 0;   -- chỉ 1/3 NVL có history dày, tránh phình quá to

-- Đồng bộ lại qty_on_hand = balance_after mới nhất (do đã thêm giao dịch bổ sung).
UPDATE stock s SET qty_on_hand = t.bal
FROM (
    SELECT DISTINCT ON (material_id, warehouse_id) material_id, warehouse_id, balance_after AS bal
    FROM stock_transactions
    ORDER BY material_id, warehouse_id, created_at DESC, id DESC
) t
WHERE s.material_id = t.material_id AND s.warehouse_id = t.warehouse_id;

-- ------------------------------------------------------------------------------------
-- 4) PRODUCTS  (120 mã hàng thành phẩm; uom = Cái/Bộ/Đôi)
-- ------------------------------------------------------------------------------------
INSERT INTO products (sku, name, uom_id, is_active, created_by)
SELECT
    'SP-' || lpad(g::text, 4, '0'),
    (ARRAY['Áo sơ mi','Quần tây','Áo khoác','Váy','Quần jean','Áo thun','Đầm','Quần short'])[1 + (g % 8)]
        || ' mã ' || lpad(g::text, 4, '0'),
    (ARRAY[3,7,8])[1 + (g % 3)]::bigint,   -- PCS / SET / PAIR
    (g % 30 <> 0),
    1 + (g % 30)
FROM generate_series(1, 120) AS g;

-- ------------------------------------------------------------------------------------
-- 5) BOM  (1 BOM ACTIVE / mỗi product active; vài product có thêm bản ARCHIVED/DRAFT)
-- ------------------------------------------------------------------------------------
-- 5a. BOM ACTIVE cho mọi product active.
INSERT INTO bom (product_id, version, status, effective_from, note, created_by)
SELECT p.id, 1, 'ACTIVE', now() - interval '30 days', 'Định mức chuẩn', 1 + (p.id % 30)
FROM products p WHERE p.is_active;

-- 5b. Bản cũ ARCHIVED (version 0->dùng version cũ hơn: đặt version 1 đã dùng,
--     nên archived là version... phải khác) -> dùng version = 2 nhưng ARCHIVED cho ~1/4 product,
--     và 1 DRAFT version 3 cho ~1/6 product. (uq_bom_product_version giữ duy nhất).
INSERT INTO bom (product_id, version, status, effective_from, note, created_by)
SELECT p.id, 2, 'ARCHIVED', now() - interval '60 days', 'Phiên bản cũ', 1 + (p.id % 30)
FROM products p WHERE p.is_active AND (p.id % 4 = 0);

INSERT INTO bom (product_id, version, status, note, created_by)
SELECT p.id, 3, 'DRAFT', 'Bản nháp đang soạn', 1 + (p.id % 30)
FROM products p WHERE p.is_active AND (p.id % 6 = 0);

INSERT INTO bom (product_id, version, status, note, created_by)
SELECT p.id, 4, 'PENDING_APPROVAL', 'Chờ duyệt', 1 + (p.id % 30)
FROM products p WHERE p.is_active AND (p.id % 9 = 0);

-- ------------------------------------------------------------------------------------
-- 6) BOM_ITEMS  (mỗi BOM 3-6 NVL; qty_per_unit > 0, waste_pct 0..8%)
--    Dùng hash ổn định (material theo product_id + offset) để mỗi BOM khác nhau.
-- ------------------------------------------------------------------------------------
INSERT INTO bom_items (bom_id, material_id, qty_per_unit, waste_pct, note)
SELECT
    b.id,
    mat.material_id,
    round((0.2 + ((b.id * 7 + k * 13) % 25) * 0.15)::numeric, 6)   AS qty_per_unit,
    round((((b.id + k) % 9))::numeric, 4)                          AS waste_pct,   -- 0..8
    NULL
FROM bom b
CROSS JOIN LATERAL generate_series(0, 2 + (b.id % 4)) AS k   -- 3..6 item
CROSS JOIN LATERAL (
    SELECT m.id AS material_id
    FROM materials m
    WHERE m.is_active
    ORDER BY ((m.id + b.id * 31 + k * 101) % 300), m.id   -- chọn NVL "ngẫu nhiên ổn định"
    LIMIT 1
) mat
ON CONFLICT (bom_id, material_id) DO NOTHING;   -- tránh trùng NVL trong 1 BOM

-- ------------------------------------------------------------------------------------
-- 7) BOM change history + approvals cho các BOM PENDING_APPROVAL / ARCHIVED
-- ------------------------------------------------------------------------------------
INSERT INTO bom_approvals (bom_id, status, requested_by, requested_at, decided_by, decided_at, decision_note)
SELECT
    b.id,
    CASE WHEN b.status='PENDING_APPROVAL' THEN 'PENDING'
         WHEN b.status='ARCHIVED' THEN 'APPROVED'
         ELSE 'APPROVED' END,
    1 + (b.id % 30),
    now() - interval '20 days',
    CASE WHEN b.status='PENDING_APPROVAL' THEN NULL ELSE 1 + ((b.id+1) % 30) END,
    CASE WHEN b.status='PENDING_APPROVAL' THEN NULL ELSE now() - interval '18 days' END,
    CASE WHEN b.status='PENDING_APPROVAL' THEN NULL ELSE 'Đã duyệt' END
FROM bom b
WHERE b.status IN ('PENDING_APPROVAL','ARCHIVED');

INSERT INTO bom_change_history (bom_id, bom_item_id, material_id, change_type, old_value, new_value, reason, created_by)
SELECT bi.bom_id, bi.id, bi.material_id, 'UPDATE_QTY',
       bi.qty_per_unit, round(bi.qty_per_unit * 1.05, 6), 'Điều chỉnh định mức', 1 + (bi.id % 30)
FROM bom_items bi
WHERE bi.id % 7 = 0;

-- ------------------------------------------------------------------------------------
-- 8) PRODUCTION_ORDERS  (400 đơn; đủ trạng thái; đơn CONFIRMED+ chốt bom_id = BOM ACTIVE)
-- ------------------------------------------------------------------------------------
INSERT INTO production_orders
    (order_no, product_id, bom_id, quantity, status, due_date, confirmed_at, completed_at, note, created_by, created_at)
SELECT
    'PO-2026-' || lpad(g::text, 5, '0'),
    p.id,
    -- Đơn DRAFT chưa chốt bom (NULL); còn lại chốt BOM ACTIVE của product.
    CASE WHEN st.status = 'DRAFT' THEN NULL ELSE ab.bom_id END,
    (ARRAY[50,100,150,200,300,500,80,120,250,1000])[1 + (g % 10)]::numeric,
    st.status,
    (current_date + ((g % 60) - 20)),                    -- hạn: -20..+40 ngày
    CASE WHEN st.status = 'DRAFT' THEN NULL ELSE now() - ((g % 25) || ' days')::interval END,
    CASE WHEN st.status = 'COMPLETED' THEN now() - ((g % 10) || ' days')::interval END,
    'Đơn sản xuất test #' || g,
    1 + (g % 30),
    now() - ((g % 40) || ' days')::interval
FROM generate_series(1, 400) AS g
CROSS JOIN LATERAL (
    -- Phân bố trạng thái: nhiều CONFIRMED/IN_PROGRESS để test giữ chỗ & tiến trình.
    SELECT (ARRAY['DRAFT','CONFIRMED','CONFIRMED','IN_PROGRESS','IN_PROGRESS',
                  'IN_PROGRESS','COMPLETED','COMPLETED','CANCELLED','CONFIRMED'])[1 + (g % 10)] AS status
) st
CROSS JOIN LATERAL (
    -- product active ngẫu nhiên ổn định
    SELECT pr.id FROM products pr WHERE pr.is_active
    ORDER BY ((pr.id * 37 + g * 91) % 120), pr.id LIMIT 1
) p
LEFT JOIN LATERAL (
    SELECT b.id AS bom_id FROM bom b WHERE b.product_id = p.id AND b.status = 'ACTIVE' LIMIT 1
) ab ON true;

-- ------------------------------------------------------------------------------------
-- 9) PRODUCTION_PLANS  (mỗi đơn CONFIRMED/IN_PROGRESS/COMPLETED có 1-2 kế hoạch)
-- ------------------------------------------------------------------------------------
INSERT INTO production_plans
    (production_order_id, planned_qty, planned_start, planned_end, line_code, status, note, created_by)
SELECT
    po.id,
    round((po.quantity / k.parts)::numeric, 4),
    po.created_at::date + (k.idx * 2)::int,
    po.created_at::date + (k.idx * 2)::int + 5,
    'LINE-' || (1 + (po.id % 8)),
    CASE po.status
        WHEN 'CONFIRMED'   THEN 'PLANNED'
        WHEN 'IN_PROGRESS' THEN 'IN_PROGRESS'
        WHEN 'COMPLETED'   THEN 'DONE'
        ELSE 'PLANNED' END,
    'KH tổ ' || (1 + (po.id % 8)),
    1 + (po.id % 30)
FROM production_orders po
CROSS JOIN LATERAL (
    SELECT (2 - (po.id % 2))::int AS parts   -- 1 hoặc 2 phần
) pc
CROSS JOIN LATERAL generate_series(0, (2 - (po.id % 2)) - 1) AS gsidx(idx)
CROSS JOIN LATERAL (SELECT (2 - (po.id % 2))::numeric AS parts, gsidx.idx AS idx) k
WHERE po.status IN ('CONFIRMED','IN_PROGRESS','COMPLETED');

-- ------------------------------------------------------------------------------------
-- 10) PRODUCTION_STEPS  (state machine 4 công đoạn cho đơn IN_PROGRESS & COMPLETED)
--     IN_PROGRESS: CUTTING done, SEWING in-progress, QC/FG pending.
--     COMPLETED  : cả 4 công đoạn DONE, có qty_defect nhỏ ở QC.
-- ------------------------------------------------------------------------------------
INSERT INTO production_steps
    (production_order_id, stage_id, status, qty_in, qty_out, qty_defect, started_at, finished_at, note, updated_by)
SELECT
    po.id, s.id,
    CASE
        WHEN po.status = 'COMPLETED' THEN 'DONE'
        WHEN po.status = 'IN_PROGRESS' AND s.seq = 1 THEN 'DONE'
        WHEN po.status = 'IN_PROGRESS' AND s.seq = 2 THEN 'IN_PROGRESS'
        ELSE 'PENDING'
    END AS status,
    -- qty_in: các công đoạn đã chạy nhận full quantity; công đoạn pending = 0.
    CASE
        WHEN po.status = 'COMPLETED' THEN po.quantity
        WHEN po.status = 'IN_PROGRESS' AND s.seq = 1 THEN po.quantity
        WHEN po.status = 'IN_PROGRESS' AND s.seq = 2 THEN po.quantity
        ELSE 0
    END AS qty_in,
    -- qty_out: DONE => ra gần hết (trừ defect ở QC); IN_PROGRESS/PENDING => 0.
    CASE
        WHEN po.status = 'COMPLETED' AND s.seq = 3 THEN round(po.quantity * 0.97, 4)  -- QC loại 3%
        WHEN po.status = 'COMPLETED' THEN po.quantity
        WHEN po.status = 'IN_PROGRESS' AND s.seq = 1 THEN po.quantity
        ELSE 0
    END AS qty_out,
    CASE WHEN po.status = 'COMPLETED' AND s.seq = 3 THEN round(po.quantity * 0.03, 4) ELSE 0 END AS qty_defect,
    CASE WHEN (po.status='COMPLETED') OR (po.status='IN_PROGRESS' AND s.seq<=2)
         THEN po.created_at + (s.seq || ' days')::interval END,
    CASE WHEN po.status='COMPLETED' OR (po.status='IN_PROGRESS' AND s.seq=1)
         THEN po.created_at + (s.seq || ' days')::interval + interval '20 hours' END,
    NULL,
    1 + (po.id % 30)
FROM production_orders po
JOIN production_stages s ON true
WHERE po.status IN ('IN_PROGRESS','COMPLETED');

-- ------------------------------------------------------------------------------------
-- 11) MATERIAL_RESERVATIONS  (giữ chỗ cho đơn CONFIRMED & IN_PROGRESS)
--     Nhu cầu = po.quantity * qty_per_unit * (1+waste/100), nhưng KHÔNG được vượt
--     tồn khả dụng còn lại của NVL (ck_stock_reserved_le_onhand). Ta cấp phần
--     giữ chỗ = min(nhu cầu, phần on_hand còn trống). Cập nhật stock.qty_reserved.
-- ------------------------------------------------------------------------------------
-- Tính nhu cầu thô theo (đơn, NVL) rồi cộng dồn theo NVL, cắt theo tồn.
WITH demand AS (
    SELECT
        po.id AS production_order_id,
        bi.material_id,
        1::bigint AS warehouse_id,
        (po.quantity * bi.qty_per_unit * (1 + bi.waste_pct/100.0))::numeric(18,4) AS need
    FROM production_orders po
    JOIN bom_items bi ON bi.bom_id = po.bom_id
    WHERE po.status IN ('CONFIRMED','IN_PROGRESS')
),
-- Cộng dồn nhu cầu theo NVL, giữ thứ tự đơn để cấp lần lượt tới khi cạn tồn trống.
ranked AS (
    SELECT d.*,
           SUM(d.need) OVER (PARTITION BY d.material_id ORDER BY d.production_order_id
                             ROWS BETWEEN UNBOUNDED PRECEDING AND CURRENT ROW) AS cum_need
    FROM demand d
),
capped AS (
    SELECT r.*,
           s.qty_on_hand,
           -- phần giữ chỗ thực tế cho dòng này = clamp(cum<=onhand) - clamp(prev_cum)
           GREATEST(0,
               LEAST(r.cum_need, s.qty_on_hand)
               - LEAST(r.cum_need - r.need, s.qty_on_hand)
           )::numeric(18,4) AS grant_qty
    FROM ranked r
    JOIN stock s ON s.material_id = r.material_id AND s.warehouse_id = r.warehouse_id
),
ins AS (
    INSERT INTO material_reservations
        (production_order_id, material_id, warehouse_id, qty_reserved, status, created_by)
    SELECT production_order_id, material_id, warehouse_id, grant_qty, 'ACTIVE', 1 + (production_order_id % 30)
    FROM capped
    WHERE grant_qty > 0
    ON CONFLICT (production_order_id, material_id, warehouse_id) DO NOTHING
    RETURNING material_id, warehouse_id, qty_reserved
)
UPDATE stock s
SET qty_reserved = s.qty_reserved + agg.total
FROM (
    SELECT material_id, warehouse_id, SUM(qty_reserved) AS total
    FROM ins GROUP BY material_id, warehouse_id
) agg
WHERE s.material_id = agg.material_id AND s.warehouse_id = agg.warehouse_id;

-- ------------------------------------------------------------------------------------
-- 12) MATERIAL_ISSUES + items  (xuất NVL cho đơn IN_PROGRESS & COMPLETED)
--     Trừ stock.qty_on_hand và giải phóng reserved tương ứng (mô phỏng cấp phát).
--     Chỉ xuất phần <= tồn hiện có để không vi phạm CHECK qty_on_hand >= 0.
-- ------------------------------------------------------------------------------------
-- 12a. Header phiếu xuất: 1 phiếu / đơn (IN_PROGRESS, COMPLETED).
INSERT INTO material_issues (issue_no, production_order_id, warehouse_id, status, issued_at, note, created_by)
SELECT
    'ISS-2026-' || lpad(po.id::text, 5, '0'),
    po.id, 1, 'POSTED',
    COALESCE(po.confirmed_at, po.created_at) + interval '2 days',
    'Xuất NVL cho ' || po.order_no, 1 + (po.id % 30)
FROM production_orders po
WHERE po.status IN ('IN_PROGRESS','COMPLETED');

-- 12b. Chi tiết xuất: cho từng NVL trong BOM, xuất ~ nhu cầu nhưng cắt theo tồn.
--      Ghi song song: material_issue_items + trừ stock + stock_transactions(ISSUE).
WITH lines AS (
    SELECT
        mi.id AS issue_id,
        po.id AS po_id,
        bi.material_id,
        mi.warehouse_id,
        LEAST(
            (po.quantity * bi.qty_per_unit * (1 + bi.waste_pct/100.0))::numeric(18,4),
            round(s.qty_on_hand * 0.6, 4)      -- xuất tối đa 60% tồn, giữ NVL luôn dương
        )::numeric(18,4) AS qty_issue,
        m.standard_cost AS unit_cost
    FROM material_issues mi
    JOIN production_orders po ON po.id = mi.production_order_id
    JOIN bom_items bi ON bi.bom_id = po.bom_id
    JOIN materials m ON m.id = bi.material_id
    JOIN stock s ON s.material_id = bi.material_id AND s.warehouse_id = mi.warehouse_id
),
-- Cộng dồn theo NVL để không xuất quá tồn khi nhiều đơn cùng dùng 1 NVL.
ranked AS (
    SELECT l.*,
           SUM(l.qty_issue) OVER (PARTITION BY l.material_id, l.warehouse_id ORDER BY l.issue_id
                                  ROWS BETWEEN UNBOUNDED PRECEDING AND CURRENT ROW) AS cum
    FROM lines l
),
capped AS (
    -- Trần cấp phát TÍCH LŨY = 60% tồn seeded => mỗi NVL luôn giữ >=40% tồn (dương,
    -- và "sản lượng tối đa" vẫn ra số đẹp sau khi xuất).
    SELECT r.*,
           s.qty_on_hand,
           round(s.qty_on_hand * 0.6, 4) AS cap_qty,
           GREATEST(0,
               LEAST(r.cum, round(s.qty_on_hand * 0.6, 4))
               - LEAST(r.cum - r.qty_issue, round(s.qty_on_hand * 0.6, 4))
           )::numeric(18,4) AS real_issue
    FROM ranked r
    JOIN stock s ON s.material_id = r.material_id AND s.warehouse_id = r.warehouse_id
),
good AS (SELECT * FROM capped WHERE real_issue > 0),
ins_items AS (
    INSERT INTO material_issue_items (material_issue_id, material_id, qty_issued, unit_cost)
    SELECT issue_id, material_id, real_issue, unit_cost FROM good
    ON CONFLICT (material_issue_id, material_id) DO NOTHING
    RETURNING material_issue_id, material_id, qty_issued
)
-- Trừ tồn + giảm reserved (đối ứng phần đã giữ chỗ) cho các NVL vừa xuất.
, agg AS (
    SELECT material_id, SUM(qty_issued) AS total
    FROM ins_items GROUP BY material_id
)
UPDATE stock s
SET qty_on_hand  = s.qty_on_hand - agg.total,
    qty_reserved = GREATEST(0, s.qty_reserved - agg.total)
FROM agg
WHERE s.material_id = agg.material_id AND s.warehouse_id = 1;

-- 12c. Ghi stock_transactions ISSUE (âm) cho mỗi dòng xuất, balance_after = tồn sau.
INSERT INTO stock_transactions
    (warehouse_id, material_id, txn_type, quantity, unit_cost, balance_after, ref_type, ref_id, note, created_by, created_at)
SELECT
    mi.warehouse_id, ii.material_id, 'ISSUE',
    -ii.qty_issued,               -- âm: xuất
    ii.unit_cost,
    s.qty_on_hand,                -- tồn hiện tại sau khi đã trừ ở 12b
    'MATERIAL_ISSUE', ii.material_issue_id,
    'Xuất SX', 1 + (ii.material_id % 30),
    now() - interval '3 days'
FROM material_issue_items ii
JOIN material_issues mi ON mi.id = ii.material_issue_id
JOIN stock s ON s.material_id = ii.material_id AND s.warehouse_id = mi.warehouse_id;

-- 12d. ĐÃ XUẤT cho đơn IN_PROGRESS => giữ chỗ của các đơn đó chuyển sang CONSUMED
--      (NVL đã cấp phát cho sản xuất, không còn "giữ chỗ" nữa).
UPDATE material_reservations r
SET status = 'CONSUMED'
FROM production_orders po
WHERE r.production_order_id = po.id AND po.status = 'IN_PROGRESS' AND r.status = 'ACTIVE';

-- 12e. ĐỒNG BỘ LẠI stock.qty_reserved = tổng giữ chỗ ACTIVE còn lại (chỉ đơn CONFIRMED),
--      nhưng KẸP <= qty_on_hand ngay trong câu lệnh để không vi phạm
--      ck_stock_reserved_le_onhand (một số NVL đã bị xuất bớt tồn ở 12b).
UPDATE stock s
SET qty_reserved = LEAST(COALESCE(a.total, 0), s.qty_on_hand)
FROM (
    SELECT s2.id,
           (SELECT COALESCE(SUM(r.qty_reserved),0)
            FROM material_reservations r
            WHERE r.material_id = s2.material_id AND r.warehouse_id = s2.warehouse_id
              AND r.status = 'ACTIVE') AS total
    FROM stock s2
) a
WHERE s.id = a.id;

-- ------------------------------------------------------------------------------------
-- 13) FINISHED_GOODS_RECEIPTS  (nhập kho TP cho đơn COMPLETED; kho TP id=2)
-- ------------------------------------------------------------------------------------
INSERT INTO finished_goods_receipts
    (receipt_no, production_order_id, product_id, warehouse_id, qty_received, received_at, note, created_by)
SELECT
    'FG-2026-' || lpad(po.id::text, 5, '0'),
    po.id, po.product_id, 2,
    round(po.quantity * 0.97, 4),   -- sản lượng đạt (khớp QC ở bước 10)
    COALESCE(po.completed_at, now()),
    'Nhập kho TP ' || po.order_no,
    1 + (po.id % 30)
FROM production_orders po
WHERE po.status = 'COMPLETED';

-- ------------------------------------------------------------------------------------
-- 14) LOSS_REPORTS  (đối chiếu cấp phát vs định mức cho đơn COMPLETED)
-- ------------------------------------------------------------------------------------
INSERT INTO loss_reports
    (production_order_id, material_id, qty_issued, qty_standard, finished_qty, reported_at, created_by)
SELECT
    po.id,
    bi.material_id,
    COALESCE(iss.total_issued, 0)                                             AS qty_issued,
    round(fg.qty_received * bi.qty_per_unit, 4)                               AS qty_standard,
    fg.qty_received                                                           AS finished_qty,
    fg.received_at,
    1 + (po.id % 30)
FROM production_orders po
JOIN finished_goods_receipts fg ON fg.production_order_id = po.id
JOIN bom_items bi ON bi.bom_id = po.bom_id
LEFT JOIN LATERAL (
    SELECT SUM(mii.qty_issued) AS total_issued
    FROM material_issues mi
    JOIN material_issue_items mii ON mii.material_issue_id = mi.id
    WHERE mi.production_order_id = po.id AND mii.material_id = bi.material_id
) iss ON true
WHERE po.status = 'COMPLETED'
ON CONFLICT (production_order_id, material_id) DO NOTHING;

-- ------------------------------------------------------------------------------------
-- 15) ALERTS  (LOW_STOCK theo tồn thực; ORDER_SHORTAGE cho đơn thiếu; BOM_PENDING)
-- ------------------------------------------------------------------------------------
-- 15a. LOW_STOCK: NVL có tồn khả dụng < reorder_level.
INSERT INTO alerts (alert_type, severity, entity_type, entity_id, message, status, created_at)
SELECT
    'LOW_STOCK',
    CASE WHEN vms.total_available <= 0 THEN 'CRITICAL' ELSE 'WARN' END,
    'MATERIAL', vms.material_id,
    'Tồn thấp: ' || vms.sku || ' (' || vms.name || ') còn ' || vms.total_available
        || ' < ngưỡng ' || vms.reorder_level,
    (ARRAY['OPEN','OPEN','OPEN','ACKNOWLEDGED','RESOLVED'])[1 + (vms.material_id % 5)],
    now() - ((vms.material_id % 15) || ' days')::interval
FROM v_material_stock vms
WHERE vms.is_low_stock;

-- 15b. ORDER_SHORTAGE: đơn có shortage_qty > 0 (theo view nhu cầu NVL).
INSERT INTO alerts (alert_type, severity, entity_type, entity_id, message, status, created_at)
SELECT DISTINCT ON (r.production_order_id)
    'ORDER_SHORTAGE', 'CRITICAL', 'PRODUCTION_ORDER', r.production_order_id,
    'Đơn ' || r.order_no || ' thiếu NVL (thiếu ' || round(r.shortage_qty,2) || ')',
    'OPEN',
    now() - ((r.production_order_id % 10) || ' days')::interval
FROM v_order_material_requirement r
WHERE r.shortage_qty > 0
ORDER BY r.production_order_id, r.shortage_qty DESC;

-- 15c. BOM_PENDING: BOM đang chờ duyệt.
INSERT INTO alerts (alert_type, severity, entity_type, entity_id, message, status, created_at)
SELECT 'BOM_PENDING', 'INFO', 'BOM', b.id,
       'BOM #' || b.id || ' (SP ' || p.sku || ') đang chờ duyệt', 'OPEN',
       now() - interval '5 days'
FROM bom b JOIN products p ON p.id = b.product_id
WHERE b.status = 'PENDING_APPROVAL';

COMMIT;

-- =====================================================================================
-- BÁO CÁO TÓM TẮT SAU SEED
-- =====================================================================================
SELECT 'users' t, count(*) n FROM users
UNION ALL SELECT 'materials', count(*) FROM materials
UNION ALL SELECT 'stock', count(*) FROM stock
UNION ALL SELECT 'stock_transactions', count(*) FROM stock_transactions
UNION ALL SELECT 'products', count(*) FROM products
UNION ALL SELECT 'bom', count(*) FROM bom
UNION ALL SELECT 'bom_items', count(*) FROM bom_items
UNION ALL SELECT 'bom_approvals', count(*) FROM bom_approvals
UNION ALL SELECT 'bom_change_history', count(*) FROM bom_change_history
UNION ALL SELECT 'production_orders', count(*) FROM production_orders
UNION ALL SELECT 'production_plans', count(*) FROM production_plans
UNION ALL SELECT 'production_steps', count(*) FROM production_steps
UNION ALL SELECT 'material_reservations', count(*) FROM material_reservations
UNION ALL SELECT 'material_issues', count(*) FROM material_issues
UNION ALL SELECT 'material_issue_items', count(*) FROM material_issue_items
UNION ALL SELECT 'finished_goods_receipts', count(*) FROM finished_goods_receipts
UNION ALL SELECT 'loss_reports', count(*) FROM loss_reports
UNION ALL SELECT 'alerts', count(*) FROM alerts
ORDER BY t;
