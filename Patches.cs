using System;
using System.Collections.Generic;
using ADOFAI;
using ADOFAI.LevelEditor.Controls;
using HarmonyLib;
using TMPro;
using UnityEngine;

namespace RDWindowEvent
{
    internal static class Patches
    {
        internal static bool IsOurType(LevelEventType type)
        {
            return type == EventDefinition.Carrier || type == EventDefinition.ResizeCarrier;
        }

        internal static bool IsOurInfo(LevelEventInfo info)
        {
            return EventDefinition.IsOurInfo(info);
        }

        internal static void EnsureIconEntry()
        {
            var icons = GCS.levelEventIcons;
            if (icons == null)
            {
                return;
            }
            if (!icons.ContainsKey(EventDefinition.Carrier))
            {
                icons[EventDefinition.Carrier] = EventDefinition.Icon;
            }
            if (!icons.ContainsKey(EventDefinition.ResizeCarrier))
            {
                icons[EventDefinition.ResizeCarrier] = EventDefinition.ResizeIcon;
            }
        }

        // 游戏启动解析完事件表之后注入自定义事件。
        [HarmonyPatch(typeof(ADOStartup), "SetupLevelEventsInfo")]
        internal static class SetupLevelEventsInfoPatch
        {
            private static void Postfix()
            {
                EventDefinition.Install();
            }
        }

        // 读取关卡时把自定义 eventType 映射到载体类型。
        [HarmonyPatch(typeof(LevelEvent), "Decode")]
        internal static class LevelEventDecodePatch
        {
            private static void Prefix(Dictionary<string, object> dict, ref string explicitEventType, bool isGlobal)
            {
                if (isGlobal || dict == null)
                {
                    return;
                }
                if (explicitEventType == EventDefinition.EventName)
                {
                    explicitEventType = EventDefinition.Carrier.ToString();
                    return;
                }
                if (explicitEventType == EventDefinition.ResizeEventName)
                {
                    explicitEventType = EventDefinition.ResizeCarrier.ToString();
                    return;
                }
                if (explicitEventType != null || !dict.TryGetValue("eventType", out var raw))
                {
                    return;
                }
                var typeName = raw as string;
                if (typeName == EventDefinition.EventName)
                {
                    dict["eventType"] = EventDefinition.Carrier.ToString();
                }
                else if (typeName == EventDefinition.ResizeEventName)
                {
                    dict["eventType"] = EventDefinition.ResizeCarrier.ToString();
                }
            }
        }

        // 保存关卡时写回自定义事件名。
        [HarmonyPatch(typeof(LevelEvent), "Encode")]
        internal static class LevelEventEncodePatch
        {
            private static void Postfix(LevelEvent __instance, Dictionary<string, object> __result)
            {
                if (__result == null || __instance == null)
                {
                    return;
                }
                if (__instance.info == EventDefinition.Info)
                {
                    __result["eventType"] = EventDefinition.EventName;
                }
                else if (__instance.info == EventDefinition.ResizeInfo)
                {
                    __result["eventType"] = EventDefinition.ResizeEventName;
                }
            }
        }

        // 自定义事件按普通 floor 事件处理。
        [HarmonyPatch(typeof(scnEditor), "GetFloorEvents")]
        internal static class GetFloorEventsPatch
        {
            private static bool Prefix(int floorID, LevelEventType eventType, ref List<LevelEvent> __result)
            {
                if (!IsOurType(eventType))
                {
                    return true;
                }
                __result = new List<LevelEvent>();
                var editor = ADOBase.editor;
                if (editor == null || editor.events == null)
                {
                    return false;
                }
                foreach (var ev in editor.events)
                {
                    if (ev != null && ev.floor == floorID && ev.eventType == eventType)
                    {
                        __result.Add(ev);
                    }
                }
                return false;
            }
        }

