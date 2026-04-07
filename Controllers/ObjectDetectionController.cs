using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SixLabors.Fonts;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Drawing.Processing;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using WebFileBrowser.Extensions;
using WebFileBrowser.Models;
using WebFileBrowser.Services;
using WebFileBrowser.Services.ObjectDetection;

namespace WebFileBrowser.Controllers;

[Authorize]
public class ObjectDetectionController : Controller {
    private readonly ImageLoader _imageLoader;
    private readonly IObjectDetectionService _objectDetectionService;

    public ObjectDetectionController(IObjectDetectionService objectDetectionService, ImageLoader imageLoader) {
        _objectDetectionService = objectDetectionService;
        _imageLoader = imageLoader;
    }

    public IActionResult Detect(string share, string path) {
        var faceDetector = new SharpAiFaceDetector();
        using(ImageWrapper sourceImage = _imageLoader.Load(share, path)) {
            sourceImage.Image.ResizeImageToMaxDimension(1024);

            var predictions = _objectDetectionService.GetPredictions(sourceImage);

            const float fontSize = 14f;
            var font = _getFont(fontSize);

            Dictionary<DetectedObjectClass, Color> classColors = new();
            classColors.Add(DetectedObjectClass.Face, Color.SeaGreen);
            classColors.Add(DetectedObjectClass.Person, Color.SeaGreen);
            classColors.Add(DetectedObjectClass.Animal, Color.ForestGreen);
            classColors.Add(DetectedObjectClass.Furniture, Color.Orange);

            // var foundObjects = predictions.Select(p => $"{p.Label}: {p.Confidence}").ToArray();
            foreach(var p in predictions) {
                var labelClass = p.Label;
                Color color;
                if(classColors.ContainsKey(p.ObjectClass)) {
                    color = classColors[p.ObjectClass];
                } else {
                    color = Color.DarkSlateBlue;
                }
                var pen = Pens.Solid(color, 4);

                // sourceImage.Mutate(i => i.DrawText(label, font, color, new PointF(p.Box.Xmin, p.Box.Ymin)));
                sourceImage.Image.Mutate(i => i.Draw(pen, new RectangleF(p.Box.Left, p.Box.Top, p.Box.Width, p.Box.Height)));
            }

            foreach(var p in predictions) {
                var labelClass = p.Label;
                Color color;
                if(classColors.ContainsKey(p.ObjectClass)) {
                    color = classColors[p.ObjectClass];
                } else {
                    color = Color.DarkSlateBlue;
                }
                var pen = Pens.Solid(color, 4);
                
                var label = $"{labelClass} ({p.Confidence:0.000})";

                FontRectangle size = TextMeasurer.MeasureBounds(label, new TextOptions(font));
                var textLocation = new PointF(p.Box.Left, p.Box.Top);
                var padding = 5f;

                var rect = new RectangleF(
                    textLocation.X - padding,
                    textLocation.Y - padding,
                    size.Width + (padding * 2),
                    size.Height + (padding * 2));

                sourceImage.Image.Mutate(x => x
                    .BackgroundColor(color)
                    .Fill(color, rect) // Draw blue background
                    .DrawText(label, font, Color.White, textLocation) // Draw white text
                );
            }

            // return Ok(string.Join(", ", foundObjects));
            return File(_GetImageAsWebpBytes(sourceImage.Image), "image/webp");
        }
    }

    private byte[] _GetImageAsWebpBytes(Image image) {
        var thumbnailImageStream = new MemoryStream();
        image.SaveAsWebp(thumbnailImageStream, new SixLabors.ImageSharp.Formats.Webp.WebpEncoder() {
            FileFormat = SixLabors.ImageSharp.Formats.Webp.WebpFileFormatType.Lossy,
            Quality = 100
        });

        var thumbnailData = thumbnailImageStream.ToArray();
        return thumbnailData;
    }

    private static Font _getFont(float size) {
        FontCollection fontCollection = new();
        var family = fontCollection.Add("JetBrainsMono-Regular.ttf");
        return family.CreateFont(size, FontStyle.Regular);
    }
}