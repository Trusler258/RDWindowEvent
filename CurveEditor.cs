using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using ADOFAI;
using UnityEngine;

namespace RDWindowEvent
{
    internal static class CurveUtil
    {
        public static List<Vector2> Parse(string data)
        {
            var points = new List<Vector2>();
            if (string.IsNullOrWhiteSpace(data))
            {
                return points;
            }
            var parts = data.Split(new[] { ';', ' ' }, System.StringSplitOptions.RemoveEmptyEntries);
            foreach (var part in parts)
            {
                var pair = part.Split(',');
                if (pair.Length < 2)
                {
                    continue;
                }
                if (float.TryParse(pair[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var time) &&
                    float.TryParse(pair[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var value))
                {
                    points.Add(new Vector2(Mathf.Clamp01(time), Mathf.Clamp01(value)));
                }
            }
            points.Sort((a, b) => a.x.CompareTo(b.x));
            return points;
        }

        public static AnimationCurve Build(string data)
        {
            var points = Parse(data);
            if (points.Count < 2)
            {
                return null;
            }
            var keys = points.Select(p => new Keyframe(p.x, p.y)).ToArray();
            var curve = new AnimationCurve(keys);
            for (int i = 0; i < keys.Length; i++)
            {
                curve.SmoothTangents(i, 0f);
            }
            curve.preWrapMode = WrapMode.Clamp;
            curve.postWrapMode = WrapMode.Clamp;
            return curve;
        }

        public static string Serialize(IEnumerable<Vector2> points)
        {
            return string.Join(";", points.Select(p =>
                p.x.ToString("0.###", CultureInfo.InvariantCulture) + "," +
                p.y.ToString("0.###", CultureInfo.InvariantCulture)));
        }
    }

    // 独立的 IMGUI 曲线编辑器，不依赖 ADOFAI 的属性控件系统。
    public class CurveEditorWindow : MonoBehaviour
    {
        private static CurveEditorWindow _instance;
        private static Texture2D _white;

        private bool _open;
        private LevelEvent _event;
        private readonly List<Vector2> _points = new List<Vector2>();
        private int _dragIndex = -1;
        private Rect _windowRect = new Rect(180f, 80f, 520f, 380f);

        public static void Ensure()
        {
            if (!Application.isPlaying || _instance != null)
            {
                return;
            }
            var go = new GameObject("RDWindowEventCurveEditor");
            Object.DontDestroyOnLoad(go);
            _instance = go.AddComponent<CurveEditorWindow>();
        }

        public static void OpenForSelected()
        {
            Ensure();
            _instance?.Open();
        }

        private void Update()
        {
            if (!Application.isPlaying || !ADOBase.isLevelEditor)
            {
                return;
            }
            var editor = ADOBase.editor;
            if (editor == null)
            {
                return;
            }
            if (Localization.RdStyle)
            {
                if (_open)
                {
                    Close();
                }
                return;
            }
            if (Input.GetKey(KeyCode.LeftControl) && Input.GetKey(KeyCode.LeftShift) &&
                Input.GetKeyDown(KeyCode.E))
            {
                Open();
            }
            if (_open && _event != null && editor.levelEventsPanel.selectedEvent != _event)
            {
                Close();
            }
        }

        private void Open()
        {
            if (Localization.RdStyle)
            {
                return;
            }
            var editor = ADOBase.editor;
            if (editor == null)
            {
                return;
            }
            var ev = editor.levelEventsPanel.selectedEvent;
            if (ev == null || ev.info == null || !EventDefinition.IsOurInfo(ev.info))
            {
                return;
            }

            _event = ev;
            _points.Clear();
            _points.AddRange(CurveUtil.Parse(ev["customEaseData"] as string));
            if (_points.Count < 2)
            {
                _points.Clear();
                _points.Add(new Vector2(0f, 0f));
                _points.Add(new Vector2(1f, 1f));
            }
            EnsureEndpoints();
            _open = true;
        }

        private void Close()
        {
            _open = false;
            _event = null;
            _dragIndex = -1;
        }

        private void EnsureEndpoints()
        {
            _points.Sort((a, b) => a.x.CompareTo(b.x));
            if (_points.Count == 0)
            {
                _points.Add(new Vector2(0f, 0f));
                _points.Add(new Vector2(1f, 1f));
            }
            if (_points[0].x > 0f)
            {
                _points.Insert(0, new Vector2(0f, _points[0].y));
            }
            if (_points[_points.Count - 1].x < 1f)
            {
                _points.Add(new Vector2(1f, _points[_points.Count - 1].y));
            }
            for (int i = 1; i < _points.Count - 1; i++)
            {
                float min = _points[i - 1].x + 0.01f;
                float max = _points[i + 1].x - 0.01f;
                if (min > max)
                {
                    min = max = (_points[i - 1].x + _points[i + 1].x) / 2f;
                }
                _points[i] = new Vector2(Mathf.Clamp(_points[i].x, min, max), _points[i].y);
            }
        }

        private void OnGUI()
        {
            if (!Application.isPlaying)
            {
                return;
            }
            EnsureWhite();
            var editor = ADOBase.editor;
            if (editor == null)
            {
                return;
            }

            if (Localization.RdStyle)
            {
                return;
            }
            var selected = editor.levelEventsPanel.selectedEvent;
            bool ours = selected != null && selected.info != null && EventDefinition.IsOurInfo(selected.info);
            if (!_open && ours)
            {
                if (GUI.Button(new Rect(Screen.width - 270f, 8f, 260f, 28f),
                    "Edit Ease Curve (Ctrl+Shift+E)"))
                {
                    Open();
                }
            }

            if (!_open || _event == null)
            {
                return;
            }
            _windowRect = GUI.Window(823451, _windowRect, DrawWindow, "Ease Curve Editor");
        }

        private void DrawWindow(int id)
        {
            var area = new Rect(24f, 34f, _windowRect.width - 48f, _windowRect.height - 106f);
            DrawGrid(area);
            DrawCurve(area);
            HandleMouse(area);
            DrawPoints(area);

            if (GUI.Button(new Rect(24f, _windowRect.height - 58f, 100f, 30f), "Save"))
            {
                Save();
            }
            if (GUI.Button(new Rect(134f, _windowRect.height - 58f, 100f, 30f), "Linear"))
            {
                _points.Clear();
                _points.Add(new Vector2(0f, 0f));
                _points.Add(new Vector2(1f, 1f));
            }
            if (GUI.Button(new Rect(244f, _windowRect.height - 58f, 100f, 30f), "Cancel"))
            {
                Close();
            }

            GUI.Label(new Rect(354f, _windowRect.height - 52f, 150f, 20f), "Double-click: add");
            GUI.Label(new Rect(354f, _windowRect.height - 34f, 150f, 20f), "Right-click: remove");
            GUI.DragWindow(new Rect(0f, 0f, _windowRect.width, 24f));
        }

        private void DrawGrid(Rect area)
        {
            for (int i = 0; i <= 4; i++)
            {
                float t = i / 4f;
                DrawLine(new Vector2(area.x + area.width * t, area.y),
                    new Vector2(area.x + area.width * t, area.yMax), new Color(1f, 1f, 1f, 0.12f));
                DrawLine(new Vector2(area.x, area.y + area.height * t),
                    new Vector2(area.xMax, area.y + area.height * t), new Color(1f, 1f, 1f, 0.12f));
            }
            DrawLine(new Vector2(area.x, area.yMax), new Vector2(area.xMax, area.yMax),
                new Color(1f, 1f, 1f, 0.4f));
            DrawLine(new Vector2(area.x, area.y), new Vector2(area.x, area.yMax),
                new Color(1f, 1f, 1f, 0.4f));
        }

        private void DrawCurve(Rect area)
        {
            AnimationCurve curve = CurveUtil.Build(CurveUtil.Serialize(_points));
            if (curve == null)
            {
                return;
            }
            Vector2 previous = ToScreen(area, 0f, curve.Evaluate(0f));
            for (int i = 1; i <= 64; i++)
            {
                float t = i / 64f;
                Vector2 current = ToScreen(area, t, curve.Evaluate(t));
                DrawLine(previous, current, new Color(0.3f, 0.85f, 1f, 0.95f));
                previous = current;
            }
        }

        private void DrawPoints(Rect area)
        {
            for (int i = 0; i < _points.Count; i++)
            {
                Vector2 position = ToScreen(area, _points[i].x, _points[i].y);
                GUI.color = i == _dragIndex ? Color.yellow : Color.white;
                GUI.DrawTexture(new Rect(position.x - 5f, position.y - 5f, 10f, 10f), _white);
            }
            GUI.color = Color.white;
        }

        private void HandleMouse(Rect area)
        {
            var current = Event.current;
            if (current.type == EventType.MouseDown && area.Contains(current.mousePosition))
            {
                int index = FindPoint(area, current.mousePosition);
                if (current.button == 0 && current.clickCount == 2)
                {
                    _points.Add(FromScreen(area, current.mousePosition));
                    _points.Sort((a, b) => a.x.CompareTo(b.x));
                    _dragIndex = FindPoint(area, current.mousePosition);
                    current.Use();
                }
                else if (current.button == 0 && index >= 0)
                {
                    _dragIndex = index;
                    current.Use();
                }
                else if (current.button == 1 && index > 0 && index < _points.Count - 1)
                {
                    _points.RemoveAt(index);
                    current.Use();
                }
            }
            else if (current.type == EventType.MouseDrag && _dragIndex >= 0)
            {
                var point = FromScreen(area, current.mousePosition);
                if (_dragIndex == 0)
                {
                    point.x = 0f;
                }
                else if (_dragIndex == _points.Count - 1)
                {
                    point.x = 1f;
                }
                else
                {
                    point.x = Mathf.Clamp(point.x, _points[_dragIndex - 1].x + 0.01f,
                        _points[_dragIndex + 1].x - 0.01f);
                }
                _points[_dragIndex] = point;
                current.Use();
            }
            else if (current.type == EventType.MouseUp && _dragIndex >= 0)
            {
                _dragIndex = -1;
                current.Use();
            }
        }

        private int FindPoint(Rect area, Vector2 mouse)
        {
            int best = -1;
            float bestDistance = 14f;
            for (int i = 0; i < _points.Count; i++)
            {
                float distance = Vector2.Distance(ToScreen(area, _points[i].x, _points[i].y), mouse);
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    best = i;
                }
            }
            return best;
        }

        private void Save()
        {
            if (_event == null)
            {
                return;
            }
            var editor = ADOBase.editor;
            if (editor == null)
            {
                return;
            }
            EnsureEndpoints();
            var data = CurveUtil.Serialize(_points);
            using (new SaveStateScope(editor, clearRedo: false, dataHasChanged: true))
            {
                _event["customEase"] = true;
                _event["customEaseData"] = data;
                _event.disabled["customEase"] = false;
                _event.disabled["customEaseData"] = false;
                editor.ApplyEventsToFloors();
            }

            int index = 0;
            int count = 0;
            foreach (var ev in editor.events)
            {
                if (ev.floor == _event.floor && ev.eventType == _event.eventType)
                {
                    if (ev == _event)
                    {
                        index = count;
                    }
                    count++;
                }
            }
            editor.levelEventsPanel.ShowPanel(_event.eventType, index);
            Close();
        }

        private static Vector2 ToScreen(Rect area, float time, float value)
        {
            return new Vector2(area.x + area.width * time, area.y + area.height * (1f - value));
        }

        private static Vector2 FromScreen(Rect area, Vector2 point)
        {
            return new Vector2(
                Mathf.Clamp01((point.x - area.x) / area.width),
                Mathf.Clamp01(1f - (point.y - area.y) / area.height));
        }

        private static void DrawLine(Vector2 from, Vector2 to, Color color)
        {
            EnsureWhite();
            float angle = Mathf.Atan2(to.y - from.y, to.x - from.x) * Mathf.Rad2Deg;
            float length = Vector2.Distance(from, to);
            GUI.color = color;
            GUIUtility.RotateAroundPivot(angle, from);
            GUI.DrawTexture(new Rect(from.x, from.y - 1f, length, 2f), _white);
            GUIUtility.RotateAroundPivot(-angle, from);
            GUI.color = Color.white;
        }

        private static void EnsureWhite()
        {
            if (_white != null)
            {
                return;
            }
            _white = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            _white.SetPixel(0, 0, Color.white);
            _white.Apply();
        }
    }
}
