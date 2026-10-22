using System;
using System.Collections.Generic;
using ADOFAI;
using DG.Tweening;
using UnityEngine;
using Object = UnityEngine.Object;

namespace RDWindowEvent
{
    // 把 Rhythm Doctor 的 WindowDancer 逻辑搬到 ADOFAI 的游戏窗口上。
    public sealed class WindowDanceController : MonoBehaviour
    {
        private static WindowDanceController _instance;

        public static WindowDanceController Instance
        {
            get
            {
                Ensure();
                return _instance;
            }
        }

        public static void Ensure()
        {
            if (!Application.isPlaying || _instance != null)
            {
                return;
            }
            var go = new GameObject("RDWindowEventController");
            Object.DontDestroyOnLoad(go);
            _instance = go.AddComponent<WindowDanceController>();
        }

        public static void ResetAll()
        {
            if (_instance != null)
            {
                _instance.ResetSession();
            }
        }

        public static void PrepareForPlay(IEnumerable<LevelEvent> events)
        {
            if (events == null)
            {
                return;
            }
            bool hasWindowEvent = false;
            foreach (var ev in events)
            {
                if (ev != null && EventDefinition.IsOurInfo(ev.info))
                {
                    hasWindowEvent = true;
                    break;
                }
            }
            if (!hasWindowEvent)
            {
                return;
            }
            Instance?.BeginBaselineCapture();
        }

        private enum Preset
        {
            None = -1,
            Move,
            Sway,
            Wrap,
            Ellipse,
            ShakePer
        }

        private IntPtr _hwnd = IntPtr.Zero;
        private float _nextWindowSearch;
        private bool _warnedFullscreen;
        private bool _warnedMaximized;

        private bool _session;
        private bool _hasOriginal;
        private Win32Window.Rect _originalRect;
        private bool _hasBaseline;
        private bool _baselinePending;
        private float _baselineUntil;
        private int _baselineRetries;
        private Win32Window.Rect _baselineRect;

        private WindowMoveEffect _pending;
        private WindowResizeEffect _pendingResize;
        private float _pendingUntil;
        private int _pendingRetries;

        private bool _resizing;
        private bool _resizeApplied;
        private float _resizeElapsed;
        private float _resizeDuration;
        private Ease _resizeEase = Ease.Linear;
        private Win32Window.Rect _resizeFrom;
        private int _resizeToWidth;
        private int _resizeToHeight;
        private float _resizePivotX;
        private float _resizePivotY;
        private float _resizeAnchorX;
        private float _resizeAnchorY;
        private bool _zoomApplied;
        private bool _zoomActive;
        private float _zoomFrom;
        private float _zoomTo;
        private int _observedWidth;
        private int _observedHeight;

        private bool _pausedLast;
        private bool _hasState;

        private Preset _preset = Preset.None;
        private Vector2 _baseNorm = new Vector2(0.5f, 0.5f);
        private Vector2 _amp = Vector2.zero;
        private float _angleRad;
        private float _speedPerSec;
        private float _freqPerSec;
        private float _periodSec;
        private float _phase;
        private Ease _ease = Ease.Linear;
        private Ease _subEase = Ease.Linear;
        private EasingType _easingType = EasingType.Repeat;
        private ReferenceType _reference = ReferenceType.Center;
        private AnimationCurve _curve;
        private AnimationCurve _moveCurve;
        private AnimationCurve _resizeCurve;

        private Vector2 _moveFrom = new Vector2(0.5f, 0.5f);
        private Vector2 _moveTo = new Vector2(0.5f, 0.5f);
        private float _moveElapsed;
        private float _moveDuration;
        private Ease _moveEase = Ease.Linear;

        private Vector2 _transitionFrom;
        private float _transitionElapsed;
        private float _transitionDuration;

        private float _lastShakeTime;
        private Vector2 _lastShakeOffset;
        private Vector2 _currentShakeTarget;

        private Vector2 _currentNorm = new Vector2(0.5f, 0.5f);

        private void BeginBaselineCapture()
        {
            ResetSession();
            _hasBaseline = false;
            _baselinePending = true;
            _baselineRetries = 0;
            _baselineUntil = Time.realtimeSinceStartup + 0.3f;
            if (Screen.fullScreen || Screen.fullScreenMode != FullScreenMode.Windowed)
            {
                Screen.fullScreen = false;
                _baselineUntil = Time.realtimeSinceStartup + 0.4f;
            }
        }

