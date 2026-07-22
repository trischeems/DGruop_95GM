-- =====================================================================================
-- Seed dữ liệu TEST BIỂU ĐỒ THEO THÁNG cho tenant "public" (app Manager_Perfoment)
--
-- YÊU CẦU:
--   - XÓA TOÀN BỘ dữ liệu cũ (cả danh mục nền + demo) rồi seed lại từ đầu.
--   - Đơn sản xuất phân bổ THEO THÁNG, từ tháng 1 đến hiện tại (tháng 7/2026),
--     mỗi tháng < 10k, TĂNG DẦN theo tháng (xu hướng tăng trưởng) + nhiễu ngẫu nhiên.
--   - created_at của đơn rải đều trong tháng tương ứng => biểu đồ "đơn theo tháng" đúng.
--   - Số LẺ ngẫu nhiên (tiền, số lượng, định mức...). Tôn trọng mọi FK/CHECK/UNIQUE.
--
-- AN TOÀN:
--   - 1 transaction; setseed cố định => tái lập được.
--   - TRUNCATE ... RESTART IDENTITY CASCADE: sạch hoàn toàn, id đánh lại từ 1.
--   - Mọi cột numeric round() đúng scale (tránh overflow khi Npgsql -> decimal).
-- =====================================================================================
SET search_path TO public;
SELECT setseed(0.2026);

BEGIN;

-- -------------------------------------------------------------------------------------
-- 0. XÓA TOÀN BỘ. RESTART IDENTITY để id về 1 (khớp mặc định uomId=1/warehouseId=1 app).
--    production_stages cũng xóa rồi seed lại (giữ đúng seq/code như V001).
-- -------------------------------------------------------------------------------------
TRUNCATE
    loss_reports, finished_goods_receipts, material_issue_items, material_issues,
    material_reservations, production_steps, production_plans, production_orders,
    bom_approvals, bom_change_history, bom_items, bom,
    stock_transactions, stock, alerts,
    products, materials, warehouses, material_categories, units_of_measure,
    production_stages, users
    RESTART IDENTITY CASCADE;

-- -------------------------------------------------------------------------------------
-- 1. DANH MỤC NỀN (seed lại giống V001/V002 — id bắt đầu từ 1).
-- -------------------------------------------------------------------------------------
INSERT INTO units_of_measure (code, name) VALUES
    ('M','Mét'),('KG','Kilogram'),('PCS','Cái'),('ROLL','Cuộn'),('YARD','Yard'),
    ('CONE','Cuộn chỉ'),('SET','Bộ'),('PAIR','Đôi'),('BOX','Hộp'),('G','Gram');

INSERT INTO material_categories (code, name) VALUES
    ('VAI','Vải'),('CHI','Chỉ may'),('CUC','Cúc / Nút'),
    ('KHOAKEO','Khoá kéo'),('NHAN','Nhãn mác'),('PHULIEU','Phụ liệu khác');

INSERT INTO warehouses (code, name) VALUES
    ('WH-NVL','Kho nguyên vật liệu'),('WH-TP','Kho thành phẩm');

INSERT INTO production_stages (code, name, seq) VALUES
    ('CUTTING','Cắt vải',1),('SEWING','May',2),
    ('QC','Kiểm tra chất lượng',3),('FG_RECEIPT','Nhập kho thành phẩm',4);

-- -------------------------------------------------------------------------------------
-- 2. USERS (200) + hàm chọn user ngẫu nhiên (id thật, không hard-code).
-- -------------------------------------------------------------------------------------
INSERT INTO users (username, full_name, email)
SELECT 'pt_user' || g, 'Nhân viên ' || g, 'pt_user' || g || '@dgroup.test'
FROM generate_series(1, 200) g;

SELECT set_config('pt.user_min', (SELECT min(id)::text FROM users), false);
CREATE OR REPLACE FUNCTION pt_rand_user() RETURNS bigint AS $fn$
    SELECT current_setting('pt.user_min')::bigint + floor(random() * 200)::bigint;
$fn$ LANGUAGE sql VOLATILE;

-- -------------------------------------------------------------------------------------
-- 3. MATERIALS (20000) — nhóm/uom/tiền/ngưỡng LẺ ngẫu nhiên.
-- -------------------------------------------------------------------------------------
INSERT INTO materials (sku, name, category_id, uom_id, reorder_level, reorder_quantity, standard_cost, is_active, created_by)
SELECT
    'PT-MAT-' || lpad(g::text, 6, '0'),
    (ARRAY['Vải cotton','Vải kate','Vải kaki','Chỉ polyester','Chỉ cotton','Cúc nhựa','Cúc kim loại',
           'Khoá kéo','Nhãn dệt','Nhãn giấy','Dây thun','Mex dựng','Ren','Bo cổ'])[1 + floor(random()*14)::int] || ' #' || g,
    1 + floor(random()*6)::int,
    1 + floor(random()*10)::int,
    round((20 + random()*480)::numeric, 3),
    round((50 + random()*950)::numeric, 3),
    round((3 + random()*997)::numeric, 4),
    (random() > 0.03),
    pt_rand_user()
