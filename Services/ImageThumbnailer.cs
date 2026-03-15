using System.Text.Json;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using WebFileBrowser.Models;

namespace WebFileBrowser.Services;

public class ImageThumbnailer {
    private readonly IShareService _shareService;
    private readonly IBrowseService _browseService;
    private readonly IFileTypeService _fileTypeService;
    private readonly ILogger<ImageThumbnailer> _logger;

    public ImageThumbnailer(IShareService shareService, IFileTypeService fileTypeService, ILogger<ImageThumbnailer> logger, IBrowseService browseService) {
        _shareService = shareService;
        _fileTypeService = fileTypeService;
        _logger = logger;
        _browseService = browseService;
    }

    public Image<Rgb24> LoadThumbnailImageForFile(string share, string path) {
        return Image.Load<Rgb24>(_shareService.GetPath(share, path));
    }

    private string? _findThumbnailFilePath(string share, string path) {
        if(_browseService.IsFile(share, path)) {
            if(_fileTypeService.IsImage(share, path)) {
                return path;
            } else {
                return null;
            }
        }

        var customThumbnailPath = _findCustomThumbnailFile(share, path);
        if(customThumbnailPath != null) {
            return _findThumbnailFilePath(share, customThumbnailPath);
        }

        var imageFiles = _browseService.GetFiles(share, path)
            .Where(_fileTypeService.IsImage)
            .Order()
            .ToArray();
        if(imageFiles.Any()) {
            return imageFiles[imageFiles.Length / 2];
        }

        var directories = _browseService.GetDirectories(share, path);
        foreach(var dir in directories) {
            var thumbnailFilePath = _findThumbnailFilePath(share, dir);
            if(thumbnailFilePath != null) {
                return thumbnailFilePath;
            }
        }

        return null;
    }

    private string? _findCustomThumbnailFile(string share, string path) {
        var fsPath = _shareService.GetPath(share, path);
        if(!_fileTypeService.IsDirectory(fsPath)) {
            return null;
        }

        var thumbnailConfigPath = Path.Combine(fsPath, ".thumbnail.json");
        if(_fileTypeService.IsFile(thumbnailConfigPath)) {
            var thumbnailConfig = JsonSerializer.Deserialize<ThumbnailConfig>(File.ReadAllText(thumbnailConfigPath));
            if(thumbnailConfig != null && !string.IsNullOrEmpty(thumbnailConfig.Thumbnail)) {
                var thumbnailPath = Path.GetRelativePath(_shareService.GetSharePath(share), Path.Combine(fsPath, thumbnailConfig.Thumbnail));
                return thumbnailPath;
            }
        }

        return null;
    }

    

    public byte[] GetImageFileThumbnailImage(string share, string path, int size) {
        var imagePath = _shareService.GetPath(share, path);

        if(!File.Exists(_shareService.GetPath(share, path))) {
            throw new Exception($"{share}:{path} is not a file");
        }

        if(!_fileTypeService.IsImage(imagePath)) {
            throw new Exception($"{share}:{path} is not a supported image");
        }

        using(var srcImage = Image.Load<Rgb24>(imagePath)) {
            ScaleImageToThumbnail(srcImage, size);
            return GetImageAsBytes(srcImage);
        }
    }

    public byte[] GetImageAsBytes(ThumbnailImage image) =>
        GetImageAsBytes(image.Image);

    public byte[] GetImageAsBytes(Image image) =>
        _GetImageAsWebpBytes(image);

    private byte[] _GetImageAsJpgBytes(Image image) {
        var thumbnailImageStream = new MemoryStream();
        var writer = new StreamWriter(thumbnailImageStream);
        image.SaveAsJpeg(thumbnailImageStream);

        var thumbnailData = thumbnailImageStream.ToArray();
        return thumbnailData;
    }

    private byte[] _GetImageAsWebpBytes(Image image) {
        var thumbnailImageStream = new MemoryStream();
        image.SaveAsWebp(thumbnailImageStream, new SixLabors.ImageSharp.Formats.Webp.WebpEncoder() {
            FileFormat = SixLabors.ImageSharp.Formats.Webp.WebpFileFormatType.Lossy,
            Quality = 100
        });

        var thumbnailData = thumbnailImageStream.ToArray();
        return thumbnailData;
    }

    public void ScaleImageToThumbnail(ThumbnailImage image, int size) {
        ScaleImageToThumbnail(image.Image, size);
    }

    public void ScaleImageToThumbnail(Image image, int size) {
        var width = 0;
        var height = 0;

        if(image.Width > image.Height) {
            width = size;
        } else {
            height = size;
        }
        image.Mutate(x => x.Resize(width, height));
    }
}