using System.Diagnostics.CodeAnalysis;

namespace ShakaPlayerThumbnail.Data;

public class Video
{
    public string Name { get; set; }
    public string FileName { get; set; }
    public DateTime UploadDate { get; set; }
    public bool IsProcessing { get; set; }
    public int Progress { get; set; }
    public double TaskDuration { get; set; } 
}

