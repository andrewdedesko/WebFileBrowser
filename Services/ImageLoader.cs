using System.Security.Cryptography;
using Microsoft.Extensions.Caching.Distributed;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using WebFileBrowser.Models;

namespace WebFileBrowser.Services;

public class ImageLoader {
    private readonly IShareService _shareService;
    private readonly IDistributedCache _cache;
    private readonly ILogger<ImageLoader> _logger;

    public ImageLoader(IShareService shareService, IDistributedCache cache, ILogger<ImageLoader> logger) {
        _shareService = shareService;
        _cache = cache;
        _logger = logger;
    }

    public ImageWrapper Load(string share, string path) {
        var fsPath = _shareService.GetPath(share, path);

        var imageFileData = File.ReadAllBytes(fsPath);
        string? imageFileHash = _getCachedFileHash(share, path);
        if(imageFileHash == null) {
            imageFileHash = Convert.ToHexStringLower(SHA1.HashData(imageFileData));
            _cacheFileHash(share, path, imageFileHash);
            // _logger.LogInformation("File hash cache miss for {share}:{path}", share, path);
        } else {
            // _logger.LogInformation("File hash cache hit for {hash} {share}:{path}", imageFileHash, share, path);
        }

        using(Stream stream = new MemoryStream(imageFileData)) {
            var image = Image.Load<Rgb24>(stream);
            return new ImageWrapper(share, path, imageFileHash, image);
        }
    }

    public async Task<ImageWrapper> LoadAsync(string share, string path) {
        throw new NotImplementedException();
    }

    private string? _getCachedFileHash(string share, string path) {
        return _cache.GetString(_fileHashCacheKey(share, path));
    }

    private void _cacheFileHash(string share, string path, string hash) {
        _cache.SetString(_fileHashCacheKey(share, path), hash);
    }

    private string _fileHashCacheKey(string share, string path) => $"FileHash:Sha1:{share}:{path}";
}