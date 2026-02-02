using IISLogAnalyzer_WPF.Models;
using IISLogAnalyzer_WPF.Services;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using System.Runtime.CompilerServices;
using CommunityToolkit.Mvvm.Input;

namespace IISLogAnalyzer_WPF.ViewModels
{
    public class MainViewModel : INotifyPropertyChanged
    {
        private readonly LogParser _parser;
        private List<LogEntry> _allLogs = new();
        private System.Threading.CancellationTokenSource? _filterCts;

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null) => 
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        
        protected bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
        {
            if (EqualityComparer<T>.Default.Equals(field, value)) return false;
            field = value;
            OnPropertyChanged(propertyName);
            return true;
        }

        private bool _isLoading;
        public bool IsLoading { get => _isLoading; set => SetProperty(ref _isLoading, value); }

        private string _statusMessage = "Ready";
        public string StatusMessage { get => _statusMessage; set => SetProperty(ref _statusMessage, value); }


        private string _loadedFileName = "No File Loaded";
        public string LoadedFileName { get => _loadedFileName; set => SetProperty(ref _loadedFileName, value); }


        private int _totalLogsInFile;
        public int TotalLogsInFile { get => _totalLogsInFile; set => SetProperty(ref _totalLogsInFile, value); }

        private int _totalRequests;
        public int TotalRequests { get => _totalRequests; set => SetProperty(ref _totalRequests, value); }

        private int _successCount;
        public int SuccessCount { get => _successCount; set => SetProperty(ref _successCount, value); }

        private int _errorCount;
        public int ErrorCount { get => _errorCount; set => SetProperty(ref _errorCount, value); }

        private int _redirectCount;
        public int RedirectCount { get => _redirectCount; set => SetProperty(ref _redirectCount, value); }

        private int _clientErrorCount;
        public int ClientErrorCount { get => _clientErrorCount; set => SetProperty(ref _clientErrorCount, value); }

        private int _serverErrorCount;
        public int ServerErrorCount { get => _serverErrorCount; set => SetProperty(ref _serverErrorCount, value); }

        private int _getRequestCount;
        public int GetRequestCount { get => _getRequestCount; set => SetProperty(ref _getRequestCount, value); }

        private int _postRequestCount;
        public int PostRequestCount { get => _postRequestCount; set => SetProperty(ref _postRequestCount, value); }

        private int _uniqueIpCount;
        public int UniqueIpCount { get => _uniqueIpCount; set => SetProperty(ref _uniqueIpCount, value); }

        // New Dashboard Metrics
        private int _tokenEndpointCount;
        public int TokenEndpointCount { get => _tokenEndpointCount; set => SetProperty(ref _tokenEndpointCount, value); }

        private string _mostRequestedUrl = "-";
        public string MostRequestedUrl { get => _mostRequestedUrl; set => SetProperty(ref _mostRequestedUrl, value); }

        private int _slowestResponseTime;
        public int SlowestResponseTime { get => _slowestResponseTime; set => SetProperty(ref _slowestResponseTime, value); }

        // Search Properties
        public ObservableCollection<string> SearchTypes { get; } = new() { "URL", "IP", "Method", "Status" };

        private string _searchType = "URL";
        public string SearchType
        {
            get => _searchType;
            set
            {
                if (SetProperty(ref _searchType, value))
                {
                    _ = ApplyFiltersAsync(); // Fire and forget
                }
            }
        }

        private string _searchText = "";
        public string SearchText
        {
            get => _searchText;
            set => SetProperty(ref _searchText, value);
        }

        private bool _showErrorsOnly;
        public bool ShowErrorsOnly 
        { 
            get => _showErrorsOnly; 
            set 
            {
                if (SetProperty(ref _showErrorsOnly, value))
                {
                    _ = ApplyFiltersAsync(); // Fire and forget
                }
            }
        }

