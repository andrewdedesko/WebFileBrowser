using System.Text.Json;
using FaceAiSharp;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Drawing.Processing;
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

    public byte[] GetThumbnailImage(string share, string path, int size) {
        // var customThumbnailPath = _findCustomThumbnailFile(share, path);
        // if(!string.IsNullOrEmpty(customThumbnailPath)) {
        //     using(var srcImage = Image.Load<Rgb24>(customThumbnailPath)) {
        //         _cropToSquareAroundFace(srcImage);
        //         ScaleImageToThumbnail(srcImage, size);
        //         return GetImageAsBytes(srcImage);
        //     }
        // }

        var thumbnailFilePath = _findThumbnailFilePath(share, path);
        if(thumbnailFilePath == null) {
            throw new Exception($"Could not find thumbnail file for {share}:{path}");
        }

        using(var srcImage = Image.Load<Rgb24>(_shareService.GetPath(share, thumbnailFilePath))) {
            CropImageToSquareAroundFace(srcImage);
            ScaleImageToThumbnail(srcImage, size);
            return GetImageAsBytes(srcImage);
        }
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

    private string? _findMiddleImage(string share, string path) {
        if(!_browseService.IsDirectory(share, path)) {
            throw new ArgumentException($"Method can only work with a directory, {share}:{path} is not a directory");
        }

        string? thumbnailFilePath = null;
        Queue<string> dirs = new();
        dirs.Enqueue(path);

        while(thumbnailFilePath == null && dirs.Count > 0) {
            var currPath = dirs.Dequeue();
            var imageFiles = _browseService.GetDirectories(share, currPath)
                .Where(f => _fileTypeService.IsImage(share, f))
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

        return thumbnailFilePath;
    }

    private byte[] GetThumbnailImageFromMiddleImage(string share, string path, int size) {
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
            throw new ThumbnailNotAvailableException();
        }

        using(var srcImage = Image.Load<Rgb24>(thumbnailFilePath)) {
            CropImageToSquareAroundFace(srcImage);
            ScaleImageToThumbnail(srcImage, size);
            return GetImageAsBytes(srcImage);
        }
    }

    public byte[]? GetDirectoryThumbnailImageFromMiddleImageAndPreferImagesWithFaces(string share, string path, int size, bool annotateImage = false) {
        var directoryPath = _shareService.GetPath(share, path);
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

                            var srcWidth = srcImage.Bounds.Width;
                            var srcHeight = srcImage.Bounds.Height;

                            // Annotate faces
                            if(annotateImage) {
                                var pen = Pens.Dot(Color.GreenYellow, 2);
                                foreach(var face in faces) {
                                    var faceX = (int)Math.Floor((face.Box.Left + srcWidth / 8));
                                    var faceWidth = (int)Math.Floor(face.Box.Width);

                                    var faceY = (int)Math.Floor((face.Box.Top + srcHeight / 8));
                                    var faceHeight = (int)Math.Floor(face.Box.Height);

                                    var rectangle = new Rectangle(faceX, faceY, faceWidth, faceHeight);
                                    srcImage.Mutate(i => i.Draw(pen, rectangle));
                                }
                            }

                            // Crop to square around face
                            if(faces.Any()) {
                                var face = faces.First();
                                int srcFaceLeft = (int)Math.Floor((face.Box.Left + srcWidth / 8));
                                int srcFaceRight = srcFaceLeft + (int)Math.Floor(face.Box.Width);
                                int srcFaceTop = (int)Math.Floor((face.Box.Top + srcHeight / 8));
                                int srcFaceBottom = srcFaceTop + (int)Math.Floor(face.Box.Height);

                                var smallestDimension = Math.Min(srcWidth, srcHeight);

                                var cropLeft = 0;
                                if(srcWidth > smallestDimension) {
                                    int srcFaceCentre = srcFaceLeft + (int)face.Box.Width / 2;
                                    float srcFaceCentrePercentage = (float)srcFaceCentre / srcWidth;

                                    int cropFaceCentre = (int)Math.Floor(smallestDimension * srcFaceCentrePercentage);
                                    cropLeft = srcFaceCentre - cropFaceCentre;

                                    if(face.Box.Width < smallestDimension) {
                                        // if(srcFaceLeft < cropLeft) {
                                        //     cropLeft = srcFaceLeft;
                                        // }else if(srcFaceRight > cropLeft + smallestDimension) {
                                        //     cropLeft = srcFaceRight - smallestDimension;
                                        // }
                                        if(srcFaceLeft <= cropLeft || srcFaceRight >= cropLeft + smallestDimension) {
                                            cropLeft = (int)Math.Clamp(srcFaceCentre - (srcWidth / 2), 0, srcWidth);
                                        }
                                    }
                                }

                                var cropTop = 0;
                                if(srcHeight > smallestDimension) {
                                    int srcFaceCentre = srcFaceTop + (int)face.Box.Height / 2;
                                    float srcFaceCentrePercentage = (float)srcFaceCentre / srcHeight;


                                    int cropFaceCentre = (int)Math.Floor(smallestDimension * srcFaceCentrePercentage);
                                    cropTop = srcFaceCentre - cropFaceCentre;
                                    if(face.Box.Height < smallestDimension) {
                                        // if(srcFaceTop < cropTop) {
                                        //     cropTop = srcFaceTop;
                                        // }else if(srcFaceBottom > cropTop + smallestDimension) {
                                        //     cropTop = srcFaceBottom - smallestDimension;
                                        // }

                                        if(srcFaceTop <= cropTop || srcFaceBottom >= cropTop + smallestDimension) {
                                            cropTop = (int)Math.Clamp(srcFaceCentre - (srcHeight / 2), 0, srcHeight);
                                        }
                                    }
                                }

                                var cropPen = Pens.Dash(Color.MediumVioletRed, 2);
                                var cropRectangle = new Rectangle(cropLeft, cropTop, smallestDimension, smallestDimension);

                                if(annotateImage) {
                                    srcImage.Mutate(i => i.Draw(cropPen, cropRectangle));
                                } else {
                                    srcImage.Mutate(i => i.Crop(cropRectangle));
                                }
                            }

                            if(!annotateImage) {
                                ScaleImageToThumbnail(srcImage, size);
                            }

                            return GetImageAsBytes(srcImage);
                        }
                    } catch(Exception ex) {
                        _logger.LogError(ex, "An error occurred trying to thumbnail {0}:{1}", share, path);
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
                ScaleImageToThumbnail(srcImage, size);
                return GetImageAsBytes(srcImage);
            }
        }

        return null;
    }

    public void CropImageToSquareAroundFace(Image<Rgb24> srcImage, bool annotateImage = false) {
        var det = FaceAiSharpBundleFactory.CreateFaceDetectorWithLandmarks();
        var eyeDet = FaceAiSharpBundleFactory.CreateEyeStateDetector();

        var imgCopy = srcImage.Clone();
        var cropRect = new Rectangle(srcImage.Bounds.Width / 8, srcImage.Bounds.Height / 8, srcImage.Bounds.Width * 6 / 8, srcImage.Bounds.Height * 6 / 8);
        imgCopy.Mutate(i => i.Crop(cropRect));

        var faces = det.DetectFaces(imgCopy);
        if(!faces.Any()) {
            return;
        }

        var srcWidth = srcImage.Bounds.Width;
        var srcHeight = srcImage.Bounds.Height;

        // Annotate faces
        if(annotateImage) {
            var pen = Pens.Dot(Color.GreenYellow, 2);
            foreach(var face in faces) {
                var faceX = (int)Math.Floor((face.Box.Left + srcWidth / 8));
                var faceWidth = (int)Math.Floor(face.Box.Width);

                var faceY = (int)Math.Floor((face.Box.Top + srcHeight / 8));
                var faceHeight = (int)Math.Floor(face.Box.Height);

                var rectangle = new Rectangle(faceX, faceY, faceWidth, faceHeight);
                srcImage.Mutate(i => i.Draw(pen, rectangle));
            }
        }

        // Crop to square around face
        if(faces.Any()) {
            var _mostLeftFace = faces.Select(f => f.Box.Left).Order().First();
            var _mostRightFace = faces.Select(f => f.Box.Right).OrderDescending().First();
            var _mostTopFace = faces.Select(f => f.Box.Top).Order().First();
            var _mostBottomFace = faces.Select(f => f.Box.Bottom).OrderDescending().First();

            double imageMarginLower = 0.33;
            double imageMarginUpper = 0.66;
            if(faces.Count() > 1) {
                imageMarginLower = 0.2;
                imageMarginUpper = 0.8;
            }


            int faceMargin = 0;

            int srcFaceLeft = (int)Math.Floor((_mostLeftFace + srcWidth / 8)) - faceMargin;
            int srcFaceRight = (int)Math.Floor(_mostRightFace + srcWidth / 8) + faceMargin;
            int srcFaceTop = (int)Math.Floor((_mostTopFace + srcHeight / 8)) - faceMargin;
            int srcFaceBottom = (int)Math.Floor(_mostBottomFace + srcHeight / 8) + faceMargin;
            var srcFaceWidth = srcFaceRight - srcFaceLeft;
            var srcFaceHeight = srcFaceBottom - srcFaceTop;

            var smallestDimension = Math.Min(srcWidth, srcHeight);

            var cropLeft = 0;
            if(srcWidth > smallestDimension) {
                int srcFaceCentre = srcFaceLeft + srcFaceWidth / 2;
                double srcFaceCentrePercentage = (double)srcFaceCentre / srcWidth;

                srcFaceCentrePercentage = Math.Clamp(srcFaceCentrePercentage, imageMarginLower, imageMarginUpper);

                int cropFaceCentre = (int)Math.Floor(smallestDimension * srcFaceCentrePercentage);
                cropLeft = srcFaceCentre - cropFaceCentre;

                if(cropLeft < 0) {
                    cropLeft = 0;
                }

                if(cropLeft + smallestDimension > srcWidth) {
                    cropLeft = srcWidth - smallestDimension;
                }
            }

            var cropTop = 0;
            if(srcHeight > smallestDimension) {
                int srcFaceCentre = srcFaceTop + srcFaceHeight / 2;
                double srcFaceCentrePercentage = (double)srcFaceCentre / srcHeight;

                srcFaceCentrePercentage = Math.Clamp(srcFaceCentrePercentage, imageMarginLower, imageMarginUpper);

                if(annotateImage) {
                    var centreLinePen = Pens.Solid(Color.AliceBlue);
                    srcImage.Mutate(i => i.Draw(centreLinePen, new Rectangle(0, srcFaceCentre, srcWidth, 2)));
                }

                int cropFaceCentre = (int)Math.Floor(smallestDimension * srcFaceCentrePercentage);
                cropTop = srcFaceCentre - cropFaceCentre;
                if(cropTop < 0) {
                    cropTop = 0;
                }

                if(cropTop + smallestDimension > srcHeight) {
                    cropTop = srcHeight - smallestDimension;
                }
            }

            var cropPen = Pens.Dash(Color.MediumVioletRed, 2);
            var cropRectangle = new Rectangle(cropLeft, cropTop, smallestDimension, smallestDimension);

            if(annotateImage) {
                srcImage.Mutate(i => i.Draw(cropPen, cropRectangle));
            } else {
                srcImage.Mutate(i => i.Crop(cropRectangle));
            }
        }
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