using System;
using System.Collections.Generic;
using MadeYellow.WAVES.Surfaces;
using UnityEditor;
using UnityEngine;

namespace MadeYellow.WAVES.Editor
{
    /// <summary>Edits surface types, their keywords, and the materials and textures that identify them.</summary>
    public sealed class SurfaceTypeManagerWindow : EditorWindow
    {
        const string Title = "WAVES Surface Type Manager";
        const string KeywordControl = "WAVES.SurfaceKeyword";
        const string KeywordHelp =
            "Matched against material and texture names, ignoring case. " +
            "The name only has to contain the keyword. A * stands in for any text, so *color* matches grass_color. " +
            "Auto Search uses these keywords. A dropped asset or folder is added as it is.";

        const float ListWidth = 256f;
        const float Row = 42f;
        const float Pad = 12f;
        const float ChipHeight = 22f;
        const float ChipGap = 4f;
        const float KeywordLimit = 168f;

        static readonly Color Yellow = new Color(0.96f, 0.78f, 0.18f, 1f);

        [SerializeField] SurfaceTypeDefinition _selected;
        [SerializeField] string _keywordDraft = string.Empty;
        [SerializeField] Vector2 _listScroll;
        [SerializeField] Vector2 _keywordScroll;
        [SerializeField] Vector2 _materialScroll;
        [SerializeField] Vector2 _textureScroll;

        readonly List<SurfaceTypeDefinition> _types = new List<SurfaceTypeDefinition>();
        SerializedObject _serialized;
        int _press = -1;
        int _drop;
        bool _dragging;
        bool _reveal;
        float _listViewHeight;
        Vector2 _pressPos;
        bool _stylesPro;
        GUIStyle _rowLabel;
        GUIStyle _rowCaption;
        GUIStyle _section;
        GUIStyle _chipText;
        GUIStyle _chipClose;
        GUIStyle _assetLeft;
        GUIStyle _empty;
        GUIContent _trash;
        GUIContent _info;
        GUIContent _autoMaterials;
        GUIContent _autoTextures;
        GUIContent _clearMaterials;
        GUIContent _clearTextures;

        /// <summary>Opens the manager.</summary>
        [MenuItem("Window/MadeYellow/WAVES/Surface Type Manager")]
        public static void Open()
        {
            var window = GetWindow<SurfaceTypeManagerWindow>();
            window.titleContent = new GUIContent(Title);
            window.minSize = new Vector2(960f, 560f);
            window.Show();
            window.Focus();
        }

        /// <summary>Opens the manager and selects <paramref name="surface"/>.</summary>
        public static void Open(SurfaceTypeDefinition surface)
        {
            Open();
            if (surface != null)
                GetWindow<SurfaceTypeManagerWindow>().Select(surface);
        }

        /// <summary>Selects <paramref name="surface"/> in this window.</summary>
        public void Select(SurfaceTypeDefinition surface)
        {
            if (surface != null && !_types.Contains(surface))
                Reload();

            _selected = surface;
            _keywordDraft = string.Empty;
            _reveal = surface != null;
            BindSerialized();
            Repaint();
        }

        void OnEnable()
        {
            titleContent = new GUIContent(Title);
            minSize = new Vector2(960f, 560f);
            wantsMouseMove = true;
            WAVESCatalog.Retain();
            EditorApplication.projectChanged += Reload;
            Undo.undoRedoPerformed += OnUndo;
            Reload();
        }

        void OnDisable()
        {
            EditorApplication.projectChanged -= Reload;
            Undo.undoRedoPerformed -= OnUndo;
            WAVESCatalog.Release();
            DisposeSerialized();
        }

        void OnInspectorUpdate()
        {
            if (AssetPreview.IsLoadingAssetPreviews())
                Repaint();
        }

        void OnUndo()
        {
            if (_serialized != null)
                _serialized.Update();

            Reload();
        }

        void OnGUI()
        {
            EnsureStyles();
            int indent = EditorGUI.indentLevel;
            EditorGUI.indentLevel = 0;
            var left = new Rect(0f, 0f, ListWidth, position.height);
            var right = new Rect(ListWidth, 0f, Mathf.Max(0f, position.width - ListWidth), position.height);
            if (Event.current.type == EventType.Repaint)
            {
                EditorGUI.DrawRect(left, WAVESChrome.Strip);
                EditorGUI.DrawRect(right, WAVESChrome.Canvas);
                EditorGUI.DrawRect(new Rect(ListWidth, 0f, 1f, position.height), WAVESChrome.Line);
            }

            DrawList(left);
            DrawDetail(right);
            EditorGUI.indentLevel = indent;
            if (Event.current.type == EventType.MouseMove)
                Repaint();
        }

