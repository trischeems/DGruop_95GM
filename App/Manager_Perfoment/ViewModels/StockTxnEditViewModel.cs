using CommunityToolkit.Mvvm.ComponentModel;
using GM95.App.ManagerPerformance.Models;

namespace GM95.App.ManagerPerformance.ViewModels;

/// <summary>
/// Than form SUA 1 dong so nhap/xuat kho (chuot phai vao dong -> Sua).
/// Chi cho sua SO LUONG / DON GIA / GHI CHU; loai giao dich va NVL giu nguyen.
/// </summary>
public sealed partial class StockTxnEditViewModel : ObservableObject
{
    public StockTransaction Txn { get; }

    /// <summary>Mo ta dong dang sua (hien o dau form, khong sua duoc).</summary>
    public string Header { get; }

    /// <summary>So luong dang sua — luon la so DUONG (chieu nhap/xuat giu theo dong goc).</summary>
    [ObservableProperty] private decimal _quantity;
    [ObservableProperty] private decimal _unitCost;
    [ObservableProperty] private string _note = "";

    public StockTxnEditViewModel(StockTransaction txn)
    {
        Txn = txn;
        Quantity = Math.Abs(txn.Quantity);
        UnitCost = txn.UnitCost;
        Note = txn.Note ?? "";
        var loai = Converters.CodeToVietnameseConverter.Translate(txn.TxnType);
        Header = $"{loai} · {txn.MaterialSku} — {txn.MaterialName} · kho {txn.WarehouseName} · " +
                 $"{txn.CreatedAt:dd/MM/yyyy HH:mm}";
    }
}
