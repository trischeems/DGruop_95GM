using System.Text.Json.Serialization;

namespace DGroup.App.ManagerPerformance.Models;

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
}

/// <summary>Ket qua nhap kho (khop StockTransactionResult server).</summary>
public sealed class StockTransactionResult
{
    [JsonPropertyName("transactionId")] public long TransactionId { get; set; }
    [JsonPropertyName("materialId")]    public long MaterialId { get; set; }
    [JsonPropertyName("warehouseId")]   public long WarehouseId { get; set; }
    [JsonPropertyName("balanceAfter")]  public decimal BalanceAfter { get; set; }
}