        void DrawList(Rect area)
        {
            const float inset = 8f;
            var add = new Rect(area.x + inset, area.yMax - 28f - inset, area.width - inset * 2f, 28f);
            var view = new Rect(area.x, area.y + 4f, area.width, Mathf.Max(0f, add.y - area.y - 8f));
            _listViewHeight = view.height;
            if (_reveal && _listViewHeight > 1f)
            {
                Reveal();
                _reveal = false;
            }

            bool overflow = _types.Count * Row > view.height;
            float width = overflow ? view.width - 16f : view.width;
            float height = Mathf.Max(view.height, _types.Count * Row);
            _listScroll = GUI.BeginScrollView(view, _listScroll, new Rect(0f, 0f, width, height), GUIStyle.none, GUI.skin.verticalScrollbar);
            HandleListPointer();
            for (int i = 0; i < _types.Count; i++)
                DrawRow(new Rect(0f, i * Row, width, Row), i);

            if (_dragging && Event.current.type == EventType.Repaint)
                EditorGUI.DrawRect(new Rect(4f, _drop * Row - 1f, Mathf.Max(0f, width - 8f), 2f), Yellow);

            GUI.EndScrollView();
            if (WAVESChrome.FlatButton(add, "Add Surface Type"))
            {
                CreateSurface();
                GUIUtility.ExitGUI();
            }
        }

        void DrawRow(Rect rect, int index)
        {
            SurfaceTypeDefinition type = _types[index];
            if (type == null)
                return;

            Event evt = Event.current;
            bool selected = type == _selected;
            bool hover = rect.Contains(evt.mousePosition);
            var grip = new Rect(rect.x + 8f, rect.y, 14f, rect.height);
            if (evt.type == EventType.Repaint)
            {
                if (selected)
                {
                    Color wash = type.Color;
                    wash.a = 0.25f;
                    EditorGUI.DrawRect(rect, wash);
                }
                else if (hover)
                {
                    EditorGUI.DrawRect(rect, HoverWash());
                }

                EditorGUI.DrawRect(new Rect(rect.x, rect.y, 4f, rect.height), type.Color);
                DrawGrip(grip);
            }

            EditorGUIUtility.AddCursorRect(grip, MouseCursor.MoveArrow);

            var text = new Rect(rect.x + 24f, rect.y + 4f, Mathf.Max(0f, rect.width - 28f), 16f);
            GUI.Label(text, type.name ?? string.Empty, _rowLabel != null ? _rowLabel : EditorStyles.label);
            GUI.Label(
                new Rect(text.x, text.yMax, text.width, 16f),
                "Materials: " + type.MaterialCount + " | Textures: " + type.TextureCount,
                _rowCaption != null ? _rowCaption : EditorStyles.miniLabel);

            if (evt.type == EventType.MouseDown && evt.button == 0 && hover)
            {
                _press = index;
                _pressPos = evt.mousePosition;
                _dragging = false;
                _drop = index;
                evt.Use();
            }
        }

        static void DrawGrip(Rect rect)
        {
            const float dot = 2f;
            const float gap = 2f;
            float width = dot * 2f + gap;
            float height = dot * 3f + gap * 2f;
            float x = rect.x + (rect.width - width) * 0.5f;
            float y = rect.center.y - height * 0.5f;
            Color color = WAVESChrome.Muted;
            for (int row = 0; row < 3; row++)
            {
                for (int column = 0; column < 2; column++)
                {
                    EditorGUI.DrawRect(
                        new Rect(x + column * (dot + gap), y + row * (dot + gap), dot, dot),
                        color);
                }
            }
        }

        void HandleListPointer()
        {
            if (_press < 0)
                return;

            Event evt = Event.current;
            if (evt.type == EventType.MouseDrag)
            {
                if (!_dragging && (evt.mousePosition - _pressPos).sqrMagnitude < 16f)
                    return;

                _dragging = true;
                _drop = Mathf.Clamp(Mathf.RoundToInt(evt.mousePosition.y / Row), 0, _types.Count);
                evt.Use();
                Repaint();
                return;
            }

            if (evt.type != EventType.MouseUp || evt.button != 0)
                return;

            int from = _press;
            bool drag = _dragging;
            int drop = _drop;
            _press = -1;
            _dragging = false;
            evt.Use();
            if (!drag)
            {
                if (from >= 0 && from < _types.Count)
                    Select(_types[from]);
                return;
            }

            CommitReorder(from, drop);
        }

