using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace MadeYellow.WAVES.Editor
{
    public sealed class FootstepClipPreview : VisualElement
    {
        readonly IMGUIContainer _imgui;
        readonly Label _empty;
        AnimationClip _clip;
        UnityEditor.Editor _editor;
        float _length;
        float _playbackSpeed = 1f;

        public FootstepClipPreview()
        {
            AddToClassList("footstep-preview");

            _empty = new Label("Assign an animation clip to preview it.");
            _empty.AddToClassList("footstep-preview-empty");
            _empty.pickingMode = PickingMode.Ignore;

            _imgui = new IMGUIContainer(DrawPreview);
            _imgui.AddToClassList("footstep-preview-imgui");

            Add(_imgui);
            Add(_empty);

            RegisterCallback<DetachFromPanelEvent>(_ => DestroyEditor());
            RegisterCallback<AttachToPanelEvent>(_ => _imgui.MarkDirtyRepaint());
        }

        public bool NeedsRepaint => _editor != null && _editor.RequiresConstantRepaint();

        public event Action<float> TimeChanged;

        float _reportedTime = float.NaN;

        public void Seek(float time)
        {
            if (_clip == null || _editor == null)
                return;

            float length = PreviewLength;
            time = Mathf.Clamp(time, 0f, length);
            FootstepPreviewClock.Seek(_editor, time, length);
            _reportedTime = time;
            TimeChanged?.Invoke(time);
            _imgui.MarkDirtyRepaint();
        }

        public bool IsPlaying => _editor != null && FootstepPreviewClock.IsPlaying(_editor);

        public void GoToStart()
        {
            Seek(0f);
        }

        public void GoToEnd()
        {
            if (_editor != null)
                FootstepPreviewClock.SetPlaying(_editor, false);
            Seek(PreviewLength);
        }

        public void TogglePlayback()
        {
            if (_clip == null || _editor == null)
                return;

            bool playing = FootstepPreviewClock.IsPlaying(_editor);
            float length = PreviewLength;
            if (!playing &&
                length > 0f &&
                FootstepPreviewClock.TryGetTime(_editor, out float time) &&
                time >= length - 0.0001f)
            {
                FootstepPreviewClock.Seek(_editor, 0f, length);
                _reportedTime = 0f;
                TimeChanged?.Invoke(0f);
            }

            FootstepPreviewClock.SetPlaying(_editor, !playing);
            _imgui.MarkDirtyRepaint();
        }

        public void SetPlaybackSpeed(float speed)
        {
            _playbackSpeed = Mathf.Clamp(speed, 0.1f, 2f);
            if (_editor != null)
                FootstepPreviewClock.SetPlaybackSpeed(_editor, _playbackSpeed);
        }

        public void SetClip(AnimationClip clip, float length)
        {
            _length = Mathf.Max(0f, length);
            if (_clip == clip && (_clip == null || _editor != null))
            {
                UpdateEmptyState();
                return;
            }

            _clip = clip;
            _reportedTime = float.NaN;
            DestroyEditor();
            if (_clip != null)
                _editor = UnityEditor.Editor.CreateEditor(_clip);

            UpdateEmptyState();
            _imgui.MarkDirtyRepaint();
        }

        public void Tick()
        {
            if (NeedsRepaint)
                _imgui.MarkDirtyRepaint();
        }

        void UpdateEmptyState()
        {
            bool hasClip = _clip != null;
            _empty.style.display = hasClip ? DisplayStyle.None : DisplayStyle.Flex;
            _imgui.style.display = hasClip ? DisplayStyle.Flex : DisplayStyle.None;
        }

        // Unity's AvatarPreview reserves this strip for its own play button, scrubber, and speed.
        const float PreviewToolbarHeight = 21f;

        float PreviewLength => _length > 0f ? _length : (_clip != null ? Mathf.Max(_clip.length, 0f) : 0f);

        void DrawPreview()
        {
            if (_clip == null)
                return;

            if (_editor == null)
            {
                _editor = UnityEditor.Editor.CreateEditor(_clip);
                _reportedTime = float.NaN;
            }

            if (!_editor.HasPreviewGUI())
            {
                EditorGUILayout.LabelField("This animation clip has no preview.");
                return;
            }

            FootstepPreviewClock.SetRange(_editor, PreviewLength);
            FootstepPreviewClock.SetPlaybackSpeed(_editor, _playbackSpeed);
            Rect visible = GUILayoutUtility.GetRect(
                10f,
                10f,
                GUILayout.ExpandWidth(true),
                GUILayout.ExpandHeight(true));
            GUI.BeginGroup(visible);
            _editor.OnInteractivePreviewGUI(
                new Rect(0f, -PreviewToolbarHeight, visible.width, visible.height + PreviewToolbarHeight),
                EditorStyles.helpBox);
            GUI.EndGroup();

            if (!FootstepPreviewClock.TryGetTime(_editor, out float time, warn: true))
                return;
            if (float.IsNaN(_reportedTime) || Mathf.Abs(time - _reportedTime) > 0.00005f)
            {
                _reportedTime = time;
                TimeChanged?.Invoke(time);
            }
        }

        void DestroyEditor()
        {
            if (_editor == null)
                return;

            UnityEngine.Object.DestroyImmediate(_editor);
            _editor = null;
        }
    }

    static class FootstepPreviewClock
    {
        const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        static bool _warned;

        public static bool TryGetTime(UnityEditor.Editor editor, out float time, bool warn = false)
        {
            time = 0f;
            object control = FindTimeControl(editor, warn);
            if (control == null)
                return false;

            FieldInfo field = control.GetType().GetField("currentTime", Flags);
            if (field == null)
                return false;

            time = (float)field.GetValue(control);
            return !float.IsNaN(time) && !float.IsInfinity(time);
        }

        public static bool IsPlaying(UnityEditor.Editor editor)
        {
            object control = FindTimeControl(editor, warn: false);
            if (control == null)
                return false;

            PropertyInfo property = control.GetType().GetProperty("playing", Flags);
            if (property != null)
                return property.GetValue(control) is bool playing && playing;

            FieldInfo field = control.GetType().GetField("m_Playing", Flags);
            return field != null && field.GetValue(control) is bool value && value;
        }

        public static void SetPlaying(UnityEditor.Editor editor, bool playing)
        {
            object control = FindTimeControl(editor, warn: false);
            if (control == null)
                return;

            PropertyInfo property = control.GetType().GetProperty("playing", Flags);
            if (property != null && property.CanWrite)
            {
                property.SetValue(control, playing);
                return;
            }

            control.GetType().GetField("m_Playing", Flags)?.SetValue(control, playing);
        }

        public static void SetPlaybackSpeed(UnityEditor.Editor editor, float speed)
        {
            object control = FindTimeControl(editor, warn: false);
            if (control == null)
                return;

            control.GetType().GetField("playbackSpeed", Flags)?.SetValue(control, speed);
        }

        public static void SetRange(UnityEditor.Editor editor, float length)
        {
            object control = FindTimeControl(editor, warn: false);
            if (control == null)
                return;

            Type type = control.GetType();
            float stop = Mathf.Max(length, 0.0001f);
            type.GetField("startTime", Flags)?.SetValue(control, 0f);
            type.GetField("stopTime", Flags)?.SetValue(control, stop);
            FieldInfo current = type.GetField("currentTime", Flags);
            if (current == null)
                return;

            float time = (float)current.GetValue(control);
            if (float.IsNaN(time) || float.IsInfinity(time))
                return;

            if (time > stop)
                current.SetValue(control, stop);
            else if (time < 0f)
                current.SetValue(control, 0f);
        }

        public static void Seek(UnityEditor.Editor editor, float time, float length)
        {
            object control = FindTimeControl(editor, warn: false);
            if (control == null)
                return;

            Type type = control.GetType();
            type.GetField("startTime", Flags)?.SetValue(control, 0f);
            type.GetField("stopTime", Flags)?.SetValue(control, Mathf.Max(length, 0.0001f));
            type.GetField("currentTime", Flags)?.SetValue(control, time);
            type.GetProperty("nextCurrentTime", Flags)?.SetValue(control, time);
        }

        static object FindTimeControl(UnityEditor.Editor editor, bool warn)
        {
            if (editor == null)
                return null;

            FieldInfo avatarField = editor.GetType().GetField("m_AvatarPreview", Flags);
            object avatar = avatarField?.GetValue(editor);
            if (avatar == null)
            {
                if (warn)
                    WarnOnce(editor);
                return null;
            }

            object control = avatar.GetType().GetField("timeControl", Flags)?.GetValue(avatar);
            if (control == null && warn)
                WarnOnce(editor);
            return control;
        }

        static void WarnOnce(UnityEditor.Editor editor)
        {
            if (_warned)
                return;

            _warned = true;
            Debug.LogWarning(
                "Footstep could not read the animation preview time from " + editor.GetType().Name + ".");
        }
    }
}
