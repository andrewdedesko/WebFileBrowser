using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Caching.Distributed;
using WebFileBrowser.Models;

namespace WebFileBrowser.Services.ObjectDetection;

public class ObjectDetectionService : IObjectDetectionService {
    private readonly IEnumerable<IObjectDetector> _objectDetectors;
    private readonly IDistributedCache _cache;
    private readonly ILogger<ObjectDetectionService> _logger;

    public ObjectDetectionService(IEnumerable<IObjectDetector> objectDetectors, IDistributedCache cache, ILogger<ObjectDetectionService> logger) {
        _objectDetectors = objectDetectors;
        _cache = cache;
        _logger = logger;
    }

    public IEnumerable<Prediction> GetPredictions(IImageWrapper image) {
        var predictions = _getPredictions(image);

        // Scale predictions to image size
        foreach(var prediction in predictions) {
            prediction.Box = new Box(
                prediction.Box.Left * image.Image.Width,
                prediction.Box.Top * image.Image.Height,
                prediction.Box.Right * image.Image.Width,
                prediction.Box.Bottom * image.Image.Height
            );
        }

        return predictions;
    }

    public IEnumerable<Prediction> _getPredictions(IImageWrapper image) {
        List<Prediction> predictions = new();
        foreach(var objectDetector in _objectDetectors) {
            predictions.AddRange(_getPredictionsWithObjectDetector(objectDetector, image));
        }

        return predictions;
    }

    private IEnumerable<Prediction> _getPredictionsWithObjectDetector(IObjectDetector objectDetector, IImageWrapper image) {
        var cachedPredictions = _findCachedPredictions(objectDetector, image);
        if(cachedPredictions != null) {
            // _logger.LogInformation("Found cached predictions for detector {objectDetectorIdentifier} for {imageShare}:{imagePath} #{imageHash}: found {predictionCount} predictions",
            //     objectDetector.GetModelIdentifier(),
            //     image.Share,
            //     image.Path,
            //     image.FileHash,
            //     cachedPredictions.Count());
            return cachedPredictions;
        }

        // _logger.LogInformation("Prediction cache miss for detector {detectorIdentifier} for {imageShare}:{imagePath}",
        //     objectDetector.GetModelIdentifier(),
        //     image.Share,
        //     image.Path);

        var predictions = objectDetector.FindObjects(image.Image);
        _cachePredictions(objectDetector, image, predictions);
        return predictions;
    }

    private void _cachePredictions(IObjectDetector objectDetector, IImageWrapper image, IEnumerable<Prediction> predictions) {
        var json = JsonSerializer.Serialize(new CachedPredictions(){ Predictions = predictions });
        _cache.SetString(_cacheKey(objectDetector, image), json);
    }

    private IEnumerable<Prediction>? _findCachedPredictions(IObjectDetector objectDetector, IImageWrapper image) {
        string? result;

        // Find cache with new model key
        result = _cache.GetString(_cacheKey(objectDetector, image));
        if(result != null) {
            return JsonSerializer.Deserialize<CachedPredictions>(result)?.Predictions;
        }

        // Find cache with old model key and migrate to new key
        result = _cache.GetString(_oldCacheKey(objectDetector, image));
        if(result != null) {
            _cache.SetString(_cacheKey(objectDetector, image), result);
            _cache.Remove(_oldCacheKey(objectDetector, image));
            // _logger.LogInformation("Migrated prediction cache from {oldKey} to {newKey}", _oldCacheKey(objectDetector, image), _cacheKey(objectDetector, image));
            return JsonSerializer.Deserialize<CachedPredictions>(result)?.Predictions;
        }

        return null;
    }

    private string _cacheKey(IObjectDetector objectDetector, IImageWrapper imageWrapper) =>
        $"ImageObjectDetectionPredictionCache:{objectDetector.GetModelIdentifier()}:{imageWrapper.FileHash}";

    private string _oldCacheKey(IObjectDetector objectDetector, IImageWrapper imageWrapper) =>
        $"ImageObjectDetectionPredictionCache:{objectDetector.GetOldModelIdentifier()}:{imageWrapper.FileHash}";

    private record CachedPredictions {
        [JsonPropertyName("predictions")]
        public IEnumerable<Prediction> Predictions {get; set;}
    }
}