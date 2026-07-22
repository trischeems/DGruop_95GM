-- =====================================================================================
-- App: ManagerPerfoment (Quản lý sản xuất may mặc)
-- File: V001__init.sql  -- Migration khởi tạo schema mẫu cho MỖI tenant
-- DBMS: PostgreSQL 16
-- Data access: Dapper (raw SQL), Npgsql. KHÔNG dùng EF Core.
-- =====================================================================================
--
-- KIẾN TRÚC MULTI-TENANT: schema-per-tenant.
--   - Mỗi tenant (bộ phận / công ty con) = 1 PostgreSQL schema riêng trong DB của app.
--   - File này là "khuôn" DDL, được chạy lại cho từng schema tenant khi khởi tạo tenant.
--   - TOÀN BỘ đối tượng nằm trong 1 schema, KHÔNG tham chiếu chéo sang schema khác.
--
-- CÁCH DÙNG (ở tầng ứng dụng, Dapper/Npgsql):
--   1) Tạo schema:            CREATE SCHEMA IF NOT EXISTS "<tenant>";
--   2) Đặt search_path:       SET search_path TO "<tenant>";  (chạy 1 lần đầu mỗi kết nối)
--   3) Chạy nội dung file này (các CREATE TABLE bên dưới không prefix schema
--      => tự tạo vào schema đang trỏ bởi search_path).
--   Vì mỗi lệnh không hard-code tên schema nên cùng 1 file dùng lại cho mọi tenant.
--   Row-Level Security (RLS) ready: có thể bật RLS trên từng bảng nếu về sau
--   dùng chung schema, hiện tại cô lập bằng schema nên chưa bắt buộc.
--
-- =====================================================================================
-- QUYẾT ĐỊNH KIỂU KHÓA CHÍNH: dùng bigint GENERATED ALWAYS AS IDENTITY.
--   LÝ DO chọn bigint identity thay vì uuid:
--     - Dữ liệu CỰC NHIỀU + truy xuất phải NHANH: bigint 8 byte, so sánh/join/index
--       nhanh và gọn hơn uuid 16 byte => index B-tree nhỏ hơn, cache tốt hơn.
--     - Khóa tăng dần (monotonic) => giảm phân mảnh index, tốt cho bảng ghi nhiều
--       (stock_transactions, material_issues...) khác hẳn uuid v4 ngẫu nhiên gây
--       page-split liên tục.
--     - Không có nhu cầu sinh id ở client trước khi insert, cũng không merge dữ liệu
--       đa nguồn (tenant đã cô lập bằng schema) => uuid không cần thiết.
--   Nếu về sau cần id không đoán được để lộ ra ngoài (public API), thêm cột phụ
--   public_code uuid DEFAULT gen_random_uuid() ở bảng cần, GIỮ khóa chính bigint.
-- =====================================================================================


-- Bật extension cần cho gen_random_uuid() (dùng cho vài cột public_code). Idempotent.
CREATE EXTENSION IF NOT EXISTS pgcrypto;


-- =====================================================================================
-- 1. NGƯỜI DÙNG / DANH MỤC NỀN
-- =====================================================================================
-- Ghi chú: users ở đây là bản chiếu tối thiểu phục vụ audit & phê duyệt trong tenant.
-- Danh tính/đăng nhập thật do module chung quản lý; ở đây chỉ cần id + tên hiển thị.
CREATE TABLE users (
    id           bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    username     text        NOT NULL,                 -- tên đăng nhập / mã nhân sự
    full_name    text        NOT NULL,                 -- tên hiển thị
    email        text,
    is_active    boolean     NOT NULL DEFAULT true,
    created_at   timestamptz NOT NULL DEFAULT now(),
    updated_at   timestamptz NOT NULL DEFAULT now(),
    CONSTRAINT uq_users_username UNIQUE (username)
);
COMMENT ON TABLE users IS 'Người dùng trong phạm vi tenant (phục vụ audit created_by, phê duyệt).';


-- Đơn vị đo (kg, m, cái, cuộn...). Chuẩn hoá để quy đổi & hiển thị nhất quán.
CREATE TABLE units_of_measure (
    id           bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    code         text        NOT NULL,                 -- 'KG', 'M', 'PCS', 'ROLL'
    name         text        NOT NULL,                 -- 'Kilogram', 'Mét', 'Cái'
    created_at   timestamptz NOT NULL DEFAULT now(),
    CONSTRAINT uq_uom_code UNIQUE (code)
);
COMMENT ON TABLE units_of_measure IS 'Danh mục đơn vị tính cho NVL và thành phẩm.';


-- =====================================================================================
-- 2. KHO & NGUYÊN VẬT LIỆU (NVL)
-- =====================================================================================
CREATE TABLE warehouses (
    id           bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    code         text        NOT NULL,                 -- mã kho: 'WH-VAI', 'WH-PHULIEU'
    name         text        NOT NULL,
    is_active    boolean     NOT NULL DEFAULT true,
    created_at   timestamptz NOT NULL DEFAULT now(),
    updated_at   timestamptz NOT NULL DEFAULT now(),
    created_by   bigint      REFERENCES users(id),
    CONSTRAINT uq_warehouses_code UNIQUE (code)
);
COMMENT ON TABLE warehouses IS 'Danh mục kho vật lý chứa NVL / thành phẩm.';


-- Loại NVL để nhóm báo cáo (vải, chỉ, cúc, khoá kéo, phụ liệu...).
CREATE TABLE material_categories (
    id           bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    code         text        NOT NULL,
    name         text        NOT NULL,
    created_at   timestamptz NOT NULL DEFAULT now(),
    CONSTRAINT uq_material_categories_code UNIQUE (code)
);
COMMENT ON TABLE material_categories IS 'Nhóm/loại nguyên vật liệu để phân loại và báo cáo.';


