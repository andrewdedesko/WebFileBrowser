using System.Text.Json;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using WebFileBrowser.Models;
using WebFileBrowser.Services.ObjectDetection;

namespace WebFileBrowser.Services;

public class DirectoryThumbnailer {
    private readonly ImageThumbnailer _imageThumbnailer;
    private readonly VideoThumbnailer _videoThumbnailer;
    private readonly IShareService _shareService;
    private readonly IBrowseService _browseService;
    private readonly IFileTypeService _fileTypeService;
    private readonly IAutoCropper _autoCropper;
    private readonly ThumbnailAutoCropper _thumbnailAutoCropper;
    private readonly IObjectDetectionService _objectDetectionService;
    private readonly ImageLoader _imageLoader;
    private readonly ILogger<DirectoryThumbnailer> _logger;

    public DirectoryThumbnailer(ImageThumbnailer imageThumbnailer, VideoThumbnailer videoThumbnailer, IBrowseService browseService, IFileTypeService fileTypeService, IShareService shareService, ILogger<DirectoryThumbnailer> logger, ThumbnailAutoCropper thumbnailAutoCropper, IAutoCropper autoCropper, ImageLoader imageLoader, IObjectDetectionService objectDetectionService) {
        _imageThumbnailer = imageThumbnailer;
        _videoThumbnailer = videoThumbnailer;
        _browseService = browseService;
        _fileTypeService = fileTypeService;
        _shareService = shareService;
        _logger = logger;
        _thumbnailAutoCropper = thumbnailAutoCropper;
        _autoCropper = autoCropper;
        _imageLoader = imageLoader;
        _objectDetectionService = objectDetectionService;
    }

    public ThumbnailImage? FindThumbnail(string share, string path) {
        var image = _findThumbnail(share, path);
        if(image == null) {
            return null;
        }

        return new ThumbnailImage(image);
    }

    private Image<Rgb24>? _findThumbnail(string share, string path) {
        foreach(var imagePath in FindThumbnailImages(share, path)) {
            try {
                var thumbnail = _loadThumbnailImageForFile(share, imagePath);
                if(thumbnail != null) {
                    return thumbnail;
                }
            } catch(Exception) {}
        }

        return null;
    }

    public IEnumerable<Tuple<string, CropResult>> FindBestThumbnailImage(string share, string path) {
        List<Tuple<string, CropResult>> thumbnailOptions = new();
        int attempts = 0;
        foreach(var currentPath in FindThumbnailImages(share, path)) {
            if(!_fileTypeService.IsImage(share, currentPath)) {
                continue;
            }

            try {
            // using(var image = Image.Load<Rgb24>(_shareService.GetPath(share, currentPath))) {
            using(var imageWrapper = _imageLoader.Load(share, currentPath)){
                var image = imageWrapper.Image;
                var predictions = _objectDetectionService.GetPredictions(imageWrapper);
                CropResult? cropResult = _autoCropper.FindCrop(image.Width, image.Height, predictions);
                if(cropResult != null) {
                    thumbnailOptions.Add(new Tuple<string, CropResult>(currentPath, cropResult));

                    if(thumbnailOptions.Count() >= 6) {
                        break;
                    }
                }
            }
            }catch(Exception ex) {
                _logger.LogError(ex, "Failed to analyse image {share}:{currentPath}", share, currentPath);
            }

            if(++attempts >= 10) {
                break;
            }
        }

        return thumbnailOptions;
    }

    public IEnumerable<string> FindThumbnailImages(string share, string path) {
        Queue<string> pathQueue = new();
        pathQueue.Enqueue(path);

        while(pathQueue.Any()) {
            var currPath = pathQueue.Dequeue();
            if(_browseService.IsFile(share, currPath)) {
                yield return currPath;

            } else if(_browseService.IsDirectory(share, currPath)) {
                var customThumbnailPath = _findCustomThumbnailFile(share, currPath);
                if(customThumbnailPath != null) {
                    pathQueue.Enqueue(customThumbnailPath);
                } else {
                    var files = _browseService.GetFiles(share, currPath)
                        .Order()
                        .ToArray();
                    var reorderedFiles = _getElementsMiddleOut(files);
                    foreach(var file in reorderedFiles) {
                        pathQueue.Enqueue(file);
                    }

                    var directories = _browseService.GetDirectories(share, currPath)
                        .Order();
                    foreach(var directory in directories) {
                        pathQueue.Enqueue(directory);
                    }
                }
            }
        }
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

    private IEnumerable<string> _getElementsMiddleOut(string[] array) {
        if(array.Length == 0) {
            return Array.Empty<string>();
        }

        List<string> reordered = new();
        var left = array.Length / 2;
        var right = array.Length / 2 + 1;
        while(left >= 0 || right < array.Length) {
            if(left >= 0) {
                if(left < array.Length){
                    reordered.Add(array[left]);
                }
                left--;
            }

            if(right < array.Length) {
                reordered.Add(array[right]);
                right++;
            }
        }

        return reordered;
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