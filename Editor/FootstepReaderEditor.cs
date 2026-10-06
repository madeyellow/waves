using System.Collections.Generic;
using MadeYellow.WAVES.Footsteps;
using UnityEditor;
using UnityEngine;

namespace MadeYellow.WAVES.Editor
{
    [CustomEditor(typeof(FootstepReader))]
    public sealed class FootstepReaderEditor : UnityEditor.Editor
    {
        SerializedProperty _profile;
        SerializedProperty _feet;
        SerializedProperty _sampleTiming;
        SerializedProperty _useRaycasting;
        SerializedProperty _raycastMask;
        SerializedProperty _raycastOffset;
        SerializedProperty _surfaceCollider;
        SerializedProperty _onFootstepStarted;
        SerializedProperty _onFootstepFinished;
        SerializedProperty _trackEventHashes;
        SerializedProperty _trackEventSubscriptions;
        SerializedProperty _showDebug;
        SerializedProperty _gizmoSize;
        string _ensuredSignature;

        const string ConfigurationIconPath = "Packages/com.madeyellow.waves/Editor/Icons/footstep-window-icon.png";
        const string WorldIconPath = "Packages/com.madeyellow.waves/Editor/Icons/world-icon.png";
        const string EventIconPath = "Packages/com.madeyellow.waves/Editor/Icons/event-icon.png";
        const string DebugIconPath = "Packages/com.madeyellow.waves/Editor/Icons/debug-icon.png";
        const float SectionIconSize = 16f;
        const float SectionArrowWidth = 16f;

        static int _paletteRevision = -1;
        static readonly Dictionary<string, Texture> SectionIcons = new Dictionary<string, Texture>();
        static Texture2D _sectionHeaderTexture;
        static Texture2D _sectionContentTexture;
        static GUIStyle _sectionContentStyle;
        static bool _sectionStylePro;

        void OnEnable()
        {
            _profile = serializedObject.FindProperty("_profile");
            _feet = serializedObject.FindProperty("_feet");
            _sampleTiming = serializedObject.FindProperty("_sampleTiming");
            _useRaycasting = serializedObject.FindProperty("_useRaycasting");
            _raycastMask = serializedObject.FindProperty("_raycastMask");
            _raycastOffset = serializedObject.FindProperty("_raycastOffset");
            _surfaceCollider = serializedObject.FindProperty("_surfaceCollider");
            _onFootstepStarted = serializedObject.FindProperty("_onFootstepStarted");
            _onFootstepFinished = serializedObject.FindProperty("_onFootstepFinished");
            _trackEventHashes = serializedObject.FindProperty("_trackEventHashes");
            _trackEventSubscriptions = serializedObject.FindProperty("_trackEventSubscriptions");
            _showDebug = serializedObject.FindProperty("_showDebug");
            _gizmoSize = serializedObject.FindProperty("_gizmoSize");
            ScheduleParameterSync();
        }

        void OnDisable()
        {
            EditorApplication.delayCall -= SyncParameters;
        }

        public override void OnInspectorGUI()
        {
            SyncPalette();
            serializedObject.Update();

            EditorGUILayout.PropertyField(
                _profile,
                new GUIContent("Profile", "Footstep Tracks Profile. Each track is read from its baked animation curve."));
            var profile = _profile.objectReferenceValue as FootstepTracksProfile;
            if (profile == null)
                EditorGUILayout.HelpBox("Assign a Footstep Tracks Profile.", MessageType.Warning);
            else
                DrawControllerWarning();

            bool configuration = BeginSection("configuration", "Configuration", ConfigurationIconPath);
            if (configuration)
            {
                if (profile != null)
                    DrawFeet(profile);

                EditorGUILayout.Space(EditorGUIUtility.singleLineHeight);
                EditorGUILayout.PropertyField(
                    _sampleTiming,
                    new GUIContent(
                        "Update method",
                        "Update reads curves in Update. Late Update reads them after the Animator has applied the pose. Manual does not read curves on its own; call SampleFootsteps()."));
            }

            EndSection(configuration);

            bool world = BeginSection("world", "World Interaction", WorldIconPath);
            if (world)
            {
                EditorGUILayout.PropertyField(_useRaycasting, new GUIContent(
                    "Use Raycasting",
                    "Casts a ray down from the foot during contact and stores the collider, point, and normal on the footstep. Turn this off to skip physics and use Surface Collider instead."));
                if (_useRaycasting.boolValue)
                {
                    EditorGUILayout.PropertyField(_raycastMask, new GUIContent(
                        "Raycast Mask",
                        "Layers the foot ray can hit. Include the ground and exclude the character, or the ray stops on the body collider."));
                    EditorGUILayout.PropertyField(_raycastOffset, new GUIContent(
                        "Raycast Offset",
                        "Meters above the foot where the ray starts. Increase it when the foot is already inside the ground. The ray then travels this distance plus 2 meters downward."));
                }
                else
                {
                    EditorGUILayout.PropertyField(_surfaceCollider, new GUIContent(
                        "Surface Collider",
                        "Ground collider used when raycasting is off. The contact point is the closest point on this collider to the foot, and the normal is up. Leave it empty to publish a step with no collider."));
                }
            }

            EndSection(world);

            bool events = BeginSection("events", "Events", EventIconPath);
            if (events)
            {
                EditorGUILayout.LabelField(new GUIContent(
                    "All Tracks",
                    "Invoked for every footstep, on any track."));
                EditorGUILayout.PropertyField(_onFootstepStarted);
                EditorGUILayout.PropertyField(_onFootstepFinished);
                DrawTrackEvents(profile);
            }

            EndSection(events);

            if (_showDebug != null)
            {
                bool debug = BeginSection("debug", "Debug", DebugIconPath);
                if (debug)
                {
                    EditorGUILayout.PropertyField(_showDebug, new GUIContent("Show Gizmos"));
                    if (_showDebug.boolValue && _gizmoSize != null)
                        EditorGUILayout.PropertyField(_gizmoSize, new GUIContent("Gizmo Size"));
                }

                EndSection(debug);
            }

            serializedObject.ApplyModifiedProperties();
            ScheduleParameterSync();
        }

