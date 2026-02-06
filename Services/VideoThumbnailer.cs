using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace WebFileBrowser.Services;

public class VideoThumbnailer {
    private readonly ImageThumbnailer _imageThumbnailer;

    public VideoThumbnailer(ImageThumbnailer imageThumbnailer) {
        _imageThumbnailer = imageThumbnailer;
    }

    public byte[]? GetVideoThumbnail(string path) {
        var thumbnailPath = FindVideoThumbnailPath(path);
        if(thumbnailPath == null) {
            return null;
        }

        using(var srcImage = Image.Load<Rgb24>(thumbnailPath)) {
            _imageThumbnailer.ScaleImageToThumbnail(srcImage);
            return _imageThumbnailer.GetImageAsBytes(srcImage);
        }
    }

    public string? FindVideoThumbnailPath(string videoPath) {
        var videoFileName = Path.GetFileName(videoPath);
        if(File.Exists(videoPath)) {
            var directoryPath = Directory.GetParent(videoPath).FullName;
            var files = Directory.GetFiles(directoryPath);
            var thumbnailFilePaths = files
                .Where(f => IsVideoThumbnailFile(f, videoFileName))
                .AsEnumerable();

            if(thumbnailFilePaths.Any()) {
                return thumbnailFilePaths.First();
            }
        }

        return null;
    }

    public static bool IsVideoThumbnailFile(string path, string videoFileName) {
        var extension = Path.GetExtension(path).ToLower();
        if(!(extension == ".jpg" || extension == ".jpeg" || extension == ".png")) {
            return false;
        }

        var fileName = Path.GetFileName(path);
        if(fileName.StartsWith(".")) {
            fileName = fileName.Substring(1);
        }

        if(Path.GetFileNameWithoutExtension(fileName) == videoFileName) {
            return true;
        }

        return false;
    }
}