using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.VFX;

namespace MadeYellow.WAVES.Editor
{
    /// <summary>Audio and visual blocks shared by footstep, jump, and landing slots.</summary>
    static class WAVESEffectSlotDrawer
    {
        internal const float StatusIconReserve = 39f;

        const float StatusIconSize = 14f;
        const float StatusIconGap = 3f;
        const float StatusIconPad = 4f;
        const float StatusIconIdleAlpha = 0.33f;
        const float HelpIconSize = 13f;
        const float HelpIconGap = 4f;
        const float HelpIconAlpha = 0.55f;

        static GUIStyle _inkLabel;
        static bool _inkPro;
        static GUIStyle _fallbackCaption;
        static bool _fallbackCaptionPro;
        static Texture _audioStatusIcon;
        static Texture _visualStatusIcon;
        static Texture _gearIcon;
        static bool _gearPro;
        static Texture _helpIcon;
        static bool _helpPro;
        static Texture2D _stepContentTexture;
        static GUIStyle _stepContentStyle;
        static bool _stepPanelPro;
        static int _stepPaletteRevision = -1;
        static GUIStyle _blendEndLabel;
        static bool _blendEndPro;

        internal struct PlaybackDraft
        {
            public AudioMixerGroup Mixer;
            public float Volume;
            public float Spatial;
            public float Reverb;
            public float Audible;
            public float Min;
            public AudioRolloffMode Rolloff;
            public float Doppler;

            public static PlaybackDraft Defaults => new PlaybackDraft
            {
                Volume = 1f,
                Spatial = 1f,
                Reverb = 1f,
                Audible = 15f,
                Min = 1f,
                Rolloff = AudioRolloffMode.Logarithmic
            };

            public static PlaybackDraft Read(SerializedProperty parent)
            {
                if (parent == null || parent.FindPropertyRelative("_volume") == null)
                    return Defaults;

                return new PlaybackDraft
                {
                    Mixer = parent.FindPropertyRelative("_mixerGroup").objectReferenceValue as AudioMixerGroup,
                    Volume = parent.FindPropertyRelative("_volume").floatValue,
                    Spatial = parent.FindPropertyRelative("_spatialBlend").floatValue,
                    Reverb = parent.FindPropertyRelative("_reverbZoneMix").floatValue,
                    Audible = parent.FindPropertyRelative("_audibleDistance").floatValue,
                    Min = parent.FindPropertyRelative("_minDistance").floatValue,
                    Rolloff = (AudioRolloffMode)parent.FindPropertyRelative("_rolloff").enumValueIndex,
                    Doppler = parent.FindPropertyRelative("_dopplerLevel").floatValue
                };
            }

            public void Write(SerializedProperty parent)
            {
                if (parent == null || parent.FindPropertyRelative("_volume") == null)
                    return;

                parent.FindPropertyRelative("_mixerGroup").objectReferenceValue = Mixer;
                parent.FindPropertyRelative("_volume").floatValue = Unit(Volume);
                parent.FindPropertyRelative("_spatialBlend").floatValue = Unit(Spatial);
                parent.FindPropertyRelative("_reverbZoneMix").floatValue = Unit(Reverb);
                parent.FindPropertyRelative("_audibleDistance").floatValue = Audible < 0f ? 0f : Audible;
                parent.FindPropertyRelative("_minDistance").floatValue = Min < 0f ? 0f : Min;
                parent.FindPropertyRelative("_rolloff").enumValueIndex = (int)Rolloff;
                parent.FindPropertyRelative("_dopplerLevel").floatValue = Doppler < 0f ? 0f : Doppler;
            }
        }

        internal struct EffectDraft
        {
            public AudioResource Audio;
            public bool OverrideSettings;
            public bool OverrideVisual;
            public PlaybackDraft Playback;
            public ParticleSystem Particles;
            public VisualEffectAsset Graph;
            public float Visible;

            public bool Filled => Audio != null || Particles != null || Graph != null;

