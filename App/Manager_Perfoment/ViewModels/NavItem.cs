using CommunityToolkit.Mvvm.ComponentModel;

namespace DGroup.App.ManagerPerformance.ViewModels;

/// <summary>Mot muc menu ben sidebar: icon + nhan + trang tuong ung.</summary>
public sealed partial class NavItem : ViewModelBase
{
    public NavItem(string icon, string label, PageViewModel page, string group = "")
    {
        Icon = icon;
        Label = label;
        Page = page;
        Group = group;
    }

    public string Icon { get; }
    public string Label { get; }
    public PageViewModel Page { get; }
    /// <summary>Nhom hien tren sidebar (DANH MUC / SAN XUAT / BAO CAO...) de tach khoi cho de tim.</summary>
    public string Group { get; }

    /// <summary>Dang duoc chon? (dieu khien class 'active' cua nut).</summary>
    [ObservableProperty] private bool _isSelected;
}
