using GM95.Server.Apps.ManagerPerformance.Contracts;
using GM95.Server.Infrastructure.Data;

namespace GM95.Server.Apps.ManagerPerformance.Repositories;

public sealed class ProductionStepRepository : IProductionStepRepository
{
    public Task<int> InitStepsAsync(TenantScope scope, long orderId) =>
        scope.ExecuteAsync(
            // Sinh buoc theo MAU QUY TRINH cua don (V006): uu tien routing_id cua don,
            // chua chon thi lay mau mac dinh. Buoc da co (UNIQUE order+stage) thi bo qua.
            """
            INSERT INTO production_steps (production_order_id, stage_id, seq, status)
            SELECT @OrderId, rs.stage_id, rs.seq, 'PENDING'
            FROM routing_steps rs
            WHERE rs.routing_id = COALESCE(
                    (SELECT routing_id FROM production_orders WHERE id = @OrderId),
                    (SELECT id FROM production_routings WHERE is_default AND is_active LIMIT 1))
            ON CONFLICT (production_order_id, stage_id) DO NOTHING
            """, new { OrderId = orderId });

    public Task<IEnumerable<ProductionStepDto>> ListByOrderAsync(TenantScope scope, long orderId) =>
        scope.QueryAsync<ProductionStepDto>(
            """
            SELECT ps.id, ps.production_order_id, ps.stage_id,
                   st.code AS stage_code, st.name AS stage_name,
                   COALESCE(ps.seq, st.seq) AS seq,
                   ps.status, ps.qty_in, ps.qty_out, ps.qty_defect,
                   ps.started_at, ps.finished_at, ps.note, ps.is_skipped,
                   pu.code AS product_uom_code, pu.name AS product_uom_name
            FROM production_steps ps
            JOIN production_stages st ON st.id = ps.stage_id
            LEFT JOIN production_orders po ON po.id = ps.production_order_id
            LEFT JOIN products p ON p.id = po.product_id
            LEFT JOIN units_of_measure pu ON pu.id = p.uom_id
            WHERE ps.production_order_id = @OrderId
            ORDER BY COALESCE(ps.seq, st.seq), st.name
            """, new { OrderId = orderId });

    public async Task<(string Status, DateTime? StartedAt)?> LockStepAsync(TenantScope scope, long id)
    {
        // FOR UPDATE: khoa dong buoc chong race khi 500+ user cap nhat dong thoi.
        var row = await scope.QueryFirstOrDefaultAsync<StepLockRow>(
            "SELECT status, started_at FROM production_steps WHERE id = @Id FOR UPDATE",
            new { Id = id });
        return row is null ? null : (row.Status, row.StartedAt);
    }

    // Doc order id cua buoc KHONG khoa (de khoa dong don TRUOC roi moi khoa buoc — chong deadlock).
    public Task<long?> GetStepOrderIdAsync(TenantScope scope, long id) =>
        scope.QueryFirstOrDefaultAsync<long?>(
            "SELECT production_order_id FROM production_steps WHERE id = @Id", new { Id = id });

    // Khoa dong DON (FOR UPDATE) — moi giao dich ghi lien quan don PHAI khoa dong nay TRUOC
    // (thu tu khoa toan cuc: don -> ke hoach -> cong doan) de 2 chieu plan<->step khong deadlock.
    public Task<int> LockOrderAsync(TenantScope scope, long orderId) =>
        scope.ExecuteAsync(
            "SELECT id FROM production_orders WHERE id = @orderId FOR UPDATE", new { orderId });

    // Tong TP da nhap kho cua don — chan ha SL ra cua QC xuong duoi so TP da nhap.
    public Task<decimal> SumFinishedGoodsAsync(TenantScope scope, long orderId) =>
        scope.ExecuteScalarAsync<decimal>(
            "SELECT COALESCE(SUM(qty_received), 0) FROM finished_goods_receipts WHERE production_order_id = @orderId",
            new { orderId });

    public Task<StepContextRow?> LockStepContextAsync(TenantScope scope, long id) =>
        scope.QueryFirstOrDefaultAsync<StepContextRow>(
            """
            SELECT ps.production_order_id, st.code AS stage_code,
                   COALESCE(ps.seq, st.seq) AS seq, ps.status
            FROM production_steps ps
            JOIN production_stages st ON st.id = ps.stage_id
            WHERE ps.id = @Id
            FOR UPDATE OF ps
            """, new { Id = id });