            public static EffectDraft Read(SerializedProperty element)
            {
                if (element == null || element.FindPropertyRelative("_audio") == null)
                {
                    return new EffectDraft
                    {
                        Playback = PlaybackDraft.Defaults,
                        Visible = 20f
                    };
                }

                return new EffectDraft
                {
                    Audio = element.FindPropertyRelative("_audio").objectReferenceValue as AudioResource,
                    OverrideSettings = element.FindPropertyRelative("_overrideSettings").boolValue,
                    OverrideVisual = element.FindPropertyRelative("_overrideVisual") != null
                        && element.FindPropertyRelative("_overrideVisual").boolValue,
                    Playback = PlaybackDraft.Read(element),
                    Particles = element.FindPropertyRelative("_particles").objectReferenceValue as ParticleSystem,
                    Graph = element.FindPropertyRelative("_graph").objectReferenceValue as VisualEffectAsset,
                    Visible = element.FindPropertyRelative("_visibleDistance").floatValue
                };
            }

            public void Write(SerializedProperty element)
            {
                if (element == null || element.FindPropertyRelative("_audio") == null)
                    return;

                element.FindPropertyRelative("_audio").objectReferenceValue = Audio;
                element.FindPropertyRelative("_overrideSettings").boolValue = OverrideSettings;
                SerializedProperty visualOverride = element.FindPropertyRelative("_overrideVisual");
                if (visualOverride != null)
                    visualOverride.boolValue = OverrideVisual;
                Playback.Write(element);
                element.FindPropertyRelative("_particles").objectReferenceValue = Particles;
                element.FindPropertyRelative("_graph").objectReferenceValue = Graph;
                element.FindPropertyRelative("_visibleDistance").floatValue = Visible < 0f ? 0f : Visible;
            }
        }

        internal static void DrawEffectBody(ref EffectDraft draft)
        {
            EnsureStatusIcons();
            DrawEffectGroup("AudioFX", _audioStatusIcon, draft.Audio != null);
            EditorGUI.indentLevel++;
            draft.Audio = AssetField(
                new GUIContent("Audio", "Audio Random Container is recommended. A clip still plays, without a sequence. Dropping a scene object takes the resource from its Audio Source."),
                draft.Audio,
                AudioFromSceneObject);
            if (draft.Audio != null)
            {
                if (draft.Audio is AudioClip)
                {
                    EditorGUILayout.HelpBox(
                        "Audio Random Container is recommended so a sequence can stay with this emitter.",
                        MessageType.Info);
                }

                draft.OverrideSettings = EditorGUILayout.Toggle(
                    new GUIContent("Override settings", "Playback settings for this effect. While this is off, the group common settings are used."),
                    draft.OverrideSettings);
                if (draft.OverrideSettings)
                    DrawPlayback(ref draft.Playback);
            }

            EditorGUI.indentLevel--;
            DrawEffectGroup("VisualFX", _visualStatusIcon, draft.Particles != null || draft.Graph != null);
            EditorGUI.indentLevel++;
            bool showParticles = draft.Particles != null || draft.Graph == null;
            bool showGraph = draft.Graph != null || draft.Particles == null;
            if (showParticles)
            {
                draft.Particles = AssetField(
                    new GUIContent("Particles", "One-shot particle prefab. It is reused in world space, so use a burst rather than a loop. Dropping a scene object takes the prefab it came from."),
                    draft.Particles,
                    ParticlesFromSceneObject);
            }

            if (showGraph)
            {
                draft.Graph = AssetField(
                    new GUIContent("Graph", "Visual Effect asset. Position and angle are sent as event attributes. Dropping a scene object takes the asset from its Visual Effect."),
                    draft.Graph,
                    GraphFromSceneObject);
            }

            if (draft.Particles != null || draft.Graph != null)
            {
                draft.OverrideVisual = EditorGUILayout.Toggle(
                    new GUIContent("Override settings", "Visible distance for this effect. While this is off, the group common visible distance is used."),
                    draft.OverrideVisual);
                if (draft.OverrideVisual)
                    draft.Visible = DrawVisibleDistance(draft.Visible);
            }

            EditorGUI.indentLevel--;
        }

