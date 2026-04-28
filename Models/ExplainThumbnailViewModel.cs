namespace WebFileBrowser.Models;

public class ExplainThumbnailViewModel
{
    public ImageThumbnailCandidateSet ImageThumbnailCandidateSet {get; set;}
}

public record ThumbnailOptionViewModel(string path);

public record ThumbnailResultViewModel(string Share, string Path, IEnumerable<KeyValuePair<string, int>> PredictionCounts, ThumbnailCropResultViewModel? CropResult);

public record ThumbnailCropResultViewModel(string CropMethod, double Score, int CropLeft, int CropTop, int CropRight, int CropBottom, IEnumerable<KeyValuePair<string, int>> PredictionCountsInCrop);