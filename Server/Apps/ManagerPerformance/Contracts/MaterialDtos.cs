namespace DGroup.Server.Apps.ManagerPerformance.Contracts;

/// <summary>Ban ghi NVL tra ve client (khop cot bang materials).</summary>
public sealed record MaterialDto(
    long Id,
    string Sku,
    string Name,
    long? CategoryId,
    long UomId,
    decimal ReorderLevel,
    decimal ReorderQuantity,
    decimal StandardCost,
    bool IsActive);

/// <summary>Yeu cau tao NVL moi.</summary>
public sealed record CreateMaterialRequest(
    string Sku,
    string Name,
    long UomId,
    long? CategoryId,
    decimal ReorderLevel,
    decimal ReorderQuantity,
    decimal StandardCost);

/// <summary>Yeu cau cap nhat NVL.</summary>
public sealed record UpdateMaterialRequest(
    string Name,
    long? CategoryId,
    decimal ReorderLevel,
    decimal ReorderQuantity,
    decimal StandardCost,
    bool IsActive);

/// <summary>Anh huong khi xoa NVL: dem cac noi dang tham chieu (phuc vu popup canh bao o app).</summary>
public sealed record MaterialImpactDto(
    long StockRowCount,
    decimal TotalOnHand,
    long BomItemCount,
    long ReservationCount,
    long IssueLineCount,
    long TransactionCount,
    long LossReportCount,
    bool CanDelete);