        internal static void DrawGearHeader(string title, string gearTooltip, Action onGear)
        {
            const float gearSize = 18f;
            const float gearGap = 4f;
            float height = Mathf.Max(EditorGUIUtility.singleLineHeight + 2f, gearSize);
            Rect row = EditorGUILayout.GetControlRect(false, height, GUIStyle.none);
            GUI.Label(
                new Rect(row.x, row.y, Mathf.Max(0f, row.width - gearSize - gearGap), row.height),
                title,
                InkLabel());

            var gear = new Rect(
                row.xMax - gearSize,
                row.y + (row.height - gearSize) * 0.5f,
                gearSize,
                gearSize);
            if (GUI.Button(gear, new GUIContent(GearIcon(), gearTooltip), EditorStyles.iconButton) && onGear != null)
                onGear();
        }

        internal static void DrawCommon(SerializedProperty data, string help)
        {
            EnsureStatusIcons();
            DrawEffectGroup("General Effects Settings", help);
            EditorGUI.indentLevel++;
            DrawEffectGroup("AudioFX", AudioStatusIcon, true);
            EditorGUI.indentLevel++;
            PlaybackDraft common = PlaybackDraft.Read(data);
            SerializedProperty visibleProperty = data != null ? data.FindPropertyRelative("_visibleDistance") : null;
            float visible = visibleProperty != null ? visibleProperty.floatValue : 20f;
            EditorGUI.BeginChangeCheck();
            DrawPlayback(ref common);
            EditorGUI.indentLevel--;
            DrawEffectGroup("VisualFX", VisualStatusIcon, true);
            EditorGUI.indentLevel++;
            visible = DrawVisibleDistance(visible);
            EditorGUI.indentLevel--;
            if (EditorGUI.EndChangeCheck())
            {
                common.Write(data);
                if (visibleProperty != null)
                    visibleProperty.floatValue = visible < 0f ? 0f : visible;
            }

            EditorGUI.indentLevel--;
        }

        static float DrawVisibleDistance(float value)
        {
            return EditorGUILayout.FloatField(
                new GUIContent("Visible Distance", "Meters. Beyond this the camera skips the visual."),
                value);
        }

        internal static void DrawPlayback(ref PlaybackDraft playback)
        {
            playback.Mixer = (AudioMixerGroup)EditorGUILayout.ObjectField(
                new GUIContent("Audio Mixer Group", "Mixer group this sound is routed through. Empty plays straight to the listener."),
                playback.Mixer,
                typeof(AudioMixerGroup),
                false);
            playback.Volume = EditorGUILayout.Slider(
                new GUIContent("Volume", "Linear loudness, from silent to full."),
                Unit(playback.Volume),
                0f,
                1f);
            playback.Spatial = SpatialBlendSlider(
                new GUIContent("Spatial Blend", "0 plays in 2D. 1 plays in 3D at the contact."),
                playback.Spatial);
            playback.Reverb = EditorGUILayout.Slider(
                new GUIContent("Reverb Zone Mix", "How much of this sound is sent to reverb zones."),
                Unit(playback.Reverb),
                0f,
                1f);
            playback.Audible = EditorGUILayout.FloatField(
                new GUIContent("Audible Distance", "Meters. Also the AudioSource max distance and the cull distance."),
                playback.Audible);
            playback.Min = EditorGUILayout.FloatField(
                new GUIContent("Min Distance", "Meters. Inside this distance the sound stays at full volume."),
                playback.Min);
            playback.Rolloff = (AudioRolloffMode)EditorGUILayout.EnumPopup(new GUIContent("Rolloff"), playback.Rolloff);
            playback.Doppler = EditorGUILayout.FloatField(
                new GUIContent("Doppler Level", "Zero keeps a source that moves to the contact from changing pitch."),
                playback.Doppler);
        }

