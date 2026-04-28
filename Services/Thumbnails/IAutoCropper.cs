using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using WebFileBrowser.Models;

namespace WebFileBrowser.Services;

public interface IAutoCropper {
    CropResult? FindCrop(int imageWidth, int imageHeight, IEnumerable<Prediction> predictions);
}