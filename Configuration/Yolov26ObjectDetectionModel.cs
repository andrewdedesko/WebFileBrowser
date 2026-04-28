using System.Text.Json.Serialization;

namespace  WebFileBrowser.Configuration;

public class Yolov26ObjectDetectionModelConfig {
    [JsonPropertyName("modelType")]
    public required string ModelType {get; set;}

    [JsonPropertyName("model")]
    public required string ModelPath { get; set; }

    [JsonPropertyName("labelClasses")]
    public required IEnumerable<Yolov26ObjectDetectionLabelClassConfig> LabelClasses { get; set; }
}

public class Yolov26ObjectDetectionLabelClassConfig {
    [JsonPropertyName("name")]
    public required string Name { get; set; }

    [JsonPropertyName("class")]
    public required string ClassName { get; set; }

    [JsonPropertyName("annotationColor")]
    public string? AnnotationColor { get; set; }
}