        internal static void DrawEffectGroup(string title, string help = null)
        {
            DrawEffectGroup(title, null, true, help);
        }

        internal static void DrawEffectGroup(string title, Texture icon, bool active, string help = null)
        {
            EditorGUILayout.Space(6f);
            Rect row = EditorGUILayout.GetControlRect(false, EditorGUIUtility.singleLineHeight + 2f, GUIStyle.none);
            float indent = EditorGUI.indentLevel * 15f;
            if (indent > 0.5f)
            {
                row.x += indent;
                row.width = Mathf.Max(0f, row.width - indent);
            }

            if (Event.current.type == EventType.Repaint)
            {
                EditorGUI.DrawRect(row, WAVESPalettePreferences.EffectFill);
                Color line = WAVESPalettePreferences.EffectLine;
                EditorGUI.DrawRect(new Rect(row.x, row.yMax - 1f, row.width, 1f), line);
            }

            float labelX = row.x + 6f;
            if (icon != null)
            {
                float y = row.y + (row.height - StatusIconSize) * 0.5f;
                if (Event.current.type == EventType.Repaint)
                    DrawStatusIcon(new Rect(labelX, y, StatusIconSize, StatusIconSize), icon, active);
                labelX += StatusIconSize + StatusIconGap;
            }

            float titleRoom = Mathf.Max(0f, row.xMax - labelX - 6f);
            if (!string.IsNullOrEmpty(help))
                titleRoom = Mathf.Max(0f, titleRoom - HelpIconSize - HelpIconGap);
            float titleWidth = Mathf.Min(EditorStyles.boldLabel.CalcSize(new GUIContent(title)).x, titleRoom);
            GUI.Label(new Rect(labelX, row.y, titleWidth, row.height), title, EditorStyles.boldLabel);
            if (string.IsNullOrEmpty(help))
                return;

            DrawHelp(
                new Rect(
                    labelX + titleWidth + HelpIconGap,
                    row.y + (row.height - HelpIconSize) * 0.5f,
                    HelpIconSize,
                    HelpIconSize),
                help);
        }

        internal static bool DrawFoldout(
            string id,
            string title,
            bool openByDefault,
            float rightReserve,
            string caption,
            string help,
            out Rect row)
        {
            string key = "MadeYellow.WAVES.Browser.Step." + id;
            bool open = SessionState.GetBool(key, openByDefault);
            row = EditorGUILayout.GetControlRect(false, EditorGUIUtility.singleLineHeight + 4f, GUIStyle.none);
            if (Event.current.type == EventType.Repaint)
                EditorGUI.DrawRect(row, WAVESChrome.PlateRaised);

            int indent = EditorGUI.indentLevel;
            EditorGUI.indentLevel = 0;
            bool next = EditorGUI.Foldout(row, open, GUIContent.none, true);
            EditorGUI.indentLevel = indent;
            if (next != open)
                SessionState.SetBool(key, next);

            if (Event.current.type == EventType.Repaint)
            {
                GUIStyle style = InkLabel();
                float x = row.x + 16f;
                float ceiling = row.xMax - rightReserve;
                bool hasHelp = !string.IsNullOrEmpty(help);
                float reserved = hasHelp ? HelpIconSize + HelpIconGap : 0f;
                float titleWidth = Mathf.Min(style.CalcSize(new GUIContent(title)).x, Mathf.Max(0f, ceiling - x - reserved));
                GUI.Label(new Rect(x, row.y, titleWidth, row.height), title, style);
                float cursor = x + titleWidth;
                if (!string.IsNullOrEmpty(caption))
                {
                    float captionX = cursor + 6f;
                    float captionWidth = Mathf.Min(
                        FallbackCaption().CalcSize(new GUIContent(caption)).x,
                        Mathf.Max(0f, ceiling - captionX - reserved));
                    GUI.Label(new Rect(captionX, row.y, captionWidth, row.height), caption, FallbackCaption());
                    cursor = captionX + captionWidth;
                }

                if (hasHelp)
                {
                    DrawHelp(
                        new Rect(
                            cursor + HelpIconGap,
                            row.y + (row.height - HelpIconSize) * 0.5f,
                            HelpIconSize,
                            HelpIconSize),
                        help);
                }
            }

            return next;
        }

