using System.Net.Http;
using System.Text.Json;
using GM95.App.ManagerPerformance.Models;

namespace GM95.App.ManagerPerformance.Services;

/// <summary>
/// Phan mo rong ApiClient: goi API cac module catalog + van hanh (Lookups, Products, BOM,
/// Don SX, Ke hoach, Cong doan, Xuat kho, Nhap TP, Hao hut, Canh bao). Dung lai cac helper
/// private (SendAsync/GetListAsync/PostForIdAsync) o ApiClient.cs (cung 1 class partial).
/// </summary>
public sealed partial class ApiClient
{
    // ----- helper bo sung -----
    private async Task<T?> GetOneAsync<T>(string url, CancellationToken ct) =>
        await SendAsync<T>(HttpMethod.Get, url, null, ct);

    /// <summary>POST/PUT hanh dong (confirm, submit, approve...) chi can ok, khong doc data.</summary>
    private async Task ActionAsync(HttpMethod method, string url, object? body, CancellationToken ct) =>
        await SendAsync<JsonElement>(method, url, body, ct);

    // ===== Lookups =====
    public Task<List<Uom>> GetUomsAsync(CancellationToken ct = default) =>
        GetListAsync<Uom>($"{_prefix}/lookups/units-of-measure", ct);
    public Task<long> CreateUomAsync(object body, CancellationToken ct = default) =>
        PostForIdAsync($"{_prefix}/lookups/units-of-measure", body, ct);

    public Task<List<Warehouse>> GetWarehousesAsync(CancellationToken ct = default) =>
        GetListAsync<Warehouse>($"{_prefix}/lookups/warehouses", ct);
    public Task<long> CreateWarehouseAsync(object body, CancellationToken ct = default) =>
        PostForIdAsync($"{_prefix}/lookups/warehouses", body, ct);

    public Task<List<MaterialCategory>> GetCategoriesAsync(CancellationToken ct = default) =>
        GetListAsync<MaterialCategory>($"{_prefix}/lookups/material-categories", ct);
    public Task<long> CreateCategoryAsync(object body, CancellationToken ct = default) =>
        PostForIdAsync($"{_prefix}/lookups/material-categories", body, ct);

    // ===== Products =====
    public Task<List<Product>> GetProductsAsync(bool activeOnly = false, int? year = null, int? month = null, CancellationToken ct = default) =>
        GetListAsync<Product>($"{_prefix}/products?activeOnly={activeOnly.ToString().ToLowerInvariant()}{Period(year, month)}", ct);
    public Task<long> CreateProductAsync(object body, CancellationToken ct = default) =>
        PostForIdAsync($"{_prefix}/products", body, ct);
    public Task UpdateProductAsync(long id, object body, CancellationToken ct = default) =>
        ActionAsync(HttpMethod.Put, $"{_prefix}/products/{id}", body, ct);
    public Task<ProductImpact?> GetProductImpactAsync(long id, CancellationToken ct = default) =>
        GetOneAsync<ProductImpact>($"{_prefix}/products/{id}/impact", ct);
    public Task DeleteProductAsync(long id, CancellationToken ct = default) =>
        ActionAsync(HttpMethod.Delete, $"{_prefix}/products/{id}", null, ct);

    // ===== Materials (sua/xoa) =====
    public Task UpdateMaterialAsync(long id, object body, CancellationToken ct = default) =>
        ActionAsync(HttpMethod.Put, $"{_prefix}/materials/{id}", body, ct);
    public Task<MaterialImpact?> GetMaterialImpactAsync(long id, CancellationToken ct = default) =>
        GetOneAsync<MaterialImpact>($"{_prefix}/materials/{id}/impact", ct);
    public Task DeleteMaterialAsync(long id, CancellationToken ct = default) =>
        ActionAsync(HttpMethod.Delete, $"{_prefix}/materials/{id}", null, ct);

