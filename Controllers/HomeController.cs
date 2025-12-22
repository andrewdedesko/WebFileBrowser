using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using WebFileBrowser.Models;

namespace WebFileBrowser.Controllers;

public class HomeController : Controller
{
    public IActionResult Index()
    {
        ViewData["time"] = DateTime.Now;
        return View();
    }

    public IActionResult Image(string file)
    {
        // var image = System.IO.File.OpenRead("/run/user/1000/gvfs/smb-share:server=nas-o-matic.lan,share=andrew/Pictures/14080008.jpg");
        var imagePath = Path.Join("/run/user/1000/gvfs/smb-share:server=nas-o-matic.lan,share=andrew", file);
        var image = System.IO.File.OpenRead(imagePath);
        return File(image, "image/jpeg");
    }

    public IActionResult Privacy()
    {
        return View();
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}
