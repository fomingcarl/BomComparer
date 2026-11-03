// Location: Services/ComparisonService.cs
using System.Collections.Generic;
using System.Linq;
using BomComparer.Models;

namespace BomComparer.Services
{
    public class ComparisonService : IComparisonService
    {
        public List<BomDifference> CompareBoms(Dictionary<string, List<BomItem>> loadedBoms)
        {
            var differences = new List<BomDifference>();

            // Requires at least 2 files to perform a comparison
            if (loadedBoms.Count < 2) return differences;

            var fileNames = loadedBoms.Keys.ToList();

            // 1. Gather every unique Part Number across all queued files
            var allPartNumbers = loadedBoms.Values.SelectMany(b => b.Select(i => i.PartNo)).Distinct().ToList();

            foreach (var partNo in allPartNumbers)
            {
                // Extract the specific BOM Items for this part from every file
                var itemsPerFile = new Dictionary<string, List<BomItem>>();
                foreach (var file in fileNames)
                {
                    itemsPerFile[file] = loadedBoms[file].Where(i => i.PartNo == partNo).ToList();
                }

                // Count how many files actually contain this part
                int filesContainingPart = itemsPerFile.Count(kvp => kvp.Value.Any());

                // Grab description and cost from the first file that has the part to populate the DataGrid
                var firstPresentItem = itemsPerFile.First(kvp => kvp.Value.Any()).Value.First();
                string desc = firstPresentItem.Description;
                double? cost = firstPresentItem.Cost;

                // PRESENCE MISMATCH: Replaces "Added/Removed" for N-Way comparisons
                if (filesContainingPart < fileNames.Count)
                {
                    var presentIn = itemsPerFile.Where(kvp => kvp.Value.Any()).Select(k => k.Key);
                    var missingFrom = itemsPerFile.Where(kvp => !kvp.Value.Any()).Select(k => k.Key);

                    differences.Add(new BomDifference
                    {
                        PartNo = partNo,
                        Description = desc,
                        Cost = cost,
                        IssueType = "Presence Mismatch",
                        Details = $"Present: [{string.Join(", ", presentIn)}]  |  Missing: [{string.Join(", ", missingFrom)}]"
                    });

                    // If a part is missing from a file entirely, comparing its quantities/costs will throw false positives. 
                    continue;
                }

                // QTY MISMATCH: Checks if any file's total Extended Qty differs from the others
                if (itemsPerFile.Values.Any(list => list.Any(i => i.ExtendedQty.HasValue)))
                {
                    var qtys = itemsPerFile.ToDictionary(k => k.Key, v => v.Value.Sum(i => i.ExtendedQty ?? 0));

                    // .Distinct() checks if there is more than 1 unique value across all files
                    if (qtys.Values.Distinct().Count() > 1)
                    {
                        string details = string.Join("  |  ", qtys.Select(q => $"[{q.Key}]: {q.Value}"));
                        differences.Add(new BomDifference { PartNo = partNo, Description = desc, Cost = cost, IssueType = "Qty Mismatch", Details = details });
                    }
                }

                // REV CHANGE
                var revs = itemsPerFile.ToDictionary(k => k.Key, v => v.Value.First().Revision);
                if (revs.Values.Distinct().Count() > 1)
                {
                    string details = string.Join("  |  ", revs.Select(r => $"[{r.Key}]: {(string.IsNullOrEmpty(r.Value) ? "-" : r.Value)}"));
                    differences.Add(new BomDifference { PartNo = partNo, Description = desc, Cost = cost, IssueType = "Rev Change", Details = details });
                }

                // UOM MISMATCH
                var uoms = itemsPerFile.ToDictionary(k => k.Key, v => v.Value.First().UoM);
                if (uoms.Values.Distinct().Count() > 1)
                {
                    string details = string.Join("  |  ", uoms.Select(u => $"[{u.Key}]: {(string.IsNullOrEmpty(u.Value) ? "-" : u.Value)}"));
                    differences.Add(new BomDifference { PartNo = partNo, Description = desc, Cost = cost, IssueType = "UoM Mismatch", Details = details });
                }

                // COST CHANGE
                if (itemsPerFile.Values.All(list => list.First().Cost.HasValue))
                {
                    var costs = itemsPerFile.ToDictionary(k => k.Key, v => v.Value.First().Cost);
                    if (costs.Values.Distinct().Count() > 1)
                    {
                        string details = string.Join("  |  ", costs.Select(c => $"[{c.Key}]: {c.Value}"));
                        differences.Add(new BomDifference { PartNo = partNo, Description = desc, Cost = cost, IssueType = "Cost Change", Details = details });
                    }
                }

                // FREQUENCY MISMATCH
                var freqs = itemsPerFile.ToDictionary(k => k.Key, v => v.Value.Count);
                if (freqs.Values.Distinct().Count() > 1)
                {
                    string details = string.Join("  |  ", freqs.Select(f => $"[{f.Key}]: {f.Value}x"));
                    differences.Add(new BomDifference { PartNo = partNo, Description = desc, Cost = cost, IssueType = "Frequency Mismatch", Details = details });
                }
            }
            return differences;
        }