    // ===== BOM =====
    public Task<List<Bom>> GetBomsAsync(long? productId = null, CancellationToken ct = default) =>
        GetListAsync<Bom>($"{_prefix}/boms" + (productId.HasValue ? $"?productId={productId}" : ""), ct);
    public Task<BomDetail?> GetBomAsync(long id, CancellationToken ct = default) =>
        GetOneAsync<BomDetail>($"{_prefix}/boms/{id}", ct);
    public Task<long> CreateBomAsync(object body, CancellationToken ct = default) =>
        PostForIdAsync($"{_prefix}/boms", body, ct);
    public Task UpsertBomItemAsync(long bomId, object body, CancellationToken ct = default) =>
        ActionAsync(HttpMethod.Put, $"{_prefix}/boms/{bomId}/items", body, ct);
    public Task DeleteBomItemAsync(long bomId, long materialId, CancellationToken ct = default) =>
        ActionAsync(HttpMethod.Delete, $"{_prefix}/boms/{bomId}/items/{materialId}", null, ct);
    public Task SubmitBomAsync(long bomId, CancellationToken ct = default) =>
        ActionAsync(HttpMethod.Post, $"{_prefix}/boms/{bomId}/submit", null, ct);
    public Task ApproveBomAsync(long bomId, object body, CancellationToken ct = default) =>
        ActionAsync(HttpMethod.Post, $"{_prefix}/boms/{bomId}/approve", body, ct);
    public Task RejectBomAsync(long bomId, object body, CancellationToken ct = default) =>
        ActionAsync(HttpMethod.Post, $"{_prefix}/boms/{bomId}/reject", body, ct);
    public Task<List<BomApproval>> GetBomApprovalsAsync(long bomId, CancellationToken ct = default) =>
        GetListAsync<BomApproval>($"{_prefix}/boms/{bomId}/approvals", ct);
    public Task<BomImpact?> GetBomImpactAsync(long id, CancellationToken ct = default) =>
        GetOneAsync<BomImpact>($"{_prefix}/boms/{id}/impact", ct);
    public Task DeleteBomAsync(long id, CancellationToken ct = default) =>
        ActionAsync(HttpMethod.Delete, $"{_prefix}/boms/{id}", null, ct);

    // ===== Production Orders (+ giu cho) =====
    public Task<List<ProductionOrder>> GetOrdersAsync(string? status = null, int? year = null, int? month = null, CancellationToken ct = default) =>
        GetListAsync<ProductionOrder>($"{_prefix}/production-orders?x=1" + (string.IsNullOrEmpty(status) ? "" : $"&status={status}") + Period(year, month), ct);
    public Task<ProductionOrder?> GetOrderAsync(long id, CancellationToken ct = default) =>
        GetOneAsync<ProductionOrder>($"{_prefix}/production-orders/{id}", ct);
    public Task<long> CreateOrderAsync(object body, CancellationToken ct = default) =>
        PostForIdAsync($"{_prefix}/production-orders", body, ct);
    public Task ConfirmOrderAsync(long id, CancellationToken ct = default) =>
        ActionAsync(HttpMethod.Post, $"{_prefix}/production-orders/{id}/confirm", null, ct);
    public Task CancelOrderAsync(long id, CancellationToken ct = default) =>
        ActionAsync(HttpMethod.Post, $"{_prefix}/production-orders/{id}/cancel", null, ct);
    public Task<List<Reservation>> GetReservationsAsync(long orderId, CancellationToken ct = default) =>
        GetListAsync<Reservation>($"{_prefix}/production-orders/{orderId}/reservations", ct);
    public Task UpdateOrderAsync(long id, object body, CancellationToken ct = default) =>
        ActionAsync(HttpMethod.Put, $"{_prefix}/production-orders/{id}", body, ct);
    public Task<OrderImpact?> GetOrderImpactAsync(long id, CancellationToken ct = default) =>
        GetOneAsync<OrderImpact>($"{_prefix}/production-orders/{id}/impact", ct);
    public Task DeleteOrderAsync(long id, CancellationToken ct = default) =>
        ActionAsync(HttpMethod.Delete, $"{_prefix}/production-orders/{id}", null, ct);

    // ===== Production Plans =====
    public Task<List<ProductionPlan>> GetPlansAsync(long? orderId = null, int? year = null, int? month = null, CancellationToken ct = default) =>
        GetListAsync<ProductionPlan>($"{_prefix}/production-plans?x=1" + (orderId.HasValue ? $"&orderId={orderId}" : "") + Period(year, month), ct);
    public Task<long> CreatePlanAsync(object body, CancellationToken ct = default) =>
        PostForIdAsync($"{_prefix}/production-plans", body, ct);
    public Task UpdatePlanStatusAsync(long id, object body, CancellationToken ct = default) =>
        ActionAsync(HttpMethod.Put, $"{_prefix}/production-plans/{id}/status", body, ct);
    public Task UpdatePlanAsync(long id, object body, CancellationToken ct = default) =>
        ActionAsync(HttpMethod.Put, $"{_prefix}/production-plans/{id}", body, ct);
    public Task DeletePlanAsync(long id, CancellationToken ct = default) =>
        ActionAsync(HttpMethod.Delete, $"{_prefix}/production-plans/{id}", null, ct);