        void DrawDetail(Rect area)
        {
            if (_selected == null)
            {
                DrawMessage(area, "Select a surface type");
                return;
            }

            BindSerialized();
            if (_serialized == null)
                return;

            _serialized.Update();
            float y = area.y + Pad;
            float line = EditorGUIUtility.singleLineHeight;
            var swatch = new Rect(area.x + Pad, y, line, line);
            var trash = new Rect(area.xMax - Pad - line, y, line, line);
            var name = new Rect(swatch.xMax + 8f, y, Mathf.Max(0f, trash.x - swatch.xMax - 16f), line);
            DrawColor(swatch);
            DrawName(name);
            if (GUI.Button(trash, TrashContent(), EditorStyles.iconButton))
            {
                DeleteSelected();
                GUIUtility.ExitGUI();
            }

            y += line + 12f;
            GUI.Label(new Rect(area.x + Pad, y, 72f, 18f), "Keywords", _section);
            GUI.Label(new Rect(area.x + Pad + 72f, y, 18f, 18f), InfoContent());
            y += 22f;

            float width = Mathf.Max(1f, area.width - Pad * 2f);
            float flowWidth = width;
            float keywordsH = PlaceKeywords(0f, 0f, flowWidth, false);
            if (keywordsH > KeywordLimit)
            {
                flowWidth = width - 16f;
                keywordsH = PlaceKeywords(0f, 0f, flowWidth, false);
            }

            float shown = Mathf.Min(keywordsH, KeywordLimit);
            var flow = new Rect(area.x + Pad, y, width, shown);
            if (keywordsH > KeywordLimit)
            {
                _keywordScroll = GUI.BeginScrollView(flow, _keywordScroll, new Rect(0f, 0f, flowWidth, keywordsH), GUIStyle.none, GUI.skin.verticalScrollbar);
                PlaceKeywords(0f, 0f, flowWidth, true);
                GUI.EndScrollView();
            }
            else
            {
                PlaceKeywords(flow.x, flow.y, flowWidth, true);
            }

            y += shown + 12f;
            GUI.Label(new Rect(area.x + Pad, y, width, 18f), "Assets", _section);
            y += 20f;
            var content = new Rect(area.x + Pad, y, width, Mathf.Max(0f, area.yMax - y - Pad));
            if (Event.current.type == EventType.Repaint)
                EditorGUI.DrawRect(content, WAVESChrome.Plate);

            const float inner = 10f;
            var box = new Rect(content.x + inner, content.y + inner, Mathf.Max(0f, content.width - inner * 2f), Mathf.Max(0f, content.height - inner * 2f));
            const float gap = 10f;
            float column = Mathf.Max(0f, (box.width - gap) * 0.5f);
            DrawGroup(new Rect(box.x, box.y, column, box.height), "Materials", true);
            DrawGroup(new Rect(box.x + column + gap, box.y, column, box.height), "Textures", false);
        }

        float PlaceKeywords(float x0, float y0, float width, bool draw)
        {
            SerializedProperty keywords = _serialized.FindProperty("_keywords");
            float x = x0;
            float y = y0;
            Color fill = _selected.Color;
            fill.a = 0.8f;
            Color ink = ChipInk(_selected.Color);
            int count = keywords != null ? keywords.arraySize : 0;
            for (int i = 0; i < count; i++)
            {
                string keyword = keywords.GetArrayElementAtIndex(i).stringValue ?? string.Empty;
                float textW = _chipText.CalcSize(new GUIContent(string.IsNullOrEmpty(keyword) ? " " : keyword)).x;
                float chipW = Mathf.Clamp(textW + 28f, 44f, Mathf.Min(220f, width));
                if (x > x0 && x + chipW > x0 + width)
                {
                    x = x0;
                    y += ChipHeight + ChipGap;
                }

                if (draw)
                    DrawChip(new Rect(x, y, chipW, ChipHeight), keyword, i, fill, ink);

                x += chipW + ChipGap;
            }

            const float addW = 52f;
            float fieldW = Mathf.Clamp(width - addW - ChipGap, 48f, 140f);
            float rowW = fieldW + ChipGap + addW;
            if (x > x0 && x + rowW > x0 + width)
            {
                x = x0;
                y += ChipHeight + ChipGap;
            }

            float line = EditorGUIUtility.singleLineHeight;
            float fieldY = y + Mathf.Max(0f, (ChipHeight - line) * 0.5f);
            if (draw)
                DrawKeywordEntry(new Rect(x, fieldY, fieldW, line), new Rect(x + fieldW + ChipGap, fieldY, addW, line));

            return y + ChipHeight - y0;
        }

        void DrawChip(Rect rect, string keyword, int index, Color fill, Color ink)
        {
            WAVESChrome.Fill(rect, fill);
            Tint(_chipText, ink);
            GUI.Label(new Rect(rect.x + 8f, rect.y, rect.width - 26f, rect.height), new GUIContent(keyword, keyword), _chipText);

            const float side = 14f;
            var close = new Rect(rect.xMax - side - 4f, rect.center.y - side * 0.5f, side, side);
            bool hover = close.Contains(Event.current.mousePosition);
            if (hover)
            {
                EditorGUIUtility.AddCursorRect(close, MouseCursor.Link);
                WAVESChrome.Circle(close, Color.black);
            }

            Color mark = Color.black;
            if (hover)
            {
                mark = fill;
                mark.a = 1f;
            }

            Tint(_chipClose, mark);
            GUI.Label(close, "×", _chipClose);
            if (GUI.Button(close, GUIContent.none, GUIStyle.none))
            {
                RemoveKeyword(index);
                GUIUtility.ExitGUI();
            }
        }

        void DrawKeywordEntry(Rect field, Rect add)
        {
            Event evt = Event.current;
            bool submit = evt.type == EventType.KeyDown
                && (evt.keyCode == KeyCode.Return || evt.keyCode == KeyCode.KeypadEnter)
                && GUI.GetNameOfFocusedControl() == KeywordControl;
            if (submit)
                evt.Use();

            GUI.SetNextControlName(KeywordControl);
            _keywordDraft = EditorGUI.TextField(field, _keywordDraft);
            bool clicked = GUI.Button(add, "Add");
            if ((submit || clicked) && CommitKeyword())
                GUIUtility.ExitGUI();
        }

