using System.Text.Json.Serialization;

namespace GM95.App.ManagerPerformance.Models;

// =====================================================================================
//  Model client cho cac module server bo sung (khop DTO server, JSON camelCase).
//  Chi chua du lieu; khong logic. Dung boi ApiClient + ViewModels.
// =====================================================================================

/// <summary>Don vi tinh (units_of_measure).</summary>
public sealed class Uom
{
    [JsonPropertyName("id")]   public long Id { get; set; }
    [JsonPropertyName("code")] public string Code { get; set; } = "";
    [JsonPropertyName("name")] public string Name { get; set; } = "";
    public override string ToString() => $"{Name} ({Code})";
}

/// <summary>Kho (warehouses).</summary>
public sealed class Warehouse
{
    [JsonPropertyName("id")]       public long Id { get; set; }
    [JsonPropertyName("code")]     public string Code { get; set; } = "";
    [JsonPropertyName("name")]     public string Name { get; set; } = "";
    [JsonPropertyName("isActive")] public bool IsActive { get; set; }
    public override string ToString() => $"{Name} ({Code})";
}

/// <summary>Nhom NVL (material_categories).</summary>
public sealed class MaterialCategory
{
    [JsonPropertyName("id")]   public long Id { get; set; }
    [JsonPropertyName("code")] public string Code { get; set; } = "";
    [JsonPropertyName("name")] public string Name { get; set; } = "";
    public override string ToString() => $"{Name} ({Code})";
}

/// <summary>Ma hang thanh pham (products).</summary>
public sealed class Product
{
    [JsonPropertyName("id")]       public long Id { get; set; }
    [JsonPropertyName("sku")]      public string Sku { get; set; } = "";
    [JsonPropertyName("name")]     public string Name { get; set; } = "";
    [JsonPropertyName("uomId")]    public long UomId { get; set; }
    [JsonPropertyName("uomCode")]  public string? UomCode { get; set; }   // ma DVT (join server)
    [JsonPropertyName("uomName")]  public string? UomName { get; set; }   // ten DVT (join server)
    [JsonPropertyName("isActive")] public bool IsActive { get; set; }
    public override string ToString() => $"{Name} ({Sku})";
}

/// <summary>BOM header (bom).</summary>
public sealed class Bom
{
    [JsonPropertyName("id")]            public long Id { get; set; }
    [JsonPropertyName("productId")]     public long ProductId { get; set; }
    [JsonPropertyName("version")]       public int Version { get; set; }
    [JsonPropertyName("status")]        public string Status { get; set; } = "";
    [JsonPropertyName("effectiveFrom")] public DateTime? EffectiveFrom { get; set; }
    [JsonPropertyName("note")]          public string? Note { get; set; }
}

/// <summary>Dong dinh muc (bom_items).</summary>
public sealed class BomItem
{
    [JsonPropertyName("id")]           public long Id { get; set; }
    [JsonPropertyName("bomId")]        public long BomId { get; set; }
    [JsonPropertyName("materialId")]      public long MaterialId { get; set; }
    [JsonPropertyName("materialSku")]     public string? MaterialSku { get; set; }   // SKU NVL (join server)
    [JsonPropertyName("materialName")]    public string? MaterialName { get; set; }  // ten NVL (join server)
    [JsonPropertyName("materialUomCode")] public string? MaterialUomCode { get; set; } // ma DVT NVL
    [JsonPropertyName("materialUomName")] public string? MaterialUomName { get; set; }
    [JsonPropertyName("qtyPerUnit")]      public decimal QtyPerUnit { get; set; }
    [JsonPropertyName("wastePct")]        public decimal WastePct { get; set; }
    [JsonPropertyName("note")]            public string? Note { get; set; }
}

/// <summary>BOM day du (header + items).</summary>
public sealed class BomDetail
{
    [JsonPropertyName("bom")]   public Bom? Bom { get; set; }
    [JsonPropertyName("items")] public List<BomItem> Items { get; set; } = new();
}

