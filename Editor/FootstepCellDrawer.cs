using System;
using MadeYellow.WAVES.AudioVisualEffects.Modules.FootstepsModule;
using MadeYellow.WAVES.Footsteps;
using UnityEditor;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.VFX;

namespace MadeYellow.WAVES.Editor
{
    /// <summary>Step list for one actor and surface group.</summary>
    [InitializeOnLoad]
    static class FootstepCellDrawer
    {
        const float FoldoutIconSize = 16f;
        const float StatusIconSize = 14f;
        const float StatusIconReserve = 39f;
        const float StatusIconGap = 3f;
        const float StatusIconPad = 4f;
        const float StatusIconIdleAlpha = 0.33f;

        static GUIStyle _lightLabel;
        static GUIStyle _darkLabel;
        static GUIStyle _inkLabel;
        static bool _inkPro;
        static int _labelPaletteRevision = -1;
        static Texture _audioStatusIcon;
        static Texture _visualStatusIcon;
        static Texture _gearIcon;
        static bool _gearPro;
        static Texture2D _stepContentTexture;
        static GUIStyle _stepContentStyle;
        static bool _stepPanelPro;
        static int _stepPaletteRevision = -1;

        static FootstepCellDrawer()
        {
            WAVESModuleDrawers.Register(typeof(WAVESFootstepModule), Draw);
        }

        static void Draw(SerializedProperty data, string scope)
        {
            SerializedProperty steps = data != null ? data.FindPropertyRelative("_steps") : null;
            if (steps == null || !steps.isArray)
            {
                EditorGUILayout.HelpBox("This group has no step list.", MessageType.Info);
                return;
            }

            DrawTypeHeader();
            for (int i = 0; i < WAVESCatalog.Steps.Count; i++)
            {
                FootstepType step = WAVESCatalog.Steps[i];
                if (step == null)
                    continue;

                DrawStep(steps, scope, step.name, step, false);
            }

            EditorGUILayout.Space(8f);
            EnsureStatusIcons();
            DrawEffectGroup("Common effects settings");
            EditorGUI.indentLevel++;
            DrawEffectGroup("AudioFX", _audioStatusIcon, true);
            EditorGUI.indentLevel++;
            PlaybackDraft common = PlaybackDraft.Read(data);
            EditorGUI.BeginChangeCheck();
            DrawPlayback(ref common);
            if (EditorGUI.EndChangeCheck())
                common.Write(data);
            EditorGUI.indentLevel--;
            DrawEffectGroup("VisualFX", _visualStatusIcon, true);
            EditorGUI.indentLevel++;
            EditorGUILayout.LabelField("<b>WIP</b> Coming soon.", NoteLabel());
            EditorGUI.indentLevel--;
            EditorGUI.indentLevel--;
        }

        static void DrawTypeHeader()
        {
            const float gearSize = 18f;
            const float gearGap = 4f;
            float height = Mathf.Max(EditorGUIUtility.singleLineHeight + 2f, gearSize);
            Rect row = EditorGUILayout.GetControlRect(false, height, GUIStyle.none);
            EnsureLabels();
            GUI.Label(
                new Rect(row.x, row.y, Mathf.Max(0f, row.width - gearSize - gearGap), row.height),
                "Footstep type effects",
                InkLabel());

            var gear = new Rect(
                row.xMax - gearSize,
                row.y + (row.height - gearSize) * 0.5f,
                gearSize,
                gearSize);
            if (GUI.Button(gear, new GUIContent(GearIcon(), "Manage footstep types"), EditorStyles.iconButton))
                FootstepTypeSettingsWindow.Open();
        }

