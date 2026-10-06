using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using MadeYellow.WAVES.Actors;
using MadeYellow.WAVES.AudioVisualEffects.Modules;
using MadeYellow.WAVES.Surfaces;
using UnityEditor;
using UnityEngine;

namespace MadeYellow.WAVES.Editor
{
    /// <summary>WAVES Module Browser. Edits the module presets on a WAVES component.</summary>
    public sealed class WAVESBrowserWindow : EditorWindow
    {
        const float Cell = 48f;
        const float CardWidth = 200f;
        const float CardHeight = 96f;
        const string WindowTitle = "WAVES Module Browser";
        const float PresetPopup = 20f;
        const float DetailWidth = 420f;
        const float Border = 3f;
        const float AxisIcon = 14f;
        const float ColumnLabelPad = 10f;
        const float PanelGap = 10f;
        const float DetailInset = 10f;
        const float DetailBarHeight = 48f;
        const float HeaderMin = 72f;
        const float HeaderMax = 180f;
        const float RowHeaderMin = 96f;
        const float RowHeaderMax = 200f;
        const float FallbackEmptyAlpha = 0.4f;
        const float StripesAlpha = 0.1f;
        const float CaptionGap = 2f;
        const string StripesPath = "Packages/com.madeyellow.waves/Editor/Backgrounds/stripes-background.png";

        [SerializeField] AudioVisualEffects.WAVES _waves;
        [SerializeField] WAVESModuleBase _loose;
        [SerializeField] WAVESModuleBase _selected;
        [SerializeField] bool _actorsAreRows = true;
        [SerializeField] bool _hasSelection;
        [SerializeField] SurfaceTypeDefinition _selectedSurface;
        [SerializeField] ActorProfile _selectedActor;

        readonly List<AxisEntry> _rows = new List<AxisEntry>();
        readonly List<AxisEntry> _columns = new List<AxisEntry>();
        readonly Dictionary<Pair, bool> _groups = new Dictionary<Pair, bool>();
        readonly Dictionary<Type, List<WAVESModuleBase>> _presets = new Dictionary<Type, List<WAVESModuleBase>>();

        SerializedObject _moduleObject;
        Vector2 _cardScroll;
        Vector2 _matrixScroll;
        Vector2 _detailScroll;
        int _hoverRow = -1;
        int _hoverColumn = -1;
        int _axisPress = -1;
        int _axisDrop = -1;
        bool _axisPressRow;
        bool _axisDragging;
        Vector2 _axisPressPos;
        Rect _rowHeaders;
        Rect _columnHeaders;
        GUIStyle _rowLabel;
        GUIStyle _columnLabel;
        GUIStyle _rowCaption;
        GUIStyle _columnCaption;
        GUIStyle _subtitle;
        GUIStyle _cardTitle;
        bool _cardPro;
        GUIStyle _emptyTitle;
        GUIStyle _emptyAction;
        GUIStyle _emptyHint;
        bool _emptyPro;
        GUIStyle _plusLabel;
        bool _plusPro;
        static GUIStyle _checkLabel;
        static Texture2D _stripes;
        GUIStyle _moduleStripStyle;
        GUIStyle _detailPanelStyle;
        GUIStyle _bleedStyle;
        GUIStyle _detailContentStyle;
        GUIStyle _barRowStyle;
        Rect _detailPanelRect;
        GUIStyle _removeButton;
        GUISkin _removeButtonSkin;
        Texture2D _cardGradient;
        Texture2D _moduleStrip;
        Texture2D _detailPanel;
        GUIStyle _hintStyle;
        bool _hintPro;
        bool _proSkin;
        int _paletteRevision = -1;
        bool _labelPro;
        int _labelRevision = -1;

        struct AxisEntry
        {
            public UnityEngine.Object Asset;
            public string Label;
            public string Caption;
            public Color Color;
            public bool Plus;
            public bool Actor;
        }

        readonly struct Pair : IEquatable<Pair>
        {
            public readonly EntityId Surface;
            public readonly EntityId Actor;

            public Pair(EntityId surface, EntityId actor)
            {
                Surface = surface;
                Actor = actor;
            }

            public bool Equals(Pair other)
            {
                return Surface == other.Surface && Actor == other.Actor;
            }

            public override bool Equals(object obj)
            {
                return obj is Pair other && Equals(other);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    return (Surface.GetHashCode() * 397) ^ Actor.GetHashCode();
                }
            }
        }

        /// <summary>Opens the browser on the current selection.</summary>
        [MenuItem("Window/MadeYellow/WAVES/Module Browser")]
        public static void Open()
        {
            var window = GetWindow<WAVESBrowserWindow>();
            window.titleContent = new GUIContent(WindowTitle);
            window.Show();
        }

        /// <summary>Opens the browser on <paramref name="waves"/> and selects <paramref name="module"/>.</summary>
        public static void Open(AudioVisualEffects.WAVES waves, WAVESModuleBase module)
        {
            var window = GetWindow<WAVESBrowserWindow>();
            window.titleContent = new GUIContent(WindowTitle);
            window.Bind(waves, module);
            window.Show();
            window.Focus();
        }

        void Bind(AudioVisualEffects.WAVES waves, WAVESModuleBase module)
        {
            _waves = waves;
            _loose = waves == null ? module : null;
            _selected = null;
            _hasSelection = false;
            if (waves != null && module != null)
            {
                for (int i = 0; i < waves.ModuleCount; i++)
                {
                    if (waves.GetModule(i) != module)
                        continue;

                    _selected = module;
                    break;
                }
            }

            Repaint();
        }

        void OnEnable()
        {
            titleContent = new GUIContent(WindowTitle);
            minSize = new Vector2(880f, 520f);
            wantsMouseMove = true;
            WAVESCatalog.Retain();
            EditorApplication.projectChanged += OnCatalogChanged;
            Undo.undoRedoPerformed += OnCatalogChanged;
        }

        void OnDisable()
        {
            EditorApplication.projectChanged -= OnCatalogChanged;
            Undo.undoRedoPerformed -= OnCatalogChanged;
            WAVESCatalog.Release();
            DisposeModuleObject();
        }

        void OnCatalogChanged()
        {
            _presets.Clear();
            WAVESCatalog.Refresh();
            WAVESModuleCatalog.Invalidate();
            Repaint();
        }

        internal static void RepaintOpen()
        {
            WAVESBrowserWindow[] windows = Resources.FindObjectsOfTypeAll<WAVESBrowserWindow>();
            for (int i = 0; i < windows.Length; i++)
                windows[i].Repaint();
        }

        void OnSelectionChange()
        {
            GameObject selected = Selection.activeGameObject;
            if (selected == null)
                return;

            var waves = selected.GetComponent<AudioVisualEffects.WAVES>();
            if (waves == null || waves == _waves)
                return;

            _waves = waves;
            _loose = null;
            _selected = null;
            _hasSelection = false;
            Repaint();
        }

        void OnGUI()
        {
            if (Event.current.type == EventType.MouseMove)
                Repaint();

            if (Event.current.type == EventType.Repaint)
                EditorGUI.DrawRect(new Rect(0f, 0f, position.width, position.height), WAVESChrome.Canvas);

            DrawWavesField();
            DrawCards();
            DrawBody();
        }

        void DrawWavesField()
        {
            EditorGUI.BeginChangeCheck();
            var next = (AudioVisualEffects.WAVES)EditorGUILayout.ObjectField(
                "WAVES",
                _waves,
                typeof(AudioVisualEffects.WAVES),
                true);
            if (!EditorGUI.EndChangeCheck())
                return;

            _waves = next;
            _loose = null;
            _selected = null;
            _hasSelection = false;
            GUIUtility.ExitGUI();
        }

        void DrawCards()
        {
            if (_waves == null)
                return;

            EnsureChrome();
            Rect strip = EditorGUILayout.BeginVertical(_moduleStripStyle, GUILayout.ExpandWidth(true));
            if (Event.current.type == EventType.Repaint)
                EditorGUI.DrawRect(strip, WAVESChrome.Strip);
            _cardScroll = EditorGUILayout.BeginScrollView(
                _cardScroll,
                false,
                false,
                GUI.skin.horizontalScrollbar,
                GUIStyle.none,
                GUIStyle.none,
                GUILayout.Height(CardStripHeight(WAVESModuleCatalog.Slots.Count)));
            EditorGUILayout.BeginHorizontal();
            IReadOnlyList<WAVESModuleCatalog.Slot> slots = WAVESModuleCatalog.Slots;
            for (int i = 0; i < slots.Count; i++)
            {
                if (i > 0)
                    GUILayout.Space(8f);

                WAVESModuleCatalog.Slot slot = slots[i];
                WAVESModuleBase module = AssignedModule(slot.Type);
                EditorGUILayout.BeginVertical(GUILayout.Width(CardWidth));
                if (module == null)
                    DrawEmptyCard(slot.Type, slot.Title);
                else if (DrawFilledCard(module))
                {
                    _moduleObject?.ApplyModifiedProperties();
                    Undo.RecordObject(_waves, "Remove WAVES Module");
                    int index = ModuleIndex(module);
                    if (index >= 0)
                        _waves.RemoveModuleAt(index);
                    EditorUtility.SetDirty(_waves);
                    if (_selected == module)
                        _selected = null;
                    _hasSelection = false;
                    GUIUtility.ExitGUI();
                }

                GUILayout.Space(4f);
                DrawPresetPopup(slot.Type, module);
                EditorGUILayout.EndVertical();
            }

            EditorGUILayout.EndHorizontal();
            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();
            if (Event.current.type == EventType.Repaint)
            {
                Color line = BarLine();
                EditorGUI.DrawRect(new Rect(strip.x, strip.y, strip.width, 1f), line);
                EditorGUI.DrawRect(new Rect(strip.x, strip.yMax - 1f, strip.width, 1f), line);
            }
        }

