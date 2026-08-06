using GM95.Server.Apps.ManagerPerformance.Contracts;
using GM95.Server.Infrastructure.Data;

namespace GM95.Server.Apps.ManagerPerformance.Repositories;

public sealed class ProductionStepRepository : IProductionStepRepository
{
    // Sinh buoc cho MOI MAT HANG cua don theo mau quy trinh RIENG cua tung mat hang (V007):
    // uu tien routing_id cua dong, chua chon thi lay mau mac dinh. Buoc da co thi bo qua.
    public Task<int> InitStepsAsync(TenantScope scope, long orderId) =>
        scope.ExecuteAsync(
            """
            INSERT INTO production_steps (production_order_id, production_order_item_id, stage_id, seq, status)
            SELECT i.production_order_id, i.id, rs.stage_id, rs.seq, 'PENDING'
            FROM production_order_items i
            JOIN routing_steps rs ON rs.routing_id = COALESCE(
                    i.routing_id,
                    (SELECT id FROM production_routings WHERE is_default AND is_active LIMIT 1))
            WHERE i.production_order_id = @OrderId
            ON CONFLICT (production_order_item_id, stage_id) DO NOTHING
            """, new { OrderId = orderId });

    // Sinh buoc cho RIENG 1 mat hang (khi them mat hang moi vao don da khoi tao cong doan).
    public Task<int> InitStepsForItemAsync(TenantScope scope, long orderItemId) =>
        scope.ExecuteAsync(
            """
            INSERT INTO production_steps (production_order_id, production_order_item_id, stage_id, seq, status)
            SELECT i.production_order_id, i.id, rs.stage_id, rs.seq, 'PENDING'
            FROM production_order_items i
            JOIN routing_steps rs ON rs.routing_id = COALESCE(
                    i.routing_id,
                    (SELECT id FROM production_routings WHERE is_default AND is_active LIMIT 1))
            WHERE i.id = @ItemId
            ON CONFLICT (production_order_item_id, stage_id) DO NOTHING
            """, new { ItemId = orderItemId });

    // SELECT chung: cong doan + ten cong doan + MAT HANG cua dong (de biet dang lam cho hang nao).
    private const string StepSelect =
        """
        SELECT ps.id, ps.production_order_id, ps.production_order_item_id,
               i.line_no, lp.sku AS line_product_sku, lp.name AS line_product_name, i.quantity AS line_quantity,
               ps.stage_id, st.code AS stage_code, st.name AS stage_name,
               COALESCE(ps.seq, st.seq) AS seq,
               ps.status, ps.qty_in, ps.qty_out, ps.qty_defect,
               ps.started_at, ps.finished_at, ps.note, ps.is_skipped,
               pu.code AS product_uom_code, pu.name AS product_uom_name
        FROM production_steps ps
        JOIN production_stages st ON st.id = ps.stage_id
        LEFT JOIN production_order_items i ON i.id = ps.production_order_item_id
        LEFT JOIN products lp ON lp.id = i.product_id
        LEFT JOIN units_of_measure pu ON pu.id = lp.uom_id
        """;

    public Task<IEnumerable<ProductionStepDto>> ListByOrderAsync(TenantScope scope, long orderId) =>
        scope.QueryAsync<ProductionStepDto>(
            $"{StepSelect} WHERE ps.production_order_id = @OrderId " +
            "ORDER BY i.line_no, COALESCE(ps.seq, st.seq), st.name",
            new { OrderId = orderId });

    /// <summary>Cong doan cua RIENG 1 mat hang trong don.</summary>
    public Task<IEnumerable<ProductionStepDto>> ListByItemAsync(TenantScope scope, long orderItemId) =>
        scope.QueryAsync<ProductionStepDto>(
            $"{StepSelect} WHERE ps.production_order_item_id = @ItemId " +
            "ORDER BY COALESCE(ps.seq, st.seq), st.name",
            new { ItemId = orderItemId });

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

    // Tong TP da nhap kho cua RIENG 1 MAT HANG — chan ha SL ra cua QC xuong duoi so da nhap.
    // Phai tinh theo mat hang (V007): don nhieu mat hang thi so cua mat hang khac khong lien quan.
    public Task<decimal> SumFinishedGoodsForItemAsync(TenantScope scope, long orderItemId) =>
        scope.ExecuteScalarAsync<decimal>(
            "SELECT COALESCE(SUM(qty_received), 0) FROM finished_goods_receipts " +
            "WHERE production_order_item_id = @orderItemId",
            new { orderItemId });

