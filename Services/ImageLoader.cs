using System.Security.Cryptography;
using Microsoft.Extensions.Caching.Distributed;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using WebFileBrowser.Models;

namespace WebFileBrowser.Services;

public class ImageLoader {
    private readonly IShareService _shareService;
    private readonly IDistributedCache _cache;

    public ImageLoader(IShareService shareService, IDistributedCache cache) {
        _shareService = shareService;
        _cache = cache;
    }

    public ImageWrapper Load(string share, string path) {
        var fsPath = _shareService.GetPath(share, path);

        var imageFileData = File.ReadAllBytes(fsPath);
        byte[]? imageFileHash = _getCachedFileHash(share, path);
        if(imageFileHash == null) {
            imageFileHash = SHA1.HashData(imageFileData);
            _cacheFileHash(share, path, imageFileHash);
        }

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

    private byte[]? _getCachedFileHash(string share, string path) {
        return _cache.Get(_fileHashCacheKey(share, path));
    }

    private void _cacheFileHash(string share, string path, byte[] hash) {
        _cache.Set(_fileHashCacheKey(share, path), hash);
    }

    private string _fileHashCacheKey(string share, string path) => $"FileHash:Sha1:{share}:{path}";
}