-- NGUYÊN VẬT LIỆU: danh mục "cái gì" (không chứa số tồn - tồn nằm ở stock).
CREATE TABLE materials (
    id                bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    sku               text        NOT NULL,            -- mã NVL nội bộ, duy nhất
    name              text        NOT NULL,
    category_id       bigint      REFERENCES material_categories(id),
    uom_id            bigint      NOT NULL REFERENCES units_of_measure(id),
    -- Ngưỡng tồn tối thiểu để CẢNH BÁO tồn thấp (so sánh với tổng available).
    reorder_level     numeric(18,4) NOT NULL DEFAULT 0 CHECK (reorder_level >= 0),
    -- Số lượng đề xuất mua mỗi lần đặt (lot). Dùng khi đề xuất mua thêm.
    reorder_quantity  numeric(18,4) NOT NULL DEFAULT 0 CHECK (reorder_quantity >= 0),
    -- Đơn giá tham chiếu (để ước tính tiền mua thêm). numeric, KHÔNG float.
    standard_cost     numeric(18,4) NOT NULL DEFAULT 0 CHECK (standard_cost >= 0),
    is_active         boolean     NOT NULL DEFAULT true,
    created_at        timestamptz NOT NULL DEFAULT now(),
    updated_at        timestamptz NOT NULL DEFAULT now(),
    created_by        bigint      REFERENCES users(id),
    CONSTRAINT uq_materials_sku UNIQUE (sku)
);
COMMENT ON TABLE materials  IS 'Danh mục nguyên vật liệu (định nghĩa, KHÔNG chứa số tồn).';
COMMENT ON COLUMN materials.reorder_level    IS 'Ngưỡng tồn tối thiểu; tồn khả dụng < ngưỡng này => cảnh báo tồn thấp.';
COMMENT ON COLUMN materials.reorder_quantity IS 'Cỡ lô đặt mua đề xuất khi cần mua thêm.';

-- INDEX: lọc/tìm NVL đang hoạt động theo tên (màn hình chọn NVL).
CREATE INDEX ix_materials_active_name ON materials (is_active, name);
-- INDEX: lọc theo nhóm NVL cho báo cáo.
CREATE INDEX ix_materials_category ON materials (category_id) WHERE category_id IS NOT NULL;


-- TỒN KHO: số lượng của 1 NVL tại 1 kho. 1 dòng / (warehouse, material).
-- TÁCH available vs reserved để tính TỒN KHẢ DỤNG (free) = qty_on_hand - qty_reserved.
CREATE TABLE stock (
    id            bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    warehouse_id  bigint      NOT NULL REFERENCES warehouses(id),
    material_id   bigint      NOT NULL REFERENCES materials(id),
    -- Tồn vật lý thực tế đang có trong kho.
    qty_on_hand   numeric(18,4) NOT NULL DEFAULT 0 CHECK (qty_on_hand   >= 0),
    -- Tồn đã GIỮ CHỖ cho các đơn đã xác nhận (chưa xuất). Không được vượt qty_on_hand.
    qty_reserved  numeric(18,4) NOT NULL DEFAULT 0 CHECK (qty_reserved  >= 0),
    updated_at    timestamptz NOT NULL DEFAULT now(),
    CONSTRAINT uq_stock_wh_material UNIQUE (warehouse_id, material_id),
    -- Không cho giữ chỗ nhiều hơn tồn thực tế.
    CONSTRAINT ck_stock_reserved_le_onhand CHECK (qty_reserved <= qty_on_hand)
);
COMMENT ON TABLE  stock              IS 'Tồn kho theo (kho, NVL). Tồn khả dụng = qty_on_hand - qty_reserved.';
COMMENT ON COLUMN stock.qty_on_hand  IS 'Tồn vật lý thực tế.';
COMMENT ON COLUMN stock.qty_reserved IS 'Đã giữ chỗ cho đơn đã xác nhận (chưa xuất kho).';

-- INDEX NÓNG: tra tồn theo material (tính sản lượng tối đa gộp tồn khả dụng mọi kho).
--   Truy vấn: SELECT material_id, SUM(qty_on_hand - qty_reserved) ... GROUP BY material_id.
--   INCLUDE 2 cột số để index-only scan, khỏi chạm heap.
CREATE INDEX ix_stock_material ON stock (material_id) INCLUDE (qty_on_hand, qty_reserved);


