namespace WebFileBrowser.Services;

public interface IImageThumbnailService
{
    public byte[] GetImageThumbnail(string share, string path);
    public ImageThumbnailCandidateSet GetImageThumbnailCandidates(string share, string path);
    public ImageThumbnailCandidate PickThumbnailCandidate(ImageThumbnailCandidateSet candidateSet);
}