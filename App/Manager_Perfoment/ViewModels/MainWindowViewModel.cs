using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GM95.App.ManagerPerformance.Services;
using GM95.App.ManagerPerformance.ViewModels.Pages;

namespace GM95.App.ManagerPerformance.ViewModels;

/// <summary>
/// Shell cua app: sidebar (menu theo quy trinh nghiep vu) + vung content (trang dang chon).
/// </summary>
public sealed partial class MainWindowViewModel : ViewModelBase
{
    // Constructor khong tham so cho designer.
    public MainWindowViewModel() : this(new ApiClient(new AppConfig())) { }

    public MainWindowViewModel(ApiClient api)
    {
        // Sidebar theo dung quy trinh nghiep vu: Kho NVL -> Ma hang -> BOM -> Don SX
        //   -> Ke hoach -> San xuat (Cat/May/QC) -> Nhap kho TP -> Canh bao.
        // Tat ca deu co API server + man hinh that.
        // Moi muc co ICON (emoji) de de phan biet bang mat + nhan nhom (Group) de tach khoi.
        NavItems = new ObservableCollection<NavItem>
        {
            new("📊", "Tổng quan",             new DashboardViewModel(api),        "TỔNG QUAN"),
            new("📦", "Kho nguyên vật liệu",   new MaterialsViewModel(api),        "DANH MỤC"),
            new("👕", "Mã hàng",               new ProductsViewModel(api),         "DANH MỤC"),
            new("📋", "Định mức BOM",          new BomViewModel(api),              "DANH MỤC"),
            new("🧭", "Mẫu quy trình",         new RoutingsViewModel(api),         "DANH MỤC"),
            new("🧾", "Đơn hàng sản xuất",     new ProductionOrdersViewModel(api), "SẢN XUẤT"),
            new("🗓️", "Kế hoạch sản xuất",     new ProductionPlansViewModel(api),  "SẢN XUẤT"),
            new("✂️", "Sản xuất (Cắt/May/QC)", new ProductionViewModel(api),       "SẢN XUẤT"),
            new("🏭", "Nhập kho thành phẩm",   new FinishedGoodsViewModel(api),    "SẢN XUẤT"),
            new("🔔", "Cảnh báo",              new AlertsViewModel(api),           "BÁO CÁO"),
            new("📒", "Sổ & thống kê",         new StatsViewModel(api),            "BÁO CÁO"),
            new("📈", "So sánh tháng",         new MonthCompareViewModel(api),     "BÁO CÁO"),
        };

        // Gom cac muc theo Group (giu nguyen thu tu) -> hien tieu de nhom tren sidebar.
        NavGroups = new ObservableCollection<NavGroup>(
            NavItems.GroupBy(n => n.Group)
                    .Select(g => new NavGroup(g.Key, new ObservableCollection<NavItem>(g))));

        // Mo trang dau tien.
        Select(NavItems[0]);
    }

    public ObservableCollection<NavItem> NavItems { get; }
    /// <summary>Cac muc menu gom theo nhom (co tieu de) — cho sidebar de tim.</summary>
    public ObservableCollection<NavGroup> NavGroups { get; }

    [ObservableProperty] private PageViewModel? _currentPage;
    [ObservableProperty] private string _appTitle = "GM95";
    [ObservableProperty] private string _appSubtitle = "Quản lý sản xuất";

    /// <summary>Xuat Excel cac bang cua trang dang mo (hop thoai chon bang / pham vi / che do sheet).</summary>
    [RelayCommand]
    private Task ExportExcel() => ExportService.ExportAsync(CurrentPage);

    // QUAN TRONG: lenh chon tab la DONG BO (void), khong await.
    // Neu de async + await OnActivatedAsync, CommunityToolkit tu disable SelectCommand
    // trong suot qua trinh tai du lieu -> TAT CA nut sidebar bi xam, bam khong duoc.
    // => Doi trang ngay lap tuc, nap du lieu chay NEN (trang tu hien "Dang tai..." qua IsBusy).
    [RelayCommand]
    private void Select(NavItem item)
    {
        foreach (var n in NavItems) n.IsSelected = ReferenceEquals(n, item);
        CurrentPage = item.Page;
        _ = item.Page.OnActivatedAsync();
    }
}
