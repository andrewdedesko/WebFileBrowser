using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using WebFileBrowser.Models;

namespace WebFileBrowser.Services.ObjectDetection;

public class OnnxObjectDetector : IObjectDetector {
    // private readonly string _modelPath = "Ai/collared_yolo26s.onnx";

    // private readonly string[] _labels = new[] {"head_neck_face", "head_neck_face_collared", "collar", "ball_gag", "harness_gag"};
    // private readonly string[] _labels = new[] { "person" };

    private readonly string _modelPath;
    private readonly IDictionary<int, LabelClass> _labels;

    private readonly ILogger<OnnxObjectDetector> _logger;

    public OnnxObjectDetector(string modelPath, LabelClass[] labels, ILogger<OnnxObjectDetector> logger) : this(modelPath, _labelArrayToDictionary(labels), logger) {
    }

    public OnnxObjectDetector(string modelPath, IDictionary<int, LabelClass> labels, ILogger<OnnxObjectDetector> logger) {
        _modelPath = modelPath;
        _labels = labels;
        _logger = logger;
    }

    public IEnumerable<Prediction> FindObjects(Image<Rgb24> sourceImage) {
        var image = sourceImage.Clone();
        image.Mutate(i => i.Resize(640, 640));

        var paddedHeight = (int)(Math.Ceiling(image.Height / 32f) * 32f);
        var paddedWidth = (int)(Math.Ceiling(image.Width / 32f) * 32f);
        // var mean = new[] { 102.9801f, 115.9465f, 122.7717f };
        var mean = new[] { 0, 0, 0 };

        // Preprocessing image
        // We use DenseTensor for multi-dimensional access
        DenseTensor<float> input = new(new[] { 1, 3, 640, 640 });
        image.ProcessPixelRows(accessor => {
            for(int y = 0; y < accessor.Height; y++) {
                Span<Rgb24> pixelSpan = accessor.GetRowSpan(y);
                for(int x = 0; x < accessor.Width; x++) {
                    input[0, 0, y, x] = pixelSpan[x].R / 255f;
                    input[0, 1, y, x] = pixelSpan[x].G / 255f;
                    input[0, 2, y, x] = pixelSpan[x].B / 255f;
                }
            }
        });

        using var inputOrtValue = OrtValue.CreateTensorValueFromMemory(OrtMemoryInfo.DefaultInstance,
            input.Buffer, new long[] { 1, 3, 640, 640 });


        var inputs = new Dictionary<string, OrtValue>
        {
            { "images", inputOrtValue }
        };

        using var session = new InferenceSession(_modelPath);
        // _logger.LogInformation($"Model input names: {string.Join(", ", session.InputNames)}");
        using var runOptions = new RunOptions();
        using IDisposableReadOnlyCollection<OrtValue> results = session.Run(runOptions, inputs, session.OutputNames);

        // Console.WriteLine($"Number of outputs: {results.Count}");
        for(int i = 0; i < results.Count; i++) {
            var shape = results[i].GetTensorTypeAndShape();
            // Console.WriteLine($"Output[{i}]: name={session.OutputNames[i]}, shape=[{string.Join(", ", shape.Shape)}], type={shape.ElementDataType}");
        }

        var outputSpan = results[0].GetTensorDataAsSpan<float>();

        const float minConfidence = 0.7f;
        var predictions = new List<Prediction>();

        // Each detection is 6 floats: x1, y1, x2, y2, confidence, class_id
        float scaleX = sourceImage.Width / 640f;
        float scaleY = sourceImage.Height / 640f;

        const int stride = 6;
        for(int i = 0; i < 300; i++) {
            int offset = i * stride;
            float confidence = outputSpan[offset + 4];

            if(confidence >= minConfidence) {
                int labelIndex = (int)outputSpan[offset + 5];
                LabelClass label;
                if(_labels.ContainsKey(labelIndex)){
                    label = _labels[labelIndex];
                } else {
                    label = new LabelClass("_unknown_label", DetectedObjectClass.Unknown);
                }
                predictions.Add(new Prediction {
                    Box = new Box(
                        outputSpan[offset + 0] * scaleX,  // x1
                        outputSpan[offset + 1] * scaleY,  // y1
                        outputSpan[offset + 2] * scaleX,  // x2
                        outputSpan[offset + 3] * scaleY   // y2
                    ),
                    Label = label.Name,
                    ObjectClass = label.DetectedObjectClass,
                    Confidence = confidence
                });
            }
        }

        return predictions;
    }

    private static IDictionary<int, LabelClass> _labelArrayToDictionary(LabelClass[] labels) {
        Dictionary<int, LabelClass> labelDictionary = new();
        for(var i = 0; i < labels.Length; i++) {
            labelDictionary.Add(i, labels[i]);
        }

        return labelDictionary;
    }
}