    // ===== Production Steps (Cat/May/QC) =====
    public Task InitStepsAsync(long orderId, CancellationToken ct = default) =>
        ActionAsync(HttpMethod.Post, $"{_prefix}/production-steps/init/{orderId}", null, ct);
    public Task<List<ProductionStep>> GetStepsAsync(long orderId, CancellationToken ct = default) =>
        GetListAsync<ProductionStep>($"{_prefix}/production-steps?orderId={orderId}", ct);
    public Task UpdateStepAsync(long id, object body, CancellationToken ct = default) =>
        ActionAsync(HttpMethod.Put, $"{_prefix}/production-steps/{id}", body, ct);

    // ===== Material Issues (xuat kho NVL) =====
    public Task<List<MaterialIssue>> GetIssuesAsync(long? orderId = null, CancellationToken ct = default) =>
        GetListAsync<MaterialIssue>($"{_prefix}/material-issues" + (orderId.HasValue ? $"?orderId={orderId}" : ""), ct);
    /// <summary>Danh sach DONG NVL cua cac phieu xuat (kem ma + ten NVL, ten kho).</summary>
    public Task<List<MaterialIssueItem>> GetIssueItemsAsync(long? orderId = null, int limit = 200, CancellationToken ct = default) =>
        GetListAsync<MaterialIssueItem>($"{_prefix}/material-issues/items?limit={limit}" + (orderId.HasValue ? $"&orderId={orderId}" : ""), ct);
    public Task<MaterialIssueResult> CreateIssueAsync(object body, CancellationToken ct = default) =>
        PostAsync<MaterialIssueResult>($"{_prefix}/material-issues", body, ct);

    // ===== Finished Goods (nhap kho TP) =====
    public Task<List<FinishedGoodsReceipt>> GetFinishedGoodsAsync(long? orderId = null, int? year = null, int? month = null, CancellationToken ct = default) =>
        GetListAsync<FinishedGoodsReceipt>($"{_prefix}/finished-goods?x=1" + (orderId.HasValue ? $"&orderId={orderId}" : "") + Period(year, month), ct);
    public Task<long> CreateFinishedGoodsAsync(object body, CancellationToken ct = default) =>
        PostForIdAsync($"{_prefix}/finished-goods", body, ct);
    public Task<FinishedGoodsImpact?> GetFinishedGoodsImpactAsync(long id, CancellationToken ct = default) =>
        GetOneAsync<FinishedGoodsImpact>($"{_prefix}/finished-goods/{id}/impact", ct);
    public Task DeleteFinishedGoodsAsync(long id, CancellationToken ct = default) =>
        ActionAsync(HttpMethod.Delete, $"{_prefix}/finished-goods/{id}", null, ct);

    // ===== Loss Reports (hao hut) =====
    public Task<List<LossReport>> GetLossReportsAsync(long? orderId = null, CancellationToken ct = default) =>
        GetListAsync<LossReport>($"{_prefix}/loss-reports" + (orderId.HasValue ? $"?orderId={orderId}" : ""), ct);
    public Task GenerateLossAsync(long orderId, CancellationToken ct = default) =>
        ActionAsync(HttpMethod.Post, $"{_prefix}/loss-reports/generate/{orderId}", null, ct);

    // ===== Alerts =====
    public Task<List<Alert>> GetAlertsAsync(string? status = null, int? year = null, int? month = null, CancellationToken ct = default) =>
        GetListAsync<Alert>($"{_prefix}/alerts?x=1" + (string.IsNullOrEmpty(status) ? "" : $"&status={status}") + Period(year, month), ct);
    public Task GenerateAlertsAsync(CancellationToken ct = default) =>
        ActionAsync(HttpMethod.Post, $"{_prefix}/alerts/generate", null, ct);
    public Task AcknowledgeAlertAsync(long id, CancellationToken ct = default) =>
        ActionAsync(HttpMethod.Put, $"{_prefix}/alerts/{id}/acknowledge", null, ct);
    public Task ResolveAlertAsync(long id, CancellationToken ct = default) =>
        ActionAsync(HttpMethod.Put, $"{_prefix}/alerts/{id}/resolve", null, ct);