FROM generate_series(1, 20000) g;

-- -------------------------------------------------------------------------------------
-- 4. STOCK — kho NVL(1) mỗi material; ~35% có thêm kho TP(2). reserved<=on_hand. ~18% tồn thấp.
-- -------------------------------------------------------------------------------------
INSERT INTO stock (warehouse_id, material_id, qty_on_hand, qty_reserved)
SELECT 1, m.id, oh, round((oh * random() * 0.6)::numeric, 4)
FROM materials m
CROSS JOIN LATERAL (
    SELECT round(CASE WHEN random() < 0.18 THEN (random()*40)::numeric
                      ELSE (200 + random()*4800)::numeric END, 4) AS oh
) s;

INSERT INTO stock (warehouse_id, material_id, qty_on_hand, qty_reserved)
SELECT 2, m.id, round((random()*300)::numeric, 4), 0
FROM materials m WHERE random() < 0.35;

-- -------------------------------------------------------------------------------------
-- 5. STOCK_TRANSACTIONS — 1-4 giao dịch/material, created_at rải 7 tháng (biểu đồ nhập kho).
-- -------------------------------------------------------------------------------------
INSERT INTO stock_transactions (warehouse_id, material_id, txn_type, quantity, unit_cost, balance_after, ref_type, note, created_at, created_by)
SELECT
    1, m.id,
    (ARRAY['RECEIPT','RECEIPT','RECEIPT','ADJUST'])[1 + floor(random()*4)::int],
    round((10 + random()*490)::numeric, 4),
    round((3 + random()*497)::numeric, 4),
    round((50 + random()*4950)::numeric, 4),
    'RECEIPT', 'Giao dịch kho test',
    -- rải từ 2026-01-01 đến hôm nay (2026-07-02)
    (DATE '2026-01-01' + (random() * (CURRENT_DATE - DATE '2026-01-01'))::int)::timestamptz
        + (random() * interval '1 day'),
    pt_rand_user()
FROM materials m
CROSS JOIN LATERAL generate_series(1, 1 + floor(random()*3)::int) k;

-- -------------------------------------------------------------------------------------
-- 6. PRODUCTS (5000) + BOM ACTIVE mỗi product + BOM_ITEMS 3-9 NVL.
-- -------------------------------------------------------------------------------------
INSERT INTO products (sku, name, uom_id, is_active, created_by)
SELECT
    'PT-SP-' || lpad(g::text, 6, '0'),
    (ARRAY['Áo sơ mi','Quần tây','Áo khoác','Váy','Áo thun','Quần jean','Áo len','Đầm','Quần short','Áo vest'])[1 + floor(random()*10)::int] || ' mã ' || g,
    3, (random() > 0.02), pt_rand_user()
FROM generate_series(1, 5000) g;

INSERT INTO bom (product_id, version, status, effective_from, note, created_by)
SELECT p.id, 1, 'ACTIVE',
       (DATE '2026-01-01' + (random()*150)::int)::timestamptz,
       'BOM test cho ' || p.sku, pt_rand_user()
FROM products p;

DO $$
DECLARE v_mat_min bigint; v_mat_cnt bigint := 20000;
BEGIN
    SELECT min(id) INTO v_mat_min FROM materials;
    INSERT INTO bom_items (bom_id, material_id, qty_per_unit, waste_pct, note)
    SELECT b.id,
           v_mat_min + floor(random() * v_mat_cnt)::bigint,
           round((0.05 + random()*7.95)::numeric, 6),
           round((random()*15)::numeric, 4),
           NULL
    FROM bom b
    CROSS JOIN LATERAL generate_series(1, 3 + floor(random()*7)::int) i
    ON CONFLICT (bom_id, material_id) DO NOTHING;
END $$;

