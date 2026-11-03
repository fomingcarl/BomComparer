// Location: ViewModels/MainViewModel.cs
using BomComparer.Models;
using BomComparer.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Data;

namespace BomComparer.ViewModels
{
    public partial class MainViewModel : ObservableObject
    {
        private readonly IExcelService _excelService;
        private readonly IComparisonService _comparisonService;

        [ObservableProperty]
        private ObservableCollection<BomDifference> _differences = new();

        public ICollectionView DifferencesView { get; }

        // --- NEW QUEUE PROPERTIES ---
        // Holds the visual list of file paths in the UI
        [ObservableProperty]
        private ObservableCollection<FileQueueItem> _fileQueue = new();

        // Tracks which file the user currently has selected in the UI listbox to enable deletion
        [ObservableProperty]
        private FileQueueItem _selectedQueuedFile;

        // Stores the actual BOM data in memory for the popup window, keyed by filename
        public Dictionary<string, List<BomItem>> LoadedBoms { get; private set; } = new();

        public List<string> AvailableIssueTypes { get; } = new()
        {
            "All", "Presence Mismatch", "Qty Mismatch", "Rev Change", "UoM Mismatch", "Cost Change", "Frequency Mismatch"
        };

        [ObservableProperty]
        private string _searchAllColumns = string.Empty;
        partial void OnSearchAllColumnsChanged(string value) => DifferencesView.Refresh();

        [ObservableProperty]
        private string _searchPartNo = string.Empty;
        partial void OnSearchPartNoChanged(string value) => DifferencesView.Refresh();

        [ObservableProperty]
        private string _searchIssueType = "All";
        partial void OnSearchIssueTypeChanged(string value) => DifferencesView.Refresh();

        [ObservableProperty]
        private bool _isBusy;

        [ObservableProperty]
        private string _statusMessage = "Ready";

        public MainViewModel(IExcelService excelService, IComparisonService comparisonService)
        {
            _excelService = excelService;
            _comparisonService = comparisonService;
            DifferencesView = CollectionViewSource.GetDefaultView(Differences);
            DifferencesView.Filter = FilterDifferences;
        }

        private bool FilterDifferences(object obj)
        {
            if (obj is not BomDifference diff) return false;

            bool matchesPart = string.IsNullOrWhiteSpace(SearchPartNo) || diff.PartNo.Contains(SearchPartNo, StringComparison.OrdinalIgnoreCase);
            bool matchesIssue = SearchIssueType == "All" || string.IsNullOrWhiteSpace(SearchIssueType) || diff.IssueType.Contains(SearchIssueType, StringComparison.OrdinalIgnoreCase);
            bool matchesAll = string.IsNullOrWhiteSpace(SearchAllColumns) ||
                              diff.PartNo.Contains(SearchAllColumns, StringComparison.OrdinalIgnoreCase) ||
                              diff.Description.Contains(SearchAllColumns, StringComparison.OrdinalIgnoreCase) ||
                              diff.IssueType.Contains(SearchAllColumns, StringComparison.OrdinalIgnoreCase) ||
                              diff.Details.Contains(SearchAllColumns, StringComparison.OrdinalIgnoreCase);

            return matchesPart && matchesIssue && matchesAll;
        }

        // --- NEW QUEUE MANAGEMENT COMMANDS ---
        [RelayCommand]
        private void AddFiles()
        {
            var openFileDialog = new OpenFileDialog { Multiselect = true, Filter = "Excel Files|*.xlsx" };
            if (openFileDialog.ShowDialog() == true)
            {
                foreach (var file in openFileDialog.FileNames)
                {
                    // Check if file is already in queue
                    if (!FileQueue.Any(f => f.FilePath == file))
                    {
                        FileQueue.Add(new FileQueueItem
                        {
                            FilePath = file,
                            OriginalName = Path.GetFileNameWithoutExtension(file)
                        });
                    }
                }
                StatusMessage = $"Added files to queue.";
            }
        }

        // NEW COMMAND
        [RelayCommand]
        private void SetNickname()
        {
            if (SelectedQueuedFile != null)
            {
                var dialog = new RenameWindow(SelectedQueuedFile.DisplayName);
                // If you want this centered on the main window, you'd ideally pass the owner, 
                // but this works perfectly as a standalone modal.
                if (dialog.ShowDialog() == true)
                {
                    SelectedQueuedFile.Nickname = dialog.NewName;
                    StatusMessage = $"Set nickname to '{dialog.NewName}'.";
                }
            }
        }

        [RelayCommand]
        private void RemoveFile()
        {
            if (SelectedQueuedFile != null)
            {
                FileQueue.Remove(SelectedQueuedFile);
                StatusMessage = "File removed from queue.";
            }
        }

        [RelayCommand]
        private void ClearQueue()
        {
            FileQueue.Clear();
            Differences.Clear();
            LoadedBoms.Clear();
            StatusMessage = "Queue cleared.";
        }

