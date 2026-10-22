using ADOFAI;
using DG.Tweening;
using UnityEngine;

namespace RDWindowEvent
{
    // 运行时效果：在事件时间点把参数交给窗口控制器。
    public class WindowMoveEffect : ffxPlusBase
    {
        public WindowDancePreset preset;
        public SamePresetBehavior samePreset;
        public Vector2 position;
        public ReferenceType reference;
        public float speed;
        public bool useCircle;
        public float amplitude;
        public Vector2 amplitudeVector;
        public float angle;
        public float frequency;
        public float period;
        public Ease subEase;
        public EasingType easingType;
        public float durationBeats;
        public bool customEase;
        public string customEaseData;
        public AnimationCurve customCurve;

        public override void Decode(LevelEvent evnt)
        {
            preset = (WindowDancePreset)evnt["preset"];
            samePreset = (SamePresetBehavior)evnt["samePresetBehavior"];
            position = evnt.Get<Vector2>("position");
            reference = (ReferenceType)evnt["reference"];
            speed = evnt.GetFloat("speed");
            useCircle = evnt.GetBool("useCircle");
            amplitude = evnt.GetFloat("amplitude");
            amplitudeVector = evnt.Get<Vector2>("amplitudeVector");
            angle = evnt.GetFloat("angle");
            frequency = evnt.GetFloat("frequency");
            period = evnt.GetFloat("period");
            subEase = (Ease)evnt["subEase"];
            easingType = (EasingType)evnt["easeType"];
            durationBeats = evnt.GetFloat("duration");
            duration = durationBeats * crotchet;
            ease = (Ease)evnt["ease"];
            customEase = !Localization.RdStyle && evnt.GetBool("customEase");
            customEaseData = evnt["customEaseData"] as string;
            customCurve = customEase ? CurveUtil.Build(customEaseData) : null;
        }

        public override void StartEffect(scrPlanet planet)
        {
            WindowDanceController.Instance?.Apply(this);
        }
    }

    // 运行时效果：把 Rhythm Doctor 的窗口缩放参数交给窗口控制器。
    public class WindowResizeEffect : ffxPlusBase
    {
        public Vector2 scale;
        public ZoomMode zoomMode;
        public PivotMode pivotMode;
        public Vector2 pivot;
        public PivotAnchorType anchorType;
        public float durationBeats;
        public bool customEase;
        public string customEaseData;
        public AnimationCurve customCurve;

        public override void Decode(LevelEvent evnt)
        {
            scale = evnt.Get<Vector2>("scale");
            zoomMode = (ZoomMode)evnt["zoomMode"];
            pivotMode = (PivotMode)evnt["pivotMode"];
            pivot = evnt.Get<Vector2>("pivot");
            anchorType = (PivotAnchorType)evnt["anchorType"];
            durationBeats = evnt.GetFloat("duration");
            duration = durationBeats * crotchet;
            ease = (Ease)evnt["ease"];
            customEase = !Localization.RdStyle && evnt.GetBool("customEase");
            customEaseData = evnt["customEaseData"] as string;
            customCurve = customEase ? CurveUtil.Build(customEaseData) : null;
        }

        public override void StartEffect(scrPlanet planet)
        {
            WindowDanceController.Instance?.ApplyResize(this);
        }
    }
}
