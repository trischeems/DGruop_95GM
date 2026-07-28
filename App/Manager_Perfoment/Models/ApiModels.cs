using CommunityToolkit.Mvvm.ComponentModel;
using System.Text.Json.Serialization;

namespace GM95.App.ManagerPerformance.Models;

/// <summary>Bao response chuan tu server: { ok, data, error }.</summary>
public sealed class ApiResult<T>
{
    [JsonPropertyName("ok")]    public bool Ok { get; set; }
    [JsonPropertyName("data")]  public T? Data { get; set; }
    [JsonPropertyName("error")] public ApiError? Error { get; set; }
}

public sealed class ApiError
{
    [JsonPropertyName("code")]    public string Code { get; set; } = "";
    [JsonPropertyName("message")] public string Message { get; set; } = "";
}

/// <summary>NVL (khop MaterialDto server).</summary>
public sealed class Material
{
    [JsonPropertyName("id")]              public long Id { get; set; }
    [JsonPropertyName("sku")]             public string Sku { get; set; } = "";
    [JsonPropertyName("name")]            public string Name { get; set; } = "";
    [JsonPropertyName("categoryId")]      public long? CategoryId { get; set; }
    [JsonPropertyName("uomId")]           public long UomId { get; set; }
    [JsonPropertyName("uomCode")]         public string? UomCode { get; set; }   // ma DVT (kg/m/cai...)
    [JsonPropertyName("uomName")]         public string? UomName { get; set; }
    [JsonPropertyName("reorderLevel")]    public decimal ReorderLevel { get; set; }
    [JsonPropertyName("reorderQuantity")] public decimal ReorderQuantity { get; set; }
    [JsonPropertyName("standardCost")]    public decimal StandardCost { get; set; }
    [JsonPropertyName("isActive")]        public bool IsActive { get; set; }
    public override string ToString() => $"{Name} ({Sku})";
}

/// <summary>Ton kha dung theo NVL (khop MaterialStockDto server / view v_material_stock).</summary>
public sealed class MaterialStock
{
    [JsonPropertyName("materialId")]      public long MaterialId { get; set; }
    [JsonPropertyName("sku")]             public string Sku { get; set; } = "";
    [JsonPropertyName("name")]            public string Name { get; set; } = "";
    [JsonPropertyName("reorderLevel")]    public decimal ReorderLevel { get; set; }
    [JsonPropertyName("reorderQuantity")] public decimal ReorderQuantity { get; set; }
    [JsonPropertyName("totalOnHand")]     public decimal TotalOnHand { get; set; }
    [JsonPropertyName("totalReserved")]   public decimal TotalReserved { get; set; }
    [JsonPropertyName("totalAvailable")]  public decimal TotalAvailable { get; set; }
    [JsonPropertyName("isLowStock")]      public bool IsLowStock { get; set; }
    [JsonPropertyName("lastUnitCost")]    public decimal LastUnitCost { get; set; }   // gia nhap gan nhat
    [JsonPropertyName("avgUnitCost")]     public decimal AvgUnitCost { get; set; }    // binh quan gia quyen
    [JsonPropertyName("stockValue")]      public decimal StockValue { get; set; }     // gia tri ton
    [JsonPropertyName("uomCode")]         public string? UomCode { get; set; }        // ma DVT
    [JsonPropertyName("uomName")]         public string? UomName { get; set; }
}

/// <summary>Ket qua nhap kho 1 dong (khop StockTransactionResult server).</summary>
public sealed class StockTransactionResult
{
    [JsonPropertyName("transactionId")] public long TransactionId { get; set; }
    [JsonPropertyName("materialId")]    public long MaterialId { get; set; }
    [JsonPropertyName("warehouseId")]   public long WarehouseId { get; set; }
    [JsonPropertyName("balanceAfter")]  public decimal BalanceAfter { get; set; }
}

