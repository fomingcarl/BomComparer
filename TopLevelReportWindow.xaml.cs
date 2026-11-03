using System.Data;
using System.Windows;
using Microsoft.Win32;
using BomComparer.Services;
using System.Collections.Generic;

namespace BomComparer
{
    public partial class TopLevelReportWindow : Window
    {
        private readonly DataTable _reportData;
        private readonly IExcelService _excelService;

        public TopLevelReportWindow(DataTable reportData, IExcelService excelService)
        {
            InitializeComponent();
            _reportData = reportData;
            _excelService = excelService;

            ReportGrid.ItemsSource = _reportData.DefaultView;
        }

        // NEW: Live filtering logic for the DataTable
        private void Filter_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            if (_reportData == null) return;

            // Escaping single quotes to prevent SQL syntax crashes
            string partFilter = PartNoFilter.Text.Trim().Replace("'", "''");
            string catFilter = CategoryFilter.Text.Trim().Replace("'", "''");

            var filters = new List<string>();

            // [Part Number] requires brackets because the column name contains a space
            if (!string.IsNullOrEmpty(partFilter))
                filters.Add($"[Part Number] LIKE '%{partFilter}%'");

            if (!string.IsNullOrEmpty(catFilter))
                filters.Add($"Category LIKE '%{catFilter}%'");

            // Combine filters and apply to the DataView
            _reportData.DefaultView.RowFilter = string.Join(" AND ", filters);
        }

        private void ExportButton_Click(object sender, RoutedEventArgs e)
        {
            var saveFileDialog = new SaveFileDialog { Filter = "Excel Files|*.xlsx", DefaultExt = "xlsx" };

            if (saveFileDialog.ShowDialog() == true)
            {
                try
                {
                    // Export the dynamically filtered view, not the underlying raw data
                    _excelService.ExportDataTable(saveFileDialog.FileName, _reportData.DefaultView.ToTable(), "Consolidated Comparison");
                    MessageBox.Show("Report exported successfully.", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (System.Exception ex)
                {
                    MessageBox.Show($"Export Error: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }
    }
}