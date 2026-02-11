namespace WebFileBrowser.Services;

public class FileSystemBrowseService : IBrowseService {
    private readonly IShareService _shareService;
    private readonly IFileTypeService _fileTypeService;

    public FileSystemBrowseService(IShareService shareService, IFileTypeService fileTypeService) {
        _shareService = shareService;
        _fileTypeService = fileTypeService;
    }

    public IEnumerable<string> GetDirectories(string share, string path) {
        var sharePath = _shareService.GetSharePath(share);
        var dirPath = _shareService.GetPath(share, path);
        return Directory.GetDirectories(dirPath)
            .Where(_fileTypeService.IsNotHidden)
            .Select(p => Path.GetRelativePath(sharePath, p))
            .AsEnumerable();
    }

    public IEnumerable<string> GetFiles(string share, string path) {
        var sharePath = _shareService.GetSharePath(share);
        var dirPath = _shareService.GetPath(share, path);
        return Directory.GetFiles(dirPath)
            .Where(_fileTypeService.IsNotHidden)
            .Where(_fileTypeService.IsNotSystemFile)
            .Select(p => Path.GetRelativePath(sharePath, p))
            .AsEnumerable();
    }
}