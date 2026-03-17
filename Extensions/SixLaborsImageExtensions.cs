using System.Runtime.CompilerServices;
using SixLabors.Fonts;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Drawing.Processing;
using SixLabors.ImageSharp.Processing;
using WebFileBrowser.Models;

namespace WebFileBrowser.Extensions;

public static class SixLaborsImageExtensions {
    public static void ResizeImageToMaxDimension(this Image image, int size) {
        var width = 0;
        var height = 0;

        if(image.Width > image.Height) {
            width = size;
        } else {
            height = size;
        }
        image.Mutate(x => x.Resize(width, height));
    }

    public static IImageProcessingContext DrawTextWithBackground(this IImageProcessingContext imageProcessingContext, string text, Font font, Color textColor, Color backgroundColor, float textX, float textY, float padding = 5f) {
        FontRectangle size = TextMeasurer.MeasureBounds(text, new TextOptions(font));
        var textLocation = new PointF(textX, textY);

        var rect = new RectangleF(
            textLocation.X - padding,
            textLocation.Y - padding,
            size.Width + (padding * 2),
            size.Height + (padding * 2));
        
        imageProcessingContext
            .BackgroundColor(backgroundColor)
            .Fill(backgroundColor, rect)
            .DrawText(text, font, textColor, textLocation);

        return imageProcessingContext;
    }

    public static Rectangle AsRectangle(this Box box) {
        return new Rectangle((int)Math.Floor(box.Left), (int)Math.Floor(box.Top), (int)Math.Floor(box.Width), (int)Math.Floor(box.Height));
    }
}