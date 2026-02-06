namespace WebFileBrowser.Services;

public interface IFileTypeService {
    public bool IsImage(string path);
    public bool IsVideo(string path);
    public bool IsMacDotUnderscoreFile(string path);
    public bool IsHidden(string path);
    public bool IsNotHidden(string path);
}