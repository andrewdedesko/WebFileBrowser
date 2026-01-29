namespace WebFileBrowser;

public class ImageThumbnailCandidateSet
{
    public IEnumerable<ImageThumbnailCandidate> Candidates {get; set;}
    public double LowerFaceAreaThreshold {get; set;}
    public double UpperFaceAreaThreshold {get; set;}
    public double ConfidenceThreshold {get; set;}
}

public class ImageThumbnailCandidate
{
    public required string Share {get; set;}
    public required string Path {get; set;}
    public required string Filename {get; set;}
    public int FileSystemOrder { get; set; }
    public int FaceCount {get; set;}
    public int OpenEyesCount {get; set;}
    public int ClosedEyesCount {get; set;}
    public double OpenEyesPercentage {get; set;}
    public double Confidence {get; set;}
    public double FaceAreaPercentage {get; set;}
    public bool TopPick {get; set;}
}