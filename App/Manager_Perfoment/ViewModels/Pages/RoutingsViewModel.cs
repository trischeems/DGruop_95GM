using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DGroup.App.ManagerPerformance.Models;
using DGroup.App.ManagerPerformance.Services;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;

namespace DGroup.App.ManagerPerformance.ViewModels.Pages;

/// <summary>
/// Trang MAU QUY TRINH: quan ly danh muc cong doan + cac mau quy trinh san xuat.
/// Moi mau = 1 chuoi cong doan; don sản xuat chon 1 mau de sinh buoc.
/// </summary>
public sealed partial class RoutingsViewModel : PageViewModel
{
    private readonly ApiClient _api;
    private bool _suppressSelectionHook;

    public RoutingsViewModel(ApiClient api) => _api = api;

    public override string Title => "Mẫu quy trình sản xuất";
    public override string Subtitle => "Tạo chuỗi công đoạn riêng cho từng loại hàng — đơn chọn mẫu khi sản xuất";

    // ----- Trang thai chung -----
    [ObservableProperty] private string _status = "";
    [ObservableProperty] private bool _isBusy;

    // ----- Cot trai: danh sach mau -----
    [ObservableProperty] private string _listFilter = "";
    partial void OnListFilterChanged(string value) => ApplyRoutingFilter();

    [ObservableProperty] private Routing? _selectedRouting;
    partial void OnSelectedRoutingChanged(Routing? value)
    {
        if (_suppressSelectionHook) return;
        _ = LoadDetailCoreAsync();
    }

    public ObservableCollection<Routing> Routings { get; } = new();
    public ObservableCollection<Routing> FilteredRoutings { get; } = new();

    // ----- Danh muc cong doan -----
    public ObservableCollection<Stage> Stages { get; } = new();
    public ObservableCollection<Stage> FilteredStages { get; } = new();

    /// <summary>O tim cong doan o cot trai dialog soan mau.</summary>
    [ObservableProperty] private string _stageListFilter = "";
    partial void OnStageListFilterChanged(string value) => ApplyStageFilter();

    [ObservableProperty] private Stage? _selectedStage;          // cong doan chon de them vao mau
    [ObservableProperty] private Stage? _selectedCatalogStage;   // cong doan chon trong danh muc (de sua)

    partial void OnSelectedCatalogStageChanged(Stage? value)
    {
        if (value is null) return;
        StageName = value.Name;
        StageSeq = value.Seq;
        StageActive = value.IsActive;
    }

    // Form them/sua cong doan.
    [ObservableProperty] private string _stageCode = "";
    [ObservableProperty] private string _stageName = "";
    [ObservableProperty] private int _stageSeq;
    [ObservableProperty] private bool _stageActive = true;

    // ----- Form mau dang soan -----
    [ObservableProperty] private string _routingCode = "";
    [ObservableProperty] private string _routingName = "";
    [ObservableProperty] private string _routingNote = "";
    [ObservableProperty] private bool _routingIsDefault;
    [ObservableProperty] private bool _routingIsActive = true;

    /// <summary>Cac buoc cua mau dang soan (luoi giua trang).</summary>
    public ObservableCollection<RoutingLine> Lines { get; } = new();
    [ObservableProperty] private RoutingLine? _selectedLine;

    // =================================================================================
    // NAP DU LIEU
    // =================================================================================

    public override Task OnActivatedAsync() => LoadAsync();

    [RelayCommand]
    private Task LoadAsync() => RunAsync("Đang tải mẫu quy trình...", LoadCoreAsync);

    private async Task LoadCoreAsync()
    {
        var keepRouting = SelectedRouting?.Id;

        var stages = await _api.GetStagesAsync(activeOnly: false);
        Stages.Clear();
        foreach (var s in stages) Stages.Add(s);
        ApplyStageFilter();

        var routings = await _api.GetRoutingsAsync(activeOnly: false);
        Routings.Clear();
        foreach (var r in routings) Routings.Add(r);
        ApplyRoutingFilter();

        // Giu lai mau dang chon sau khi tai lai (khong bat lai hook de khoi nap 2 lan).
        _suppressSelectionHook = true;
        SelectedRouting = Routings.FirstOrDefault(r => r.Id == keepRouting) ?? Routings.FirstOrDefault();
        _suppressSelectionHook = false;

        await LoadDetailCoreAsync();
        Status = $"{Routings.Count} mẫu quy trình · {Stages.Count} công đoạn.";
    }

    /// <summary>Nap chi tiet mau dang chon vao form soan.</summary>
    private async Task LoadDetailCoreAsync()
    {
        Lines.Clear();
        if (SelectedRouting is null)
        {
            RoutingCode = RoutingName = RoutingNote = "";
            RoutingIsDefault = false;
            RoutingIsActive = true;
            return;
        }

        RoutingCode = SelectedRouting.Code;
        RoutingName = SelectedRouting.Name;
        RoutingNote = SelectedRouting.Note ?? "";
        RoutingIsDefault = SelectedRouting.IsDefault;
        RoutingIsActive = SelectedRouting.IsActive;

        var detail = await _api.GetRoutingAsync(SelectedRouting.Id);
        if (detail is null) return;
        foreach (var st in detail.Steps.OrderBy(s => s.Seq))
            Lines.Add(new RoutingLine
            {
                Seq = st.Seq,
                Stage = Stages.FirstOrDefault(g => g.Id == st.StageId),
                IsOptional = st.IsOptional,
                Note = st.Note
            });
    }

