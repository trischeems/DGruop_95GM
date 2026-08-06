namespace GM95.Server.Apps.ManagerPerformance.Contracts;

/// <summary>1 dong NVL trong phieu nhap kho (so luong nhap + don gia).</summary>
public sealed record StockReceiptItemInput(
    long MaterialId,
    decimal QtyReceived,
    decimal UnitCost);

/// <summary>
/// Yeu cau tao phieu NHAP kho NVL nhieu dong (cong ton). Nhap kho doc lap -> khong gan don SX.
/// </summary>
public sealed record CreateStockReceiptRequest(
    long WarehouseId,
    IEnumerable<StockReceiptItemInput> Items,
    string? Note);

/// <summary>Ket qua sau khi tao & POST phieu nhap.</summary>
public sealed record StockReceiptResultDto(
    long ReceiptId,
    string ReceiptNo,
    int LineCount);

/// <summary>Phieu nhap kho (dong tom tat, khop bang stock_receipts).</summary>
public sealed record StockReceiptDto(
    long Id,
    string ReceiptNo,
    long WarehouseId,
    string? WarehouseName,   // ten kho (join warehouses) - hien thi canh WarehouseId
    string Status,
    DateTime ReceivedAt,
    string? Note);

/// <summary>
/// Sua 1 dong so cai giao dich kho (nhap/xuat). Quantity la so TUYET DOI nguoi dung go (> 0);
/// chieu nhap/xuat cua dong duoc GIU NGUYEN theo dau cua so luong cu.
/// </summary>
public sealed record UpdateStockTransactionRequest(
    decimal Quantity,
    decimal UnitCost,
    string? Note);

/// <summary>1 dong so cai giao dich kho (phuc vu xem lich su nhap/xuat + don gia tung lan).</summary>
public sealed record StockTransactionDto(
    long Id,
    long MaterialId,
    string? MaterialSku,     // SKU NVL (join materials)
    string? MaterialName,    // ten NVL (join materials)
    string? MaterialUomCode, // ma DVT - hien canh so luong
    string? MaterialUomName, // ten DVT
    long WarehouseId,
    string? WarehouseName,   // ten kho (join warehouses)
    string TxnType,          // RECEIPT / ISSUE / ADJUST / RETURN
    decimal Quantity,        // duong = nhap, am = xuat
    decimal UnitCost,        // don gia lan giao dich nay
    decimal BalanceAfter,    // ton on_hand ngay sau giao dich
    string? RefType,
    long? RefId,
    string? Note,
    DateTime CreatedAt);
