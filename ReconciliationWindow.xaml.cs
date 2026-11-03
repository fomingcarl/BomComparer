using System;
using System.Threading.Tasks;
using System.Windows;
using Microsoft.Win32;
using BomComparer.Services;

namespace BomComparer
{
    public partial class ReconciliationWindow : Window
    {
        private readonly IExcelService _excelService;
        private readonly IComparisonService _comparisonService;

        public ReconciliationWindow(IExcelService excelService, IComparisonService comparisonService)
        {
            InitializeComponent();
            _excelService = excelService;
            _comparisonService = comparisonService;
        }

        private void BrowseSchematic_Click(object sender, RoutedEventArgs e)
        {
            var fd = new OpenFileDialog { Filter = "Excel Files|*.xlsx" };
            if (fd.ShowDialog() == true) SchematicPathBox.Text = fd.FileName;
        }

        private void BrowseExploded_Click(object sender, RoutedEventArgs e)
        {
            var fd = new OpenFileDialog { Filter = "Excel Files|*.xlsx" };
            if (fd.ShowDialog() == true) ExplodedPathBox.Text = fd.FileName;
        }

        private async void Generate_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(SchematicPathBox.Text) || string.IsNullOrWhiteSpace(ExplodedPathBox.Text))
            {
                MessageBox.Show("Please select both files before generating.", "Missing Files", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var saveFd = new SaveFileDialog { Filter = "Excel Files|*.xlsx", DefaultExt = "xlsx", FileName = "Subsystem Cable BOM Reconciliation" };
            if (saveFd.ShowDialog() != true) return;

            GenerateButton.IsEnabled = false;
            StatusText.Text = "Processing... Please wait.";

            // 1. Extract UI strings on the main thread BEFORE starting the background task
            string schematicPath = SchematicPathBox.Text;
            string explodedPath = ExplodedPathBox.Text;
            string savePath = saveFd.FileName;

            try
            {
                await Task.Run(() =>
                {
                    // 2. Use the extracted string variables, not the UI controls
                    var schematic = _excelService.ReadSchematic(schematicPath);
                    var explodedResult = _excelService.ReadBom(explodedPath);

                    // 3. Execute Math and Categorization
                    var reportData = _comparisonService.ReconcileBoms(schematic, explodedResult.BomItems);

                    // 4. Export to Excel
                    _excelService.ExportReconciliationReport(savePath, reportData);
                });

                MessageBox.Show("Reconciliation Report generated successfully!", "Success", MessageBoxButton.OK, MessageBoxImage.Information);

                // NEW: Automatically open the generated Excel file using the default system application
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = savePath,
                    UseShellExecute = true
                });

                this.Close(); // Closes the dialog automatically on success
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Error Generating Report", MessageBoxButton.OK, MessageBoxImage.Error);
                StatusText.Text = "Failed.";
            }
            finally
            {
                GenerateButton.IsEnabled = true;
            }
        }
    }
}