        // 自定义事件的检查器面板需要绕过原版的事件分支。
        [HarmonyPatch(typeof(InspectorPanel), "ShowPanel")]
        internal static class ShowPanelPatch
        {
            private static bool Prefix(InspectorPanel __instance, LevelEventType eventType, int eventIndex)
            {
                if (!__instance.floorPanel || !IsOurType(eventType))
                {
                    return true;
                }
                WindowEventInspector.Show(__instance, eventIndex, eventType);
                return false;
            }
        }

        // 原版只用 Enum.GetValues 选择页签，100 不在枚举里，
        // 所以只含缩放事件的格子会被判定为“没有事件”，显示上一个缓存页签。
        [HarmonyPatch(typeof(InspectorPanel), "ShowTabsForFloor")]
        internal static class ShowTabsForFloorPatch
        {
            private static void Postfix(InspectorPanel __instance, int floorID)
            {
                if (__instance == null || !__instance.floorPanel)
                {
                    return;
                }
                var editor = ADOBase.editor;
                if (editor == null || editor.events == null)
                {
                    return;
                }

                bool hasResize = false;
                bool hasMove = false;
                foreach (var ev in editor.events)
                {
                    if (ev == null || ev.floor != floorID)
                    {
                        continue;
                    }
                    if (ev.eventType == EventDefinition.ResizeCarrier)
                    {
                        hasResize = true;
                    }
                    else if (ev.eventType == EventDefinition.Carrier)
                    {
                        hasMove = true;
                    }
                }
                if (!hasResize && !hasMove)
                {
                    return;
                }

                var selected = __instance.selectedEventType;
                if (selected == EventDefinition.ResizeCarrier || selected == EventDefinition.Carrier)
                {
                    return;
                }
                if (selected != LevelEventType.None)
                {
                    return;
                }

                WindowEventInspector.Show(__instance, 0,
                    hasResize ? EventDefinition.ResizeCarrier : EventDefinition.Carrier);
                __instance.ShowInspector(true);
            }
        }

        [HarmonyPatch(typeof(InspectorTab), "Init")]
        internal static class InspectorTabInitPatch
        {
            private static void Prefix(LevelEventType type)
            {
                if (IsOurType(type))
                {
                    EnsureIconEntry();
                }
            }

            private static void Postfix(InspectorTab __instance)
            {
                if (__instance.panel == null || !__instance.panel.floorPanel ||
                    !IsOurType(__instance.levelEventType))
                {
                    return;
                }

                // 移动事件的载体被游戏当成设置事件，页签不会执行 FlipTab，
                // 结果页签外形左右颠倒。这里按普通 floor 事件的方式翻转。
                try
                {
                    var field = AccessTools.Field(typeof(InspectorTab), "isEventTab");
                    bool isEventTab = field != null && (bool)field.GetValue(__instance);
                    if (!isEventTab)
                    {
                        field?.SetValue(__instance, true);
                        AccessTools.Method(typeof(InspectorTab), "FlipTab")?.Invoke(__instance, null);
                        __instance.SetSelected(false);
                    }
                }
                catch (Exception ex)
                {
                    Main.Warn("修正窗口事件页签方向失败: " + ex.Message);
                }

                __instance.icon.sprite = EventDefinition.GetIcon(__instance.levelEventType == EventDefinition.ResizeCarrier
                    ? EventDefinition.ResizeInfo
                    : EventDefinition.Info);
            }
        }

        [HarmonyPatch(typeof(LevelEventButton), "Init")]
        internal static class LevelEventButtonInitPatch
        {
            private static void Prefix(LevelEventType type)
            {
                if (IsOurType(type))
                {
                    EnsureIconEntry();
                }
            }

            private static void Postfix(LevelEventButton __instance)
            {
                if (__instance.info != null && IsOurInfo(__instance.info))
                {
                    __instance.icon.sprite = EventDefinition.GetIcon(__instance.info);
                }
            }
        }

        [HarmonyPatch(typeof(LevelEventButton), "ShowAsSelected")]
        internal static class LevelEventButtonTooltipPatch
        {
            private static void Postfix(LevelEventButton __instance, bool selected)
            {
                if (!selected || __instance.info == null || !IsOurInfo(__instance.info) ||
                    ADOBase.editor == null)
                {
                    return;
                }
                ADOBase.editor.eventPickerText.text = __instance.info == EventDefinition.ResizeInfo
                    ? Localization.ResizeTitle
                    : Localization.MoveTitle;
            }
        }