        internal static void DrawStatusIcons(Rect row, bool audioOn, bool visualOn)
        {
            if (Event.current.type != EventType.Repaint)
                return;

            EnsureStatusIcons();
            float y = row.y + (row.height - StatusIconSize) * 0.5f;
            float x = row.xMax - StatusIconPad - StatusIconSize;
            DrawStatusIcon(new Rect(x, y, StatusIconSize, StatusIconSize), _visualStatusIcon, visualOn);
            x -= StatusIconGap + StatusIconSize;
            DrawStatusIcon(new Rect(x, y, StatusIconSize, StatusIconSize), _audioStatusIcon, audioOn);
        }

        internal static void BeginContent(Rect anchor)
        {
            EnsureStepPanel();
            BeginAlignedVertical(anchor, _stepContentStyle);
        }

        internal static void EndContent()
        {
            EndAlignedVertical();
        }

        internal static void EnsureStatusIcons()
        {
            if (_audioStatusIcon == null)
                _audioStatusIcon = EditorGUIUtility.ObjectContent(null, typeof(AudioSource)).image;
            if (_visualStatusIcon == null)
                _visualStatusIcon = EditorGUIUtility.ObjectContent(null, typeof(ParticleSystem)).image;
        }

        internal static Texture AudioStatusIcon
        {
            get
            {
                EnsureStatusIcons();
                return _audioStatusIcon;
            }
        }

        internal static Texture VisualStatusIcon
        {
            get
            {
                EnsureStatusIcons();
                return _visualStatusIcon;
            }
        }

        static void DrawStatusIcon(Rect rect, Texture icon, bool active)
        {
            if (icon == null)
                return;

            Color previous = GUI.color;
            Color tint = GUI.color;
            tint.a *= active ? 1f : StatusIconIdleAlpha;
            GUI.color = tint;
            GUI.DrawTexture(rect, icon, ScaleMode.ScaleToFit, true);
            GUI.color = previous;
        }

        static Texture GearIcon()
        {
            bool pro = EditorGUIUtility.isProSkin;
            if (_gearIcon != null && _gearPro == pro)
                return _gearIcon;

            _gearPro = pro;
            string primary = pro ? "d_Settings" : "Settings";
            string fallback = pro ? "Settings" : "d_Settings";
            _gearIcon = EditorGUIUtility.IconContent(primary).image;
            if (_gearIcon == null)
                _gearIcon = EditorGUIUtility.IconContent(fallback).image;
            if (_gearIcon == null)
                _gearIcon = EditorGUIUtility.IconContent("_Popup").image;
            return _gearIcon;
        }

        static void DrawHelp(Rect rect, string tooltip)
        {
            Texture icon = HelpIcon();
            if (icon == null)
                return;

            Color previous = GUI.color;
            Color tint = GUI.color;
            tint.a *= HelpIconAlpha;
            GUI.color = tint;
            GUI.Label(rect, new GUIContent(icon, tooltip), GUIStyle.none);
            GUI.color = previous;
        }

        static Texture HelpIcon()
        {
            bool pro = EditorGUIUtility.isProSkin;
            if (_helpIcon != null && _helpPro == pro)
                return _helpIcon;

            _helpPro = pro;
            string primary = pro ? "d__Help" : "_Help";
            string fallback = pro ? "_Help" : "d__Help";
            _helpIcon = EditorGUIUtility.IconContent(primary).image;
            if (_helpIcon == null)
                _helpIcon = EditorGUIUtility.IconContent(fallback).image;
            return _helpIcon;
        }

