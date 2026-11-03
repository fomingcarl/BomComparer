// Location: Services/IComparisonService.cs
using System.Collections.Generic;
using BomComparer.Models;

namespace BomComparer.Services
{
    public interface IComparisonService
    {
        List<BomDifference> CompareBoms(Dictionary<string, List<BomItem>> loadedBoms);

        // Moved here so the ViewModel can successfully call it via _comparisonService
        System.Data.DataTable GenerateTopLevelReport(Dictionary<string, List<BomItem>> loadedBoms);

        ReconciliationReportData ReconcileBoms(List<SchematicItem> schematic, List<BomItem> explodedBom);


    }






}