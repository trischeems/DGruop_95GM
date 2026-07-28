using CommunityToolkit.Mvvm.ComponentModel;

namespace GM95.App.ManagerPerformance.ViewModels;

/// <summary>Lop co so cho moi trang (man hinh) hien trong vung content ben phai.</summary>
public abstract partial class PageViewModel : ViewModelBase
{
    /// <summary>Tieu de trang (hien o dau vung content).</summary>
    public abstract string Title { get; }

    /// <summary>Phu de mo ta ngan.</summary>
    public virtual string Subtitle => "";

    /// <summary>Goi khi trang duoc mo (nap du lieu). Mac dinh khong lam gi.</summary>
    public virtual Task OnActivatedAsync() => Task.CompletedTask;
}