        private void UpdateBaselineCapture()
        {
            if (Time.realtimeSinceStartup < _baselineUntil)
            {
                return;
            }
            if (Screen.fullScreen || Screen.fullScreenMode != FullScreenMode.Windowed)
            {
                Screen.fullScreen = false;
                _baselineUntil = Time.realtimeSinceStartup + 0.3f;
                _baselineRetries++;
                if (_baselineRetries > 5)
                {
                    Main.Warn("无法切换到窗口模式，未能记录窗口缩放基准。");
                    _baselinePending = false;
                }
                return;
            }
            if (CaptureBaselineNow())
            {
                _baselinePending = false;
                Main.Info($"已记录窗口缩放基准: {_baselineRect.Width}x{_baselineRect.Height}");
                return;
            }

            _baselineRetries++;
            if (_baselineRetries > 5)
            {
                Main.Warn("未能记录窗口缩放基准，将在第一个窗口事件时重试。");
                _baselinePending = false;
                return;
            }
            _baselineUntil = Time.realtimeSinceStartup + 0.25f;
        }

        private bool CaptureBaselineNow()
        {
            if (!EnsureWindow() || !Win32Window.TryGetRect(_hwnd, out var rect))
            {
                return false;
            }
            if (rect.Width < 100 || rect.Height < 100)
            {
                return false;
            }
            _baselineRect = rect;
            _hasBaseline = true;
            _observedWidth = rect.Width;
            _observedHeight = rect.Height;
            return true;
        }

        private void Update()
        {
            if (!Application.isPlaying)
            {
                return;
            }

            if (_baselinePending)
            {
                UpdateBaselineCapture();
                return;
            }

            if (_pending != null || _pendingResize != null)
            {
                if (Time.realtimeSinceStartup < _pendingUntil)
                {
                    return;
                }
                if (Screen.fullScreen || Screen.fullScreenMode != FullScreenMode.Windowed)
                {
                    Screen.fullScreen = false;
                    _pendingUntil = Time.realtimeSinceStartup + 0.3f;
                    _pendingRetries++;
                    if (_pendingRetries > 5)
                    {
                        Main.Warn("窗口仍处于全屏/无边框状态，无法改变窗口。");
                        _pending = null;
                        _pendingResize = null;
                    }
                    return;
                }
                var effect = _pending;
                var resize = _pendingResize;
                _pending = null;
                _pendingResize = null;
                if (effect != null)
                {
                    ApplyInternal(effect);
                }
                if (resize != null)
                {
                    ApplyResizeInternal(resize);
                }
            }

            if (!_session)
            {
                return;
            }

            var controller = ADOBase.controller;
            if (controller == null)
            {
                ResetSession();
                return;
            }

            if (controller.paused)
            {
                if (!_pausedLast)
                {
                    RestoreOriginal();
                    _pausedLast = true;
                }
                return;
            }

            bool resumed = _pausedLast;
            _pausedLast = false;
            DetectExternalResize();

            var conductor = ADOBase.conductor;
            if (conductor == null || !conductor.hasSongStarted)
            {
                return;
            }

            if (!_hasState && !_resizing && !resumed)
            {
                return;
            }

            float dt = Time.unscaledDeltaTime;
            Advance(dt);
            if (resumed && _resizeApplied)
            {
                _resizeElapsed = _resizeDuration;
                ApplyResizeFrame();
            }
            if (_resizing)
            {
                ApplyResizeFrame();
            }
            if (_hasState)
            {
                ApplyCenter(ComputeCenter());
            }
        }

        public void Apply(WindowMoveEffect effect)
        {
            if (effect == null || !Application.isPlaying)
            {
                return;
            }

            if (Screen.fullScreen || Screen.fullScreenMode != FullScreenMode.Windowed)
            {
                _pending = effect;
                _pendingRetries = 0;
                _pendingUntil = Time.realtimeSinceStartup + 0.35f;
                if (!_warnedFullscreen)
                {
                    Main.Info("检测到全屏，按 Rhythm Doctor 的行为切换为窗口模式。");
                    _warnedFullscreen = true;
                }
                Screen.fullScreen = false;
                return;
            }

            ApplyInternal(effect);
        }

