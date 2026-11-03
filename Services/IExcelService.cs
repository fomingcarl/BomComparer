// Location: Services/IExcelService.cs
using System.Collections.Generic;
using BomComparer.Models;

namespace BomComparer.Services
{
    public class BomImportResult
    {
        public List<BomItem> BomItems { get; set; } = new();
        public List<string> MissingColumns { get; set; } = new();
    }

    public interface IExcelService
    {
        BomImportResult ReadBom(string filePath);
        void ExportComparison(string savePath, List<BomDifference> differences);

        // ExportDataTable stays here because it handles Excel file generation
        void ExportDataTable(string savePath, System.Data.DataTable dataTable, string sheetName);

        List<SchematicItem> ReadSchematic(string filePath);
        void ExportReconciliationReport(string savePath, ReconciliationReportData reportData);



    }









}