/// <summary>Phe duyet BOM (bom_approvals).</summary>
public sealed class BomApproval
{
    [JsonPropertyName("id")]           public long Id { get; set; }
    [JsonPropertyName("bomId")]        public long BomId { get; set; }
    [JsonPropertyName("status")]       public string Status { get; set; } = "";
    [JsonPropertyName("requestedAt")]  public DateTime RequestedAt { get; set; }
    [JsonPropertyName("decidedAt")]    public DateTime? DecidedAt { get; set; }
    [JsonPropertyName("decisionNote")] public string? DecisionNote { get; set; }
}

/// <summary>Don hang san xuat (production_orders).</summary>
public sealed class ProductionOrder
{
    [JsonPropertyName("id")]          public long Id { get; set; }
    [JsonPropertyName("orderNo")]     public string OrderNo { get; set; } = "";
    [JsonPropertyName("productId")]   public long ProductId { get; set; }
    [JsonPropertyName("productSku")]  public string? ProductSku { get; set; }   // SKU ma hang (join server)
    [JsonPropertyName("productName")] public string? ProductName { get; set; }  // ten ma hang (join server)
    [JsonPropertyName("productUomCode")] public string? ProductUomCode { get; set; }  // ma DVT thanh pham
    [JsonPropertyName("productUomName")] public string? ProductUomName { get; set; }
    [JsonPropertyName("bomId")]       public long? BomId { get; set; }
    [JsonPropertyName("quantity")]    public decimal Quantity { get; set; }
    [JsonPropertyName("status")]      public string Status { get; set; } = "";
    [JsonPropertyName("dueDate")]     public DateTime? DueDate { get; set; }
    [JsonPropertyName("confirmedAt")] public DateTime? ConfirmedAt { get; set; }
    [JsonPropertyName("routingId")]   public long? RoutingId { get; set; }     // mau quy trinh da chon
    [JsonPropertyName("routingName")] public string? RoutingName { get; set; }  // ten mau quy trinh
    public override string ToString() => OrderNo;
}

/// <summary>Giu cho NVL (material_reservations).</summary>
public sealed class Reservation
{
    [JsonPropertyName("id")]                public long Id { get; set; }
    [JsonPropertyName("productionOrderId")] public long ProductionOrderId { get; set; }
    [JsonPropertyName("materialId")]        public long MaterialId { get; set; }
    [JsonPropertyName("materialSku")]       public string? MaterialSku { get; set; }   // SKU NVL (join server)
    [JsonPropertyName("materialName")]      public string? MaterialName { get; set; }  // ten NVL (join server)
    [JsonPropertyName("materialUomCode")]   public string? MaterialUomCode { get; set; } // ma DVT NVL
    [JsonPropertyName("materialUomName")]   public string? MaterialUomName { get; set; }
    [JsonPropertyName("warehouseId")]       public long WarehouseId { get; set; }
    [JsonPropertyName("warehouseName")]     public string? WarehouseName { get; set; } // ten kho (join server)
    [JsonPropertyName("qtyReserved")]       public decimal QtyReserved { get; set; }
    [JsonPropertyName("status")]            public string Status { get; set; } = "";
}

/// <summary>Ke hoach san xuat (production_plans).</summary>
public sealed class ProductionPlan
{
    [JsonPropertyName("id")]                public long Id { get; set; }
    [JsonPropertyName("productionOrderId")] public long ProductionOrderId { get; set; }
    [JsonPropertyName("orderNo")]           public string? OrderNo { get; set; }   // so don (join server)
    [JsonPropertyName("plannedQty")]        public decimal PlannedQty { get; set; }
    [JsonPropertyName("plannedStart")]      public DateTime? PlannedStart { get; set; }
    [JsonPropertyName("plannedEnd")]        public DateTime? PlannedEnd { get; set; }
    [JsonPropertyName("lineCode")]          public string? LineCode { get; set; }
    [JsonPropertyName("status")]            public string Status { get; set; } = "";
    [JsonPropertyName("note")]              public string? Note { get; set; }
    [JsonPropertyName("productUomCode")]    public string? ProductUomCode { get; set; }  // ma DVT thanh pham
    [JsonPropertyName("productUomName")]    public string? ProductUomName { get; set; }
}

