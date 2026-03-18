using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Drawing.Processing;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using WebFileBrowser.Extensions;
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

    private Box? _findPortraitCropV1(IEnumerable<Prediction> predictions, int imageWidth, int imageHeight) {
        var predList = predictions.ToList();

        // Gather face and person predictions, filtered by confidence
        var faces = predList
            .Where(p => p.ObjectClass == DetectedObjectClass.Face && p.Confidence >= 0.5f)
            .OrderByDescending(p => p.Confidence)
            .ToList();

        var people = predList
            .Where(p => p.ObjectClass == DetectedObjectClass.Person && p.Confidence >= 0.5f)
            .OrderByDescending(p => p.Confidence)
            .ToList();

        // No usable detections at all — bail out
        if(faces.Count == 0 && people.Count == 0)
            return null;

        // --- Determine the region of interest (ROI) ---
        // Strategy:
        //   1. If we have 1–2 faces, build the ROI from those faces alone.
        //      The face bounding boxes are tight, so we'll add headroom/padding below.
        //   2. If we have no faces but have a person box, fall back to the upper
        //      portion of that box (head/shoulders area).
        //   3. If we have 3+ faces (group shot), skip — no reliable single crop.

        Box roi;

        if(faces.Count >= 1 && faces.Count <= 2) {
            // Union of all face boxes
            float faceLeft = faces.Min(f => f.Box.Xmin);
            float faceTop = faces.Min(f => f.Box.Ymin);
            float faceRight = faces.Max(f => f.Box.Xmax);
            float faceBottom = faces.Max(f => f.Box.Ymax);

            float faceUnionWidth = faceRight - faceLeft;
            float faceUnionHeight = faceBottom - faceTop;

            // Add padding around the face union so the crop doesn't feel too tight.
            // For a portrait we want a little more room below the chin than above
            // the forehead, and comfortable side margins.
            float padX = faceUnionWidth * 0.5f;   // 50% of face width on each side
            float padTop = faceUnionHeight * 0.6f;   // 60% above (forehead / hair room)
            float padBottom = faceUnionHeight * 0.8f;   // 80% below (chin / neck room)

            // If we also have a person box that contains these faces, clamp the
            // bottom of the ROI to the person box so we don't bleed into another subject.
            float? personBottom = null;
            if(people.Count > 0) {
                // Find the person box most likely to belong to this face
                var matchingPerson = people
                    .Where(p =>
                        p.Box.Xmin <= faceRight && p.Box.Xmax >= faceLeft &&  // overlaps horizontally
                        p.Box.Ymin <= faceBottom)                              // starts above face bottom
                    .OrderByDescending(p => p.Confidence)
                    .FirstOrDefault();

                if(matchingPerson != null)
                    personBottom = matchingPerson.Box.Ymax;
            }

            float roiBottom = Math.Min(faceBottom + padBottom, personBottom ?? float.MaxValue);

            roi = new Box(
                xmin: faceLeft - padX,
                ymin: faceTop - padTop,
                xmax: faceRight + padX,
                ymax: roiBottom
            );
        } else if(faces.Count == 0 && people.Count > 0) {
            // No face detected — use the upper ~55% of the best person box as a
            // rough head-and-shoulders region.
            var person = people.First();
            float headHeight = person.Box.Height * 0.55f;

            roi = new Box(
                xmin: person.Box.Xmin,
                ymin: person.Box.Ymin,
                xmax: person.Box.Xmax,
                ymax: person.Box.Ymin + headHeight
            );
        } else {
            // 3+ faces (group photo) or some other unhandled case — no reliable crop
            return null;
        }

        // --- Convert the ROI into a square crop (1:1 aspect ratio) ---
        // Expand the shorter axis to match the longer one, keeping the crop centred
        // on the ROI's centre point.
        float roiWidth = roi.Xmax - roi.Xmin;
        float roiHeight = roi.Ymax - roi.Ymin;
        float side = Math.Max(roiWidth, roiHeight);

        float centerX = roi.Xmin + roiWidth / 2f;
        float centerY = roi.Ymin + roiHeight / 2f;

        // Bias the vertical centre upward slightly so the face sits in the upper-
        // middle of the square rather than dead-centre (more natural for portraits).
        centerY -= side * 0.05f;

        var squareCrop = new Box(
            xmin: centerX - side / 2f,
            ymin: centerY - side / 2f,
            xmax: centerX + side / 2f,
            ymax: centerY + side / 2f
        );

        return squareCrop;
    }

    private PortraitCropResult? _findPortraitCropV2(IEnumerable<Prediction> predictions, int imageWidth, int imageHeight) {
        var predList = predictions.ToList();

        var faces = predList
            .Where(p => p.ObjectClass == DetectedObjectClass.Face && p.Confidence >= 0.5f)
            .OrderByDescending(p => p.Confidence)
            .ToList();

        var people = predList
            .Where(p => p.ObjectClass == DetectedObjectClass.Person && p.Confidence >= 0.5f)
            .OrderByDescending(p => p.Confidence)
            .ToList();

        if(faces.Count == 0 && people.Count == 0)
            return null;

        // -------------------------------------------------------------------------
        // 1. Build a region of interest (ROI)
        // -------------------------------------------------------------------------

        Box roi;
        float baseScore;

        if(faces.Count >= 1 && faces.Count <= 2) {
            float faceLeft = faces.Min(f => f.Box.Xmin);
            float faceTop = faces.Min(f => f.Box.Ymin);
            float faceRight = faces.Max(f => f.Box.Xmax);
            float faceBottom = faces.Max(f => f.Box.Ymax);

            float faceUnionWidth = faceRight - faceLeft;
            float faceUnionHeight = faceBottom - faceTop;

            float padX = faceUnionWidth * 0.5f;
            float padTop = faceUnionHeight * 0.6f;
            float padBottom = faceUnionHeight * 0.8f;

            float? personBottom = null;
            if(people.Count > 0) {
                var matchingPerson = people
                    .Where(p =>
                        p.Box.Xmin <= faceRight && p.Box.Xmax >= faceLeft &&
                        p.Box.Ymin <= faceBottom)
                    .OrderByDescending(p => p.Confidence)
                    .FirstOrDefault();

                if(matchingPerson != null)
                    personBottom = matchingPerson.Box.Ymax;
            }

            float roiBottom = Math.Min(faceBottom + padBottom, personBottom ?? float.MaxValue);

            roi = new Box(
                xmin: faceLeft - padX,
                ymin: faceTop - padTop,
                xmax: faceRight + padX,
                ymax: roiBottom
            );

            // Score: average face confidence, slightly discounted for two faces
            // since a two-face crop is inherently less clean than a solo portrait
            float avgFaceConfidence = faces.Average(f => f.Confidence);
            baseScore = faces.Count == 1
                ? avgFaceConfidence
                : avgFaceConfidence * 0.85f;
        } else if(faces.Count == 0 && people.Count > 0) {
            var person = people.First();
            float headHeight = person.Box.Height * 0.55f;

            roi = new Box(
                xmin: person.Box.Xmin,
                ymin: person.Box.Ymin,
                xmax: person.Box.Xmax,
                ymax: person.Box.Ymin + headHeight
            );

            // No face detected — inherently lower confidence in the crop quality
            baseScore = person.Confidence * 0.5f;
        } else {
            // 3+ faces
            return null;
        }

        // -------------------------------------------------------------------------
        // 2. Expand ROI to a square
        // -------------------------------------------------------------------------

        float roiWidth = roi.Xmax - roi.Xmin;
        float roiHeight = roi.Ymax - roi.Ymin;
        float side = Math.Max(roiWidth, roiHeight);

        side = Math.Min(side, Math.Min(imageWidth, imageHeight));

        float centerX = roi.Xmin + roiWidth / 2f;
        float centerY = roi.Ymin + roiHeight / 2f;
        centerY -= side * 0.05f;

        float x1 = centerX - side / 2f;
        float y1 = centerY - side / 2f;
        float x2 = x1 + side;
        float y2 = y1 + side;

        // --- Zoom ---
        // The ROI was padded generously to give context. Now shrink it back toward
        // the face(s) so they fill the frame more. We target a zoom such that the
        // face union occupies roughly 45% of the crop's side length — a natural
        // portrait framing. We never zoom beyond that target, and we never produce
        // a crop smaller than the face union itself (with a small safety margin).
        if(faces.Count > 0) {
            float faceUnionWidth = faces.Max(f => f.Box.Xmax) - faces.Min(f => f.Box.Xmin);
            float faceUnionHeight = faces.Max(f => f.Box.Ymax) - faces.Min(f => f.Box.Ymin);
            float faceUnionSize = Math.Max(faceUnionWidth, faceUnionHeight);

            // How large should the square be so the face union is 45% of it?
            const float targetFaceFraction = 0.45f;
            float zoomedSide = faceUnionSize / targetFaceFraction;

            // Only zoom in (shrink the crop), never zoom out beyond the padded ROI
            // Also never go smaller than the face union + 20% safety margin
            float minSide = faceUnionSize * 1.2f;
            side = Math.Clamp(zoomedSide, minSide, side);

            side = Math.Min(side, Math.Min(imageWidth, imageHeight));
        }

        // -------------------------------------------------------------------------
        // 3. Slide the square inside the image bounds, measuring how much it had
        //    to move so we can penalise heavily edge-clamped crops
        // -------------------------------------------------------------------------

        float preClampX1 = x1;
        float preClampY1 = y1;

        if(x1 < 0) { x2 -= x1; x1 = 0; } else if(x2 > imageWidth) { x1 -= (x2 - imageWidth); x2 = imageWidth; }

        if(y1 < 0) { y2 -= y1; y1 = 0; } else if(y2 > imageHeight) { y1 -= (y2 - imageHeight); y2 = imageHeight; }

        // -------------------------------------------------------------------------
        // 4. Score penalty for edge clamping
        //    Expressed as a fraction of the square's side length that was shifted.
        //    A full-side shift would be a penalty of 1.0 (worst possible).
        // -------------------------------------------------------------------------

        float shiftX = Math.Abs(x1 - preClampX1);
        float shiftY = Math.Abs(y1 - preClampY1);
        float clampPenalty = Math.Min(1f, (shiftX + shiftY) / side);

        float finalScore = baseScore * (1f - clampPenalty * 0.6f);
        finalScore = Math.Clamp(finalScore, 0f, 1f);

        var box = new Box(xmin: x1, ymin: y1, xmax: x2, ymax: y2);
        return new PortraitCropResult(box, finalScore);
    }

    public PortraitCropResult? CropImageToPortrait(ThumbnailImage thumbnailImage) {
        Image<Rgb24> image = thumbnailImage.Image;
        List<Prediction> predictions = new();
        foreach(var detector in _objectDetectors) {
            predictions.AddRange(detector.FindObjects(image));
        }

        var cropResult = _findPortraitCropV2(predictions, image.Width, image.Height);
        if(cropResult?.Box != null) {
            image.Mutate(i => i.Crop(cropResult.Box.AsRectangle()));
        }

        return cropResult;
    }

    public void CropImageToSquareAroundFace(ThumbnailImage thumbnailImage, bool annotateImage = false) {
        CropImageToSquareAroundFace(thumbnailImage.Image, annotateImage: annotateImage);
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

public record PortraitCropResult(Box Box, float Score);