-- =====================================================================================
-- 3. AUDIT GIAO DỊCH KHO (nhập / xuất / điều chỉnh)  -- BẤT BIẾN, không sửa/xoá
-- =====================================================================================
-- Loại giao dịch kho. Mọi thay đổi tồn PHẢI ghi 1 dòng ở đây (nguồn sự thật audit).
CREATE TABLE stock_transactions (
    id             bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    warehouse_id   bigint      NOT NULL REFERENCES warehouses(id),
    material_id    bigint      NOT NULL REFERENCES materials(id),
    -- Loại: RECEIPT nhập, ISSUE xuất sx, ADJUST điều chỉnh (+/-), RETURN trả lại kho.
    txn_type       text        NOT NULL
        CHECK (txn_type IN ('RECEIPT','ISSUE','ADJUST','RETURN')),
    -- Lượng thay đổi tồn: DƯƠNG khi nhập/tăng, ÂM khi xuất/giảm. numeric, KHÔNG float.
    quantity       numeric(18,4) NOT NULL CHECK (quantity <> 0),
    unit_cost      numeric(18,4) NOT NULL DEFAULT 0 CHECK (unit_cost >= 0),
    -- Tồn còn lại sau giao dịch (snapshot để tra cứu nhanh & kiểm tra tính đúng đắn).
    balance_after  numeric(18,4) NOT NULL CHECK (balance_after >= 0),
    -- Liên kết chứng từ nguồn (đa hình): loại + id, không FK cứng để linh hoạt.
    ref_type       text,                               -- 'MATERIAL_ISSUE','RECEIPT','MANUAL'...
    ref_id         bigint,
    note           text,
    created_at     timestamptz NOT NULL DEFAULT now(),
    created_by     bigint      REFERENCES users(id)
);
COMMENT ON TABLE  stock_transactions          IS 'Sổ cái giao dịch kho (audit đầy đủ, chỉ ghi thêm - append only).';
COMMENT ON COLUMN stock_transactions.quantity IS 'Delta tồn: (+) nhập/tăng, (-) xuất/giảm.';
COMMENT ON COLUMN stock_transactions.balance_after IS 'Tồn qty_on_hand còn lại NGAY SAU giao dịch này.';

-- INDEX NÓNG: xem lịch sử giao dịch của 1 NVL theo thời gian mới nhất trước.
CREATE INDEX ix_stxn_material_time ON stock_transactions (material_id, created_at DESC);
-- INDEX: truy ngược từ chứng từ nguồn (vd xem các giao dịch của 1 phiếu xuất).
CREATE INDEX ix_stxn_ref ON stock_transactions (ref_type, ref_id) WHERE ref_id IS NOT NULL;


-- =====================================================================================
-- 4. THÀNH PHẨM & ĐỊNH MỨC BOM
-- =====================================================================================
-- Mã hàng thành phẩm (mã sản xuất). Ví dụ áo/quần theo mã.
CREATE TABLE products (
    id           bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    sku          text        NOT NULL,                 -- mã hàng, duy nhất
    name         text        NOT NULL,
    uom_id       bigint      NOT NULL REFERENCES units_of_measure(id),
    is_active    boolean     NOT NULL DEFAULT true,
    created_at   timestamptz NOT NULL DEFAULT now(),
    updated_at   timestamptz NOT NULL DEFAULT now(),
    created_by   bigint      REFERENCES users(id),
    CONSTRAINT uq_products_sku UNIQUE (sku)
);
COMMENT ON TABLE products IS 'Danh mục mã hàng thành phẩm cần sản xuất.';
CREATE INDEX ix_products_active_name ON products (is_active, name);


-- BOM (Bill of Materials): 1 phiên bản định mức của 1 product.
-- Cho phép nhiều phiên bản (version) theo thời gian; chỉ 1 phiên bản ACTIVE.
CREATE TABLE bom (
    id             bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    product_id     bigint      NOT NULL REFERENCES products(id),
    version        integer     NOT NULL DEFAULT 1 CHECK (version >= 1),
    -- Trạng thái vòng đời định mức: DRAFT -> PENDING_APPROVAL -> ACTIVE -> ARCHIVED.
    status         text        NOT NULL DEFAULT 'DRAFT'
        CHECK (status IN ('DRAFT','PENDING_APPROVAL','ACTIVE','ARCHIVED','REJECTED')),
    effective_from timestamptz,                        -- ngày hiệu lực khi ACTIVE
    note           text,
    created_at     timestamptz NOT NULL DEFAULT now(),
    updated_at     timestamptz NOT NULL DEFAULT now(),
    created_by     bigint      REFERENCES users(id),
    CONSTRAINT uq_bom_product_version UNIQUE (product_id, version)
);
COMMENT ON TABLE bom IS 'Phiên bản định mức nguyên vật liệu (BOM) của một mã hàng.';

-- RÀNG BUỘC: mỗi product chỉ được 1 BOM ACTIVE tại một thời điểm (partial unique index).
CREATE UNIQUE INDEX uq_bom_one_active_per_product
    ON bom (product_id) WHERE status = 'ACTIVE';
-- INDEX: lấy nhanh BOM active theo product (bước "Lấy BOM/định mức").
CREATE INDEX ix_bom_product_status ON bom (product_id, status);


-- BOM_ITEMS: chi tiết định mức - 1 sản phẩm cần bao nhiêu của từng NVL.
CREATE TABLE bom_items (
    id             bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    bom_id         bigint      NOT NULL REFERENCES bom(id) ON DELETE CASCADE,
    material_id    bigint      NOT NULL REFERENCES materials(id),
    -- Định mức: số lượng NVL cần cho 1 ĐƠN VỊ thành phẩm. Phải > 0.
    qty_per_unit   numeric(18,6) NOT NULL CHECK (qty_per_unit > 0),
    -- Tỷ lệ hao hụt định mức (%) cộng thêm khi tính nhu cầu. 0 = không hao hụt.
    waste_pct      numeric(9,4)  NOT NULL DEFAULT 0 CHECK (waste_pct >= 0 AND waste_pct < 100),
    note           text,
    CONSTRAINT uq_bom_item UNIQUE (bom_id, material_id)
);
COMMENT ON TABLE  bom_items              IS 'Chi tiết định mức: mỗi NVL cần bao nhiêu cho 1 đơn vị thành phẩm.';
COMMENT ON COLUMN bom_items.qty_per_unit IS 'Định mức NVL / 1 đơn vị thành phẩm (numeric, KHÔNG float).';
COMMENT ON COLUMN bom_items.waste_pct    IS 'Hao hụt định mức %: nhu cầu thực = qty_per_unit * (1 + waste_pct/100).';

