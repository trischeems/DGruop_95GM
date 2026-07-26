namespace DGroup.Server.Apps.ManagerPerformance.Contracts;

/// <summary>Don hang san xuat tra ve client (khop cot bang production_orders + ten product join san).</summary>
public sealed record ProductionOrderDto(
    long Id,
    string OrderNo,
    long ProductId,
    string? ProductSku,      // SKU ma hang (join products) - hien thi canh ProductId
    string? ProductName,     // Ten ma hang (join products)
    string? ProductUomCode,  // Ma DVT thanh pham (join units_of_measure qua products.uom_id)
    string? ProductUomName,  // Ten DVT thanh pham (join units_of_measure qua products.uom_id)
    long? BomId,
    decimal Quantity,
    string Status,
    DateTime? DueDate,
    DateTime? ConfirmedAt,
    long? RoutingId,         // Mau quy trinh da chon cho don (V006)
    string? RoutingName);    // Ten mau quy trinh (join production_routings)

/// <summary>Yeu cau tao don hang san xuat moi (ban nhap DRAFT).</summary>
public sealed record CreateProductionOrderRequest(
    string OrderNo,
    long ProductId,
    long? BomId,
    decimal Quantity,
    DateTime? DueDate,
    string? Note,
    long? RoutingId = null); // Mau quy trinh chon khi tao don (null = lay mau mac dinh)

/// <summary>1 phieu giu cho NVL cho don hang (khop cot material_reservations + ten NVL/kho join san).</summary>
public sealed record ReservationDto(
    long Id,
    long ProductionOrderId,
    long MaterialId,
    string? MaterialSku,     // SKU NVL (join materials) - hien thi canh MaterialId
    string? MaterialName,    // Ten NVL (join materials)
    string? MaterialUomCode, // Ma DVT NVL (join units_of_measure) - hien thi canh QtyReserved
    string? MaterialUomName, // Ten DVT NVL (join units_of_measure)
    long WarehouseId,
    string? WarehouseName,   // Ten kho (join warehouses) - hien thi canh WarehouseId
    decimal QtyReserved,
    string Status);

/// <summary>Yeu cau sua don hang (chi don DRAFT).</summary>
public sealed record UpdateProductionOrderRequest(
    decimal Quantity,
    DateTime? DueDate,
    string? Note);

/// <summary>Anh huong khi xoa don: trang thai + dem du lieu con (ke hoach/cong doan/giu cho/phieu).</summary>
public sealed record OrderImpactDto(
    string Status,
    string OrderNo,
    long PlanCount,
    long StepCount,
    long ReservationCount,
    long IssueCount,
    long ReceiptCount,
    long LossReportCount,
    bool CanDelete);
