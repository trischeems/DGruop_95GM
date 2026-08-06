namespace GM95.Server.Apps.ManagerPerformance.Contracts;

/// <summary>Phieu nhap kho thanh pham (khop finished_goods_receipts + ten product/kho/don join san).</summary>
public sealed record FinishedGoodsReceiptDto(
    long Id,
    string ReceiptNo,
    long ProductionOrderId,
    string? OrderNo,         // So don (join production_orders)
    long ProductionOrderItemId, // Nhap kho cho MAT HANG nao trong don (V007)
    int LineNo,                 // So thu tu mat hang trong don
    long ProductId,
    string? ProductSku,      // SKU ma hang (join products) - hien thi canh ProductId
    string? ProductName,     // Ten ma hang (join products)
    string? ProductUomCode,  // Ma DVT thanh pham (join units_of_measure qua products.uom_id)
    string? ProductUomName,  // Ten DVT thanh pham (join units_of_measure qua products.uom_id)
    long WarehouseId,
    string? WarehouseName,   // Ten kho (join warehouses) - hien thi canh WarehouseId
    decimal QtyReceived,
    DateTime ReceivedAt);

/// <summary>Yeu cau nhap kho thanh pham tu 1 lenh san xuat.</summary>
public sealed record CreateFinishedGoodsRequest(
    long ProductionOrderId,
    long ProductionOrderItemId, // Nhap kho cho MAT HANG nao trong don (V007)
    long ProductId,
    long WarehouseId,
    decimal QtyReceived,
    string? Note);

/// <summary>Anh huong khi xoa phieu nhap TP: trang thai don + hao hut se lech.</summary>
public sealed record FinishedGoodsImpactDto(
    string ReceiptNo,
    decimal QtyReceived,
    string OrderStatus,
    long OrderReceiptCount,
    long LossReportCount,
    bool WillRevertOrder);
