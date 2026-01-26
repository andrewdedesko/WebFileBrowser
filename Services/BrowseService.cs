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
            .Select(p => Path.GetRelativePath(sharePath, p))
            .AsEnumerable();
    }

    public IEnumerable<string> GetFiles(string share, string path)
    {
        var sharePath = _shareService.GetSharePath(share);
        var dirPath = GetPath(share, path);
        return Directory.GetFiles(dirPath)
            .Select(p => Path.GetRelativePath(sharePath, p))
            .AsEnumerable();
    }

    private string GetPath(string share, string path)
    {
        var sharePath = _shareService.GetSharePath(share);
        if (string.IsNullOrEmpty(path)) {
            return sharePath;
        }

        var absolutePath = Path.GetFullPath(Path.Combine(sharePath, path));
        if (!absolutePath.StartsWith(sharePath)) {
            throw new Exception("Invalid path");
        }

        return absolutePath;
    }
}