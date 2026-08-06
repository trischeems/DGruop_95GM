namespace GM95.Server.Apps.ManagerPerformance.Contracts;

/// <summary>
/// Don hang san xuat (HEADER) tra ve client.
/// Tu V007 mot don chua NHIEU DONG (nhieu ma hang) — cac cot ProductId/ProductSku/BomId o day
/// la MA HANG DAI DIEN (dong dau tien) va Quantity la TONG SL cac dong; chi tiet nam o
/// <see cref="ProductionOrderItemDto"/>. Giu nguyen cac cot cu de man hinh/bao cao tong quan chay tiep.
/// </summary>
public sealed record ProductionOrderDto(
    long Id,
    string OrderNo,
    long ProductId,
    string? ProductSku,      // SKU ma hang dai dien (dong dau tien)
    string? ProductName,     // Ten ma hang dai dien
    string? ProductUomCode,  // Ma DVT thanh pham (join units_of_measure qua products.uom_id)
    string? ProductUomName,  // Ten DVT thanh pham
    long? BomId,
    decimal Quantity,        // TONG SL cua moi dong trong don
    string Status,
    DateTime? DueDate,
    DateTime? ConfirmedAt,
    long? RoutingId,         // Mau quy trinh dai dien (V006)
    string? RoutingName,     // Ten mau quy trinh
    int LineCount,           // So dong (ma hang) trong don — >1 la don nhieu mat hang
    bool AllStepsDone);      // Da xong het cong doan chua (chua nhap kho TP thi don van IN_PROGRESS)

/// <summary>1 DONG don: 1 ma hang + so luong + BOM/quy trinh rieng (bang production_order_items).</summary>
public sealed record ProductionOrderItemDto(
    long Id,
    long ProductionOrderId,
    int LineNo,
    long ProductId,
    string? ProductSku,      // SKU ma hang (join products)
    string? ProductName,     // Ten ma hang
    string? ProductUomCode,  // Ma DVT thanh pham
    string? ProductUomName,  // Ten DVT thanh pham
    long? BomId,
    int? BomVersion,         // Phien ban BOM da chot (join bom)
    long? RoutingId,
    string? RoutingName,     // Ten mau quy trinh cua dong (join production_routings)
    decimal Quantity,
    string? Note,
    decimal ReceivedQty,     // TP da nhap kho cua RIENG dong nay
    int StepCount,           // So cong doan da khoi tao cho dong
    bool AllStepsDone);      // Moi cong doan (khong tinh bo qua/huy) da DONE

/// <summary>1 dong ma hang khi TAO / THEM vao don.</summary>
public sealed record OrderItemInput(
    long ProductId,
    long? BomId,
    long? RoutingId,
    decimal Quantity,
    string? Note);

/// <summary>
/// Yeu cau tao don hang san xuat moi (ban nhap DRAFT) voi 1 hoac NHIEU dong ma hang.
/// </summary>
public sealed record CreateProductionOrderRequest(
    string OrderNo,
    DateTime? DueDate,
    string? Note,
    IEnumerable<OrderItemInput> Items);

/// <summary>Yeu cau sua 1 dong cua don (chi khi don con DRAFT).</summary>
public sealed record UpdateOrderItemRequest(
    decimal Quantity,
    long? BomId,
    long? RoutingId,
    string? Note);

/// <summary>1 phieu giu cho NVL cho don hang (khop cot material_reservations + ten NVL/kho join san).</summary>
public sealed record ReservationDto(
    long Id,
    long ProductionOrderId,
    long ProductionOrderItemId,  // Giu cho thuoc DONG nao cua don (V007)
    int LineNo,                  // So thu tu dong (join production_order_items)
    string? LineProductSku,      // SKU ma hang cua dong — de biet giu cho cho mat hang nao
    long MaterialId,
    string? MaterialSku,     // SKU NVL (join materials) - hien thi canh MaterialId
    string? MaterialName,    // Ten NVL (join materials)
    string? MaterialUomCode, // Ma DVT NVL (join units_of_measure) - hien thi canh QtyReserved
    string? MaterialUomName, // Ten DVT NVL (join units_of_measure)
    long WarehouseId,
    string? WarehouseName,   // Ten kho (join warehouses) - hien thi canh WarehouseId
    decimal QtyReserved,
    string Status);

/// <summary>Yeu cau sua don hang (chi don DRAFT) — phan HEADER.</summary>
public sealed record UpdateProductionOrderRequest(
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
