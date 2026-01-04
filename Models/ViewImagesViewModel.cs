namespace WebFileBrowser.Models;

class ViewImagesViewModel
{
    public string Name {get; set;}
    public string Share {get; set;}
    public string Path {get; set;}
    public IEnumerable<PathViewModel> PathComponents {get; set;}

    public string? StartingImageName {get; set;}
    public IEnumerable<FileViewModel> Files {get; set;}
}