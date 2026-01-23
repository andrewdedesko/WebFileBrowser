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
        var dirPath = Path.Join(sharePath, path);
        return Directory.GetDirectories(dirPath)
            .Select(p => Path.GetRelativePath(sharePath, p))
            .AsEnumerable();
    }

    public IEnumerable<string> GetFiles(string share, string path)
    {
        var sharePath = _shareService.GetSharePath(share);
        var dirPath = Path.Join(sharePath, path);
        return Directory.GetFiles(dirPath)
            .Select(p => Path.GetRelativePath(sharePath, p))
            .AsEnumerable();
    }
}