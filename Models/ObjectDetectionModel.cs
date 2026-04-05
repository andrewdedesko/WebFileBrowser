namespace WebFileBrowser.Models;

public class Prediction {
    public Box Box { get; set; }
    public string Label { get; set; }
    public DetectedObjectClass ObjectClass {get; set;}
    public float Confidence { get; set; }
}

public enum DetectedObjectClass {
    Unknown,
    Face,
    Person,
    Animal,
    Furniture
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
}

public record LabelClass(string Name, DetectedObjectClass DetectedObjectClass) {
}