        bool BeginSection(string id, string title, string iconPath)
        {
            EnsureSectionStyles();
            string key = "MadeYellow.WAVES.Footsteps.FootstepReader." + id + "." + target.GetEntityId();
            bool expanded = SessionState.GetBool(key, true);

            EditorGUILayout.Space(8f);
            Rect row = EditorGUILayout.GetControlRect(false, EditorGUIUtility.singleLineHeight + 4f);
            if (Event.current.type == EventType.Repaint)
                GUI.DrawTexture(row, _sectionHeaderTexture, ScaleMode.StretchToFill, false);

            bool next = EditorGUI.Foldout(row, expanded, GUIContent.none, true);
            if (next != expanded)
                SessionState.SetBool(key, next);

            if (Event.current.type == EventType.Repaint)
            {
                float x = row.x + SectionArrowWidth;
                Texture icon = LoadSectionIcon(iconPath);
                if (icon != null)
                {
                    float y = row.y + (row.height - SectionIconSize) * 0.5f;
                    GUI.DrawTexture(new Rect(x, y, SectionIconSize, SectionIconSize), icon, ScaleMode.ScaleToFit, true);
                    x += SectionIconSize + 4f;
                }

                // GUI.Label, because EditorGUI.LabelField claims a control id. Claiming one only on
                // repaint shifts every id after it, so a click lands on a different control than the
                // one that was drawn and number fields never take the keyboard.
                GUI.Label(
                    EditorGUI.IndentedRect(new Rect(x, row.y, Mathf.Max(0f, row.xMax - x), row.height)),
                    title,
                    EditorStyles.boldLabel);
            }

            if (!next)
                return false;

            EditorGUILayout.Space(-EditorGUIUtility.standardVerticalSpacing);
            EditorGUILayout.BeginVertical(_sectionContentStyle);
            EditorGUI.indentLevel++;
            return true;
        }

        static void EndSection(bool expanded)
        {
            if (!expanded)
                return;

            EditorGUI.indentLevel--;
            EditorGUILayout.EndVertical();
        }

        static void SyncPalette()
        {
            int revision = WAVESPalettePreferences.Revision;
            bool pro = EditorGUIUtility.isProSkin;
            if (_paletteRevision == revision && _sectionStylePro == pro)
                return;

            _paletteRevision = revision;
            _sectionStylePro = pro;
            if (_sectionHeaderTexture != null)
                DestroyImmediate(_sectionHeaderTexture);
            if (_sectionContentTexture != null)
                DestroyImmediate(_sectionContentTexture);

            _sectionHeaderTexture = null;
            _sectionContentTexture = null;
            _sectionContentStyle = null;
        }

        static void EnsureSectionStyles()
        {
            SyncPalette();
            if (_sectionContentStyle != null)
                return;

            _sectionHeaderTexture = SolidTexture(WAVESPalettePreferences.SectionHeader);
            _sectionContentTexture = SolidTexture(WAVESPalettePreferences.SectionContent);
            _sectionContentStyle = new GUIStyle
            {
                padding = new RectOffset(4, 4, 4, 6),
                margin = new RectOffset(0, 0, 0, 0)
            };
            _sectionContentStyle.normal.background = _sectionContentTexture;
        }

        static Texture2D SolidTexture(Color color)
        {
            var texture = new Texture2D(1, 1, TextureFormat.RGBA32, false)
            {
                hideFlags = HideFlags.HideAndDontSave,
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Point
            };
            texture.SetPixel(0, 0, color);
            texture.Apply();
            return texture;
        }

