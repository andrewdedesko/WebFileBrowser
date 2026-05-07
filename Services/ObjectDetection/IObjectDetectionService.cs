using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using WebFileBrowser.Models;

namespace WebFileBrowser.Services.ObjectDetection;

public interface IObjectDetectionService {
    IEnumerable<Prediction> GetPredictions(IImageWrapper image);
}