        void DrawGroup(Rect rect, string title, bool materials)
        {
            var header = new Rect(rect.x, rect.y, rect.width, 26f);
            var body = new Rect(rect.x, header.yMax, rect.width, Mathf.Max(0f, rect.height - header.height));
            if (Event.current.type == EventType.Repaint)
            {
                EditorGUI.DrawRect(rect, WAVESChrome.PlateRaised);
                EditorGUI.DrawRect(header, WAVESChrome.Strip);
            }

            HandleDrop(rect, materials);
            float clearW = 52f;
            float autoW = 88f;
            float x = header.xMax - 4f;
            var clear = new Rect(x - clearW, header.y + 3f, clearW, 20f);
            x = clear.x - 4f;
            var auto = new Rect(x - autoW, header.y + 3f, autoW, 20f);
            GUI.Label(new Rect(header.x + 6f, header.y, Mathf.Max(0f, auto.x - header.x - 8f), header.height), title, _section);

            EditorGUI.BeginDisabledGroup(!HasKeywords());
            bool search = GUI.Button(auto, materials ? AutoMaterials() : AutoTextures());
            EditorGUI.EndDisabledGroup();
            SerializedProperty entries = _serialized.FindProperty(materials ? "_materials" : "_textures");
            EditorGUI.BeginDisabledGroup(entries == null || entries.arraySize == 0);
            bool clearClicked = GUI.Button(clear, materials ? ClearMaterials() : ClearTextures());
            EditorGUI.EndDisabledGroup();
            if (search)
            {
                AutoSearch(materials);
                GUIUtility.ExitGUI();
            }

            if (clearClicked)
            {
                ClearGroup(materials);
                GUIUtility.ExitGUI();
            }

            DrawAssets(body, materials);
        }

        void DrawAssets(Rect body, bool materials)
        {
            SerializedProperty prop = _serialized.FindProperty(materials ? "_materials" : "_textures");
            int count = prop != null ? prop.arraySize : 0;
            const float pad = 6f;
            const float listStride = 30f;
            float contentW = body.width;
            float contentH = pad * 2f;
            if (count > 0)
                contentH = pad * 2f + count * listStride;

            if (contentH > body.height)
                contentW = Mathf.Max(1f, body.width - 16f);

            var content = new Rect(0f, 0f, contentW, Mathf.Max(contentH, body.height));
            Vector2 scroll = materials ? _materialScroll : _textureScroll;
            scroll = GUI.BeginScrollView(body, scroll, content, GUIStyle.none, GUI.skin.verticalScrollbar);
            if (materials)
                _materialScroll = scroll;
            else
                _textureScroll = scroll;

            if (count == 0)
                GUI.Label(new Rect(pad, content.height * 0.5f - 10f, Mathf.Max(0f, contentW - pad * 2f), 20f), materials ? "Drop materials" : "Drop textures", _empty);

            for (int i = 0; i < count; i++)
            {
                var item = new Rect(pad, pad + i * listStride, Mathf.Max(0f, contentW - pad * 2f), 28f);
                UnityEngine.Object asset = prop.GetArrayElementAtIndex(i).objectReferenceValue;
                DrawAssetItem(item, asset, materials, i);
            }

            GUI.EndScrollView();
        }

        void DrawAssetItem(Rect rect, UnityEngine.Object asset, bool materials, int index)
        {
            Event evt = Event.current;
            bool hover = rect.Contains(evt.mousePosition);
            if (hover && asset != null)
                EditorGUIUtility.AddCursorRect(rect, MouseCursor.Link);
            if (evt.type == EventType.Repaint && hover)
                EditorGUI.DrawRect(rect, HoverWash());

            if (evt.type == EventType.ContextClick && hover)
            {
                int captured = index;
                bool group = materials;
                var menu = new GenericMenu();
                menu.AddItem(new GUIContent("Remove"), false, () => RemoveAsset(group, captured));
                menu.ShowAsContext();
                evt.Use();
            }

            string label = asset != null ? asset.name : "Missing";
            Texture preview = asset != null ? AssetPreview.GetAssetPreview(asset) : null;
            if (preview == null && asset != null)
                preview = AssetPreview.GetMiniThumbnail(asset);

            var thumb = new Rect(rect.x + 4f, rect.y + 4f, 20f, 20f);
            if (preview != null)
                GUI.DrawTexture(thumb, preview, ScaleMode.ScaleToFit, true);

            const float side = 16f;
            var close = new Rect(rect.xMax - side - 6f, rect.center.y - side * 0.5f, side, side);
            GUI.Label(
                new Rect(thumb.xMax + 6f, rect.y, Mathf.Max(0f, close.x - thumb.xMax - 10f), rect.height),
                new GUIContent(label, label),
                _assetLeft);
            if (RemoveButton(close))
            {
                RemoveAsset(materials, index);
                GUIUtility.ExitGUI();
            }

            if (evt.type == EventType.MouseDown && evt.button == 0 && hover && !close.Contains(evt.mousePosition))
                ShowAsset(asset, evt);
        }

