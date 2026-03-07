using WebFileBrowser.Models;

namespace WebFileBrowser.Services.ObjectDetection;

public class YoloCocoObjectDetector : OnnxObjectDetector {
    public YoloCocoObjectDetector(ILogger<YoloCocoObjectDetector> logger) : base(
        "Onnx/yolo26s-coco.onnx",
        new Dictionary<int, LabelClass>{
            [0] = new LabelClass("person", DetectedObjectClass.Person),
            [13] = new LabelClass("bench", DetectedObjectClass.Unknown),
            [56] = new LabelClass("chair", DetectedObjectClass.Unknown),
            [57] = new LabelClass("couch", DetectedObjectClass.Unknown),
            [58] = new LabelClass("potted plant", DetectedObjectClass.Unknown),
            [59] = new LabelClass("bed", DetectedObjectClass.Unknown),
            [76] = new LabelClass("scissors", DetectedObjectClass.Unknown)
        },
        logger
    ){}
}