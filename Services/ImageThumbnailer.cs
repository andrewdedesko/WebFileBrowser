using System.Text.Json;
using FaceAiSharp;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using WebFileBrowser.Models;

namespace WebFileBrowser.Services;

public class ImageThumbnailer {
    private readonly IShareService _shareService;
    private readonly IFileTypeService _fileTypeService;

    public ImageThumbnailer(IShareService shareService, IFileTypeService fileTypeService) {
        _shareService = shareService;
        _fileTypeService = fileTypeService;
    }

    public byte[]? GetThumbnailImage(string share, string path) {
        var customThumbnailPath = _findCustomThumbnailFile(share, path);
        if(!string.IsNullOrEmpty(customThumbnailPath)) {
            using(var srcImage = Image.Load<Rgb24>(customThumbnailPath)) {
                ScaleImageToThumbnail(srcImage);
                return GetImageAsBytes(srcImage);
            }
        }

        return GetThumbnailImageFromMiddleImage(share, path);
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
                return Path.Combine(fsPath, thumbnailConfig.Thumbnail);
            }
        }

        return null;
    }

    private byte[]? GetThumbnailImageFromMiddleImage(string share, string path) {
        string? thumbnailFilePath = null;
        var attr = System.IO.File.GetAttributes(Path.Join(_shareService.GetSharePath(share), path));
        if(true || attr.HasFlag(FileAttributes.Directory)) {
            Queue<string> dirs = new();
            dirs.Enqueue(_shareService.GetPath(share, path));

            while(thumbnailFilePath == null && dirs.Count > 0) {
                var currPath = dirs.Dequeue();
                var imageFiles = Directory.GetFiles(currPath)
                    .Where(f => _fileTypeService.IsImage(Path.GetFileName(f)))
                    .Order()
                    .ToArray();

                if(imageFiles.Any()) {
                    thumbnailFilePath = imageFiles[imageFiles.Length / 2];
                } else {
                    var subDirs = Directory.GetDirectories(currPath);
                    foreach(var d in subDirs) {
                        dirs.Enqueue(d);
                    }
                }
            }
        } else {
            thumbnailFilePath = Path.Join(_shareService.GetSharePath(share), path);
        }

        if(thumbnailFilePath == null) {
            return null;
        }

        using(var srcImage = Image.Load<Rgb24>(thumbnailFilePath)) {
            ScaleImageToThumbnail(srcImage);
            return GetImageAsBytes(srcImage);
        }
    }

    public byte[]? GetDirectoryThumbnailImageFromMiddleImageAndPreferImagesWithFaces(string directoryPath) {
        if(!Directory.Exists(directoryPath)) {
            throw new Exception($"{directoryPath} is not a directory");
        }

        var det = FaceAiSharpBundleFactory.CreateFaceDetectorWithLandmarks();
        var eyeDet = FaceAiSharpBundleFactory.CreateEyeStateDetector();

        Queue<string> dirs = new();
        dirs.Enqueue(directoryPath);

        int attempts = 0;
        string? firstImage = null;

        while(attempts < 3 && dirs.Count > 0) {
            var currPath = dirs.Dequeue();
            var imageFiles = Directory.GetFiles(currPath)
                .Where(f => _fileTypeService.IsImage(Path.GetFileName(f)))
                .Order()
                .ToArray();

            if(imageFiles.Any()) {
                List<int> sampleIndexes =
                [
                    imageFiles.Length / 2,
                    imageFiles.Length / 3,
                    imageFiles.Length / 4,
                    // 0,
                ];

                foreach(var i in sampleIndexes.Distinct()) {
                    var imagePath = imageFiles[i];
                    if(firstImage == null) {
                        firstImage = imagePath;
                    }

                    attempts++;

                    try {
                        using(var srcImage = Image.Load<Rgb24>(imagePath)) {
                            var imgCopy = srcImage.Clone();
                            var cropRect = new Rectangle(srcImage.Bounds.Width / 8, srcImage.Bounds.Height / 8, srcImage.Bounds.Width * 6 / 8, srcImage.Bounds.Height * 6 / 8);
                            imgCopy.Mutate(i => i.Crop(cropRect));

                            var faces = det.DetectFaces(imgCopy);
                            if(!faces.Any()) {
                                continue;
                            }

                            var eyeDetectionResult = det.CountEyeStates(eyeDet, imgCopy);
                            if(eyeDetectionResult.ClosedEyes > 0) {
                                continue;
                            }

                            // foreach(var face in faces) {
                            //     var rectangle = new Rectangle((int)Math.Floor(face.Box.Left), (int)Math.Floor(face.Box.Top), (int)Math.Floor(face.Box.Width), (int)Math.Floor(face.Box.Height));
                            //     srcImage.Mutate(i => i.Fill(Color.Red, rectangle));
                            // }

                            ScaleImageToThumbnail(srcImage);
                            // var star = new SixLabors.ImageSharp.Drawing.Star(x: 25.0f, y: 25.0f, prongs: 5, innerRadii: 10.0f, outerRadii: 15.0f);
                            // srcImage.Mutate(x => x.Fill(Color.RebeccaPurple, star));

                            return GetImageAsBytes(srcImage);
                        }
                    } catch(Exception) {
                        attempts++;
                        continue;
                    }
                }
            }

            var subDirs = Directory.GetDirectories(currPath);
            foreach(var d in subDirs) {
                dirs.Enqueue(d);
            }
        }

        if(firstImage != null) {
            using(var srcImage = Image.Load<Rgb24>(firstImage)) {
                ScaleImageToThumbnail(srcImage);
                return GetImageAsBytes(srcImage);
            }
        }

        return null;
    }

    public byte[] GetImageFileThumbnailImage(string share, string path) {
        var imagePath = _shareService.GetPath(share, path);

        if(!File.Exists(_shareService.GetPath(share, path))) {
            throw new Exception($"{share}:{path} is not a file");
        }

        if(!_fileTypeService.IsImage(imagePath)) {
            throw new Exception($"{share}:{path} is not a supported image");
        }

        using(var srcImage = Image.Load<Rgb24>(imagePath)) {
            ScaleImageToThumbnail(srcImage);
            return GetImageAsBytes(srcImage);
        }
    }

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

    public void ScaleImageToThumbnail(Image image) {
        var width = 0;
        var height = 0;

        if(image.Width > image.Height) {
            width = 240;
        } else {
            height = 240;
        }
        image.Mutate(x => x.Resize(width, height));
    }

    
}