        float CardStripHeight(int slotCount)
        {
            float content = CardHeight + 4f + PresetPopup + 6f;
            if (slotCount <= 0)
                return content;

            float contentWidth = slotCount * CardWidth + Mathf.Max(0, slotCount - 1) * 8f;
            float viewWidth = Mathf.Max(0f, position.width - 24f);
            if (contentWidth <= viewWidth)
                return content;

            float bar = GUI.skin.horizontalScrollbar.fixedHeight;
            if (bar < 12f)
                bar = 15f;

            return content + bar;
        }

        void DrawEmptyCard(Type type, string title)
        {
            Rect rect = GUILayoutUtility.GetRect(CardWidth, CardHeight, GUILayout.Width(CardWidth), GUILayout.Height(CardHeight));
            bool hover = rect.Contains(Event.current.mousePosition);
            if (Event.current.type == EventType.Repaint)
            {
                if (hover)
                {
                    Color wash = HoverWash();
                    EditorGUI.DrawRect(rect, new Color(wash.r, wash.g, wash.b, 0.12f));
                }

                DrawOutline(rect, hover ? WAVESChrome.Ink : WAVESChrome.Line, 1f);
                EnsureEmptyStyles();
                float innerWidth = Mathf.Max(0f, rect.width - 16f);
                var nameContent = new GUIContent(string.IsNullOrEmpty(title) ? "Module" : title);
                var actionContent = new GUIContent("Click to create preset");
                var hintContent = new GUIContent("Or select existing preset from drop-down below");
                float nameHeight = _emptyTitle.CalcHeight(nameContent, innerWidth);
                float actionHeight = _emptyAction.CalcHeight(actionContent, innerWidth);
                float hintHeight = _emptyHint.CalcHeight(hintContent, innerWidth);
                const float gap = 2f;
                float block = nameHeight + gap + actionHeight + gap + hintHeight;
                float y = rect.y + Mathf.Max(4f, (rect.height - block) * 0.5f);
                float x = rect.x + 8f;
                GUI.Label(new Rect(x, y, innerWidth, nameHeight), nameContent, _emptyTitle);
                y += nameHeight + gap;
                GUI.Label(new Rect(x, y, innerWidth, actionHeight), actionContent, _emptyAction);
                y += actionHeight + gap;
                GUI.Label(new Rect(x, y, innerWidth, hintHeight), hintContent, _emptyHint);
            }

            string tip = string.IsNullOrEmpty(title) ? "Create a new module preset" : "Create a new " + title + " preset";
            if (GUI.Button(rect, new GUIContent(string.Empty, tip), GUIStyle.none))
                CreatePreset(type, title);
        }

        bool DrawFilledCard(WAVESModuleBase module)
        {
            Rect rect = GUILayoutUtility.GetRect(CardWidth, CardHeight, GUILayout.Width(CardWidth), GUILayout.Height(CardHeight));
            bool selected = module == _selected;
            if (Event.current.type == EventType.Repaint)
            {
                GUI.DrawTexture(rect, CardGradient(), ScaleMode.StretchToFill, false);
                if (selected)
                    DrawOutline(rect, WAVESChrome.Gold, 1f);
            }

            Rect remove = new Rect(rect.xMax - 18f, rect.y + 2f, 16f, 16f);
            bool removed = WAVESEditor.DrawRemoveButton(remove, "Remove from this WAVES");
            string title = WAVESModuleCatalog.Title(module);
            EnsureCardStyles();
            var titleContent = new GUIContent(title);
            float textWidth = Mathf.Max(0f, rect.width - 28f);
            float titleHeight = _cardTitle.CalcHeight(titleContent, textWidth);
            float y = rect.y + (rect.height - titleHeight) * 0.5f;
            GUI.Label(new Rect(rect.x + 8f, y, textWidth, titleHeight), titleContent, _cardTitle);

            Event click = Event.current;
            if (!removed &&
                click.type == EventType.MouseDown &&
                click.button == 0 &&
                rect.Contains(click.mousePosition) &&
                !remove.Contains(click.mousePosition))
            {
                if (_selected != module)
                {
                    _selected = module;
                    _hasSelection = false;
                    click.Use();
                    Repaint();
                    GUIUtility.ExitGUI();
                }

                click.Use();
                Repaint();
            }

            return removed;
        }

        void DrawPresetPopup(Type type, WAVESModuleBase module)
        {
            Rect popup = GUILayoutUtility.GetRect(
                CardWidth,
                PresetPopup,
                GUILayout.Width(CardWidth),
                GUILayout.Height(PresetPopup));
            List<WAVESModuleBase> presets = Presets(type);
            if (module != null && !presets.Contains(module))
            {
                presets = new List<WAVESModuleBase>(presets) { module };
                presets.Sort(ComparePresetName);
            }

            if (module == null && presets.Count == 0)
            {
                EditorGUI.BeginDisabledGroup(true);
                EditorGUI.DropdownButton(popup, new GUIContent("No presets"), FocusType.Passive);
                EditorGUI.EndDisabledGroup();
                return;
            }

            string label = module != null ? module.name : "Select preset";
            if (!EditorGUI.DropdownButton(popup, new GUIContent(label), FocusType.Keyboard))
                return;

            ShowPresetMenu(type, module, presets);
        }

        void ShowPresetMenu(Type type, WAVESModuleBase current, List<WAVESModuleBase> presets)
        {
            if (presets == null)
                presets = Presets(type);

            var counts = new Dictionary<string, int>();
            for (int i = 0; i < presets.Count; i++)
            {
                string name = presets[i] != null ? presets[i].name : string.Empty;
                counts.TryGetValue(name, out int count);
                counts[name] = count + 1;
            }

            var menu = new GenericMenu();
            if (presets.Count == 0)
            {
                menu.AddDisabledItem(new GUIContent("No presets"));
                menu.ShowAsContext();
                return;
            }

            for (int i = 0; i < presets.Count; i++)
            {
                WAVESModuleBase preset = presets[i];
                if (preset == null)
                    continue;

                string label = preset.name.Replace("/", "-");
                if (counts.TryGetValue(preset.name, out int count) && count > 1)
                {
                    string folder = Path.GetDirectoryName(AssetDatabase.GetAssetPath(preset));
                    if (!string.IsNullOrEmpty(folder))
                        label = label + " (" + folder.Replace("/", "-").Replace("\\", "/") + ")";
                }

                if (preset == current)
                {
                    menu.AddDisabledItem(new GUIContent(label));
                    continue;
                }

                WAVESModuleBase chosen = preset;
                menu.AddItem(new GUIContent(label), false, () => AssignPreset(chosen));
            }

            menu.ShowAsContext();
        }

        List<WAVESModuleBase> Presets(Type type)
        {
            if (type == null)
                return new List<WAVESModuleBase>();
            if (_presets.TryGetValue(type, out List<WAVESModuleBase> cached) && cached != null)
                return cached;

            List<WAVESModuleBase> loaded = LoadPresets(type);
            _presets[type] = loaded;
            return loaded;
        }

        static List<WAVESModuleBase> LoadPresets(Type type)
        {
            var result = new List<WAVESModuleBase>();
            if (type == null || string.IsNullOrEmpty(type.Name))
                return result;

            string[] guids;
            try
            {
                guids = AssetDatabase.FindAssets("t:" + type.Name);
            }
            catch (Exception)
            {
                return result;
            }

            for (int i = 0; i < guids.Length; i++)
            {
                var preset = AssetDatabase.LoadAssetAtPath<WAVESModuleBase>(AssetDatabase.GUIDToAssetPath(guids[i]));
                if (preset != null && preset.GetType() == type)
                    result.Add(preset);
            }

            result.Sort(ComparePresetName);
            return result;
        }

        static int ComparePresetName(WAVESModuleBase left, WAVESModuleBase right)
        {
            if (left == null && right == null)
                return 0;
            if (left == null)
                return 1;
            if (right == null)
                return -1;

            return string.CompareOrdinal(left.name, right.name);
        }

        void AssignPreset(WAVESModuleBase preset)
        {
            if (_waves == null || preset == null)
                return;

            Undo.RecordObject(_waves, "Assign WAVES Module");
            _waves.AssignModule(preset);
            EditorUtility.SetDirty(_waves);
            _selected = preset;
            _hasSelection = false;
            DisposeModuleObject();
            Repaint();
        }

        void CreatePreset(Type type, string title)
        {
            if (_waves == null || type == null)
                return;

            if (string.IsNullOrEmpty(title))
                title = WAVESModuleCatalog.Title(type);

            string path = EditorUtility.SaveFilePanelInProject(
                "Create " + title + " Preset",
                PresetFileName(title),
                "asset",
                "Choose where to save the preset.");
            if (string.IsNullOrEmpty(path))
                return;

            var preset = ScriptableObject.CreateInstance(type) as WAVESModuleBase;
            if (preset == null)
                return;

            AssetDatabase.CreateAsset(preset, path);
            AssetDatabase.SaveAssets();
            AssetDatabase.ImportAsset(path);
            _presets.Remove(type);
            Undo.RecordObject(_waves, "Create WAVES Module Preset");
            _waves.AssignModule(preset);
            EditorUtility.SetDirty(_waves);
            _selected = preset;
            _hasSelection = false;
            DisposeModuleObject();
            GUIUtility.ExitGUI();
        }

        static string PresetFileName(string title)
        {
            if (string.IsNullOrWhiteSpace(title))
                title = "Module";

            char[] invalid = Path.GetInvalidFileNameChars();
            var builder = new StringBuilder(title.Length);
            for (int i = 0; i < title.Length; i++)
            {
                char character = title[i];
                bool skip = false;
                for (int j = 0; j < invalid.Length; j++)
                {
                    if (invalid[j] != character)
                        continue;

                    skip = true;
                    break;
                }

                builder.Append(skip ? ' ' : character);
            }

            string clean = builder.ToString().Trim();
            if (clean.Length == 0)
                clean = "Module";

            return "New " + clean + " Preset";
        }

