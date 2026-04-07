using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace WebFileBrowser.Models;

public class ImageWrapper : IDisposable {
    public string Share;
    public string Path;
    public string FileHash;
    public Image<Rgb24> Image;

    public ImageWrapper(string share, string path, string fileHash, Image<Rgb24> image) {
        Share = share;
        Path = path;
        FileHash = fileHash;
        Image = image;
    }

    public void Dispose() {
        Image.Dispose();
    }
}
