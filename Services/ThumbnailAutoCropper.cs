using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Drawing.Processing;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using WebFileBrowser.Models;
using WebFileBrowser.Services.ObjectDetection;

namespace WebFileBrowser.Services;

public class ThumbnailAutoCropper {
    private readonly IEnumerable<IObjectDetector> _objectDetectors;
    private readonly ILogger<ThumbnailAutoCropper> _logger;

    public ThumbnailAutoCropper(IEnumerable<IObjectDetector> objectDetectors, ILogger<ThumbnailAutoCropper> logger) {
        _objectDetectors = objectDetectors;
        _logger = logger;
    }

    private IEnumerable<Box> _getHumanCropTargets(IEnumerable<Prediction> predictions) {
        var faces = predictions.Where(p => p.ObjectClass == DetectedObjectClass.Face);
        var people = predictions.Where(p => p.ObjectClass == DetectedObjectClass.Person);

        if(predictions.Any(p => p.ObjectClass == DetectedObjectClass.Face)) {
            List<List<Box>> faceTargets = new();

            var facesByArea = faces
                    .OrderByDescending(f => _getArea(f));

            int faceAreaThreshold = 0;
            if(faces.Count() > 1) {
                var largestFaceArea = _getArea(facesByArea.First());
                var smallestFaceArea = _getArea(facesByArea.Last());

                faceAreaThreshold = (int)Math.Floor(smallestFaceArea + (largestFaceArea - smallestFaceArea) / 2);
                faceAreaThreshold = (int)Math.Floor(faceAreaThreshold * 0.75);
            }

            var facesInPeopleBoundaries = faces
                .Where(f => _getArea(f) >= faceAreaThreshold)
                .Where(f => people.Any(p => _areOverlapping(f, p)));

            if(facesInPeopleBoundaries.Any()) {
                return facesInPeopleBoundaries
                    .Select(f => f.Box)
                    .AsEnumerable();
            }

            return faces
                .Where(f => _getArea(f) >= faceAreaThreshold)
                .Select(f => f.Box)
                .AsEnumerable();
        }

        if(people.Any()) {
            return people.Select(p => p.Box).AsEnumerable();
        }

        return Enumerable.Empty<Box>();
    }