        static Texture LoadSectionIcon(string path)
        {
            if (string.IsNullOrEmpty(path))
                return null;

            if (SectionIcons.TryGetValue(path, out Texture cached))
                return cached;

            Texture loaded = AssetDatabase.LoadAssetAtPath<Texture>(path);
            SectionIcons[path] = loaded;
            return loaded;
        }

        void DrawControllerWarning()
        {
            if (target is not FootstepReader agent)
                return;

            Animator animator = agent.GetComponent<Animator>();
            if (animator != null && animator.runtimeAnimatorController == null)
            {
                EditorGUILayout.HelpBox(
                    "Assign an Animator Controller so Footstep can add a float parameter for each track.",
                    MessageType.Warning);
            }
        }

        void ScheduleParameterSync()
        {
            if (target is not FootstepReader agent)
                return;

            Animator animator = agent.GetComponent<Animator>();
            string controllerId = animator != null && animator.runtimeAnimatorController != null
                ? animator.runtimeAnimatorController.GetEntityId().ToString()
                : EntityId.None.ToString();
            string signature = controllerId + "\n" + ParameterSignature(agent.Profile);
            if (signature == _ensuredSignature)
                return;

            _ensuredSignature = signature;
            EditorApplication.delayCall -= SyncParameters;
            EditorApplication.delayCall += SyncParameters;
        }

        void SyncParameters()
        {
            EditorApplication.delayCall -= SyncParameters;
            if (target is not FootstepReader agent)
                return;

            agent.EnsureControllerFloatParameters();
        }

        static string ParameterSignature(FootstepTracksProfile profile)
        {
            if (profile == null || profile.tracks == null)
                return string.Empty;

            var names = new System.Text.StringBuilder();
            for (int i = 0; i < profile.tracks.Count; i++)
            {
                FootstepTrackDefinition track = profile.tracks[i];
                if (track == null || string.IsNullOrEmpty(track.name))
                    continue;

                if (names.Length > 0)
                    names.Append('\n');
                names.Append(track.BakedCurveName);
            }

            return names.ToString();
        }

        void DrawFeet(FootstepTracksProfile profile)
        {
            EditorGUILayout.LabelField("Assign a foot for each leg.", EditorStyles.wordWrappedMiniLabel);
            if (profile.tracks == null || profile.tracks.Count == 0)
            {
                EditorGUILayout.HelpBox("Add tracks to the Footstep Tracks Profile.", MessageType.Info);
                return;
            }

            SyncFeet(profile);

            for (int i = 0; i < profile.tracks.Count; i++)
            {
                FootstepTrackDefinition track = profile.tracks[i];
                SerializedProperty element = _feet.GetArrayElementAtIndex(i);
                SerializedProperty foot = element.FindPropertyRelative("foot");
                Rect row = EditorGUILayout.GetControlRect();
                var swatch = new Rect(row.x, row.y + 2f, 6f, row.height - 4f);
                EditorGUI.DrawRect(swatch, track != null ? track.color : Color.gray);
                var field = new Rect(row.x + 10f, row.y, row.width - 10f, row.height);
                string label = track == null || string.IsNullOrEmpty(track.name) ? "(unnamed)" : track.name;
                EditorGUI.PropertyField(field, foot, new GUIContent(label));
            }
        }

        void SyncFeet(FootstepTracksProfile profile)
        {
            int count = profile.tracks != null ? profile.tracks.Count : 0;
            bool same = _feet.arraySize == count;
            if (same)
            {
                for (int i = 0; i < count; i++)
                {
                    SerializedProperty element = _feet.GetArrayElementAtIndex(i);
                    if (element.FindPropertyRelative("trackName").stringValue == TrackName(profile, i))
                        continue;

                    same = false;
                    break;
                }
            }

            if (same)
                return;

            var previousNames = new string[_feet.arraySize];
            var previousFeet = new Object[_feet.arraySize];
            for (int i = 0; i < _feet.arraySize; i++)
            {
                SerializedProperty element = _feet.GetArrayElementAtIndex(i);
                previousNames[i] = element.FindPropertyRelative("trackName").stringValue;
                previousFeet[i] = element.FindPropertyRelative("foot").objectReferenceValue;
            }

            _feet.arraySize = count;
            for (int i = 0; i < count; i++)
            {
                string trackName = TrackName(profile, i);
                Object foot = null;
                if (i < previousNames.Length && previousNames[i] == trackName)
                {
                    foot = previousFeet[i];
                }
                else
                {
                    for (int previous = 0; previous < previousNames.Length; previous++)
                    {
                        if (previousNames[previous] != trackName)
                            continue;

                        foot = previousFeet[previous];
                        break;
                    }
                }

                SerializedProperty element = _feet.GetArrayElementAtIndex(i);
                element.FindPropertyRelative("trackName").stringValue = trackName;
                element.FindPropertyRelative("foot").objectReferenceValue = foot;
            }
        }