/// <summary>Cong doan san xuat (production_steps + stage).</summary>
public sealed class ProductionStep
{
    [JsonPropertyName("id")]                public long Id { get; set; }
    [JsonPropertyName("productionOrderId")] public long ProductionOrderId { get; set; }
    [JsonPropertyName("stageId")]           public long StageId { get; set; }
    [JsonPropertyName("stageCode")]         public string StageCode { get; set; } = "";
    [JsonPropertyName("stageName")]         public string StageName { get; set; } = "";
    [JsonPropertyName("seq")]               public int Seq { get; set; }
    [JsonPropertyName("status")]            public string Status { get; set; } = "";
    [JsonPropertyName("qtyIn")]             public decimal QtyIn { get; set; }
    [JsonPropertyName("qtyOut")]            public decimal QtyOut { get; set; }
    [JsonPropertyName("qtyDefect")]         public decimal QtyDefect { get; set; }
    [JsonPropertyName("startedAt")]         public DateTime? StartedAt { get; set; }
    [JsonPropertyName("finishedAt")]        public DateTime? FinishedAt { get; set; }
    [JsonPropertyName("note")]              public string? Note { get; set; }
    [JsonPropertyName("isSkipped")]         public bool IsSkipped { get; set; }          // true = buoc bi BO QUA cho don
    [JsonPropertyName("productUomCode")]    public string? ProductUomCode { get; set; }  // ma DVT thanh pham
    [JsonPropertyName("productUomName")]    public string? ProductUomName { get; set; }
}

/// <summary>Ket qua xuat kho (material_issues).</summary>
public sealed class MaterialIssueResult
{
    [JsonPropertyName("issueId")]   public long IssueId { get; set; }
    [JsonPropertyName("issueNo")]   public string IssueNo { get; set; } = "";
    [JsonPropertyName("lineCount")] public int LineCount { get; set; }
}

/// <summary>Phieu xuat kho NVL (material_issues header).</summary>
public sealed class MaterialIssue
{
    [JsonPropertyName("id")]                public long Id { get; set; }
    [JsonPropertyName("issueNo")]           public string IssueNo { get; set; } = "";
    [JsonPropertyName("productionOrderId")] public long ProductionOrderId { get; set; }
    [JsonPropertyName("warehouseId")]       public long WarehouseId { get; set; }
    [JsonPropertyName("status")]            public string Status { get; set; } = "";
    [JsonPropertyName("issuedAt")]          public DateTime IssuedAt { get; set; }
}

/// <summary>Nhap kho thanh pham (finished_goods_receipts).</summary>
public sealed class FinishedGoodsReceipt
{
    [JsonPropertyName("id")]                public long Id { get; set; }
    [JsonPropertyName("receiptNo")]         public string ReceiptNo { get; set; } = "";
    [JsonPropertyName("productionOrderId")] public long ProductionOrderId { get; set; }
    [JsonPropertyName("orderNo")]           public string? OrderNo { get; set; }       // so don (join server)
    [JsonPropertyName("productId")]         public long ProductId { get; set; }
    [JsonPropertyName("productSku")]        public string? ProductSku { get; set; }    // SKU ma hang (join server)
    [JsonPropertyName("productName")]       public string? ProductName { get; set; }   // ten ma hang (join server)
    [JsonPropertyName("productUomCode")]    public string? ProductUomCode { get; set; } // ma DVT thanh pham
    [JsonPropertyName("productUomName")]    public string? ProductUomName { get; set; }
    [JsonPropertyName("warehouseId")]       public long WarehouseId { get; set; }
    [JsonPropertyName("warehouseName")]     public string? WarehouseName { get; set; } // ten kho (join server)
    [JsonPropertyName("qtyReceived")]       public decimal QtyReceived { get; set; }
    [JsonPropertyName("receivedAt")]        public DateTime ReceivedAt { get; set; }
}

