using System.Collections.ObjectModel;

namespace GM95.App.ManagerPerformance.ViewModels;

/// <summary>1 nhom muc menu tren sidebar: tieu de nhom + cac muc con.</summary>
public sealed class NavGroup
{
    public NavGroup(string title, ObservableCollection<NavItem> items)
    {
        Title = title;
        Items = items;
    }

    public string Title { get; }
    public ObservableCollection<NavItem> Items { get; }
}