        public void ApplyResize(WindowResizeEffect effect)
        {
            if (effect == null || !Application.isPlaying)
            {
                return;
            }

            if (Screen.fullScreen || Screen.fullScreenMode != FullScreenMode.Windowed)
            {
                _pendingResize = effect;
                _pendingRetries = 0;
                _pendingUntil = Time.realtimeSinceStartup + 0.35f;
                if (!_warnedFullscreen)
                {
                    Main.Info("检测到全屏，按 Rhythm Doctor 的行为切换为窗口模式。");
                    _warnedFullscreen = true;
                }
                Screen.fullScreen = false;
                return;
            }

            ApplyResizeInternal(effect);
        }

        private void ApplyResizeInternal(WindowResizeEffect effect)
        {
            if (effect == null || !EnsureSession())
            {
                return;
            }
            if (!Win32Window.TryGetRect(_hwnd, out var current))
            {
                return;
            }

            // 移动动画结束后由缩放事件接管窗口，避免每帧的居中逻辑覆盖锚点缩放。
            if (_preset == Preset.Move)
            {
                float moveT = _moveDuration <= 0f ? 1f : _moveElapsed / _moveDuration;
                if (moveT >= 1f)
                {
                    _hasState = false;
                }
            }

            float baseWidth;
            float baseHeight;
            if (Main.Config != null && Main.Config.ResizeBase == ResizeBaseMode.FixedResolution)
            {
                baseWidth = Mathf.Max(1f, Main.Config.BaseWidth);
                baseHeight = Mathf.Max(1f, Main.Config.BaseHeight);
            }
            else
            {
                baseWidth = Mathf.Max(1f, _originalRect.Width);
                baseHeight = Mathf.Max(1f, _originalRect.Height);
            }
            float scaleX = float.IsNaN(effect.scale.x) ? current.Width / baseWidth : effect.scale.x;
            float scaleY = float.IsNaN(effect.scale.y) ? current.Height / baseHeight : effect.scale.y;
            scaleX = Mathf.Clamp(scaleX, 0.1f, 2f);
            scaleY = Mathf.Clamp(scaleY, 0.1f, 2f);

            _resizeFrom = current;
            _resizeToWidth = Mathf.Max(1, Mathf.RoundToInt(baseWidth * scaleX));
            _resizeToHeight = Mathf.Max(1, Mathf.RoundToInt(baseHeight * scaleY));
            _resizeDuration = Mathf.Max(0f, effect.durationBeats) * Mathf.Max(effect.crotchet, 0.0001f);
            _resizeElapsed = 0f;
            _resizeEase = effect.ease;
            _resizeCurve = effect.customEase ? effect.customCurve : null;

            ComputeResizeAnchor(effect, current, out _resizePivotX, out _resizePivotY,
                out _resizeAnchorX, out _resizeAnchorY);

            _zoomActive = false;
            if (effect.zoomMode != ZoomMode.None)
            {
                var cam = ADOBase.controller?.camy;
                if (cam != null)
                {
                    _zoomActive = true;
                    _zoomApplied = true;
                    _zoomFrom = cam.zoomSize;
                    _zoomTo = effect.zoomMode == ZoomMode.Fill
                        ? Mathf.Max(scaleX, scaleY)
                        : Mathf.Min(scaleX, scaleY);
                }
            }

            _resizeApplied = true;
            _resizing = true;
            ApplyResizeFrame();
        }

        private static void ComputeResizeAnchor(WindowResizeEffect effect, Win32Window.Rect current,
            out float pivotX, out float pivotY, out float anchorX, out float anchorY)
        {
            if (effect.pivotMode == PivotMode.AnchorEdge)
            {
                pivotX = effect.anchorType == PivotAnchorType.LeftEdge
                    ? 0f
                    : effect.anchorType == PivotAnchorType.RightEdge ? 1f : 0.5f;
                pivotY = effect.anchorType == PivotAnchorType.BottomEdge
                    ? 0f
                    : effect.anchorType == PivotAnchorType.TopEdge ? 1f : 0.5f;
            }
            else
            {
                pivotX = Mathf.Clamp(effect.pivot.x / 100f, 0f, 1f);
                pivotY = Mathf.Clamp(effect.pivot.y / 100f, 0f, 1f);
            }

            anchorX = current.Left + current.Width * pivotX;
            anchorY = current.Top + current.Height * (1f - pivotY);
        }

