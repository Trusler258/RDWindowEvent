using System.Collections.Generic;
using ADOFAI;
using UnityEngine;

namespace RDWindowEvent
{
    // 与 Rhythm Doctor 编辑器中的窗口舞蹈预设一致
    public enum WindowDancePreset
    {
        Move,
        Sway,
        Wrap,
        Ellipse,
        ShakePer
    }

    public enum SamePresetBehavior
    {
        Keep,
        Reset
    }

    public enum ReferenceType
    {
        Center,
        Edge
    }

    public enum EasingType
    {
        Repeat,
        Mirror
    }

    // 以下三个枚举与 Rhythm Doctor 的 WindowResize 参数一致
    public enum ZoomMode
    {
        None,
        Fill,
        Fit
    }

    public enum PivotMode
    {
        Default,
        AnchorEdge
    }

    public enum PivotAnchorType
    {
        LeftEdge,
        RightEdge,
        BottomEdge,
        TopEdge
    }

    internal static class EventDefinition
    {
        public const string EventName = "WindowMove";
        public const string ResizeEventName = "WindowResize";

        // ADOFAI 的事件类型枚举不可扩展，这里借用 MiscSettings 作为移动事件的载体。
        // 该类型不会出现在关卡的 floor 事件里，只被本模组的自定义事件使用。
        public const LevelEventType Carrier = LevelEventType.MiscSettings;

        // 缩放事件使用枚举范围外的值，并通过 Decode/Encode 映射为 WindowResize。
        public const LevelEventType ResizeCarrier = (LevelEventType)100;

        private static LevelEventInfo _info;
        private static LevelEventInfo _resizeInfo;
        private static Sprite _icon;
        private static Sprite _resizeIcon;
        private static Sprite _categoryIcon;
        private static bool _installedLogged;

        public static LevelEventInfo Info
        {
            get
            {
                if (_info == null)
                {
                    _info = BuildInfo();
                }
                return _info;
            }
        }

        public static LevelEventInfo ResizeInfo
        {
            get
            {
                if (_resizeInfo == null)
                {
                    _resizeInfo = BuildResizeInfo();
                }
                return _resizeInfo;
            }
        }

        public static Sprite Icon
        {
            get
            {
                if (_icon == null)
                {
                    _icon = BuildIcon(IconKind.Move, "WindowMoveIcon");
                }
                return _icon;
            }
        }

        public static Sprite ResizeIcon
        {
            get
            {
                if (_resizeIcon == null)
                {
                    _resizeIcon = BuildIcon(IconKind.Resize, "WindowResizeIcon");
                }
                return _resizeIcon;
            }
        }

        public static Sprite CategoryIcon
        {
            get
            {
                if (_categoryIcon == null)
                {
                    _categoryIcon = BuildIcon(IconKind.Window, "WindowEventIcon");
                }
                return _categoryIcon;
            }
        }

        public static bool IsOurInfo(LevelEventInfo info)
        {
            return info == Info || info == ResizeInfo;
        }

        private static int CountVisible(LevelEventInfo info)
        {
            int count = 0;
            foreach (var property in info.propertiesInfo.Values)
            {
                if (!property.invisible)
                {
                    count++;
                }
            }
            return count;
        }

        public static Sprite GetIcon(LevelEventInfo info)
        {
            return info == ResizeInfo ? ResizeIcon : Icon;
        }

        public static void Install()
        {
            var all = GCS.levelEventsInfo;
            if (all == null)
            {
                return;
            }
            all[Carrier.ToString()] = Info;
            all[ResizeCarrier.ToString()] = ResizeInfo;
            // LevelEvent 的构造函数会用这个字典把自定义枚举值转换成事件名。
            if (GCS.levelEventTypeString != null && !GCS.levelEventTypeString.ContainsKey(ResizeCarrier))
            {
                GCS.levelEventTypeString[ResizeCarrier] = ResizeCarrier.ToString();
            }
            // 允许同一格子上存在多个窗口事件时使用页签的左右切换按钮。
            EditorConstants.settingsTypes?.Remove(Carrier);
            if (!_installedLogged)
            {
                _installedLogged = true;
                Main.Info("已注入窗口事件，属性数量: " + Info.propertiesInfo.Count + "/" +
                    ResizeInfo.propertiesInfo.Count + "，可见属性: " + CountVisible(Info) + "/" +
                    CountVisible(ResizeInfo) + "，菜单风格: " + (Localization.RdStyle ? "RD" : "Modern"));
            }
        }

