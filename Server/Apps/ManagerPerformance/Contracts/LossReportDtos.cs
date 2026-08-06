namespace GM95.Server.Apps.ManagerPerformance.Contracts;

/// <summary>
/// 1 dong doi chieu cap phat vs dinh muc cho 1 NVL cua 1 don san xuat
/// (khop cot bang loss_reports). qty_variance = qty_issued - qty_standard (DB tu tinh).
/// </summary>
public sealed record LossReportDto(
    long Id,
    long ProductionOrderId,
    string? OrderNo,         // So don (join production_orders)
    long ProductionOrderItemId, // Hao hut cua MAT HANG nao trong don (V007)
    int LineNo,                 // So thu tu mat hang trong don
    string? LineProductSku,     // SKU mat hang — 1 don nhieu hang thi tach hao hut theo tung hang
    string? LineProductName,
    long MaterialId,
    string? MaterialSku,     // SKU NVL (join materials) - hien thi canh MaterialId
    string? MaterialName,    // Ten NVL (join materials)
    string? MaterialUomCode, // Ma DVT NVL (join units_of_measure qua materials.uom_id)
    string? MaterialUomName, // Ten DVT NVL
    decimal QtyIssued,
    decimal QtyStandard,
    decimal QtyVariance,
    decimal FinishedQty,
    string? ProductUomCode,  // Ma DVT thanh pham (join units_of_measure qua products.uom_id)
    string? ProductUomName); // Ten DVT thanh pham
