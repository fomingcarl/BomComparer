// Location: Services/ExcelService.cs
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ClosedXML.Excel;
using BomComparer.Models;

namespace BomComparer.Services
{
    public class ExcelService : IExcelService
    {
        public BomImportResult ReadBom(string filePath)
        {
            var result = new BomImportResult();

            try
            {
                using (var workbook = new XLWorkbook(filePath))
                {
                    var worksheet = workbook.Worksheet(1);
                    var headerMap = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
                    var headerRow = worksheet.Row(1);

                    // 1. Map columns dynamically from the first row
                    foreach (var cell in headerRow.CellsUsed())
                    {
                        string headerName = cell.GetString().Trim();
                        if (!string.IsNullOrWhiteSpace(headerName))
                            headerMap[headerName] = cell.Address.ColumnNumber;
                    }

                    // --- ALIAS HELPER METHODS ---
                    // Checks multiple possible header names (aliases) and returns the first matching column index
                    int GetColumnIndex(params string[] possibleNames)
                    {
                        foreach (var name in possibleNames)
                        {
                            if (headerMap.TryGetValue(name, out int colIndex)) return colIndex;
                        }
                        return -1; // Not found
                    }

                    string GetStringSafe(IXLRow r, params string[] colNames)
                    {
                        int colIndex = GetColumnIndex(colNames);
                        return colIndex != -1 ? r.Cell(colIndex).GetString().Trim() : string.Empty;
                    }

                    double? GetDoubleSafe(IXLRow r, params string[] colNames)
                    {
                        int colIndex = GetColumnIndex(colNames);
                        if (colIndex != -1 && r.Cell(colIndex).TryGetValue<double>(out var val)) return val;
                        return null;
                    }

                    int? GetIntSafe(IXLRow r, params string[] colNames)
                    {
                        int colIndex = GetColumnIndex(colNames);
                        if (colIndex != -1 && r.Cell(colIndex).TryGetValue<int>(out var val)) return val;
                        return null;
                    }

                    // 2. Detect missing columns using the primary name and its aliases
                    void CheckRequired(string displayWarningName, params string[] aliases)
                    {
                        if (GetColumnIndex(aliases) == -1)
                            result.MissingColumns.Add(displayWarningName);
                    }

                    // Checks for either the Engineering Toolbox format or BOM Reader format
                    CheckRequired("ID or Level", "ID", "Level", "level");
                    CheckRequired("Part No", "Part No", "NPart");
                    CheckRequired("Rev", "Rev");
                    CheckRequired("Qty", "Qty", "BOM_Qty");
                    CheckRequired("UoM", "UoM", "Meas");
                    CheckRequired("Description", "Description", "Desc");
                    CheckRequired("Cost", "Cost"); // BOM Reader lacks this, so it will correctly warn and skip
                    CheckRequired("Extended Qty", "Extended Qty", "Total_Qty");

                    // 3. Read data and generate missing IDs
                    var rows = worksheet.RowsUsed().Skip(1);
                    var levelCounters = new Dictionary<int, int>(); // Tracks the numbering for the dynamic ID generator

                    foreach (var row in rows)
                    {
                        var item = new BomItem
                        {
                            PartNo = GetStringSafe(row, "Part No", "NPart"),
                            Revision = GetStringSafe(row, "Rev"),
                            Quantity = GetDoubleSafe(row, "Qty", "BOM_Qty"),
                            UoM = GetStringSafe(row, "UoM", "Meas"),
                            Description = GetStringSafe(row, "Description", "Desc"),
                            Cost = GetDoubleSafe(row, "Cost"),
                            ExtendedQty = GetDoubleSafe(row, "Extended Qty", "Total_Qty")
                        };

                        // ID GENERATION CONTINGENCY
                        string rawId = GetStringSafe(row, "ID");
                        if (!string.IsNullOrWhiteSpace(rawId))
                        {
                            item.Id = rawId; // Engineering Toolbox format: Use the provided ID
                        }
                        else
                        {
                            // BOM Reader format: Generate the ID using the Level column
                            int currentLevel = GetIntSafe(row, "Level", "level") ?? 1;

                            // Increment the counter for the current level
                            if (levelCounters.ContainsKey(currentLevel))
                                levelCounters[currentLevel]++;
                            else
                                levelCounters[currentLevel] = 1;

                            // Reset counters for any deeper levels so they start back at 1
                            var keysToRemove = levelCounters.Keys.Where(k => k > currentLevel).ToList();
                            foreach (var k in keysToRemove) levelCounters.Remove(k);

                            // Construct the ID string (e.g., 001.002.001)
                            var idParts = new List<string>();
                            for (int i = 1; i <= currentLevel; i++)
                            {
                                if (levelCounters.TryGetValue(i, out int count))
                                    idParts.Add(count.ToString("D3")); // Pads with zeroes
                                else
                                    idParts.Add("001");
                            }
                            item.Id = string.Join(".", idParts);
                        }

                        if (!string.IsNullOrWhiteSpace(item.PartNo))
                            result.BomItems.Add(item);
                    }
                }
            }
            catch (IOException ex)
            {
                throw new Exception($"The file '{Path.GetFileName(filePath)}' is currently open in another program (like Excel). Please close it and try again.", ex);
            }

            return result;
        }