-- -------------------------------------------------------------------------------------
-- 7. PRODUCTION_ORDERS — PHÂN BỔ THEO THÁNG (1..7/2026), TĂNG DẦN + nhiễu, < 10k/tháng.
--    Số đơn mỗi tháng: base tăng theo tháng (1000 + month*1150) + nhiễu ±15%, kẹp < 9800.
--    created_at rải đều trong tháng (tháng 7 chỉ tới hôm nay).
--    Trạng thái: đơn tháng CŨ nghiêng về COMPLETED; đơn tháng GẦN nghiêng DRAFT/CONFIRMED.
-- -------------------------------------------------------------------------------------
DO $$
DECLARE
    v_prod_min bigint;
    v_prod_cnt bigint := 5000;
    v_month    int;
    v_cnt      int;
    v_seq      int := 0;      -- số thứ tự đơn toàn cục (đảm bảo order_no duy nhất)
    v_mstart   date;
    v_mend     date;         -- ngày cuối kỳ của tháng (tháng 7 = hôm nay)
BEGIN
    SELECT min(id) INTO v_prod_min FROM products;

    FOR v_month IN 1..7 LOOP
        -- Số đơn tháng này: tăng dần theo tháng + nhiễu ngẫu nhiên, luôn < 9800.
        v_cnt := LEAST(9800, GREATEST(300,
                    (900 + v_month * 1150 + (random()*2000 - 1000))::int));

        v_mstart := make_date(2026, v_month, 1);
        v_mend   := LEAST(CURRENT_DATE, (v_mstart + interval '1 month' - interval '1 day')::date);
        -- Nếu tháng chưa tới (không xảy ra vì tới tháng 7) thì bỏ qua.
        CONTINUE WHEN v_mstart > CURRENT_DATE;

        INSERT INTO production_orders
            (order_no, product_id, bom_id, quantity, status, due_date, confirmed_at, completed_at, note, created_at, created_by)
        SELECT
            'PT-PO-' || lpad((v_seq + n)::text, 7, '0'),
            pick.pid, pick.bid,
            round((20 + random()*4980)::numeric, 3),
            st.status,
            (cdate::date + (5 + floor(random()*40))::int),                 -- due sau created vài chục ngày
            CASE WHEN st.status IN ('CONFIRMED','IN_PROGRESS','COMPLETED')
                 THEN cdate + (random()*2 * interval '1 day') END,          -- xác nhận ngay sau tạo
            CASE WHEN st.status = 'COMPLETED'
                 THEN cdate + ((3 + random()*20) * interval '1 day') END,   -- hoàn thành vài tuần sau
            'Đơn T' || v_month || ' #' || n,
            cdate,
            pt_rand_user()
        FROM generate_series(1, v_cnt) n
        -- ngày tạo rải đều trong kỳ của tháng (giờ ngẫu nhiên)
        CROSS JOIN LATERAL (
            SELECT (v_mstart + (random() * (v_mend - v_mstart))::int)::timestamptz
                   + (random() * interval '1 day') AS cdate
        ) d
        CROSS JOIN LATERAL (
            SELECT v_prod_min + ((n::bigint * 2654435761) % v_prod_cnt) AS pid_calc
        ) k
        CROSS JOIN LATERAL (
            SELECT k.pid_calc AS pid,
                   (SELECT bo.id FROM bom bo WHERE bo.product_id = k.pid_calc AND bo.status='ACTIVE' LIMIT 1) AS bid
        ) pick
        CROSS JOIN LATERAL (
            -- Phân bố trạng thái LỆCH theo tháng: tháng cũ nhiều COMPLETED, tháng gần nhiều DRAFT.
            -- prog = mức độ "đã cũ" (tháng 1 -> ~1.0, tháng 7 -> ~0.14)
            SELECT CASE
                WHEN r < 0.05 THEN 'CANCELLED'
                WHEN r < 0.05 + 0.85*prog          THEN 'COMPLETED'
                WHEN r < 0.05 + 0.85*prog + 0.10   THEN 'IN_PROGRESS'
                WHEN r < 0.05 + 0.85*prog + 0.20   THEN 'CONFIRMED'
                ELSE 'DRAFT'
            END AS status
            FROM (SELECT random() + (n*0) AS r, (8 - v_month)::numeric/7 AS prog) rr
        ) st;

        v_seq := v_seq + v_cnt;
        RAISE NOTICE 'Tháng %: % đơn (created % .. %)', v_month, v_cnt, v_mstart, v_mend;
    END LOOP;
END $$;

-- -------------------------------------------------------------------------------------
-- 8. PRODUCTION_PLANS — cho đơn CONFIRMED/IN_PROGRESS/COMPLETED, ngày bám created_at đơn.
-- -------------------------------------------------------------------------------------
INSERT INTO production_plans (production_order_id, planned_qty, planned_start, planned_end, line_code, status, note, created_by)
SELECT
    po.id,
    round((po.quantity / seg.parts)::numeric, 4),
    dt.start_d,
    (dt.start_d + (2 + floor(random()*12))::int)::date,   -- end >= start (delta >= 2 ngày)
    'LINE-' || (1 + floor(random()*12)::int),
    CASE po.status WHEN 'COMPLETED' THEN 'DONE'
                   WHEN 'IN_PROGRESS' THEN (ARRAY['IN_PROGRESS','RELEASED'])[1 + floor(random()*2)::int]
                   ELSE (ARRAY['PLANNED','RELEASED'])[1 + floor(random()*2)::int] END,
    'Kế hoạch test', pt_rand_user()