        static void ShowAsset(UnityEngine.Object asset, Event evt)
        {
            if (asset == null)
                return;

            EditorGUIUtility.PingObject(asset);
            if (InspectorOpen())
                Selection.activeObject = asset;

            evt.Use();
        }

        static bool InspectorOpen()
        {
            EditorWindow[] windows = Resources.FindObjectsOfTypeAll<EditorWindow>();
            for (int i = 0; i < windows.Length; i++)
            {
                EditorWindow window = windows[i];
                if (window != null && window.GetType().Name == "InspectorWindow")
                    return true;
            }

            return false;
        }

        bool RemoveButton(Rect close)
        {
            bool hover = close.Contains(Event.current.mousePosition);
            if (hover)
            {
                EditorGUIUtility.AddCursorRect(close, MouseCursor.Link);
                if (Event.current.type == EventType.Repaint)
                    WAVESChrome.Circle(close, Color.black);
            }

            Tint(_chipClose, hover ? Color.white : WAVESChrome.Ink);
            GUI.Label(close, "×", _chipClose);
            return GUI.Button(close, GUIContent.none, GUIStyle.none);
        }

        void HandleDrop(Rect rect, bool materials)
        {
            Event evt = Event.current;
            bool inside = rect.Contains(evt.mousePosition);
            bool dragging = DragPayload();
            if (evt.type == EventType.Repaint && inside && dragging && CanDrop(materials))
                Outline(rect, Yellow);

            if (!inside || (evt.type != EventType.DragUpdated && evt.type != EventType.DragPerform))
                return;

            if (!CanDrop(materials))
            {
                DragAndDrop.visualMode = DragAndDropVisualMode.Rejected;
                evt.Use();
                return;
            }

            DragAndDrop.visualMode = DragAndDropVisualMode.Copy;
            if (evt.type == EventType.DragPerform)
            {
                DragAndDrop.AcceptDrag();
                ImportDropped(materials);
                evt.Use();
                GUIUtility.ExitGUI();
            }

            evt.Use();
        }

        static bool DragPayload()
        {
            if (DragAndDrop.objectReferences != null && DragAndDrop.objectReferences.Length > 0)
                return true;

            return DragAndDrop.paths != null && DragAndDrop.paths.Length > 0;
        }

        static void ReadDrop(List<UnityEngine.Object> assets, List<string> folders)
        {
            var seen = new HashSet<string>();
            UnityEngine.Object[] refs = DragAndDrop.objectReferences;
            if (refs != null)
            {
                for (int i = 0; i < refs.Length; i++)
                {
                    UnityEngine.Object obj = refs[i];
                    if (obj == null)
                        continue;

                    string path = AssetDatabase.GetAssetPath(obj);
                    if (!string.IsNullOrEmpty(path))
                    {
                        if (!seen.Add(path))
                            continue;

                        if (AssetDatabase.IsValidFolder(path))
                        {
                            folders.Add(path);
                            continue;
                        }
                    }

                    if (EditorUtility.IsPersistent(obj))
                        assets.Add(obj);
                }
            }

            string[] paths = DragAndDrop.paths;
            if (paths == null)
                return;

            for (int i = 0; i < paths.Length; i++)
            {
                string path = paths[i];
                if (string.IsNullOrEmpty(path) || !seen.Add(path))
                    continue;

                if (AssetDatabase.IsValidFolder(path))
                {
                    folders.Add(path);
                    continue;
                }

                UnityEngine.Object asset = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(path);
                if (asset != null && EditorUtility.IsPersistent(asset))
                    assets.Add(asset);
            }
        }

        bool CanDrop(bool materials)
        {
            var assets = new List<UnityEngine.Object>();
            var folders = new List<string>();
            ReadDrop(assets, folders);
            if (folders.Count > 0)
                return true;

            for (int i = 0; i < assets.Count; i++)
            {
                UnityEngine.Object obj = assets[i];
                if (materials && obj is Material)
                    return true;

                if (!materials && obj is Texture)
                    return true;
            }

            return false;
        }

        void ImportDropped(bool materials)
        {
            var found = new List<UnityEngine.Object>();
            var assets = new List<UnityEngine.Object>();
            var folders = new List<string>();
            ReadDrop(assets, folders);
            for (int i = 0; i < folders.Count; i++)
                SurfaceAssetSearch.Collect(materials, folders[i], found);

            for (int i = 0; i < assets.Count; i++)
            {
                UnityEngine.Object obj = assets[i];
                if (materials && obj is Material)
                    found.Add(obj);
                else if (!materials && obj is Texture)
                    found.Add(obj);
            }

            if (found.Count == 0)
                return;

            Change(materials ? "Add Materials" : "Add Textures", () =>
            {
                SerializedProperty prop = _serialized.FindProperty(materials ? "_materials" : "_textures");
                for (int i = 0; i < found.Count; i++)
                    Append(prop, found[i]);
            });
        }