        private DateTime? _startDate;
        public DateTime? StartDate 
        { 
            get => _startDate; 
            set 
            {
                if (SetProperty(ref _startDate, value))
                {
                    // Tarih seçildiğinde StartTime'ı da otomatik ayarla
                    if (value.HasValue)
                    {
                        if (IncludeStartTime)
                        {
                            // Checkbox açıksa: seçili saat ile
                            StartTime = value.Value.Date.AddHours(_startHour).AddMinutes(_startMinute);
                        }
                        else
                        {
                            // Checkbox kapalıysa: 00:00
                            StartTime = value.Value.Date;
                        }
                    }
                    else
                    {
                        StartTime = null;
                    }
                    OnPropertyChanged(nameof(StartDateDisplay));
                }
            }
        }

        private DateTime? _startTime;
        public DateTime? StartTime { get => _startTime; set => SetProperty(ref _startTime, value); }

        private DateTime? _endDate;
        public DateTime? EndDate 
        { 
            get => _endDate; 
            set 
            {
                if (SetProperty(ref _endDate, value))
                {
                    // Tarih seçildiğinde EndTime'ı da otomatik ayarla
                    if (value.HasValue)
                    {
                        if (IncludeEndTime)
                        {
                            // Checkbox açıksa: seçili saat ile
                            EndTime = value.Value.Date.AddHours(_endHour).AddMinutes(_endMinute);
                        }
                        else
                        {
                            // Checkbox kapalıysa: 23:59
                            EndTime = value.Value.Date.AddHours(23).AddMinutes(59);
                        }
                    }
                    else
                    {
                        EndTime = null;
                    }
                    OnPropertyChanged(nameof(EndDateDisplay));
                }
            }
        }

        private DateTime? _endTime;
        public DateTime? EndTime { get => _endTime; set => SetProperty(ref _endTime, value); }

        // Display properties for buttons
        public string StartDateDisplay => StartDate.HasValue && StartTime.HasValue 
            ? $"{StartDate.Value:dd.MM.yyyy} {StartTime.Value:HH:mm}" 
            : "Not selected";

        public string EndDateDisplay => EndDate.HasValue && EndTime.HasValue 
            ? $"{EndDate.Value:dd.MM.yyyy} {EndTime.Value:HH:mm}" 
            : "Not selected";

        // Hour and minute values (int for actual use)
        private int _startHour = 0;
        private int _startMinute = 0;
        private int _endHour = 23;
        private int _endMinute = 59;

        // String backing fields for TextBox binding (prevents circular updates)
        private string _startHourInput = "00";
        private string _startMinuteInput = "00";
        private string _endHourInput = "23";
        private string _endMinuteInput = "59";

        public string StartHourInput
        {
            get => _startHourInput;
            set
            {
                if (SetProperty(ref _startHourInput, value))
                {
                    // Parse sadece valid değerler için
                    if (int.TryParse(value, out int hour) && hour >= 0 && hour <= 23)
                    {
                        _startHour = hour;
                        UpdateStartTime();
                    }
                }
            }
        }

        public string StartMinuteInput
        {
            get => _startMinuteInput;
            set
            {
                if (SetProperty(ref _startMinuteInput, value))
                {
                    if (int.TryParse(value, out int minute) && minute >= 0 && minute <= 59)
                    {
                        _startMinute = minute;
                        UpdateStartTime();
                    }
                }
            }
        }

        public string EndHourInput
        {
            get => _endHourInput;
            set
            {
                if (SetProperty(ref _endHourInput, value))
                {
                    if (int.TryParse(value, out int hour) && hour >= 0 && hour <= 23)
                    {
                        _endHour = hour;
                        UpdateEndTime();
                    }
                }
            }
        }

        public string EndMinuteInput
        {
            get => _endMinuteInput;
            set
            {
                if (SetProperty(ref _endMinuteInput, value))
                {
                    if (int.TryParse(value, out int minute) && minute >= 0 && minute <= 59)
                    {
                        _endMinute = minute;
                        UpdateEndTime();
                    }
                }
            }
        }

        // Helper methods to update DateTime properties
        private void UpdateStartTime()
        {
            if (StartDate.HasValue)
            {
                var baseDate = StartDate.Value.Date;
                StartTime = baseDate.AddHours(_startHour).AddMinutes(_startMinute);
                OnPropertyChanged(nameof(StartDateDisplay));
            }
        }

