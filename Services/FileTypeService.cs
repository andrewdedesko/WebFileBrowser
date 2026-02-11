namespace WebFileBrowser.Services;

public class FileTypeService : IFileTypeService {
    private readonly IEnumerable<string> _imageExtensions = new List<string>()
    {
        ".jpg", ".jpeg", ".png", ".webp", ".gif"
    };

    private readonly IEnumerable<string> _videoExtensions = new List<string>() {
        ".mp4", ".m4v", ".webm"
    };

    private readonly IShareService _shareService;

    public FileTypeService(IShareService shareService) {
        _shareService = shareService;
    }

    public bool IsFile(string shareName, string path) =>
        IsFile(GetPath(shareName, path));

    public bool IsFile(string path) =>
        File.Exists(path);

    public bool IsDirectory(string shareName, string path) =>
        IsDirectory(GetPath(shareName, path));

    public bool IsDirectory(string path) =>
        Directory.Exists(path);

    public bool IsImage(string path) {
        var extension = Path.GetExtension(path).ToLower();
        return _imageExtensions.Contains(extension);
    }

    public bool IsVideo(string path) {
        var extension = Path.GetExtension(path).ToLower();
        return _videoExtensions.Contains(extension);
    }

    private string GetPath(string share, string path) =>
        _shareService.GetPath(share, path);

    public bool IsNotHidden(string path) =>
        !IsHidden(path);

    public bool IsNotSystemFile(string path) {
        var fileName = Path.GetFileName(path).ToLower();
        if(fileName == "thumbs.db") {
            return false;
        }

        return true;
    }

    public bool IsMacDotUnderscoreFile(string path) =>
        Path.GetFileName(path).StartsWith("._");

    public bool IsHidden(string path) =>
        Path.GetFileName(path).StartsWith(".");
}