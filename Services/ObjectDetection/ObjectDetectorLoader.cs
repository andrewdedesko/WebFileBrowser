using System.Text.Json;
using WebFileBrowser.Configuration;
using WebFileBrowser.Models;

namespace WebFileBrowser.Services.ObjectDetection;

public class ObjectDetectorLoader {
    public IObjectDetector LoadObjectDetector(string configFilePath, ILogger<OnnxObjectDetector> logger) {
        var config = JsonSerializer.Deserialize<Yolov26ObjectDetectionModelConfig>(File.ReadAllText(configFilePath));

        if(config == null) {
            throw new Exception($"Unable to parse object detector config: {configFilePath}");
        }
        
        if(config.ModelType != "yolo26") {
            throw new Exception($"Unsupported ONNX model type: {config.ModelType}");
        }

        var modelPath = Path.Combine(Directory.GetParent(configFilePath)?.FullName ?? "/", config.ModelPath);
        var labelClasses = config.LabelClasses
            .Select(c => new LabelClass(c.Name, DetectedObjectClass.Unknown))
            .ToArray();

        var objectDetector = new OnnxObjectDetector(
            modelPath: modelPath,
            labelClasses,
            logger
        );

        return objectDetector;
    }
}