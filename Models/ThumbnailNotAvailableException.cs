namespace WebFileBrowser.Models;

public class ThumbnailNotAvailableException : Exception {
    public ThumbnailNotAvailableException() : base("Could not find a thumbnail") { }

    public ThumbnailNotAvailableException(string message) : base(message) { }
}