    public void CropImageToSquareAroundFace(Image<Rgb24> srcImage, bool annotateImage = false) {
        List<Prediction> predictions = new();
        foreach(var detector in _objectDetectors) {
            predictions.AddRange(detector.FindObjects(srcImage));
        }

        IEnumerable<Box> cropTargets = _getHumanCropTargets(predictions);

        var srcWidth = srcImage.Bounds.Width;
        var srcHeight = srcImage.Bounds.Height;

        // Annotate faces
        if(annotateImage) {
            var pen = Pens.Solid(Color.SeaGreen, 2);
            var predictionPen = Pens.Dot(Color.Yellow, 2);

            foreach(var prediction in predictions) {
                if(!cropTargets.Any(t => t == prediction.Box)) {
                    srcImage.Mutate(i => i.Draw(predictionPen, _rectangle(prediction.Box)));
                }
            }

            foreach(var box in cropTargets) {
                srcImage.Mutate(i => i.Draw(pen, _rectangle(box)));
            }
        }

        // Crop to square around face
        if(cropTargets.Any()) {
            int _mostLeftFace = (int)Math.Floor(cropTargets.Select(f => f.Left).Order().First());
            int _mostRightFace = (int)Math.Floor(cropTargets.Select(f => f.Right).OrderDescending().First());
            int _mostTopFace = (int)Math.Floor(cropTargets.Select(f => f.Top).Order().First());
            int _mostBottomFace = (int)Math.Floor(cropTargets.Select(f => f.Bottom).OrderDescending().First());
            int boundingBoxWidth = _mostRightFace - _mostLeftFace;
            int boundingBoxHeight = _mostBottomFace - _mostTopFace;

            double imageMarginLower = 0.33;
            double imageMarginUpper = 0.66;
            if(cropTargets.Count() > 1) {
                imageMarginLower = 0.2;
                imageMarginUpper = 0.8;
            }

            int faceMargin = 0;

            int srcFaceLeft = _mostLeftFace - faceMargin;
            int srcFaceRight = _mostRightFace + faceMargin;
            int srcFaceTop = _mostTopFace - faceMargin;
            int srcFaceBottom = _mostBottomFace + faceMargin;
            var srcFaceWidth = srcFaceRight - srcFaceLeft;
            var srcFaceHeight = srcFaceBottom - srcFaceTop;

            if(annotateImage) {
                var pen = Pens.Dash(Color.HotPink, 2);
                srcImage.Mutate(i => i.Draw(pen, new Rectangle(srcFaceLeft, srcFaceTop, srcFaceWidth, srcFaceHeight)));
            }

            var smallestDimension = Math.Min(srcWidth, srcHeight);
            if(smallestDimension < boundingBoxWidth || smallestDimension < boundingBoxHeight) {
                return;
            }

            var cropLeft = 0;
            if(srcWidth > smallestDimension) {
                int srcFaceCentre = srcFaceLeft + srcFaceWidth / 2;
                double srcFaceCentrePercentage = (double)srcFaceCentre / srcWidth;

                srcFaceCentrePercentage = Math.Clamp(srcFaceCentrePercentage, imageMarginLower, imageMarginUpper);

                int cropFaceCentre = (int)Math.Floor(smallestDimension * srcFaceCentrePercentage);
                cropLeft = srcFaceCentre - cropFaceCentre;

                if(cropLeft < 0) {
                    cropLeft = 0;
                }

                if(cropLeft + smallestDimension > srcWidth) {
                    cropLeft = srcWidth - smallestDimension;
                }
            }

            var cropTop = 0;
            if(srcHeight > smallestDimension) {
                int srcFaceCentre = srcFaceTop + srcFaceHeight / 2;
                double srcFaceCentrePercentage = (double)srcFaceCentre / srcHeight;

                srcFaceCentrePercentage = Math.Clamp(srcFaceCentrePercentage, imageMarginLower, imageMarginUpper);

                int cropFaceCentre = (int)Math.Floor(smallestDimension * srcFaceCentrePercentage);
                cropTop = srcFaceCentre - cropFaceCentre;
                if(cropTop < 0) {
                    cropTop = 0;
                }

                if(cropTop + smallestDimension > srcHeight) {
                    cropTop = srcHeight - smallestDimension;
                }
            }

            var cropRectangle = new Rectangle(cropLeft, cropTop, smallestDimension, smallestDimension);

            if(annotateImage) {
                var cropPen = Pens.Dash(Color.MediumVioletRed, 2);
                srcImage.Mutate(i => i.Draw(cropPen, cropRectangle));
            } else {
                srcImage.Mutate(i => i.Crop(cropRectangle));
            }
        }
    }

    private static float _getArea(Box box) =>
        box.Width * box.Height;

    private static float _getArea(Prediction prediction) =>
        _getArea(prediction.Box);

    private static RectangleF _rectangle(Box box) =>
        new RectangleF(box.Left, box.Top, box.Width, box.Height);

    private static bool _areOverlapping(Prediction a, Prediction b) {
        return _getOverlappingPercentage(a.Box, b.Box) >= 0.75;
    }

    private static float _getOverlappingPercentage(Box a, Box b) {
        var overlappingArea = _getOverlappingArea(a, b);
        var smallestArea = Math.Min(_getArea(a), _getArea(b));
        return overlappingArea / smallestArea;
    }

    private static float _getOverlappingArea(Box a, Box b) {
        float left = Math.Max(a.Left, b.Left);
        float right = Math.Min(a.Right, b.Right);

        float top = Math.Max(a.Top, b.Top);
        float bottom = Math.Min(a.Bottom, b.Bottom);

        return Math.Max(0, right - left) * Math.Max(0, bottom - top);
    }

}