    // ===== Reports =====
    public Task<List<MaxOutputByProduct>> GetMaxOutputAsync(CancellationToken ct = default) =>
        GetListAsync<MaxOutputByProduct>($"{_prefix}/reports/max-output", ct);
    public Task<List<MonthlyStats>> GetMonthlyStatsAsync(int year, CancellationToken ct = default) =>
        GetListAsync<MonthlyStats>($"{_prefix}/reports/monthly-stats?year={year}", ct);
    public Task<List<OrderMaterialRequirement>> GetOrderRequirementsAsync(long? orderId = null, CancellationToken ct = default) =>
        GetListAsync<OrderMaterialRequirement>($"{_prefix}/reports/order-requirements" + (orderId.HasValue ? $"?orderId={orderId}" : ""), ct);
    /// <summary>So NVL theo tung ma: ton dau ky / tong nhap / tong xuat / ton cuoi ky (khoang tu-den ngay).</summary>
    public Task<List<MaterialLedgerRow>> GetMaterialLedgerAsync(DateTime? from = null, DateTime? to = null, CancellationToken ct = default) =>
        GetListAsync<MaterialLedgerRow>($"{_prefix}/reports/material-ledger?x=1{Range(from, to)}", ct);
    /// <summary>Thong ke san xuat theo ma hang trong khoang tu-den ngay.</summary>
    public Task<List<ProductionSummaryRow>> GetProductionSummaryAsync(DateTime? from = null, DateTime? to = null, CancellationToken ct = default) =>
        GetListAsync<ProductionSummaryRow>($"{_prefix}/reports/production-summary?x=1{Range(from, to)}", ct);

    // ===== Quy trinh linh hoat: danh muc cong doan + mau quy trinh (V006) =====
    public Task<List<Stage>> GetStagesAsync(bool activeOnly = true, CancellationToken ct = default) =>
        GetListAsync<Stage>($"{_prefix}/stages?activeOnly={(activeOnly ? "true" : "false")}", ct);
    public Task<long> CreateStageAsync(object body, CancellationToken ct = default) =>
        PostForIdAsync($"{_prefix}/stages", body, ct);
    public Task UpdateStageAsync(long id, object body, CancellationToken ct = default) =>
        ActionAsync(HttpMethod.Put, $"{_prefix}/stages/{id}", body, ct);

    public Task<List<Routing>> GetRoutingsAsync(bool activeOnly = true, CancellationToken ct = default) =>
        GetListAsync<Routing>($"{_prefix}/routings?activeOnly={(activeOnly ? "true" : "false")}", ct);
    public Task<RoutingDetail?> GetRoutingAsync(long id, CancellationToken ct = default) =>
        GetOneAsync<RoutingDetail>($"{_prefix}/routings/{id}", ct);
    public Task<long> CreateRoutingAsync(object body, CancellationToken ct = default) =>
        PostForIdAsync($"{_prefix}/routings", body, ct);
    public Task UpdateRoutingAsync(long id, object body, CancellationToken ct = default) =>
        ActionAsync(HttpMethod.Put, $"{_prefix}/routings/{id}", body, ct);
    public Task DeleteRoutingAsync(long id, CancellationToken ct = default) =>
        ActionAsync(HttpMethod.Delete, $"{_prefix}/routings/{id}", null, ct);

    // ----- Sua buoc rieng cho tung don -----
    /// <summary>Ap 1 mau quy trinh vao don (co the bat dau tu buoc giua).</summary>
    public Task ApplyRoutingAsync(long orderId, object body, CancellationToken ct = default) =>
        ActionAsync(HttpMethod.Post, $"{_prefix}/production-steps/apply-routing/{orderId}", body, ct);
    /// <summary>Them 1 cong doan le vao don (ngoai mau).</summary>
    public Task<long> AddOrderStepAsync(long orderId, object body, CancellationToken ct = default) =>
        PostForIdAsync($"{_prefix}/production-steps/order/{orderId}", body, ct);
    /// <summary>Bat/tat co BO QUA cho 1 buoc (nhay buoc).</summary>
    public Task SkipStepAsync(long stepId, object body, CancellationToken ct = default) =>
        ActionAsync(HttpMethod.Put, $"{_prefix}/production-steps/{stepId}/skip", body, ct);
    public Task DeleteStepAsync(long stepId, CancellationToken ct = default) =>
        ActionAsync(HttpMethod.Delete, $"{_prefix}/production-steps/{stepId}", null, ct);
    /// <summary>Doi thu tu cac buoc cua don (gui danh sach id theo thu tu moi).</summary>
    public Task ReorderStepsAsync(long orderId, object body, CancellationToken ct = default) =>
        ActionAsync(HttpMethod.Put, $"{_prefix}/production-steps/order/{orderId}/reorder", body, ct);
}
