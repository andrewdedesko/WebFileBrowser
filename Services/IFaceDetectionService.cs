using SixLabors.ImageSharp.PixelFormats;
using WebFileBrowser.Models;

namespace WebFileBrowser.Services;

public interface IFaceDetectionService
{
    public FaceDetectionResult DetectFaces(SixLabors.ImageSharp.Image<Rgb24> image);
}