-- INDEX NÓNG: tính sản lượng tối đa / nhu cầu NVL => join bom_items theo bom_id.
CREATE INDEX ix_bom_items_bom ON bom_items (bom_id) INCLUDE (material_id, qty_per_unit, waste_pct);
-- INDEX: tra "NVL này nằm trong BOM nào" (khi 1 NVL thiếu, tìm sản phẩm ảnh hưởng).
CREATE INDEX ix_bom_items_material ON bom_items (material_id);


-- =====================================================================================
-- 5. LỊCH SỬ ĐIỀU CHỈNH ĐỊNH MỨC & PHÊ DUYỆT
-- =====================================================================================
-- Lịch sử thay đổi định mức (audit "ai đổi gì, từ giá trị nào sang giá trị nào").
CREATE TABLE bom_change_history (
    id             bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    bom_id         bigint      NOT NULL REFERENCES bom(id) ON DELETE CASCADE,
    bom_item_id    bigint      REFERENCES bom_items(id) ON DELETE SET NULL,
    material_id    bigint      REFERENCES materials(id),
    -- Hành động: ADD_ITEM, REMOVE_ITEM, UPDATE_QTY, UPDATE_WASTE, STATUS_CHANGE...
    change_type    text        NOT NULL
        CHECK (change_type IN ('ADD_ITEM','REMOVE_ITEM','UPDATE_QTY','UPDATE_WASTE','STATUS_CHANGE')),
    old_value      numeric(18,6),                      -- giá trị định mức cũ (nếu có)
    new_value      numeric(18,6),                      -- giá trị định mức mới (nếu có)
    reason         text,
    created_at     timestamptz NOT NULL DEFAULT now(),
    created_by     bigint      REFERENCES users(id)
);
COMMENT ON TABLE bom_change_history IS 'Lịch sử điều chỉnh định mức BOM (audit thay đổi giá trị).';
CREATE INDEX ix_bomhist_bom_time ON bom_change_history (bom_id, created_at DESC);


-- Phê duyệt thay đổi BOM/định mức. Mỗi lần đưa BOM đi duyệt tạo 1 request.
CREATE TABLE bom_approvals (
    id             bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    bom_id         bigint      NOT NULL REFERENCES bom(id) ON DELETE CASCADE,
    -- Trạng thái phê duyệt.
    status         text        NOT NULL DEFAULT 'PENDING'
        CHECK (status IN ('PENDING','APPROVED','REJECTED','CANCELLED')),
    requested_by   bigint      REFERENCES users(id),
    requested_at   timestamptz NOT NULL DEFAULT now(),
    decided_by     bigint      REFERENCES users(id),   -- người duyệt/từ chối
    decided_at     timestamptz,
    decision_note  text,
    CONSTRAINT ck_bom_approval_decided
        CHECK ( (status IN ('APPROVED','REJECTED')) = (decided_at IS NOT NULL) )
);
COMMENT ON TABLE bom_approvals IS 'Yêu cầu & kết quả phê duyệt thay đổi BOM/định mức.';
-- INDEX: hàng đợi duyệt (lấy nhanh các request PENDING cũ nhất).
CREATE INDEX ix_bom_approvals_pending ON bom_approvals (requested_at) WHERE status = 'PENDING';
CREATE INDEX ix_bom_approvals_bom ON bom_approvals (bom_id);


-- =====================================================================================
-- 6. ĐƠN HÀNG SẢN XUẤT & KẾ HOẠCH
-- =====================================================================================
CREATE TABLE production_orders (
    id             bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    public_code    uuid        NOT NULL DEFAULT gen_random_uuid(), -- mã lộ ra ngoài, không đoán được
    order_no       text        NOT NULL,               -- số đơn hiển thị (nghiệp vụ)
    product_id     bigint      NOT NULL REFERENCES products(id),
    -- Chốt BOM dùng cho đơn (để đơn không bị đổi khi BOM sau này thay đổi).
    bom_id         bigint      REFERENCES bom(id),
    quantity       numeric(18,4) NOT NULL CHECK (quantity > 0),   -- số lượng thành phẩm đặt
    -- Trạng thái đơn: DRAFT nháp, CONFIRMED xác nhận (mới được giữ chỗ NVL),
    --   IN_PROGRESS đang sx, COMPLETED xong, CANCELLED huỷ.
    status         text        NOT NULL DEFAULT 'DRAFT'
        CHECK (status IN ('DRAFT','CONFIRMED','IN_PROGRESS','COMPLETED','CANCELLED')),
    due_date       date,
    confirmed_at   timestamptz,
    completed_at   timestamptz,
    note           text,
    created_at     timestamptz NOT NULL DEFAULT now(),
    updated_at     timestamptz NOT NULL DEFAULT now(),
    created_by     bigint      REFERENCES users(id),
    CONSTRAINT uq_po_order_no UNIQUE (order_no)
);
COMMENT ON TABLE  production_orders        IS 'Đơn hàng sản xuất (theo mã hàng + số lượng).';
COMMENT ON COLUMN production_orders.bom_id IS 'BOM được chốt cho đơn tại thời điểm xác nhận (không đổi theo BOM sau này).';

-- INDEX NÓNG: bảng công việc theo trạng thái (tìm đơn theo trạng thái, sắp theo hạn).
--   Truy vấn: WHERE status = 'CONFIRMED' ORDER BY due_date.
CREATE INDEX ix_po_status_due ON production_orders (status, due_date);
-- INDEX: đơn theo mã hàng.
CREATE INDEX ix_po_product ON production_orders (product_id);


