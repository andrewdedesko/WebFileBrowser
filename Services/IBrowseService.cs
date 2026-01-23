namespace WebFileBrowser.Services;

public interface IBrowseService
{
    public IEnumerable<string> GetDirectories(string shareName, string path);
    public IEnumerable<string> GetFiles(string shareName, string path);
}