namespace GM95.Server.Apps.ManagerPerformance.Contracts;

/// <summary>Ton kha dung gop moi kho theo NVL + gia + DVT (khop view v_material_stock sau V005).</summary>
public sealed record MaterialStockDto(
    long MaterialId,
    string Sku,
    string Name,
    decimal ReorderLevel,
    decimal ReorderQuantity,
    decimal TotalOnHand,
    decimal TotalReserved,
    decimal TotalAvailable,
    bool IsLowStock,
    decimal LastUnitCost,    // don gia nhap gan nhat (RECEIPT moi nhat)
    decimal AvgUnitCost,     // don gia binh quan gia quyen
    decimal StockValue,      // gia tri ton = total_on_hand * avg_unit_cost
    string? UomCode,         // ma DVT (kg/m/cai...) - hien canh so luong ton
    string? UomName);        // ten DVT

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