-- Kế hoạch sản xuất: chia nhỏ / lên lịch cho đơn (theo ngày/tuyến chuyền).
CREATE TABLE production_plans (
    id                bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    production_order_id bigint    NOT NULL REFERENCES production_orders(id) ON DELETE CASCADE,
    planned_qty       numeric(18,4) NOT NULL CHECK (planned_qty > 0),
    planned_start     date,
    planned_end       date,
    line_code         text,                            -- mã chuyền/tổ sản xuất
    status            text        NOT NULL DEFAULT 'PLANNED'
        CHECK (status IN ('PLANNED','RELEASED','IN_PROGRESS','DONE','CANCELLED')),
    note              text,
    created_at        timestamptz NOT NULL DEFAULT now(),
    updated_at        timestamptz NOT NULL DEFAULT now(),
    created_by        bigint      REFERENCES users(id),
    CONSTRAINT ck_plan_dates CHECK (planned_end IS NULL OR planned_start IS NULL OR planned_end >= planned_start)
);
COMMENT ON TABLE production_plans IS 'Kế hoạch sản xuất chi tiết cho đơn (lịch, chuyền, số lượng).';
CREATE INDEX ix_plans_order ON production_plans (production_order_id);
-- INDEX: lịch theo ngày bắt đầu (bảng kế hoạch theo thời gian).
CREATE INDEX ix_plans_start ON production_plans (planned_start);


-- =====================================================================================
-- 7. QUY TRÌNH SẢN XUẤT (STATE MACHINE): Cắt vải -> May -> QC -> Nhập kho TP
-- =====================================================================================
-- Danh mục công đoạn chuẩn (thứ tự seq để state machine biết bước kế tiếp).
CREATE TABLE production_stages (
    id           bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    code         text        NOT NULL,                 -- 'CUTTING','SEWING','QC','FG_RECEIPT'
    name         text        NOT NULL,
    seq          integer     NOT NULL,                 -- thứ tự công đoạn (1,2,3,4)
    CONSTRAINT uq_stage_code UNIQUE (code),
    CONSTRAINT uq_stage_seq  UNIQUE (seq)
);
COMMENT ON TABLE production_stages IS 'Danh mục công đoạn: Cắt vải, May, QC, Nhập kho thành phẩm.';

-- Dữ liệu công đoạn chuẩn (thứ tự cố định). Idempotent.
INSERT INTO production_stages (code, name, seq) VALUES
    ('CUTTING',    'Cắt vải',              1),
    ('SEWING',     'May',                  2),
    ('QC',         'Kiểm tra chất lượng',  3),
    ('FG_RECEIPT', 'Nhập kho thành phẩm',  4)
ON CONFLICT (code) DO NOTHING;


-- production_steps: tiến trình 1 công đoạn của 1 đơn (bản ghi state machine).
CREATE TABLE production_steps (
    id                  bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    production_order_id bigint    NOT NULL REFERENCES production_orders(id) ON DELETE CASCADE,
    stage_id            bigint    NOT NULL REFERENCES production_stages(id),
    -- Trạng thái công đoạn: PENDING chờ, IN_PROGRESS đang làm, DONE xong, ON_HOLD tạm dừng.
    status              text      NOT NULL DEFAULT 'PENDING'
        CHECK (status IN ('PENDING','IN_PROGRESS','DONE','ON_HOLD','CANCELLED')),
    qty_in              numeric(18,4) NOT NULL DEFAULT 0 CHECK (qty_in  >= 0), -- lượng vào công đoạn
    qty_out             numeric(18,4) NOT NULL DEFAULT 0 CHECK (qty_out >= 0), -- lượng ra (đạt)
    qty_defect          numeric(18,4) NOT NULL DEFAULT 0 CHECK (qty_defect >= 0), -- lỗi (QC)
    started_at          timestamptz,
    finished_at         timestamptz,
    note                text,
    created_at          timestamptz NOT NULL DEFAULT now(),
    updated_at          timestamptz NOT NULL DEFAULT now(),
    updated_by          bigint      REFERENCES users(id),
    -- Mỗi đơn chỉ có 1 bản ghi / công đoạn.
    CONSTRAINT uq_step_order_stage UNIQUE (production_order_id, stage_id),
    CONSTRAINT ck_step_out_le_in   CHECK (qty_out + qty_defect <= qty_in OR qty_in = 0)
);
COMMENT ON TABLE production_steps IS 'Tiến trình từng công đoạn của đơn (state machine: Cắt->May->QC->Nhập kho).';
-- INDEX NÓNG: bảng theo dõi công đoạn đang chạy (WHERE status IN (...) theo stage).
CREATE INDEX ix_steps_stage_status ON production_steps (stage_id, status);
CREATE INDEX ix_steps_order ON production_steps (production_order_id);


