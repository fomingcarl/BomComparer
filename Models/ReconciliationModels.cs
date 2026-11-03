// Location: Models/ReconciliationModels.cs
using System.Collections.Generic;

namespace BomComparer.Models
{
    public class SchematicItem
    {
        public string Subsystem { get; set; } = string.Empty;
        public string PartNumber { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public double Qty { get; set; }
    }

    public class ReconciliationItem
    {
        public string Subsystem { get; set; } = string.Empty;
        public string PartNumber { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public double SchematicQty { get; set; }
        public double AllocatedQty { get; set; }
        public double RemainingQty { get; set; }
        public string Status { get; set; } = string.Empty;
    }

    public class NewKitAdd
    {
        public string PartNumber { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public double AdditionalQty { get; set; }
        public double? UnitCost { get; set; }
        public double? ExtendedCost { get; set; }
        public string Subsystems { get; set; } = string.Empty;
    }

    public class ReconciliationReportData
    {
        public List<ReconciliationItem> MainReport { get; set; } = new();
        public List<NewKitAdd> NewKitAdds { get; set; } = new();
    }
}