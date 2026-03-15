using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace WebFileBrowser.Models;

public record ThumbnailImage(Image<Rgb24> Image) {
}