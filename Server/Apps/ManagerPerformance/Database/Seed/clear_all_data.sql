-- =====================================================================================
-- clear_all_data.sql  —  XOA SACH du lieu giao dich cua tenant (de database TRONG).
-- GIU nguyen reference da seed san (warehouses, units_of_measure,
--   material_categories, production_stages) va bang __migration_history.
-- Xoa theo thu tu con->cha (KHONG dung TRUNCATE CASCADE de khong dung warehouses).
-- Dung boi run_all_trong.bat. Truyen -v tenant=<schema> (mac dinh public).
-- =====================================================================================
\if :{?tenant}
\else
  \set tenant public
\endif
SET search_path TO :"tenant";
\echo Xoa sach du lieu tenant: :tenant
BEGIN;

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

-- Reset identity de id bat dau lai tu 1.
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

COMMIT;

\echo Da xoa sach. Cac bang giao dich hien TRONG.
