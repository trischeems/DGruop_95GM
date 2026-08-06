using GM95.App.ManagerPerformance.Models;
using GM95.App.ManagerPerformance.ViewModels;

namespace GM95.App.ManagerPerformance.Services;

/// <summary>
/// Thao tac SUA / XOA 1 dong so nhap-xuat kho, dung chung cho moi trang co bang so
/// (Kho NVL, So &amp; thong ke). Goi tu menu chuot phai cua bang.
/// Tra ve true neu du lieu da doi (trang goi phai nap lai bang).
/// </summary>
public static class StockTxnActions
{
    /// <summary>Mo cua so sua 1 dong so (SL / don gia / ghi chu). true = da luu.</summary>
    public static async Task<bool> EditAsync(ApiClient api, StockTransaction? txn)
    {
        if (txn is null)
        {
            await DialogService.InfoAsync("Sửa dòng sổ", "Chọn một dòng trong sổ nhập/xuất trước.");
            return false;
        }

        var vm = new StockTxnEditViewModel(txn);
        return await DialogService.ShowFormAsync(
            "Sửa dòng sổ nhập / xuất",
            new Views.Dialogs.StockTxnEditView { DataContext = vm },
            async () =>
            {
                if (vm.Quantity <= 0) return "Số lượng phải lớn hơn 0.";
                if (vm.UnitCost < 0) return "Đơn giá không được âm.";
                await api.UpdateStockTransactionAsync(txn.Id, new
                {
                    quantity = vm.Quantity,
                    unitCost = vm.UnitCost,
                    note = string.IsNullOrWhiteSpace(vm.Note) ? null : vm.Note.Trim(),
                });
                return null;
            },
            saveText: "Lưu thay đổi", width: 620, height: 400);
    }

    /// <summary>Hoi xac nhan roi xoa 1 dong so (hoan lai ton kho). true = da xoa.</summary>
    public static async Task<bool> DeleteAsync(ApiClient api, StockTransaction? txn)
    {
        if (txn is null)
        {
            await DialogService.InfoAsync("Xoá dòng sổ", "Chọn một dòng trong sổ nhập/xuất trước.");
            return false;
        }

        var loai = Converters.CodeToVietnameseConverter.Translate(txn.TxnType);
        var huong = txn.Quantity >= 0 ? "trừ lại" : "cộng lại";
        var ok = await DialogService.ConfirmAsync(
            "Xoá dòng sổ nhập / xuất",
            $"Xoá bút toán {loai} ngày {txn.CreatedAt:dd/MM/yyyy HH:mm}?\n" +
            $"• NVL: {txn.MaterialSku} — {txn.MaterialName}\n" +
            $"• Số lượng: {txn.Quantity:0.####} {txn.MaterialUomName} · kho {txn.WarehouseName}\n\n" +
            $"Tồn kho sẽ được {huong} {Math.Abs(txn.Quantity):0.####} {txn.MaterialUomName}, " +
            "dòng tương ứng trong phiếu nhập/xuất cũng bị xoá và cột 'Tồn sau' của các dòng sau sẽ được tính lại.",
            "Xoá dòng sổ", danger: true);
        if (!ok) return false;

        await api.DeleteStockTransactionAsync(txn.Id);
        return true;
    }
}
