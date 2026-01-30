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
                if (SetProperty(ref _searchType, value)) ApplyFilters();
            }
        }

        private string _searchText = "";
        public string SearchText
        {
            get => _searchText;
            set
            {
                if (SetProperty(ref _searchText, value)) ApplyFilters();
            }
        }

        private bool _showErrorsOnly;
        public bool ShowErrorsOnly 
        { 
            get => _showErrorsOnly; 
            set 
            {
                if (SetProperty(ref _showErrorsOnly, value)) ApplyFilters();
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
            ApplyFiltersCommand = new SimpleRelayCommand((o) => ApplyFilters());
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
             
             ApplyFilters();
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

        private void ApplyFilters()
        {
            var query = _allLogs.AsEnumerable();

            // Error filter
            if (ShowErrorsOnly) query = query.Where(l => l.IsError);

            // Search filter
            if (!string.IsNullOrWhiteSpace(SearchText))
            {
                switch (SearchType)
                {
                    case "URL":
                        query = query.Where(l =>
                            l.UriStem.Contains(SearchText, StringComparison.OrdinalIgnoreCase) ||
                            l.UriQuery.Contains(SearchText, StringComparison.OrdinalIgnoreCase));
                        break;
                    case "IP":
                        query = query.Where(l =>
                            l.ClientIp.Contains(SearchText, StringComparison.OrdinalIgnoreCase));
                        break;
                    case "Method":
                        query = query.Where(l =>
                            l.Method.Equals(SearchText, StringComparison.OrdinalIgnoreCase));
                        break;
                    case "Status":
                        if (int.TryParse(SearchText, out int statusCode))
                        {
                            query = query.Where(l => l.StatusCode == statusCode);
                        }
                        break;
                }
            }

            // Date & Time filters
            if (StartDate.HasValue)
            {
                var start = StartDate.Value.Date + (StartTime?.TimeOfDay ?? TimeSpan.Zero);
                query = query.Where(l => l.Timestamp >= start);
            }

            if (EndDate.HasValue)
            {
                var end = EndDate.Value.Date + (EndTime?.TimeOfDay ?? TimeSpan.FromHours(23).Add(TimeSpan.FromMinutes(59)).Add(TimeSpan.FromSeconds(59)));
                query = query.Where(l => l.Timestamp <= end);
            }


            var result = query.ToList();

            FilteredLogs.Clear();
            foreach (var log in result) FilteredLogs.Add(log);

            TotalRequests = result.Count;
            SuccessCount = result.Count(l => l.StatusCode >= 200 && l.StatusCode < 300);
            ErrorCount = result.Count(l => l.IsError);
            
            // HTTP Status Code Breakdown
            RedirectCount = result.Count(l => l.StatusCode >= 300 && l.StatusCode < 400);
            ClientErrorCount = result.Count(l => l.StatusCode >= 400 && l.StatusCode < 500);
            ServerErrorCount = result.Count(l => l.StatusCode >= 500 && l.StatusCode < 600);
            
            // HTTP Method Breakdown
            GetRequestCount = result.Count(l => l.Method.Equals("GET", StringComparison.OrdinalIgnoreCase));
            PostRequestCount = result.Count(l => l.Method.Equals("POST", StringComparison.OrdinalIgnoreCase));

            UniqueIpCount = result.Select(l => l.ClientIp).Distinct().Count();

            // New Metrics
            // Token endpoint count
            TokenEndpointCount = result.Count(l => 
                l.UriStem.Contains("/token", StringComparison.OrdinalIgnoreCase) || 
                l.UriQuery.Contains("token", StringComparison.OrdinalIgnoreCase));

            // Most requested URL
            if (result.Any())
            {
                var urlGroups = result.GroupBy(l => l.UriStem)
                                      .OrderByDescending(g => g.Count())
                                      .FirstOrDefault();
                MostRequestedUrl = urlGroups?.Key ?? "-";
            }
            else
            {
                MostRequestedUrl = "-";
            }

            // Slowest response time
            SlowestResponseTime = result.Any() ? result.Max(l => l.TimeTaken) : 0;
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
