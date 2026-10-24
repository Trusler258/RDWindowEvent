using System;
using System.Reflection;
using HarmonyLib;
using UnityModManagerNet;
using UnityEngine;

namespace RDWindowEvent
{
    public enum LanguageMode
    {
        Auto,
        Chinese,
        English
    }

    public enum ResizeBaseMode
    {
        WindowAtLevelStart,
        FixedResolution
    }

    public enum EventMenuStyle
    {
        RhythmDoctor,
        Modern
    }

    public class ModConfig : UnityModManager.ModSettings
    {
        public LanguageMode Language = LanguageMode.Auto;
        public EventMenuStyle MenuStyle = EventMenuStyle.Modern;
        public ResizeBaseMode ResizeBase = ResizeBaseMode.WindowAtLevelStart;
        public int BaseWidth = 1920;
        public int BaseHeight = 1080;
        public int MinWidth = 704;
        public int MinHeight = 396;
        public bool RebaseOnResolutionChange = true;

        public override void Save(UnityModManager.ModEntry modEntry)
        {
            Save(this, modEntry);
        }
    }

    internal static class Localization
    {
        public static bool Chinese
        {
            get
            {
                var mode = Main.Config != null ? Main.Config.Language : LanguageMode.Auto;
                if (mode == LanguageMode.Chinese)
                {
                    return true;
                }
                if (mode == LanguageMode.English)
                {
                    return false;
                }
                var language = Application.systemLanguage;
                return language == SystemLanguage.Chinese ||
                       language == SystemLanguage.ChineseSimplified ||
                       language == SystemLanguage.ChineseTraditional;
            }
        }

        public static bool RdStyle => Main.Config != null && Main.Config.MenuStyle == EventMenuStyle.RhythmDoctor;
        public static string CategoryName => Chinese ? "窗口事件" : "Window Events";
        public static string MoveTitle => RdStyle
            ? (Chinese ? "窗口舞蹈" : "Window Dance")
            : (Chinese ? "窗口移动" : "Window Move");
        public static string ResizeTitle => Chinese ? "窗口缩放" : "Window Resize";
        public static string MoveNote => Chinese
            ? "注：位置和幅度使用屏幕百分比，原点在屏幕左下角"
            : "Note: position and amplitude use screen percentages, origin at the bottom-left of the screen";
        public static string ResizeNote => Chinese
            ? "缩放倍数相对于基准分辨率：1 表示基准大小，2 表示两倍"
            : "Scale is relative to the base resolution: 1 is base size, 2 is double";
        public static string CurveHint => Chinese
            ? "点击右上角“编辑缓动曲线”，或按 Ctrl+Shift+E 编辑曲线"
            : "Click 'Edit Ease Curve' at the top-right, or press Ctrl+Shift+E";

        public static string MoveLabel(string key)
        {
            if (!Chinese)
            {
                switch (key)
                {
                    case "eventTag": return "Event tag";
                    case "window": return "Window (only 0 exists)";
                    case "preset": return "Preset";
                    case "samePreset": return "Same preset";
                    case "position": return "Position (%)";
                    case "reference": return "Reference";
                    case "speed": return "Speed (%/beat)";
                    case "useCircle": return "Use circle";
                    case "amplitude": return "Amplitude (%)";
                    case "amplitudeVector": return "Amplitude (x, y) %";
                    case "angle": return "Angle (°)";
                    case "frequency": return "Frequency (/beat)";
                    case "period": return "Period (beats)";
                    case "subEase": return "Sub ease";
                    case "easeType": return "Ease type";
                    case "customEase": return "Custom ease";
                }
                return key;
            }
            switch (key)
            {
                case "eventTag": return "事件标签";
                case "window": return "窗口（仅支持 0）";
                case "preset": return "预设";
                case "samePreset": return "同预设行为";
                case "position": return "位置 (%)";
                case "reference": return "参考点";
                case "speed": return "速度 (%/拍)";
                case "useCircle": return "使用圆形";
                case "amplitude": return "幅度 (%)";
                case "amplitudeVector": return "幅度 (x, y) %";
                case "angle": return "角度 (°)";
                case "frequency": return "频率 (/拍)";
                case "period": return "周期 (拍)";
                case "subEase": return "子缓动";
                case "easeType": return "缓动类型";
                case "customEase": return "自定义缓动曲线";
            }
            return key;
        }

