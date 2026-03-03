using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using WebFileBrowser.Services;

namespace WebFileBrowser.Controllers;

[Authorize]
public class ThumbnailController : Controller {
    private readonly IShareService _shareService;
    private readonly ImageThumbnailer _imageThumbnailer;

    public ThumbnailController(ImageThumbnailer imageThumbnailer, IShareService shareService) {
        _imageThumbnailer = imageThumbnailer;
        _shareService = shareService;
    }

    public IActionResult Index(string share, string path, int size = 800) {
        // var image = _imageThumbnailer.GetDirectoryThumbnailImageFromMiddleImageAndPreferImagesWithFaces(share, path, size, annotateImage: true);

        using var image = Image.Load<Rgb24>(_shareService.GetPath(share, path));
        _imageThumbnailer.CropImageToSquareAroundFace(image, annotateImage: true);

        if(image != null){
            var imageBytes = _imageThumbnailer.GetImageAsBytes(image);
            return File(imageBytes, "image/webp");
        } else {
            return NotFound();
        }
    }
}