        private void UpdateEndTime()
        {
            if (EndDate.HasValue)
            {
                var baseDate = EndDate.Value.Date;
                EndTime = baseDate.AddHours(_endHour).AddMinutes(_endMinute);
                OnPropertyChanged(nameof(EndDateDisplay));
            }
        }

        private bool _includeStartTime = false;
        public bool IncludeStartTime
        {
            get => _includeStartTime;
            set
            {
                if (SetProperty(ref _includeStartTime, value))
                {
                    if (!value)
                    {
                        // Checkbox kapatıldı: saatleri sıfırla
                        _startHour = 0;
                        _startMinute = 0;
                        _startHourInput = "00";
                        _startMinuteInput = "00";
                        // Mevcut StartDate varsa kullan
                        if (StartDate.HasValue)
                        {
                            StartTime = StartDate.Value.Date;
                        }
                        else
                        {
                            StartTime = null;
                        }
                        OnPropertyChanged(nameof(StartHourInput));
                        OnPropertyChanged(nameof(StartMinuteInput));
                        OnPropertyChanged(nameof(StartDateDisplay));
                    }
                    else
                    {
                        // Checkbox açıldı: mevcut tarihi saatlerle birleştir
                        if (StartDate.HasValue)
                        {
                            StartTime = StartDate.Value.Date.AddHours(_startHour).AddMinutes(_startMinute);
                            OnPropertyChanged(nameof(StartDateDisplay));
                        }
                    }
                }
            }
        }

        private bool _includeEndTime = false;
        public bool IncludeEndTime
        {
            get => _includeEndTime;
            set
            {
                if (SetProperty(ref _includeEndTime, value))
                {
                    if (!value)
                    {
                        // Checkbox kapatıldı: saatleri varsayılana çek
                        _endHour = 23;
                        _endMinute = 59;
                        _endHourInput = "23";
                        _endMinuteInput = "59";
                        // Mevcut EndDate varsa kullan
                        if (EndDate.HasValue)
                        {
                            EndTime = EndDate.Value.Date.AddHours(23).AddMinutes(59);
                        }
                        else
                        {
                            EndTime = null;
                        }
                        OnPropertyChanged(nameof(EndHourInput));
                        OnPropertyChanged(nameof(EndMinuteInput));
                        OnPropertyChanged(nameof(EndDateDisplay));
                    }
                    else
                    {
                        // Checkbox açıldı: mevcut tarihi saatlerle birleştir
                        if (EndDate.HasValue)
                        {
                            EndTime = EndDate.Value.Date.AddHours(_endHour).AddMinutes(_endMinute);
                            OnPropertyChanged(nameof(EndDateDisplay));
                        }
                    }
                }
            }
        }



        public ObservableCollection<LogEntry> FilteredLogs { get; } = new();

        private LogEntry? _selectedLogEntry;
        public LogEntry? SelectedLogEntry { get => _selectedLogEntry; set => SetProperty(ref _selectedLogEntry, value); }

        public ICommand LoadLogsCommand { get; }

        public ICommand ClearFiltersCommand { get; }
        public ICommand ApplyFiltersCommand { get; }

        public MainViewModel()
        {
            _parser = new LogParser();
            LoadLogsCommand = new SimpleRelayCommand(async (o) => await LoadLogsAsync());
            ClearFiltersCommand = new SimpleRelayCommand((o) => ClearFilters());
            ApplyFiltersCommand = new SimpleRelayCommand(async (o) => await ApplyFiltersAsync());
        }


        private void ClearFilters()
        {
             ShowErrorsOnly = false;
             SearchType = "URL";
             SearchText = "";
             StartDate = null;
             StartTime = null;
             EndDate = null;
             EndTime = null;
             
             // Reset hour/minute values
             _startHour = 0;
             _startMinute = 0;
             _endHour = 23;
             _endMinute = 59;
             
             // Reset string fields
             _startHourInput = "00";
             _startMinuteInput = "00";
             _endHourInput = "23";
             _endMinuteInput = "59";
             
             // Reset checkbox states
             IncludeStartTime = false;
             IncludeEndTime = false;
             
             OnPropertyChanged(nameof(StartHourInput));
             OnPropertyChanged(nameof(StartMinuteInput));
             OnPropertyChanged(nameof(EndHourInput));
             OnPropertyChanged(nameof(EndMinuteInput));
             OnPropertyChanged(nameof(StartDateDisplay));
             OnPropertyChanged(nameof(EndDateDisplay));
             
             _ = ApplyFiltersAsync(); // Fire and forget
        }


