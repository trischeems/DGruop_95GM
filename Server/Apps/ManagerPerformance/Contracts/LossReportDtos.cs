namespace DGroup.Server.Apps.ManagerPerformance.Contracts;

/// <summary>
/// 1 dong doi chieu cap phat vs dinh muc cho 1 NVL cua 1 don san xuat
/// (khop cot bang loss_reports). qty_variance = qty_issued - qty_standard (DB tu tinh).
/// </summary>
public sealed record LossReportDto(
    long Id,
    long ProductionOrderId,
    string? OrderNo,         // So don (join production_orders)
    long MaterialId,
    string? MaterialSku,     // SKU NVL (join materials) - hien thi canh MaterialId
    string? MaterialName,    // Ten NVL (join materials)
    decimal QtyIssued,
    decimal QtyStandard,
    decimal QtyVariance,
    decimal FinishedQty);
