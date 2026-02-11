namespace WebFileBrowser.Services;

public interface IFileTypeService {
    public bool IsFile(string shareName, string path);
    public bool IsFile(string path);
    public bool IsDirectory(string shareName, string path);
    public bool IsDirectory(string path);
    public bool IsImage(string path);
    public bool IsVideo(string path);
    public bool IsMacDotUnderscoreFile(string path);
    public bool IsHidden(string path);
    public bool IsNotHidden(string path);
    public bool IsNotSystemFile(string path);
}