FROM production_orders po
CROSS JOIN LATERAL (SELECT (1 + floor(random()*2)::int) AS parts) seg
CROSS JOIN LATERAL generate_series(1, seg.parts) AS segn
CROSS JOIN LATERAL (SELECT (po.created_at::date + floor(random()*5)::int)::date AS start_d) dt
WHERE po.status IN ('CONFIRMED','IN_PROGRESS','COMPLETED');

-- -------------------------------------------------------------------------------------
-- 9. PRODUCTION_STEPS — đơn IN_PROGRESS & COMPLETED. out+defect<=in tuyệt đối; PENDING=0.
--    started/finished bám created_at đơn.
-- -------------------------------------------------------------------------------------
INSERT INTO production_steps (production_order_id, stage_id, status, qty_in, qty_out, qty_defect, started_at, finished_at, updated_by)
SELECT
    po.id, s.stage_id, s.step_status, s.qty_in, s.qty_out, s.qty_defect,
    CASE WHEN s.step_status <> 'PENDING' THEN po.created_at + (random()*3 * interval '1 day') END,
    CASE WHEN s.step_status = 'DONE'     THEN po.created_at + ((4 + random()*20) * interval '1 day') END,
    pt_rand_user()
FROM production_orders po
CROSS JOIN LATERAL (
    SELECT
        stage_id, step_status,
        CASE WHEN step_status='PENDING' THEN 0 ELSE q_in END AS qty_in,
        CASE WHEN step_status='PENDING' THEN 0
             ELSE LEAST(round((qin * pass_rate)::numeric, 4), q_in - q_def) END AS qty_out,
        CASE WHEN step_status='PENDING' THEN 0 ELSE q_def END AS qty_defect
    FROM (
        SELECT v.stage_id, v.step_status,
            po.quantity * v.in_factor AS qin,
            round((po.quantity * v.in_factor)::numeric, 4) AS q_in,
            round((po.quantity * v.in_factor * (1-(0.90+random()*0.095)) * (0.3+random()*0.7))::numeric, 4) AS q_def,
            (0.90 + random()*0.095) AS pass_rate
        FROM (VALUES
              (1,'DONE',1.00),
              (2, CASE WHEN po.status='COMPLETED' THEN 'DONE' ELSE 'IN_PROGRESS' END, 0.97),
              (3, CASE WHEN po.status='COMPLETED' THEN 'DONE' ELSE 'PENDING' END, 0.94),
              (4, CASE WHEN po.status='COMPLETED' THEN 'DONE' ELSE 'PENDING' END, 0.92)
            ) AS v(stage_id, step_status, in_factor)
    ) base
) s
WHERE po.status IN ('IN_PROGRESS','COMPLETED')
ON CONFLICT (production_order_id, stage_id) DO NOTHING;

-- -------------------------------------------------------------------------------------
-- 10. FINISHED_GOODS_RECEIPTS — đơn COMPLETED, received_at = completed_at (biểu đồ SL/tháng).
-- -------------------------------------------------------------------------------------
INSERT INTO finished_goods_receipts (receipt_no, production_order_id, product_id, warehouse_id, qty_received, received_at, note, created_by)
SELECT
    'PT-FG-' || lpad(row_number() OVER (ORDER BY po.id)::text, 7, '0'),
    po.id, po.product_id, 2,
    round((po.quantity * (0.88 + random()*0.10))::numeric, 4),
    COALESCE(po.completed_at, po.created_at + interval '20 days'),
    'Nhập kho TP test', pt_rand_user()
FROM production_orders po
WHERE po.status = 'COMPLETED';

-- -------------------------------------------------------------------------------------
-- 11. MATERIAL_RESERVATIONS — đơn CONFIRMED (tối đa 4 NVL/đơn).
-- -------------------------------------------------------------------------------------
INSERT INTO material_reservations (production_order_id, material_id, warehouse_id, qty_reserved, status, created_by)
SELECT DISTINCT ON (po.id, bi.material_id)
    po.id, bi.material_id, 1,
    round((po.quantity * bi.qty_per_unit * (1 + bi.waste_pct/100))::numeric, 4),
    (ARRAY['ACTIVE','ACTIVE','ACTIVE','RELEASED'])[1 + floor(random()*4)::int],
    pt_rand_user()