-- =====================================================================================
-- 8. GIỮ CHỖ & XUẤT NVL
-- =====================================================================================
-- Giữ chỗ NVL cho đơn ĐÃ XÁC NHẬN (tăng stock.qty_reserved, chưa trừ tồn thực).
CREATE TABLE material_reservations (
    id                  bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    production_order_id bigint    NOT NULL REFERENCES production_orders(id) ON DELETE CASCADE,
    material_id         bigint    NOT NULL REFERENCES materials(id),
    warehouse_id        bigint    NOT NULL REFERENCES warehouses(id),
    -- Số lượng giữ chỗ (theo nhu cầu = số lượng đơn * định mức * (1+hao hụt)).
    qty_reserved        numeric(18,4) NOT NULL CHECK (qty_reserved > 0),
    -- Đã giải phóng chưa (khi đơn huỷ hoặc đã xuất hết thì release).
    status              text      NOT NULL DEFAULT 'ACTIVE'
        CHECK (status IN ('ACTIVE','RELEASED','CONSUMED')),
    created_at          timestamptz NOT NULL DEFAULT now(),
    updated_at          timestamptz NOT NULL DEFAULT now(),
    created_by          bigint      REFERENCES users(id),
    CONSTRAINT uq_reservation UNIQUE (production_order_id, material_id, warehouse_id)
);
COMMENT ON TABLE material_reservations IS 'Giữ chỗ NVL cho đơn đã xác nhận (đối ứng stock.qty_reserved).';
-- INDEX: tra các giữ chỗ đang active của 1 NVL (để cộng dồn reserved khi cần).
CREATE INDEX ix_resv_material_active ON material_reservations (material_id) WHERE status = 'ACTIVE';
CREATE INDEX ix_resv_order ON material_reservations (production_order_id);


-- Phiếu xuất NVL cho sản xuất (header). Khi xuất => trừ stock.qty_on_hand.
CREATE TABLE material_issues (
    id                  bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    issue_no            text      NOT NULL,
    production_order_id bigint    NOT NULL REFERENCES production_orders(id),
    warehouse_id        bigint    NOT NULL REFERENCES warehouses(id),
    status              text      NOT NULL DEFAULT 'POSTED'
        CHECK (status IN ('DRAFT','POSTED','CANCELLED')),
    issued_at           timestamptz NOT NULL DEFAULT now(),
    note                text,
    created_at          timestamptz NOT NULL DEFAULT now(),
    created_by          bigint      REFERENCES users(id),
    CONSTRAINT uq_issue_no UNIQUE (issue_no)
);
COMMENT ON TABLE material_issues IS 'Phiếu xuất NVL cho sản xuất (header). Khi POSTED trừ tồn thực.';
CREATE INDEX ix_issues_order ON material_issues (production_order_id);


-- Chi tiết xuất NVL.
CREATE TABLE material_issue_items (
    id               bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    material_issue_id bigint   NOT NULL REFERENCES material_issues(id) ON DELETE CASCADE,
    material_id      bigint    NOT NULL REFERENCES materials(id),
    qty_issued       numeric(18,4) NOT NULL CHECK (qty_issued > 0),
    unit_cost        numeric(18,4) NOT NULL DEFAULT 0 CHECK (unit_cost >= 0),
    CONSTRAINT uq_issue_item UNIQUE (material_issue_id, material_id)
);
COMMENT ON TABLE material_issue_items IS 'Chi tiết NVL đã xuất cho sản xuất (dùng đối chiếu cấp phát).';
CREATE INDEX ix_issue_items_issue ON material_issue_items (material_issue_id);
CREATE INDEX ix_issue_items_material ON material_issue_items (material_id);


-- =====================================================================================
-- 9. NHẬP KHO THÀNH PHẨM & ĐỐI CHIẾU / HAO HỤT
-- =====================================================================================
-- Nhập kho thành phẩm (kết quả cuối quy trình).
CREATE TABLE finished_goods_receipts (
    id                  bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    receipt_no          text      NOT NULL,
    production_order_id bigint    NOT NULL REFERENCES production_orders(id),
    product_id          bigint    NOT NULL REFERENCES products(id),
    warehouse_id        bigint    NOT NULL REFERENCES warehouses(id),
    qty_received        numeric(18,4) NOT NULL CHECK (qty_received > 0), -- thành phẩm đạt nhập kho
    received_at         timestamptz NOT NULL DEFAULT now(),
    note                text,
    created_at          timestamptz NOT NULL DEFAULT now(),
    created_by          bigint      REFERENCES users(id),
    CONSTRAINT uq_fg_receipt_no UNIQUE (receipt_no)
);
COMMENT ON TABLE finished_goods_receipts IS 'Nhập kho thành phẩm (sản lượng đạt của đơn).';
CREATE INDEX ix_fg_order ON finished_goods_receipts (production_order_id);
CREATE INDEX ix_fg_product_time ON finished_goods_receipts (product_id, received_at DESC);


-- Báo cáo đối chiếu & hao hụt: NVL đã cấp phát so với định mức lẽ ra cần cho
-- sản lượng thành phẩm thực tế đã nhập kho => ra hao hụt thực tế vs định mức.
CREATE TABLE loss_reports (
    id                  bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    production_order_id bigint    NOT NULL REFERENCES production_orders(id),
    material_id         bigint    NOT NULL REFERENCES materials(id),
    -- NVL thực tế đã cấp phát (tổng qty_issued cho đơn+NVL này).
    qty_issued          numeric(18,4) NOT NULL DEFAULT 0 CHECK (qty_issued >= 0),
    -- NVL lẽ ra cần theo định mức cho sản lượng thành phẩm thực nhập (chưa gồm hao hụt).
    qty_standard        numeric(18,4) NOT NULL DEFAULT 0 CHECK (qty_standard >= 0),
    -- Hao hụt thực tế = qty_issued - qty_standard (có thể âm nếu tiết kiệm).
    qty_variance        numeric(18,4) GENERATED ALWAYS AS (qty_issued - qty_standard) STORED,
    -- Sản lượng thành phẩm thực tế dùng để tính qty_standard (để tái lập số liệu).
    finished_qty        numeric(18,4) NOT NULL DEFAULT 0 CHECK (finished_qty >= 0),
    reported_at         timestamptz NOT NULL DEFAULT now(),
    created_by          bigint      REFERENCES users(id),
    CONSTRAINT uq_loss_order_material UNIQUE (production_order_id, material_id)
);
COMMENT ON TABLE  loss_reports              IS 'Đối chiếu NVL cấp phát vs định mức & hao hụt thực tế theo đơn.';
COMMENT ON COLUMN loss_reports.qty_variance IS 'Hao hụt thực tế = cấp phát - định mức (dương = hao nhiều hơn định mức).';
CREATE INDEX ix_loss_order ON loss_reports (production_order_id);
CREATE INDEX ix_loss_material ON loss_reports (material_id);


