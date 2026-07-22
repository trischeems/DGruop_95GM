using DGroup.Server.Apps.ManagerPerformance.Contracts;
using DGroup.Server.Infrastructure.Data;

namespace DGroup.Server.Apps.ManagerPerformance.Repositories;

public sealed class AlertRepository : IAlertRepository
{
    public Task<IEnumerable<AlertDto>> ListAsync(TenantScope scope, string status, int? year, int? month) =>
        scope.QueryAsync<AlertDto>(
            """
            SELECT id, alert_type, severity, entity_type, entity_id, message, status, created_at
            FROM alerts
            WHERE status = @status
              AND (@year IS NULL OR EXTRACT(YEAR FROM created_at) = @year)
              AND (@month IS NULL OR EXTRACT(MONTH FROM created_at) = @month)
            ORDER BY created_at DESC
            """, new { status, year, month });

    public Task<int> GenerateLowStockAsync(TenantScope scope) =>
        scope.ExecuteAsync(
            // INSERT ... SELECT ... WHERE NOT EXISTS: chong trung canh bao OPEN cung entity.
            """
            INSERT INTO alerts (alert_type, severity, entity_type, entity_id, message, status)
            SELECT 'LOW_STOCK', 'WARN', 'MATERIAL', v.material_id,
                   'Ton thap: ' || v.sku || ' con ' || v.total_available, 'OPEN'
            FROM v_material_stock v
            WHERE v.is_low_stock
              AND NOT EXISTS (
                  SELECT 1 FROM alerts a
                  WHERE a.status = 'OPEN'
                    AND a.alert_type = 'LOW_STOCK'
                    AND a.entity_type = 'MATERIAL'
                    AND a.entity_id = v.material_id)
            """);

    public Task<int> GenerateOrderShortageAsync(TenantScope scope) =>
        scope.ExecuteAsync(
            // INSERT ... SELECT ... WHERE NOT EXISTS: chong trung canh bao OPEN cung don hang.
            // DISTINCT ON (production_order_id): 1 don co the thieu nhieu NVL -> chi tao 1 canh bao/don.
            """
            INSERT INTO alerts (alert_type, severity, entity_type, entity_id, message, status)
            SELECT DISTINCT ON (v.production_order_id)
                   'ORDER_SHORTAGE', 'CRITICAL', 'PRODUCTION_ORDER', v.production_order_id,
                   'Don ' || v.order_no || ' thieu NVL ' || v.material_id || ' (' || v.shortage_qty || ')', 'OPEN'
            FROM v_order_material_requirement v
            WHERE v.shortage_qty > 0
              AND NOT EXISTS (
                  SELECT 1 FROM alerts a
                  WHERE a.status = 'OPEN'
                    AND a.alert_type = 'ORDER_SHORTAGE'
                    AND a.entity_type = 'PRODUCTION_ORDER'
                    AND a.entity_id = v.production_order_id)
            ORDER BY v.production_order_id, v.material_id
            """);

    public Task<int> AcknowledgeAsync(TenantScope scope, long id) =>
        scope.ExecuteAsync(
            "UPDATE alerts SET status = 'ACKNOWLEDGED' WHERE id = @id",
            new { id });

    public Task<int> ResolveAsync(TenantScope scope, long id) =>
        scope.ExecuteAsync(
            "UPDATE alerts SET status = 'RESOLVED', resolved_at = now() WHERE id = @id",
            new { id });
}
