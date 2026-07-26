-- =====================================================================================
-- App: ManagerPerfoment
-- File: V006__flexible_routing.sql
-- Muc dich: QUY TRINH SAN XUAT LINH HOAT.
--   Truoc: moi don deu cung 4 buoc cung (Cat->May->QC->Nhap TP), khong bo/nhay buoc duoc.
--   Sau  : (1) DANH MUC CONG DOAN mo rong tuy y (them buoc In, Say, Theu, Dong goi...).
--          (2) MAU QUY TRINH (production_routings + routing_steps): moi mau = 1 chuoi buoc.
--          (3) Don chon 1 mau (production_orders.routing_id) -> sinh buoc theo mau.
--          (4) Moi don van SUA RIENG duoc: them/bo/doi thu tu buoc (production_steps.seq).
-- V001-V005 bat bien; day la file moi. Khong prefix schema (chay theo tenant).
-- =====================================================================================


-- =====================================================================================
-- PHAN 1: NOI LONG DANH MUC CONG DOAN
-- =====================================================================================
-- Bo rang buoc seq DUY NHAT toan cuc: seq gio thuoc ve TUNG MAU quy trinh,
-- nen 2 cong doan khac nhau co the cung so thu tu o 2 mau khac nhau.
ALTER TABLE production_stages DROP CONSTRAINT IF EXISTS uq_stage_seq;

-- Cho phep danh dau cong doan ngung dung (khong xoa de giu lich su).
ALTER TABLE production_stages ADD COLUMN IF NOT EXISTS is_active boolean NOT NULL DEFAULT true;
-- seq o day chi con la GOI Y thu tu mac dinh khi hien danh muc.
COMMENT ON COLUMN production_stages.seq IS 'Thu tu goi y khi hien danh muc; thu tu THUC TE nam o routing_steps.seq / production_steps.seq.';


-- =====================================================================================
-- PHAN 2: MAU QUY TRINH (routing)
-- =====================================================================================
-- 1 mau quy trinh = 1 chuoi cong doan dung cho 1 loai hang (vd "Quy trinh ao thun").
CREATE TABLE production_routings (
    id           bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    code         text        NOT NULL,                 -- ma mau: 'RT-AO', 'RT-IN'...
    name         text        NOT NULL,                 -- ten mau: 'Quy trinh ao thun'
    note         text,
    is_active    boolean     NOT NULL DEFAULT true,
    is_default   boolean     NOT NULL DEFAULT false,   -- mau mac dinh khi tao don
    created_at   timestamptz NOT NULL DEFAULT now(),
    updated_at   timestamptz NOT NULL DEFAULT now(),
    created_by   bigint      REFERENCES users(id),
    CONSTRAINT uq_routing_code UNIQUE (code)
);
COMMENT ON TABLE production_routings IS 'Mau quy trinh san xuat (chuoi cong doan) - don chon 1 mau de sinh buoc.';
CREATE INDEX ix_routings_active ON production_routings (is_active, name);
-- Chi 1 mau duoc dat lam mac dinh.
CREATE UNIQUE INDEX uq_routing_one_default ON production_routings (is_default) WHERE is_default;

-- Cac buoc trong 1 mau: cong doan nao, thu tu bao nhieu, co bat buoc khong.
CREATE TABLE routing_steps (
    id           bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    routing_id   bigint      NOT NULL REFERENCES production_routings(id) ON DELETE CASCADE,
    stage_id     bigint      NOT NULL REFERENCES production_stages(id),
    seq          integer     NOT NULL CHECK (seq > 0),  -- thu tu buoc TRONG mau nay
    is_optional  boolean     NOT NULL DEFAULT false,    -- buoc co the bo qua khi chay don
    note         text,
    CONSTRAINT uq_routing_step UNIQUE (routing_id, stage_id),
    CONSTRAINT uq_routing_seq  UNIQUE (routing_id, seq)
);
COMMENT ON TABLE routing_steps IS 'Cac buoc cua 1 mau quy trinh: cong doan + thu tu + co bat buoc hay khong.';
CREATE INDEX ix_routing_steps_routing ON routing_steps (routing_id, seq);


-- =====================================================================================
-- PHAN 3: DON CHON MAU QUY TRINH
-- =====================================================================================
ALTER TABLE production_orders ADD COLUMN IF NOT EXISTS routing_id bigint REFERENCES production_routings(id);
COMMENT ON COLUMN production_orders.routing_id IS 'Mau quy trinh da chon cho don (sinh buoc theo mau nay).';
CREATE INDEX IF NOT EXISTS ix_po_routing ON production_orders (routing_id) WHERE routing_id IS NOT NULL;


-- =====================================================================================
-- PHAN 4: BUOC CUA DON — cho SUA RIENG (them/bo/doi thu tu)
-- =====================================================================================
-- seq rieng cua tung don: cho phep NHAY BUOC (1,2,4) hoac doi thu tu so voi mau.
ALTER TABLE production_steps ADD COLUMN IF NOT EXISTS seq integer;
COMMENT ON COLUMN production_steps.seq IS 'Thu tu buoc THUC TE cua don nay (cho nhay/doi thu tu). NULL = lay theo stage.seq.';

-- Danh dau buoc bi BO QUA cho don nay (giu ban ghi de biet da chu dong bo, khong phai quen).
ALTER TABLE production_steps ADD COLUMN IF NOT EXISTS is_skipped boolean NOT NULL DEFAULT false;
COMMENT ON COLUMN production_steps.is_skipped IS 'true = buoc nay duoc BO QUA cho don (khong phai lam).';

-- Dien seq cho cac buoc da co (lay theo thu tu cong doan chuan) de du lieu cu nhat quan.
UPDATE production_steps ps
SET seq = st.seq
FROM production_stages st
WHERE st.id = ps.stage_id AND ps.seq IS NULL;

CREATE INDEX IF NOT EXISTS ix_steps_order_seq ON production_steps (production_order_id, seq);


-- =====================================================================================
-- PHAN 5: DU LIEU MAU — quy trinh may mac chuan (tu 4 cong doan da co)
-- =====================================================================================
-- Tao mau mac dinh "Quy trinh may chuan" gom dung 4 buoc dang dung, de du lieu cu chay tiep.
INSERT INTO production_routings (code, name, note, is_default)
VALUES ('RT-MAY', 'Quy trình may chuẩn', 'Cắt vải → May → QC → Nhập kho thành phẩm', true)
ON CONFLICT (code) DO NOTHING;

INSERT INTO routing_steps (routing_id, stage_id, seq, is_optional)
SELECT r.id, st.id, st.seq, false
FROM production_routings r
CROSS JOIN production_stages st
WHERE r.code = 'RT-MAY' AND st.code IN ('CUTTING','SEWING','QC','FG_RECEIPT')
ON CONFLICT (routing_id, stage_id) DO NOTHING;

-- Gan mau mac dinh cho cac don CU chua co routing (giu hanh vi nhu truoc).
UPDATE production_orders po
SET routing_id = (SELECT id FROM production_routings WHERE code = 'RT-MAY')
WHERE po.routing_id IS NULL;


-- =====================================================================================
-- HET V006__flexible_routing.sql
-- =====================================================================================