/// <summary>Doi chieu hao hut (loss_reports).</summary>
public sealed class LossReport
{
    [JsonPropertyName("id")]                public long Id { get; set; }
    [JsonPropertyName("productionOrderId")] public long ProductionOrderId { get; set; }
    [JsonPropertyName("orderNo")]           public string? OrderNo { get; set; }       // so don (join server)
    [JsonPropertyName("materialId")]        public long MaterialId { get; set; }
    [JsonPropertyName("materialSku")]       public string? MaterialSku { get; set; }   // SKU NVL (join server)
    [JsonPropertyName("materialName")]      public string? MaterialName { get; set; }  // ten NVL (join server)
    [JsonPropertyName("materialUomCode")]   public string? MaterialUomCode { get; set; } // ma DVT NVL (cho cap phat/dinh muc/hao hut)
    [JsonPropertyName("materialUomName")]   public string? MaterialUomName { get; set; }
    [JsonPropertyName("productUomCode")]    public string? ProductUomCode { get; set; }  // ma DVT thanh pham (cho SL TP)
    [JsonPropertyName("productUomName")]    public string? ProductUomName { get; set; }
    [JsonPropertyName("qtyIssued")]         public decimal QtyIssued { get; set; }
    [JsonPropertyName("qtyStandard")]       public decimal QtyStandard { get; set; }
    [JsonPropertyName("qtyVariance")]       public decimal QtyVariance { get; set; }
    [JsonPropertyName("finishedQty")]       public decimal FinishedQty { get; set; }
}

/// <summary>Canh bao (alerts).</summary>
public sealed class Alert
{
    [JsonPropertyName("id")]         public long Id { get; set; }
    [JsonPropertyName("alertType")]  public string AlertType { get; set; } = "";
    [JsonPropertyName("severity")]   public string Severity { get; set; } = "";
    [JsonPropertyName("entityType")] public string EntityType { get; set; } = "";
    [JsonPropertyName("entityId")]   public long EntityId { get; set; }
    [JsonPropertyName("message")]    public string Message { get; set; } = "";
    [JsonPropertyName("status")]     public string Status { get; set; } = "";
    [JsonPropertyName("createdAt")]  public DateTime CreatedAt { get; set; }
}

/// <summary>San luong toi da theo ma hang (v_max_output_by_product).</summary>
public sealed class MaxOutputByProduct
{
    [JsonPropertyName("productId")]               public long ProductId { get; set; }
    [JsonPropertyName("productSku")]              public string? ProductSku { get; set; }   // SKU ma hang (join server)
    [JsonPropertyName("productName")]             public string? ProductName { get; set; }  // ten ma hang (join server)
    [JsonPropertyName("bomId")]                   public long BomId { get; set; }
    [JsonPropertyName("maxProducibleQty")]        public decimal MaxProducibleQty { get; set; }
    [JsonPropertyName("bottleneckMaterialId")]    public long BottleneckMaterialId { get; set; }
    [JsonPropertyName("bottleneckMaterialSku")]   public string? BottleneckMaterialSku { get; set; }  // SKU NVL nut that
    [JsonPropertyName("bottleneckMaterialName")]  public string? BottleneckMaterialName { get; set; } // ten NVL nut that
    [JsonPropertyName("bottleneckMaterialOutput")] public decimal BottleneckMaterialOutput { get; set; }
}