        static string TrackName(FootstepTracksProfile profile, int index)
        {
            FootstepTrackDefinition track = profile.tracks[index];
            if (track == null || track.name == null)
                return string.Empty;

            return track.name;
        }

        void DrawTrackEvents(FootstepTracksProfile profile)
        {
            if (_trackEventHashes == null || _trackEventSubscriptions == null)
                return;

            EnsureTrackEvents(profile);
            var drawn = new HashSet<int>();
            if (profile != null && profile.tracks != null)
            {
                for (int i = 0; i < profile.tracks.Count; i++)
                {
                    FootstepTrackDefinition track = profile.tracks[i];
                    if (track == null || string.IsNullOrEmpty(track.name))
                        continue;

                    int hash = Animator.StringToHash(track.name);
                    if (!drawn.Add(hash))
                        continue;

                    int index = FindTrackEventIndex(hash);
                    if (index < 0)
                        continue;

                    DrawTrackEventEntry(index, track.name, track);
                }
            }

            int count = _trackEventHashes.arraySize;
            if (_trackEventSubscriptions.arraySize < count)
                count = _trackEventSubscriptions.arraySize;

            for (int i = 0; i < count; i++)
            {
                int hash = _trackEventHashes.GetArrayElementAtIndex(i).intValue;
                if (!drawn.Add(hash))
                    continue;

                DrawTrackEventEntry(i, "Unknown", new ColorCodedColor(WAVESPalettePreferences.Neutral));
            }
        }

        void EnsureTrackEvents(FootstepTracksProfile profile)
        {
            while (_trackEventSubscriptions.arraySize < _trackEventHashes.arraySize)
                _trackEventSubscriptions.arraySize++;

            while (_trackEventHashes.arraySize < _trackEventSubscriptions.arraySize)
                _trackEventHashes.arraySize++;

            if (profile == null || profile.tracks == null)
                return;

            for (int i = 0; i < profile.tracks.Count; i++)
            {
                FootstepTrackDefinition track = profile.tracks[i];
                if (track == null || string.IsNullOrEmpty(track.name))
                    continue;

                int hash = Animator.StringToHash(track.name);
                if (FindTrackEventIndex(hash) >= 0)
                    continue;

                int index = _trackEventHashes.arraySize;
                _trackEventHashes.arraySize = index + 1;
                _trackEventSubscriptions.arraySize = index + 1;
                _trackEventHashes.GetArrayElementAtIndex(index).intValue = hash;
            }
        }

        void DrawTrackEventEntry(int index, string label, IColorCodedElement element)
        {
            EnsureSectionStyles();
            int hash = _trackEventHashes.GetArrayElementAtIndex(index).intValue;
            string key = "MadeYellow.WAVES.Footsteps.FootstepReader.track." + target.GetEntityId() + "." + hash;
            bool expanded = SessionState.GetBool(key, true);

            EditorGUILayout.Space(6f);
            Rect row = EditorGUILayout.GetControlRect(false, EditorGUIUtility.singleLineHeight + 4f);
            if (Event.current.type == EventType.Repaint)
            {
                Color header = expanded
                    ? element.GetSelectedBackgroundColor()
                    : element.GetBackgroundColor();
                EditorGUI.DrawRect(row, header);
            }

            bool next = EditorGUI.Foldout(row, expanded, GUIContent.none, true);
            if (next != expanded)
                SessionState.SetBool(key, next);

            if (Event.current.type == EventType.Repaint)
            {
                float x = row.x + SectionArrowWidth;
                GUI.Label(
                    EditorGUI.IndentedRect(new Rect(x, row.y, Mathf.Max(0f, row.xMax - x), row.height)),
                    label,
                    EditorStyles.boldLabel);
            }

            if (!next)
                return;

            EditorGUILayout.Space(-EditorGUIUtility.standardVerticalSpacing);
            EditorGUILayout.BeginVertical(_sectionContentStyle);
            EditorGUI.indentLevel++;
            SerializedProperty events = _trackEventSubscriptions.GetArrayElementAtIndex(index);
            EditorGUILayout.PropertyField(events.FindPropertyRelative("onFootstepStarted"));
            EditorGUILayout.PropertyField(events.FindPropertyRelative("onFootstepFinished"));
            EditorGUI.indentLevel--;
            EditorGUILayout.EndVertical();
        }

        int FindTrackEventIndex(int hash)
        {
            for (int i = 0; i < _trackEventHashes.arraySize; i++)
            {
                if (_trackEventHashes.GetArrayElementAtIndex(i).intValue == hash)
                    return i;
            }

            return -1;
        }
    }
}