        [HarmonyPatch(typeof(EventIndicator), "Init")]
        internal static class EventIndicatorInitPatch
        {
            private static void Postfix(EventIndicator __instance, LevelEvent baseEvent)
            {
                if (baseEvent != null && baseEvent.info != null && IsOurInfo(baseEvent.info))
                {
                    __instance.icon.sprite = EventDefinition.GetIcon(baseEvent.info);
                }
            }
        }

        [HarmonyPatch(typeof(ListItem_Event), "SetEvent")]
        internal static class ListItemEventPatch
        {
            private static void Postfix(ListItem_Event __instance, LevelEvent ev)
            {
                if (ev == null || ev.info == null || !IsOurInfo(ev.info))
                {
                    return;
                }
                bool resize = ev.info == EventDefinition.ResizeInfo;
                __instance.transform.name = resize ? EventDefinition.ResizeEventName : EventDefinition.EventName;
                __instance.itemTypeImage.sprite = EventDefinition.GetIcon(ev.info);
                var label = Traverse.Create(__instance).Field("itemName").GetValue<TMP_Text>();
                if (label != null)
                {
                    label.text = resize ? Localization.ResizeTitle : Localization.MoveTitle;
                }
            }
        }

        // 自定义枚举使用英文标签，避免依赖游戏本地化表。
        [HarmonyPatch(typeof(PropertyControl_Toggle), "EnumSetup")]
        internal static class EnumSetupPatch
        {
            private static void Postfix(PropertyControl_Toggle __instance, string enumTypeString)
            {
                if (__instance.propertyInfo == null ||
                    !IsOurInfo(__instance.propertyInfo.levelEventInfo))
                {
                    return;
                }
                var labels = EventDefinition.GetEnumLabels(enumTypeString);
                if (labels == null)
                {
                    return;
                }
                var dropdown = __instance.dropdown;
                if (dropdown != null && dropdown.items != null && dropdown.items.Count == labels.Count)
                {
                    for (int i = 0; i < dropdown.items.Count; i++)
                    {
                        var item = dropdown.items[i];
                        item.useCustomLabel = true;
                        item.customLabel = labels[i];
                        item.ResetVisuals();
                    }
                    dropdown.useCustomLabels = true;
                    dropdown.customLabels = labels;
                    dropdown.localizeEnumStrings = false;
                }
                if (__instance.buttonsDict != null)
                {
                    foreach (var pair in __instance.buttonsDict)
                    {
                        int index = __instance.enumValList.IndexOf(pair.Key);
                        if (index >= 0 && index < labels.Count)
                        {
                            var text = pair.Value.GetComponentInChildren<TMP_Text>();
                            if (text != null)
                            {
                                text.text = labels[index];
                            }
                        }
                    }
                }
            }
        }

        // 运行时把自定义事件挂到对应的 ffx 效果上，复用游戏的起播/暂停/重播流程。
        [HarmonyPatch(typeof(scnGame), "ApplyEvent")]
        internal static class ApplyEventPatch
        {
            private static void Postfix(LevelEvent evnt, float bpm, float pitch, List<scrFloor> floors, float offset,
                int? customFloorID, ref ffxPlusBase __result)
            {
                if (evnt == null || evnt.info == null || floors == null)
                {
                    return;
                }
                if (evnt.info == EventDefinition.Info)
                {
                    __result = CreateEffect<WindowMoveEffect>(evnt, bpm, pitch, floors, offset, customFloorID);
                }
                else if (evnt.info == EventDefinition.ResizeInfo)
                {
                    __result = CreateEffect<WindowResizeEffect>(evnt, bpm, pitch, floors, offset, customFloorID);
                }
            }