        public static List<string> GetEnumLabels(string enumTypeString)
        {
            switch (enumTypeString)
            {
                case "RDWindowEvent.WindowDancePreset, RDWindowEvent":
                    return new List<string>(Localization.PresetLabels);
                case "RDWindowEvent.SamePresetBehavior, RDWindowEvent":
                    return new List<string>(Localization.SamePresetLabels);
                case "RDWindowEvent.ReferenceType, RDWindowEvent":
                    return new List<string>(Localization.ReferenceLabels);
                case "RDWindowEvent.EasingType, RDWindowEvent":
                    return new List<string>(Localization.EaseTypeLabels);
                case "RDWindowEvent.ZoomMode, RDWindowEvent":
                    return new List<string>(Localization.ZoomModeLabels);
                case "RDWindowEvent.PivotMode, RDWindowEvent":
                    return new List<string>(Localization.PivotModeLabels);
                case "RDWindowEvent.PivotAnchorType, RDWindowEvent":
                    return new List<string>(Localization.AnchorLabels);
            }
            return null;
        }

        private static LevelEventInfo BuildInfo()
        {
            var info = new LevelEventInfo
            {
                // name 必须是载体名，否则编辑器事件按钮无法解析类型。
                name = Carrier.ToString(),
                type = Carrier,
                pro = false,
                taroDLC = false,
                isDecoration = false,
                executionTime = LevelEventExecutionTime.OnBar,
                // 游戏的事件栏每个分类最多只有 11 个按钮且没有翻页功能。
                // 这里不占用原版分类，改由自定义分类页签提供入口。
                categories = new List<LevelEventCategory>(),
                propertiesInfo = new Dictionary<string, PropertyInfo>()
            };

            bool rd = Localization.RdStyle;
            Add(info, rd
                ? Hide(Float("angleOffset", 0f, "°", "editor.angleOffset", "Angle offset", null))
                : Float("angleOffset", 0f, "°", "editor.angleOffset", "Angle offset", null));
            Add(info, rd ? Hide(Str("eventTag", "", Localization.MoveLabel("eventTag")))
                : Str("eventTag", "", Localization.MoveLabel("eventTag")));
            Add(info, rd ? Hide(Int("window", 0, 0, 0, Localization.MoveLabel("window")))
                : Int("window", 0, 0, 0, Localization.MoveLabel("window")));
            Add(info, Enum("preset", "RDWindowEvent.WindowDancePreset, RDWindowEvent", "Move",
                Localization.MoveLabel("preset"), null, null));
            Add(info, Enum("samePresetBehavior", "RDWindowEvent.SamePresetBehavior, RDWindowEvent",
                "Reset", Localization.MoveLabel("samePreset"), null, HideIf("preset", "Move")));
            Add(info, Vec2("position", 50f, 50f, true, Localization.MoveLabel("position"), null));
            Add(info, Enum("reference", "RDWindowEvent.ReferenceType, RDWindowEvent", "Center",
                Localization.MoveLabel("reference"), null, ShowIf("preset", "Move")));
            Add(info, Float("speed", 0f, Localization.Unit("%/beat"), null, Localization.MoveLabel("speed"),
                ShowIf("preset", "Wrap", "preset", "Ellipse"), HideIf("samePresetBehavior", "Keep")));
            Add(info, Bool("useCircle", false, Localization.MoveLabel("useCircle"), ShowIf("preset", "Ellipse")));
            Add(info, Float("amplitude", 0f, "%", null, Localization.MoveLabel("amplitude"),
                ShowIf("preset", "Sway", "preset", "Wrap"), null));
            Add(info, Vec2("amplitudeVector", 0f, 0f, false, Localization.MoveLabel("amplitudeVector"), null,
                ShowIf("preset", "Ellipse", "preset", "ShakePer")));
            Add(info, Float("angle", 0f, "°", null, Localization.MoveLabel("angle"),
                ShowIf("preset", "Sway", "preset", "Wrap", "preset", "Ellipse"), null));
            Add(info, Float("frequency", 0f, Localization.Unit("/beat"), null, Localization.MoveLabel("frequency"),
                ShowIf("preset", "Sway", "preset", "Wrap", "preset", "ShakePer"), HideIf("samePresetBehavior", "Keep")));
            Add(info, Float("period", 0f, Localization.Unit("beats"), null, Localization.MoveLabel("period"),
                ShowIf("preset", "ShakePer"), HideIf("samePresetBehavior", "Keep")));
            Add(info, Enum("subEase", "Ease", "Linear", Localization.MoveLabel("subEase"), null,
                ShowIf("preset", "Sway", "preset", "ShakePer"), HideIf("samePresetBehavior", "Keep")));
            Add(info, Enum("easeType", "RDWindowEvent.EasingType, RDWindowEvent", "Repeat",
                Localization.MoveLabel("easeType"), null, ShowIf("preset", "Sway"), HideIf("samePresetBehavior", "Keep")));
            Add(info, Float("duration", 1f, Localization.Unit("beats"), "editor.duration", "Duration", null));
            Add(info, Enum("ease", "Ease", "Linear", "Ease", "editor.ease", null,
                HideIf("customEase", "Enabled")));
            Add(info, rd ? Hide(Bool("customEase", false, Localization.MoveLabel("customEase"), null))
                : Bool("customEase", false, Localization.MoveLabel("customEase"), null));
            Add(info, StrHidden("customEaseData", "0,0;1,1"));
            if (!rd)
            {
                Add(info, Note("curveHint", "wm.note.curve", ShowIf("customEase", "Enabled")));
                Add(info, Note("hint", "wm.note.move"));
            }

            return info;
        }