        public static string ResizeLabel(string key)
        {
            if (!Chinese)
            {
                switch (key)
                {
                    case "eventTag": return "Event tag";
                    case "window": return "Window (only 0 exists)";
                    case "scale": return "Scale (x, y)";
                    case "zoomMode": return "Zoom mode";
                    case "pivotMode": return "Pivot mode";
                    case "pivot": return "Pivot (%)";
                    case "anchorType": return "Anchor";
                    case "customEase": return "Custom ease";
                }
                return key;
            }
            switch (key)
            {
                case "eventTag": return "事件标签";
                case "window": return "窗口（仅支持 0）";
                case "scale": return "缩放倍数 (x, y)";
                case "zoomMode": return "镜头缩放模式";
                case "pivotMode": return "枢轴模式";
                case "pivot": return "枢轴位置 (%)";
                case "anchorType": return "固定边";
                case "customEase": return "自定义缓动曲线";
            }
            return key;
        }

        public static string Unit(string unit)
        {
            if (!Chinese)
            {
                return unit;
            }
            switch (unit)
            {
                case "%/beat": return "%/拍";
                case "/beat": return "/拍";
                case "beats": return "拍";
            }
            return unit;
        }

        public static string[] PresetLabels => Chinese
            ? new[] { "移动", "摇摆", "缠绕", "椭圆", "震动" }
            : new[] { "Move", "Sway", "Wrap", "Ellipse", "Shake Per" };

        public static string[] SamePresetLabels => Chinese
            ? new[] { "保持", "重置" }
            : new[] { "Keep", "Reset" };

        public static string[] ReferenceLabels => Chinese
            ? new[] { "中心", "边缘" }
            : new[] { "Center", "Edge" };

        public static string[] EaseTypeLabels => Chinese
            ? new[] { "重复", "镜像" }
            : new[] { "Repeat", "Mirror" };

        public static string[] ZoomModeLabels => Chinese
            ? new[] { "无", "填充", "适应" }
            : new[] { "None", "Fill", "Fit" };

        public static string[] PivotModeLabels => Chinese
            ? new[] { "默认", "固定边" }
            : new[] { "Default", "Anchor Edge" };

        public static string[] AnchorLabels => Chinese
            ? new[] { "左边", "右边", "下边", "上边" }
            : new[] { "Left Edge", "Right Edge", "Bottom Edge", "Top Edge" };
    }

    public static class Main
    {
        internal static UnityModManager.ModEntry.ModLogger Log;
        internal static ModConfig Config;
        private static Harmony _harmony;
        private static bool _enabled;

        public static bool Load(UnityModManager.ModEntry modEntry)
        {
            Log = modEntry.Logger;
            try
            {
                Config = UnityModManager.ModSettings.Load<ModConfig>(modEntry);
                modEntry.OnGUI = OnGUI;
                modEntry.OnHideGUI = e => Config.Save(e);
                modEntry.OnSaveGUI = e => Config.Save(e);
                modEntry.OnToggle = OnToggle;
                modEntry.OnUnload = OnUnload;

                _harmony = new Harmony(modEntry.Info.Id);
                PatchManager.Apply(_harmony);
                // 如果模组是在游戏启动后才启用，事件表可能已经初始化，需要补一次注入。
                EventDefinition.Install();
                WindowDanceController.Ensure();
                CurveEditorWindow.Ensure();
                _enabled = true;
                Log.Log("RDWindowEvent 已加载。");
            }
            catch (Exception ex)
            {
                Log.Error("RDWindowEvent 加载失败: " + ex);
                return false;
            }
            return true;
        }

        private static bool OnToggle(UnityModManager.ModEntry modEntry, bool active)
        {
            if (active)
            {
                if (!_enabled)
                {
                    _harmony = new Harmony(modEntry.Info.Id);
                    PatchManager.Apply(_harmony);
                    EventDefinition.Install();
                    WindowDanceController.Ensure();
                    CurveEditorWindow.Ensure();
                    _enabled = true;
                    Log?.Log("RDWindowEvent 已启用。");
                }
            }
            else if (_enabled)
            {
                Disable(modEntry);
            }
            return true;
        }

        private static bool OnUnload(UnityModManager.ModEntry modEntry)
        {
            if (_enabled)
            {
                Disable(modEntry);
            }
            return true;
        }

        private static void Disable(UnityModManager.ModEntry modEntry)
        {
            WindowDanceController.Shutdown();
            CurveEditorWindow.Shutdown();
            EventDefinition.Uninstall();
            if (ADOBase.editor != null)
            {
                CustomCategoryIntegration.Uninstall(ADOBase.editor);
            }
            _harmony?.UnpatchAll(modEntry.Info.Id);
            _harmony = null;
            _enabled = false;
            Log?.Log("RDWindowEvent 已停用。");
        }

