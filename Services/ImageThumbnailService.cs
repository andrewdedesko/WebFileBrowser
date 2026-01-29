using FaceAiSharp;
using Microsoft.Extensions.Caching.Distributed;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Drawing.Processing;

namespace WebFileBrowser.Services;

public class ImageThumbnailService : IImageThumbnailService {
    private readonly IShareService _shareService;
    private readonly IBrowseService _browseService;
    private readonly IDistributedCache _cache;

    private readonly int _numberOfCandidates = 4;

    public ImageThumbnailService(IShareService shareService, IBrowseService browseService, IDistributedCache cache) {
        _shareService = shareService;
        _browseService = browseService;
        _cache = cache;
    }

    public byte[] GetImageThumbnail(string share, string path) {
        var cacheKey = $"Thumbnail:Image:{Path.Join(_shareService.GetSharePath(share), path)}";
        var cachedThumbnail = _cache.Get(cacheKey);
        if(cachedThumbnail != null) {
            return cachedThumbnail;
        }

        DistributedCacheEntryOptions cacheEntryOptions = new();
        var filePath = _shareService.GetPath(share, path);
        byte[]? data = null;
        if(Directory.Exists(filePath)) {
            data = GetDirectoryThumbnailImageFromMiddleImageAndPreferImagesWithFaces(share, path);
            // var data = GetThumbnailImageFromMiddleImage(share, path);
            // var data = GetThumbnailImageUsingComplicatedFaceDetection(share, path);
            // GetThumbnailImageFromMiddleImageAndPreferImagesWithFaces(share, path);
        } else if(File.Exists(filePath) && IsImage(filePath)) {
            data = GetImageFileThumbnailImage(share, path);
            cacheEntryOptions.SetSlidingExpiration(TimeSpan.FromHours(1));
        }


        if(data == null) {
            throw new Exception($"Could not get a thumbnail for share: {share}, path: {path}");
        }

        _cache.Set(cacheKey, data, cacheEntryOptions);
        return data;
    }

    private byte[]? GetThumbnailImageUsingComplicatedFaceDetection(string share, string path) {
        var candidateSet = GetImageThumbnailCandidates(share, path);
        var selectedCandidate = PickThumbnailCandidate(candidateSet);

        using(var srcImage = Image.Load<Rgb24>(_shareService.GetPath(selectedCandidate.Share, selectedCandidate.Path))) {
            ScaleImageToThumbnail(srcImage);
            return GetImageAsJpgBytes(srcImage);
        }
    }

