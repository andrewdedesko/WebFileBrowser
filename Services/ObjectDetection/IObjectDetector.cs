using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using WebFileBrowser.Models;

namespace WebFileBrowser.Services.ObjectDetection;

public interface IObjectDetector {
    public IEnumerable<Prediction> FindObjects(Image<Rgb24> sourceImage);
}