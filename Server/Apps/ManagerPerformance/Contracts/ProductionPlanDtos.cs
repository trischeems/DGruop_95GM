namespace GM95.Server.Apps.ManagerPerformance.Contracts;

/// <summary>Ban ghi ke hoach san xuat tra ve client (khop cot production_plans + so don join san).</summary>
public sealed record ProductionPlanDto(
    long Id,
    long ProductionOrderId,
    string? OrderNo,         // So don (join production_orders) - hien thi canh ProductionOrderId
    long ProductionOrderItemId, // Ke hoach cho MAT HANG nao trong don (V007)
    int LineNo,                 // So thu tu mat hang trong don
    string? LineProductSku,     // SKU mat hang — 1 don nhieu hang thi phai biet lap cho hang nao
    string? LineProductName,
    decimal LineQuantity,       // SL dat cua rieng mat hang do
    decimal PlannedQty,
    DateTime? PlannedStart,
    DateTime? PlannedEnd,
    string? LineCode,
    string Status,
    string? Note,
    string? ProductUomCode,  // Ma DVT thanh pham (join units_of_measure qua production_orders.product_id)
    string? ProductUomName); // Ten DVT thanh pham (join units_of_measure qua production_orders.product_id)

/// <summary>Yeu cau tao ke hoach san xuat moi.</summary>
public sealed record CreateProductionPlanRequest(
    long ProductionOrderId,
    long ProductionOrderItemId, // Lap ke hoach cho MAT HANG nao trong don (V007)
    decimal PlannedQty,
    DateTime? PlannedStart,
    DateTime? PlannedEnd,
    string? LineCode,
    string? Note);

/// <summary>Yeu cau cap nhat trang thai ke hoach.</summary>
public sealed record UpdatePlanStatusRequest(string Status);

/// <summary>Yeu cau sua ke hoach (so luong + chuyen + ghi chu).</summary>
public sealed record UpdatePlanRequest(
    decimal PlannedQty,
    string? LineCode,
    string? Note);