            private static T CreateEffect<T>(LevelEvent evnt, float bpm, float pitch, List<scrFloor> floors,
                float offset, int? customFloorID) where T : ffxPlusBase
            {
                int id = customFloorID ?? evnt.floor;
                if (id < 0 || id >= floors.Count)
                {
                    return null;
                }
                var floor = floors[id];
                var effect = floor.gameObject.AddComponent<T>();
                effect.floorID = id;
                effect.floors = floors;
                effect.crotchet = 60f / (bpm * pitch * floor.speed);
                effect.Decode(evnt);
                floor.plusEffects.Add(effect);
                evnt.TryGet<float>("angleOffset", out var angleOffset);
                effect.SetStartTime(bpm, angleOffset + offset);
                effect.sourceLevelEvent = evnt;
                return effect;
            }
        }

        // 在开始播放前记录窗口基准，避免第一个事件触发时窗口尺寸已经异常。
        [HarmonyPatch(typeof(scnEditor), "Play")]
        internal static class PlayBaselinePatch
        {
            private static void Prefix(scnEditor __instance)
            {
                if (__instance != null)
                {
                    WindowDanceController.PrepareForPlay(__instance.events);
                }
            }
        }

        [HarmonyPatch(typeof(scnGame), "ApplyEventsToFloors",
            new[] { typeof(List<scrFloor>), typeof(LevelData), typeof(scrLevelMaker), typeof(List<LevelEvent>) })]
        internal static class LoadBaselinePatch
        {
            private static void Prefix(List<LevelEvent> events)
            {
                if (!ADOBase.isLevelEditor)
                {
                    WindowDanceController.PrepareForPlay(events);
                }
            }
        }

        // 曲线编辑窗口获得鼠标时，暂停编辑器的鼠标拖拽和选轨行为。
        [HarmonyPatch(typeof(scnEditor), "HandleMouseActions")]
        internal static class CurveEditorInputPatch
        {
            private static bool Prefix()
            {
                return !CurveEditorWindow.BlocksGameInput;
            }
        }

        // 关卡重开或退出播放时恢复窗口。
        [HarmonyPatch(typeof(scrVfxPlus), "Reset")]
        internal static class VfxResetPatch
        {
            private static void Postfix()
            {
                WindowDanceController.ResetAll();
            }
        }
    }

    // 游戏的事件栏每个分类最多容纳 11 个按钮，而且没有翻页功能。
    // 这里在枚举之外新建一个分类页签，专门放置窗口事件。
    internal static class CustomCategoryIntegration
    {
        internal const int CustomCategoryId = 8;
        internal static readonly LevelEventCategory CustomCategory = (LevelEventCategory)CustomCategoryId;

        [HarmonyPatch(typeof(scnEditor), "LoadEditorProperties")]
        internal static class LoadEditorPropertiesPatch
        {
            private static void Postfix(scnEditor __instance)
            {
                try
                {
                    Ensure(__instance);
                }
                catch (Exception ex)
                {
                    Main.Warn("创建窗口事件分类失败: " + ex);
                }
            }
        }

        // 自定义说明文字使用灰色小字，并位于参数列表底部。
        [HarmonyPatch(typeof(PropertyControl_Note), "Setup")]
        internal static class NoteStylePatch
        {
            private static void Postfix(PropertyControl_Note __instance)
            {
                if (__instance.propertyInfo == null || __instance.propertyInfo.noteKey == null)
                {
                    return;
                }
                if (!__instance.propertyInfo.noteKey.StartsWith("wm.note.", StringComparison.Ordinal))
                {
                    return;
                }
                var text = __instance.noteText;
                if (text == null)
                {
                    return;
                }
                text.enableAutoSizing = false;
                text.fontSize = 16f;
                text.color = new Color(0.62f, 0.62f, 0.62f, 1f);
            }
        }

