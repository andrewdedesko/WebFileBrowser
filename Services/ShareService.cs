namespace WebFileBrowser.Services;

public class ShareService : IShareService
{
    public string GetSharePath(string shareName)
    {
        if(shareName == "share")
        {
            return "/mnt/share";
        }

        throw new NotImplementedException($"Unknown share {shareName}");
    }
}