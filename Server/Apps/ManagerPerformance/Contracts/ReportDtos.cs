namespace GM95.Server.Apps.ManagerPerformance.Contracts;

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
    string? MaterialUomCode, // Ma DVT NVL (join units_of_measure) - hien canh cac so luong
    string? MaterialUomName, // Ten DVT NVL
    decimal RequiredQty,
    decimal TotalAvailable,
    decimal ShortageQty,
    decimal SuggestedPurchaseQty);

/// <summary>
/// So NVL theo tung ma trong 1 khoang thoi gian: ton dau ky, tong nhap, tong xuat, ton cuoi ky
/// (tinh tu so cai stock_transactions) + ton thuc te hien tai (v_material_stock).
/// </summary>
public sealed record MaterialLedgerDto(
    long MaterialId,
    string Sku,
    string Name,
    string? UomCode,          // ma DVT - hien canh so luong
    string? UomName,          // ten DVT
    bool IsActive,
    decimal OpeningQty,       // ton dau ky (tong so cai truoc 'from')
    decimal InQty,            // tong nhap trong ky (quantity > 0)
    decimal InValue,          // gia tri nhap trong ky (VND)
    decimal OutQty,           // tong xuat trong ky (quantity < 0, doi dau)
    decimal OutValue,         // gia tri xuat trong ky (VND)
    decimal ClosingQty,       // ton cuoi ky = dau ky + nhap - xuat
    decimal CurrentOnHand,    // ton thuc te hien tai (moi kho gop)
    decimal CurrentAvailable);// kha dung hien tai = on_hand - reserved

/// <summary>Thong ke san xuat theo ma hang trong 1 khoang thoi gian.</summary>
public sealed record ProductionSummaryDto(
    long ProductId,
    string Sku,
    string Name,
    string? UomCode,          // DVT cua ma hang
    string? UomName,
    long OrderCount,          // so don tao trong ky
    decimal OrderQty,         // tong SL dat cua cac don tao trong ky
    decimal DefectQty,        // tong SL loi (cong doan) cua cac don tao trong ky
    decimal FgQty,            // thanh pham nhap kho trong ky
    decimal IssueValue);      // gia tri NVL xuat cho cac don cua ma hang trong ky (VND)

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
