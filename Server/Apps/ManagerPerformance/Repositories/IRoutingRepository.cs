using GM95.Server.Apps.ManagerPerformance.Contracts;
using GM95.Server.Infrastructure.Data;

namespace GM95.Server.Apps.ManagerPerformance.Repositories;

/// <summary>
/// Truy van/ghi DANH MUC CONG DOAN + MAU QUY TRINH (V006).
/// Ghi lam trong 1 transaction cua scope (khong tu mo connection).
/// </summary>
public interface IRoutingRepository
{
    // ----- Danh muc cong doan -----
    Task<IEnumerable<StageDto>> ListStagesAsync(TenantScope scope, bool activeOnly);
    Task<long> InsertStageAsync(TenantScope scope, CreateStageRequest req);
    Task<int> UpdateStageAsync(TenantScope scope, long id, UpdateStageRequest req);
    /// <summary>So mau quy trinh dang dung cong doan nay (chan ngung dung khi con mau tham chieu).</summary>
    Task<int> CountRoutingsUsingStageAsync(TenantScope scope, long stageId);
    Task<bool> StageCodeExistsAsync(TenantScope scope, string code, long? exceptId);

    // ----- Mau quy trinh -----
    Task<IEnumerable<RoutingDto>> ListRoutingsAsync(TenantScope scope, bool activeOnly);
    Task<RoutingDto?> GetRoutingAsync(TenantScope scope, long id);
    Task<IEnumerable<RoutingStepDto>> ListRoutingStepsAsync(TenantScope scope, long routingId);
    Task<bool> RoutingCodeExistsAsync(TenantScope scope, string code);
    Task<long> InsertRoutingAsync(TenantScope scope, CreateRoutingRequest req);
    Task<int> UpdateRoutingAsync(TenantScope scope, long id, UpdateRoutingRequest req);
    /// <summary>Bo co mac dinh o moi mau khac (chi 1 mau duoc mac dinh - unique index).</summary>
    Task<int> ClearDefaultAsync(TenantScope scope, long exceptId);
    Task<int> DeleteRoutingStepsAsync(TenantScope scope, long routingId);
    Task<int> InsertRoutingStepAsync(TenantScope scope, long routingId, RoutingStepInput step);
    /// <summary>So don dang dung mau nay (chan xoa mau da su dung).</summary>
    Task<int> CountOrdersUsingRoutingAsync(TenantScope scope, long routingId);
    Task<int> DeleteRoutingAsync(TenantScope scope, long id);
    Task<long?> FindDefaultRoutingIdAsync(TenantScope scope);

    // ----- Buoc cua tung don (sinh tu mau + sua rieng) -----
    /// <summary>Gan mau quy trinh cho don.</summary>
    Task<int> SetOrderRoutingAsync(TenantScope scope, long orderId, long routingId);
    /// <summary>Xoa cac buoc CHUA LAM (PENDING, chua co so lieu) cua don - phuc vu ap lai mau.</summary>
    Task<int> DeletePendingStepsAsync(TenantScope scope, long orderId);
    /// <summary>Sinh buoc cho don tu mau, bo qua cac buoc co seq nho hon startSeq. Tra ve so dong them.</summary>
    Task<int> GenerateStepsFromRoutingAsync(TenantScope scope, long orderId, long routingId, int startSeq);
    /// <summary>Them 1 cong doan le vao don (seq null = xep cuoi). Tra ve id buoc moi (0 neu da co).</summary>
    Task<long> AddOrderStepAsync(TenantScope scope, long orderId, long stageId, int? seq, string? note);
    /// <summary>Bat/tat co bo qua cho 1 buoc. Tra ve so dong doi.</summary>
    Task<int> SetStepSkippedAsync(TenantScope scope, long stepId, bool isSkipped, string? note);
    /// <summary>Doi seq cua 1 buoc.</summary>
    Task<int> SetStepSeqAsync(TenantScope scope, long stepId, int seq);
    /// <summary>Khoa buoc + tra trang thai/so lieu de kiem tra truoc khi xoa/bo qua.</summary>
    Task<OrderStepGuardRow?> LockOrderStepAsync(TenantScope scope, long stepId);
    Task<int> DeleteOrderStepAsync(TenantScope scope, long stepId);
    /// <summary>Id cac buoc cua don (de kiem tra reorder gui dung/du).</summary>
    Task<IEnumerable<long>> ListStepIdsAsync(TenantScope scope, long orderId);
}

/// <summary>Thong tin 1 buoc cua don (da khoa FOR UPDATE) de kiem tra truoc khi sua co cau.</summary>
public sealed record OrderStepGuardRow(
    long Id,
    long ProductionOrderId,
    string Status,
    decimal QtyIn,
    decimal QtyOut,
    bool IsSkipped);