        static void DrawStep(
            SerializedProperty steps,
            string scope,
            string title,
            FootstepType step,
            bool openByDefault)
        {
            string stepId = step != null ? step.GetEntityId().ToString() : EntityId.None.ToString();
            string key = scope + ".t." + stepId;
            EditorGUILayout.Space(2f);
            int index = FindStep(steps, step);
            SerializedProperty element = index >= 0 ? steps.GetArrayElementAtIndex(index) : null;
            bool hasAudio = element != null && element.FindPropertyRelative("_audio").objectReferenceValue != null;
            bool hasVisual = element != null && (
                element.FindPropertyRelative("_particles").objectReferenceValue != null ||
                element.FindPropertyRelative("_graph").objectReferenceValue != null);
            Texture icon = step != null ? step.icon : null;
            if (!DrawFoldout(key, title, StepHeaderColor(), openByDefault, icon, StatusIconReserve, out Rect header))
            {
                DrawStatusIcons(header, hasAudio, hasVisual);
                return;
            }

            DrawStatusIcons(header, hasAudio, hasVisual);
            EnsureStepPanel();
            BeginAlignedVertical(header, _stepContentStyle);
            AudioResource audio = element != null
                ? element.FindPropertyRelative("_audio").objectReferenceValue as AudioResource
                : null;
            bool overrideSettings = element != null && element.FindPropertyRelative("_overrideSettings").boolValue;
            PlaybackDraft playback = PlaybackDraft.Read(element);
            ParticleSystem particles = element != null
                ? element.FindPropertyRelative("_particles").objectReferenceValue as ParticleSystem
                : null;
            VisualEffectAsset graph = element != null
                ? element.FindPropertyRelative("_graph").objectReferenceValue as VisualEffectAsset
                : null;
            float visible = element != null ? element.FindPropertyRelative("_visibleDistance").floatValue : 20f;

            EditorGUI.BeginChangeCheck();
            EnsureStatusIcons();
            DrawEffectGroup("AudioFX", _audioStatusIcon, audio != null);
            EditorGUI.indentLevel++;
            audio = AssetField(
                new GUIContent("Audio", "Audio Random Container is recommended. A clip still plays, without a sequence. Dropping a scene object takes the resource from its Audio Source."),
                audio,
                AudioFromSceneObject);
            if (audio != null)
            {
                if (audio is AudioClip)
                {
                    EditorGUILayout.HelpBox(
                        "Audio Random Container is recommended so a sequence can stay with this emitter.",
                        MessageType.Info);
                }

                overrideSettings = EditorGUILayout.Toggle(
                    new GUIContent("Override settings", "Playback settings for this footstep type. While this is off, the group common settings are used."),
                    overrideSettings);
                if (overrideSettings)
                    DrawPlayback(ref playback);
            }

            EditorGUI.indentLevel--;
            DrawEffectGroup("VisualFX", _visualStatusIcon, particles != null || graph != null);
            EditorGUI.indentLevel++;
            bool showParticles = particles != null || graph == null;
            bool showGraph = graph != null || particles == null;
            if (showParticles)
            {
                particles = AssetField(
                    new GUIContent("Particles", "One-shot particle prefab. It is reused in world space, so use a burst rather than a loop. Dropping a scene object takes the prefab it came from."),
                    particles,
                    ParticlesFromSceneObject);
            }

            if (showGraph)
            {
                graph = AssetField(
                    new GUIContent("Graph", "Visual Effect asset. Position and angle are sent as event attributes. Dropping a scene object takes the asset from its Visual Effect."),
                    graph,
                    GraphFromSceneObject);
            }

            if (particles != null || graph != null)
            {
                visible = EditorGUILayout.FloatField(
                    new GUIContent("Visible Distance", "Meters. Beyond this the camera skips the visual."),
                    visible);
            }

            EditorGUI.indentLevel--;
            EndAlignedVertical();
            if (!EditorGUI.EndChangeCheck())
                return;

            bool filled = audio != null || particles != null || graph != null;
            if (!filled)
            {
                if (index >= 0)
                    steps.DeleteArrayElementAtIndex(index);
                return;
            }

            if (index < 0)
            {
                index = steps.arraySize;
                steps.arraySize = index + 1;
                element = steps.GetArrayElementAtIndex(index);
                element.FindPropertyRelative("_step").objectReferenceValue = step;
            }
            else
            {
                element = steps.GetArrayElementAtIndex(index);
            }

            element.FindPropertyRelative("_audio").objectReferenceValue = audio;
            element.FindPropertyRelative("_overrideSettings").boolValue = overrideSettings;
            playback.Write(element);
            element.FindPropertyRelative("_particles").objectReferenceValue = particles;
            element.FindPropertyRelative("_graph").objectReferenceValue = graph;
            element.FindPropertyRelative("_visibleDistance").floatValue = visible < 0f ? 0f : visible;
        }

