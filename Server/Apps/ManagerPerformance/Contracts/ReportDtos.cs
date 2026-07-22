namespace DGroup.Server.Apps.ManagerPerformance.Contracts;

/// <summary>San luong toi da moi ma hang + NVL nut that (view v_max_output_by_product + ten join san).</summary>
public sealed record MaxOutputByProductDto(
    long ProductId,
    string? ProductSku,               // SKU ma hang (join products) - canh ProductId
    string? ProductName,              // Ten ma hang (join products)
    long BomId,
    decimal MaxProducibleQty,
    long BottleneckMaterialId,
    string? BottleneckMaterialSku,    // SKU NVL nut that (join materials) - canh BottleneckMaterialId
    string? BottleneckMaterialName,   // Ten NVL nut that (join materials)
    decimal BottleneckMaterialOutput);

/// <summary>Nhu cau NVL cua don vs ton kha dung (view v_order_material_requirement + ten NVL join san).</summary>
public sealed record OrderMaterialRequirementDto(
    long ProductionOrderId,
    string OrderNo,
    string OrderStatus,
    long MaterialId,
    string? MaterialSku,     // SKU NVL (join materials) - hien thi canh MaterialId
    string? MaterialName,    // Ten NVL (join materials)
    decimal RequiredQty,
    decimal TotalAvailable,
    decimal ShortageQty,
    decimal SuggestedPurchaseQty);

/// <summary>So lieu tong hop 1 thang (phuc vu tab So sanh thang o app).</summary>
public sealed record MonthlyStatsDto(
    int Month,
    long OrderCount,        // so don tao trong thang
    decimal OrderQty,       // tong SL dat cua cac don tao trong thang
    decimal StockInQty,     // NVL nhap kho (RECEIPT)
    decimal StockInValue,   // gia tri NVL nhap (VND)
    decimal StockOutQty,    // NVL xuat kho (ISSUE)
    decimal StockOutValue,  // gia tri NVL xuat (VND)
    long FgReceiptCount,    // so phieu nhap TP
    decimal FgQty,          // tong TP nhap kho
    decimal LossVariance,   // tong hao hut (cap phat - dinh muc)
    long AlertCount);       // so canh bao phat sinh
