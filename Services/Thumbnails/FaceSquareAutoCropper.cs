using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Drawing.Processing;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using WebFileBrowser.Extensions;
using WebFileBrowser.Models;

namespace WebFileBrowser.Services;

public class FaceSquareAutoCropper : IAutoCropper {
    private readonly ILogger<FaceSquareAutoCropper> _logger;

    public FaceSquareAutoCropper(ILogger<FaceSquareAutoCropper> logger) {
        _logger = logger;
    }

    public CropResult? FindCrop(int imageWidth, int imageHeight, IEnumerable<Prediction> predictions, Image<Rgb24> image) =>
        FindFaceSquareCrop(imageWidth, imageHeight, predictions, image, annotateImage: false);

    public CropResult? FindFaceSquareCrop(int imageWidth, int imageHeight, IEnumerable<Prediction> predictions, Image<Rgb24> image, bool annotateImage = false) {
        var allFaces = predictions.Where(p => p.ObjectClass == DetectedObjectClass.Face);

        if(!allFaces.Any()) {
            return null;
        }

        int smallestImageDimension = Math.Min(imageWidth, imageHeight);

        var largestFaceArea = allFaces.Max(p => p.Box.Area);
        var smallestFaceArea = allFaces.Min(p => p.Box.Area);
        int faceAreaThreshold = (int)Math.Floor(smallestFaceArea + (largestFaceArea - smallestFaceArea) / 2);
        faceAreaThreshold = (int)Math.Floor(faceAreaThreshold * 0.6);

        var faces = allFaces.Where(p => p.Box.Area >= faceAreaThreshold);
        var allPeople = predictions.Where(p => p.ObjectClass == DetectedObjectClass.Person);
        Dictionary<Prediction, IEnumerable<Prediction>> peopleByFace = new();
        foreach(var face in faces) {
            var overlappingPeople = predictions
                .Where(p => p.ObjectClass == DetectedObjectClass.Person && Box.AreOverlapping(face, p))
                .AsEnumerable();

            peopleByFace.Add(face, overlappingPeople);
        }

        var faceCropTargets = faces
            .Select(f => {
                var people = peopleByFace[f];

                if(people.Any()) {
                    var peopleBoundingBox = Prediction.GetBoundingBox(people);

                    var cropTargetTop = f.Box.Top;
                    if(cropTargetTop > peopleBoundingBox.Top && cropTargetTop - peopleBoundingBox.Top <= peopleBoundingBox.Height * 0.2) {
                        cropTargetTop = peopleBoundingBox.Top;
                    }

                    var cropTargetBottom = f.Box.Bottom;
                    if(f.Box.Bottom < peopleBoundingBox.Bottom && peopleBoundingBox.Bottom - f.Box.Bottom <= peopleBoundingBox.Height * 0.2) {
                        cropTargetBottom = peopleBoundingBox.Bottom;
                    }

                    var cropTargetLeft = f.Box.Left;
                    if(f.Box.Left > peopleBoundingBox.Left && f.Box.Left - peopleBoundingBox.Left <= peopleBoundingBox.Width * 0.2) {
                        cropTargetLeft = peopleBoundingBox.Left;
                    }

                    var cropTargetRight = f.Box.Right;
                    if(f.Box.Right < peopleBoundingBox.Right && peopleBoundingBox.Right - f.Box.Right <= peopleBoundingBox.Width * 0.2) {
                        cropTargetRight = peopleBoundingBox.Right;
                    }

                    return new Box(cropTargetLeft, cropTargetTop, cropTargetRight, cropTargetBottom);
                } else {
                    return f.Box;
                }
            });


        if(annotateImage) {
            var facePen = Pens.Dash(Color.HotPink, 2);
            foreach(var cropTarget in faceCropTargets) {
                image.Mutate(i => i.Draw(facePen, cropTarget.AsRectangle()));
            }

            var personPen = Pens.Dash(Color.SeaGreen, 2);
            foreach(var person in peopleByFace.SelectMany(pair => pair.Value)) {
                image.Mutate(i => i.Draw(personPen, person.Box.AsRectangle()));
            }
        }

        double score = 1;
        if(faces.Count() != predictions.Count(p => p.ObjectClass == DetectedObjectClass.Person)) {
            score *= 0.75;
        }

        int cropLeft = 0;
        int cropTop = 0;
        int cropSize = smallestImageDimension;

        var facesBoundingBox = Box.GetBoundingBox(faceCropTargets);

        if(allPeople.Any()) {
            var peopleBoundingBox = Prediction.GetBoundingBox(allPeople);

            // All detected people fit inside the crop
            if(peopleBoundingBox.Width <= cropSize && peopleBoundingBox.Height <= cropSize) {
                cropLeft = (int)Math.Floor(peopleBoundingBox.Left - ((smallestImageDimension - peopleBoundingBox.Width) / 2));
                cropTop = (int)Math.Floor(peopleBoundingBox.Top - ((smallestImageDimension - peopleBoundingBox.Height) / 2));

                score *= 1.25;

            } else {

                var greatestFaceToBodyRatio = peopleByFace.Keys
                    .Max(face => face.Box.Area / Prediction.GetBoundingBox(peopleByFace[face]).Area);

                if(imageWidth > imageHeight) {
                    if(facesBoundingBox.Width > smallestImageDimension) {
                        score *= 0.5;
                    }

                    var horizontalFacePosition = ((facesBoundingBox.Left + facesBoundingBox.Width / 2) - peopleBoundingBox.Left) / peopleBoundingBox.Width;
                    var faceMiddle = (facesBoundingBox.Left - facesBoundingBox.Width * 0.25) + facesBoundingBox.Width / 2;
                    var cropBoxOffset = cropSize * horizontalFacePosition;

                    cropLeft = (int)Math.Floor(facesBoundingBox.Left - ((smallestImageDimension - facesBoundingBox.Width) / 2));

                } else if(imageHeight > imageWidth) {
                    if(facesBoundingBox.Height > smallestImageDimension) {
                        score *= 0.5;
                    }

                    var facesMiddle = facesBoundingBox.Top + facesBoundingBox.Height / 2;

                    if(greatestFaceToBodyRatio < 0.2){
                    var verticalFacePosition = (facesMiddle - peopleBoundingBox.Top) / peopleBoundingBox.Height;
                    double facePadding = 0;
                    if(facesMiddle / imageHeight <= 0.5) {
                        facePadding = facesBoundingBox.Height * -0.25;
                    } else {
                        facePadding = facesBoundingBox.Height * 0.5;
                    }
                    var faceMiddle = (facesBoundingBox.Top + facePadding) + facesBoundingBox.Height / 2;
                    var cropBoxOffset = cropSize / 2 * verticalFacePosition;
                    cropTop = (int)Math.Floor(faceMiddle - cropBoxOffset);
                    }else{

                    var facesVerticalMiddle = (facesBoundingBox.Top + facesBoundingBox.Height / 2);
                    cropTop = (int)Math.Floor(facesVerticalMiddle - cropSize / 2);
                    }
                }

                var facesCropArea = Box.GetOverlappingArea(facesBoundingBox, new Box(cropLeft, cropTop, cropLeft + cropSize, cropTop + cropSize));
                var peopleCropArea = Box.GetOverlappingArea(peopleBoundingBox, new Box(cropLeft, cropTop, cropLeft + cropSize, cropTop + cropSize));

                if(facesCropArea / peopleCropArea >= 0.75) {
                    score *= Math.Clamp(1 - facesCropArea / peopleCropArea, 0.25, 1);
                }
            }
        } else {
            if(imageWidth > imageHeight) {
                if(facesBoundingBox.Width > smallestImageDimension) {
                    _logger.LogInformation("Crop targets are too wide for square crop");
                }

                cropLeft = (int)Math.Floor(facesBoundingBox.Left - ((smallestImageDimension - facesBoundingBox.Width) / 2));

            } else if(imageHeight > imageWidth) {
                if(facesBoundingBox.Height > smallestImageDimension) {
                    _logger.LogInformation("Crop targets are to tall for square crop");
                }

                var facesMiddle = facesBoundingBox.Top + (facesBoundingBox.Height / 2);
                cropTop = (int)Math.Floor(facesMiddle - ((smallestImageDimension - facesBoundingBox.Height) / 2));
            }
        }

        if(cropLeft + cropSize >= imageWidth) {
            cropLeft = imageWidth - cropSize;
        }

        if(cropLeft < 0) {
            cropLeft = 0;
        }

        if((cropTop + smallestImageDimension) >= imageHeight) {
            cropTop = imageHeight - cropSize;
        }

        if(cropTop < 0) {
            cropTop = 0;
        }

        var cropBox = new Box(cropLeft, cropTop, cropLeft + cropSize, cropTop + cropSize);
        if(annotateImage) {
            image.Mutate(i => i.Draw(Pens.Dot(Color.Azure, 2), cropBox.AsRectangle()));
        }
        var facesCropOverlap = Box.GetOverlappingPercentage(facesBoundingBox, cropBox);

        if(facesCropOverlap < 1) {
            score *= facesCropOverlap * 0.5;
        }

        var cropPadding = 0.05f;
        var cropBoxPadded = new Box(cropLeft + (cropSize * cropPadding), cropTop + (cropSize * cropPadding), cropLeft + cropSize * (1 - cropPadding), cropTop + cropSize * (1 - cropPadding));
        if(annotateImage) {
            image.Mutate(i => i.Draw(Pens.Dot(Color.Azure, 2), cropBoxPadded.AsRectangle()));
        }
        var facesCropPaddedOverlap = Box.GetOverlappingPercentage(facesBoundingBox, cropBoxPadded);
        if(facesCropPaddedOverlap < 1) {
            score *= facesCropPaddedOverlap;
        }

        return new CropResult(new Rectangle(cropLeft, cropTop, cropSize, cropSize), score);
    }
}