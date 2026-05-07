using Microsoft.AspNetCore.SignalR;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace WebFileBrowser.Models;

public interface IImageWrapper : IDisposable {
    string Share { get; }
    string Path { get; }
    string FileHash { get; }
    Image<Rgb24> Image { get; }
    public int Width { get; }
    public int Height { get; }
}

public class LoadedImageWrapper : IImageWrapper {
    public string Share { get; init; }
    public string Path { get; init; }
    public string FileHash { get; init; }
    public Image<Rgb24> Image { get; init; }
    public int Width { get => Image.Width; }
    public int Height { get => Image.Height; }

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

public class DeferredImageWrapper : IImageWrapper {
    public string Share { get; init; }
    public string Path { get; init; }
    public string FileHash { get; init; }
    public Image<Rgb24> Image {
        get {
            if(_image == null) {
                _image = _imageProvider.Invoke();
            }

            return _image;
        }
    }

    public int Width {
        get {
            if(_image != null) {
                return _image.Width;
            }

            return _imageInfo.Width;
        }
    }

    public int Height {
        get {
            if(_image != null) {
                return _image.Height;
            }

            return _imageInfo.Height;
        }
    }

    private Image<Rgb24>? _image;
    private readonly Func<Image<Rgb24>> _imageProvider;

    private ImageInfo _imageInfo {
        get {
            if(_imageInfoCached == null) {
                _imageInfoCached = _imageInfoProvider.Invoke();
            }

            return _imageInfoCached;
        }
    }
    private ImageInfo? _imageInfoCached;
    private readonly Func<ImageInfo> _imageInfoProvider;

    public DeferredImageWrapper(string share, string path, string fileHash, Func<Image<Rgb24>> imageProvider, Func<ImageInfo> imageInfoProvider) {
        Share = share;
        Path = path;
        FileHash = fileHash;
        _imageProvider = imageProvider;
        _imageInfoProvider = imageInfoProvider;
    }

    public void Dispose() {
        if(_image != null) {
            _image.Dispose();
        }
    }
}
