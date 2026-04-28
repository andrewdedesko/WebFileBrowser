namespace WebFileBrowser.Models;

public class Prediction {
    public Box Box { get; set; }
    public string Label { get; set; }
    public DetectedObjectClass ObjectClass {get; set;}
    public float Confidence { get; set; }

    public static Box GetBoundingBox(IEnumerable<Prediction> predictions) =>
        Box.GetBoundingBox(predictions.Select(p => p.Box));
}

public enum DetectedObjectClass {
    Unknown,
    Face,
    Person,
    Animal,
    Furniture
}

public record BoxI(int Left, int Top, int Right, int Bottom) {
    public int Width => Right - Left;
    public int Height => Bottom - Top;
}

public record Box {
    public float Xmin { get; init; }
    public float Ymin { get; init; }
    public float Xmax { get; init; }
    public float Ymax { get; init; }

    public Box(float xmin, float ymin, float xmax, float ymax) {
        Xmin = xmin;
        Ymin = ymin;
        Xmax = xmax;
        Ymax = ymax;
    }

    public float Left => Xmin;
    public float Right => Xmax;

    public float Top => Ymin;
    public float Bottom => Ymax;

    public float Width => Xmax - Xmin;
    public float Height => Ymax - Ymin;

    public float Area => Width * Height;

    public bool IsOverlapping(Box otherBox) =>
        AreOverlapping(this, otherBox);
    
    public bool IsOverlapping(Prediction prediction) =>
        IsOverlapping(prediction.Box);

    public static bool AreOverlapping(Prediction a, Prediction b) =>
        AreOverlapping(a.Box, b.Box);

    public static bool AreOverlapping(Box a, Box b) =>
        GetOverlappingPercentage(a, b) >= 0.75;

    public static float GetOverlappingPercentage(Box a, Box b) {
        var overlappingArea = GetOverlappingArea(a, b);
        var smallestArea = Math.Min(a.Area, b.Area);
        return overlappingArea / smallestArea;
    }

    public static float GetOverlappingArea(Box a, Box b) {
        float left = Math.Max(a.Left, b.Left);
        float right = Math.Min(a.Right, b.Right);

        float top = Math.Max(a.Top, b.Top);
        float bottom = Math.Min(a.Bottom, b.Bottom);

        return Math.Max(0, right - left) * Math.Max(0, bottom - top);
    }

    public static Box GetBoundingBox(IEnumerable<Box> boxes) {
        if(!boxes.Any()) {
            throw new ArgumentException("boxes cannot be empty");
        }

        var left = boxes.Min(b => b.Left);
        var right = boxes.Max(b => b.Right);
        var top = boxes.Min(b => b.Top);
        var bottom = boxes.Max(b => b.Bottom);

        return new Box(left, top, right, bottom);
    }
}

public record LabelClass(string Name, DetectedObjectClass DetectedObjectClass) {
}