        [RelayCommand]
        private async Task RunComparisonAsync()
        {
            if (FileQueue.Count < 2)
            {
                System.Windows.MessageBox.Show("Please add at least 2 files to the queue to compare.", "Insufficient Files", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
                return;
            }

            IsBusy = true;
            StatusMessage = "Loading and comparing BOMs...";

            try
            {
                List<string> missingWarnings = new List<string>();

                var diffs = await Task.Run(() =>
                {
                    LoadedBoms.Clear();

                    // Loop through the dynamic queue and load everything
                    foreach (var fileItem in FileQueue)
                    {
                        var result = _excelService.ReadBom(fileItem.FilePath);

                        // We now extract the DisplayName (which is the Nickname if provided, or OriginalName if not)
                        string fileName = fileItem.DisplayName;

                        // The contingency here prevents a crash if a user accidentally nicknames two files the exact same thing
                        if (!LoadedBoms.ContainsKey(fileName))
                            LoadedBoms.Add(fileName, result.BomItems);
                        else
                            LoadedBoms.Add($"{fileName} ({Guid.NewGuid().ToString().Substring(0, 4)})", result.BomItems);

                        var missing = result.MissingColumns.Except(missingWarnings).ToList();
                        if (missing.Any()) missingWarnings.AddRange(missing);
                    }

                    // Pass the dictionary to our new N-way comparator
                    return _comparisonService.CompareBoms(LoadedBoms);
                });

                Differences.Clear();
                foreach (var d in diffs) Differences.Add(d);

                if (missingWarnings.Any())
                {
                    System.Windows.MessageBox.Show($"Warning: The following expected columns were not found and will be skipped:\n\n{string.Join(", ", missingWarnings)}",
                        "Missing Columns Detected", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
                }

                StatusMessage = $"Comparison complete. Found {diffs.Count} differences across {FileQueue.Count} files.";
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show(ex.Message, "Error Loading File", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
                StatusMessage = "Error encountered during comparison.";
            }
            finally
            {
                IsBusy = false;
            }
        }

        [RelayCommand]
        private async Task ExportReportAsync()
        {
            if (Differences.Count == 0) return;
            var saveFileDialog = new SaveFileDialog { Filter = "Excel Files|*.xlsx", DefaultExt = "xlsx" };

            if (saveFileDialog.ShowDialog() == true)
            {
                IsBusy = true;
                StatusMessage = "Exporting report...";

                var filteredList = new List<BomDifference>();
                foreach (BomDifference item in DifferencesView) filteredList.Add(item);

                try
                {
                    await Task.Run(() => _excelService.ExportComparison(saveFileDialog.FileName, filteredList));
                    StatusMessage = "Report exported successfully.";
                }
                catch (Exception ex)
                {
                    StatusMessage = $"Export Error: {ex.Message}";
                }
                finally
                {
                    IsBusy = false;
                }
            }
        }


        [RelayCommand]
        private async Task RunTopLevelComparisonAsync()
        {
            if (FileQueue.Count < 2)
            {
                System.Windows.MessageBox.Show("Please add at least 2 files to the queue to compare.", "Insufficient Files", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
                return;
            }

            IsBusy = true;
            StatusMessage = "Generating Top Level Comparison...";

            try
            {
                // ADDED: Instantiate the missing list here so the foreach loop can access it
                List<string> missingWarnings = new List<string>();

                var dataTable = await Task.Run(() =>
                {
                    // Ensure files are loaded into memory first
                    LoadedBoms.Clear();
                    // Loop through the dynamic queue and load everything
                    foreach (var fileItem in FileQueue)
                    {
                        var result = _excelService.ReadBom(fileItem.FilePath);

                        // We now extract the DisplayName (which is the Nickname if provided, or OriginalName if not)
                        string fileName = fileItem.DisplayName;

                        // The contingency here prevents a crash if a user accidentally nicknames two files the exact same thing
                        if (!LoadedBoms.ContainsKey(fileName))
                            LoadedBoms.Add(fileName, result.BomItems);
                        else
                            LoadedBoms.Add($"{fileName} ({Guid.NewGuid().ToString().Substring(0, 4)})", result.BomItems);

                        var missing = result.MissingColumns.Except(missingWarnings).ToList();
                        if (missing.Any()) missingWarnings.AddRange(missing);
                    }

                    return _comparisonService.GenerateTopLevelReport(LoadedBoms);
                });

                // Open the new window and pass the DataTable and the ExcelService
                var reportWindow = new TopLevelReportWindow(dataTable, _excelService);
                reportWindow.Show();

                // ADDED: Show the warning popup if columns were missing, just like the other comparison method
                if (missingWarnings.Any())
                {
                    System.Windows.MessageBox.Show($"Warning: The following expected columns were not found and will be skipped:\n\n{string.Join(", ", missingWarnings)}",
                        "Missing Columns Detected", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
                }

                StatusMessage = "Top Level Report Generated.";
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show(ex.Message, "Error", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
                StatusMessage = "Error generating report.";
            }
            finally
            {
                IsBusy = false;
            }
        }


        [RelayCommand]
        private void OpenReconciliationWindow()
        {
            var window = new ReconciliationWindow(_excelService, _comparisonService);
            // Setting the Owner locks it to the center of your main app screen
            window.Owner = App.Current.MainWindow;
            window.ShowDialog();
        }








    }
}