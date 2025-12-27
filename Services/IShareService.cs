namespace WebFileBrowser.Services;

public interface IShareService
{
    public IEnumerable<string> GetShareNames();
    public string GetSharePath(string shareName);
}