        private void ApplyResizeFrame()
        {
            if (!Win32Window.IsValid(_hwnd))
            {
                _resizing = false;
                return;
            }
            float t = _resizeDuration <= 0f ? 1f : Mathf.Clamp01(_resizeElapsed / _resizeDuration);
            float eased = EasedValue(0f, 1f, t, _resizeEase, _resizeCurve);
            int width = Mathf.Max(1, Mathf.RoundToInt(
                Mathf.LerpUnclamped(_resizeFrom.Width, _resizeToWidth, eased)));
            int height = Mathf.Max(1, Mathf.RoundToInt(
                Mathf.LerpUnclamped(_resizeFrom.Height, _resizeToHeight, eased)));
            int left = Mathf.RoundToInt(_resizeAnchorX - width * _resizePivotX);
            int top = Mathf.RoundToInt(_resizeAnchorY - height * (1f - _resizePivotY));

            Win32Window.MoveAndResize(_hwnd, new Win32Window.Rect
            {
                Left = left,
                Top = top,
                Right = left + width,
                Bottom = top + height
            });
            _observedWidth = width;
            _observedHeight = height;

            if (_zoomActive)
            {
                var cam = ADOBase.controller?.camy;
                if (cam != null)
                {
                    cam.zoomSize = Mathf.LerpUnclamped(_zoomFrom, _zoomTo, eased);
                }
            }

            if (t >= 1f)
            {
                _resizing = false;
            }
        }

        private bool EnsureSession()
        {
            if (!EnsureWindow())
            {
                return false;
            }
            if (_session)
            {
                return true;
            }
            if (!_hasBaseline && !CaptureBaselineNow())
            {
                Main.Warn("无法获取有效的窗口基准尺寸，本次窗口事件已跳过。");
                return false;
            }
            _originalRect = _baselineRect;
            _session = true;
            _hasOriginal = true;
            _currentNorm = GetCenterNorm();
            _hasState = false;
            _preset = Preset.None;
            _observedWidth = _originalRect.Width;
            _observedHeight = _originalRect.Height;
            return true;
        }

        private void DetectExternalResize()
        {
            if (!_session || _resizing || !Win32Window.TryGetRect(_hwnd, out var current))
            {
                return;
            }
            if (Mathf.Abs(current.Width - _observedWidth) <= 2 &&
                Mathf.Abs(current.Height - _observedHeight) <= 2)
            {
                return;
            }
            if (current.Width < 100 || current.Height < 100)
            {
                return;
            }

            _observedWidth = current.Width;
            _observedHeight = current.Height;

            if (Main.Config == null || !Main.Config.RebaseOnResolutionChange)
            {
                return;
            }

            // 用户在游戏内改了分辨率：恢复基准也要跟随，避免暂停/重开时退回旧分辨率。
            _baselineRect = current;
            _hasBaseline = true;
            if (_hasOriginal)
            {
                _originalRect = current;
            }

            if (Main.Config.ResizeBase == ResizeBaseMode.FixedResolution)
            {
                return;
            }

            // 非固定基准时，把新窗口尺寸作为新的 1.0 基准。
            _resizeApplied = false;
            Main.Info("检测到窗口分辨率变化，缩放基准已更新。");
        }