        WAVESModuleBase AssignedModule(Type type)
        {
            if (_waves == null || type == null)
                return null;

            int count = _waves.ModuleCount;
            for (int i = 0; i < count; i++)
            {
                WAVESModuleBase module = _waves.GetModule(i);
                if (module != null && module.GetType() == type)
                    return module;
            }

            return null;
        }

        int ModuleIndex(WAVESModuleBase module)
        {
            if (_waves == null || module == null)
                return -1;

            int count = _waves.ModuleCount;
            for (int i = 0; i < count; i++)
            {
                if (_waves.GetModule(i) == module)
                    return i;
            }

            return -1;
        }

        void DrawBody()
        {
            if (_waves == null && _loose == null)
            {
                EditorGUILayout.HelpBox("Select a WAVES component to edit its modules.", MessageType.Info);
                return;
            }

            WAVESModuleBase module = CurrentModule();
            if (module == null)
            {
                WAVESModuleEditors.DrawCenteredNotice(
                    "Select Module to edit",
                    "WAVES is a modular system. To set up its behavior, add the modules you need by creating or selecting a preset. The module editor will appear here.");
                return;
            }

            EnsureModuleObject(module);
            _moduleObject.Update();
            if (WAVESModuleEditors.TryDraw(module, _moduleObject))
            {
                _moduleObject.ApplyModifiedProperties();
                return;
            }

            SerializedProperty cells = _moduleObject.FindProperty("_cells");
            if (!IsActorSurfaceList(module, cells))
            {
                WAVESModuleEditors.DrawCenteredNotice("No editor", "This module has no editor yet.");
                _moduleObject.ApplyModifiedProperties();
                return;
            }

            ReadGroups(cells);
            BuildAxes();
            EnsureChrome();
            EditorGUILayout.BeginHorizontal(GUILayout.ExpandHeight(true));
            GUILayout.Space(PanelGap);
            EditorGUILayout.BeginVertical(GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
            Rect matrix = GUILayoutUtility.GetRect(
                10f,
                10000f,
                10f,
                10000f,
                GUILayout.ExpandWidth(true),
                GUILayout.ExpandHeight(true));
            DrawMatrix(matrix);
            EditorGUILayout.EndVertical();
            GUILayout.Space(8f);
            EditorGUILayout.BeginVertical(GUILayout.Width(DetailWidth), GUILayout.ExpandHeight(true));
            _detailPanelRect = EditorGUILayout.BeginVertical(BleedStyle(), GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
            if (Event.current.type == EventType.Repaint)
                EditorGUI.DrawRect(_detailPanelRect, DetailPanelColor());
            DrawDetail(module, cells);
            EditorGUILayout.EndVertical();
            GUILayout.Space(PanelGap);
            EditorGUILayout.EndVertical();
            GUILayout.Space(PanelGap);
            EditorGUILayout.EndHorizontal();
            _moduleObject.ApplyModifiedProperties();
        }

        WAVESModuleBase CurrentModule()
        {
            if (_waves == null)
                return _loose;

            if (_selected == null)
                return null;

            if (ModuleIndex(_selected) < 0)
            {
                _selected = null;
                return null;
            }

            return _selected;
        }

        void DrawDetail(WAVESModuleBase module, SerializedProperty cells)
        {
            if (!_hasSelection)
            {
                GUILayout.Space(12f);
                EditorGUILayout.BeginHorizontal();
                GUILayout.Space(12f);
                GUILayout.Label("Click a cell to select effects for <b>Surface type</b> and <b>Actor</b>.", HintStyle());
                GUILayout.Space(12f);
                EditorGUILayout.EndHorizontal();
                return;
            }

            int index = FindCellIndex(cells, _selectedSurface, _selectedActor);
            SerializedProperty cell = index >= 0 ? cells.GetArrayElementAtIndex(index) : null;
            if (cell == null)
            {
                GUILayout.FlexibleSpace();
                EditorGUILayout.BeginHorizontal();
                GUILayout.FlexibleSpace();
                Rect createRect = GUILayoutUtility.GetRect(220f, 36f, GUILayout.Width(220f), GUILayout.Height(36f));
                bool create = WAVESChrome.FlatButton(createRect, "Create Effects Group");
                GUILayout.FlexibleSpace();
                EditorGUILayout.EndHorizontal();
                GUILayout.FlexibleSpace();
                if (!create)
                    return;

                EnsureCell(cells, _selectedSurface, _selectedActor);
                _moduleObject.ApplyModifiedProperties();
                GUIUtility.ExitGUI();
            }

            DrawGroupHeader(cell);
            string scope = module.GetEntityId() + "." + Identity(_selectedSurface) + "." + Identity(_selectedActor);
            SerializedProperty data = cell.FindPropertyRelative("_data");
            if (WAVESModuleDrawers.Scrolls(module))
            {
                _detailScroll = EditorGUILayout.BeginScrollView(
                    _detailScroll,
                    false,
                    false,
                    GUIStyle.none,
                    GUI.skin.verticalScrollbar,
                    DetailContentStyle(),
                    GUILayout.ExpandWidth(true),
                    GUILayout.ExpandHeight(true));
                GUILayout.Space(DetailInset);
                EditorGUILayout.BeginHorizontal();
                GUILayout.Space(DetailInset);
                EditorGUILayout.BeginVertical(GUILayout.ExpandWidth(true));
                if (!WAVESModuleDrawers.Draw(module, data, scope))
                    EditorGUILayout.HelpBox("This module has no group editor.", MessageType.Info);
                EditorGUILayout.EndVertical();
                GUILayout.Space(DetailInset);
                EditorGUILayout.EndHorizontal();
                GUILayout.Space(DetailInset);
                EditorGUILayout.EndScrollView();
            }
            else
            {
                EditorGUILayout.BeginVertical(GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
                if (!WAVESModuleDrawers.Draw(module, data, scope))
                    EditorGUILayout.HelpBox("This module has no group editor.", MessageType.Info);
                EditorGUILayout.EndVertical();
            }
            if (!DrawRemoveBar())
                return;

            RemoveGroup(_selectedSurface, _selectedActor);
            GUIUtility.ExitGUI();
        }

        void DrawGroupHeader(SerializedProperty cell)
        {
            string actor = _selectedActor != null ? _selectedActor.name : "Any Actor";
            string surface = _selectedSurface != null ? _selectedSurface.name : "Any Surface";
            Rect header = FullWidthRow(DetailBarHeight);
            const float titleHeight = 16f;
            const float subtitleHeight = 14f;
            const float gap = 1f;
            const float switchWidth = 40f;
            const float switchHeight = 18f;
            float block = titleHeight + gap + subtitleHeight;
            float textY = header.y + (header.height - block) * 0.5f;
            float textX = header.x + DetailInset;
            Rect toggle = new Rect(
                header.xMax - switchWidth - DetailInset,
                header.y + (header.height - switchHeight) * 0.5f,
                switchWidth,
                switchHeight);
            float textWidth = Mathf.Max(0f, toggle.x - textX - DetailInset);
            if (Event.current.type == EventType.Repaint)
            {
                EditorGUI.DrawRect(header, BarColor());
                EditorGUI.DrawRect(new Rect(header.x, header.yMax - 1f, header.width, 1f), BarLine());
            }

            EnsureTextStyles();
            GUI.Label(new Rect(textX, textY, textWidth, titleHeight), actor, EditorStyles.boldLabel);
            GUI.Label(
                new Rect(textX, textY + titleHeight + gap, textWidth, subtitleHeight),
                "on <b>" + Rich(surface) + "</b>",
                _subtitle);

            if (cell == null)
                return;

            SerializedProperty enabled = cell.FindPropertyRelative("_enabled");
            if (enabled == null)
                return;

            bool on = enabled.boolValue;
            string tooltip = on ? "Click to disable this group" : "Click to enable this group";
            if (DrawSwitch(toggle, on, tooltip))
            {
                enabled.boolValue = !on;
                Repaint();
            }
        }

        bool DrawRemoveBar()
        {
            Rect footer = FullWidthRow(DetailBarHeight);
            if (Event.current.type == EventType.Repaint)
            {
                EditorGUI.DrawRect(footer, BarColor());
                EditorGUI.DrawRect(new Rect(footer.x, footer.y, footer.width, 1f), BarLine());
            }

            var button = new Rect(
                footer.x + DetailInset,
                footer.y + DetailInset,
                Mathf.Max(0f, footer.width - DetailInset * 2f),
                Mathf.Max(0f, footer.height - DetailInset * 2f));
            return GUI.Button(button, "Remove Effects Group", RemoveButtonStyle());
        }

        GUIStyle RemoveButtonStyle()
        {
            if (_removeButton != null && _removeButtonSkin == GUI.skin)
                return _removeButton;

            _removeButtonSkin = GUI.skin;
            _removeButton = new GUIStyle(GUI.skin.button)
            {
                alignment = TextAnchor.MiddleCenter,
                fixedHeight = 0f,
                stretchHeight = true
            };
            return _removeButton;
        }

        static Color BarColor()
        {
            return WAVESChrome.PlateRaised;
        }

        static Color BarLine()
        {
            return WAVESChrome.Line;
        }

        static Color DetailPanelColor()
        {
            return WAVESChrome.Plate;
        }

        GUIStyle DetailContentStyle()
        {
            if (_detailContentStyle != null)
                return _detailContentStyle;

            _detailContentStyle = new GUIStyle
            {
                margin = new RectOffset(0, 0, 0, 0),
                padding = new RectOffset(0, 0, 0, 0),
                border = new RectOffset(0, 0, 0, 0),
                stretchWidth = true,
                stretchHeight = true
            };
            return _detailContentStyle;
        }

        GUIStyle BleedStyle()
        {
            if (_bleedStyle != null)
                return _bleedStyle;

            _bleedStyle = new GUIStyle
            {
                margin = new RectOffset(0, 0, 0, 0),
                padding = new RectOffset(0, 0, 0, 0),
                border = new RectOffset(0, 0, 0, 0),
                stretchWidth = true,
                stretchHeight = true
            };
            return _bleedStyle;
        }

        Rect FullWidthRow(float height)
        {
            return GUILayoutUtility.GetRect(
                1f,
                height,
                BarRowStyle(),
                GUILayout.ExpandWidth(true),
                GUILayout.Height(height));
        }

        GUIStyle BarRowStyle()
        {
            if (_barRowStyle != null)
                return _barRowStyle;

            _barRowStyle = new GUIStyle
            {
                margin = new RectOffset(0, 0, 0, 0),
                padding = new RectOffset(0, 0, 0, 0),
                border = new RectOffset(0, 0, 0, 0),
                stretchWidth = true,
                stretchHeight = false
            };
            return _barRowStyle;
        }

        GUIStyle HintStyle()
        {
            bool pro = EditorGUIUtility.isProSkin;
            if (_hintStyle != null && _hintPro == pro)
                return _hintStyle;

            _hintPro = pro;
            _hintStyle = new GUIStyle(EditorStyles.helpBox)
            {
                richText = true,
                wordWrap = true
            };
            return _hintStyle;
        }

        void DrawMatrix(Rect area)
        {
            EnsureTextStyles();
            float rowHeaderW = RowHeaderWidth();
            float columnHeaderH = ColumnHeaderHeight();
            float bodyW = Mathf.Max(1f, area.width - rowHeaderW);
            float bodyH = Mathf.Max(1f, area.height - columnHeaderH);
            var body = new Rect(area.x + rowHeaderW, area.y + columnHeaderH, bodyW, bodyH);
            var content = new Rect(0f, 0f, _columns.Count * Cell, _rows.Count * Cell);
            if (Event.current.type == EventType.Repaint)
                EditorGUI.DrawRect(area, WAVESChrome.Canvas);
            bool inBody = body.Contains(Event.current.mousePosition);
            if (!inBody)
            {
                _hoverRow = -1;
                _hoverColumn = -1;
            }
            else
            {
                TrackHover(body);
            }

            _matrixScroll = GUI.BeginScrollView(body, _matrixScroll, content);
            if (Event.current.type == EventType.Repaint)
                DrawCells();
            GUI.EndScrollView();
            if (inBody)
                HandleMatrixClick(body, Event.current);

            _columnHeaders = new Rect(area.x + rowHeaderW, area.y, bodyW, columnHeaderH);
            _rowHeaders = new Rect(area.x, area.y + columnHeaderH, rowHeaderW, bodyH);
            DrawColumnHeaders(_columnHeaders);
            DrawRowHeaders(_rowHeaders);
            DrawCorner(new Rect(area.x, area.y, rowHeaderW, columnHeaderH));
            HandleAxisGesture();
            EditorGUIUtility.AddCursorRect(body, MouseCursor.Link);
        }

        void DrawCells()
        {
            EditorGUI.DrawRect(
                new Rect(_matrixScroll.x, _matrixScroll.y, 10000f, 10000f),
                WAVESChrome.Canvas);
            int rows = _rows.Count;
            int columns = _columns.Count;
            for (int row = 0; row < rows; row++)
            {
                for (int column = 0; column < columns; column++)
                {
                    var rect = new Rect(column * Cell, row * Cell, Cell, Cell);
                    if (rect.xMax < _matrixScroll.x || rect.yMax < _matrixScroll.y)
                        continue;
                    if (rect.x > _matrixScroll.x + 4000f || rect.y > _matrixScroll.y + 4000f)
                        continue;
                    if (_rows[row].Plus || _columns[column].Plus)
                        continue;

                    ResolvePair(row, column, out SurfaceTypeDefinition surface, out ActorProfile actor);
                    var pair = new Pair(Identity(surface), Identity(actor));
                    bool exists = _groups.TryGetValue(pair, out bool enabled);
                    bool hovered = _hoverRow == row && _hoverColumn == column;
                    bool traceLeft = _hoverRow == row && column < _hoverColumn;
                    bool traceUp = _hoverColumn == column && row < _hoverRow;
                    bool selected = _hasSelection && _selectedSurface == surface && _selectedActor == actor;
                    Color fill = exists
                        ? Color.Lerp(WAVESChrome.PlateRaised, WAVESChrome.Gold, 0.16f)
                        : Color.Lerp(WAVESChrome.PlateRaised, Color.black, EditorGUIUtility.isProSkin ? 0.16f : 0.05f);
                    if (hovered && exists)
                        fill = Color.Lerp(fill, Brighter(WAVESChrome.Gold), 0.62f);
                    else if (hovered)
                        fill = Color.Lerp(fill, HoverWash(), 0.55f);
                    else if (traceLeft || traceUp)
                        fill = Color.Lerp(fill, HoverWash(), 0.35f);

                    bool fallbackEmpty = !exists && (IsFallback(_rows[row]) || IsFallback(_columns[column]));
                    if (fallbackEmpty)
                    {
                        if (hovered)
                            fill.a = 0.72f;
                        else if (traceLeft || traceUp)
                            fill.a = 0.58f;
                        else
                            fill.a = FallbackEmptyAlpha;
                    }

                    EditorGUI.DrawRect(rect, fill);
                    if (fallbackEmpty)
                        DrawFallbackStripes(rect);
                    Color line = WAVESChrome.Line;
                    EditorGUI.DrawRect(new Rect(rect.xMax - 1f, rect.y, 1f, rect.height), line);
                    EditorGUI.DrawRect(new Rect(rect.x, rect.yMax - 1f, rect.width, 1f), line);
                    if (selected)
                        DrawOutline(rect, WAVESChrome.Gold, 1f);
                    if (exists && enabled)
                        DrawMark(rect, "✓", WAVESChrome.Gold);
                    else if (exists)
                        DrawMark(rect, "×", WAVESChrome.Gold);
                    else if (hovered)
                        DrawMark(rect, "+", WAVESChrome.Ink);
                }
            }
        }

        static void DrawFallbackStripes(Rect rect)
        {
            Texture2D tile = StripesTile();
            if (tile == null)
                return;

            // Texture V grows up and GUI Y grows down. A negative span keeps the tile upright
            // and continuous from one cell into the next.
            float scale = EditorGUIUtility.pixelsPerPoint;
            float width = tile.width;
            float height = tile.height;
            var coords = new Rect(
                rect.x * scale / width,
                -(rect.y + rect.height) * scale / height,
                rect.width * scale / width,
                rect.height * scale / height);
            Color previous = GUI.color;
            GUI.color = new Color(1f, 1f, 1f, StripesAlpha);
            GUI.DrawTextureWithTexCoords(rect, tile, coords, true);
            GUI.color = previous;
        }

        static Texture2D StripesTile()
        {
            if (_stripes != null)
                return _stripes;

            _stripes = AssetDatabase.LoadAssetAtPath<Texture2D>(StripesPath);
            if (_stripes != null)
                _stripes.wrapMode = TextureWrapMode.Repeat;
            return _stripes;
        }

        static void DrawMark(Rect rect, string mark, Color color)
        {
            if (_checkLabel == null)
            {
                _checkLabel = new GUIStyle(EditorStyles.boldLabel)
                {
                    alignment = TextAnchor.MiddleCenter,
                    fontSize = 16,
                    clipping = TextClipping.Overflow
                };
            }

            _checkLabel.normal.textColor = color;
            GUI.Label(rect, mark, _checkLabel);
        }

        static Color Brighter(Color color)
        {
            return Color.Lerp(color, Color.white, 0.42f);
        }

        void TrackHover(Rect body)
        {
            if (!CellAt(body, Event.current.mousePosition, out int row, out int column))
            {
                _hoverRow = -1;
                _hoverColumn = -1;
                return;
            }

            if (_rows[row].Plus || _columns[column].Plus)
            {
                _hoverRow = -1;
                _hoverColumn = -1;
                return;
            }

            _hoverRow = row;
            _hoverColumn = column;
        }

        void HandleMatrixClick(Rect body, Event evt)
        {
            if (evt.type != EventType.MouseDown)
                return;
            if (!CellAt(body, evt.mousePosition, out int row, out int column))
                return;
            if (_rows[row].Plus || _columns[column].Plus)
                return;

            ResolvePair(row, column, out SurfaceTypeDefinition surface, out ActorProfile actor);
            if (evt.button == 1)
            {
                if (_groups.ContainsKey(new Pair(Identity(surface), Identity(actor))))
                    ShowRemoveMenu(surface, actor);
                evt.Use();
                return;
            }

            if (evt.button != 0)
                return;

            _selectedSurface = surface;
            _selectedActor = actor;
            _hasSelection = true;
            evt.Use();
            Repaint();
            GUIUtility.ExitGUI();
        }

        bool CellAt(Rect body, Vector2 mouse, out int row, out int column)
        {
            float x = mouse.x - body.x + _matrixScroll.x;
            float y = mouse.y - body.y + _matrixScroll.y;
            column = (int)(x / Cell);
            row = (int)(y / Cell);
            return x >= 0f && y >= 0f && row >= 0 && column >= 0 && row < _rows.Count && column < _columns.Count;
        }

        static Color HoverWash()
        {
            return EditorGUIUtility.isProSkin
                ? new Color(0.72f, 0.72f, 0.72f, 1f)
                : new Color(1f, 1f, 1f, 1f);
        }

        static Color TraceColor(Color color, float alpha)
        {
            Color lifted = WAVESPalette.Opaque(color);
            lifted.a = alpha;
            return lifted;
        }

        void ShowRemoveMenu(SurfaceTypeDefinition surface, ActorProfile actor)
        {
            var menu = new GenericMenu();
            menu.AddItem(new GUIContent("Remove Group"), false, () => RemoveGroup(surface, actor));
            menu.ShowAsContext();
        }

        void RemoveGroup(SurfaceTypeDefinition surface, ActorProfile actor)
        {
            string actorName = actor != null ? actor.name : "Any Actor";
            string surfaceName = surface != null ? surface.name : "Any Surface";
            bool confirmed = EditorUtility.DisplayDialog(
                "Remove Effects Group",
                "Are you sure you want to delete the " + actorName + " effects for " + surfaceName + "?",
                "Yes, Delete",
                "Don't Delete");
            if (!confirmed)
                return;

            WAVESModuleBase module = CurrentModule();
            if (module == null)
                return;

            EnsureModuleObject(module);
            _moduleObject.Update();
            SerializedProperty cells = _moduleObject.FindProperty("_cells");
            int index = cells != null ? FindCellIndex(cells, surface, actor) : -1;
            if (index >= 0)
                cells.DeleteArrayElementAtIndex(index);
            _moduleObject.ApplyModifiedProperties();
            Repaint();
        }

        void DrawColumnHeaders(Rect viewport)
        {
            for (int i = 0; i < _columns.Count; i++)
            {
                float x = viewport.x + i * Cell - _matrixScroll.x;
                var column = new Rect(x, viewport.y, Cell, viewport.height);
                if (_columns[i].Plus)
                {
                    var plus = new Rect(column.x, column.y, Cell, column.height);
                    DrawPlusButton(plus, _columns[i].Actor);
                    continue;
                }

                if (x + Cell <= viewport.x || x >= viewport.xMax)
                    continue;

                float left = Mathf.Max(x, viewport.x);
                float right = Mathf.Min(x + Cell, viewport.xMax);
                var visible = new Rect(left, viewport.y, Mathf.Max(0f, right - left), viewport.height);
                bool inside = x >= viewport.x - 0.5f && x + Cell <= viewport.xMax + 0.5f;

                if (Event.current.type == EventType.Repaint)
                {
                    EditorGUI.DrawRect(visible, WAVESChrome.PlateRaised);
                    if (i == _hoverColumn)
                        EditorGUI.DrawRect(visible, new Color(HoverWash().r, HoverWash().g, HoverWash().b, 0.18f));

                    if (inside)
                        DrawAxisTitle(column, _columns[i], false);

                    if (IsFallback(_columns[i]))
                        EditorGUI.DrawRect(new Rect(visible.x, visible.y, visible.width, 1f), WAVESChrome.Line);
                    else
                    {
                        Color border = _columns[i].Color;
                        border.a = 1f;
                        EditorGUI.DrawRect(new Rect(visible.x, visible.y, visible.width, 2f), border);
                    }
                    if (!PlusCoversEdge(i, _columns))
                        EditorGUI.DrawRect(new Rect(column.xMax - 1f, visible.y, 1f, visible.height), WAVESChrome.Line);
                    EditorGUI.DrawRect(new Rect(visible.x, visible.yMax - 1f, visible.width, 1f), WAVESChrome.Line);
                }

                if (inside)
                    GUI.Label(column, new GUIContent(string.Empty, _columns[i].Label));
                if (CanReorder(_columns[i]))
                    EditorGUIUtility.AddCursorRect(column, MouseCursor.MoveArrow);
            }

            if (_axisDragging && !_axisPressRow && Event.current.type == EventType.Repaint)
            {
                float x = viewport.x + _axisDrop * Cell - _matrixScroll.x;
                EditorGUI.DrawRect(new Rect(x - 1f, viewport.y, 2f, viewport.height), WAVESPalettePreferences.BrandLeft);
            }
        }

        void DrawRowHeaders(Rect viewport)
        {
            GUI.BeginGroup(viewport);
            for (int i = 0; i < _rows.Count; i++)
            {
                float localY = i * Cell - _matrixScroll.y;
                var row = new Rect(0f, localY, viewport.width, Cell);
                if (_rows[i].Plus)
                {
                    var plus = new Rect(0f, row.y, row.width, Cell);
                    DrawPlusButton(plus, _rows[i].Actor);
                    continue;
                }

                if (localY + Cell < 0f || localY > viewport.height)
                    continue;

                if (Event.current.type == EventType.Repaint)
                {
                    EditorGUI.DrawRect(row, WAVESChrome.PlateRaised);
                    if (i == _hoverRow)
                        EditorGUI.DrawRect(row, new Color(HoverWash().r, HoverWash().g, HoverWash().b, 0.18f));

                    DrawAxisTitle(row, _rows[i], true);
                    if (IsFallback(_rows[i]))
                        EditorGUI.DrawRect(new Rect(row.x, row.y, 1f, row.height), WAVESChrome.Line);
                    else
                    {
                        Color border = _rows[i].Color;
                        border.a = 1f;
                        EditorGUI.DrawRect(new Rect(row.x, row.y, 2f, row.height), border);
                    }
                    if (!PlusCoversEdge(i, _rows))
                        EditorGUI.DrawRect(new Rect(row.x, row.yMax - 1f, row.width, 1f), WAVESChrome.Line);
                    EditorGUI.DrawRect(new Rect(row.xMax - 1f, row.y, 1f, row.height), WAVESChrome.Line);
                }

                GUI.Label(row, new GUIContent(string.Empty, _rows[i].Label));
                if (CanReorder(_rows[i]))
                    EditorGUIUtility.AddCursorRect(row, MouseCursor.MoveArrow);
            }

            if (_axisDragging && _axisPressRow && Event.current.type == EventType.Repaint)
            {
                float y = _axisDrop * Cell - _matrixScroll.y;
                EditorGUI.DrawRect(new Rect(0f, y - 1f, viewport.width, 2f), WAVESPalettePreferences.BrandLeft);
            }

            GUI.EndGroup();
        }

        void DrawCorner(Rect corner)
        {
            var button = corner;
            bool hover = button.Contains(Event.current.mousePosition);
            if (Event.current.type == EventType.Repaint)
            {
                if (hover)
                    EditorGUI.DrawRect(button, new Color(HoverWash().r, HoverWash().g, HoverWash().b, 0.12f));
                DrawOutline(button, hover ? WAVESChrome.Ink : WAVESChrome.Line, 1f);
            }

            if (GUI.Button(button, new GUIContent(string.Empty, "Swap rows and columns"), GUIStyle.none))
            {
                _actorsAreRows = !_actorsAreRows;
                _matrixScroll = Vector2.zero;
            }

            if (Event.current.type != EventType.Repaint)
                return;

            Texture icon = SwapIcon(hover);
            if (icon == null)
                return;

            var iconRect = new Rect(
                button.center.x - AxisIcon * 0.5f,
                button.center.y - AxisIcon * 0.5f,
                AxisIcon,
                AxisIcon);
            GUI.DrawTexture(iconRect, icon, ScaleMode.ScaleToFit);
        }

        static bool PlusCoversEdge(int index, List<AxisEntry> axis)
        {
            return index + 1 < axis.Count && axis[index + 1].Plus;
        }

        void DrawAxisTitle(Rect rect, AxisEntry entry, bool row)
        {
            if (string.IsNullOrEmpty(entry.Caption))
            {
                if (row)
                {
                    GUI.Label(
                        new Rect(rect.x + ColumnLabelPad, rect.y, Mathf.Max(0f, rect.width - ColumnLabelPad * 2f), rect.height),
                        entry.Label,
                        _rowLabel);
                    return;
                }

                DrawColumnLabel(rect, entry.Label, _columnLabel);
                return;
            }

            if (row)
            {
                float titleH = TextHeight(_rowLabel, entry.Label);
                float captionH = TextHeight(_rowCaption, entry.Caption);
                float y = rect.y + (rect.height - titleH - CaptionGap - captionH) * 0.5f;
                float x = rect.x + ColumnLabelPad;
                float width = Mathf.Max(0f, rect.width - ColumnLabelPad * 2f);
                GUI.Label(new Rect(x, y, width, titleH), entry.Label, _rowLabel);
                float captionW = _rowCaption.CalcSize(new GUIContent(entry.Caption)).x;
                float captionX = rect.xMax - ColumnLabelPad - _rowLabel.padding.right - captionW;
                GUI.Label(new Rect(captionX, y + titleH + CaptionGap, captionW, captionH), entry.Caption, _rowCaption);
                return;
            }

            DrawRotatedStack(rect, entry.Label, entry.Caption);
        }

        static float TextHeight(GUIStyle style, string text)
        {
            return Mathf.Max(1f, style.CalcSize(new GUIContent(text)).y);
        }

        void DrawRotatedStack(Rect column, string title, string caption)
        {
            float titleLength = _columnLabel.CalcSize(new GUIContent(title)).x;
            float captionLength = _columnCaption.CalcSize(new GUIContent(caption)).x;
            float titleHeight = TextHeight(_columnLabel, title);
            float captionHeight = TextHeight(_columnCaption, caption);
            float length = Mathf.Max(titleLength, captionLength);
            float stack = titleHeight + CaptionGap + captionHeight;
            float limit = Mathf.Max(1f, column.height - ColumnLabelPad * 2f);
            length = Mathf.Min(length, limit);
            var inner = new Rect(
                column.x,
                column.y + ColumnLabelPad + (limit - length),
                column.width,
                length);
            Matrix4x4 matrix = GUI.matrix;
            GUIUtility.RotateAroundPivot(-90f, inner.center);
            var swapped = new Rect(
                inner.center.x - inner.height * 0.5f,
                inner.center.y - inner.width * 0.5f,
                inner.height,
                inner.width);
            float y = swapped.y + (swapped.height - stack) * 0.5f;
            GUI.Label(new Rect(swapped.x, y, titleLength, titleHeight), title, _columnLabel);
            GUI.Label(
                new Rect(swapped.x, y + titleHeight + CaptionGap, captionLength, captionHeight),
                caption,
                _columnCaption);
            GUI.matrix = matrix;
        }

        static void DrawColumnLabel(Rect column, string text, GUIStyle style)
        {
            DrawRotatedLabel(column, text, style, column.yMax - ColumnLabelPad);
        }

        static void DrawRotatedLabel(Rect column, string text, GUIStyle style, float bottom)
        {
            float length = style.CalcSize(new GUIContent(text)).x;
            float top = column.y + ColumnLabelPad;
            length = Mathf.Min(length, Mathf.Max(1f, bottom - top));
            var inner = new Rect(column.x, bottom - length, column.width, length);
            Matrix4x4 matrix = GUI.matrix;
            GUIUtility.RotateAroundPivot(-90f, inner.center);
            var swapped = new Rect(
                inner.center.x - inner.height * 0.5f,
                inner.center.y - inner.width * 0.5f,
                inner.height,
                inner.width);
            GUI.Label(swapped, text, style);
            GUI.matrix = matrix;
        }

        void ResolvePair(int row, int column, out SurfaceTypeDefinition surface, out ActorProfile actor)
        {
            UnityEngine.Object rowAsset = _rows[row].Asset;
            UnityEngine.Object columnAsset = _columns[column].Asset;
            if (_actorsAreRows)
            {
                actor = rowAsset as ActorProfile;
                surface = columnAsset as SurfaceTypeDefinition;
                return;
            }

            actor = columnAsset as ActorProfile;
            surface = rowAsset as SurfaceTypeDefinition;
        }

        void BuildAxes()
        {
            if (_actorsAreRows)
            {
                FillActors(_rows);
                FillSurfaces(_columns);
                return;
            }

            FillSurfaces(_rows);
            FillActors(_columns);
        }

        static void FillActors(List<AxisEntry> into)
        {
            into.Clear();
            for (int i = 0; i < WAVESCatalog.Actors.Count; i++)
            {
                ActorProfile actor = WAVESCatalog.Actors[i];
                if (actor == null)
                    continue;

                into.Add(new AxisEntry { Asset = actor, Label = actor.name, Color = actor.Color, Actor = true });
            }

            into.Add(new AxisEntry
            {
                Asset = null,
                Label = "Any Actor",
                Caption = "Fallback",
                Color = WAVESPalettePreferences.Actor,
                Actor = true
            });
            into.Add(new AxisEntry { Plus = true, Actor = true, Color = WAVESPalettePreferences.Actor });
        }

        static void FillSurfaces(List<AxisEntry> into)
        {
            into.Clear();
            for (int i = 0; i < WAVESCatalog.Surfaces.Count; i++)
            {
                SurfaceTypeDefinition surface = WAVESCatalog.Surfaces[i];
                if (surface == null)
                    continue;

                into.Add(new AxisEntry { Asset = surface, Label = surface.name, Color = surface.Color });
            }

            into.Add(new AxisEntry
            {
                Asset = null,
                Label = "Any Surface",
                Caption = "Fallback",
                Color = WAVESPalettePreferences.FallbackSurface
            });
            into.Add(new AxisEntry { Plus = true, Color = WAVESPalettePreferences.FallbackSurface });
        }

        static bool IsActorSurfaceList(WAVESModuleBase module, SerializedProperty cells)
        {
            if (cells == null || !cells.isArray)
                return false;

            if (cells.arraySize > 0)
                return HasGroupFields(cells.GetArrayElementAtIndex(0));

            string elementType = cells.arrayElementType;
            if (!string.IsNullOrEmpty(elementType) && elementType.IndexOf("ModuleCell", StringComparison.Ordinal) >= 0)
                return true;

            return WAVESModuleDrawers.Has(module);
        }

        static bool HasGroupFields(SerializedProperty element)
        {
            return element != null &&
                   element.FindPropertyRelative("_surface") != null &&
                   element.FindPropertyRelative("_actor") != null &&
                   element.FindPropertyRelative("_enabled") != null &&
                   element.FindPropertyRelative("_data") != null;
        }

        void ReadGroups(SerializedProperty cells)
        {
            _groups.Clear();
            for (int i = 0; i < cells.arraySize; i++)
            {
                SerializedProperty element = cells.GetArrayElementAtIndex(i);
                SerializedProperty surface = element.FindPropertyRelative("_surface");
                SerializedProperty actor = element.FindPropertyRelative("_actor");
                SerializedProperty enabled = element.FindPropertyRelative("_enabled");
                if (surface == null || actor == null || enabled == null)
                    continue;

                _groups[new Pair(Identity(surface.objectReferenceValue), Identity(actor.objectReferenceValue))] = enabled.boolValue;
            }
        }

        void EnsureCell(SerializedProperty cells, SurfaceTypeDefinition surface, ActorProfile actor)
        {
            if (FindCellIndex(cells, surface, actor) >= 0)
                return;

            int index = cells.arraySize;
            cells.arraySize = index + 1;
            SerializedProperty element = cells.GetArrayElementAtIndex(index);
            SerializedProperty surfaceProperty = element.FindPropertyRelative("_surface");
            SerializedProperty actorProperty = element.FindPropertyRelative("_actor");
            SerializedProperty enabled = element.FindPropertyRelative("_enabled");
            if (surfaceProperty == null || actorProperty == null || enabled == null)
            {
                cells.arraySize = index;
                return;
            }

            surfaceProperty.objectReferenceValue = surface;
            actorProperty.objectReferenceValue = actor;
            enabled.boolValue = true;
            ResetPayload(element.FindPropertyRelative("_data"));
            _groups[new Pair(Identity(surface), Identity(actor))] = true;
        }

        static void ResetPayload(SerializedProperty data)
        {
            if (data == null)
                return;

            SerializedProperty steps = data.FindPropertyRelative("_steps");
            if (steps != null && steps.isArray)
                steps.ClearArray();

            SerializedProperty lands = data.FindPropertyRelative("_lands");
            if (lands != null && lands.isArray)
                lands.ClearArray();

            SerializedProperty jump = data.FindPropertyRelative("_jump");
            if (jump == null)
                return;

            SerializedProperty audio = jump.FindPropertyRelative("_audio");
            if (audio != null)
                audio.objectReferenceValue = null;
            SerializedProperty particles = jump.FindPropertyRelative("_particles");
            if (particles != null)
                particles.objectReferenceValue = null;
            SerializedProperty graph = jump.FindPropertyRelative("_graph");
            if (graph != null)
                graph.objectReferenceValue = null;
            SerializedProperty overrideSettings = jump.FindPropertyRelative("_overrideSettings");
            if (overrideSettings != null)
                overrideSettings.boolValue = false;
        }

        static int FindCellIndex(SerializedProperty cells, UnityEngine.Object surface, UnityEngine.Object actor)
        {
            for (int i = 0; i < cells.arraySize; i++)
            {
                SerializedProperty element = cells.GetArrayElementAtIndex(i);
                SerializedProperty surfaceProperty = element.FindPropertyRelative("_surface");
                SerializedProperty actorProperty = element.FindPropertyRelative("_actor");
                if (surfaceProperty == null || actorProperty == null)
                    continue;
                if (surfaceProperty.objectReferenceValue != surface)
                    continue;
                if (actorProperty.objectReferenceValue != actor)
                    continue;

                return i;
            }

            return -1;
        }

        float RowHeaderWidth()
        {
            float width = RowHeaderMin;
            for (int i = 0; i < _rows.Count; i++)
            {
                if (_rows[i].Plus || string.IsNullOrEmpty(_rows[i].Label))
                    continue;

                width = Mathf.Max(width, _rowLabel.CalcSize(new GUIContent(_rows[i].Label)).x + ColumnLabelPad * 2f);
            }

            return Mathf.Min(width, RowHeaderMax);
        }

        float ColumnHeaderHeight()
        {
            float height = HeaderMin;
            for (int i = 0; i < _columns.Count; i++)
            {
                if (_columns[i].Plus || string.IsNullOrEmpty(_columns[i].Label))
                    continue;

                float span = _columnLabel.CalcSize(new GUIContent(_columns[i].Label)).x + ColumnLabelPad * 2f;
                if (!string.IsNullOrEmpty(_columns[i].Caption))
                    span = Mathf.Max(span, _columnCaption.CalcSize(new GUIContent(_columns[i].Caption)).x + ColumnLabelPad * 2f);
                height = Mathf.Max(height, span);
            }

            return Mathf.Min(height, HeaderMax);
        }

        void EnsureModuleObject(WAVESModuleBase module)
        {
            if (module == null)
            {
                DisposeModuleObject();
                return;
            }

            if (_moduleObject != null && _moduleObject.targetObject == module)
                return;

            DisposeModuleObject();
            _moduleObject = new SerializedObject(module);
        }

        void DisposeModuleObject()
        {
            if (_moduleObject == null)
                return;

            _moduleObject.Dispose();
            _moduleObject = null;
        }

        void CreateSurfaceType()
        {
            string path = EditorUtility.SaveFilePanelInProject(
                "Create Surface Type",
                "SurfaceType",
                "asset",
                "Choose where to save the surface type.");
            if (string.IsNullOrEmpty(path))
                return;

            var surface = CreateInstance<SurfaceTypeDefinition>();
            AssetDatabase.CreateAsset(surface, path);
            AssignOrder(surface, NextSurfaceOrder());
            AssignColor(surface, NextSurfaceColor());
            AssetDatabase.SaveAssets();
            AssetDatabase.ImportAsset(path);
            WAVESCatalog.Refresh();
            Repaint();
        }

        void CreateActorProfile()
        {
            string path = EditorUtility.SaveFilePanelInProject(
                "Create Actor Profile",
                "ActorProfile",
                "asset",
                "Choose where to save the actor profile.");
            if (string.IsNullOrEmpty(path))
                return;

            var actor = CreateInstance<ActorProfile>();
            AssetDatabase.CreateAsset(actor, path);
            AssignOrder(actor, NextActorOrder());
            AssignColor(actor, NextActorColor());
            AssetDatabase.SaveAssets();
            AssetDatabase.ImportAsset(path);
            WAVESCatalog.Refresh();
            Repaint();
        }

        static EntityId Identity(UnityEngine.Object asset)
        {
            return asset != null ? asset.GetEntityId() : EntityId.None;
        }

        void EnsureEmptyStyles()
        {
            bool pro = EditorGUIUtility.isProSkin;
            if (_emptyTitle != null && _emptyPro == pro)
                return;

            _emptyPro = pro;
            Color ink = WAVESChrome.Ink;
            Color muted = WAVESChrome.Muted;
            Color title = WAVESChrome.Gold;
            _emptyTitle = new GUIStyle(EditorStyles.boldLabel)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 13,
                wordWrap = true,
                clipping = TextClipping.Clip
            };
            _emptyTitle.normal.textColor = title;
            _emptyTitle.hover.textColor = title;
            _emptyTitle.active.textColor = title;
            _emptyTitle.focused.textColor = title;

            _emptyAction = new GUIStyle(EditorStyles.boldLabel)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 12,
                wordWrap = true,
                clipping = TextClipping.Clip
            };
            _emptyAction.normal.textColor = ink;
            _emptyAction.hover.textColor = ink;
            _emptyAction.active.textColor = ink;
            _emptyAction.focused.textColor = ink;

            _emptyHint = new GUIStyle(EditorStyles.miniLabel)
            {
                alignment = TextAnchor.UpperCenter,
                fontSize = 10,
                wordWrap = true,
                clipping = TextClipping.Clip
            };
            _emptyHint.normal.textColor = muted;
            _emptyHint.hover.textColor = muted;
            _emptyHint.active.textColor = muted;
            _emptyHint.focused.textColor = muted;
        }

        GUIStyle PlusLabel()
        {
            bool pro = EditorGUIUtility.isProSkin;
            if (_plusLabel != null && _plusPro == pro)
                return _plusLabel;

            _plusPro = pro;
            _plusLabel = new GUIStyle(EditorStyles.boldLabel)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 14,
                clipping = TextClipping.Overflow
            };
            Color ink = WAVESChrome.Ink;
            _plusLabel.normal.textColor = ink;
            _plusLabel.hover.textColor = ink;
            return _plusLabel;
        }

