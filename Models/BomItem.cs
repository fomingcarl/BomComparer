// Location: Models/BomItem.cs
namespace BomComparer.Models
{
    public class BomItem
    {
        public string Id { get; set; } = string.Empty;
        public string PartNo { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Revision { get; set; } = string.Empty;

        // Nullable types (?) allow the data to be genuinely "empty" if missing
        public double? Quantity { get; set; }
        public string UoM { get; set; } = string.Empty;
        public double? Cost { get; set; }
        public double? ExtendedQty { get; set; }

        public int OccurrenceCount { get; set; }
    }
}