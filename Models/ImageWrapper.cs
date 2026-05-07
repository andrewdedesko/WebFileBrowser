using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace WebFileBrowser.Models;

public interface IImageWrapper : IDisposable {
    string Share {get;}
    string Path {get;}
    string FileHash {get;}
    Image<Rgb24> Image {get;}
}

public class LoadedImageWrapper : IImageWrapper {
    public string Share {get; init;}
    public string Path { get; init; }
    public string FileHash { get; init; }
    public Image<Rgb24> Image { get; init; }

    public LoadedImageWrapper(string share, string path, string fileHash, Image<Rgb24> image) {
        Share = share;
        Path = path;
        FileHash = fileHash;
        Image = image;
    }

    public void Dispose() {
        Image.Dispose();
    }
}
