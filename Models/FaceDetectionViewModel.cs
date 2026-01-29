namespace WebFileBrowser.Models;

public class GalleryFaceDetectionResultViewModel
{
    public IEnumerable<ImageFaceDetectionResult> Images {get; set;}
}

public class ImageFaceDetectionResult
{
    public required string Share {get; set;}
    public required string Path {get; set;}
    public required string Filename {get; set;}
    public int FaceCount {get; set;}

}

public class FaceBox
{
    public int Top {get; set;}
    public int Right {get; set;}
    public int Left {get; set;}
    public int Bottom {get; set;}
}