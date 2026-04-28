using System.Drawing;

namespace WebFileBrowser.Models;

public record Annotation(BoxI Box, string Label, Color Color);

public class AnnotationCollection {
    private readonly IList<Annotation> _annotations = new List<Annotation>();

    public void Add(Box box, string label, Color color) =>
        Add(box.AsBoxI(), label, color);

    public void Add(BoxI box, string label, Color color) {
        _annotations.Add(new Annotation(box, label, color));
    }

    public IEnumerable<Annotation> AsEnumerable() =>
        _annotations.AsEnumerable();
}