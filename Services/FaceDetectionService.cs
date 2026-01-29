using FaceAiSharp;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using WebFileBrowser.Models;

namespace WebFileBrowser.Services;

public class FaceDetectionService : IFaceDetectionService
{
    public FaceDetectionResult DetectFaces(Image<Rgb24> image)
    {
        var detector = FaceAiSharpBundleFactory.CreateFaceDetectorWithLandmarks();
        var faces = detector.DetectFaces(image);

        var totalFaceArea = faces.Select(f => f.Box.Width * f.Box.Height).Sum();
        var imageArea = image.Width * image.Height;
        var totalFaceAreaPercentage = totalFaceArea / imageArea;

        return new FaceDetectionResult()
        {
            FaceCount = faces.Count(),
            FaceAreaPercentage = totalFaceArea
        };
    }
}