        private static LevelEventInfo BuildResizeInfo()
        {
            var info = new LevelEventInfo
            {
                name = ResizeCarrier.ToString(),
                type = ResizeCarrier,
                pro = false,
                taroDLC = false,
                isDecoration = false,
                executionTime = LevelEventExecutionTime.OnBar,
                categories = new List<LevelEventCategory>(),
                propertiesInfo = new Dictionary<string, PropertyInfo>()
            };

            bool rd = Localization.RdStyle;
            Add(info, rd
                ? Hide(Float("angleOffset", 0f, "°", "editor.angleOffset", "Angle offset", null))
                : Float("angleOffset", 0f, "°", "editor.angleOffset", "Angle offset", null));
            Add(info, rd ? Hide(Str("eventTag", "", Localization.ResizeLabel("eventTag")))
                : Str("eventTag", "", Localization.ResizeLabel("eventTag")));
            Add(info, rd ? Hide(Int("window", 0, 0, 0, Localization.ResizeLabel("window")))
                : Int("window", 0, 0, 0, Localization.ResizeLabel("window")));
            Add(info, Vec2("scale", 1f, 1f, true, Localization.ResizeLabel("scale"), null, null,
                new List<object> { 0f, 0f }, new List<object> { 2f, 2f }));
            Add(info, Enum("zoomMode", "RDWindowEvent.ZoomMode, RDWindowEvent", "None",
                Localization.ResizeLabel("zoomMode"), null, null));
            Add(info, Enum("pivotMode", "RDWindowEvent.PivotMode, RDWindowEvent", "Default",
                Localization.ResizeLabel("pivotMode"), null, null));
            Add(info, Vec2("pivot", 50f, 50f, false, Localization.ResizeLabel("pivot"), null,
                ShowIf("pivotMode", "Default")));
            Add(info, Enum("anchorType", "RDWindowEvent.PivotAnchorType, RDWindowEvent", "LeftEdge",
                Localization.ResizeLabel("anchorType"), null, ShowIf("pivotMode", "AnchorEdge")));
            Add(info, Float("duration", 1f, Localization.Unit("beats"), "editor.duration", "Duration", null));
            Add(info, Enum("ease", "Ease", "Linear", "Ease", "editor.ease", null,
                HideIf("customEase", "Enabled")));
            Add(info, rd ? Hide(Bool("customEase", false, Localization.ResizeLabel("customEase"), null))
                : Bool("customEase", false, Localization.ResizeLabel("customEase"), null));
            Add(info, StrHidden("customEaseData", "0,0;1,1"));
            if (!rd)
            {
                Add(info, Note("curveHint", "wm.note.curve", ShowIf("customEase", "Enabled")));
                Add(info, Note("hint", "wm.note.resize"));
            }

            return info;
        }

