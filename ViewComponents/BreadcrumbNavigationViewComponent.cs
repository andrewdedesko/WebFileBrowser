using Microsoft.AspNetCore.Mvc;
using WebFileBrowser.Models;
using WebFileBrowser.Services;

namespace WebFileBrowser.ViewComponents;

public class BreadcrumbNavigationViewComponent : ViewComponent {
    private readonly IShareService _shareService;

    public BreadcrumbNavigationViewComponent(IShareService shareService) {
        _shareService = shareService;
    }

    public async Task<IViewComponentResult> InvokeAsync(string share, string path) {
        IList<PathViewModel> pathComponents = new List<PathViewModel>();
        var shareDir = new DirectoryInfo(_shareService.GetSharePath(share));
        var dirPath = Path.Join(_shareService.GetSharePath(share), path);
        var dir = new DirectoryInfo(dirPath);
        while(dir != null && dir.FullName != shareDir.FullName) {
            var p = new PathViewModel() {
                Name = dir.Name,
                Path = Path.GetRelativePath(shareDir.FullName, dir.FullName)
            };
            pathComponents.Insert(0, p);
            dir = dir.Parent;
        }
        
        var data = new ShareBreadcrumbNavigationModel() {
            Share = share,
            CurrentPath = path,
            PathComponents = pathComponents
        };
        return View(data);
    }
}