using FaceAiSharp;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using WebFileBrowser.Models;

namespace WebFileBrowser.Services.ObjectDetection;

public class SharpAiFaceDetector : IObjectDetector {
    public IEnumerable<Prediction> FindObjects(Image<Rgb24> sourceImage) {
        var det = FaceAiSharpBundleFactory.CreateFaceDetectorWithLandmarks();
        var eyeDet = FaceAiSharpBundleFactory.CreateEyeStateDetector();

        var faces = det.DetectFaces(sourceImage);
        return faces.Select(f => new Prediction() {
            Label = "face",
            ObjectClass = DetectedObjectClass.Face,
            Confidence = f.Confidence ?? 0,
            Box = new Box(f.Box.Left, f.Box.Top, f.Box.Right, f.Box.Bottom)
        });
    }

    public string GetModelIdentifier() => "SharpAiFaceDetector";
}