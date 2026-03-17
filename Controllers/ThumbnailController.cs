using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
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
        } else {
            resizedHeight = size;
        }
        image.Mutate(i => i.Resize(resizedWidth, resizedHeight));

        _thumbnailAutoCropper.CropImageToSquareAroundFace(image, annotateImage: true);

        if(image != null){
            var imageBytes = _imageThumbnailer.GetImageAsBytes(image);
            return File(imageBytes, "image/webp");
        } else {
            return NotFound();
        }
    }
}