        /// <summary>
        /// Object field for a module asset. Scene objects cannot be stored in an asset, so a scene
        /// drop is resolved to the asset behind it instead of being rejected without a word.
        /// </summary>
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

        static int FindStep(SerializedProperty steps, FootstepType step)
        {
            for (int i = 0; i < steps.arraySize; i++)
            {
                if (steps.GetArrayElementAtIndex(i).FindPropertyRelative("_step").objectReferenceValue == step)
                    return i;
            }

            return -1;
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

        struct PlaybackDraft
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

        static void DrawPlayback(ref PlaybackDraft playback)
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
                new GUIContent("Spatial Blend", "0 plays in 2D. 1 plays in 3D at the foot."),
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
                new GUIContent("Doppler Level", "Zero keeps a source that jumps to the foot from changing pitch."),
                playback.Doppler);
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

        static GUIStyle _blendEndLabel;
        static bool _blendEndPro;
        static GUIStyle _noteLabel;
        static bool _notePro;

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

        static void DrawEffectGroup(string title)
        {
            DrawEffectGroup(title, null, true);
        }

        static void DrawEffectGroup(string title, Texture icon, bool active)
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

            EditorGUI.LabelField(
                new Rect(labelX, row.y, Mathf.Max(0f, row.xMax - labelX), row.height),
                title,
                EditorStyles.boldLabel);
        }

        static GUIStyle NoteLabel()
        {
            bool pro = EditorGUIUtility.isProSkin;
            if (_noteLabel != null && _notePro == pro)
                return _noteLabel;

            _notePro = pro;
            _noteLabel = new GUIStyle(EditorStyles.label)
            {
                richText = true,
                wordWrap = true
            };
            return _noteLabel;
        }

        static bool DrawFoldout(
            string id,
            string title,
            Color color,
            bool openByDefault,
            Texture icon,
            float rightReserve,
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
                EnsureLabels();
                GUIStyle style = InkLabel();
                float x = row.x + 16f;
                if (icon != null)
                {
                    float y = row.y + (row.height - FoldoutIconSize) * 0.5f;
                    GUI.DrawTexture(new Rect(x, y, FoldoutIconSize, FoldoutIconSize), icon, ScaleMode.ScaleToFit, true);
                    x += FoldoutIconSize + 4f;
                }

                float labelWidth = Mathf.Max(0f, row.xMax - x - rightReserve);
                GUI.Label(new Rect(x, row.y, labelWidth, row.height), title, style);
            }

            return next;
        }

        static void DrawStatusIcons(Rect row, bool audioOn, bool visualOn)
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

        static void EnsureStatusIcons()
        {
            if (_audioStatusIcon == null)
                _audioStatusIcon = EditorGUIUtility.ObjectContent(null, typeof(AudioSource)).image;
            if (_visualStatusIcon == null)
                _visualStatusIcon = EditorGUIUtility.ObjectContent(null, typeof(ParticleSystem)).image;
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

        static Color StepHeaderColor()
        {
            return WAVESPalettePreferences.StepHeader;
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

        static void EnsureLabels()
        {
            int revision = WAVESPalettePreferences.Revision;
            if (_lightLabel != null && _labelPaletteRevision == revision)
                return;

            _labelPaletteRevision = revision;
            _lightLabel = new GUIStyle(EditorStyles.boldLabel)
            {
                alignment = TextAnchor.MiddleLeft
            };
            _lightLabel.normal.textColor = Color.white;
            _darkLabel = new GUIStyle(EditorStyles.boldLabel)
            {
                alignment = TextAnchor.MiddleLeft
            };
            _darkLabel.normal.textColor = WAVESPalettePreferences.InkStrong;
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
    }
}
