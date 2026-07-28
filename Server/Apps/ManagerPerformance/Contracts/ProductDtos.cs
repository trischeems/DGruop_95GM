namespace GM95.Server.Apps.ManagerPerformance.Contracts;

/// <summary>Ban ghi ma hang thanh pham tra ve client (khop cot products + ten DVT join san).</summary>
public sealed record ProductDto(
    long Id,
    string Sku,
    string Name,
    long UomId,
    string? UomCode,         // Ma DVT (join units_of_measure) - hien thi canh UomId
    string? UomName,         // Ten DVT (join units_of_measure)
    bool IsActive);

/// <summary>Yeu cau tao ma hang moi.</summary>
public sealed record CreateProductRequest(
    string Sku,
    string Name,
    long UomId);

/// <summary>Yeu cau cap nhat ma hang.</summary>
public sealed record UpdateProductRequest(
    string Name,
    long UomId,
    bool IsActive);

/// <summary>Anh huong khi xoa ma hang: dem BOM/don/phieu TP dang tham chieu.</summary>
public sealed record ProductImpactDto(
    long BomCount,
    long OrderCount,
    long ReceiptCount,
    bool CanDelete);