        private async Task LoadLogsAsync()
        {
            var dialog = new OpenFileDialog
            {
                Filter = "Log Files (*.log)|*.log|All Files (*.*)|*.*",
                Multiselect = false
            };

            if (dialog.ShowDialog() == true)
            {
                IsLoading = true;
                StatusMessage = "Parsing logs...";
                _allLogs.Clear();
                FilteredLogs.Clear();

                try
                {
                    var logs = await _parser.ParseLogFileAsync(dialog.FileName);
                    _allLogs.AddRange(logs);

                    
                    LoadedFileName = System.IO.Path.GetFileName(dialog.FileName);
                    TotalLogsInFile = logs.Count;
                    ClearFilters(); // Yeni log yüklendiğinde filtreleri temizle
                    StatusMessage = $"Loaded {logs.Count} entries.";

                }
                catch (Exception ex)
                {
                    StatusMessage = $"Error: {ex.Message}";
                }
                finally
                {
                    IsLoading = false;
                }
            }
        }

        private async Task ApplyFiltersAsync()
        {
            // Cancel previous operation
            _filterCts?.Cancel();
            _filterCts = new System.Threading.CancellationTokenSource();
            var token = _filterCts.Token;

            // Capture filter values to avoid closure issues
            var showErrors = ShowErrorsOnly;
            var searchText = SearchText;
            var searchType = SearchType;
            var startDate = StartDate;
            var startTime = StartTime;
            var endDate = EndDate;
            var endTime = EndTime;

            try 
            {
                // Run filtering on background thread
                var asyncResult = await Task.Run(() =>
                {
                    token.ThrowIfCancellationRequested();

                    var query = _allLogs.AsEnumerable();

                // Error filter
                if (showErrors) query = query.Where(l => l.IsError);

                // Search filter
                if (!string.IsNullOrWhiteSpace(searchText))
                {
                    switch (searchType)
                    {
                        case "URL":
                            query = query.Where(l =>
                                l.UriStem.Contains(searchText, StringComparison.OrdinalIgnoreCase) ||
                                l.UriQuery.Contains(searchText, StringComparison.OrdinalIgnoreCase));
                            break;
                        case "IP":
                            query = query.Where(l =>
                                l.ClientIp.Contains(searchText, StringComparison.OrdinalIgnoreCase));
                            break;
                        case "Method":
                            query = query.Where(l =>
                                l.Method.Equals(searchText, StringComparison.OrdinalIgnoreCase));
                            break;
                        case "Status":
                            if (int.TryParse(searchText, out int statusCode))
                            {
                                query = query.Where(l => l.StatusCode == statusCode);
                            }
                            break;
                    }
                }

                // Date & Time filters
                if (startDate.HasValue)
                {
                    var start = startDate.Value.Date + (startTime?.TimeOfDay ?? TimeSpan.Zero);
                    query = query.Where(l => l.Timestamp >= start);
                }

                if (endDate.HasValue)
                {
                    var end = endDate.Value.Date + (endTime?.TimeOfDay ?? TimeSpan.FromHours(23).Add(TimeSpan.FromMinutes(59)).Add(TimeSpan.FromSeconds(59)));
                    query = query.Where(l => l.Timestamp <= end);
                }

                var result = query.ToList();

                // Calculate all statistics in a single pass
                int successCount = 0, errorCount = 0, redirectCount = 0;
                int clientErrorCount = 0, serverErrorCount = 0;
                int getCount = 0, postCount = 0, tokenCount = 0;
                int maxTimeTaken = 0;
                var uniqueIps = new HashSet<string>();
                var urlCounts = new Dictionary<string, int>();

                // Check cancel before heavy loop
                token.ThrowIfCancellationRequested();

                foreach (var log in result)
                {
                    // Status code categories
                    if (log.StatusCode >= 200 && log.StatusCode < 300) successCount++;
                    if (log.IsError) errorCount++;
                    if (log.StatusCode >= 300 && log.StatusCode < 400) redirectCount++;
                    if (log.StatusCode >= 400 && log.StatusCode < 500) clientErrorCount++;
                    if (log.StatusCode >= 500 && log.StatusCode < 600) serverErrorCount++;

                    // HTTP methods
                    if (log.Method.Equals("GET", StringComparison.OrdinalIgnoreCase)) getCount++;
                    if (log.Method.Equals("POST", StringComparison.OrdinalIgnoreCase)) postCount++;

                    // Unique IPs
                    uniqueIps.Add(log.ClientIp);

                    // Token endpoint
                    if (log.UriStem.Contains("/token", StringComparison.OrdinalIgnoreCase) ||
                        log.UriQuery.Contains("token", StringComparison.OrdinalIgnoreCase))
                        tokenCount++;

                    // URL grouping
                    if (!urlCounts.ContainsKey(log.UriStem))
                        urlCounts[log.UriStem] = 0;
                    urlCounts[log.UriStem]++;

                    // Max time taken
                    if (log.TimeTaken > maxTimeTaken)
                        maxTimeTaken = log.TimeTaken;
                }

                token.ThrowIfCancellationRequested();

                // Find most requested URL
                string mostRequested = "-";
                if (urlCounts.Count > 0)
                {
                    mostRequested = urlCounts.OrderByDescending(kvp => kvp.Value).First().Key;
                }

                var statistics = new FilterStatistics
                {
                    TotalCount = result.Count,
                    SuccessCount = successCount,
                    ErrorCount = errorCount,
                    RedirectCount = redirectCount,
                    ClientErrorCount = clientErrorCount,
                    ServerErrorCount = serverErrorCount,
                    GetRequestCount = getCount,
                    PostRequestCount = postCount,
                    UniqueIpCount = uniqueIps.Count,
                    TokenEndpointCount = tokenCount,
                    MostRequestedUrl = mostRequested,
                    SlowestResponseTime = maxTimeTaken
                };

                return (Logs: result, Stats: statistics);
            }, token);

            // Update UI on UI thread
            FilteredLogs.Clear();
            foreach (var log in asyncResult.Logs)
            {
                FilteredLogs.Add(log);
            }

            // Update statistics
            var stats = asyncResult.Stats;
            TotalRequests = stats.TotalCount;
            SuccessCount = stats.SuccessCount;
            ErrorCount = stats.ErrorCount;
            RedirectCount = stats.RedirectCount;
            ClientErrorCount = stats.ClientErrorCount;
            ServerErrorCount = stats.ServerErrorCount;
            GetRequestCount = stats.GetRequestCount;
            PostRequestCount = stats.PostRequestCount;
            UniqueIpCount = stats.UniqueIpCount;
            TokenEndpointCount = stats.TokenEndpointCount;
            MostRequestedUrl = stats.MostRequestedUrl;
            SlowestResponseTime = stats.SlowestResponseTime;
            
            }
            catch (OperationCanceledException)
            {
                // Ignore cancellation
            }
        }

        // Helper class for statistics
        private class FilterStatistics
        {
            public int TotalCount { get; set; }
            public int SuccessCount { get; set; }
            public int ErrorCount { get; set; }
            public int RedirectCount { get; set; }
            public int ClientErrorCount { get; set; }
            public int ServerErrorCount { get; set; }
            public int GetRequestCount { get; set; }
            public int PostRequestCount { get; set; }
            public int UniqueIpCount { get; set; }
            public int TokenEndpointCount { get; set; }
            public string MostRequestedUrl { get; set; } = "-";
            public int SlowestResponseTime { get; set; }
        }



    }

    public class SimpleRelayCommand : ICommand
    {
        private readonly Action<object?> _execute;
        private readonly Predicate<object?>? _canExecute;
        public SimpleRelayCommand(Action<object?> execute, Predicate<object?>? canExecute = null) { _execute = execute; _canExecute = canExecute; }
        public event EventHandler? CanExecuteChanged { add { CommandManager.RequerySuggested += value; } remove { CommandManager.RequerySuggested -= value; } }
        public bool CanExecute(object? parameter) => _canExecute == null || _canExecute(parameter);
        public void Execute(object? parameter) => _execute(parameter);
    }
}
