using System.Diagnostics.CodeAnalysis;

namespace ShakaPlayerThumbnail.Data;

public class Video
{
    public string Name { get; set; } = ""; 
    public string FileName { get; set; } = ""; 
    public DateTime UploadDate { get; set; } = DateTime.Now; 
}
