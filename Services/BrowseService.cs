using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.ObjectPool;

namespace WebFileBrowser.Services;

class BrowseService : IBrowseService, IFileTypeService
{
    private readonly IShareService _shareService;

    private readonly IEnumerable<string> _imageExtensions = new List<string>()
    {
        ".jpg", ".jpeg", ".png", ".webp", ".gif"
    };

    private readonly IEnumerable<string> _videoExtensions = new List<string>() {
        ".mp4", ".m4v", ".webm"
    };

    public BrowseService(IShareService shareService)
    {
        _shareService = shareService;
    }

    public IEnumerable<string> GetDirectories(string share, string path)
    {
        var sharePath = _shareService.GetSharePath(share);
        var dirPath = GetPath(share, path);
        return Directory.GetDirectories(dirPath)
            .Where(IsNotHidden)
            .Select(p => Path.GetRelativePath(sharePath, p))
            .AsEnumerable();
    }

    public IEnumerable<string> GetFiles(string share, string path)
    {
        var sharePath = _shareService.GetSharePath(share);
        var dirPath = GetPath(share, path);
        return Directory.GetFiles(dirPath)
            .Where(IsNotHidden)
            .Where(IsNotSystemFile)
            .Select(p => Path.GetRelativePath(sharePath, p))
            .AsEnumerable();
    }

    public bool IsFile(string shareName, string path) =>
        IsFile(GetPath(shareName, path));

    public bool IsFile(string path) =>
        File.Exists(path);

    public bool IsDirectory(string shareName, string path) =>
        IsDirectory(GetPath(shareName, path));

    public bool IsDirectory(string path) =>
        Directory.Exists(path);

    public bool IsImage(string path) {
        var extension = Path.GetExtension(path).ToLower();
        return _imageExtensions.Contains(extension);
    }

    public bool IsVideo(string path) {
        var extension = Path.GetExtension(path).ToLower();
        return _videoExtensions.Contains(extension);
    }

    private string GetPath(string share, string path) =>
        _shareService.GetPath(share, path);

    public bool IsNotHidden(string path) =>
        !IsHidden(path);

    private bool IsNotSystemFile(string path) {
        var fileName = Path.GetFileName(path).ToLower();
        if(fileName == "thumbs.db") {
            return false;
        }

        return true;
    }

    public bool IsMacDotUnderscoreFile(string path) =>
        Path.GetFileName(path).StartsWith("._");

    public bool IsHidden(string path) =>
        Path.GetFileName(path).StartsWith(".");
}