        public System.Data.DataTable GenerateTopLevelReport(Dictionary<string, List<BomItem>> loadedBoms)
        {
            var dt = new System.Data.DataTable();
            if (loadedBoms.Count == 0) return dt;

            var fileNames = loadedBoms.Keys.ToList();

            // 1. Setup Static Columns
            dt.Columns.Add("Category", typeof(string));
            dt.Columns.Add("Location", typeof(string));
            dt.Columns.Add("Location by SN", typeof(string));
            dt.Columns.Add("Part Number", typeof(string));
            dt.Columns.Add("Description", typeof(string));
            dt.Columns.Add("UOM", typeof(string));
            dt.Columns.Add("Present In", typeof(string));
            dt.Columns.Add("Missing From", typeof(string));

            // 2. Setup Dynamic Columns
            foreach (var file in fileNames)
            {
                dt.Columns.Add($"{file} Qty", typeof(double));
            }
            //dt.Columns.Add("Notes", typeof(string));




            // 3. Filter all BOMs to gather unique Level 2 items (ID contains exactly 1 dot)
            var level2Parts = loadedBoms.Values
                .SelectMany(b => b.Where(i => i.Id.Count(c => c == '.') == 1))
                .Select(i => i.PartNo)
                .Distinct()
                .ToList();

            foreach (var partNo in level2Parts)
            {
                var row = dt.NewRow();
                var presentIn = new List<string>();
                var missingFrom = new List<string>();
                var locationsByFile = new Dictionary<string, string>();
                var quantities = new List<double>();

                string desc = "", uom = "";

                foreach (var file in fileNames)
                {
                    var bom = loadedBoms[file];
                    // Find the specific Level 2 item in this file
                    var item = bom.FirstOrDefault(i => i.PartNo == partNo && i.Id.Count(c => c == '.') == 1);

                    if (item != null)
                    {
                        presentIn.Add(file);
                        desc = item.Description;
                        uom = item.UoM;
                        row[$"{file} Qty"] = item.ExtendedQty ?? 0;
                        quantities.Add(item.ExtendedQty ?? 0);

                        // Find Parent Location
                        string parentId = item.Id.Split('.')[0];
                        var parent = bom.FirstOrDefault(b => b.Id == parentId);
                        string pDesc = parent?.Description?.ToUpper() ?? "";

                        if (pDesc.Contains("ASSY, CM CONFIG")) locationsByFile[file] = "CM";
                        else if (pDesc.Contains("ASSY, NEXX CONFIG")) locationsByFile[file] = "NEXX";
                        else if (pDesc.Contains("KIT, FIELD UPGRADE")) locationsByFile[file] = "FUPG";
                        else locationsByFile[file] = "UNASSIGNED";
                    }
                    else
                    {
                        missingFrom.Add(file);
                        row[$"{file} Qty"] = System.DBNull.Value;
                    }
                }

                row["Part Number"] = partNo;
                row["Description"] = desc;
                row["UOM"] = uom;
                row["Present In"] = string.Join(", ", presentIn);
                row["Missing From"] = string.Join(", ", missingFrom);

                // Location Logic
                var uniqueLocations = locationsByFile.Values.Distinct().ToList();
                if (uniqueLocations.Count == 1)
                {
                    row["Location"] = uniqueLocations.First();
                }
                else
                {
                    row["Location"] = "VARIES";
                    row["Location by SN"] = string.Join("; ", locationsByFile.Select(kvp => $"{kvp.Key}: {kvp.Value}"));
                }

                // Category Logic
                bool qtyConsistent = quantities.Distinct().Count() == 1;

                if (presentIn.Count == fileNames.Count)
                    row["Category"] = qtyConsistent ? "Common to All - Qty Consistent" : "Common to All - Qty Differs";
                else if (presentIn.Count > 1)
                    row["Category"] = qtyConsistent ? "Shared by Some - Qty Consistent" : "Shared by Some - Qty Differs";
                else
                    row["Category"] = $"Unique to {presentIn.First()}";

                dt.Rows.Add(row);
            }

            return dt;
        }



