using System.Text.Json;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using WebFileBrowser.Models;

namespace WebFileBrowser.Services;

public class DirectoryThumbnailer {
    private readonly ImageThumbnailer _imageThumbnailer;
    private readonly VideoThumbnailer _videoThumbnailer;
    private readonly IShareService _shareService;
    private readonly IBrowseService _browseService;
    private readonly IFileTypeService _fileTypeService;

    public DirectoryThumbnailer(ImageThumbnailer imageThumbnailer, VideoThumbnailer videoThumbnailer, IBrowseService browseService, IFileTypeService fileTypeService, IShareService shareService) {
        _imageThumbnailer = imageThumbnailer;
        _videoThumbnailer = videoThumbnailer;
        _browseService = browseService;
        _fileTypeService = fileTypeService;
        _shareService = shareService;
    }

    public byte[]? FindThumbnail(string share, string path, int size) {
        var image = _findThumbnail(share, path);
        if(image == null) {
            return null;
        }

        _imageThumbnailer.CropImageToSquareAroundFace(image);
        _imageThumbnailer.ScaleImageToThumbnail(image, size);
        return _imageThumbnailer.GetImageAsBytes(image);
    }

    private Image<Rgb24>? _findThumbnail(string share, string path) {
        Queue<string> pathQueue = new();
        pathQueue.Enqueue(path);

        while(pathQueue.Any()) {
            var currPath = pathQueue.Dequeue();
            if(_browseService.IsFile(share, currPath)) {
                try {
                    var thumbnail = _loadThumbnailImageForFile(share, currPath);
                    if(thumbnail != null) {
                        return thumbnail;
                    }
                }catch(Exception ex) {
                    
                }

            } else if(_browseService.IsDirectory(share, currPath)) {
                var customThumbnailPath = _findCustomThumbnailFile(share, currPath);
                if(customThumbnailPath != null) {
                    pathQueue.Enqueue(customThumbnailPath);
                } else {
                    var files = _browseService.GetFiles(share, currPath);
                    foreach(var file in files) {
                        pathQueue.Enqueue(file);
                    }

                    var directories = _browseService.GetDirectories(share, currPath);
                    foreach(var directory in directories) {
                        pathQueue.Enqueue(directory);
                    }
                }
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

    private Image<Rgb24>? _loadThumbnailImageForFile(string share, string path) {
        if(_fileTypeService.IsImage(share, path)) {
            return _imageThumbnailer.LoadThumbnailImageForFile(share, path);
        }

        if(_fileTypeService.IsVideo(path)) {
            return _videoThumbnailer.LoadThumbnailImageForFile(share, path);
        }

        return null;
    }
}