    // Day don CONFIRMED -> IN_PROGRESS (khi cong doan bat dau chay). Chi doi khi dang CONFIRMED.
    public Task<int> MarkOrderInProgressAsync(TenantScope scope, long orderId) =>
        scope.ExecuteAsync(
            "UPDATE production_orders SET status = 'IN_PROGRESS', updated_at = now() WHERE id = @orderId AND status = 'CONFIRMED'",
            new { orderId });

    // Day ke hoach PLANNED/RELEASED -> IN_PROGRESS khi bat dau san xuat.
    public Task<int> MarkPlansInProgressAsync(TenantScope scope, long orderId) =>
        scope.ExecuteAsync(
            """
            UPDATE production_plans SET status = 'IN_PROGRESS', updated_at = now()
            WHERE production_order_id = @orderId AND status IN ('PLANNED', 'RELEASED')
            """, new { orderId });

    // Day ke hoach IN_PROGRESS -> DONE khi cong doan cuoi hoan tat.
    public Task<int> MarkPlansDoneAsync(TenantScope scope, long orderId) =>
        scope.ExecuteAsync(
            "UPDATE production_plans SET status = 'DONE', updated_at = now() WHERE production_order_id = @orderId AND status = 'IN_PROGRESS'",
            new { orderId });

    public Task<int> UpdateStepAsync(TenantScope scope, long id, UpdateStepRequest req) =>
        scope.ExecuteAsync(
            """
            UPDATE production_steps SET
                status = @Status,
                qty_in = @QtyIn,
                qty_out = @QtyOut,
                qty_defect = @QtyDefect,
                note = COALESCE(@Note, note),
                started_at = CASE
                    WHEN @Status = 'IN_PROGRESS' AND started_at IS NULL THEN now()
                    ELSE started_at END,
                finished_at = CASE
                    WHEN @Status = 'DONE' THEN COALESCE(finished_at, now())
                    WHEN @Status IN ('PENDING', 'IN_PROGRESS', 'ON_HOLD') THEN NULL
                    ELSE finished_at END,
                updated_at = now()
            WHERE id = @Id
            """,
            new { Id = id, req.Status, req.QtyIn, req.QtyOut, req.QtyDefect, req.Note });

    // Ke hoach DONE -> keo tien do moi cong doan len TONG SL cac ke hoach da DONE (khong nhap tay nua).
    // Dung GREATEST (khong cong don): chay lai khong phong so, tron voi so lieu nhap tay khong dem trung.
    // qty_out chua tinh phan loi da ghi (out' = doneQty - defect) nen CHECK (out + defect <= in) van thoa.
    // Cong doan da DONE thu cong khong bi ha xuong IN_PROGRESS.
    public Task<int> AutoProgressStepsAsync(TenantScope scope, long orderId, decimal doneQty, decimal orderQty) =>
        scope.ExecuteAsync(
            """
            UPDATE production_steps SET
                -- qty_in phai phu ca (qty_out + qty_defect) cu: dong cu tao qua lo hong vao=0
                -- co the co out/loi > 0 — khong phu thi CHECK ck_step_out_le_in no giua transaction.
                qty_in  = GREATEST(qty_in,  @DoneQty, qty_out + qty_defect),
                qty_out = GREATEST(qty_out, @DoneQty - qty_defect),
                status = CASE
                    WHEN GREATEST(qty_in, @DoneQty) >= @OrderQty THEN 'DONE'
                    WHEN status = 'DONE' THEN 'DONE'
                    ELSE 'IN_PROGRESS' END,
                started_at = COALESCE(started_at, now()),
                finished_at = CASE
                    WHEN GREATEST(qty_in, @DoneQty) >= @OrderQty OR status = 'DONE' THEN COALESCE(finished_at, now())
                    ELSE NULL END,
                updated_at = now()
            WHERE production_order_id = @OrderId
              AND status <> 'CANCELLED'
              AND NOT is_skipped
            """, new { OrderId = orderId, DoneQty = doneQty, OrderQty = orderQty });

    // Tong SL cac ke hoach da DONE cua don (thuoc do tien do tu dong).
    public Task<decimal> SumDonePlannedQtyAsync(TenantScope scope, long orderId) =>
        scope.ExecuteScalarAsync<decimal>(
            "SELECT COALESCE(SUM(planned_qty), 0) FROM production_plans WHERE production_order_id = @orderId AND status = 'DONE'",
            new { orderId });

    /// <summary>Dong tam de doc khi khoa buoc (FOR UPDATE).</summary>
    private sealed record StepLockRow(string Status, DateTime? StartedAt);
}
