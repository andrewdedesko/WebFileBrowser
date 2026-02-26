namespace WebFileBrowser.Services;

public interface IBrowseService
{
    public IEnumerable<string> GetDirectories(string shareName, string path);
    public IEnumerable<string> GetFiles(string shareName, string path);
    public bool IsDirectory(string share, string path);
    public bool IsFile(string share, string path);
    public bool Exists(string share, string path);
}