namespace WebFileBrowser.Models;

class DirectoryViewModel
{
    public string Name {get; set;}
    public string Share {get; set;}
    public string Path {get; set;}
}

class FileViewModel
{
    public string Name {get; set;}
    public string Share {get; set;}
    public string Path {get; set;}
}

class BrowseDirectoryViewModel
{
    public string Name {get; set;}
    public string Share {get; set;}
    public string Path {get; set;}
    public IEnumerable<DirectoryViewModel> Directories {get; set;}
    public IEnumerable<FileViewModel> Files {get; set;}
}