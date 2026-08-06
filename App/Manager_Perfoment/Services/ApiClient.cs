using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using GM95.App.ManagerPerformance.Models;

namespace GM95.App.ManagerPerformance.Services;

/// <summary>
/// Goi API server GM95 (/dgrpi/manager-performance/...). Tu gan header tenant (X-Tenant).
/// Tra ve ApiResult&lt;T&gt; da bung .Data; nem ApiException neu server bao loi hoac ket noi fail.
/// </summary>
public sealed partial class ApiClient
{
    private readonly HttpClient _http;
    private readonly AppConfig _config;
    private readonly string _prefix; // "dgrpi/manager-performance"

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    public ApiClient(AppConfig config)
    {
        _config = config;
        _prefix = $"{config.Server.ApiPrefix.Trim('/')}/manager-performance";
        _http = new HttpClient
        {
            BaseAddress = new Uri(config.Server.BaseUrl.TrimEnd('/') + "/"),
            Timeout = TimeSpan.FromSeconds(config.Server.TimeoutSecondsValue),
        };
        _http.DefaultRequestHeaders.Add(config.Tenant.Header, config.Tenant.Current);
    }

    /// <summary>Ping server (endpoint he thong /dgrpi/health, khong gan tenant).</summary>
    public async Task<bool> HealthAsync(CancellationToken ct = default)
    {
        try
        {
            using var resp = await _http.GetAsync($"{_config.Server.ApiPrefix.Trim('/')}/health", ct);
            return resp.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>Ghep query string thang/nam (bo qua khi null).</summary>
    private static string Period(int? year, int? month) =>
        (year is null ? "" : $"&year={year}") + (month is null ? "" : $"&month={month}");

    // ----- NVL -----
    public Task<List<Material>> GetMaterialsAsync(bool activeOnly = true, int? year = null, int? month = null, CancellationToken ct = default) =>
        GetListAsync<Material>($"{_prefix}/materials?activeOnly={activeOnly.ToString().ToLowerInvariant()}{Period(year, month)}", ct);

    public Task<long> CreateMaterialAsync(object request, CancellationToken ct = default) =>
        PostForIdAsync($"{_prefix}/materials", request, ct);

    /// <summary>Tim NVL gan giong theo SKU/ten (goi y chong trung khi tao moi).</summary>
    public Task<List<Material>> SearchMaterialsAsync(string query, int limit = 10, CancellationToken ct = default) =>
        GetListAsync<Material>($"{_prefix}/materials/search?q={Uri.EscapeDataString(query)}&limit={limit}", ct);

    // ----- Ton kho -----
    public Task<List<MaterialStock>> GetStockAsync(bool lowStockOnly = false, int? year = null, int? month = null, CancellationToken ct = default) =>
        GetListAsync<MaterialStock>($"{_prefix}/stock?lowStockOnly={lowStockOnly.ToString().ToLowerInvariant()}{Period(year, month)}", ct);

    public Task<StockTransactionResult> ReceiveStockAsync(object request, CancellationToken ct = default) =>
        PostAsync<StockTransactionResult>($"{_prefix}/stock/receive", request, ct);

    /// <summary>Tao phieu nhap kho NHIEU DONG (cong ton mọi dong trong 1 transaction).</summary>
    public Task<StockReceiptResult> CreateReceiptAsync(object request, CancellationToken ct = default) =>
        PostAsync<StockReceiptResult>($"{_prefix}/stock/receipts", request, ct);

    /// <summary>Danh sach phieu nhap kho (loc theo kho neu co).</summary>
    public Task<List<StockReceipt>> GetReceiptsAsync(long? warehouseId = null, CancellationToken ct = default) =>
        GetListAsync<StockReceipt>($"{_prefix}/stock/receipts" + (warehouseId.HasValue ? $"?warehouseId={warehouseId}" : ""), ct);

    /// <summary>Ghep query string tu ngay/den ngay (bo qua khi null; server hieu theo ngay dia phuong).</summary>
    private static string Range(DateTime? from, DateTime? to) =>
        (from is null ? "" : $"&from={from:yyyy-MM-dd}") + (to is null ? "" : $"&to={to:yyyy-MM-dd}");

    /// <summary>Lich su giao dich kho (don gia tung lan), loc theo NVL + tu/den ngay neu co.</summary>
    public Task<List<StockTransaction>> GetStockTransactionsAsync(
        long? materialId = null, int limit = 200, DateTime? from = null, DateTime? to = null, CancellationToken ct = default) =>
        GetListAsync<StockTransaction>(
            $"{_prefix}/stock/transactions?limit={limit}"
            + (materialId.HasValue ? $"&materialId={materialId}" : "") + Range(from, to), ct);

    /// <summary>Sua 1 dong nhap/xuat (so luong tuyet doi + don gia + ghi chu); server dong bo lai ton.</summary>
    public Task UpdateStockTransactionAsync(long id, object body, CancellationToken ct = default) =>
        ActionAsync(HttpMethod.Put, $"{_prefix}/stock/transactions/{id}", body, ct);

    /// <summary>Xoa 1 dong nhap/xuat; server tra ton ve nhu chua co dong nay.</summary>
    public Task DeleteStockTransactionAsync(long id, CancellationToken ct = default) =>
        ActionAsync(HttpMethod.Delete, $"{_prefix}/stock/transactions/{id}", null, ct);

    // ----- Bao cao -----
    public Task<List<MaterialStock>> GetLowStockAsync(int? year = null, int? month = null, CancellationToken ct = default) =>
        GetListAsync<MaterialStock>($"{_prefix}/reports/low-stock?x=1{Period(year, month)}", ct);

    // ----- helpers -----
    private async Task<List<T>> GetListAsync<T>(string url, CancellationToken ct)
    {
        var result = await SendAsync<List<T>>(HttpMethod.Get, url, null, ct);
        return result ?? new List<T>();
    }

    private async Task<long> PostForIdAsync(string url, object body, CancellationToken ct)
    {
        // Server tra { ok, data: { id } } khi tao.
        var el = await SendAsync<JsonElement>(HttpMethod.Post, url, body, ct);
        return el.ValueKind == JsonValueKind.Object && el.TryGetProperty("id", out var idEl)
            ? idEl.GetInt64()
            : 0;
    }

    private async Task<T> PostAsync<T>(string url, object body, CancellationToken ct)
    {
        var result = await SendAsync<T>(HttpMethod.Post, url, body, ct);
        return result ?? throw new ApiException("EMPTY", "Server tra du lieu rong.");
    }

    private async Task<T?> SendAsync<T>(HttpMethod method, string url, object? body, CancellationToken ct)
    {
        HttpResponseMessage resp;
        try
        {
            using var req = new HttpRequestMessage(method, url);
            if (body is not null) req.Content = JsonContent.Create(body);
            resp = await _http.SendAsync(req, ct);
        }
        catch (Exception ex)
        {
            throw new ApiException("CONNECT", $"Khong ket noi duoc server ({_http.BaseAddress}): {ex.Message}", ex);
        }

        var text = await resp.Content.ReadAsStringAsync(ct);
        ApiResult<T>? parsed = null;
        try { parsed = JsonSerializer.Deserialize<ApiResult<T>>(text, JsonOpts); } catch { /* fallthrough */ }

        if (!resp.IsSuccessStatusCode)
        {
            var msg = parsed?.Error?.Message ?? $"HTTP {(int)resp.StatusCode}";
            throw new ApiException(parsed?.Error?.Code ?? "HTTP", msg);
        }
        if (parsed is { Ok: false })
            throw new ApiException(parsed.Error?.Code ?? "ERROR", parsed.Error?.Message ?? "Loi khong xac dinh.");

        return parsed is null ? default : parsed.Data;
    }
}

/// <summary>Loi khi goi API (ket noi / server bao loi).</summary>
public sealed class ApiException : Exception
{
    public string Code { get; }
    public ApiException(string code, string message, Exception? inner = null) : base(message, inner) => Code = code;
}