    public ImageThumbnailCandidate PickThumbnailCandidate(ImageThumbnailCandidateSet candidateSet) {
        var candidates = candidateSet.Candidates;

        // Find images with faces
        if(candidates.Any(c => c.FaceCount > 0)) {
            candidates = candidates.Where(c => c.FaceCount > 0);

            // Find images with eyes open
            if(candidates.Any(c => c.OpenEyesCount > 0 && c.ClosedEyesCount == 0)) {
                candidates = candidates.Where(c => c.OpenEyesCount > 0 && c.ClosedEyesCount == 0);
            }

            // Se if there's an image that's not a close up of someone's face
            if(candidates.Any(c => c.FaceAreaPercentage <= 0.1)) {
                candidates = candidates.Where(c => c.FaceAreaPercentage <= 0.1);
            }

            // var facePercentages = candidates
            //     .Select(c => c.FaceAreaPercentage)
            //     .Order()
            //     .ToArray();
            // var lowerThreshold = facePercentages[0];
            // var upperThreshold = facePercentages[facePercentages.Length * 4 / 5];

            // return candidates
            //     .Where(c => c.FaceAreaPercentage >= lowerThreshold && c.FaceAreaPercentage <= upperThreshold)
            //     .OrderBy(c => c.FileSystemOrder)
            //     .First();
            return candidates.OrderByDescending(c => c.FaceAreaPercentage).First();
        }

        return candidateSet.Candidates
            .OrderBy(c => c.FileSystemOrder)
            .First();
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
                    .Where(f => IsImage(Path.GetFileName(f)))
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
            return GetImageAsJpgBytes(srcImage);
        }
    }

    private byte[]? GetDirectoryThumbnailImageFromMiddleImageAndPreferImagesWithFaces(string share, string path) {
        if(!Directory.Exists(_shareService.GetPath(share, path))) {
            throw new Exception($"{share}:{path} is not a directory");
        }

        var det = FaceAiSharpBundleFactory.CreateFaceDetectorWithLandmarks();
        var eyeDet = FaceAiSharpBundleFactory.CreateEyeStateDetector();

        Queue<string> dirs = new();
        dirs.Enqueue(_shareService.GetPath(share, path));

        int attempts = 0;
        string? firstImage = null;

        while(attempts < 3 && dirs.Count > 0) {
            var currPath = dirs.Dequeue();
            var imageFiles = Directory.GetFiles(currPath)
                .Where(f => IsImage(Path.GetFileName(f)))
                .Order()
                .ToArray();

            if(imageFiles.Any()) {
                List<int> sampleIndexes =
                [
                    imageFiles.Length / 2,
                        imageFiles.Length / 3,
                        0,
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

                            return GetImageAsJpgBytes(srcImage);
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
                return GetImageAsJpgBytes(srcImage);
            }
        }

        return null;
    }

    private byte[] GetImageFileThumbnailImage(string share, string path) {
        var imagePath = _shareService.GetPath(share, path);

        if(!File.Exists(_shareService.GetPath(share, path))) {
            throw new Exception($"{share}:{path} is not a file");
        }

        if(!IsImage(imagePath)) {
            throw new Exception($"{share}:{path} is not a supported image");
        }

        using(var srcImage = Image.Load<Rgb24>(imagePath)) {
            ScaleImageToThumbnail(srcImage);
            return GetImageAsJpgBytes(srcImage);
        }
    }

    public byte[] GetImageAsJpgBytes(Image image) {
        ScaleImageToThumbnail(image);

        var thumbnailImageStream = new MemoryStream();
        var writer = new StreamWriter(thumbnailImageStream);
        image.SaveAsJpeg(thumbnailImageStream);

        var thumbnailData = thumbnailImageStream.ToArray();
        return thumbnailData;
    }

    private void ScaleImageToThumbnail(Image image) {
        var width = 0;
        var height = 0;

        if(image.Width > image.Height) {
            width = 240;
        } else {
            height = 240;
        }
        image.Mutate(x => x.Resize(width, height));
    }

    public ImageThumbnailCandidateSet GetImageThumbnailCandidates(string share, string path) {
        var sharePath = _shareService.GetSharePath(share);
        var targetPath = Path.Join(sharePath, path);
        if(!Directory.Exists(targetPath)) {
            return new ImageThumbnailCandidateSet() {
                Candidates = Enumerable.Empty<ImageThumbnailCandidate>()
            };
        }

        var det = FaceAiSharpBundleFactory.CreateFaceDetectorWithLandmarks();
        var eyeDet = FaceAiSharpBundleFactory.CreateEyeStateDetector();

        Queue<string> dirs = new();
        List<string> images = new();

        dirs.Enqueue(targetPath);
        while(images.Count < _numberOfCandidates && dirs.Count > 0) {
            var currPath = dirs.Dequeue();
            var imageFiles = Directory.GetFiles(currPath)
                .Where(f => IsImage(Path.GetFileName(f)))
                .Order()
                .ToArray();

            if(imageFiles.Any()) {
                var lowerIndex = imageFiles.Length / 4;
                var upperIndex = imageFiles.Length * 3 / 4;
                var delta = upperIndex - lowerIndex;
                if(delta <= 1) {
                    images.Add(imageFiles[lowerIndex]);
                } else {
                    images.Add(imageFiles[lowerIndex]);
                    // images.Add(imageFiles.ElementAt(lowerIndex + (delta / 2)));
                    images.Add(imageFiles[upperIndex]);
                }
            }

            var subDirs = Directory.GetDirectories(currPath);
            foreach(var d in subDirs) {
                dirs.Enqueue(d);
            }
        }

        List<ImageThumbnailCandidate> thumbnailCandidates = new();
        foreach(var imagePath in images.AsEnumerable()) {
            using(var image = Image.Load<Rgb24>(imagePath)) {
                var faces = det.DetectFaces(image);
                var faceCount = faces.Count();

                var eyeResult = det.CountEyeStates(eyeDet, image);

                var totalFaceArea = faces.Sum(f => f.Box.Width * f.Box.Height);
                var imageArea = image.Width * image.Height;
                var totalFaceAreaPercentage = totalFaceArea / imageArea;

                thumbnailCandidates.Add(new ImageThumbnailCandidate() {
                    Share = share,
                    Path = Path.GetRelativePath(sharePath, imagePath),
                    Filename = Path.GetFileName(imagePath),
                    FileSystemOrder = 0,
                    FaceCount = faceCount,
                    OpenEyesCount = eyeResult.OpenEyes,
                    ClosedEyesCount = eyeResult.ClosedEyes,
                    OpenEyesPercentage = eyeResult.OpenEyes == 0 ? 0 : eyeResult.OpenEyes / (eyeResult.OpenEyes + eyeResult.ClosedEyes),
                    Confidence = faces.Average(f => f.Confidence).GetValueOrDefault(0),
                    FaceAreaPercentage = totalFaceAreaPercentage,
                    TopPick = false
                });
            }
        }

        return new ImageThumbnailCandidateSet() {
            Candidates = thumbnailCandidates
        };
    }

    private bool IsImage(string path) {
        var extension = Path.GetExtension(path).ToLower();
        switch(extension) {
            case ".jpg":
            case ".jpeg":
            case ".png":
                return true;

            default:
                return false;
        }
    }
}