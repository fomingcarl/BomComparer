// Location: Models/FileQueueItem.cs
using CommunityToolkit.Mvvm.ComponentModel;

namespace BomComparer.Models
{
    // Inherits ObservableObject so the UI updates instantly if a nickname is added
    public partial class FileQueueItem : ObservableObject
    {
        [ObservableProperty]
        private string _filePath = string.Empty;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(DisplayName))]
        private string _originalName = string.Empty;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(DisplayName))]
        private string _nickname = string.Empty;

        // If a nickname exists, use it. Otherwise, fall back to the original filename.
        public string DisplayName => string.IsNullOrWhiteSpace(Nickname) ? OriginalName : Nickname;
    }
}