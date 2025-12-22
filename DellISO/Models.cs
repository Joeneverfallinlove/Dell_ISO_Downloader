using System.Collections.Generic;
using System.Text.Json.Serialization;
using CommunityToolkit.Mvvm.ComponentModel;

namespace DellISO
{
    public partial class DellFileItem : ObservableObject
    {
        [ObservableProperty] private bool isSelected;
        public string Title { get; set; }
        public string Version { get; set; }
        public string ReleaseDate { get; set; }
        public string OriginalSize { get; set; }
        public string DownloadUrl { get; set; }
        public string Category { get; set; }

        public string FormattedSize
        {
            get
            {
                if (double.TryParse(OriginalSize, out double sizeBytes))
                {
                    if (sizeBytes >= 1024 * 1024 * 1024)
                        return $"{(sizeBytes / (1024 * 1024 * 1024)):F2} GB";
                    else
                        return $"{(sizeBytes / (1024 * 1024)):F2} MB";
                }
                return OriginalSize;
            }
        }
    }

    public class OAuthResponse
    {
        [JsonPropertyName("access_token")]
        public string AccessToken { get; set; }
    }

    public class DellIsoResponse
    {
        [JsonPropertyName("images")]
        public List<DellIsoItem> Images { get; set; }
    }

    public class DellIsoItem
    {
        [JsonPropertyName("title")]
        public string Title { get; set; }
        [JsonPropertyName("url")]
        public string Url { get; set; }
        [JsonPropertyName("size")]
        public string Size { get; set; }
        [JsonPropertyName("releaseDate")]
        public string ReleaseDate { get; set; }
        [JsonPropertyName("dellVersion")]
        public string DellVersion { get; set; }
    }

    public class DellComplexResponse
    {
        [JsonPropertyName("swbs")]
        public List<DellComplexItem> Swbs { get; set; } 

        [JsonPropertyName("images")]
        public List<DellComplexItem> Images { get; set; } 
    }

    public class DellComplexItem
    {
        [JsonPropertyName("title")]
        public string Title { get; set; }
        [JsonPropertyName("vendorVersion")]
        public string VendorVersion { get; set; }
        [JsonPropertyName("releaseDate")]
        public string ReleaseDate { get; set; }
        [JsonPropertyName("files")]
        public List<DellFileObj> Files { get; set; }
    }

    public class DellFileObj
    {
        [JsonPropertyName("url")]
        public string Url { get; set; }
        [JsonPropertyName("size")]
        public string Size { get; set; }
    }
}