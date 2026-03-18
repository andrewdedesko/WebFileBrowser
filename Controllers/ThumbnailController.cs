using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SixLabors.Fonts;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using WebFileBrowser.Extensions;
using WebFileBrowser.Services;

namespace WebFileBrowser.Controllers;

[Authorize]
public class ThumbnailController : Controller {
    private readonly IShareService _shareService;
    private readonly ImageThumbnailer _imageThumbnailer;
    private readonly ThumbnailAutoCropper _thumbnailAutoCropper;

    public ThumbnailController(ImageThumbnailer imageThumbnailer, IShareService shareService, ThumbnailAutoCropper thumbnailAutoCropper) {
        _imageThumbnailer = imageThumbnailer;
        _shareService = shareService;
        _thumbnailAutoCropper = thumbnailAutoCropper;
    }

    public IActionResult Index(string share, string path, int size = 800) {
        using var image = Image.Load<Rgb24>(_shareService.GetPath(share, path));
        image.ResizeImageToMaxDimension(size);

        _thumbnailAutoCropper.CropImageToSquareAroundFace(image, annotateImage: true);

        var imageBytes = _imageThumbnailer.GetImageAsBytes(image);
        return File(imageBytes, "image/webp");
    }

    public IActionResult Auto(string share, string path, int size = 800) {
        using var image = Image.Load<Rgb24>(_shareService.GetPath(share, path));

        _thumbnailAutoCropper.CropImageToSquareAroundFace(image);
        image.ResizeImageToMaxDimension(size);

        if(image != null) {
            var imageBytes = _imageThumbnailer.GetImageAsBytes(image);
            return File(imageBytes, "image/webp");
        } else {
            return NotFound();
        }
    }

    public IActionResult Portrait(string share, string path, int size = 800) {
        using var image = Image.Load<Rgb24>(_shareService.GetPath(share, path));

        var portraitCropResult = _thumbnailAutoCropper.CropImageToPortrait(new Models.ThumbnailImage(image));
        image.ResizeImageToMaxDimension(size);

        if(portraitCropResult != null) {
            const float fontSize = 14f;
            var font = _getFont(fontSize);
            image.Mutate(x => x.DrawTextWithBackground($"Portrait Crop [Score: {portraitCropResult.Score}]", font, Color.WhiteSmoke, Color.Black, 10, 10));
        }

        if(image != null) {
            var imageBytes = _imageThumbnailer.GetImageAsBytes(image);
            return File(imageBytes, "image/webp");
        } else {
            return NotFound();
        }
    }

    private static Font _getFont(float size) {
        FontCollection fontCollection = new();
        var family = fontCollection.Add("JetBrainsMono-Regular.ttf");
        return family.CreateFont(size, FontStyle.Regular);
    }
}