        void AutoSearch(bool materials)
        {
            if (!HasKeywords())
                return;

            var found = new List<UnityEngine.Object>();
            SurfaceAssetSearch.Find(materials, null, ReadKeywords(), found);
            int added = CountNew(materials, found);
            if (added == 0)
            {
                string message = found.Count == 0
                    ? (materials ? "No materials matched the keywords." : "No textures matched the keywords.")
                    : (materials ? "Every matching material is already in this group." : "Every matching texture is already in this group.");
                EditorUtility.DisplayDialog("Auto Search", message, "OK");
                return;
            }

            Change(materials ? "Auto Search Materials" : "Auto Search Textures", () =>
            {
                SerializedProperty prop = _serialized.FindProperty(materials ? "_materials" : "_textures");
                for (int i = 0; i < found.Count; i++)
                    Append(prop, found[i]);
            });
        }

        void ClearGroup(bool materials)
        {
            Change(materials ? "Clear Materials" : "Clear Textures", () =>
            {
                SerializedProperty prop = _serialized.FindProperty(materials ? "_materials" : "_textures");
                if (prop != null)
                    prop.ClearArray();
            });
        }

        void RemoveAsset(bool materials, int index)
        {
            Change(materials ? "Remove Material" : "Remove Texture", () =>
            {
                SerializedProperty prop = _serialized.FindProperty(materials ? "_materials" : "_textures");
                DeleteElement(prop, index);
            });
            Repaint();
        }

        void RemoveKeyword(int index)
        {
            Change("Remove Keyword", () =>
            {
                SerializedProperty prop = _serialized.FindProperty("_keywords");
                if (prop == null || index < 0 || index >= prop.arraySize)
                    return;

                prop.DeleteArrayElementAtIndex(index);
            });
        }

        bool CommitKeyword()
        {
            string keyword = string.IsNullOrWhiteSpace(_keywordDraft) ? string.Empty : _keywordDraft.Trim();
            if (keyword.Length == 0 || HasKeyword(keyword))
                return false;

            Change("Add Keyword", () =>
            {
                SerializedProperty prop = _serialized.FindProperty("_keywords");
                int index = prop.arraySize;
                prop.arraySize++;
                prop.GetArrayElementAtIndex(index).stringValue = keyword;
            });
            _keywordDraft = string.Empty;
            return true;
        }

        void DrawColor(Rect rect)
        {
            SerializedProperty color = _serialized.FindProperty("_color");
            EditorGUI.BeginChangeCheck();
            Color picked = EditorGUI.ColorField(rect, GUIContent.none, color.colorValue, false, false, false);
            if (!EditorGUI.EndChangeCheck())
                return;

            picked.a = 1f;
            color.colorValue = picked;
            _serialized.ApplyModifiedProperties();
            WAVESBrowserWindow.RepaintOpen();
        }

        void DrawName(Rect rect)
        {
            EditorGUI.BeginChangeCheck();
            string next = EditorGUI.DelayedTextField(rect, _selected.name);
            if (!EditorGUI.EndChangeCheck())
                return;

            next = next == null ? string.Empty : next.Trim();
            if (next.Length == 0 || next == _selected.name)
                return;

            string path = AssetDatabase.GetAssetPath(_selected);
            string error = AssetDatabase.RenameAsset(path, next);
            if (!string.IsNullOrEmpty(error))
                EditorUtility.DisplayDialog("Rename Surface Type", error, "OK");

            WAVESCatalog.NotifyChanged();
            Reload();
        }

        void CreateSurface()
        {
            string path = EditorUtility.SaveFilePanelInProject(
                "Add Surface Type",
                "SurfaceType",
                "asset",
                "Choose where to save the surface type.");
            if (string.IsNullOrEmpty(path))
                return;

            var surface = CreateInstance<SurfaceTypeDefinition>();
            AssetDatabase.CreateAsset(surface, path);
            int order = 0;
            for (int i = 0; i < _types.Count; i++)
            {
                if (_types[i] != null && _types[i].Order >= order)
                    order = _types[i].Order + 1;
            }

            surface.SetOrder(order);
            var serialized = new SerializedObject(surface);
            Color color = NextColor();
            color.a = 1f;
            serialized.FindProperty("_color").colorValue = color;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            serialized.Dispose();
            AssetDatabase.SaveAssets();
            AssetDatabase.ImportAsset(path);
            WAVESCatalog.NotifyChanged();
            Reload();
            Select(surface);
        }

        void DeleteSelected()
        {
            SurfaceTypeDefinition surface = _selected;
            if (surface == null)
                return;

            if (!EditorUtility.DisplayDialog("Remove Surface Type", "Remove \"" + surface.name + "\"?", "Remove", "Cancel"))
                return;

            int index = _types.IndexOf(surface);
            SurfaceTypeDefinition next = null;
            if (index >= 0 && index + 1 < _types.Count)
                next = _types[index + 1];
            else if (index > 0)
                next = _types[index - 1];

            string path = AssetDatabase.GetAssetPath(surface);
            if (!string.IsNullOrEmpty(path))
                AssetDatabase.DeleteAsset(path);

            WAVESCatalog.NotifyChanged();
            Reload();
            if (next != null && _types.Contains(next))
                Select(next);
        }

