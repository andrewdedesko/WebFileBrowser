using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.ObjectPool;

namespace WebFileBrowser.Services;

class BrowseService : IBrowseService, IFileTypeService
{
    private readonly IShareService _shareService;

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

    public bool IsFile(string shareName, string path) {
        var fsPath = GetPath(shareName, path);
        return File.Exists(fsPath);
    }

    bool IBrowseService.IsDirectory(string shareName, string path) =>
        Directory.Exists(GetPath(shareName, path));

    public bool IsImage(string path) {
        var extension = Path.GetExtension(path).ToLower();
        switch(extension) {
            case ".jpg":
            case ".jpeg":
            case ".png":
            case ".webp":
                return true;

            default:
                return false;
        }
    }

    public bool IsVideo(string path) {
        var extension = Path.GetExtension(path).ToLower();
        switch(extension) {
            case ".mp4":
            case ".webm":
                return true;

            default:
                return false;
        }
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