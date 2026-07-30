using GM95.Server.Apps.ManagerPerformance.Contracts;
using GM95.Server.Infrastructure.Data;

namespace GM95.Server.Apps.ManagerPerformance.Repositories;

/// <summary>Doc cac view nghiep vu (san luong toi da, nhu cau NVL/don, ton thap).</summary>
public interface IReportRepository
{
    Task<IEnumerable<MaxOutputByProductDto>> MaxOutputByProductAsync(TenantScope scope);
    Task<MaxOutputByProductDto?> MaxOutputForProductAsync(TenantScope scope, long productId);
    Task<IEnumerable<OrderMaterialRequirementDto>> OrderRequirementsAsync(TenantScope scope, long? orderId);
    Task<IEnumerable<MaterialStockDto>> LowStockAsync(TenantScope scope, int? year, int? month);
    /// <summary>Tong hop so lieu 12 thang cua 1 nam.</summary>
    Task<IEnumerable<MonthlyStatsDto>> MonthlyStatsAsync(TenantScope scope, int year);

    /// <summary>So NVL theo tung ma trong khoang UTC [fromUtc, toExclusiveUtc): ton dau/nhap/xuat/ton cuoi.</summary>
    Task<IEnumerable<MaterialLedgerDto>> MaterialLedgerAsync(TenantScope scope, DateTime fromUtc, DateTime toExclusiveUtc);

    /// <summary>Thong ke san xuat theo ma hang trong khoang UTC [fromUtc, toExclusiveUtc).</summary>
    Task<IEnumerable<ProductionSummaryDto>> ProductionSummaryAsync(TenantScope scope, DateTime fromUtc, DateTime toExclusiveUtc);
}