        void CommitReorder(int from, int drop)
        {
            if (from < 0 || from >= _types.Count)
                return;

            int target = drop > from ? drop - 1 : drop;
            target = Mathf.Clamp(target, 0, _types.Count - 1);
            SurfaceTypeDefinition moved = _types[from];
            if (target != from)
            {
                _types.RemoveAt(from);
                _types.Insert(target, moved);
                ApplyOrder();
            }

            Select(moved);
        }

        void ApplyOrder()
        {
            var alive = new List<UnityEngine.Object>();
            for (int i = 0; i < _types.Count; i++)
            {
                if (_types[i] != null)
                    alive.Add(_types[i]);
            }

            Undo.RecordObjects(alive.ToArray(), "Reorder Surface Types");
            for (int i = 0; i < _types.Count; i++)
            {
                SurfaceTypeDefinition type = _types[i];
                if (type == null)
                    continue;

                type.SetOrder(i);
                EditorUtility.SetDirty(type);
            }

            WAVESCatalog.NotifyChanged();
            _types.Clear();
            _types.AddRange(WAVESCatalog.Surfaces);
        }

        void Change(string undoName, Action edit)
        {
            if (_selected == null || _serialized == null)
                return;

            Undo.RecordObject(_selected, undoName);
            _serialized.Update();
            edit();
            _serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(_selected);
            SurfaceRegistry.Touch();
            WAVESBrowserWindow.RepaintOpen();
        }

        void Reload()
        {
            WAVESCatalog.Refresh();
            SurfaceTypeDefinition selected = _selected;
            _types.Clear();
            for (int i = 0; i < WAVESCatalog.Surfaces.Count; i++)
            {
                SurfaceTypeDefinition surface = WAVESCatalog.Surfaces[i];
                if (surface != null)
                    _types.Add(surface);
            }

            if (selected != null && !_types.Contains(selected))
                _selected = null;

            BindSerialized();
            Repaint();
        }

        void BindSerialized()
        {
            if (_selected == null)
            {
                DisposeSerialized();
                return;
            }

            if (_serialized != null && _serialized.targetObject == _selected)
                return;

            DisposeSerialized();
            _serialized = new SerializedObject(_selected);
        }

        void DisposeSerialized()
        {
            if (_serialized == null)
                return;

            _serialized.Dispose();
            _serialized = null;
        }

        void Reveal()
        {
            int index = _types.IndexOf(_selected);
            if (index < 0)
                return;

            float y = index * Row;
            if (y < _listScroll.y)
                _listScroll.y = y;
            else if (y + Row > _listScroll.y + _listViewHeight)
                _listScroll.y = y + Row - _listViewHeight;
        }

        bool HasKeywords()
        {
            SerializedProperty prop = _serialized.FindProperty("_keywords");
            if (prop == null)
                return false;

            for (int i = 0; i < prop.arraySize; i++)
            {
                if (!string.IsNullOrEmpty(prop.GetArrayElementAtIndex(i).stringValue))
                    return true;
            }

            return false;
        }

