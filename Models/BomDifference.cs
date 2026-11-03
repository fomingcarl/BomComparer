// Location: Models/BomDifference.cs
namespace BomComparer.Models
{
    public class BomDifference
    {
        public string PartNo { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public double? Cost { get; set; } 
        public string IssueType { get; set; } = string.Empty;
        public string Details { get; set; } = string.Empty;
    }
}