        private static void Add(LevelEventInfo info, Dictionary<string, object> dict)
        {
            var property = new PropertyInfo(dict, info);
            property.order = info.propertiesInfo.Count;
            info.propertiesInfo[property.name] = property;
        }

        private static Dictionary<string, object> Base(string name, string type, object defaultValue, string label,
            string key)
        {
            var dict = new Dictionary<string, object>
            {
                { "name", name },
                { "type", type },
                { "encode", true }
            };
            if (defaultValue != null)
            {
                dict["default"] = defaultValue;
            }
            if (key != null)
            {
                dict["key"] = key;
            }
            else if (label != null)
            {
                dict["customLabel"] = label;
            }
            return dict;
        }

        private static Dictionary<string, object> Float(string name, float value, string unit, string key,
            string label, List<object> showIf, List<object> hideIf = null)
        {
            var dict = Base(name, "Float", value, label, key);
            if (unit != null)
            {
                dict["unit"] = unit;
            }
            if (showIf != null)
            {
                dict["showIf"] = showIf;
            }
            if (hideIf != null)
            {
                dict["hideIf"] = hideIf;
            }
            return dict;
        }

        private static Dictionary<string, object> Int(string name, int value, int min, int max, string label)
        {
            var dict = Base(name, "Int", value, label, null);
            dict["min"] = min;
            dict["max"] = max;
            return dict;
        }

        private static Dictionary<string, object> Bool(string name, bool value, string label, List<object> showIf)
        {
            var dict = Base(name, "Bool", value, label, null);
            if (showIf != null)
            {
                dict["showIf"] = showIf;
            }
            return dict;
        }

        private static Dictionary<string, object> Str(string name, string value, string label)
        {
            return Base(name, "String", value, label, null);
        }

        private static Dictionary<string, object> StrHidden(string name, string value)
        {
            var dict = Base(name, "String", value, null, null);
            dict["invisible"] = true;
            return dict;
        }

        private static Dictionary<string, object> Note(string name, string noteKey, List<object> showIf = null)
        {
            var dict = new Dictionary<string, object>
            {
                { "name", name },
                { "type", "Note" },
                { "encode", false },
                { "noteKey", noteKey }
            };
            if (showIf != null)
            {
                dict["showIf"] = showIf;
            }
            return dict;
        }

        private static Dictionary<string, object> Enum(string name, string enumType, string value, string label,
            string key, List<object> showIf, List<object> hideIf = null)
        {
            var dict = Base(name, "Enum:" + enumType, value, label, key);
            if (showIf != null)
            {
                dict["showIf"] = showIf;
            }
            if (hideIf != null)
            {
                dict["hideIf"] = hideIf;
            }
            return dict;
        }

        private static Dictionary<string, object> Vec2(string name, float x, float y, bool allowEmpty, string label,
            string key, List<object> showIf = null, List<object> min = null, List<object> max = null)
        {
            var dict = Base(name, "Vector2", new List<object> { x, y }, label, key);
            dict["allowEmpty"] = allowEmpty;
            if (showIf != null)
            {
                dict["showIf"] = showIf;
            }
            if (min != null)
            {
                dict["min"] = min;
            }
            if (max != null)
            {
                dict["max"] = max;
            }
            return dict;
        }

        private static Dictionary<string, object> Hide(Dictionary<string, object> dict)
        {
            dict["invisible"] = true;
            return dict;
        }

        private static List<object> ShowIf(params string[] pairs)
        {
            return new List<object>(pairs);
        }

        private static List<object> HideIf(params string[] pairs)
        {
            return new List<object>(pairs);
        }

        private enum IconKind
        {
            Window,
            Move,
            Resize
        }

