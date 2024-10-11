using System.IO.Compression;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;

namespace ShakaPlayerThumbnail.Tools;

public static class FileTools
{


    public static string GetFileNameWithoutExtension(string fileName)
    {
        var fileExtPos = fileName.LastIndexOf('.');
        return fileExtPos >= 0 ? fileName[..fileExtPos] : string.Empty;
    }
    public static void CompressFile(string inputFilePath, string outputFilePath)
    {
        try
        {
            using var inputFileStream = new FileStream(inputFilePath, FileMode.Open, FileAccess.Read, FileShare.Read, 1024 * 1024);
            using var outputFileStream = new FileStream(outputFilePath, FileMode.Create, FileAccess.Write, FileShare.None, 1024 * 1024);
            using var compressionStream = new GZipStream(outputFileStream, CompressionMode.Compress, true);

            byte[] buffer = new byte[3*1024 * 1024]; 
            int bytesRead;

            while ((bytesRead = inputFileStream.Read(buffer, 0, buffer.Length)) > 0)
            {
                compressionStream.Write(buffer, 0, bytesRead);
            }

            compressionStream.Flush();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error during compression: {ex.Message}");
        }
    }

    public static void DeleteFile(string filePath)
    {
        try
        {
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
                Console.WriteLine($"File deleted successfully: {filePath}");
            }
            else 
            {
                Console.WriteLine($"File not found: {filePath}");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error deleting file: {ex.Message}");
        }
    }
    public static string GetUniqueVideoName(string videoPath)
    {
        var fileName = Path.GetFileNameWithoutExtension(videoPath);
        var inputBytes = Encoding.UTF8.GetBytes(fileName);
        var hashBytes = SHA256.HashData(inputBytes);
        var sb = new StringBuilder();
        for (var i = 0; i < 8; i++)
        {
            sb.Append(hashBytes[i].ToString("X2"));
        }

        return sb.ToString();
    }
    
       
}