        // SetEnabled 会在 Setup 之后把文字颜色重置为白色，这里再覆盖一次。
        [HarmonyPatch(typeof(PropertyControl), "SetEnabled")]
        internal static class NoteStyleEnabledPatch
        {
            private static void Postfix(PropertyControl __instance)
            {
                var note = __instance as PropertyControl_Note;
                if (note == null || note.propertyInfo == null || note.propertyInfo.noteKey == null)
                {
                    return;
                }
                if (!note.propertyInfo.noteKey.StartsWith("wm.note.", StringComparison.Ordinal))
                {
                    return;
                }
                if (note.noteText == null)
                {
                    return;
                }
                note.noteText.enableAutoSizing = false;
                note.noteText.fontSize = 16f;
                note.noteText.color = new Color(0.62f, 0.62f, 0.62f, 1f);
            }
        }

        // 自定义分类不在枚举里，给它的提示文本补一个本地化结果。
        [HarmonyPatch(typeof(RDString), "Get")]
        internal static class CategoryLabelPatch
        {
            private static bool Prefix(string key, ref string __result)
            {
                if (key == "editor." + CustomCategoryId)
                {
                    __result = Localization.CategoryName;
                    return false;
                }
                if (key == "editor." + (int)EventDefinition.ResizeCarrier)
                {
                    __result = Localization.ResizeTitle;
                    return false;
                }
                if (key == "wm.note.move")
                {
                    __result = Localization.MoveNote;
                    return false;
                }
                if (key == "wm.note.resize")
                {
                    __result = Localization.ResizeNote;
                    return false;
                }
                if (key == "wm.note.curve")
                {
                    __result = Localization.CurveHint;
                    return false;
                }
                return true;
            }
        }

        private static void Ensure(scnEditor editor)
        {
            if (editor == null || editor.eventButtons == null)
            {
                return;
            }

            bool created = false;
            if (!editor.eventButtons.TryGetValue(CustomCategory, out var buttons) || buttons == null)
            {
                buttons = new List<LevelEventButton>();
                editor.eventButtons[CustomCategory] = buttons;
            }
            // 编辑器重载时旧按钮可能已经被销毁，先清理失效引用。
            int removed = buttons.RemoveAll(b => b == null);
            if (removed > 0)
            {
                created = true;
            }

            var tabsField = AccessTools.Field(typeof(scnEditor), "categoryTabs");
            var tabs = tabsField?.GetValue(editor) as List<CategoryTab>;
            tabs?.RemoveAll(t => t == null);
            bool hasTab = tabs != null && tabs.Exists(t => t.levelEventCategory == CustomCategory);
            if (!hasTab)
            {
                CreateTab(editor, tabs);
                created = true;
            }

            if (!buttons.Exists(b => b.info == EventDefinition.Info))
            {
                CreateButton(editor, buttons, EventDefinition.Carrier);
                created = true;
            }
            if (!buttons.Exists(b => b.info == EventDefinition.ResizeInfo))
            {
                CreateButton(editor, buttons, EventDefinition.ResizeCarrier);
                created = true;
            }

            if (created)
            {
                Main.Info("已创建窗口事件分类。");
            }
        }

        private static void CreateTab(scnEditor editor, List<CategoryTab> tabs)
        {
            if (editor.prefab_eventCategoryTab == null || editor.levelEventsBarCategories == null)
            {
                return;
            }
            var tabObject = UnityEngine.Object.Instantiate(editor.prefab_eventCategoryTab,
                editor.levelEventsBarCategories);
            var tabRect = tabObject.GetComponent<RectTransform>();
            tabRect.anchoredPosition = new Vector2(tabRect.anchoredPosition.x, 55f);
            var tab = tabObject.GetComponent<CategoryTab>();
            if (tab == null)
            {
                return;
            }
            tab.levelEventCategory = CustomCategory;
            if (tab.icon != null)
            {
                tab.icon.sprite = EventDefinition.CategoryIcon;
            }
            if (tab.button != null)
            {
                tab.button.name = "WindowEvents";
            }
            tab.SetSelected(false);
            tabs?.Add(tab);
        }

