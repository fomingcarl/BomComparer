using System.Collections.Generic;
using System.Linq;
using System.Windows;
using BomComparer.Models;

namespace BomComparer
{
    public partial class FrequencyMismatchWindow : Window
    {
        public FrequencyMismatchWindow(string partNo, Dictionary<string, List<BomItem>> loadedBoms)
        {
            InitializeComponent();

            // Updated the title to reflect its new universal purpose
            Title = $"Parent Hierarchy Details - Part: {partNo}";

            var combinedHierarchy = new List<ParentInfo>();

            foreach (var kvp in loadedBoms)
            {
                var fileHierarchy = GetParentHierarchy(partNo, kvp.Value, kvp.Key);
                combinedHierarchy.AddRange(fileHierarchy);
            }

            int totalFiles = loadedBoms.Count;

            foreach (var item in combinedHierarchy)
            {
                int fullMatchCount = combinedHierarchy
                    .Where(x => x.ParentPartNo == item.ParentPartNo && x.ParentDescription == item.ParentDescription && x.ParentRevision == item.ParentRevision)
                    .Select(x => x.SourceFile)
                    .Distinct()
                    .Count();

                int partialMatchCount = combinedHierarchy
                    .Where(x => x.ParentPartNo == item.ParentPartNo && x.ParentDescription == item.ParentDescription)
                    .Select(x => x.SourceFile)
                    .Distinct()
                    .Count();

                if (fullMatchCount == totalFiles)
                    item.RowColor = "#C8E6C9";
                else if (partialMatchCount == totalFiles)
                    item.RowColor = "#FFF9C4";
                else
                    item.RowColor = "Transparent";
            }

            UnifiedDataGrid.ItemsSource = combinedHierarchy.OrderBy(h => h.ParentPartNo).ThenBy(h => h.SourceFile).ToList();
        }

        private List<ParentInfo> GetParentHierarchy(string partNo, List<BomItem> bom, string sourceFile)
        {
            var results = new List<ParentInfo>();
            var occurrences = bom.Where(b => b.PartNo == partNo).ToList();

            foreach (var item in occurrences)
            {
                string parentId = GetParentId(item.Id);
                var parentItem = bom.FirstOrDefault(b => b.Id == parentId);

                // Calculate the parent's level by counting the segments in its ID. If it has no parent, return "-"
                string parentLevel = string.IsNullOrEmpty(parentId) ? "-" : parentId.Split('.').Length.ToString();

                results.Add(new ParentInfo
                {
                    SourceFile = sourceFile,
                    InstanceId = item.Id,
                    ParentLevel = parentLevel, // Bind the new calculation
                    ParentPartNo = parentItem?.PartNo ?? "TOP LEVEL",
                    ParentDescription = parentItem?.Description ?? "Assembly Root",
                    ParentRevision = parentItem?.Revision ?? "-"
                });
            }
            return results;
        }

        private string GetParentId(string id)
        {
            if (string.IsNullOrEmpty(id)) return "";
            int lastDot = id.LastIndexOf('.');
            if (lastDot > 0) return id.Substring(0, lastDot);
            return "";
        }
    }

    public class ParentInfo
    {
        public string SourceFile { get; set; }
        public string InstanceId { get; set; }
        public string ParentLevel { get; set; } // Added property
        public string ParentPartNo { get; set; }
        public string ParentDescription { get; set; }
        public string ParentRevision { get; set; }
        public string RowColor { get; set; } = "Transparent";
    }
}