        public void ExportComparison(string savePath, List<BomDifference> differences)
        {
            using (var workbook = new XLWorkbook())
            {
                var ws = workbook.Worksheets.Add("Comparison Report");

                // Set headers
                ws.Cell(1, 1).Value = "Part No";
                ws.Cell(1, 2).Value = "Description";
                ws.Cell(1, 3).Value = "Cost";
                ws.Cell(1, 4).Value = "Issue Type";
                ws.Cell(1, 5).Value = "Details";

                for (int i = 0; i < differences.Count; i++)
                {
                    ws.Cell(i + 2, 1).Value = differences[i].PartNo;
                    ws.Cell(i + 2, 2).Value = differences[i].Description;
                    ws.Cell(i + 2, 3).Value = differences[i].Cost;
                    ws.Cell(i + 2, 4).Value = differences[i].IssueType;
                    ws.Cell(i + 2, 5).Value = differences[i].Details;
                }

                ws.Columns().AdjustToContents();
                workbook.SaveAs(savePath);
            }
        }


        public void ExportDataTable(string savePath, System.Data.DataTable dataTable, string sheetName)
        {
            using (var workbook = new XLWorkbook())
            {
                var ws = workbook.Worksheets.Add(sheetName);
                ws.Cell(1, 1).InsertTable(dataTable);
                ws.Columns().AdjustToContents();
                workbook.SaveAs(savePath);
            }
        }


        public List<SchematicItem> ReadSchematic(string filePath)
        {
            var list = new List<SchematicItem>();
            using (var workbook = new XLWorkbook(filePath))
            {
                var ws = workbook.Worksheet(1);
                int headerRowIdx = -1;

                // Dynamically find where the actual data starts by scanning for "Subsystem"
                for (int i = 1; i <= 20; i++)
                {
                    if (ws.Cell(i, 1).GetString().Trim().Equals("Subsystem", System.StringComparison.OrdinalIgnoreCase))
                    {
                        headerRowIdx = i;
                        break;
                    }
                }

                if (headerRowIdx == -1) throw new System.Exception("Could not find 'Subsystem' header in schematic file.");

                var headerMap = new Dictionary<string, int>(System.StringComparer.OrdinalIgnoreCase);
                foreach (var cell in ws.Row(headerRowIdx).CellsUsed())
                {
                    string headerName = cell.GetString().Trim();
                    if (!string.IsNullOrWhiteSpace(headerName))
                        headerMap[headerName] = cell.Address.ColumnNumber;
                }

                int GetCol(params string[] aliases)
                {
                    foreach (var name in aliases)
                        if (headerMap.TryGetValue(name, out int colIndex)) return colIndex;
                    return -1;
                }

                int subCol = GetCol("Subsystem");
                int partCol = GetCol("Cable/Device K#", "Part Number");
                int descCol = GetCol("Description", "Desc");
                int qtyCol = GetCol("QTY", "Quantity");

                // FIX: Only read rows that are strictly below the header row number
                var rows = ws.RowsUsed().Where(r => r.RowNumber() > headerRowIdx);

                foreach (var row in rows)
                {
                    string part = partCol != -1 ? row.Cell(partCol).GetString().Trim() : "";
                    string sub = subCol != -1 ? row.Cell(subCol).GetString().Trim() : "";

                    // Skip completely empty rows
                    if (string.IsNullOrWhiteSpace(part) && string.IsNullOrWhiteSpace(sub)) continue;

                    list.Add(new SchematicItem
                    {
                        Subsystem = sub,
                        PartNumber = part,
                        Description = descCol != -1 ? row.Cell(descCol).GetString().Trim() : "",
                        Qty = qtyCol != -1 && row.Cell(qtyCol).TryGetValue<double>(out var q) ? q : 0
                    });
                }
            }
            return list;
        }