        private static Sprite BuildIcon(IconKind kind, string name)
        {
            const int size = 64;
            var pixels = new Color32[size * size];
            var clear = new Color32(255, 255, 255, 0);
            for (int i = 0; i < pixels.Length; i++)
            {
                pixels[i] = clear;
            }

            var white = new Color32(255, 255, 255, 255);

            // 窗口外框与标题栏
            DrawRect(pixels, size, 8, 10, 56, 52, 3, white);
            FillRect(pixels, size, 12, 44, 52, 49, white);

            if (kind == IconKind.Move)
            {
                // 向右的移动箭头
                FillRect(pixels, size, 16, 27, 42, 30, white);
                for (int x = 0; x < 8; x++)
                {
                    int half = 8 - x;
                    FillRect(pixels, size, 42 + x, 27 - half, 42 + x + 1, 30 + half, white);
                }
            }
            else if (kind == IconKind.Resize)
            {
                // 斜向双箭头表示缩放
                DrawLine(pixels, size, 21, 21, 41, 41, 3, white);
                FillTriangle(pixels, size, 43, 43, 33, 43, 43, 33, white);
                FillTriangle(pixels, size, 19, 19, 29, 19, 19, 29, white);
            }

            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            texture.filterMode = FilterMode.Bilinear;
            texture.wrapMode = TextureWrapMode.Clamp;
            texture.SetPixels32(pixels);
            texture.Apply(false, false);

            var sprite = Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), 100f);
            sprite.name = name;
            return sprite;
        }

        private static void DrawLine(Color32[] pixels, int size, int x0, int y0, int x1, int y1, int thickness,
            Color32 color)
        {
            int dx = Mathf.Abs(x1 - x0);
            int sx = x0 < x1 ? 1 : -1;
            int dy = -Mathf.Abs(y1 - y0);
            int sy = y0 < y1 ? 1 : -1;
            int error = dx + dy;
            int half = thickness / 2;
            while (true)
            {
                FillRect(pixels, size, x0 - half, y0 - half, x0 + half + 1, y0 + half + 1, color);
                if (x0 == x1 && y0 == y1)
                {
                    break;
                }
                int doubled = 2 * error;
                if (doubled >= dy)
                {
                    error += dy;
                    x0 += sx;
                }
                if (doubled <= dx)
                {
                    error += dx;
                    y0 += sy;
                }
            }
        }

        private static void FillTriangle(Color32[] pixels, int size, int ax, int ay, int bx, int by, int cx,
            int cy, Color32 color)
        {
            int minX = Mathf.Max(0, Mathf.Min(ax, Mathf.Min(bx, cx)));
            int maxX = Mathf.Min(size - 1, Mathf.Max(ax, Mathf.Max(bx, cx)));
            int minY = Mathf.Max(0, Mathf.Min(ay, Mathf.Min(by, cy)));
            int maxY = Mathf.Min(size - 1, Mathf.Max(ay, Mathf.Max(by, cy)));
            for (int y = minY; y <= maxY; y++)
            {
                for (int x = minX; x <= maxX; x++)
                {
                    int d1 = (x - bx) * (ay - by) - (ax - bx) * (y - by);
                    int d2 = (x - cx) * (by - cy) - (bx - cx) * (y - cy);
                    int d3 = (x - ax) * (cy - ay) - (cx - ax) * (y - ay);
                    bool hasNegative = d1 < 0 || d2 < 0 || d3 < 0;
                    bool hasPositive = d1 > 0 || d2 > 0 || d3 > 0;
                    if (!(hasNegative && hasPositive))
                    {
                        pixels[y * size + x] = color;
                    }
                }
            }
        }

        private static void FillRect(Color32[] pixels, int size, int x0, int y0, int x1, int y1, Color32 color)
        {
            for (int y = y0; y < y1; y++)
            {
                if (y < 0 || y >= size)
                {
                    continue;
                }
                for (int x = x0; x < x1; x++)
                {
                    if (x < 0 || x >= size)
                    {
                        continue;
                    }
                    pixels[y * size + x] = color;
                }
            }
        }

        private static void DrawRect(Color32[] pixels, int size, int x0, int y0, int x1, int y1, int thickness,
            Color32 color)
        {
            FillRect(pixels, size, x0, y0, x1, y0 + thickness, color);
            FillRect(pixels, size, x0, y1 - thickness, x1, y1, color);
            FillRect(pixels, size, x0, y0, x0 + thickness, y1, color);
            FillRect(pixels, size, x1 - thickness, y0, x1, y1, color);
        }
    }
}