        private void ApplyInternal(WindowMoveEffect effect)
        {
            if (effect == null || !EnsureSession())
            {
                return;
            }

            float crotchet = Mathf.Max(effect.crotchet, 0.0001f);
            float duration = Mathf.Max(0f, effect.durationBeats) * crotchet;

            var target = ToPreset(effect.preset);
            bool samePreset = _hasState && _preset == target;
            bool keep = effect.samePreset == SamePresetBehavior.Keep;
            bool continuePreset = samePreset && keep;

            // 与 Rhythm Doctor 一致：Keep 时沿用同一预设的速度与频率。
            if (!continuePreset)
            {
                _speedPerSec = effect.speed / 100f / crotchet;
                _freqPerSec = effect.frequency / crotchet;
            }
            if (!continuePreset || effect.preset != WindowDancePreset.Wrap)
            {
                _angleRad = effect.angle * Mathf.Deg2Rad;
            }
            _periodSec = effect.period * crotchet;
            _ease = effect.ease;
            _subEase = effect.subEase;
            _easingType = effect.easingType;
            _reference = effect.reference;
            _curve = effect.customEase ? effect.customCurve : null;
            _moveCurve = effect.preset == WindowDancePreset.Move ? _curve : null;

            Vector2 current = GetCenterNorm();
            Vector2 baseNorm = new Vector2(
                float.IsNaN(effect.position.x) ? current.x : effect.position.x / 100f,
                float.IsNaN(effect.position.y) ? current.y : effect.position.y / 100f);

            ApplyAmplitude(effect);

            switch (effect.preset)
            {
                case WindowDancePreset.Move:
                    _preset = Preset.Move;
                    _moveFrom = current;
                    _moveTo = baseNorm;
                    _moveElapsed = 0f;
                    _moveDuration = duration;
                    _moveEase = effect.ease;
                    _hasState = true;
                    break;

                case WindowDancePreset.Sway:
                case WindowDancePreset.Wrap:
                case WindowDancePreset.Ellipse:
                case WindowDancePreset.ShakePer:
                {
                    if (!continuePreset)
                    {
                        _phase = 0f;
                        ResetShake();
                    }
                    _preset = target;
                    _baseNorm = baseNorm;
                    _transitionFrom = current * GetScreenSize();
                    _transitionElapsed = 0f;
                    _transitionDuration = duration;
                    _hasState = true;
                    break;
                }

            }
        }

        private void ApplyAmplitude(WindowMoveEffect effect)
        {
            float scalar = Mathf.Max(0f, effect.amplitude) / 100f;
            switch (effect.preset)
            {
                case WindowDancePreset.Ellipse:
                    _amp = effect.useCircle
                        ? new Vector2(scalar, scalar)
                        : new Vector2(Safe(effect.amplitudeVector.x) / 100f, Safe(effect.amplitudeVector.y) / 100f);
                    break;
                case WindowDancePreset.ShakePer:
                    _amp = new Vector2(Safe(effect.amplitudeVector.x) / 100f, Safe(effect.amplitudeVector.y) / 100f);
                    break;
                default:
                    _amp = new Vector2(scalar, scalar);
                    break;
            }
        }

        private static float Safe(float value)
        {
            return float.IsNaN(value) ? 0f : value;
        }

        private static Preset ToPreset(WindowDancePreset preset)
        {
            switch (preset)
            {
                case WindowDancePreset.Sway: return Preset.Sway;
                case WindowDancePreset.Wrap: return Preset.Wrap;
                case WindowDancePreset.Ellipse: return Preset.Ellipse;
                case WindowDancePreset.ShakePer: return Preset.ShakePer;
                default: return Preset.Move;
            }
        }

        private void Advance(float dt)
        {
            _phase += dt;
            _moveElapsed += dt;
            if (_transitionElapsed < _transitionDuration)
            {
                _transitionElapsed = Mathf.Min(_transitionElapsed + dt, _transitionDuration);
            }
            if (_resizing && _resizeElapsed < _resizeDuration)
            {
                _resizeElapsed = Mathf.Min(_resizeElapsed + dt, _resizeDuration);
            }
        }