-- =====================================================================================
-- 10. CẢNH BÁO (tồn thấp, đơn nguy cơ thiếu NVL)
-- =====================================================================================
CREATE TABLE alerts (
    id           bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    -- Loại cảnh báo: LOW_STOCK tồn thấp, ORDER_SHORTAGE đơn thiếu NVL, BOM_PENDING chờ duyệt.
    alert_type   text        NOT NULL
        CHECK (alert_type IN ('LOW_STOCK','ORDER_SHORTAGE','BOM_PENDING')),
    severity     text        NOT NULL DEFAULT 'WARN'
        CHECK (severity IN ('INFO','WARN','CRITICAL')),
    -- Đối tượng liên quan (đa hình): MATERIAL / PRODUCTION_ORDER / BOM + id.
    entity_type  text        NOT NULL CHECK (entity_type IN ('MATERIAL','PRODUCTION_ORDER','BOM')),
    entity_id    bigint      NOT NULL,
    message      text        NOT NULL,
    -- Trạng thái xử lý cảnh báo.
    status       text        NOT NULL DEFAULT 'OPEN'
        CHECK (status IN ('OPEN','ACKNOWLEDGED','RESOLVED')),
    created_at   timestamptz NOT NULL DEFAULT now(),
    resolved_at  timestamptz,
    resolved_by  bigint      REFERENCES users(id)
);
COMMENT ON TABLE alerts IS 'Cảnh báo hệ thống: tồn thấp, đơn nguy cơ thiếu NVL, BOM chờ duyệt.';
-- INDEX NÓNG: danh sách cảnh báo đang mở, ưu tiên mới nhất (dashboard).
CREATE INDEX ix_alerts_open ON alerts (created_at DESC) WHERE status = 'OPEN';
-- INDEX: tránh trùng & tra cảnh báo theo đối tượng.
CREATE INDEX ix_alerts_entity ON alerts (entity_type, entity_id, alert_type);


-- =====================================================================================
-- 11. TRIGGER updated_at: tự cập nhật timestamp khi UPDATE (giảm sai sót ở tầng app)
-- =====================================================================================
CREATE OR REPLACE FUNCTION set_updated_at() RETURNS trigger AS $$
BEGIN
    NEW.updated_at := now();
    RETURN NEW;
END;
$$ LANGUAGE plpgsql;

-- Gắn trigger cho các bảng có cột updated_at.
CREATE TRIGGER trg_users_updated             BEFORE UPDATE ON users             FOR EACH ROW EXECUTE FUNCTION set_updated_at();
CREATE TRIGGER trg_warehouses_updated        BEFORE UPDATE ON warehouses        FOR EACH ROW EXECUTE FUNCTION set_updated_at();
CREATE TRIGGER trg_materials_updated         BEFORE UPDATE ON materials         FOR EACH ROW EXECUTE FUNCTION set_updated_at();
CREATE TRIGGER trg_stock_updated             BEFORE UPDATE ON stock             FOR EACH ROW EXECUTE FUNCTION set_updated_at();
CREATE TRIGGER trg_products_updated          BEFORE UPDATE ON products          FOR EACH ROW EXECUTE FUNCTION set_updated_at();
CREATE TRIGGER trg_bom_updated               BEFORE UPDATE ON bom               FOR EACH ROW EXECUTE FUNCTION set_updated_at();
CREATE TRIGGER trg_po_updated                BEFORE UPDATE ON production_orders FOR EACH ROW EXECUTE FUNCTION set_updated_at();
CREATE TRIGGER trg_plans_updated             BEFORE UPDATE ON production_plans  FOR EACH ROW EXECUTE FUNCTION set_updated_at();
CREATE TRIGGER trg_steps_updated             BEFORE UPDATE ON production_steps  FOR EACH ROW EXECUTE FUNCTION set_updated_at();
CREATE TRIGGER trg_resv_updated              BEFORE UPDATE ON material_reservations FOR EACH ROW EXECUTE FUNCTION set_updated_at();


-- =====================================================================================
-- 12. VIEW NGHIỆP VỤ (công thức quan trọng đóng gói sẵn - Dapper query trực tiếp)
-- =====================================================================================

-- v_material_stock: tồn khả dụng gộp mọi kho cho từng NVL + cờ tồn thấp.
--   available = qty_on_hand - qty_reserved  (tồn tự do có thể dùng ngay).
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
    -- Cờ CẢNH BÁO TỒN THẤP: tồn khả dụng < ngưỡng reorder_level.
    (COALESCE(SUM(s.qty_on_hand - s.qty_reserved), 0) < m.reorder_level) AS is_low_stock
FROM materials m
LEFT JOIN stock s ON s.material_id = m.id
WHERE m.is_active
GROUP BY m.id, m.sku, m.name, m.reorder_level, m.reorder_quantity;
COMMENT ON VIEW v_material_stock IS
    'Tồn khả dụng gộp mọi kho theo NVL. available = on_hand - reserved. is_low_stock = available < reorder_level.';