        static string Rich(string value)
        {
            if (string.IsNullOrEmpty(value))
                return string.Empty;

            return value.Replace("<", string.Empty).Replace(">", string.Empty);
        }

        bool DrawPlusButton(Rect rect, bool actor)
        {
            string tip = actor ? "Create Actor Profile" : "Create Surface Type";
            bool hover = rect.Contains(Event.current.mousePosition);
            if (Event.current.type == EventType.Repaint)
            {
                if (hover)
                    EditorGUI.DrawRect(rect, new Color(HoverWash().r, HoverWash().g, HoverWash().b, 0.12f));
                DrawOutline(rect, hover ? WAVESChrome.Ink : WAVESChrome.Line, 1f);
                var mark = new Rect(
                    rect.center.x - AxisIcon * 0.5f,
                    rect.center.y - AxisIcon * 0.5f,
                    AxisIcon,
                    AxisIcon);
                GUI.Label(mark, "+", PlusLabel());
            }

            if (!GUI.Button(rect, new GUIContent(string.Empty, tip), GUIStyle.none))
                return false;

            if (actor)
                CreateActorProfile();
            else
                CreateSurfaceType();
            return true;
        }

        Texture2D CardGradient()
        {
            EnsureChrome();
            return _cardGradient;
        }

        void EnsureChrome()
        {
            bool pro = EditorGUIUtility.isProSkin;
            int revision = WAVESPalettePreferences.Revision;
            if (_moduleStripStyle != null && _proSkin == pro && _paletteRevision == revision && _cardGradient != null)
                return;

            _proSkin = pro;
            _paletteRevision = revision;
            ReplaceTexture(ref _cardGradient, WAVESPalettePreferences.RowLeft, WAVESPalettePreferences.RowRight);
            ReplaceTexture(ref _moduleStrip, WAVESChrome.Strip);
            ReplaceTexture(ref _detailPanel, WAVESChrome.Plate);

            _moduleStripStyle = new GUIStyle
            {
                padding = new RectOffset(8, 8, 8, 8),
                margin = new RectOffset(0, 0, (int)PanelGap, (int)PanelGap)
            };
            _moduleStripStyle.stretchWidth = true;

            _detailPanelStyle = new GUIStyle
            {
                padding = new RectOffset(0, 0, 0, 0),
                margin = new RectOffset(0, 0, 0, 0)
            };
            _detailPanelStyle.normal.background = _detailPanel;
        }

