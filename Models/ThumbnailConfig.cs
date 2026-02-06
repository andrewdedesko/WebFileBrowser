using System.Text.Json.Serialization;

namespace WebFileBrowser.Models;

public class ThumbnailConfig {
    [JsonPropertyName("thumbnail")]
    public required string Thumbnail {get; set;}
}