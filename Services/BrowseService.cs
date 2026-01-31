using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.ObjectPool;

namespace WebFileBrowser.Services;

class BrowseService : IBrowseService
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

    private string GetPath(string share, string path) =>
        _shareService.GetPath(share, path);

    private bool IsHidden(string path) =>
        Path.GetFileName(path).StartsWith(".");

    private bool IsNotHidden(string path) =>
        !IsHidden(path);

    private bool IsNotSystemFile(string path) {
        var fileName = Path.GetFileName(path).ToLower();
        if(fileName == "thumbs.db") {
            return false;
        }

        return true;
    }
}