        bool HasKeyword(string keyword)
        {
            SerializedProperty prop = _serialized.FindProperty("_keywords");
            if (prop == null)
                return false;

            for (int i = 0; i < prop.arraySize; i++)
            {
                if (string.Equals(prop.GetArrayElementAtIndex(i).stringValue, keyword, StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
        }

        string[] ReadKeywords()
        {
            SerializedProperty prop = _serialized.FindProperty("_keywords");
            int count = prop != null ? prop.arraySize : 0;
            var keywords = new string[count];
            for (int i = 0; i < count; i++)
                keywords[i] = prop.GetArrayElementAtIndex(i).stringValue;

            return keywords;
        }

        int CountNew(bool materials, List<UnityEngine.Object> found)
        {
            SerializedProperty prop = _serialized.FindProperty(materials ? "_materials" : "_textures");
            int added = 0;
            for (int i = 0; i < found.Count; i++)
            {
                if (!Contains(prop, found[i]))
                    added++;
            }

            return added;
        }

        static bool Contains(SerializedProperty prop, UnityEngine.Object asset)
        {
            if (prop == null || asset == null)
                return false;

            for (int i = 0; i < prop.arraySize; i++)
            {
                if (prop.GetArrayElementAtIndex(i).objectReferenceValue == asset)
                    return true;
            }

            return false;
        }

        static bool Append(SerializedProperty prop, UnityEngine.Object asset)
        {
            if (prop == null || asset == null || Contains(prop, asset))
                return false;

            int index = prop.arraySize;
            prop.arraySize++;
            prop.GetArrayElementAtIndex(index).objectReferenceValue = asset;
            return true;
        }

        static void DeleteElement(SerializedProperty prop, int index)
        {
            if (prop == null || index < 0 || index >= prop.arraySize)
                return;

            bool reference = prop.GetArrayElementAtIndex(index).propertyType == SerializedPropertyType.ObjectReference;
            prop.DeleteArrayElementAtIndex(index);
            if (reference && index < prop.arraySize && prop.GetArrayElementAtIndex(index).objectReferenceValue == null)
                prop.DeleteArrayElementAtIndex(index);
        }

        Color NextColor()
        {
            var used = new List<Color>(_types.Count);
            for (int i = 0; i < _types.Count; i++)
            {
                if (_types[i] != null)
                    used.Add(_types[i].Color);
            }

            return WAVESPalettePreferences.NextFreeSwatch(used);
        }

        void DrawMessage(Rect area, string message)
        {
            GUI.Label(area, message, _empty);
        }

        static void Outline(Rect rect, Color color)
        {
            EditorGUI.DrawRect(new Rect(rect.x, rect.y, rect.width, 2f), color);
            EditorGUI.DrawRect(new Rect(rect.x, rect.yMax - 2f, rect.width, 2f), color);
            EditorGUI.DrawRect(new Rect(rect.x, rect.y, 2f, rect.height), color);
            EditorGUI.DrawRect(new Rect(rect.xMax - 2f, rect.y, 2f, rect.height), color);
        }

        static Color HoverWash()
        {
            return EditorGUIUtility.isProSkin
                ? new Color(1f, 1f, 1f, 0.06f)
                : new Color(0f, 0f, 0f, 0.05f);
        }

        static Color ChipInk(Color color)
        {
            float luma = color.r * 0.299f + color.g * 0.587f + color.b * 0.114f;
            return luma > 0.62f ? new Color(0.12f, 0.12f, 0.12f, 1f) : Color.white;
        }

        static void Tint(GUIStyle style, Color color)
        {
            style.normal.textColor = color;
            style.hover.textColor = color;
            style.active.textColor = color;
            style.focused.textColor = color;
        }

        GUIContent TrashContent()
        {
            if (_trash != null)
                return _trash;

            _trash = IconContent("Remove surface type", "TreeEditor.Trash", "d_TreeEditor.Trash");
            if (_trash.image == null)
                _trash.text = "×";

            return _trash;
        }

        GUIContent InfoContent()
        {
            if (_info != null)
                return _info;

            _info = IconContent(KeywordHelp, "_Help", "d__Help");
            if (_info.image == null)
                _info.text = "i";

            return _info;
        }

        GUIContent AutoMaterials()
        {
            if (_autoMaterials == null)
                _autoMaterials = new GUIContent("Auto Search", "Add materials whose names match the keywords. Existing entries stay.");

            return _autoMaterials;
        }

        GUIContent AutoTextures()
        {
            if (_autoTextures == null)
                _autoTextures = new GUIContent("Auto Search", "Add textures whose names match the keywords. Existing entries stay.");

            return _autoTextures;
        }

        GUIContent ClearMaterials()
        {
            if (_clearMaterials == null)
                _clearMaterials = new GUIContent("Clear", "Remove every material from this surface type. Project assets stay.");

            return _clearMaterials;
        }

        GUIContent ClearTextures()
        {
            if (_clearTextures == null)
                _clearTextures = new GUIContent("Clear", "Remove every texture from this surface type. Project assets stay.");

            return _clearTextures;
        }

        static GUIContent IconContent(string tooltip, params string[] names)
        {
            for (int i = 0; i < names.Length; i++)
            {
                GUIContent content = EditorGUIUtility.IconContent(names[i]);
                if (content != null && content.image != null)
                    return new GUIContent(content.image, tooltip);
            }

            return new GUIContent(string.Empty, tooltip);
        }

        void EnsureStyles()
        {
            bool pro = EditorGUIUtility.isProSkin;
            if (_rowLabel != null && _rowCaption != null && _stylesPro == pro)
                return;

            _stylesPro = pro;
            _rowLabel = Label(WAVESChrome.Ink, TextAnchor.MiddleLeft, 12, false);
            _rowCaption = Label(WAVESChrome.Muted, TextAnchor.MiddleLeft, 10, false);
            _section = Label(WAVESChrome.Ink, TextAnchor.MiddleLeft, 12, false);
            _section.fontStyle = FontStyle.Bold;
            _chipText = Label(Color.white, TextAnchor.MiddleLeft, 11, false);
            _chipClose = Label(Color.white, TextAnchor.MiddleCenter, 10, false);
            _chipClose.padding = new RectOffset(0, 0, 0, 0);
            _chipClose.margin = new RectOffset(0, 0, 0, 0);
            _assetLeft = Label(WAVESChrome.Ink, TextAnchor.MiddleLeft, 11, false);
            _empty = Label(WAVESChrome.Muted, TextAnchor.MiddleCenter, 11, true);
        }

        static GUIStyle Label(Color color, TextAnchor anchor, int size, bool wrap)
        {
            var style = new GUIStyle(EditorStyles.label)
            {
                alignment = anchor,
                fontSize = size,
                wordWrap = wrap,
                clipping = TextClipping.Clip
            };
            Tint(style, color);
            return style;
        }
    }
}
