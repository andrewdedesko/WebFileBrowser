namespace WebFileBrowser.Models;

public class FaceDetectionResult
{
    public int FaceCount {get; set;}
    public double FaceAreaPercentage {get; set;}
    public bool FacesDetected => FaceCount > 0;
}