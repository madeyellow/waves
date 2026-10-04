using System;
using System.Collections.Generic;
using System.Text;
using MadeYellow.WAVES.Footsteps;
using UnityEditor;
using UnityEditor.ShortcutManagement;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace MadeYellow.WAVES.Editor
{
    public sealed class FootstepWindow : EditorWindow
    {
        public const string StyleSheetPath = "Packages/com.madeyellow.waves/Editor/FootstepWindow.uss";
        public const string IconPath = "Packages/com.madeyellow.waves/Editor/Icons/footstep-window-icon.png";
        public const string UndoIconPath = "Packages/com.madeyellow.waves/Editor/Icons/undo.png";
        public const string RedoIconPath = "Packages/com.madeyellow.waves/Editor/Icons/redo.png";

        [SerializeField] FootstepTracksProfile _profile;
        [SerializeField] GameObject _model;
        [SerializeField] string _clipName;

        FootstepEditSession _session;
        AnimationClip _clip;
        readonly List<FootstepTracksProfile> _profiles = new List<FootstepTracksProfile>();
        readonly List<AnimationClip> _clips = new List<AnimationClip>();
        DropdownField _profileDropdown;
        ObjectField _modelField;
        DropdownField _clipDropdown;
        DropdownField _typeDropdown;
        Button _createTypeButton;
        Button _typeSettingsButton;
        readonly List<FootstepType> _paintTypes = new List<FootstepType>();
        FootstepType _paintType;
        Button _bakeButton;
        Button _snapButton;
        Button _undoButton;
        Button _redoButton;
        Button _goToStartButton;
        Button _playButton;
        Button _goToEndButton;
        Image _playIcon;
        Texture _playTexture;
        Texture _pauseTexture;
        bool _transportPlaying;
        bool _snapToGrid = true;
        FootstepClipPreview _preview;
        TwoPaneSplitView _split;
        VisualElement _modelEmpty;
        VisualElement _dock;
        int _modelPickerId;
        FootstepTimelineElement _timeline;
        readonly FootstepEditorHistory _history = new FootstepEditorHistory();
        string _typeRevision = string.Empty;
        string _profileRevision = string.Empty;
        int _historySerial = -1;
        bool _suppress;
        bool _applyingImport;

        public static FootstepWindow Instance { get; private set; }

        [Shortcut("Footstep/Undo", typeof(FootstepWindow), KeyCode.Z, ShortcutModifiers.Action)]
        static void ShortcutUndo()
        {
            Instance?.EditorUndo();
        }

        [Shortcut("Footstep/Redo", typeof(FootstepWindow), KeyCode.Y, ShortcutModifiers.Action)]
        static void ShortcutRedo()
        {
            Instance?.EditorRedo();
        }

        [Shortcut("Footstep/Redo Shift", typeof(FootstepWindow), KeyCode.Z, ShortcutModifiers.Action | ShortcutModifiers.Shift)]
        static void ShortcutRedoShift()
        {
            Instance?.EditorRedo();
        }

        [MenuItem("Window/MadeYellow/WAVES/Footstep Editor")]
        public static void Open()
        {
            GetWindow<FootstepWindow>().ApplyTitle();
        }

        void OnEnable()
        {
            Instance = this;
            wantsMouseMove = true;
            minSize = new Vector2(720f, 480f);
            ApplyTitle();
            _session = CreateInstance<FootstepEditSession>();
            _session.hideFlags = HideFlags.HideAndDontSave;
            Undo.undoRedoPerformed += OnUndoRedo;
            EditorApplication.projectChanged += OnProjectChanged;
            ObjectChangeEvents.changesPublished += OnObjectChanges;
            EnsureDefaultProfile();
            ResolveClip();
            if (_profile != null || _model != null)
                ReloadFromClip();
        }

        void OnDisable()
        {
            ObjectChangeEvents.changesPublished -= OnObjectChanges;
            EditorApplication.projectChanged -= OnProjectChanged;
            Undo.undoRedoPerformed -= OnUndoRedo;
            if (Instance == this)
                Instance = null;

            if (_session != null)
            {
                DestroyImmediate(_session);
                _session = null;
            }
        }

        void CreateGUI()
        {
            StyleSheet sheet = AssetDatabase.LoadAssetAtPath<StyleSheet>(StyleSheetPath);
            if (sheet != null)
                rootVisualElement.styleSheets.Add(sheet);

            rootVisualElement.AddToClassList("footstep-root");

            _preview = new FootstepClipPreview();
            _preview.TimeChanged += SetPlayheadFromPreview;
            var toolbar = BuildToolbar();
            _timeline = new FootstepTimelineElement();
            _profileDropdown = BuildProfileDropdown();
            var createProfile = new Button(CreateTracksProfile) { text = "+" };
            createProfile.AddToClassList("footstep-profile-add");
            createProfile.tooltip = "Create a Footstep Tracks Profile";
            _timeline.ProfileHost.Add(createProfile);
            _timeline.ProfileHost.Add(_profileDropdown);
            _timeline.SnapToGrid = _snapToGrid;
            _timeline.AddTrackRequested += AddTrack;
            _timeline.RenameTrackRequested += RenameTrack;
            _timeline.SetAnimationCurveNameRequested += SetAnimationCurveName;
            _timeline.ChangeTrackColorRequested += ChangeTrackColor;
            _timeline.DeleteTrackRequested += DeleteTrack;
            _timeline.MoveTrackRequested += MoveTrack;
            _timeline.PaintTypeChanged += OnPaintTypeChanged;
            _timeline.BeforeEdit = RecordEditorEdit;
            _timeline.Edited += UpdateChrome;
            _timeline.SeekRequested += SeekPreview;

            var body = new VisualElement();
            body.AddToClassList("footstep-body");
            body.Add(toolbar);
            body.Add(_timeline);

            _split = new TwoPaneSplitView(1, 168f, TwoPaneSplitViewOrientation.Vertical);
            _split.AddToClassList("footstep-split");
            _split.Add(_preview);
            _split.Add(body);
            rootVisualElement.RegisterCallback<ExecuteCommandEvent>(OnEditorCommand, TrickleDown.TrickleDown);
            rootVisualElement.Add(_split);
            rootVisualElement.Add(BuildModelEmpty());
            rootVisualElement.Add(BuildDock());

            RefreshAll();
        }

        void Update()
        {
            UpdateHistoryButtons();
            UpdateTransport();
            if (_preview == null || !_preview.NeedsRepaint)
                return;

            _preview.Tick();
            Repaint();
        }

        VisualElement BuildToolbar()
        {
            var toolbar = new VisualElement();
            toolbar.AddToClassList("footstep-toolbar");

            _modelField = new ObjectField("Model")
            {
                objectType = typeof(GameObject),
                allowSceneObjects = false,
                tooltip = "Imported model, such as an FBX."
            };
            _clipDropdown = new DropdownField();
            _clipDropdown.AddToClassList("footstep-clip");
            _clipDropdown.tooltip = "Animation clip imported with the model.";
            _clipDropdown.labelElement.style.display = DisplayStyle.None;
            _clipDropdown.labelElement.style.minWidth = 0;
            _clipDropdown.labelElement.style.width = 0;
            _bakeButton = new Button(() => { Bake(); });
            _bakeButton.AddToClassList("footstep-bake");
            _bakeButton.tooltip = "Сохраняет данные шагов в Animation Curves выбранного Animation Clip.";
            Texture saveIcon = LoadIcon("SaveActive", "d_SaveAs", "SaveAs");
            if (saveIcon != null)
            {
                var image = new Image
                {
                    image = saveIcon,
                    pickingMode = PickingMode.Ignore
                };
                image.AddToClassList("footstep-tool-icon");
                _bakeButton.Add(image);
            }

            _bakeButton.Add(new Label("Bake") { pickingMode = PickingMode.Ignore });
            var tools = BuildSnapTool();

            _modelField.RegisterValueChangedCallback(evt =>
            {
                if (_suppress)
                    return;
                SetModel(evt.newValue as GameObject);
            });
            _clipDropdown.RegisterValueChangedCallback(evt =>
            {
                if (_suppress)
                    return;
                SetClipByIndex(_clipDropdown.index);
            });

            toolbar.Add(BuildTransport());
            toolbar.Add(BuildSpeedControl());
            toolbar.Add(_clipDropdown);
            toolbar.Add(BuildTypeControl());
            toolbar.Add(tools);
            return toolbar;
        }

        VisualElement BuildDock()
        {
            _dock = new VisualElement();
            _dock.AddToClassList("footstep-dock");
            _dock.Add(_modelField);
            _dock.Add(_bakeButton);
            return _dock;
        }

        VisualElement BuildModelEmpty()
        {
            _modelEmpty = new VisualElement();
            _modelEmpty.AddToClassList("footstep-model-empty");

            var copy = new VisualElement();
            copy.AddToClassList("footstep-model-empty-copy");

            var title = new Label("This is Footstep Editor.");
            title.AddToClassList("footstep-model-empty-title");

            var intro = new Label("This tool allows you to rapidly mark up the steps of a character's animation.");
            intro.AddToClassList("footstep-model-empty-body");

            var lead = new Label("To get started:");
            lead.AddToClassList("footstep-model-empty-lead");

            var list = new VisualElement();
            list.AddToClassList("footstep-model-empty-list");
            list.Add(EmptyBullet("Select a character model with animation."));
            list.Add(EmptyBullet("Both <b>Humanoid</b> and <b>Generic</b> rigs are supported."));

            copy.Add(title);
            copy.Add(intro);
            copy.Add(lead);
            copy.Add(list);

            var button = new Button(PickCharacterModel) { text = "Select Character Model" };
            button.AddToClassList("footstep-model-empty-button");
            _modelEmpty.Add(copy);
            _modelEmpty.Add(button);
            return _modelEmpty;

            static VisualElement EmptyBullet(string text)
            {
                var row = new VisualElement();
                row.AddToClassList("footstep-model-empty-item");

                var mark = new Label("•");
                mark.AddToClassList("footstep-model-empty-mark");

                var label = new Label(text) { enableRichText = true };
                label.AddToClassList("footstep-model-empty-item-text");

                row.Add(mark);
                row.Add(label);
                return row;
            }
        }

        void PickCharacterModel()
        {
            _modelPickerId = GUIUtility.GetControlID(FocusType.Passive);
            EditorGUIUtility.ShowObjectPicker<GameObject>(_model, false, string.Empty, _modelPickerId);
        }

        void OnGUI()
        {
            Event evt = Event.current;
            if (evt.type != EventType.ExecuteCommand || evt.commandName != "ObjectSelectorClosed")
                return;
            if (EditorGUIUtility.GetObjectPickerControlID() != _modelPickerId)
                return;

            SetModel(EditorGUIUtility.GetObjectPickerObject() as GameObject);
            evt.Use();
        }

        void UpdateModelEmptyState()
        {
            bool hasModel = _model != null;
            if (_split != null)
                _split.style.display = hasModel ? DisplayStyle.Flex : DisplayStyle.None;
            if (_modelEmpty != null)
                _modelEmpty.style.display = hasModel ? DisplayStyle.None : DisplayStyle.Flex;
            if (_dock != null)
                _dock.style.display = hasModel ? DisplayStyle.Flex : DisplayStyle.None;
        }

        VisualElement BuildSnapTool()
        {
            var tools = new VisualElement();
            tools.AddToClassList("footstep-tools");
            tools.AddToClassList("footstep-history");

            _snapButton = new Button(ToggleSnap) { tooltip = "Snap to frames" };
            _snapButton.AddToClassList("footstep-tool");
            AddToolIcon(_snapButton, LoadIcon("SceneViewSnap"));

            _undoButton = new Button(EditorUndo) { tooltip = "Undo: Ctrl + Z" };
            _undoButton.AddToClassList("footstep-tool");
            AddToolIcon(_undoButton, AssetDatabase.LoadAssetAtPath<Texture2D>(UndoIconPath));

            _redoButton = new Button(EditorRedo) { tooltip = "Redo: Ctrl + Y" };
            _redoButton.AddToClassList("footstep-tool");
            AddToolIcon(_redoButton, AssetDatabase.LoadAssetAtPath<Texture2D>(RedoIconPath));

            var separator = new VisualElement();
            separator.AddToClassList("footstep-tool-separator");

            UpdateSnapButton();
            UpdateHistoryButtons();
            tools.Add(_undoButton);
            tools.Add(_redoButton);
            tools.Add(separator);
            tools.Add(_snapButton);
            return tools;
        }

        VisualElement BuildTransport()
        {
            var transport = new VisualElement();
            transport.AddToClassList("footstep-tools");

            _playTexture = LoadIcon("Animation.Play", "d_Animation.Play", "PlayButton", "d_PlayButton");
            _pauseTexture = LoadIcon("PauseButton", "d_PauseButton", "Animation.Pause", "d_Animation.Pause");

            _goToStartButton = new Button(GoToStart) { tooltip = "Go to start" };
            _goToStartButton.AddToClassList("footstep-tool");
            AddToolIcon(_goToStartButton, LoadIcon("Animation.FirstKey", "d_Animation.FirstKey"));

            _playButton = new Button(TogglePlayback) { tooltip = "Play" };
            _playButton.AddToClassList("footstep-tool");
            _playIcon = AddToolIcon(_playButton, _playTexture);

            _goToEndButton = new Button(GoToEnd) { tooltip = "Go to end" };
            _goToEndButton.AddToClassList("footstep-tool");
            AddToolIcon(_goToEndButton, LoadIcon("Animation.LastKey", "d_Animation.LastKey"));

            transport.Add(_goToStartButton);
            transport.Add(_playButton);
            transport.Add(_goToEndButton);
            return transport;
        }

        VisualElement BuildSpeedControl()
        {
            var speed = new VisualElement();
            speed.AddToClassList("footstep-speed");

            var slider = new Slider(0.1f, 2f) { value = 1f };
            slider.AddToClassList("footstep-speed-slider");
            slider.tooltip = "Playback speed. Double-click the handle to reset to 1.";
            slider.labelElement.style.display = DisplayStyle.None;
            slider.labelElement.style.minWidth = 0;
            slider.labelElement.style.width = 0;

            var value = new Label("1.0×");
            value.AddToClassList("footstep-speed-value");
            value.pickingMode = PickingMode.Ignore;

            slider.RegisterValueChangedCallback(evt =>
            {
                float next = Mathf.Clamp(evt.newValue, 0.1f, 2f);
                value.text = next.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture) + "×";
                _preview?.SetPlaybackSpeed(next);
            });
            slider.RegisterCallback<PointerDownEvent>(evt =>
            {
                if (evt.clickCount != 2 || !IsSliderThumb(evt.target as VisualElement))
                    return;

                slider.value = 1f;
                evt.StopPropagation();
            }, TrickleDown.TrickleDown);

            speed.Add(slider);
            speed.Add(value);
            return speed;
        }

        VisualElement BuildTypeControl()
        {
            var group = new VisualElement();
            group.AddToClassList("footstep-type-group");

            var label = new Label("Footstep types");
            label.AddToClassList("footstep-type-label");
            label.pickingMode = PickingMode.Ignore;

            _typeDropdown = new DropdownField();
            _typeDropdown.AddToClassList("footstep-type-dropdown");
            _typeDropdown.tooltip = "Footstep type used when drawing a new step.";
            _typeDropdown.labelElement.style.display = DisplayStyle.None;
            _typeDropdown.labelElement.style.minWidth = 0;
            _typeDropdown.labelElement.style.width = 0;
            _typeDropdown.RegisterValueChangedCallback(evt =>
            {
                if (_suppress)
                    return;
                SetPaintTypeByIndex(_typeDropdown.index);
            });

            _createTypeButton = new Button(PromptCreateType) { text = "Click to create" };
            _createTypeButton.AddToClassList("footstep-type-create");
            _createTypeButton.tooltip = "Create a footstep type.";

            _typeSettingsButton = new Button(FootstepTypeSettingsWindow.Open) { tooltip = "Footstep type settings" };
            _typeSettingsButton.AddToClassList("footstep-tool");
            Texture gear = LoadIcon("Settings", "d_Settings", "_Popup");
            if (gear != null)
                AddToolIcon(_typeSettingsButton, gear);
            else
                _typeSettingsButton.text = "\u2699";

            group.Add(label);
            group.Add(_typeDropdown);
            group.Add(_createTypeButton);
            group.Add(_typeSettingsButton);
            return group;
        }

        void PromptCreateType()
        {
            FootstepTypeSettingsWindow.PromptCreate(created =>
            {
                if (created == null)
                    return;

                _paintType = created;
                _timeline?.SetPaintType(created);
                ApplyTypeCatalog();
            });
        }

        void OnPaintTypeChanged(FootstepType type)
        {
            _paintType = type;
            SyncTypeDropdown(LoadTypes());
        }

        void SetPaintTypeByIndex(int index)
        {
            if (index < 0 || index >= _paintTypes.Count)
                return;

            _paintType = _paintTypes[index];
            _timeline?.SetPaintType(_paintType);
        }

        void ApplyTypeCatalog()
        {
            List<FootstepType> types = LoadTypes();
            _timeline?.RefreshTypePresentation(types);
            SyncTypeDropdown(types);
            _typeRevision = DescribeReferencedTypes();
        }

        void SyncTypeDropdown(List<FootstepType> types)
        {
            if (_typeDropdown == null || _createTypeButton == null)
                return;

            _paintTypes.Clear();
            if (types != null)
            {
                for (int i = 0; i < types.Count; i++)
                {
                    if (types[i] != null)
                        _paintTypes.Add(types[i]);
                }
            }

            bool any = _paintTypes.Count > 0;
            _typeDropdown.style.display = any ? DisplayStyle.Flex : DisplayStyle.None;
            _createTypeButton.style.display = any ? DisplayStyle.None : DisplayStyle.Flex;
            if (!any)
            {
                _paintType = null;
                _timeline?.SetPaintType(null);
                return;
            }

            int index = _paintTypes.IndexOf(_paintType);
            if (index < 0)
            {
                index = 0;
                _paintType = _paintTypes[0];
                _timeline?.SetPaintType(_paintType);
            }

            var labels = new List<string>(_paintTypes.Count);
            for (int i = 0; i < _paintTypes.Count; i++)
                labels.Add(TypeLabel(_paintTypes, i));

            _typeDropdown.choices = labels;
            _typeDropdown.SetValueWithoutNotify(labels[index]);
        }

        static string TypeLabel(List<FootstepType> types, int index)
        {
            string name = types[index] != null ? types[index].name : "(missing)";
            int same = 0;
            for (int i = 0; i < types.Count; i++)
            {
                string other = types[i] != null ? types[i].name : "(missing)";
                if (other == name)
                    same++;
            }

            return same < 2 ? name : name + " (" + (index + 1) + ")";
        }

        static bool IsSliderThumb(VisualElement element)
        {
            for (VisualElement current = element; current != null; current = current.parent)
            {
                if (current.ClassListContains("unity-base-slider__dragger"))
                    return true;
                if (current is Slider)
                    return false;
            }

            return false;
        }

        void GoToStart()
        {
            _preview?.GoToStart();
        }

        void GoToEnd()
        {
            _preview?.GoToEnd();
            UpdateTransport();
        }

        void TogglePlayback()
        {
            _preview?.TogglePlayback();
            UpdateTransport();
        }

        void UpdateTransport()
        {
            bool hasClip = _clip != null;
            _goToStartButton?.SetEnabled(hasClip);
            _playButton?.SetEnabled(hasClip);
            _goToEndButton?.SetEnabled(hasClip);

            bool playing = hasClip && _preview != null && _preview.IsPlaying;
            if (_playButton != null)
                _playButton.tooltip = playing ? "Pause" : "Play";
            if (_playIcon != null && _playButton != null)
                _playIcon.tooltip = _playButton.tooltip;
            if (_playIcon == null || playing == _transportPlaying && _playIcon.image != null)
                return;

            _transportPlaying = playing;
            Texture icon = playing && _pauseTexture != null ? _pauseTexture : _playTexture;
            if (icon != null)
                _playIcon.image = icon;
        }

        static Image AddToolIcon(Button button, Texture icon)
        {
            if (icon == null)
                return null;

            var image = new Image
            {
                image = icon,
                pickingMode = PickingMode.Ignore,
                tooltip = button.tooltip
            };
            image.AddToClassList("footstep-tool-icon");
            image.scaleMode = ScaleMode.ScaleToFit;
            button.Add(image);
            return image;
        }

        static Texture LoadIcon(params string[] names)
        {
            for (int i = 0; i < names.Length; i++)
            {
                Texture icon = EditorGUIUtility.IconContent(names[i]).image;
                if (icon != null)
                    return icon;
            }

            return null;
        }

        void UpdateHistoryButtons()
        {
            if (_undoButton != null)
                _undoButton.SetEnabled(_history.CanUndo);
            if (_redoButton != null)
                _redoButton.SetEnabled(_history.CanRedo);
        }

        void RecordEditorEdit()
        {
            _history.Push(_session);
            UpdateHistoryButtons();
        }

        public void EditorUndo()
        {
            if (!BeginHistoryOp(1) || !_history.Undo(_session))
                return;

            _timeline?.Refresh();
            UpdateChrome();
        }

        public void EditorRedo()
        {
            if (!BeginHistoryOp(2) || !_history.Redo(_session))
                return;

            _timeline?.Refresh();
            UpdateChrome();
        }

        bool BeginHistoryOp(int op)
        {
            if (_historySerial == Time.frameCount + op * 100000)
                return false;

            _historySerial = Time.frameCount + op * 100000;
            return true;
        }

        void OnEditorCommand(ExecuteCommandEvent evt)
        {
            if (evt.commandName == "Undo")
            {
                EditorUndo();
                evt.StopPropagation();
            }
            else if (evt.commandName == "Redo")
            {
                EditorRedo();
                evt.StopPropagation();
            }
        }

        void ToggleSnap()
        {
            _snapToGrid = !_snapToGrid;
            if (_timeline != null)
                _timeline.SnapToGrid = _snapToGrid;
            UpdateSnapButton();
        }

        void UpdateSnapButton()
        {
            _snapButton?.EnableInClassList("footstep-tool-on", _snapToGrid);
        }

        DropdownField BuildProfileDropdown()
        {
            var dropdown = new DropdownField();
            dropdown.AddToClassList("footstep-gutter-profile");
            dropdown.tooltip = "Footstep Tracks Profile";
            dropdown.labelElement.style.display = DisplayStyle.None;
            dropdown.labelElement.style.minWidth = 0;
            dropdown.labelElement.style.width = 0;
            dropdown.labelElement.style.marginRight = 0;
            dropdown.RegisterValueChangedCallback(evt =>
            {
                if (_suppress)
                    return;
                SetProfileByIndex(dropdown.index);
            });
            return dropdown;
        }

        void RefreshAll()
        {
            SyncFields();
            float clipLength = ActiveClipLength();
            _timeline?.SetContext(_session, _clip, _profile != null, CurrentImporter() != null, LoadTypes, clipLength);
            _preview?.SetClip(_clip, clipLength);
            UpdateChrome();
            _typeRevision = DescribeReferencedTypes();
            _profileRevision = DescribeProfile();
        }

        void SyncFields()
        {
            _suppress = true;
            if (_modelField != null)
                _modelField.value = _model;
            SyncProfileDropdown();
            SyncClipDropdown();
            SyncTypeDropdown(LoadTypes());
            _suppress = false;
        }

        void SyncProfileDropdown()
        {
            if (_profileDropdown == null)
                return;

            _profiles.Clear();
            _profiles.AddRange(LoadProfiles());
            var labels = new List<string>(_profiles.Count);
            for (int i = 0; i < _profiles.Count; i++)
                labels.Add(ProfileLabel(_profiles[i], _profiles));

            _profileDropdown.choices = labels;
            _profileDropdown.SetEnabled(_profiles.Count > 0);
            int index = _profiles.IndexOf(_profile);
            _profileDropdown.SetValueWithoutNotify(index >= 0 ? labels[index] : null);
        }

        void SyncClipDropdown()
        {
            if (_clipDropdown == null)
                return;

            _clips.Clear();
            if (_model != null)
                _clips.AddRange(LoadModelClips(_model));

            var labels = new List<string>(_clips.Count);
            for (int i = 0; i < _clips.Count; i++)
                labels.Add(ClipLabel(_clips, i));

            _clipDropdown.choices = labels;
            _clipDropdown.SetEnabled(_clips.Count > 0);
            int index = -1;
            for (int i = 0; i < _clips.Count; i++)
            {
                if (_clips[i].name == _clipName)
                {
                    index = i;
                    break;
                }
            }

            _clipDropdown.SetValueWithoutNotify(index >= 0 ? labels[index] : null);
        }

        void UpdateChrome()
        {
            bool canBake = _session != null
                && _clip != null
                && CurrentImporter() != null
                && !string.IsNullOrEmpty(_clipName);
            bool hasUnbakedChanges = canBake && _session.hasUnbakedChanges;
            if (_bakeButton != null)
            {
                _bakeButton.SetEnabled(canBake);
                _bakeButton.EnableInClassList("footstep-bake-dirty", hasUnbakedChanges);
            }

            ApplyTitle(hasUnbakedChanges);
            UpdateHistoryButtons();
            UpdateTransport();
            UpdateModelEmptyState();
        }

        void ApplyTitle(bool unsaved = false)
        {
            Texture2D icon = AssetDatabase.LoadAssetAtPath<Texture2D>(IconPath);
            titleContent = new GUIContent(unsaved ? "Footstep Editor*" : "Footstep Editor", icon);
        }

        void CreateTracksProfile()
        {
            string path = EditorUtility.SaveFilePanelInProject(
                "Create Footstep Tracks Profile",
                "FootstepTracksProfile",
                "asset",
                "Choose where to save the Footstep Tracks Profile.");
            if (string.IsNullOrEmpty(path))
                return;

            var profile = ScriptableObject.CreateInstance<FootstepTracksProfile>();
            AssetDatabase.CreateAsset(profile, path);
            AssetDatabase.SaveAssets();
            SetProfile(profile);
        }

        void AddTrack()
        {
            if (_profile == null)
                return;

            EditProfile("Add Track", () =>
            {
                if (_profile.tracks == null)
                    _profile.tracks = new List<FootstepTrackDefinition>();

                _profile.tracks.Add(new FootstepTrackDefinition
                {
                    name = NextUnnamedTrackName(_profile.tracks),
                    color = NewTrackColor(_profile.tracks)
                });
            });
        }

        void RenameTrack(FootstepTrackState track)
        {
            FootstepTrackDefinition definition = DefinitionAt(track);
            if (definition == null)
                return;

            FootstepRenameTrackWindow.Show(definition.name, next =>
            {
                if (_profile?.tracks == null || !_profile.tracks.Contains(definition) || definition.name == next)
                    return;

                EditProfile("Rename Track", () => definition.name = next);
            });
        }

        void SetAnimationCurveName(FootstepTrackState track)
        {
            FootstepTrackDefinition definition = DefinitionAt(track);
            if (definition == null)
                return;

            FootstepRenameTrackWindow.ShowCurve(definition.name, definition.bakeIntoAnimationCurve, next =>
            {
                if (_profile?.tracks == null || !_profile.tracks.Contains(definition))
                    return;

                string stored = definition.bakeIntoAnimationCurve ?? string.Empty;
                if (stored == next)
                    return;

                EditProfile("Set Animation Curve Name", () => definition.bakeIntoAnimationCurve = next);
            });
        }

        void ChangeTrackColor(FootstepTrackState track)
        {
            FootstepTrackDefinition definition = DefinitionAt(track);
            if (definition == null)
                return;

            FootstepTrackColorWindow.Show(definition.color, next =>
            {
                if (_profile?.tracks == null || !_profile.tracks.Contains(definition) || definition.color == next)
                    return;

                next.a = 1f;
                EditProfile("Change Track Color", () => definition.color = next);
            });
        }

        void DeleteTrack(FootstepTrackState track)
        {
            int index = TrackIndex(track);
            if (index < 0)
                return;

            string trackName = _profile.tracks[index].name;
            if (string.IsNullOrEmpty(trackName))
                trackName = "(unnamed)";

            bool confirmed = EditorUtility.DisplayDialog(
                "Delete Track",
                "This action will delete the track \"" + trackName + "\" and all of its steps. This action cannot be undone. Are you sure you want to delete this track?",
                "Yes, delete this track",
                "No, don't delete this track");
            if (!confirmed)
                return;

            EditProfile("Delete Track", () => _profile.tracks.RemoveAt(index));
        }

        void MoveTrack(FootstepTrackState track, int destination)
        {
            int index = TrackIndex(track);
            if (index < 0 || _profile.tracks == null)
                return;

            destination = Mathf.Clamp(destination, 0, _profile.tracks.Count - 1);
            if (destination == index)
                return;

            EditProfile("Move Track", () =>
            {
                FootstepTrackDefinition definition = _profile.tracks[index];
                _profile.tracks.RemoveAt(index);
                _profile.tracks.Insert(destination, definition);
            });
        }

        void EditProfile(string undoName, Action edit)
        {
            if (_profile == null || _session == null)
                return;

            Undo.RecordObject(_profile, undoName);
            edit();
            EditorUtility.SetDirty(_profile);
            ApplyProfileChannels();
            RefreshAll();
        }

        int TrackIndex(FootstepTrackState track)
        {
            if (track == null || _session?.tracks == null || _profile?.tracks == null)
                return -1;

            int index = _session.tracks.IndexOf(track);
            if (index < 0 || index >= _profile.tracks.Count || _profile.tracks[index] == null)
                return -1;

            return index;
        }

        FootstepTrackDefinition DefinitionAt(FootstepTrackState track)
        {
            int index = TrackIndex(track);
            return index < 0 ? null : _profile.tracks[index];
        }

        static string NextUnnamedTrackName(List<FootstepTrackDefinition> tracks)
        {
            var used = new HashSet<string>();
            for (int i = 0; i < tracks.Count; i++)
            {
                string name = tracks[i]?.name;
                if (!string.IsNullOrEmpty(name))
                    used.Add(name);
            }

            int index = 1;
            string candidate = "Unnamed Track " + index.ToString();
            while (used.Contains(candidate))
            {
                index++;
                candidate = "Unnamed Track " + index.ToString();
            }

            return candidate;
        }

        static Color NewTrackColor(List<FootstepTrackDefinition> tracks)
        {
            var used = new List<Color>(tracks != null ? tracks.Count : 0);
            if (tracks != null)
            {
                for (int i = 0; i < tracks.Count; i++)
                    used.Add(tracks[i].color);
            }

            return WAVESPalettePreferences.NextFreeSwatch(used);
        }

        void SetProfile(FootstepTracksProfile next)
        {
            if (next == _profile)
                return;

            bool firstAssignment = _profile == null && (_session == null || !_session.hasUnbakedChanges);
            _profile = next;
            if (firstAssignment)
                ReloadFromClip();
            else
                RemapProfile();

            RefreshAll();
        }

        void SetProfileByIndex(int index)
        {
            if (index < 0 || index >= _profiles.Count)
                return;
            SetProfile(_profiles[index]);
        }

        void SetModel(GameObject next)
        {
            if (next == _model)
                return;

            if (next != null && CurrentImporter(next) == null)
            {
                EditorUtility.DisplayDialog("Footstep", "Assign an imported model.", "OK");
                SyncFields();
                return;
            }

            if (!ConfirmLeave())
            {
                SyncFields();
                return;
            }

            _model = next;
            _clipName = null;
            ResolveClip();
            ReloadFromClip();
            RefreshAll();
        }

        void SetClipByIndex(int index)
        {
            if (index < 0 || index >= _clips.Count)
                return;

            AnimationClip next = _clips[index];
            if (next == null || next.name == _clipName)
                return;

            if (!ConfirmLeave())
            {
                SyncFields();
                return;
            }

            _clipName = next.name;
            _clip = next;
            ReloadFromClip();
            RefreshAll();
        }

        bool ConfirmLeave()
        {
            if (_session == null || !_session.hasUnbakedChanges)
                return true;

            bool bake = EditorUtility.DisplayDialog(
                "Footstep",
                "Changes are not baked into the clip Curves. Bake them?",
                "Yes, bake",
                "No, ignore bake");
            if (!bake)
            {
                _session.hasUnbakedChanges = false;
                return true;
            }

            return Bake();
        }

        void RemapProfile()
        {
            if (_session == null)
                return;

            RecordEditorEdit();

            var previous = _session.tracks ?? new List<FootstepTrackState>();
            var next = new List<FootstepTrackState>();
            List<FootstepType> types = LoadTypes();

            if (_profile != null && _profile.tracks != null)
            {
                for (int i = 0; i < _profile.tracks.Count; i++)
                {
                    FootstepTrackDefinition definition = _profile.tracks[i];
                    if (definition == null)
                        continue;

                    FootstepTrackState existing = null;
                    for (int t = 0; t < previous.Count; t++)
                    {
                        if (previous[t] != null && previous[t].name == definition.name)
                        {
                            existing = previous[t];
                            break;
                        }
                    }

                    if (existing != null)
                    {
                        existing.color = definition.color;
                        existing.bakeIntoAnimationCurve = definition.bakeIntoAnimationCurve;
                        if (existing.steps == null)
                            existing.steps = new List<FootstepMarker>();
                        next.Add(existing);
                    }
                    else
                    {
                        next.Add(new FootstepTrackState
                        {
                            name = definition.name,
                            bakeIntoAnimationCurve = definition.bakeIntoAnimationCurve,
                            color = definition.color,
                            steps = ReadSteps(definition.BakedCurveName, types)
                        });
                    }
                }
            }

            _session.tracks = next;
            _session.hasUnbakedChanges = true;
            EditorUtility.SetDirty(_session);
        }

        void ReloadFromClip()
        {
            if (_session == null)
                return;

            _session.tracks = BuildTracks();
            _session.hasUnbakedChanges = false;
            EditorUtility.SetDirty(_session);
            _history.Clear();
        }

        List<FootstepTrackState> BuildTracks()
        {
            var tracks = new List<FootstepTrackState>();
            List<FootstepType> types = LoadTypes();
            if (_profile == null || _profile.tracks == null)
                return tracks;

            for (int i = 0; i < _profile.tracks.Count; i++)
            {
                FootstepTrackDefinition definition = _profile.tracks[i];
                if (definition == null)
                    continue;

                tracks.Add(new FootstepTrackState
                {
                    name = definition.name,
                    bakeIntoAnimationCurve = definition.bakeIntoAnimationCurve,
                    color = definition.color,
                    steps = ReadSteps(definition.BakedCurveName, types)
                });
            }

            return tracks;
        }

        List<FootstepMarker> ReadSteps(string trackName, List<FootstepType> types)
        {
            ModelImporter importer = CurrentImporter();
            if (importer == null || _clip == null || string.IsNullOrEmpty(_clipName))
                return new List<FootstepMarker>();

            return FootstepCurveIO.Read(
                importer,
                _clipName,
                trackName,
                ActiveClipLength(),
                _clip.frameRate,
                types);
        }

        bool Bake()
        {
            ModelImporter importer = CurrentImporter();
            if (importer == null || _clip == null || _session == null || string.IsNullOrEmpty(_clipName))
                return false;

            Undo.IncrementCurrentGroup();
            int group = Undo.GetCurrentGroup();
            Undo.RegisterCompleteObjectUndo(importer, "Bake Footsteps");
            if (!FootstepCurveIO.TryBake(importer, _clipName, _session.tracks, ActiveClipLength(), _clip.frameRate))
            {
                EditorUtility.DisplayDialog(
                    "Footstep",
                    "The selected clip is not an imported model animation.",
                    "OK");
                Undo.CollapseUndoOperations(group);
                return false;
            }

            RecordEditorEdit();

            EditorUtility.SetDirty(importer);
            _applyingImport = true;
            try
            {
                importer.SaveAndReimport();
            }
            finally
            {
                _applyingImport = false;
            }

            ResolveClip();
            _session.tracks = BuildTracks();
            _session.hasUnbakedChanges = false;
            EditorUtility.SetDirty(_session);
            Undo.CollapseUndoOperations(group);
            RefreshAll();
            return true;
        }

        void OnProjectChanged()
        {
            if (_applyingImport || _profileDropdown == null)
                return;

            List<FootstepTracksProfile> profiles = LoadProfiles();
            bool missing = _profile != null && !profiles.Contains(_profile);
            bool needsDefault = _profile == null && profiles.Count > 0;
            if (!missing && !needsDefault)
            {
                SyncFields();
                if (SyncProfileTracks())
                    return;
                SyncTypePresentation();
                return;
            }

            _profile = profiles.Count > 0 ? profiles[0] : null;
            ReloadFromClip();
            RefreshAll();
        }

        void EnsureDefaultProfile()
        {
            if (_profile != null)
                return;

            List<FootstepTracksProfile> profiles = LoadProfiles();
            if (profiles.Count > 0)
                _profile = profiles[0];
        }

        void ResolveClip()
        {
            _clip = null;
            if (_model == null)
                return;

            List<AnimationClip> clips = LoadModelClips(_model);
            if (clips.Count == 0)
            {
                _clipName = null;
                return;
            }

            for (int i = 0; i < clips.Count; i++)
            {
                if (clips[i].name == _clipName)
                {
                    _clip = clips[i];
                    return;
                }
            }

            _clip = clips[0];
            _clipName = _clip.name;
        }

        float ActiveClipLength()
        {
            if (_clip == null)
                return 0f;

            return FootstepCurveIO.ConfiguredLength(
                CurrentImporter(),
                _clipName,
                _clip.frameRate,
                _clip.length);
        }

        ModelImporter CurrentImporter()
        {
            return CurrentImporter(_model);
        }

        static ModelImporter CurrentImporter(UnityEngine.Object model)
        {
            if (model == null)
                return null;

            string path = AssetDatabase.GetAssetPath(model);
            return AssetImporter.GetAtPath(path) as ModelImporter;
        }

        static List<AnimationClip> LoadModelClips(UnityEngine.Object model)
        {
            var result = new List<AnimationClip>();
            string path = AssetDatabase.GetAssetPath(model);
            if (string.IsNullOrEmpty(path))
                return result;

            var byName = new Dictionary<string, List<AnimationClip>>();
            UnityEngine.Object[] assets = AssetDatabase.LoadAllAssetsAtPath(path);
            for (int i = 0; i < assets.Length; i++)
            {
                if (assets[i] is not AnimationClip clip || clip.name.StartsWith("__preview__"))
                    continue;

                if (!byName.TryGetValue(clip.name, out List<AnimationClip> bucket))
                {
                    bucket = new List<AnimationClip>();
                    byName.Add(clip.name, bucket);
                }

                bucket.Add(clip);
            }

            ModelImporter importer = CurrentImporter(model);
            ModelImporterClipAnimation[] defined = importer != null ? importer.clipAnimations : null;
            ModelImporterClipAnimation[] source = defined != null && defined.Length > 0
                ? defined
                : importer != null ? importer.defaultClipAnimations : null;
            if (source != null)
            {
                for (int i = 0; i < source.Length; i++)
                {
                    ModelImporterClipAnimation definition = source[i];
                    if (definition == null || string.IsNullOrEmpty(definition.name))
                        continue;
                    if (!byName.TryGetValue(definition.name, out List<AnimationClip> bucket) || bucket.Count == 0)
                        continue;

                    result.Add(bucket[0]);
                    bucket.RemoveAt(0);
                }
            }

            var leftover = new List<AnimationClip>();
            foreach (KeyValuePair<string, List<AnimationClip>> pair in byName)
            {
                for (int i = 0; i < pair.Value.Count; i++)
                    leftover.Add(pair.Value[i]);
            }

            leftover.Sort((a, b) => string.Compare(a.name, b.name, System.StringComparison.Ordinal));
            result.AddRange(leftover);
            return result;
        }

        static List<FootstepTracksProfile> LoadProfiles()
        {
            var result = new List<FootstepTracksProfile>();
            string[] guids = AssetDatabase.FindAssets("t:FootstepTracksProfile");
            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                FootstepTracksProfile profile = AssetDatabase.LoadAssetAtPath<FootstepTracksProfile>(path);
                if (profile != null)
                    result.Add(profile);
            }

            result.Sort((a, b) => string.Compare(a.name, b.name, System.StringComparison.Ordinal));
            return result;
        }

        static string ProfileLabel(FootstepTracksProfile profile, IReadOnlyList<FootstepTracksProfile> profiles)
        {
            string name = profile != null ? profile.name : string.Empty;
            int count = 0;
            for (int i = 0; i < profiles.Count; i++)
            {
                if (profiles[i] != null && profiles[i].name == name)
                    count++;
            }

            if (count < 2)
                return name;

            string folder = System.IO.Path.GetFileName(System.IO.Path.GetDirectoryName(AssetDatabase.GetAssetPath(profile)));
            return string.IsNullOrEmpty(folder) ? name : name + " (" + folder + ")";
        }

        static string ClipLabel(IReadOnlyList<AnimationClip> clips, int index)
        {
            string name = clips[index].name;
            int count = 0;
            for (int i = 0; i < clips.Count; i++)
            {
                if (clips[i].name == name)
                    count++;
            }

            return count < 2 ? name : name + " (" + (index + 1) + ")";
        }

        public void SetPlayheadFromPreview(float time)
        {
            _timeline?.SetPlayhead(time);
            Repaint();
        }

        void SeekPreview(float time)
        {
            _preview?.Seek(time);
        }

        void OnUndoRedo()
        {
            if (_session != null && _profile != null)
                ApplyProfileChannels();
            RefreshAll();
        }

        void OnInspectorUpdate()
        {
            if (SyncProfileTracks())
                return;

            SyncTypePresentation();
        }

        void OnObjectChanges(ref ObjectChangeEventStream stream)
        {
            if (_applyingImport)
                return;

            if (AffectsCurrentProfile(ref stream))
                SyncProfileTracks();

            if (AffectsFootstepTypes(ref stream))
                ApplyTypeCatalog();
        }

        bool SyncProfileTracks()
        {
            if (_timeline == null || _session == null)
                return false;

            string revision = DescribeProfile();
            if (revision == _profileRevision)
                return false;

            ApplyProfileChannels();
            RefreshAll();
            return true;
        }

        string DescribeProfile()
        {
            var builder = new StringBuilder();
            if (_profile == null)
                return builder.ToString();

            builder.Append(_profile.GetEntityId().ToString()).Append('\n');
            if (_profile.tracks == null)
                return builder.ToString();

            for (int i = 0; i < _profile.tracks.Count; i++)
            {
                FootstepTrackDefinition track = _profile.tracks[i];
                if (track == null)
                {
                    builder.Append('\n');
                    continue;
                }

                builder.Append(track.name).Append('\n');
                builder.Append(track.bakeIntoAnimationCurve).Append('\n');
                builder.Append(ColorUtility.ToHtmlStringRGBA(track.color)).Append('\n');
            }

            return builder.ToString();
        }

        void ApplyProfileChannels()
        {
            var previous = _session.tracks ?? new List<FootstepTrackState>();
            var definitions = new List<FootstepTrackDefinition>();
            if (_profile != null && _profile.tracks != null)
            {
                for (int i = 0; i < _profile.tracks.Count; i++)
                {
                    if (_profile.tracks[i] != null)
                        definitions.Add(_profile.tracks[i]);
                }
            }

            bool renameInPlace = definitions.Count == previous.Count && !SameChannelNames(previous, definitions);
            var next = new List<FootstepTrackState>(definitions.Count);
            bool structureChanged = renameInPlace;

            if (renameInPlace)
            {
                for (int i = 0; i < definitions.Count; i++)
                {
                    FootstepTrackState existing = previous[i];
                    if (existing == null)
                        existing = new FootstepTrackState();

                    existing.name = definitions[i].name;
                    existing.color = definitions[i].color;
                    if ((existing.bakeIntoAnimationCurve ?? string.Empty) != (definitions[i].bakeIntoAnimationCurve ?? string.Empty))
                    {
                        existing.bakeIntoAnimationCurve = definitions[i].bakeIntoAnimationCurve;
                        structureChanged = true;
                    }
                    if (existing.steps == null)
                        existing.steps = new List<FootstepMarker>();
                    next.Add(existing);
                }
            }
            else
            {
                var used = new bool[previous.Count];
                List<FootstepType> types = null;
                for (int i = 0; i < definitions.Count; i++)
                {
                    int found = FindChannel(previous, used, definitions[i].name);
                    if (found >= 0)
                    {
                        used[found] = true;
                        FootstepTrackState existing = previous[found];
                        existing.color = definitions[i].color;
                        if ((existing.bakeIntoAnimationCurve ?? string.Empty) != (definitions[i].bakeIntoAnimationCurve ?? string.Empty))
                        {
                            existing.bakeIntoAnimationCurve = definitions[i].bakeIntoAnimationCurve;
                            structureChanged = true;
                        }
                        if (existing.steps == null)
                            existing.steps = new List<FootstepMarker>();
                        next.Add(existing);
                        continue;
                    }

                    structureChanged = true;
                    if (types == null)
                        types = LoadTypes();
                    next.Add(new FootstepTrackState
                    {
                        name = definitions[i].name,
                        bakeIntoAnimationCurve = definitions[i].bakeIntoAnimationCurve,
                        color = definitions[i].color,
                        steps = ReadSteps(definitions[i].BakedCurveName, types)
                    });
                }

                for (int i = 0; i < used.Length; i++)
                {
                    if (!used[i] && previous[i] != null)
                        structureChanged = true;
                }
            }

            _session.tracks = next;
            if (structureChanged)
                _session.hasUnbakedChanges = true;
            EditorUtility.SetDirty(_session);
        }

        static bool SameChannelNames(List<FootstepTrackState> previous, List<FootstepTrackDefinition> definitions)
        {
            if (previous.Count != definitions.Count)
                return false;

            var used = new bool[previous.Count];
            for (int i = 0; i < definitions.Count; i++)
            {
                int found = FindChannel(previous, used, definitions[i].name);
                if (found < 0)
                    return false;

                used[found] = true;
            }

            return true;
        }

        static int FindChannel(List<FootstepTrackState> previous, bool[] used, string name)
        {
            for (int i = 0; i < previous.Count; i++)
            {
                if (used[i] || previous[i] == null || previous[i].name != name)
                    continue;
                return i;
            }

            return -1;
        }

        bool AffectsCurrentProfile(ref ObjectChangeEventStream stream)
        {
            for (int i = 0; i < stream.length; i++)
            {
                switch (stream.GetEventType(i))
                {
                    case ObjectChangeKind.ChangeAssetObjectProperties:
                        stream.GetChangeAssetObjectPropertiesEvent(i, out ChangeAssetObjectPropertiesEventArgs changed);
                        if (ResolveChangedObject(changed) is FootstepTracksProfile profile && profile == _profile)
                            return true;
                        break;
                    case ObjectChangeKind.DestroyAssetObject:
                        return true;
                }
            }

            return false;
        }

        void SyncTypePresentation()
        {
            if (_timeline == null || _session == null)
                return;

            string revision = DescribeReferencedTypes();
            if (revision == _typeRevision)
                return;

            _timeline.RefreshTypePresentation(LoadTypes());
            _typeRevision = DescribeReferencedTypes();
        }

        string DescribeReferencedTypes()
        {
            var builder = new StringBuilder();
            if (_session?.tracks == null)
                return builder.ToString();

            for (int t = 0; t < _session.tracks.Count; t++)
            {
                List<FootstepMarker> steps = _session.tracks[t]?.steps;
                if (steps == null)
                    continue;

                for (int i = 0; i < steps.Count; i++)
                {
                    FootstepMarker marker = steps[i];
                    if (marker == null)
                        continue;

                    FootstepType type = marker.type;
                    if (type == null)
                    {
                        builder.Append("0\n");
                        continue;
                    }

                    builder.Append(type.GetEntityId().ToString()).Append('\n');
                    builder.Append(type.name).Append('\n');
                    Texture icon = type.icon;
                    if (icon == null)
                    {
                        builder.Append("0\n");
                        continue;
                    }

                    builder.Append(icon.GetEntityId().ToString()).Append('\n');
                    builder.Append(icon.updateCount).Append('\n');
                }
            }

            return builder.ToString();
        }

        static UnityEngine.Object ResolveChangedObject(ChangeAssetObjectPropertiesEventArgs args)
        {
            return EditorUtility.EntityIdToObject(args.entityId);
        }

        static UnityEngine.Object ResolveChangedObject(CreateAssetObjectEventArgs args)
        {
            return EditorUtility.EntityIdToObject(args.entityId);
        }

        static bool AffectsFootstepTypes(ref ObjectChangeEventStream stream)
        {
            for (int i = 0; i < stream.length; i++)
            {
                switch (stream.GetEventType(i))
                {
                    case ObjectChangeKind.ChangeAssetObjectProperties:
                        stream.GetChangeAssetObjectPropertiesEvent(i, out ChangeAssetObjectPropertiesEventArgs changed);
                        if (ResolveChangedObject(changed) is FootstepType)
                            return true;
                        break;
                    case ObjectChangeKind.CreateAssetObject:
                        stream.GetCreateAssetObjectEvent(i, out CreateAssetObjectEventArgs created);
                        if (ResolveChangedObject(created) is FootstepType)
                            return true;
                        break;
                    case ObjectChangeKind.DestroyAssetObject:
                        return true;
                }
            }

            return false;
        }

        static List<FootstepType> LoadTypes()
        {
            return FootstepTypeSettingsWindow.LoadTypes();
        }
    }

    sealed class FootstepRenameTrackWindow : EditorWindow
    {
        string _name = string.Empty;
        string _trackName = string.Empty;
        string _applyLabel = "Rename";
        string _fieldLabel = "Name";
        bool _allowEmpty;
        bool _curve;
        Action<string> _apply;
        bool _focused;
        GUIStyle _help;

        public static void Show(string currentName, Action<string> apply)
        {
            Open("Rename Track", "Name", currentName, null, false, false, "Rename", apply, 88f);
        }

        public static void ShowCurve(string trackName, string currentCurve, Action<string> apply)
        {
            Open(
                "Set Animation Curve Name",
                "Animation Curve",
                currentCurve,
                trackName,
                true,
                true,
                "Apply",
                apply,
                220f);
        }

        static void Open(
            string title,
            string fieldLabel,
            string current,
            string trackName,
            bool allowEmpty,
            bool curve,
            string applyLabel,
            Action<string> apply,
            float height)
        {
            var window = CreateInstance<FootstepRenameTrackWindow>();
            window._name = current ?? string.Empty;
            window._trackName = trackName ?? string.Empty;
            window._fieldLabel = fieldLabel;
            window._allowEmpty = allowEmpty;
            window._curve = curve;
            window._applyLabel = applyLabel;
            window._apply = apply;
            window.titleContent = new GUIContent(title);
            float fitted = curve ? window.ContentHeight(440f) : height;
            window.minSize = new Vector2(440f, fitted);
            window.maxSize = new Vector2(720f, fitted);
            window.ShowUtility();
        }

        void OnGUI()
        {
            Event evt = Event.current;
            if (evt.type == EventType.KeyDown && evt.keyCode == KeyCode.Escape)
            {
                Close();
                evt.Use();
                return;
            }

            bool submit = evt.type == EventType.KeyDown
                && (evt.keyCode == KeyCode.Return || evt.keyCode == KeyCode.KeypadEnter);

            EditorGUILayout.Space(8f);
            GUI.SetNextControlName("FootstepTrackName");
            _name = EditorGUILayout.TextField(_fieldLabel, _name);
            if (!_focused)
            {
                EditorGUI.FocusTextInControl("FootstepTrackName");
                _focused = true;
            }

            if (_curve)
            {
                EditorGUILayout.Space(6f);
                string help = CurveHelp();
                GUIStyle style = HelpStyle();
                float width = Mathf.Max(120f, position.width - 24f);
                float height = style.CalcHeight(new GUIContent(help), width);
                EditorGUILayout.LabelField(help, style, GUILayout.Height(height));
            }

            EditorGUILayout.Space(8f);
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("Cancel", GUILayout.Width(88f)))
                    Close();
                if (GUILayout.Button(_applyLabel, GUILayout.Width(88f)) || submit)
                    Apply(submit ? evt : null);
            }

            if (evt.type == EventType.Repaint)
                FitToContent();
        }

        void FitToContent()
        {
            float content = GUILayoutUtility.GetLastRect().yMax + 8f;
            if (content < 1f || Mathf.Abs(position.height - content) < 1f)
                return;

            minSize = new Vector2(440f, content);
            maxSize = new Vector2(720f, content);
            Rect next = position;
            next.height = content;
            position = next;
        }

        float ContentHeight(float width)
        {
            float height = 8f + EditorGUIUtility.singleLineHeight;
            if (_curve)
            {
                float helpWidth = Mathf.Max(120f, width - 24f);
                height += 6f + HelpStyle().CalcHeight(new GUIContent(CurveHelp()), helpWidth);
            }

            return height + 8f + EditorGUIUtility.singleLineHeight + 10f;
        }

        string CurveHelp()
        {
            string track = string.IsNullOrEmpty(_trackName) ? "(unnamed)" : _trackName;
            string typed = _name != null ? _name.Trim() : string.Empty;
            string target = string.IsNullOrEmpty(typed) ? track : typed;
            string always = "Chooses which animation curve this track's steps are baked into.";
            if (string.IsNullOrEmpty(typed))
                return always + "\nThe AnimationCurve will be baked into <b>" + track + "</b>.";

            return always
                + "\nThe AnimationCurve will be baked into <b>" + target + "</b>. "
                + "You can leave this field empty to bake into a curve named after the track.";
        }

        GUIStyle HelpStyle()
        {
            if (_help != null)
                return _help;

            _help = new GUIStyle(EditorStyles.wordWrappedMiniLabel)
            {
                richText = true,
                wordWrap = true
            };
            return _help;
        }

        void Apply(Event submit)
        {
            string next = _name != null ? _name.Trim() : string.Empty;
            if (!_allowEmpty && string.IsNullOrEmpty(next))
                return;

            submit?.Use();
            Action<string> apply = _apply;
            _apply = null;
            Close();
            apply?.Invoke(next);
        }
    }

    sealed class FootstepTrackColorWindow : EditorWindow
    {
        Color _color;
        Action<Color> _apply;

        public static void Show(Color current, Action<Color> apply)
        {
            var window = CreateInstance<FootstepTrackColorWindow>();
            current.a = 1f;
            window._color = current;
            window._apply = apply;
            window.titleContent = new GUIContent("Change Track Color");
            window.minSize = new Vector2(360f, 88f);
            window.maxSize = new Vector2(640f, 88f);
            window.ShowUtility();
        }

        void OnGUI()
        {
            Event evt = Event.current;
            if (evt.type == EventType.KeyDown && evt.keyCode == KeyCode.Escape)
            {
                Close();
                evt.Use();
                return;
            }

            bool submit = evt.type == EventType.KeyDown
                && (evt.keyCode == KeyCode.Return || evt.keyCode == KeyCode.KeypadEnter);

            EditorGUILayout.Space(8f);
            _color = EditorGUILayout.ColorField(new GUIContent("Color"), _color, true, false, false);
            _color.a = 1f;

            EditorGUILayout.Space(8f);
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("Cancel", GUILayout.Width(88f)))
                    Close();
                if (GUILayout.Button("Apply", GUILayout.Width(88f)) || submit)
                    Apply(submit ? evt : null);
            }
        }

        void Apply(Event submit)
        {
            submit?.Use();
            Action<Color> apply = _apply;
            _apply = null;
            Color color = _color;
            color.a = 1f;
            Close();
            apply?.Invoke(color);
        }
    }
}
