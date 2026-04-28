namespace WebFileBrowser.Models;

class DirectoryViewModel
{
    public required string Name {get; set;}
    public required string Share {get; set;}
    public string? Path {get; set;}
    public string? ViewType {get; set;}
}

class FileViewModel
{
    public required string Name {get; set;}
    public required string Share {get; set;}
    public required string Path {get; set;}
    public bool IsImage {get; set;}
    public bool IsVideo {get; set;}
}

class PathViewModel
{
    public required string Name {get; set;}
    public string? Path {get; set;}
}

record ShareBreadcrumbNavigationModel {
    public required string Share {get; init;}
    public required string CurrentPath {get; init;}
    public required IEnumerable<PathViewModel> PathComponents {get; init;}
}

class BrowseDirectoryViewModel
{
    public required string Name {get; set;}
    public required string Share {get; set;}
    public required string Path {get; set;}
    public required IEnumerable<PathViewModel> PathComponents {get; set;}
    public required IEnumerable<DirectoryViewModel> Directories {get; set;}
    public required IEnumerable<FileViewModel> Files {get; set;}

    public required string ViewType {get; set;}
    public bool ShowImageGalleryView {get; set;}
}