/// <summary>Nhu cau NVL theo don (v_order_material_requirement).</summary>
public sealed class OrderMaterialRequirement
{
    [JsonPropertyName("productionOrderId")]    public long ProductionOrderId { get; set; }
    [JsonPropertyName("orderNo")]              public string OrderNo { get; set; } = "";
    [JsonPropertyName("orderStatus")]          public string OrderStatus { get; set; } = "";
    [JsonPropertyName("materialId")]           public long MaterialId { get; set; }
    [JsonPropertyName("materialSku")]          public string? MaterialSku { get; set; }   // SKU NVL (join server)
    [JsonPropertyName("materialName")]         public string? MaterialName { get; set; }  // ten NVL (join server)
    [JsonPropertyName("materialUomCode")]      public string? MaterialUomCode { get; set; } // ma DVT NVL
    [JsonPropertyName("materialUomName")]      public string? MaterialUomName { get; set; }
    [JsonPropertyName("requiredQty")]          public decimal RequiredQty { get; set; }
    [JsonPropertyName("totalAvailable")]       public decimal TotalAvailable { get; set; }
    [JsonPropertyName("shortageQty")]          public decimal ShortageQty { get; set; }
    [JsonPropertyName("suggestedPurchaseQty")] public decimal SuggestedPurchaseQty { get; set; }
}

/// <summary>Anh huong khi xoa NVL (GET /materials/{id}/impact).</summary>
public sealed class MaterialImpact
{
    [JsonPropertyName("stockRowCount")]    public long StockRowCount { get; set; }
    [JsonPropertyName("totalOnHand")]      public decimal TotalOnHand { get; set; }
    [JsonPropertyName("bomItemCount")]     public long BomItemCount { get; set; }
    [JsonPropertyName("reservationCount")] public long ReservationCount { get; set; }
    [JsonPropertyName("issueLineCount")]   public long IssueLineCount { get; set; }
    [JsonPropertyName("transactionCount")] public long TransactionCount { get; set; }
    [JsonPropertyName("lossReportCount")]  public long LossReportCount { get; set; }
    [JsonPropertyName("canDelete")]        public bool CanDelete { get; set; }
}

/// <summary>Anh huong khi xoa ma hang (GET /products/{id}/impact).</summary>
public sealed class ProductImpact
{
    [JsonPropertyName("bomCount")]     public long BomCount { get; set; }
    [JsonPropertyName("orderCount")]   public long OrderCount { get; set; }
    [JsonPropertyName("receiptCount")] public long ReceiptCount { get; set; }
    [JsonPropertyName("canDelete")]    public bool CanDelete { get; set; }
}

/// <summary>Anh huong khi xoa BOM (GET /boms/{id}/impact).</summary>
public sealed class BomImpact
{
    [JsonPropertyName("status")]     public string Status { get; set; } = "";
    [JsonPropertyName("version")]    public int Version { get; set; }
    [JsonPropertyName("itemCount")]  public long ItemCount { get; set; }
    [JsonPropertyName("orderCount")] public long OrderCount { get; set; }
    [JsonPropertyName("canDelete")]  public bool CanDelete { get; set; }
}

/// <summary>Anh huong khi xoa don (GET /production-orders/{id}/impact).</summary>
public sealed class OrderImpact
{
    [JsonPropertyName("status")]           public string Status { get; set; } = "";
    [JsonPropertyName("orderNo")]          public string OrderNo { get; set; } = "";
    [JsonPropertyName("planCount")]        public long PlanCount { get; set; }
    [JsonPropertyName("stepCount")]        public long StepCount { get; set; }
    [JsonPropertyName("reservationCount")] public long ReservationCount { get; set; }
    [JsonPropertyName("issueCount")]       public long IssueCount { get; set; }
    [JsonPropertyName("receiptCount")]     public long ReceiptCount { get; set; }
    [JsonPropertyName("lossReportCount")]  public long LossReportCount { get; set; }
    [JsonPropertyName("canDelete")]        public bool CanDelete { get; set; }
}

/// <summary>Anh huong khi xoa phieu nhap TP (GET /finished-goods/{id}/impact).</summary>
public sealed class FinishedGoodsImpact
{
    [JsonPropertyName("receiptNo")]         public string ReceiptNo { get; set; } = "";
    [JsonPropertyName("qtyReceived")]       public decimal QtyReceived { get; set; }
    [JsonPropertyName("orderStatus")]       public string OrderStatus { get; set; } = "";
    [JsonPropertyName("orderReceiptCount")] public long OrderReceiptCount { get; set; }
    [JsonPropertyName("lossReportCount")]   public long LossReportCount { get; set; }
    [JsonPropertyName("willRevertOrder")]   public bool WillRevertOrder { get; set; }
}

