using WebFileBrowser.Configuration;

namespace WebFileBrowser.Services;

public class ShareService : IShareService
{
    private readonly ShareMapping _shares;

    public ShareService(ShareMapping shares)
    {
        _shares = shares;
    }

    public IEnumerable<string> GetShareNames()
    {
        return _shares.GetShares();
    }

    public string GetSharePath(string shareName)
    {
        if (_shares.Contains(shareName))
        {
            return _shares.GetSharePath(shareName);
        }

        throw new NotImplementedException($"Unknown share {shareName}");
    }

    public string GetPath(string share, string path) {
        var sharePath = GetSharePath(share);
        if(string.IsNullOrEmpty(path)) {
            return sharePath;
        }

        var absolutePath = Path.GetFullPath(Path.Combine(sharePath, path));
        if(!absolutePath.StartsWith(sharePath)) {
            throw new Exception("Invalid path");
        }

        return absolutePath;
    }
}