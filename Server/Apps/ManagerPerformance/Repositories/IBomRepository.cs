using GM95.Server.Apps.ManagerPerformance.Contracts;
using GM95.Server.Infrastructure.Data;

namespace GM95.Server.Apps.ManagerPerformance.Repositories;

/// <summary>Truy van/ghi dinh muc (BOM). Cac buoc ghi lam trong 1 transaction cua scope.</summary>
public interface IBomRepository
{
    /// <summary>Danh sach header BOM (loc theo product neu co), sap xep product_id, version desc.</summary>
    Task<IEnumerable<BomDto>> ListAsync(TenantScope scope, long? productId);

    /// <summary>Header 1 BOM (null neu khong co).</summary>
    Task<BomDto?> GetHeaderAsync(TenantScope scope, long id);

    /// <summary>Cac dong NVL cua 1 BOM.</summary>
    Task<IEnumerable<BomItemDto>> ListItemsAsync(TenantScope scope, long bomId);

    /// <summary>Trang thai hien tai cua BOM (null neu khong co).</summary>
    Task<string?> GetStatusAsync(TenantScope scope, long id);

    /// <summary>Tao BOM DRAFT moi: version = COALESCE(MAX(version),0)+1 cho product. Tra ve id.</summary>
    Task<long> InsertBomAsync(TenantScope scope, long productId, string? note);

    /// <summary>Them/cap nhat 1 dong NVL (upsert theo bom_id + material_id). Tra ve id dong item.</summary>
    Task<long> UpsertItemAsync(TenantScope scope, long bomId, UpsertBomItemRequest req);

    /// <summary>Xoa 1 dong NVL theo bom_id + material_id. Tra ve so dong bi xoa.</summary>
    Task<int> DeleteItemAsync(TenantScope scope, long bomId, long materialId);

    /// <summary>Ghi 1 dong lich su thay doi.</summary>
    Task InsertHistoryAsync(
        TenantScope scope, long bomId, long? bomItemId, long? materialId,
        string changeType, decimal? oldValue, decimal? newValue, string? reason);

    /// <summary>Doi trang thai BOM.</summary>
    Task SetStatusAsync(TenantScope scope, long id, string status);

    /// <summary>Kich hoat BOM: status='ACTIVE', effective_from=now().</summary>
    Task ActivateAsync(TenantScope scope, long id);

    /// <summary>Luu kho (ARCHIVE) BOM dang ACTIVE cung product voi @id (giai phong partial unique index).</summary>
    Task ArchiveActiveOfSameProductAsync(TenantScope scope, long id);

    /// <summary>Tao 1 phieu trinh duyet PENDING cho BOM.</summary>
    Task InsertPendingApprovalAsync(TenantScope scope, long bomId);

    /// <summary>Ra quyet dinh cho phieu PENDING moi nhat cua BOM (APPROVED/REJECTED). Tra ve so dong cap nhat.</summary>
    Task<int> DecideLatestPendingApprovalAsync(
        TenantScope scope, long bomId, string status, long? decidedBy, string? note);

    /// <summary>Danh sach phieu trinh duyet cua BOM, sap xep requested_at desc.</summary>
    Task<IEnumerable<BomApprovalDto>> ListApprovalsAsync(TenantScope scope, long bomId);

    /// <summary>Danh sach lich su thay doi cua BOM, sap xep created_at desc.</summary>
    Task<IEnumerable<BomChangeHistoryDto>> ListHistoryAsync(TenantScope scope, long bomId);
    /// <summary>Anh huong khi xoa BOM (trang thai, so dong, so don dang dung). Null neu khong ton tai.</summary>
    Task<BomImpactDto?> GetImpactAsync(TenantScope scope, long id);
    /// <summary>Xoa BOM (items/history/approvals cascade). Tra ve true neu co dong bi xoa.</summary>
    Task<bool> DeleteAsync(TenantScope scope, long id);
}
