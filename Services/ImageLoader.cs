using System.Security.Cryptography;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using WebFileBrowser.Models;

namespace WebFileBrowser.Services;

public class ImageLoader {
    private readonly IShareService _shareService;

    public ImageLoader(IShareService shareService) {
        _shareService = shareService;
    }

    public ImageWrapper Load(string share, string path) {
        var fsPath = _shareService.GetPath(share, path);

        var imageFileData = File.ReadAllBytes(fsPath);
        var imageFileHash = SHA1.HashData(imageFileData);

        using(Stream stream = new MemoryStream(imageFileData)) {
            var image = Image.Load<Rgb24>(stream);
            return new ImageWrapper(share, path, imageFileHash, image);
        }
    }

    public async Task<ImageWrapper> LoadAsync(string share, string path) {
        var imageFileData = await File.ReadAllBytesAsync(path);
        var imageFileHash = SHA1.HashData(imageFileData);

        using(Stream stream = new MemoryStream(imageFileData)){
            var image = await Image.LoadAsync<Rgb24>(stream);
            return new ImageWrapper(share, path, imageFileHash, image);
        }
    }
}