-- v_max_output_by_material: với 1 BOM ACTIVE, mỗi NVL cho ra sản lượng tối đa bao nhiêu.
--   CÔNG THỨC: max theo từng NVL = floor( total_available / (qty_per_unit * (1+waste_pct/100)) ).
--   Sản lượng tối đa của SẢN PHẨM = MIN(max theo từng NVL) => xem v_max_output_by_product.
CREATE OR REPLACE VIEW v_max_output_by_material AS
SELECT
    b.product_id,
    b.id                                             AS bom_id,
    bi.material_id,
    bi.qty_per_unit,
    bi.waste_pct,
    -- Nhu cầu NVL cho 1 đơn vị thành phẩm (đã tính hao hụt định mức).
    (bi.qty_per_unit * (1 + bi.waste_pct/100))       AS effective_qty_per_unit,
    ms.total_available,
    -- Sản lượng tối đa mà RIÊNG NVL này đáp ứng (làm tròn xuống).
    FLOOR( ms.total_available / (bi.qty_per_unit * (1 + bi.waste_pct/100)) ) AS max_output_for_material
FROM bom b
JOIN bom_items bi        ON bi.bom_id = b.id
JOIN v_material_stock ms ON ms.material_id = bi.material_id
WHERE b.status = 'ACTIVE';
COMMENT ON VIEW v_max_output_by_material IS
    'Sản lượng tối đa theo TỪNG NVL của BOM active = floor(available / (qty_per_unit*(1+waste%))).';


-- v_max_output_by_product: sản lượng tối đa của mỗi mã hàng = MIN qua các NVL.
--   Đồng thời chỉ ra NVL "nút thắt" (bottleneck) đang giới hạn sản lượng.
CREATE OR REPLACE VIEW v_max_output_by_product AS
SELECT DISTINCT ON (product_id)
    product_id,
    bom_id,
    MIN(max_output_for_material) OVER (PARTITION BY product_id) AS max_producible_qty,
    material_id                                                AS bottleneck_material_id,
    max_output_for_material                                    AS bottleneck_material_output
FROM v_max_output_by_material
ORDER BY product_id, max_output_for_material ASC;  -- DISTINCT ON lấy NVL sản lượng thấp nhất
COMMENT ON VIEW v_max_output_by_product IS
    'Sản lượng tối đa mỗi mã hàng = MIN qua các NVL. bottleneck_material_id = NVL giới hạn (sản lượng thấp nhất).';


-- v_order_material_requirement: nhu cầu NVL của TỪNG đơn (theo BOM đã chốt) so với
--   tồn khả dụng => thiếu hụt & đề xuất mua thêm. Phục vụ:
--   - cảnh báo đơn nguy cơ thiếu NVL (shortage_qty > 0)
--   - đề xuất số lượng mua thêm (suggested_purchase_qty)
CREATE OR REPLACE VIEW v_order_material_requirement AS
SELECT
    po.id                                            AS production_order_id,
    po.order_no,
    po.status                                        AS order_status,
    bi.material_id,
    -- Nhu cầu NVL cho cả đơn = số lượng đơn * định mức * (1+hao hụt).
    (po.quantity * bi.qty_per_unit * (1 + bi.waste_pct/100)) AS required_qty,
    ms.total_available,
    -- Thiếu hụt = max(0, nhu cầu - tồn khả dụng).
    GREATEST(0, (po.quantity * bi.qty_per_unit * (1 + bi.waste_pct/100)) - ms.total_available) AS shortage_qty,
    -- Đề xuất mua thêm: làm tròn LÊN theo cỡ lô reorder_quantity nếu có, ngược lại = thiếu hụt.
    CASE
        WHEN m.reorder_quantity > 0 THEN
            CEIL( GREATEST(0, (po.quantity * bi.qty_per_unit * (1 + bi.waste_pct/100)) - ms.total_available)
                  / m.reorder_quantity ) * m.reorder_quantity
        ELSE GREATEST(0, (po.quantity * bi.qty_per_unit * (1 + bi.waste_pct/100)) - ms.total_available)
    END                                              AS suggested_purchase_qty
FROM production_orders po
JOIN bom_items bi        ON bi.bom_id = po.bom_id
JOIN materials m         ON m.id = bi.material_id
JOIN v_material_stock ms ON ms.material_id = bi.material_id
WHERE po.status IN ('DRAFT','CONFIRMED','IN_PROGRESS');
COMMENT ON VIEW v_order_material_requirement IS
    'Nhu cầu NVL / đơn vs tồn khả dụng: required_qty, shortage_qty (thiếu hụt), suggested_purchase_qty (đề xuất mua thêm).';


-- =====================================================================================
-- HẾT V001__init.sql
-- Ghi chú tính đúng đắn tồn kho (thực thi ở tầng app trong 1 transaction):
--   * Giữ chỗ (CONFIRMED): UPDATE stock SET qty_reserved += need; INSERT material_reservations.
--   * Xuất kho (ISSUE):    UPDATE stock SET qty_on_hand -= qty, qty_reserved -= qty;
--                          INSERT material_issues + stock_transactions(txn_type='ISSUE', quantity=-qty).
--   * Nhập kho (RECEIPT):  UPDATE stock SET qty_on_hand += qty;
--                          INSERT stock_transactions(txn_type='RECEIPT', quantity=+qty).
--   Luôn dùng SELECT ... FOR UPDATE trên dòng stock để chống race khi 500+ user đồng thời.
-- =====================================================================================
