using System.Collections.ObjectModel;
using System.IO;
using System.Windows.Input;
using HidGuard.App.Mvvm;
using HidGuard.Core.Audit;
using HidGuard.Core.Incidents;

namespace HidGuard.App.ViewModels;

public sealed class AuditLogViewModel : ObservableObject
{
    private readonly IAuditTrailService auditTrail;
    private readonly IIncidentStore? incidentStore;
    private readonly List<AuditEventItemViewModel> allEvents = new();
    private string searchText = string.Empty;
    private AuditEventType? selectedTypeFilter;
    private int eventCount;

    public ObservableCollection<AuditEventItemViewModel> FilteredEvents { get; } = new();

    public string SearchText
    {
        get => searchText;
        set
        {
            if (SetProperty(ref searchText, value))
            {
                ApplyFilter();
            }
        }
    }

    public AuditEventType? SelectedTypeFilter
    {
        get => selectedTypeFilter;
        set
        {
            if (SetProperty(ref selectedTypeFilter, value))
            {
                ApplyFilter();
            }
        }
    }

    public int EventCount
    {
        get => eventCount;
        set => SetProperty(ref eventCount, value);
    }

    private readonly Func<string?>? saveFilePathProvider;

    public IReadOnlyList<AuditEventType> AvailableEventTypes { get; } = Enum.GetValues<AuditEventType>();

    public ICommand RefreshCommand { get; }
    public ICommand ClearFilterCommand { get; }
    public ICommand ExportReportCommand { get; }

    public AuditLogViewModel(
        IAuditTrailService auditTrail,
        IIncidentStore? incidentStore = null,
        Func<string?>? saveFilePathProvider = null)
    {
        this.auditTrail = auditTrail ?? throw new ArgumentNullException(nameof(auditTrail));
        this.incidentStore = incidentStore;
        this.saveFilePathProvider = saveFilePathProvider;

        RefreshCommand = new AsyncRelayCommand(LoadAuditEventsAsync);
        ClearFilterCommand = new RelayCommand(ClearFilter);
        ExportReportCommand = new AsyncRelayCommand(ExportReportInteractiveAsync);

        this.auditTrail.AuditEventRecorded += OnAuditEventRecorded;
    }

    public async Task ExportReportAsync(Stream outputStream, IncidentExportFormat format = IncidentExportFormat.Json)
    {
        ArgumentNullException.ThrowIfNull(outputStream);
        if (incidentStore != null)
        {
            await incidentStore.ExportMetadataReportAsync(outputStream, format).ConfigureAwait(false);
        }
    }

    public async Task ExportReportInteractiveAsync()
    {
        if (incidentStore == null)
        {
            return;
        }

        var filePath = saveFilePathProvider != null
            ? saveFilePathProvider()
            : DefaultSaveFilePathProvider();

        if (string.IsNullOrWhiteSpace(filePath))
        {
            return;
        }

        var format = filePath.EndsWith(".csv", StringComparison.OrdinalIgnoreCase)
            ? IncidentExportFormat.Csv
            : IncidentExportFormat.Json;

        await using var stream = File.Create(filePath);
        await ExportReportAsync(stream, format).ConfigureAwait(false);
    }

    private static string? DefaultSaveFilePathProvider()
    {
        var dialog = new Microsoft.Win32.SaveFileDialog
        {
            Filter = "JSON Report (*.json)|*.json|CSV Report (*.csv)|*.csv",
            DefaultExt = ".json",
            FileName = $"incident-report-{DateTimeOffset.UtcNow:yyyyMMdd-HHmmss}.json"
        };

        return dialog.ShowDialog() == true ? dialog.FileName : null;
    }

    public async Task LoadAuditEventsAsync()
    {
        var rawEvents = await auditTrail.QueryRecentEventsAsync(limit: 200).ConfigureAwait(false);

        DispatchToUi(() =>
        {
            allEvents.Clear();
            foreach (var evt in rawEvents)
            {
                allEvents.Add(new AuditEventItemViewModel(evt));
            }

            ApplyFilter();
        });
    }

    private void OnAuditEventRecorded(object? sender, AuditEvent e)
    {
        DispatchToUi(() =>
        {
            var item = new AuditEventItemViewModel(e);
            allEvents.Insert(0, item);
            ApplyFilter();
        });
    }

    private void ClearFilter()
    {
        SearchText = string.Empty;
        SelectedTypeFilter = null;
    }

    private void ApplyFilter()
    {
        FilteredEvents.Clear();

        var query = allEvents.AsEnumerable();

        if (selectedTypeFilter.HasValue)
        {
            query = query.Where(e => e.EventType == selectedTypeFilter.Value);
        }

        if (!string.IsNullOrWhiteSpace(searchText))
        {
            query = query.Where(e =>
                e.DeviceId.Contains(searchText, StringComparison.OrdinalIgnoreCase) ||
                e.ActionTaken.Contains(searchText, StringComparison.OrdinalIgnoreCase) ||
                e.Outcome.Contains(searchText, StringComparison.OrdinalIgnoreCase) ||
                e.Details.Contains(searchText, StringComparison.OrdinalIgnoreCase));
        }

        foreach (var item in query)
        {
            FilteredEvents.Add(item);
        }

        EventCount = FilteredEvents.Count;
    }

    private static void DispatchToUi(Action action)
    {
        var dispatcher = System.Windows.Application.Current?.Dispatcher;
        if (dispatcher != null && !dispatcher.CheckAccess())
        {
            dispatcher.Invoke(action);
        }
        else
        {
            action();
        }
    }
}
