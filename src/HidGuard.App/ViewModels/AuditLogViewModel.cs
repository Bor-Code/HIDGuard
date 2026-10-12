using System.Collections.ObjectModel;
using System.Windows.Input;
using HidGuard.App.Mvvm;
using HidGuard.Core.Audit;

namespace HidGuard.App.ViewModels;

public sealed class AuditLogViewModel : ObservableObject
{
    private readonly IAuditTrailService auditTrail;
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

    public IReadOnlyList<AuditEventType> AvailableEventTypes { get; } = Enum.GetValues<AuditEventType>();

    public ICommand RefreshCommand { get; }
    public ICommand ClearFilterCommand { get; }

    public AuditLogViewModel(IAuditTrailService auditTrail)
    {
        this.auditTrail = auditTrail ?? throw new ArgumentNullException(nameof(auditTrail));

        RefreshCommand = new AsyncRelayCommand(LoadAuditEventsAsync);
        ClearFilterCommand = new RelayCommand(ClearFilter);

        this.auditTrail.AuditEventRecorded += OnAuditEventRecorded;
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