        public ReconciliationReportData ReconcileBoms(List<SchematicItem> schematic, List<BomItem> explodedBom)
        {
            var data = new ReconciliationReportData();

            // 1. Aggregate Exploded BOM to get the total sum and capture the first valid Cost and Description
            var explodedAgg = explodedBom
                .Where(b => !string.IsNullOrWhiteSpace(b.PartNo))
                .GroupBy(b => b.PartNo)
                .ToDictionary(
                    g => g.Key,
                    g => new
                    {
                        TotalQty = g.Sum(x => x.ExtendedQty ?? 0),
                        Cost = g.FirstOrDefault(x => x.Cost.HasValue)?.Cost,
                        Description = g.FirstOrDefault(x => !string.IsNullOrWhiteSpace(x.Description))?.Description
                    }
                );

            // NEW: Create a running inventory tracker to prevent double-counting across subsystems
            var availableInventory = explodedAgg.ToDictionary(k => k.Key, v => v.Value.TotalQty);

            // 2. Build Main Reconciliation Sheet
            var missingItems = new List<ReconciliationItem>();

            foreach (var sch in schematic)
            {
                bool foundInBom = explodedAgg.TryGetValue(sch.PartNumber, out var agg);

                double alloc = 0;

                // NEW: Only allocate if we have a valid part number and remaining inventory
                if (!string.IsNullOrWhiteSpace(sch.PartNumber) && availableInventory.TryGetValue(sch.PartNumber, out double available) && available > 0)
                {
                    if (available >= sch.Qty)
                    {
                        alloc = sch.Qty;
                        availableInventory[sch.PartNumber] -= sch.Qty; // Deduct from running total
                    }
                    else
                    {
                        alloc = available; // Take whatever is left
                        availableInventory[sch.PartNumber] = 0; // Inventory exhausted
                    }
                }

                double remaining = sch.Qty - alloc;

                string status = "";
                if (string.IsNullOrWhiteSpace(sch.PartNumber)) status = "Manual Review";
                else if (alloc == 0) status = "Cable BOM Only";
                else if (alloc < sch.Qty) status = "Partially Accounted For";
                else status = "Fully Accounted For";

                // Contingency: If the Schematic Description is empty, try to pull it from the Exploded BOM
                string finalDesc = sch.Description;
                if (string.IsNullOrWhiteSpace(finalDesc) && foundInBom)
                    finalDesc = agg.Description ?? "";

                var rItem = new ReconciliationItem
                {
                    Subsystem = sch.Subsystem,
                    PartNumber = sch.PartNumber,
                    Description = finalDesc,
                    SchematicQty = sch.Qty,
                    AllocatedQty = alloc,
                    RemainingQty = remaining,
                    Status = status
                };
                data.MainReport.Add(rItem);

                // Filter shortages for the New Kit Adds list
                if (remaining > 0 && !string.IsNullOrWhiteSpace(sch.PartNumber))
                {
                    missingItems.Add(rItem);
                }
            }

            // 3. Build New Kit Adds Sheet (Grouped by Part)
            var groupedMissing = missingItems.GroupBy(m => m.PartNumber);
            foreach (var g in groupedMissing)
            {
                double totalAdditional = g.Sum(x => x.RemainingQty);
                var part = g.Key;

                bool foundInBom = explodedAgg.TryGetValue(part, out var agg);

                // Cost evaluates to null if missing from exploded BOM format, preventing crashes
                double? unitCost = foundInBom ? agg.Cost : null;
                double? extCost = unitCost.HasValue ? unitCost.Value * totalAdditional : null;

                string subsystems = string.Join(" | ", g.Select(x => x.Subsystem).Distinct());

                data.NewKitAdds.Add(new NewKitAdd
                {
                    PartNumber = part,
                    Description = g.First().Description,
                    AdditionalQty = totalAdditional,
                    UnitCost = unitCost,
                    ExtendedCost = extCost,
                    Subsystems = subsystems
                });
            }

            return data;
        }






    }
}