        private static void CreateButton(scnEditor editor, List<LevelEventButton> buttons, LevelEventType type)
        {
            if (editor.prefab_levelEventButton == null || editor.levelEventsBarButtons == null)
            {
                return;
            }
            var buttonObject = UnityEngine.Object.Instantiate(editor.prefab_levelEventButton,
                editor.levelEventsBarButtons);
            var buttonRect = buttonObject.GetComponent<RectTransform>();
            buttonRect.anchoredPosition = new Vector2(buttonRect.sizeDelta.x * buttons.Count,
                buttonRect.anchoredPosition.y);
            var button = buttonObject.GetComponent<LevelEventButton>();
            if (button == null)
            {
                return;
            }
            button.Init(type, 0, 0);
            buttons.Add(button);
            button.gameObject.SetActive(false);
        }
    }

    internal static class WindowEventInspector
    {
        private static readonly AccessTools.FieldRef<InspectorPanel, bool> ShowingPanel =
            AccessTools.FieldRefAccess<InspectorPanel, bool>("showingPanel");

        public static void Show(InspectorPanel panel, int eventIndex, LevelEventType eventType)
        {
            var editor = ADOBase.editor;
            if (editor == null)
            {
                return;
            }
            bool resize = eventType == EventDefinition.ResizeCarrier;
            string title = resize ? Localization.ResizeTitle : Localization.MoveTitle;

            ShowingPanel(panel) = true;
            try
            {
                if (editor.cacheSelectedEventIndex > 0)
                {
                    eventIndex = editor.cacheSelectedEventIndex;
                }
                else
                {
                    panel.cacheEventIndex = eventIndex;
                }
                panel.selectedEventType = eventType;
                // 原版 ShowPanel 会记录上次事件类型，我们绕过原版后需要手动同步，
                // 否则再次选中该格子时会按旧类型显示 Flash 等原版页签。
                AccessTools.Field(typeof(InspectorPanel), "cacheSelectedEventType")?.SetValue(null, eventType);

                using (new SaveStateScope(editor, clearRedo: false, dataHasChanged: false))
                {
                    PropertiesPanel target = null;
                    foreach (var candidate in panel.panelsList)
                    {
                        bool ours = candidate != null && candidate.levelEventType == eventType;
                        candidate.gameObject.SetActive(ours);
                        if (candidate.tabContainer != null)
                        {
                            candidate.tabContainer.gameObject.SetActive(ours);
                        }
                        if (ours)
                        {
                            target = candidate;
                            panel.titleCanvas.SetActive(true);
                        }
                    }

                    panel.title.text = title;

                    List<LevelEvent> list = GetEvents(editor, eventType);
                    int count = list.Count;
                    if (eventIndex < 0 || eventIndex >= count)
                    {
                        eventIndex = 0;
                        panel.cacheEventIndex = 0;
                    }
                    LevelEvent evnt = eventIndex >= 0 && eventIndex < count ? list[eventIndex] : null;
                    if (target == null || evnt == null)
                    {
                        return;
                    }

                    panel.selectedEvent = evnt;
                    panel.selectedEventType = evnt.eventType;
                    target.SetProperties(evnt);

                    foreach (RectTransform rect in panel.tabs)
                    {
                        var tab = rect.GetComponent<InspectorTab>();
                        if (tab == null)
                        {
                            continue;
                        }
                        if (tab.levelEventType == eventType)
                        {
                            tab.SetSelected(true);
                            tab.eventIndex = eventIndex;
                            if (tab.cycleButtons != null && tab.cycleButtons.text != null)
                            {
                                tab.cycleButtons.text.text = $"{eventIndex + 1}/{count}";
                            }
                        }
                        else
                        {
                            tab.SetSelected(false);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Main.Warn("显示窗口事件面板失败: " + ex);
            }
            finally
            {
                ShowingPanel(panel) = false;
            }
        }

        private static List<LevelEvent> GetEvents(scnEditor editor, LevelEventType eventType)
        {
            var result = new List<LevelEvent>();
            if (editor == null || editor.selectedFloors == null || editor.selectedFloors.Count == 0 ||
                editor.events == null)
            {
                return result;
            }
            int floor = editor.selectedFloors[0].seqID;
            foreach (var ev in editor.events)
            {
                if (ev != null && ev.floor == floor && ev.eventType == eventType)
                {
                    result.Add(ev);
                }
            }
            return result;
        }
    }
}