        private Vector2 ComputeCenter()
        {
            Vector2 screen = GetScreenSize();
            Vector2 view = GetViewSize();
            Vector2 center;

            switch (_preset)
            {
                case Preset.Move:
                {
                    float t = _moveDuration <= 0f ? 1f : Mathf.Clamp01(_moveElapsed / _moveDuration);
                    float eased = EasedValue(0f, 1f, t, _moveEase, _moveCurve);
                    Vector2 norm = Vector2.LerpUnclamped(_moveFrom, _moveTo, eased);
                    center = new Vector2(norm.x * screen.x, norm.y * screen.y);
                    if (_reference == ReferenceType.Edge)
                    {
                        center += new Vector2((0.5f - norm.x) * view.x, (0.5f - norm.y) * view.y);
                    }
                    break;
                }

                case Preset.Sway:
                {
                    float cycles = _phase * _freqPerSec;
                    float frac = Mathf.Repeat(cycles, 1f);
                    bool odd = Mathf.FloorToInt(cycles) % 2 != 0;
                    Vector2 vec = _amp.x * Diagonal(_angleRad, screen) * AngleToVec(_angleRad);
                    if (_easingType == EasingType.Mirror)
                    {
                        if (odd)
                        {
                            frac = 1f - frac;
                        }
                        vec *= DOVirtual.EasedValue(0f, 1f, frac, _subEase);
                    }
                    else
                    {
                        float from = odd ? 1f : 0f;
                        vec *= DOVirtual.EasedValue(from, 1f - from, frac, _subEase);
                    }
                    center = new Vector2(_baseNorm.x * screen.x, _baseNorm.y * screen.y) + vec;
                    break;
                }

                case Preset.Wrap:
                {
                    Vector2 bounds = screen + view;
                    Vector2 dir = AngleToVec(_angleRad);
                    Vector2 perp = AngleToVec(_angleRad + Mathf.PI / 2f);
                    Vector2 sway = Mathf.Sin(Mathf.PI * 2f * _phase * _freqPerSec) * (_amp.x / 2f) *
                                   Diagonal(_angleRad + Mathf.PI / 2f, bounds) * perp;
                    Vector2 basePx = new Vector2(_baseNorm.x * screen.x, _baseNorm.y * screen.y) + view / 2f;
                    Vector2 velocity = Diagonal(_angleRad, bounds) * dir * (_speedPerSec * _phase);
                    Vector2 moved = basePx + velocity + sway;
                    center = new Vector2(Mathf.Repeat(moved.x, bounds.x), Mathf.Repeat(moved.y, bounds.y)) - view / 2f;
                    break;
                }

                case Preset.Ellipse:
                {
                    float angle = _angleRad - Mathf.PI * 2f * _speedPerSec * _phase;
                    center = new Vector2(_baseNorm.x * screen.x, _baseNorm.y * screen.y);
                    center += new Vector2(_amp.x * screen.x * Mathf.Cos(angle), _amp.y * screen.y * Mathf.Sin(angle));
                    break;
                }

                case Preset.ShakePer:
                {
                    if ((_phase - _lastShakeTime) * _freqPerSec >= 1f)
                    {
                        _lastShakeOffset = _currentShakeTarget;
                        _currentShakeTarget = UnityEngine.Random.insideUnitCircle;
                        _lastShakeTime = _phase;
                    }
                    float period = Mathf.Max(_periodSec, 0.001f);
                    float life = Mathf.Clamp01((_phase - _lastShakeTime) / period);
                    float t = DOVirtual.EasedValue(0f, 1f, life, _subEase);
                    Vector2 offset = new Vector2(
                        Mathf.LerpUnclamped(_lastShakeOffset.x, _currentShakeTarget.x, t) * _amp.x * screen.x,
                        Mathf.LerpUnclamped(_lastShakeOffset.y, _currentShakeTarget.y, t) * _amp.y * screen.y);
                    center = new Vector2(_baseNorm.x * screen.x, _baseNorm.y * screen.y) + offset;
                    break;
                }

                default:
                    center = new Vector2(_baseNorm.x * screen.x, _baseNorm.y * screen.y);
                    break;
            }

            if (_preset != Preset.Move && _transitionDuration > 0f && _transitionElapsed < _transitionDuration)
            {
                float t = Mathf.Clamp01(_transitionElapsed / _transitionDuration);
                float eased = EasedValue(0f, 1f, t, _ease, _curve);
                center = Vector2.LerpUnclamped(_transitionFrom, center, eased);
            }

            return center;
        }

        private void ApplyCenter(Vector2 center)
        {
            if (!EnsureWindow())
            {
                return;
            }
            if (!Win32Window.TryGetRect(_hwnd, out var windowRect) ||
                !Win32Window.TryGetMonitorRect(_hwnd, out var monitor))
            {
                return;
            }
            if (Win32Window.IsMaximized(_hwnd))
            {
                if (!_warnedMaximized)
                {
                    Main.Warn("窗口处于最大化状态，无法移动。");
                    _warnedMaximized = true;
                }
                return;
            }

            int left = monitor.Left + Mathf.RoundToInt(center.x - windowRect.Width / 2f);
            int top = monitor.Top + Mathf.RoundToInt(monitor.Height - center.y - windowRect.Height / 2f);
            Win32Window.Move(_hwnd, left, top);
            _currentNorm = new Vector2(center.x / monitor.Width, center.y / monitor.Height);
        }