    public Task<StepContextRow?> LockStepContextAsync(TenantScope scope, long id) =>
        scope.QueryFirstOrDefaultAsync<StepContextRow>(
            """
            SELECT ps.production_order_id, ps.production_order_item_id, st.code AS stage_code,
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

    // Day MOI ke hoach chua ket thuc (PLANNED/RELEASED/IN_PROGRESS) -> DONE khi don da chay xong.
    // Truoc day chi bat IN_PROGRESS nen ke hoach PLANNED/RELEASED bi ket lai mai mai.
    // Bo qua DONE/CANCELLED (trang thai ket thuc) -> chay lai nhieu lan khong doi gi them.
    public Task<int> MarkPlansDoneAsync(TenantScope scope, long orderId) =>
        scope.ExecuteAsync(
            """
            UPDATE production_plans SET status = 'DONE', updated_at = now()
            WHERE production_order_id = @orderId AND status NOT IN ('DONE', 'CANCELLED')
            """, new { orderId });

    // Don da chay xong het chua? 1 truy van scalar duy nhat:
    //   - EXISTS: phai co it nhat 1 cong doan PHAI LAM (khong bo qua, khong huy) — neu khong,
    //     don chua khoi tao cong doan / bo qua het se bi ket luan "xong" sai.
    //   - NOT EXISTS: khong con cong doan phai lam nao dang do (PENDING/IN_PROGRESS/ON_HOLD).
    public Task<bool> AreAllStepsDoneAsync(TenantScope scope, long orderId) =>
        scope.ExecuteScalarAsync<bool>(
            """
            SELECT EXISTS (SELECT 1 FROM production_steps
                           WHERE production_order_id = @orderId
                             AND NOT is_skipped AND status <> 'CANCELLED')
               AND NOT EXISTS (SELECT 1 FROM production_steps
                               WHERE production_order_id = @orderId
                                 AND NOT is_skipped AND status NOT IN ('DONE', 'CANCELLED'))
            """, new { orderId });

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

    // Keo tien do cong doan cua RIENG 1 MAT HANG len muc doneQty (tong SL ke hoach DONE cua mat hang do).
    // Dung GREATEST — khong cong don, chay lai khong phong so, khong dem trung voi so nhap tay.
    public Task<int> AutoProgressItemStepsAsync(TenantScope scope, long orderItemId, decimal doneQty, decimal itemQty) =>
        scope.ExecuteAsync(
            """
            UPDATE production_steps SET
                qty_in  = GREATEST(qty_in,  @DoneQty, qty_out + qty_defect),
                qty_out = GREATEST(qty_out, @DoneQty - qty_defect),
                status = CASE
                    WHEN GREATEST(qty_in, @DoneQty) >= @ItemQty THEN 'DONE'
                    WHEN status = 'DONE' THEN 'DONE'
                    ELSE 'IN_PROGRESS' END,
                started_at = COALESCE(started_at, now()),
                finished_at = CASE
                    WHEN GREATEST(qty_in, @DoneQty) >= @ItemQty OR status = 'DONE' THEN COALESCE(finished_at, now())
                    ELSE NULL END,
                updated_at = now()
            WHERE production_order_item_id = @ItemId
              AND status <> 'CANCELLED'
              AND NOT is_skipped
            """, new { ItemId = orderItemId, DoneQty = doneQty, ItemQty = itemQty });

    // Tong SL cac ke hoach da DONE cua RIENG 1 mat hang.
    public Task<decimal> SumDonePlannedQtyForItemAsync(TenantScope scope, long orderItemId) =>
        scope.ExecuteScalarAsync<decimal>(
            "SELECT COALESCE(SUM(planned_qty), 0) FROM production_plans " +
            "WHERE production_order_item_id = @orderItemId AND status = 'DONE'",
            new { orderItemId });

    // Tong SL cac ke hoach da DONE cua don (thuoc do tien do tu dong).
    public Task<decimal> SumDonePlannedQtyAsync(TenantScope scope, long orderId) =>
        scope.ExecuteScalarAsync<decimal>(
            "SELECT COALESCE(SUM(planned_qty), 0) FROM production_plans WHERE production_order_id = @orderId AND status = 'DONE'",
            new { orderId });

    /// <summary>Dong tam de doc khi khoa buoc (FOR UPDATE).</summary>
    private sealed record StepLockRow(string Status, DateTime? StartedAt);
}
