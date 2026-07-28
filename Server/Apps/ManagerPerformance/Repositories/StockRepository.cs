using GM95.Server.Apps.ManagerPerformance.Contracts;
using GM95.Server.Infrastructure.Data;

namespace GM95.Server.Apps.ManagerPerformance.Repositories;

public sealed class StockRepository : IStockRepository
{
    public Task<IEnumerable<MaterialStockDto>> ListStockAsync(TenantScope scope, bool lowStockOnly, int? year, int? month)
    {
        // Loc theo thang/nam TAO NVL (join materials) de tab kho khong phai load het 1 luc.
        var sql = "SELECT vs.material_id, vs.sku, vs.name, vs.reorder_level, vs.reorder_quantity, " +
                  "vs.total_on_hand, vs.total_reserved, vs.total_available, vs.is_low_stock, " +
                  "vs.last_unit_cost, vs.avg_unit_cost, vs.stock_value, vs.uom_code, vs.uom_name " +
                  "FROM v_material_stock vs JOIN materials m ON m.id = vs.material_id " +
                  "WHERE (@year IS NULL OR EXTRACT(YEAR FROM m.created_at) = @year) " +
                  "AND (@month IS NULL OR EXTRACT(MONTH FROM m.created_at) = @month)" +
                  (lowStockOnly ? " AND vs.is_low_stock" : "") +
                  " ORDER BY vs.name";
        return scope.QueryAsync<MaterialStockDto>(sql, new { year, month });
    }

    public Task<decimal?> LockOnHandAsync(TenantScope scope, long warehouseId, long materialId) =>
        scope.QueryFirstOrDefaultAsync<decimal?>(
            // FOR UPDATE: khoa dong stock chong race khi 500+ user nhap/xuat dong thoi.
            "SELECT qty_on_hand FROM stock WHERE warehouse_id = @warehouseId AND material_id = @materialId FOR UPDATE",
            new { warehouseId, materialId });

    public Task EnsureStockRowAsync(TenantScope scope, long warehouseId, long materialId) =>
        scope.ExecuteAsync(
            """
            INSERT INTO stock (warehouse_id, material_id, qty_on_hand, qty_reserved)
            VALUES (@warehouseId, @materialId, 0, 0)
            ON CONFLICT (warehouse_id, material_id) DO NOTHING
            """, new { warehouseId, materialId });

    public Task<decimal> AddOnHandAsync(TenantScope scope, long warehouseId, long materialId, decimal delta) =>
        scope.QuerySingleAsync<decimal>(
            """
            UPDATE stock SET qty_on_hand = qty_on_hand + @delta
            WHERE warehouse_id = @warehouseId AND material_id = @materialId
            RETURNING qty_on_hand
            """, new { warehouseId, materialId, delta });

    public Task<long> InsertTransactionAsync(
        TenantScope scope, long warehouseId, long materialId, string txnType,
        decimal quantity, decimal unitCost, decimal balanceAfter, string? refType, long? refId, string? note) =>
        scope.QuerySingleAsync<long>(
            """
            INSERT INTO stock_transactions
                (warehouse_id, material_id, txn_type, quantity, unit_cost, balance_after, ref_type, ref_id, note)
            VALUES (@warehouseId, @materialId, @txnType, @quantity, @unitCost, @balanceAfter, @refType, @refId, @note)
            RETURNING id
            """, new { warehouseId, materialId, txnType, quantity, unitCost, balanceAfter, refType, refId, note });
}