FROM production_orders po
JOIN LATERAL (
    SELECT material_id, qty_per_unit, waste_pct FROM bom_items
    WHERE bom_id = po.bom_id ORDER BY material_id LIMIT 4
) bi ON true
WHERE po.status = 'CONFIRMED'
ON CONFLICT (production_order_id, material_id, warehouse_id) DO NOTHING;

-- -------------------------------------------------------------------------------------
-- 12. LOSS_REPORTS — ~50% đơn COMPLETED (3 NVL đầu BOM). qty_variance là GENERATED.
-- -------------------------------------------------------------------------------------
INSERT INTO loss_reports (production_order_id, material_id, qty_issued, qty_standard, finished_qty, reported_at, created_by)
SELECT DISTINCT ON (po.id, bi.material_id)
    po.id, bi.material_id,
    round((po.quantity * bi.qty_per_unit * (1 + bi.waste_pct/100) * (0.98 + random()*0.1))::numeric, 4),
    round((po.quantity * bi.qty_per_unit)::numeric, 4),
    round((po.quantity * (0.88 + random()*0.10))::numeric, 4),
    COALESCE(po.completed_at, po.created_at + interval '22 days'),
    pt_rand_user()
FROM production_orders po
JOIN LATERAL (
    SELECT material_id, qty_per_unit, waste_pct FROM bom_items
    WHERE bom_id = po.bom_id ORDER BY material_id LIMIT 3
) bi ON true
WHERE po.status = 'COMPLETED' AND random() < 0.5
ON CONFLICT (production_order_id, material_id) DO NOTHING;

-- -------------------------------------------------------------------------------------
-- 13. ALERTS — LOW_STOCK (NVL tồn thấp) + ORDER_SHORTAGE mẫu. created_at rải gần đây.
-- -------------------------------------------------------------------------------------
INSERT INTO alerts (alert_type, severity, entity_type, entity_id, message, status, created_at)
SELECT 'LOW_STOCK',
       CASE WHEN vms.total_available = 0 THEN 'CRITICAL' ELSE 'WARN' END,
       'MATERIAL', vms.material_id,
       '[PT] Tồn thấp: ' || vms.name || ' (khả dụng ' || round(vms.total_available,2) || ' < ngưỡng ' || round(vms.reorder_level,2) || ')',
       (ARRAY['OPEN','OPEN','OPEN','ACKNOWLEDGED'])[1 + floor(random()*4)::int],
       CURRENT_DATE - (floor(random()*30) * interval '1 day')
FROM v_material_stock vms WHERE vms.is_low_stock;

INSERT INTO alerts (alert_type, severity, entity_type, entity_id, message, status, created_at)
SELECT 'ORDER_SHORTAGE','WARN','PRODUCTION_ORDER', po.id,
       '[PT] Đơn ' || po.order_no || ' có nguy cơ thiếu NVL','OPEN',
       po.created_at
FROM production_orders po
WHERE po.status = 'CONFIRMED' AND random() < 0.05;

COMMIT;

-- =====================================================================================
-- BÁO CÁO
-- =====================================================================================
SELECT '--- ĐƠN THEO THÁNG (created_at) ---' AS report;
SELECT to_char(created_at,'YYYY-MM') AS thang, count(*) AS so_don
FROM production_orders GROUP BY 1 ORDER BY 1;

SELECT '--- SỐ DÒNG CÁC BẢNG ---' AS report;
SELECT 'users' AS tbl, count(*) FROM users
UNION ALL SELECT 'materials', count(*) FROM materials
UNION ALL SELECT 'stock', count(*) FROM stock
UNION ALL SELECT 'stock_transactions', count(*) FROM stock_transactions
UNION ALL SELECT 'products', count(*) FROM products
UNION ALL SELECT 'bom', count(*) FROM bom
UNION ALL SELECT 'bom_items', count(*) FROM bom_items
UNION ALL SELECT 'production_orders', count(*) FROM production_orders
UNION ALL SELECT 'production_plans', count(*) FROM production_plans
UNION ALL SELECT 'production_steps', count(*) FROM production_steps
UNION ALL SELECT 'finished_goods_receipts', count(*) FROM finished_goods_receipts
UNION ALL SELECT 'material_reservations', count(*) FROM material_reservations
UNION ALL SELECT 'loss_reports', count(*) FROM loss_reports
UNION ALL SELECT 'alerts', count(*) FROM alerts
ORDER BY tbl;

DROP FUNCTION IF EXISTS pt_rand_user();