/// <summary>Ket qua tao phieu nhap nhieu dong (khop StockReceiptResultDto server).</summary>
public sealed class StockReceiptResult
{
    [JsonPropertyName("receiptId")] public long ReceiptId { get; set; }
    [JsonPropertyName("receiptNo")] public string ReceiptNo { get; set; } = "";
    [JsonPropertyName("lineCount")] public int LineCount { get; set; }
}

/// <summary>Phieu nhap kho (header, khop StockReceiptDto server).</summary>
public sealed class StockReceipt
{
    [JsonPropertyName("id")]            public long Id { get; set; }
    [JsonPropertyName("receiptNo")]     public string ReceiptNo { get; set; } = "";
    [JsonPropertyName("warehouseId")]   public long WarehouseId { get; set; }
    [JsonPropertyName("warehouseName")] public string? WarehouseName { get; set; }
    [JsonPropertyName("status")]        public string Status { get; set; } = "";
    [JsonPropertyName("receivedAt")]    public DateTime ReceivedAt { get; set; }
    [JsonPropertyName("note")]          public string? Note { get; set; }
}

/// <summary>1 dong so cai giao dich kho (khop StockTransactionDto server) - xem lich su + don gia.</summary>
public sealed class StockTransaction
{
    [JsonPropertyName("id")]              public long Id { get; set; }
    [JsonPropertyName("materialId")]      public long MaterialId { get; set; }
    [JsonPropertyName("materialSku")]     public string? MaterialSku { get; set; }
    [JsonPropertyName("materialName")]    public string? MaterialName { get; set; }
    [JsonPropertyName("materialUomCode")] public string? MaterialUomCode { get; set; }  // ma DVT NVL
    [JsonPropertyName("materialUomName")] public string? MaterialUomName { get; set; }
    [JsonPropertyName("warehouseId")]     public long WarehouseId { get; set; }
    [JsonPropertyName("warehouseName")]   public string? WarehouseName { get; set; }
    [JsonPropertyName("txnType")]         public string TxnType { get; set; } = "";
    [JsonPropertyName("quantity")]        public decimal Quantity { get; set; }
    [JsonPropertyName("unitCost")]      public decimal UnitCost { get; set; }
    [JsonPropertyName("balanceAfter")]  public decimal BalanceAfter { get; set; }
    [JsonPropertyName("refType")]       public string? RefType { get; set; }
    [JsonPropertyName("refId")]         public long? RefId { get; set; }
    [JsonPropertyName("note")]          public string? Note { get; set; }
    [JsonPropertyName("createdAt")]     public DateTime CreatedAt { get; set; }
}

/// <summary>1 dong NVL de gui khi tao phieu nhap nhieu dong (dung o luoi nhap tren UI).</summary>
public sealed class ReceiptLine
{
    public Material? Material { get; set; }
    public decimal QtyReceived { get; set; }
    public decimal UnitCost { get; set; }
}

/// <summary>1 dong dinh muc NVL de them nhieu NVL cung luc vao BOM (luoi tren dialog).</summary>
public sealed class BomLineInput
{
    public Material? Material { get; set; }
    public decimal QtyPerUnit { get; set; } = 1;
    public decimal WastePct { get; set; }
}

/// <summary>1 dong NVL de xuat nhieu NVL cung luc trong 1 phieu xuat (luoi tren dialog).</summary>
public sealed class IssueLine
{
    public Material? Material { get; set; }
    public decimal QtyIssued { get; set; }
    public decimal UnitCost { get; set; }
}

/// <summary>
/// 1 dong buoc trong luoi soan MAU QUY TRINH (UI-only).
/// Dung ObservableObject de luoi tu cap nhat khi bam Len/Xuong doi thu tu.
/// </summary>
public sealed partial class RoutingLine : ObservableObject
{
    [ObservableProperty] private int _seq;               // thu tu buoc (1..n)
    [ObservableProperty] private Stage? _stage;          // cong doan
    [ObservableProperty] private bool _isOptional;       // buoc co the bo qua khi chay don
    [ObservableProperty] private string? _note;
}
