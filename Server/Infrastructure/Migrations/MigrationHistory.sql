-- Bang theo doi migration da ap dung (tao trong MOI schema tenant).
-- Nguon su that chong chay lai: version la khoa chinh.
CREATE TABLE IF NOT EXISTS __migration_history (
    version     text        PRIMARY KEY,      -- 'V001'
    filename    text        NOT NULL,
    checksum    text        NOT NULL,         -- sha256 noi dung file: phat hien sua file da ap dung
    applied_at  timestamptz NOT NULL DEFAULT now()
);
