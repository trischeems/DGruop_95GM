namespace GM95.App.ManagerPerformance.ViewModels.Pages;

/// <summary>
/// Trang "Sap co" cho cac buoc quy trinh chua co API server (BOM, Don SX, Ke hoach,
/// San xuat Cat-May-QC, Nhap kho thanh pham). Bang DB da co, controller se bo sung sau.
/// </summary>
public sealed class PlaceholderViewModel : PageViewModel
{
    public PlaceholderViewModel(string title, string subtitle, string note)
    {
        Title = title;
        _subtitle = subtitle;
        Note = note;
    }

    public override string Title { get; }
    private readonly string _subtitle;
    public override string Subtitle => _subtitle;

    /// <summary>Mo ta phan nghiep vu se lam o day.</summary>
    public string Note { get; }
}