        static void BeginAlignedVertical(Rect anchor, GUIStyle style)
        {
            EditorGUILayout.BeginHorizontal(GUIStyle.none, GUILayout.ExpandWidth(true));
            Rect probe = GUILayoutUtility.GetRect(
                0f,
                0f,
                0f,
                0f,
                GUIStyle.none,
                GUILayout.Width(0f),
                GUILayout.Height(0f),
                GUILayout.ExpandWidth(false),
                GUILayout.ExpandHeight(false));
            float shift = anchor.x - probe.x;
            if (shift > 0.5f)
                GUILayout.Space(shift);

            EditorGUILayout.BeginVertical(style, GUILayout.ExpandWidth(true));
        }

        static void EndAlignedVertical()
        {
            EditorGUILayout.EndVertical();
            EditorGUILayout.EndHorizontal();
        }

        static float SpatialBlendSlider(GUIContent label, float value)
        {
            float captionHeight = EditorStyles.miniLabel.lineHeight;
            Rect block = EditorGUILayout.GetControlRect(true, EditorGUIUtility.singleLineHeight + captionHeight);
            Rect slider = new Rect(block.x, block.y, block.width, EditorGUIUtility.singleLineHeight);
            value = Unit(EditorGUI.Slider(slider, label, value, 0f, 1f));

            const float gap = 5f;
            float trackX = slider.x + EditorGUIUtility.labelWidth;
            float trackWidth = Mathf.Max(0f, slider.width - EditorGUIUtility.labelWidth - EditorGUIUtility.fieldWidth - gap);
            var captions = new Rect(trackX, slider.yMax, trackWidth, captionHeight);
            if (Event.current.type == EventType.Repaint)
            {
                GUI.Label(captions, "2D", EditorStyles.miniLabel);
                GUI.Label(captions, "3D", BlendEndLabel());
            }

            return value;
        }

        static GUIStyle BlendEndLabel()
        {
            bool pro = EditorGUIUtility.isProSkin;
            if (_blendEndLabel != null && _blendEndPro == pro)
                return _blendEndLabel;

            _blendEndPro = pro;
            _blendEndLabel = new GUIStyle(EditorStyles.miniLabel)
            {
                alignment = TextAnchor.MiddleRight
            };
            return _blendEndLabel;
        }

        static float Unit(float value)
        {
            if (value < 0f)
                return 0f;
            return value > 1f ? 1f : value;
        }

        static T AssetField<T>(GUIContent label, T current, Func<UnityEngine.Object, T> fromSceneObject)
            where T : UnityEngine.Object
        {
            Rect rect = EditorGUILayout.GetControlRect(
                true,
                EditorGUIUtility.singleLineHeight,
                EditorStyles.objectField);
            T dropped = TakeSceneDrop(rect, fromSceneObject);
            T shown = (T)EditorGUI.ObjectField(rect, label, dropped != null ? dropped : current, typeof(T), false);
            if (dropped == null)
                return shown;

            GUI.changed = true;
            return dropped;
        }

        static T TakeSceneDrop<T>(Rect rect, Func<UnityEngine.Object, T> fromSceneObject)
            where T : UnityEngine.Object
        {
            Event evt = Event.current;
            if (evt.type != EventType.DragUpdated && evt.type != EventType.DragPerform)
                return null;
            if (!GUI.enabled || !rect.Contains(evt.mousePosition))
                return null;

            T source = ResolveSceneDrag(fromSceneObject);
            if (source == null)
                return null;

            DragAndDrop.visualMode = DragAndDropVisualMode.Generic;
            if (evt.type != EventType.DragPerform)
            {
                evt.Use();
                return null;
            }

            DragAndDrop.AcceptDrag();
            evt.Use();
            return source;
        }

