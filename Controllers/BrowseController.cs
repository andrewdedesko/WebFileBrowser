using Microsoft.AspNetCore.Mvc;
using WebFileBrowser.Models;
using WebFileBrowser.Services;

namespace WebFileBrowser.Controllers;

public class BrowseController : Controller
{
    private readonly IShareService _shareService;

    private readonly IEnumerable<string> _imageExtensions = new List<string>()
    {
        "jpg", "jpeg", "png", "webp", "gif"
    };

    public BrowseController(IShareService shareService)
    {
        _shareService = shareService;
    }

    public IActionResult Index(string share, string path)
    {
        var dirPath = Path.Join(_shareService.GetSharePath(share), path);
        var directories = System.IO.Directory.GetDirectories(dirPath);

        var directoryViewModels = directories
            .Select(d => new DirectoryViewModel()
            {
                Name = Path.GetFileName(d),
                Share = share,
                Path = Path.GetRelativePath(_shareService.GetSharePath(share), d)
            })
            .OrderBy(d => d.Name)
            .AsEnumerable();

        var files = System.IO.Directory.GetFiles(dirPath);
        var fileViewModels = files
            .Select(f => new FileViewModel()
            {
                Name = Path.GetFileName(f),
                Share = share,
                Path = Path.GetRelativePath(_shareService.GetSharePath(share), f)
            })
            .OrderBy(f => f.Name)
            .AsEnumerable();

        var directoryContainsImages = files.Any(f => IsImage(f));

        return View(new BrowseDirectoryViewModel()
        {
            Name = Path.GetFileName(dirPath),
            Share = share,
            Path = Path.GetRelativePath(_shareService.GetSharePath(share), dirPath),
            Directories = directoryViewModels,
            Files = fileViewModels,
            ShowImageGalleryView = directoryContainsImages
        });
    }

    public IActionResult ViewImages(string share, string path)
    {
        var images = System.IO.Directory.GetFiles(Path.Join(_shareService.GetSharePath(share), path))
            .Where(f => IsImage(f))
            .Select(f => new FileViewModel()
            {
                Name = Path.GetFileName(f),
                Share = share,
                Path = Path.GetRelativePath(_shareService.GetSharePath(share), f)
            })
            .OrderBy(f => f.Name)
            .AsEnumerable();

        return View(new BrowseDirectoryViewModel()
        {
            Name = Path.GetFileName(path),
            Share = share,
            Path = Path.GetRelativePath(_shareService.GetSharePath(share), path),
            Directories = Enumerable.Empty<DirectoryViewModel>(),
            Files = images
        });
    }

    public IActionResult Image(string share, string path)
    {
        var imagePath = Path.Join(_shareService.GetSharePath(share), path);
        var image = System.IO.File.OpenRead(imagePath);
        return File(image, "image/jpeg");
    }

    private bool IsImage(string filename)
    {
        var extension = Path.GetExtension(filename).ToLower();
        return IsJpgExtension(extension);
    }

    private bool IsJpgExtension(string extension) =>
        extension == ".jpg" || extension == ".jpeg";
}