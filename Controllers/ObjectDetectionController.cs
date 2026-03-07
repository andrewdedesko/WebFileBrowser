using Microsoft.AspNetCore.Mvc;
using SixLabors.Fonts;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Drawing.Processing;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using WebFileBrowser.Models;
using WebFileBrowser.Services;
using WebFileBrowser.Services.ObjectDetection;

namespace WebFileBrowser.Controllers;

public class ObjectDetectionController : Controller {
    private readonly IShareService _shareService;
    private readonly IEnumerable<IObjectDetector> _customObjectDetectors;

    public ObjectDetectionController(IEnumerable<IObjectDetector> customObjectDetectors, IShareService shareService) {
        _customObjectDetectors = customObjectDetectors;
        _shareService = shareService;
    }

    public IActionResult Detect(string share, string path) {
        var faceDetector = new SharpAiFaceDetector();
        using(Image<Rgb24> sourceImage = Image.Load<Rgb24>(_shareService.GetPath(share, path))) {
            int newWidth = 0;
            int newHeight = 0;
            if(sourceImage.Width >= sourceImage.Height) {
                newWidth = 1024;
            } else {
                newHeight = 1024;
            }
            sourceImage.Mutate(i => i.Resize(newWidth, newHeight));

            List<Prediction> predictions = new();
            foreach(var detector in _customObjectDetectors){
                predictions.AddRange(detector.FindObjects(sourceImage));
            }

            const string TextFont = "JetBrains Mono";
            const float fontSize = 14f;

            // foreach(var fam in SystemFonts.Families) {
            //     System.Console.WriteLine(fam.Name);
            // }
            FontFamily fontFamily;
            if(!SystemFonts.TryGet(TextFont, out fontFamily))
                throw new Exception($"Couldn't find font {TextFont}");

            var font = fontFamily.CreateFont(fontSize, FontStyle.Regular);

            Dictionary<string, Color> classColors = new();
            classColors.Add("face", Color.SeaGreen);
            classColors.Add("person", Color.SeaGreen);
            classColors.Add("head_neck_face", Color.SeaGreen);
            classColors.Add("head_neck_face_collared", Color.HotPink);
            classColors.Add("collar", Color.MediumVioletRed);
            classColors.Add("ball_gag", Color.MediumVioletRed);
            classColors.Add("harness_gag", Color.DarkRed);


            var foundObjects = predictions.Select(p => $"{p.Label}: {p.Confidence}").ToArray();
            foreach(var p in predictions) {
                var labelClass = p.Label;
                Color color;
                if(classColors.ContainsKey(labelClass)) {
                    color = classColors[labelClass];
                } else {
                    color = Color.Green;
                }
                var pen = Pens.Solid(color, 4);

                var label = $"{labelClass} ({p.Confidence})";

                FontRectangle size = TextMeasurer.MeasureBounds(label, new TextOptions(font));
                var textLocation = new PointF(p.Box.Left, p.Box.Top);
                var padding = 5f;

                var rect = new RectangleF(
                    textLocation.X - padding,
                    textLocation.Y - padding,
                    size.Width + (padding * 2),
                    size.Height + (padding * 2));


                // sourceImage.Mutate(i => i.DrawText(label, font, color, new PointF(p.Box.Xmin, p.Box.Ymin)));
                sourceImage.Mutate(i => i.Draw(pen, new RectangleF(p.Box.Left, p.Box.Top, p.Box.Width, p.Box.Height)));
                sourceImage.Mutate(x => x
        .BackgroundColor(color)
        .Fill(color, rect) // Draw blue background
        .DrawText(label, font, Color.White, textLocation) // Draw white text
        );
            }

            // return Ok(string.Join(", ", foundObjects));
            return File(_GetImageAsWebpBytes(sourceImage), "image/webp");
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
}