        private static void OnGUI(UnityModManager.ModEntry modEntry)
        {
            var config = Config ?? (Config = new ModConfig());
            GUILayout.BeginVertical();

            GUILayout.Label("Language / 语言", GUILayout.ExpandWidth(false));
            int language = GUILayout.Toolbar((int)config.Language, new[]
            {
                "Auto / 自动", "中文", "English"
            });
            config.Language = (LanguageMode)language;

            GUILayout.Space(6f);
            GUILayout.Label("Event Menu / 事件菜单", GUILayout.ExpandWidth(false));
            int menuStyle = GUILayout.Toolbar((int)config.MenuStyle, new[]
            {
                "RD compact / RD 精简", "Modern / 完整"
            });
            config.MenuStyle = (EventMenuStyle)menuStyle;

            GUILayout.Space(6f);
            GUILayout.Label("Resize Base / 缩放基准", GUILayout.ExpandWidth(false));
            int baseMode = GUILayout.Toolbar((int)config.ResizeBase, new[]
            {
                "Level start / 关卡开始", "Fixed / 固定分辨率"
            });
            config.ResizeBase = (ResizeBaseMode)baseMode;

            if (config.ResizeBase == ResizeBaseMode.FixedResolution)
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label("Width", GUILayout.Width(50f));
                config.BaseWidth = ParseInt(GUILayout.TextField(config.BaseWidth.ToString(),
                    GUILayout.Width(70f)), config.BaseWidth);
                GUILayout.Label("Height", GUILayout.Width(55f));
                config.BaseHeight = ParseInt(GUILayout.TextField(config.BaseHeight.ToString(),
                    GUILayout.Width(70f)), config.BaseHeight);
                GUILayout.EndHorizontal();
            }

            config.RebaseOnResolutionChange = GUILayout.Toggle(config.RebaseOnResolutionChange,
                "Rebase on resolution change / 分辨率变化时更新基准");

            GUILayout.Space(6f);
            GUILayout.Label("Min Window / 最小窗口", GUILayout.ExpandWidth(false));
            GUILayout.BeginHorizontal();
            GUILayout.Label("Width", GUILayout.Width(50f));
            config.MinWidth = ParseInt(GUILayout.TextField(config.MinWidth.ToString(),
                GUILayout.Width(70f)), config.MinWidth);
            GUILayout.Label("Height", GUILayout.Width(55f));
            config.MinHeight = ParseInt(GUILayout.TextField(config.MinHeight.ToString(),
                GUILayout.Width(70f)), config.MinHeight);
            GUILayout.EndHorizontal();

            GUILayout.Label("Language and menu style changes apply after restarting the game.\n" +
                "语言和事件菜单风格更改后需重启游戏生效");

            GUILayout.EndVertical();
        }

        private static int ParseInt(string text, int fallback)
        {
            return int.TryParse(text, out var value) && value > 0 ? value : fallback;
        }

        internal static void Info(string message)
        {
            Log?.Log(message);
        }

        internal static void Warn(string message)
        {
            Log?.Log("[警告] " + message);
        }
    }

    internal static class PatchManager
    {
        public static void Apply(Harmony harmony)
        {
            // 逐个打补丁，单个方法缺失不会影响其它补丁。
            Try(harmony, typeof(Patches.SetupLevelEventsInfoPatch));
            Try(harmony, typeof(CustomCategoryIntegration.LoadEditorPropertiesPatch));
            Try(harmony, typeof(CustomCategoryIntegration.CategoryLabelPatch));
            Try(harmony, typeof(CustomCategoryIntegration.NoteStylePatch));
            Try(harmony, typeof(CustomCategoryIntegration.NoteStyleEnabledPatch));
            Try(harmony, typeof(Patches.LevelEventDecodePatch));
            Try(harmony, typeof(Patches.LevelEventEncodePatch));
            Try(harmony, typeof(Patches.GetFloorEventsPatch));
            Try(harmony, typeof(Patches.ShowPanelPatch));
            Try(harmony, typeof(Patches.ShowTabsForFloorPatch));
            Try(harmony, typeof(Patches.InspectorTabInitPatch));
            Try(harmony, typeof(Patches.LevelEventButtonInitPatch));
            Try(harmony, typeof(Patches.LevelEventButtonTooltipPatch));
            Try(harmony, typeof(Patches.EventIndicatorInitPatch));
            Try(harmony, typeof(Patches.ListItemEventPatch));
            Try(harmony, typeof(Patches.EnumSetupPatch));
            Try(harmony, typeof(Patches.ApplyEventPatch));
            Try(harmony, typeof(Patches.EnsureRequiredModsPatch));
            Try(harmony, typeof(Patches.ModsDependencyPatch));
            Try(harmony, typeof(Patches.PlayBaselinePatch));
            Try(harmony, typeof(Patches.LoadBaselinePatch));
            Try(harmony, typeof(Patches.CurveEditorInputPatch));
            Try(harmony, typeof(Patches.VfxResetPatch));
        }

        private static void Try(Harmony harmony, Type type)
        {
            try
            {
                harmony.CreateClassProcessor(type).Patch();
            }
            catch (Exception ex)
            {
                Main.Warn("补丁 " + type.Name + " 应用失败: " + ex.Message);
            }
        }
    }
}