    private void ApplyRoutingFilter()
    {
        var kw = (ListFilter ?? "").Trim();
        FilteredRoutings.Clear();
        foreach (var r in Routings)
            if (kw.Length == 0
                || r.Name.Contains(kw, StringComparison.OrdinalIgnoreCase)
                || r.Code.Contains(kw, StringComparison.OrdinalIgnoreCase))
                FilteredRoutings.Add(r);
    }

    private void ApplyStageFilter()
    {
        var kw = (StageListFilter ?? "").Trim();
        FilteredStages.Clear();
        foreach (var s in Stages)
            if (kw.Length == 0
                || s.Name.Contains(kw, StringComparison.OrdinalIgnoreCase)
                || s.Code.Contains(kw, StringComparison.OrdinalIgnoreCase))
                FilteredStages.Add(s);
    }

    // =================================================================================
    // SOAN CHUOI BUOC
    // =================================================================================

    /// <summary>Them cong doan dang chon vao cuoi chuoi buoc.</summary>
    [RelayCommand]
    private void AddLine()
    {
        if (SelectedStage is null) { Status = "Chọn công đoạn để thêm."; return; }
        if (Lines.Any(l => l.Stage?.Id == SelectedStage.Id))
        {
            Status = "Công đoạn này đã có trong quy trình.";
            return;
        }
        Lines.Add(new RoutingLine { Seq = Lines.Count + 1, Stage = SelectedStage, IsOptional = false });
        Renumber();
        Status = $"Đã thêm công đoạn {SelectedStage.Name}.";
    }

    [RelayCommand]
    private void RemoveLine()
    {
        if (SelectedLine is null) { Status = "Chọn bước cần bỏ."; return; }
        Lines.Remove(SelectedLine);
        Renumber();
        Status = "Đã bỏ bước khỏi quy trình.";
    }

    [RelayCommand]
    private void MoveLineUp() => MoveLine(-1);

    [RelayCommand]
    private void MoveLineDown() => MoveLine(+1);

    private void MoveLine(int delta)
    {
        if (SelectedLine is null) { Status = "Chọn bước cần đổi thứ tự."; return; }
        var i = Lines.IndexOf(SelectedLine);
        var j = i + delta;
        if (j < 0 || j >= Lines.Count) return;
        var line = SelectedLine;
        Lines.Move(i, j);
        Renumber();
        SelectedLine = line;
    }

    /// <summary>Danh lai so thu tu 1..n cho toan bo chuoi buoc.</summary>
    private void Renumber()
    {
        for (var i = 0; i < Lines.Count; i++) Lines[i].Seq = i + 1;
    }

    // =================================================================================
    // LUU MAU
    // =================================================================================

    /// <summary>Tao mau moi (mo form trong 1 cua so rieng).</summary>
    [RelayCommand]
    private async Task NewRoutingAsync()
    {
        _suppressSelectionHook = true;
        SelectedRouting = null;
        _suppressSelectionHook = false;

        RoutingCode = RoutingName = RoutingNote = "";
        RoutingIsDefault = false;
        RoutingIsActive = true;
        Lines.Clear();
        SelectedStage = null;

        var form = new Views.Dialogs.RoutingFormView { DataContext = this };
        await DialogService.ShowFormAsync("Tạo mẫu quy trình mới", form, async () =>
        {
            var err = ValidateForm();
            if (err is not null) return err;

            var id = await _api.CreateRoutingAsync(new
            {
                code = RoutingCode.Trim(),
                name = RoutingName.Trim(),
                note = string.IsNullOrWhiteSpace(RoutingNote) ? null : RoutingNote.Trim(),
                isDefault = RoutingIsDefault,
                steps = BuildSteps()
            });
            await LoadCoreAsync();
            _suppressSelectionHook = true;
            SelectedRouting = Routings.FirstOrDefault(r => r.Id == id);
            _suppressSelectionHook = false;
            await LoadDetailCoreAsync();
            Status = $"Đã tạo mẫu quy trình {RoutingCode} ({Lines.Count} bước).";
            return null;
        }, saveText: "Tạo mẫu", width: 940, height: 600, scrollable: false);
    }

    /// <summary>Sua mau dang chon (mo form trong 1 cua so rieng).</summary>
    [RelayCommand]
    private async Task EditRoutingAsync()
    {
        if (SelectedRouting is null) { Status = "Chọn mẫu quy trình cần sửa."; return; }
        await LoadDetailCoreAsync();

        var id = SelectedRouting.Id;
        var form = new Views.Dialogs.RoutingFormView { DataContext = this };
        await DialogService.ShowFormAsync($"Sửa mẫu quy trình — {SelectedRouting.Code}", form, async () =>
        {
            var err = ValidateForm();
            if (err is not null) return err;

            await _api.UpdateRoutingAsync(id, new
            {
                name = RoutingName.Trim(),
                note = string.IsNullOrWhiteSpace(RoutingNote) ? null : RoutingNote.Trim(),
                isActive = RoutingIsActive,
                isDefault = RoutingIsDefault,
                steps = BuildSteps()
            });
            await LoadCoreAsync();
            Status = $"Đã cập nhật mẫu quy trình {RoutingCode}.";
            return null;
        }, saveText: "Lưu mẫu", width: 940, height: 600, scrollable: false);
    }