        static void ReplaceTexture(ref Texture2D texture, Color color)
        {
            ReplaceTexture(ref texture, color, color);
        }

        static void ReplaceTexture(ref Texture2D texture, Color left, Color right)
        {
            if (texture != null)
                DestroyImmediate(texture);

            texture = new Texture2D(2, 1, TextureFormat.RGBA32, false)
            {
                hideFlags = HideFlags.HideAndDontSave,
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear
            };
            texture.SetPixels(new[] { left, right });
            texture.Apply();
        }

        static bool DrawSwitch(Rect rect, bool enabled, string tooltip)
        {
            if (Event.current.type == EventType.Repaint)
            {
                WAVESChrome.Fill(rect, enabled ? WAVESChrome.Gold : WAVESChrome.Plate);
                WAVESChrome.Stroke(rect, enabled ? WAVESChrome.Gold : WAVESChrome.Line);
                float knob = rect.height - 4f;
                float x = enabled ? rect.xMax - 2f - knob : rect.x + 2f;
                WAVESChrome.Circle(new Rect(x, rect.y + 2f, knob, knob), WAVESChrome.Knob);
            }

            return GUI.Button(rect, new GUIContent(string.Empty, tooltip), GUIStyle.none);
        }

        static void DrawOutline(Rect rect, Color color, float thickness)
        {
            EditorGUI.DrawRect(new Rect(rect.x, rect.y, rect.width, thickness), color);
            EditorGUI.DrawRect(new Rect(rect.x, rect.yMax - thickness, rect.width, thickness), color);
            EditorGUI.DrawRect(new Rect(rect.x, rect.y, thickness, rect.height), color);
            EditorGUI.DrawRect(new Rect(rect.xMax - thickness, rect.y, thickness, rect.height), color);
        }