        private bool EnsureWindow()
        {
            if (Win32Window.IsValid(_hwnd))
            {
                return true;
            }
            if (Time.realtimeSinceStartup < _nextWindowSearch)
            {
                return false;
            }
            _nextWindowSearch = Time.realtimeSinceStartup + 1f;
            _hwnd = Win32Window.FindGameWindow();
            if (_hwnd == IntPtr.Zero)
            {
                Main.Warn("未找到游戏窗口句柄。");
                return false;
            }
            return true;
        }

        private Vector2 GetCenterNorm()
        {
            if (!Win32Window.TryGetRect(_hwnd, out var rect) || !Win32Window.TryGetMonitorRect(_hwnd, out var monitor) ||
                monitor.Width <= 0 || monitor.Height <= 0)
            {
                return new Vector2(0.5f, 0.5f);
            }
            float x = (rect.Left + rect.Right) / 2f - monitor.Left;
            float y = monitor.Bottom - (rect.Top + rect.Bottom) / 2f;
            return new Vector2(x / monitor.Width, y / monitor.Height);
        }

        private Vector2 GetScreenSize()
        {
            if (!Win32Window.TryGetMonitorRect(_hwnd, out var monitor))
            {
                return new Vector2(Screen.width, Screen.height);
            }
            return new Vector2(monitor.Width, monitor.Height);
        }

        private Vector2 GetViewSize()
        {
            if (!Win32Window.TryGetRect(_hwnd, out var rect))
            {
                return new Vector2(Screen.width, Screen.height);
            }
            return new Vector2(rect.Width, rect.Height);
        }

        private void RestoreOriginal()
        {
            if (!_hasOriginal || !Win32Window.IsValid(_hwnd))
            {
                return;
            }

            var rect = _originalRect;
            if (rect.Width < 100 || rect.Height < 100)
            {
                // 基准异常时用当前显示器尺寸回退，避免恢复成 0 或极小窗口。
                if (Win32Window.TryGetMonitorRect(_hwnd, out var monitor))
                {
                    int width = Mathf.Max(640, Mathf.Min(Screen.width, monitor.Width));
                    int height = Mathf.Max(360, Mathf.Min(Screen.height, monitor.Height));
                    rect = new Win32Window.Rect
                    {
                        Left = monitor.Left + (monitor.Width - width) / 2,
                        Top = monitor.Top + (monitor.Height - height) / 2,
                        Right = monitor.Left + (monitor.Width + width) / 2,
                        Bottom = monitor.Top + (monitor.Height + height) / 2
                    };
                }
            }
            Win32Window.MoveAndResize(_hwnd, rect);
        }

        private void ResetSession()
        {
            RestoreOriginal();
            ResetZoom();
            _session = false;
            _hasOriginal = false;
            _hasState = false;
            _preset = Preset.None;
            _pausedLast = false;
            _pending = null;
            _pendingResize = null;
            _resizing = false;
            _resizeApplied = false;
            _resizeElapsed = 0f;
            _warnedMaximized = false;
        }

        private void ResetZoom()
        {
            if (!_zoomApplied)
            {
                return;
            }
            var cam = ADOBase.controller?.camy;
            if (cam != null)
            {
                cam.zoomSize = 1f;
            }
            _zoomApplied = false;
            _zoomActive = false;
        }

        private void OnApplicationQuit()
        {
            ResetZoom();
            RestoreOriginal();
        }

        private void ResetShake()
        {
            _lastShakeTime = 0f;
            _lastShakeOffset = Vector2.zero;
            _currentShakeTarget = UnityEngine.Random.insideUnitCircle;
        }

        private static float EasedValue(float from, float to, float t, Ease ease, AnimationCurve curve)
        {
            if (curve != null)
            {
                return Mathf.LerpUnclamped(from, to, curve.Evaluate(t));
            }
            return DOVirtual.EasedValue(from, to, t, ease);
        }

        private static Vector2 AngleToVec(float angle)
        {
            return new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
        }

        private static float Diagonal(float angle, Vector2 bounds)
        {
            float num = Mathf.Repeat(angle, Mathf.PI);
            float ratio = Mathf.Atan2(bounds.y, bounds.x);
            bool useHeight = Mathf.Abs(Mathf.PI / 2f - num) < Mathf.PI / 2f - ratio;
            float f = useHeight ? angle : (Mathf.PI / 2f - angle);
            return Mathf.Abs((useHeight ? bounds.y : bounds.x) / Mathf.Sin(f));
        }
    }
}