        public void ExportReconciliationReport(string savePath, ReconciliationReportData reportData)
        {
            using (var workbook = new XLWorkbook())
            {
                // SHEET 1: Main Reconciliation
                var ws1 = workbook.Worksheets.Add("Subsystem Cable Reconciliation");
                ws1.Cell(1, 1).Value = "Subsystem";
                ws1.Cell(1, 2).Value = "Part Number";
                ws1.Cell(1, 3).Value = "Description";
                ws1.Cell(1, 4).Value = "Schematic Qty";
                ws1.Cell(1, 5).Value = "Tool BOM Qty Allocated";
                ws1.Cell(1, 6).Value = "Remaining Qty";
                ws1.Cell(1, 7).Value = "Status";

                for (int i = 0; i < reportData.MainReport.Count; i++)
                {
                    var item = reportData.MainReport[i];
                    int r = i + 2;
                    ws1.Cell(r, 1).Value = item.Subsystem;
                    ws1.Cell(r, 2).Value = item.PartNumber;
                    ws1.Cell(r, 3).Value = item.Description;
                    ws1.Cell(r, 4).Value = item.SchematicQty;
                    ws1.Cell(r, 5).Value = item.AllocatedQty;
                    ws1.Cell(r, 6).Value = item.RemainingQty;
                    ws1.Cell(r, 7).Value = item.Status;
                }
                ws1.Columns().AdjustToContents();

                // SHEET 2: New Kit Adds
                var ws2 = workbook.Worksheets.Add("New Kit Adds");
                ws2.Cell(1, 1).Value = "Part Number";
                ws2.Cell(1, 2).Value = "Description";
                ws2.Cell(1, 3).Value = "Total Additional Qty Required";
                ws2.Cell(1, 4).Value = "Unit Standard Cost";
                ws2.Cell(1, 5).Value = "Extended Standard Cost";
                ws2.Cell(1, 6).Value = "Subsystems Requiring Additional Qty";

                for (int i = 0; i < reportData.NewKitAdds.Count; i++)
                {
                    var item = reportData.NewKitAdds[i];
                    int r = i + 2;
                    ws2.Cell(r, 1).Value = item.PartNumber;
                    ws2.Cell(r, 2).Value = item.Description;
                    ws2.Cell(r, 3).Value = item.AdditionalQty;

                    // Safely write the nullables. If cost is missing, these cells remain blank.
                    if (item.UnitCost.HasValue) ws2.Cell(r, 4).Value = item.UnitCost.Value;
                    if (item.ExtendedCost.HasValue) ws2.Cell(r, 5).Value = item.ExtendedCost.Value;

                    ws2.Cell(r, 6).Value = item.Subsystems;
                }
                ws2.Columns().AdjustToContents();

                workbook.SaveAs(savePath);
            }
        }






    }
}