        static GUIStyle CaptionStyle(TextAnchor alignment)
        {
            int axisSize = EditorStyles.miniLabel.fontSize;
            if (axisSize < 8)
                axisSize = 10;

            var style = new GUIStyle(EditorStyles.miniLabel)
            {
                alignment = alignment,
                clipping = TextClipping.Clip,
                fontStyle = FontStyle.Normal,
                fontSize = axisSize - 1,
                wordWrap = false,
                padding = new RectOffset(0, 0, 0, 0),
                margin = new RectOffset(0, 0, 0, 0)
            };
            Color muted = WAVESChrome.Muted;
            style.normal.textColor = muted;
            style.hover.textColor = muted;
            style.active.textColor = muted;
            style.focused.textColor = muted;
            style.onNormal.textColor = muted;
            style.onHover.textColor = muted;
            style.onActive.textColor = muted;
            style.onFocused.textColor = muted;
            return style;
        }

        void EnsureTextStyles()
        {
            bool pro = EditorGUIUtility.isProSkin;
            int revision = WAVESPalettePreferences.Revision;
            if (_rowLabel != null && _rowCaption != null && _columnCaption != null && _labelPro == pro && _labelRevision == revision)
                return;

            _labelPro = pro;
            _labelRevision = revision;
            _rowLabel = new GUIStyle(EditorStyles.miniLabel);
            _rowLabel.alignment = TextAnchor.MiddleRight;
            _rowLabel.clipping = TextClipping.Clip;
            _rowLabel.fontStyle = FontStyle.Bold;
            _columnLabel = new GUIStyle(EditorStyles.miniLabel);
            _columnLabel.alignment = TextAnchor.MiddleCenter;
            _columnLabel.clipping = TextClipping.Clip;
            _columnLabel.fontStyle = FontStyle.Bold;
            _columnLabel.padding = new RectOffset(0, 0, 0, 0);
            Color ink = WAVESChrome.Ink;
            _rowLabel.normal.textColor = ink;
            _rowLabel.hover.textColor = ink;
            _columnLabel.normal.textColor = ink;
            _columnLabel.hover.textColor = ink;
            _rowCaption = CaptionStyle(TextAnchor.MiddleRight);
            _columnCaption = CaptionStyle(TextAnchor.MiddleCenter);
            _subtitle = new GUIStyle(EditorStyles.miniLabel);
            _subtitle.richText = true;
            _subtitle.clipping = TextClipping.Clip;
            Color muted = WAVESChrome.Muted;
            _subtitle.normal.textColor = muted;
            _subtitle.hover.textColor = muted;
            _subtitle.active.textColor = muted;
            _subtitle.focused.textColor = muted;
        }

