namespace DGroup.Server.Apps.ManagerPerformance.Contracts;

/// <summary>Ton kha dung gop moi kho theo NVL (khop view v_material_stock).</summary>
public sealed record MaterialStockDto(
    long MaterialId,
    string Sku,
    string Name,
    decimal ReorderLevel,
    decimal ReorderQuantity,
    decimal TotalOnHand,
    decimal TotalReserved,
    decimal TotalAvailable,
    bool IsLowStock);

/// <summary>Yeu cau nhap NVL vao kho (RECEIPT).</summary>
public sealed record ReceiveStockRequest(
    long WarehouseId,
    long MaterialId,
    decimal Quantity,
    decimal UnitCost,
    string? Note);

/// <summary>Ket qua sau khi nhap kho.</summary>
public sealed record StockTransactionResult(
    long TransactionId,
    long MaterialId,
    long WarehouseId,
    decimal BalanceAfter);