/// <summary>So lieu tong hop 1 thang (GET /reports/monthly-stats) — phuc vu tab So sanh thang.</summary>
public sealed class MonthlyStats
{
    [JsonPropertyName("month")]          public int Month { get; set; }
    [JsonPropertyName("orderCount")]     public long OrderCount { get; set; }
    [JsonPropertyName("orderQty")]       public decimal OrderQty { get; set; }
    [JsonPropertyName("stockInQty")]     public decimal StockInQty { get; set; }
    [JsonPropertyName("stockInValue")]   public decimal StockInValue { get; set; }
    [JsonPropertyName("stockOutQty")]    public decimal StockOutQty { get; set; }
    [JsonPropertyName("stockOutValue")]  public decimal StockOutValue { get; set; }
    [JsonPropertyName("fgReceiptCount")] public long FgReceiptCount { get; set; }
    [JsonPropertyName("fgQty")]          public decimal FgQty { get; set; }
    [JsonPropertyName("lossVariance")]   public decimal LossVariance { get; set; }
    [JsonPropertyName("alertCount")]     public long AlertCount { get; set; }
}

// =====================================================================================
// QUY TRINH SAN XUAT LINH HOAT (V006)
// =====================================================================================

/// <summary>1 cong doan trong danh muc (Cat vai, May, QC, In, Dong goi...).</summary>
public sealed class Stage
{
    [JsonPropertyName("id")]       public long Id { get; set; }
    [JsonPropertyName("code")]     public string Code { get; set; } = "";
    [JsonPropertyName("name")]     public string Name { get; set; } = "";
    [JsonPropertyName("seq")]      public int Seq { get; set; }
    [JsonPropertyName("isActive")] public bool IsActive { get; set; }
    public override string ToString() => $"{Name} ({Code})";
}

/// <summary>1 mau quy trinh san xuat = 1 chuoi cong doan.</summary>
public sealed class Routing
{
    [JsonPropertyName("id")]        public long Id { get; set; }
    [JsonPropertyName("code")]      public string Code { get; set; } = "";
    [JsonPropertyName("name")]      public string Name { get; set; } = "";
    [JsonPropertyName("note")]      public string? Note { get; set; }
    [JsonPropertyName("isActive")]  public bool IsActive { get; set; }
    [JsonPropertyName("isDefault")] public bool IsDefault { get; set; }
    [JsonPropertyName("stepCount")] public int StepCount { get; set; }
    [JsonPropertyName("createdAt")] public DateTime CreatedAt { get; set; }
    public override string ToString() => IsDefault ? $"{Name} (mac dinh)" : Name;
}

/// <summary>1 buoc trong mau quy trinh.</summary>
public sealed class RoutingStep
{
    [JsonPropertyName("id")]         public long Id { get; set; }
    [JsonPropertyName("routingId")]  public long RoutingId { get; set; }
    [JsonPropertyName("stageId")]    public long StageId { get; set; }
    [JsonPropertyName("stageCode")]  public string StageCode { get; set; } = "";
    [JsonPropertyName("stageName")]  public string StageName { get; set; } = "";
    [JsonPropertyName("seq")]        public int Seq { get; set; }
    [JsonPropertyName("isOptional")] public bool IsOptional { get; set; }
    [JsonPropertyName("note")]       public string? Note { get; set; }
}

/// <summary>Mau quy trinh + day du cac buoc (GET /routings/{id}).</summary>
public sealed class RoutingDetail
{
    [JsonPropertyName("routing")] public Routing? Routing { get; set; }
    [JsonPropertyName("steps")]   public List<RoutingStep> Steps { get; set; } = new();
}