    [RelayCommand]
    private async Task DeleteRoutingAsync()
    {
        if (SelectedRouting is null) { Status = "Chọn mẫu quy trình cần xoá."; return; }
        var r = SelectedRouting;
        var ok = await DialogService.ConfirmAsync(
            "Xoá mẫu quy trình",
            $"Xoá mẫu '{r.Name}' ({r.Code}) gồm {r.StepCount} bước?\n\n" +
            "Nếu đang có đơn dùng mẫu này, hệ thống sẽ chặn — khi đó hãy chuyển sang 'Ngừng dùng'.",
            "Xoá mẫu", danger: true);
        if (!ok) return;

        await RunAsync("Đang xoá mẫu quy trình...", async () =>
        {
            await _api.DeleteRoutingAsync(r.Id);
            await LoadCoreAsync();
            Status = $"Đã xoá mẫu {r.Code}.";
        });
    }

    /// <summary>Kiem tra form truoc khi luu (tra ve chuoi loi, null = hop le).</summary>
    private string? ValidateForm()
    {
        if (string.IsNullOrWhiteSpace(RoutingCode)) return "Nhập mã quy trình (vd RT-AO).";
        if (string.IsNullOrWhiteSpace(RoutingName)) return "Nhập tên quy trình.";
        if (Lines.Count == 0) return "Quy trình phải có ít nhất 1 bước. Chọn công đoạn rồi bấm '+ Thêm bước'.";
        if (Lines.Any(l => l.Stage is null)) return "Có bước chưa chọn công đoạn.";
        return null;
    }

    private object[] BuildSteps() =>
        Lines.OrderBy(l => l.Seq)
             .Select(l => (object)new
             {
                 stageId = l.Stage!.Id,
                 seq = l.Seq,
                 isOptional = l.IsOptional,
                 note = string.IsNullOrWhiteSpace(l.Note) ? null : l.Note
             })
             .ToArray();

    // =================================================================================
    // DANH MUC CONG DOAN
    // =================================================================================

    /// <summary>Them cong doan moi vao danh muc (In, Theu, Say, Dong goi...).</summary>
    [RelayCommand]
    private async Task NewStageAsync()
    {
        StageCode = StageName = "";
        StageSeq = Stages.Count + 1;
        StageActive = true;

        var form = new Views.Dialogs.StageFormView { DataContext = this };
        await DialogService.ShowFormAsync("Thêm công đoạn", form, async () =>
        {
            if (string.IsNullOrWhiteSpace(StageCode)) return "Nhập mã công đoạn (vd PRINTING).";
            if (string.IsNullOrWhiteSpace(StageName)) return "Nhập tên công đoạn.";

            await _api.CreateStageAsync(new
            {
                code = StageCode.Trim().ToUpperInvariant(),
                name = StageName.Trim(),
                seq = StageSeq > 0 ? StageSeq : (int?)null
            });
            await LoadCoreAsync();
            Status = $"Đã thêm công đoạn {StageName}.";
            return null;
        }, saveText: "Thêm công đoạn", width: 520, height: 360);
    }

    /// <summary>Sua cong doan dang chon trong danh muc.</summary>
    [RelayCommand]
    private async Task EditStageAsync()
    {
        if (SelectedCatalogStage is null) { Status = "Chọn công đoạn cần sửa."; return; }
        var st = SelectedCatalogStage;
        StageCode = st.Code;
        StageName = st.Name;
        StageSeq = st.Seq;
        StageActive = st.IsActive;

        var form = new Views.Dialogs.StageFormView { DataContext = this };
        await DialogService.ShowFormAsync($"Sửa công đoạn — {st.Code}", form, async () =>
        {
            if (string.IsNullOrWhiteSpace(StageName)) return "Nhập tên công đoạn.";

            await _api.UpdateStageAsync(st.Id, new
            {
                name = StageName.Trim(),
                seq = StageSeq > 0 ? StageSeq : (int?)null,
                isActive = StageActive
            });
            await LoadCoreAsync();
            Status = $"Đã cập nhật công đoạn {StageName}.";
            return null;
        }, saveText: "Lưu công đoạn", width: 520, height: 360);
    }

    // =================================================================================
    private async Task RunAsync(string busy, Func<Task> a)
    {
        if (IsBusy) return;
        IsBusy = true; Status = busy;
        try { await a(); }
        catch (ApiException ex) { Status = $"Lỗi [{ex.Code}]: {ex.Message}"; }
        catch (Exception ex) { Status = $"Lỗi: {ex.Message}"; }
        finally { IsBusy = false; }
    }
}
