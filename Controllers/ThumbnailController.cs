using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SixLabors.Fonts;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Drawing.Processing;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using WebFileBrowser.Extensions;
using WebFileBrowser.Models;
using WebFileBrowser.Services;
using WebFileBrowser.Services.ObjectDetection;

namespace WebFileBrowser.Controllers;

[Authorize]
public class ThumbnailController : Controller {
    private readonly IShareService _shareService;
    private readonly IBrowseService _browseService;
    private readonly IImageThumbnailService _imageThumbnailService;
    private readonly ImageThumbnailer _imageThumbnailer;
    private readonly ImageLoader _imageLoader;
    private readonly IObjectDetectionService _objectDetectionService;
    private readonly FaceSquareAutoCropper _thumbnailAutoCropper;
    private readonly DirectoryThumbnailer _directoryThumbnailer;

    public ThumbnailController(ImageThumbnailer imageThumbnailer, IShareService shareService, FaceSquareAutoCropper thumbnailAutoCropper, IBrowseService browseService, IImageThumbnailService imageThumbnailService, ImageLoader imageLoader, IObjectDetectionService objectDetectionService, DirectoryThumbnailer directoryThumbnailer) {
        _imageThumbnailer = imageThumbnailer;
        _shareService = shareService;
        _thumbnailAutoCropper = thumbnailAutoCropper;
        _browseService = browseService;
        _imageThumbnailService = imageThumbnailService;
        _imageLoader = imageLoader;
        _objectDetectionService = objectDetectionService;
        _directoryThumbnailer = directoryThumbnailer;
    }

    public IActionResult Explain(string share, string path, int size = 800, int imageCount = 6) {
        if(_browseService.IsFile(share, path)) {
            return _explainSingleImage(share, path, size);
        }else if(_browseService.IsDirectory(share, path)) {
            var imagePaths = _directoryThumbnailer.FindThumbnailImages(share, path).Take(imageCount);
            return _explainImageSet(share, imagePaths);
        }

        throw new NotImplementedException();
    }

    private IActionResult _explainImageSet(string share, IEnumerable<string> paths) {
        List<ThumbnailResultViewModel> thumbnailResultViewModels = new();
        foreach(var path in paths) {
            using(var imageWrapper = _imageLoader.Load(share, path)) {
                var predictions = _objectDetectionService.GetPredictions(imageWrapper);
                var cropResult = _thumbnailAutoCropper.FindCrop(imageWrapper.Image.Width, imageWrapper.Image.Height, predictions);

                var countedPredictions = predictions.CountBy(p => p.Label);

                ThumbnailCropResultViewModel? cropResultViewModel = null;
                if(cropResult != null) {
                    var countedPredictionsInCrop = predictions
                        .Where(p => cropResult.Box.AsBox().IsOverlapping(p))
                        .CountBy(p => p.Label);
                    cropResultViewModel = new ThumbnailCropResultViewModel(_thumbnailAutoCropper.GetType().Name,
                    cropResult.Score,
                    cropResult.Box.Left,
                    cropResult.Box.Top,
                    cropResult.Box.Right,
                    cropResult.Box.Bottom,
                    countedPredictionsInCrop);
                }

                thumbnailResultViewModels.Add(new ThumbnailResultViewModel(
                    share,
                    path,
                    countedPredictions,
                    cropResultViewModel
                ));
            }
        }

        return View("ExplainImageSet", thumbnailResultViewModels.OrderByDescending(p => p.CropResult?.Score));
    }

    private IActionResult _explainSingleImage(string share, string path, int size) {
        using var imageWrapper = _imageLoader.Load(share, path);
        var image = imageWrapper.Image;
        image.ResizeImageToMaxDimension(size);

        var predictions = _objectDetectionService.GetPredictions(imageWrapper);
        var cropResult = _thumbnailAutoCropper.FindFaceSquareCrop(image.Width, image.Height, predictions);

        if(cropResult != null) {
            var pen = Pens.Solid(Color.RebeccaPurple, 4);
            image.Mutate(i => i.Draw(pen, cropResult.Box));

            foreach(var annotation in cropResult.Annotations) {
                var annotationPen = Pens.Dot(SixLabors.ImageSharp.Color.SeaGreen, 2);
                image.Mutate(i => i.Draw(annotationPen, annotation.Box.AsRectangle()));
            }

            foreach(var annotation in cropResult.Annotations) {
                image.Mutate(i => i.DrawTextWithBackground(annotation.Label, Color.Black, Color.SeaGreen, annotation.Box.Left + 5, annotation.Box.Top + 5));
            }
        }

        var cropSummaryLabel = cropResult != null ? $"Score: {cropResult.Score:0.00}" : "No Crop";
        image.Mutate(i => i.DrawTextWithBackground(cropSummaryLabel, Color.WhiteSmoke, Color.Black, 10, 10));

        var imageBytes = _imageThumbnailer.GetImageAsBytes(image);
        return File(imageBytes, "image/webp");
    }

    public IActionResult New(string share, string path, int size = 800) {
        using var imageWrapper = _imageLoader.Load(share, path);
        var image = imageWrapper.Image;

        // _thumbnailAutoCropper.CropImageToSquareAroundFace(image, annotateImage: true);
        var predictions = _objectDetectionService.GetPredictions(imageWrapper);
        var cropResult = _thumbnailAutoCropper.FindFaceSquareCrop(image.Width, image.Height, predictions);

        image.Mutate(i => i.Crop(cropResult.Box));
        image.ResizeImageToMaxDimension(size);

        var imageBytes = _imageThumbnailer.GetImageAsBytes(image);
        return File(imageBytes, "image/webp");
    }

    // public IActionResult Auto(string share, string path, int size = 800) {
    //     using var image = Image.Load<Rgb24>(_shareService.GetPath(share, path));

    //     _thumbnailAutoCropper.CropImageToSquareAroundFace(image);
    //     image.ResizeImageToMaxDimension(size);

    //     if(image != null) {
    //         var imageBytes = _imageThumbnailer.GetImageAsBytes(image);
    //         return File(imageBytes, "image/webp");
    //     } else {
    //         return NotFound();
    //     }
    // }

    // public IActionResult Portrait(string share, string path, int size = 800) {
    //     using var image = Image.Load<Rgb24>(_shareService.GetPath(share, path));

    //     var portraitCropResult = _thumbnailAutoCropper.CropImageToPortrait(new Models.ThumbnailImage(image));
    //     image.ResizeImageToMaxDimension(size);

    //     if(portraitCropResult != null) {
    //         const float fontSize = 14f;
    //         var font = _getFont(fontSize);
    //         image.Mutate(x => x.DrawTextWithBackground($"Portrait Crop [Score: {portraitCropResult.Score}]", font, Color.WhiteSmoke, Color.Black, 10, 10));
    //     }

    //     if(image != null) {
    //         var imageBytes = _imageThumbnailer.GetImageAsBytes(image);
    //         return File(imageBytes, "image/webp");
    //     } else {
    //         return NotFound();
    //     }
    // }

    private static Font _getFont(float size) {
        FontCollection fontCollection = new();
        var family = fontCollection.Add("JetBrainsMono-Regular.ttf");
        return family.CreateFont(size, FontStyle.Regular);
    }
}