        void EnsureCardStyles()
        {
            bool pro = EditorGUIUtility.isProSkin;
            if (_cardTitle != null && _cardPro == pro)
                return;

            _cardPro = pro;
            _cardTitle = new GUIStyle(EditorStyles.boldLabel)
            {
                alignment = TextAnchor.MiddleCenter,
                clipping = TextClipping.Clip,
                wordWrap = true
            };
            Color ink = Color.white;
            _cardTitle.normal.textColor = ink;
            _cardTitle.hover.textColor = ink;
            _cardTitle.active.textColor = ink;
            _cardTitle.focused.textColor = ink;
        }

        void HandleAxisGesture()
        {
            Event evt = Event.current;
            if (evt.type == EventType.MouseDown && evt.button == 1)
            {
                if (TryHeader(_rowHeaders, true, evt.mousePosition, out int row) && CanReorder(_rows[row]))
                    ShowAxisMenu(_rows[row].Asset);
                else if (TryHeader(_columnHeaders, false, evt.mousePosition, out int column) && CanReorder(_columns[column]))
                    ShowAxisMenu(_columns[column].Asset);
                else
                    return;

                evt.Use();
                return;
            }

            if (evt.type == EventType.MouseDown && evt.button == 0)
            {
                if (TryHeader(_rowHeaders, true, evt.mousePosition, out int row) && CanReorder(_rows[row]))
                    BeginAxisPress(row, true, evt);
                else if (TryHeader(_columnHeaders, false, evt.mousePosition, out int column) && CanReorder(_columns[column]))
                    BeginAxisPress(column, false, evt);
                return;
            }

            if (evt.type == EventType.MouseDrag && _axisPress >= 0)
            {
                if (!_axisDragging && (evt.mousePosition - _axisPressPos).sqrMagnitude < 36f)
                    return;

                _axisDragging = true;
                int count = AssetCount(_axisPressRow ? _rows : _columns);
                float along = _axisPressRow
                    ? evt.mousePosition.y - _rowHeaders.y + _matrixScroll.y
                    : evt.mousePosition.x - _columnHeaders.x + _matrixScroll.x;
                _axisDrop = Mathf.Clamp(Mathf.RoundToInt(along / Cell), 0, count);
                evt.Use();
                Repaint();
                return;
            }

            if (evt.type != EventType.MouseUp || evt.button != 0 || _axisPress < 0)
                return;

            bool commit = _axisDragging;
            bool rows = _axisPressRow;
            int from = _axisPress;
            int to = _axisDrop;
            _axisPress = -1;
            _axisDragging = false;
            _axisDrop = -1;
            if (!commit)
                return;

            CommitAxisOrder(rows, from, to);
            evt.Use();
            Repaint();
        }

