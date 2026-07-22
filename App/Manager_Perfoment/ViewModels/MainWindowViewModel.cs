using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DGroup.App.ManagerPerformance.Services;
using DGroup.App.ManagerPerformance.ViewModels.Pages;

namespace DGroup.App.ManagerPerformance.ViewModels;

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
        NavItems = new ObservableCollection<NavItem>
        {
            new("", "Tổng quan",             new DashboardViewModel(api)),
            new("", "Kho nguyên vật liệu",   new MaterialsViewModel(api)),
            new("", "Mã hàng",               new ProductsViewModel(api)),
            new("", "Định mức BOM",          new BomViewModel(api)),
            new("", "Đơn hàng sản xuất",     new ProductionOrdersViewModel(api)),
            new("", "Kế hoạch sản xuất",     new ProductionPlansViewModel(api)),
            new("", "Sản xuất (Cắt/May/QC)", new ProductionViewModel(api)),
            new("", "Nhập kho thành phẩm",   new FinishedGoodsViewModel(api)),
            new("", "Cảnh báo",              new AlertsViewModel(api)),
            new("", "So sánh tháng",         new MonthCompareViewModel(api)),
        };

        // Mo trang dau tien.
        Select(NavItems[0]);
    }

    public ObservableCollection<NavItem> NavItems { get; }

    [ObservableProperty] private PageViewModel? _currentPage;
    [ObservableProperty] private string _appTitle = "DGroup";
    [ObservableProperty] private string _appSubtitle = "Quản lý sản xuất";

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