        static T ResolveSceneDrag<T>(Func<UnityEngine.Object, T> fromSceneObject) where T : UnityEngine.Object
        {
            UnityEngine.Object[] dragged = DragAndDrop.objectReferences;
            for (int i = 0; i < dragged.Length; i++)
            {
                UnityEngine.Object candidate = dragged[i];
                if (candidate == null || EditorUtility.IsPersistent(candidate))
                    continue;

                T source = fromSceneObject(candidate);
                if (source != null && EditorUtility.IsPersistent(source))
                    return source;
            }

            return null;
        }

        static AudioResource AudioFromSceneObject(UnityEngine.Object dragged)
        {
            AudioSource source = ComponentOf<AudioSource>(dragged);
            if (source == null)
                return null;

            return source.resource != null ? source.resource : source.clip;
        }

        static ParticleSystem ParticlesFromSceneObject(UnityEngine.Object dragged)
        {
            ParticleSystem system = ComponentOf<ParticleSystem>(dragged);
            return system != null ? PrefabUtility.GetCorrespondingObjectFromSource(system) : null;
        }

        static VisualEffectAsset GraphFromSceneObject(UnityEngine.Object dragged)
        {
            VisualEffect effect = ComponentOf<VisualEffect>(dragged);
            return effect != null ? effect.visualEffectAsset : null;
        }

        static T ComponentOf<T>(UnityEngine.Object dragged) where T : Component
        {
            if (dragged is T component)
                return component;

            var go = dragged as GameObject;
            return go != null ? go.GetComponentInChildren<T>(true) : null;
        }

        static void EnsureStepPanel()
        {
            bool pro = EditorGUIUtility.isProSkin;
            int revision = WAVESPalettePreferences.Revision;
            if (_stepContentStyle != null && _stepPanelPro == pro && _stepPaletteRevision == revision)
                return;

            if (_stepContentTexture != null)
                UnityEngine.Object.DestroyImmediate(_stepContentTexture);

            _stepPanelPro = pro;
            _stepPaletteRevision = revision;
            _stepContentTexture = new Texture2D(1, 1, TextureFormat.RGBA32, false)
            {
                hideFlags = HideFlags.HideAndDontSave,
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Point
            };
            _stepContentTexture.SetPixel(0, 0, WAVESPalettePreferences.StepContent);
            _stepContentTexture.Apply();
            _stepContentStyle = new GUIStyle
            {
                padding = new RectOffset(6, 6, 4, 6),
                margin = new RectOffset(0, 0, 0, 0)
            };
            _stepContentStyle.normal.background = _stepContentTexture;
        }

        static GUIStyle InkLabel()
        {
            EnsureInk();
            return _inkLabel;
        }

        static void EnsureInk()
        {
            bool pro = EditorGUIUtility.isProSkin;
            if (_inkLabel != null && _inkPro == pro)
                return;

            _inkPro = pro;
            _inkLabel = new GUIStyle(EditorStyles.boldLabel)
            {
                alignment = TextAnchor.MiddleLeft,
                clipping = TextClipping.Clip
            };
            Color ink = WAVESChrome.Ink;
            _inkLabel.normal.textColor = ink;
            _inkLabel.hover.textColor = ink;
        }

        static GUIStyle FallbackCaption()
        {
            bool pro = EditorGUIUtility.isProSkin;
            if (_fallbackCaption != null && _fallbackCaptionPro == pro)
                return _fallbackCaption;

            _fallbackCaptionPro = pro;
            _fallbackCaption = new GUIStyle(EditorStyles.miniLabel)
            {
                alignment = TextAnchor.MiddleLeft,
                clipping = TextClipping.Clip
            };
            Color muted = WAVESChrome.Muted;
            _fallbackCaption.normal.textColor = muted;
            _fallbackCaption.hover.textColor = muted;
            _fallbackCaption.active.textColor = muted;
            _fallbackCaption.focused.textColor = muted;
            return _fallbackCaption;
        }
    }
}