        void BeginAxisPress(int index, bool rows, Event evt)
        {
            _axisPress = index;
            _axisPressRow = rows;
            _axisPressPos = evt.mousePosition;
            _axisDragging = false;
            _axisDrop = index;
            evt.Use();
        }

        bool TryHeader(Rect viewport, bool rows, Vector2 mouse, out int index)
        {
            index = -1;
            if (!viewport.Contains(mouse))
                return false;

            List<AxisEntry> axis = rows ? _rows : _columns;
            float scrolled = rows ? _matrixScroll.y : _matrixScroll.x;
            float along = (rows ? mouse.y - viewport.y : mouse.x - viewport.x) + scrolled;
            index = (int)(along / Cell);
            return index >= 0 && index < axis.Count;
        }

        static bool IsFallback(AxisEntry entry)
        {
            return entry.Asset == null && !entry.Plus;
        }

        static bool CanReorder(AxisEntry entry)
        {
            return entry.Asset != null && !entry.Plus;
        }

        static int AssetCount(List<AxisEntry> axis)
        {
            int count = 0;
            for (int i = 0; i < axis.Count; i++)
            {
                if (axis[i].Plus || axis[i].Asset == null)
                    break;

                count++;
            }

            return count;
        }

        void ShowAxisMenu(UnityEngine.Object asset)
        {
            var menu = new GenericMenu();
            menu.AddItem(new GUIContent("Rename"), false, () => AxisRenamePrompt.Open(asset));
            menu.AddItem(new GUIContent("Change color"), false, () => AxisColorPrompt.Open(asset));
            menu.AddItem(new GUIContent("Delete"), false, () => DeleteAxisAsset(asset));
            menu.ShowAsContext();
        }

        void DeleteAxisAsset(UnityEngine.Object asset)
        {
            if (asset == null)
                return;

            if (!EditorUtility.DisplayDialog("Delete " + asset.name, "Delete \"" + asset.name + "\"?", "Delete", "Cancel"))
                return;

            if (_selectedActor == asset || _selectedSurface == asset)
                _hasSelection = false;

            string path = AssetDatabase.GetAssetPath(asset);
            if (!string.IsNullOrEmpty(path))
                AssetDatabase.DeleteAsset(path);

            WAVESCatalog.Refresh();
            Repaint();
        }

        void CommitAxisOrder(bool rows, int from, int to)
        {
            List<AxisEntry> axis = rows ? _rows : _columns;
            int count = AssetCount(axis);
            if (from < 0 || from >= count || to < 0 || to > count)
                return;

            int target = to > from ? to - 1 : to;
            if (target == from)
                return;

            var assets = new List<UnityEngine.Object>(count);
            for (int i = 0; i < count; i++)
                assets.Add(axis[i].Asset);

            UnityEngine.Object moved = assets[from];
            assets.RemoveAt(from);
            assets.Insert(target, moved);
            for (int i = 0; i < assets.Count; i++)
                AssignOrder(assets[i], i);

            WAVESCatalog.Refresh();
            BuildAxes();
        }

        static int NextActorOrder()
        {
            int next = 0;
            for (int i = 0; i < WAVESCatalog.Actors.Count; i++)
            {
                ActorProfile actor = WAVESCatalog.Actors[i];
                if (actor != null && actor.Order >= next)
                    next = actor.Order + 1;
            }

            return next;
        }

        static int NextSurfaceOrder()
        {
            int next = 0;
            for (int i = 0; i < WAVESCatalog.Surfaces.Count; i++)
            {
                SurfaceTypeDefinition surface = WAVESCatalog.Surfaces[i];
                if (surface != null && surface.Order >= next)
                    next = surface.Order + 1;
            }

            return next;
        }

        static void AssignOrder(UnityEngine.Object asset, int order)
        {
            var serialized = new SerializedObject(asset);
            SerializedProperty property = serialized.FindProperty("_order");
            if (property == null)
                return;

            property.intValue = order;
            serialized.ApplyModifiedProperties();
        }

        static Color NextActorColor()
        {
            var used = new List<Color>(WAVESCatalog.Actors.Count);
            for (int i = 0; i < WAVESCatalog.Actors.Count; i++)
            {
                ActorProfile actor = WAVESCatalog.Actors[i];
                if (actor != null)
                    used.Add(actor.Color);
            }

            return WAVESPalettePreferences.NextFreeSwatch(used);
        }

        static Color NextSurfaceColor()
        {
            var used = new List<Color>(WAVESCatalog.Surfaces.Count);
            for (int i = 0; i < WAVESCatalog.Surfaces.Count; i++)
            {
                SurfaceTypeDefinition surface = WAVESCatalog.Surfaces[i];
                if (surface != null)
                    used.Add(surface.Color);
            }

            return WAVESPalettePreferences.NextFreeSwatch(used);
        }

        static void AssignColor(UnityEngine.Object asset, Color color)
        {
            var serialized = new SerializedObject(asset);
            SerializedProperty property = serialized.FindProperty("_color");
            if (property == null)
                return;

            color.a = 1f;
            property.colorValue = color;
            serialized.ApplyModifiedProperties();
        }

        static Texture SwapIcon(bool hover)
        {
            bool light = EditorGUIUtility.isProSkin != hover;
            string name = light ? "d_RotateTool" : "RotateTool";
            GUIContent content = EditorGUIUtility.IconContent(name);
            if (content != null && content.image != null)
                return content.image;

            content = EditorGUIUtility.IconContent(light ? "RotateTool" : "d_RotateTool");
            return content != null ? content.image : null;
        }
    }

    sealed class AxisRenamePrompt : EditorWindow
    {
        UnityEngine.Object _asset;
        string _name = string.Empty;

        public static void Open(UnityEngine.Object asset)
        {
            var window = CreateInstance<AxisRenamePrompt>();
            window._asset = asset;
            window._name = asset != null ? asset.name : string.Empty;
            window.titleContent = new GUIContent("Rename");
            window.minSize = new Vector2(320f, 72f);
            window.maxSize = window.minSize;
            window.ShowUtility();
        }

        void OnGUI()
        {
            if (_asset == null)
            {
                Close();
                return;
            }

            _name = EditorGUILayout.TextField("Name", _name);
            if (!GUILayout.Button("Rename"))
                return;

            string trimmed = _name.Trim();
            if (trimmed.Length == 0)
                return;

            string error = AssetDatabase.RenameAsset(AssetDatabase.GetAssetPath(_asset), trimmed);
            if (!string.IsNullOrEmpty(error))
            {
                EditorUtility.DisplayDialog("Rename", error, "OK");
                return;
            }

            WAVESCatalog.Refresh();
            Close();
        }
    }

    sealed class AxisColorPrompt : EditorWindow
    {
        UnityEngine.Object _asset;
        Color _color = Color.white;

        public static void Open(UnityEngine.Object asset)
        {
            var serialized = new SerializedObject(asset);
            SerializedProperty property = serialized.FindProperty("_color");
            var window = CreateInstance<AxisColorPrompt>();
            window._asset = asset;
            window._color = property != null ? property.colorValue : Color.white;
            window.titleContent = new GUIContent("Change color");
            window.minSize = new Vector2(280f, 64f);
            window.maxSize = window.minSize;
            window.ShowUtility();
        }

        void OnGUI()
        {
            if (_asset == null)
            {
                Close();
                return;
            }

            EditorGUI.BeginChangeCheck();
            _color = EditorGUILayout.ColorField("Color", _color);
            if (!EditorGUI.EndChangeCheck())
                return;

            Color color = _color;
            color.a = 1f;
            _color = color;
            var serialized = new SerializedObject(_asset);
            SerializedProperty property = serialized.FindProperty("_color");
            if (property == null)
                return;

            property.colorValue = color;
            serialized.ApplyModifiedProperties();
            WAVESBrowserWindow[] windows = Resources.FindObjectsOfTypeAll<WAVESBrowserWindow>();
            for (int i = 0; i < windows.Length; i++)
                windows[i].Repaint();
        }
    }
}
