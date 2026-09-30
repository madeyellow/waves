using System;
using System.Collections.Generic;
using System.Globalization;
using MadeYellow.WAVES.Footsteps;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace MadeYellow.WAVES.Editor
{
    public sealed class FootstepTimelineElement : VisualElement
    {
        const float MinGutterWidth = 160f;
        const float CreateSlop = 4f;
        const float StepGradientLighten = 0.18f;

        static readonly Dictionary<Color, Texture2D> StepFills = new Dictionary<Color, Texture2D>();

        enum DragMode
        {
            None,
            Pan,
            Move,
            ResizeStart,
            ResizeEnd,
            Create,
            Scrub,
            ResizeGutter,
            ReorderTrack
        }

        readonly VisualElement _gutter;
        readonly VisualElement _profileHost;
        readonly VisualElement _ruler;
        readonly VisualElement _rows;
        readonly ScrollView _scroll;
        readonly Label _help;
        readonly VisualElement _playhead;
        readonly VisualElement _rulerGrid;
        readonly List<VisualElement> _grids = new List<VisualElement>();

        FootstepEditSession _session;
        AnimationClip _clip;
        Func<IReadOnlyList<FootstepType>> _types = () => Array.Empty<FootstepType>();
        bool _profileAssigned;
        bool _modelAssigned;

        float _viewStart;
        float _viewDuration = 1f;
        float _clipLength;
        float _lastRulerWidth = -1f;
        float _playheadTime;
        bool _layoutGuard;

        DragMode _drag;
        int _pointerId = -1;
        float _lastPanX;
        float _pressTime;
        float _originStart;
        float _originEnd;
        float _limitLeft;
        float _limitRight;
        float _ghostStart;
        float _ghostEnd;
        float _pressX;
        int _reorderDrop = -1;
        float _nameWidth = MinGutterWidth;
        float _gutterOrigin;
        float _rulerInset = -1f;
        bool _createVisible;
        bool _undoRecorded;
        bool _changed;
        VisualElement _activeLane;
        VisualElement _activeElement;
        VisualElement _ghost;
        FootstepTrackState _activeTrack;
        FootstepMarker _activeMarker;
        FootstepTrackState _selectedTrack;
        FootstepMarker _selectedMarker;
        FootstepClipboard _clipboard;
        FootstepType _preferredType;

        public FootstepTimelineElement()
        {
            AddToClassList("footstep-timeline");
            focusable = true;

            var rulerRow = new VisualElement();
            rulerRow.AddToClassList("footstep-ruler-row");

            _gutter = new VisualElement();
            _gutter.AddToClassList("footstep-gutter");
            _profileHost = new VisualElement();
            _profileHost.AddToClassList("footstep-profile-host");
            _gutter.Add(_profileHost);
            _gutter.Add(CreateGutterSplit());

            _ruler = new VisualElement();
            _ruler.AddToClassList("footstep-ruler");
            _ruler.RegisterCallback<PointerDownEvent>(OnRulerDown);
            _rulerGrid = new VisualElement();
            _rulerGrid.AddToClassList("footstep-frame-grid");
            _rulerGrid.pickingMode = PickingMode.Ignore;
            _rulerGrid.generateVisualContent += DrawFrameGrid;
            _grids.Add(_rulerGrid);
            _ruler.Add(_rulerGrid);
            rulerRow.Add(_gutter);
            rulerRow.Add(_ruler);

            _help = new Label();
            _help.AddToClassList("footstep-help");

            _rows = new VisualElement();
            _scroll = new ScrollView(ScrollViewMode.Vertical);
            _scroll.AddToClassList("footstep-scroll");
            _scroll.horizontalScrollerVisibility = ScrollerVisibility.Hidden;
            _scroll.Add(_rows);
            _scroll.RegisterCallback<PointerDownEvent>(OnEmptyDown, TrickleDown.TrickleDown);
            _scroll.RegisterCallback<GeometryChangedEvent>(OnScrollGeometry);
            _scroll.contentViewport.RegisterCallback<GeometryChangedEvent>(OnScrollGeometry);
            _scroll.verticalScroller.RegisterCallback<GeometryChangedEvent>(OnScrollGeometry);

            Add(rulerRow);
            Add(_help);
            Add(_scroll);

            _playhead = new VisualElement();
            _playhead.AddToClassList("footstep-playhead");
            _playhead.pickingMode = PickingMode.Ignore;
            _playhead.style.display = DisplayStyle.None;
            var playheadHead = new VisualElement();
            playheadHead.AddToClassList("footstep-playhead-head");
            playheadHead.pickingMode = PickingMode.Ignore;
            playheadHead.generateVisualContent += DrawPlayheadHead;
            _playhead.Add(playheadHead);
            Add(_playhead);

            _ruler.RegisterCallback<GeometryChangedEvent>(OnRulerGeometry);
            RegisterCallback<WheelEvent>(OnWheel, TrickleDown.TrickleDown);
            RegisterCallback<PointerDownEvent>(OnMiddleDown, TrickleDown.TrickleDown);
            RegisterCallback<PointerDownEvent>(_ => Focus(), TrickleDown.TrickleDown);
            RegisterCallback<PointerMoveEvent>(OnPointerMove);
            RegisterCallback<PointerUpEvent>(OnPointerUp);
            RegisterCallback<PointerCaptureOutEvent>(OnCaptureOut);
            RegisterCallback<KeyDownEvent>(OnKeyDown);
        }

        public VisualElement ProfileHost => _profileHost;

        public event Action Edited;

        public event Action AddTrackRequested;

        public event Action<FootstepTrackState> RenameTrackRequested;
        public event Action<FootstepTrackState> SetAnimationCurveNameRequested;

        public event Action<FootstepType> PaintTypeChanged;

        public event Action<FootstepTrackState> ChangeTrackColorRequested;

        public event Action<FootstepTrackState> DeleteTrackRequested;

        public event Action<FootstepTrackState, int> MoveTrackRequested;

        public event Action<float> SeekRequested;

        public Action BeforeEdit;

        public bool SnapToGrid { get; set; } = true;

        public void SetPaintType(FootstepType type)
        {
            _preferredType = type;
        }

        public void SetPlayhead(float time)
        {
            _playheadTime = Mathf.Clamp(time, 0f, Mathf.Max(Duration, 0f));
            LayoutPlayhead();
        }

        public void SetContext(
            FootstepEditSession session,
            AnimationClip clip,
            bool profileAssigned,
            bool modelAssigned,
            Func<IReadOnlyList<FootstepType>> types,
            float clipLength)
        {
            bool clipChanged = _clip != clip;
            bool rangeChanged = !Mathf.Approximately(_clipLength, clipLength);
            _session = session;
            _clip = clip;
            _clipLength = Mathf.Max(0f, clipLength);
            _profileAssigned = profileAssigned;
            _modelAssigned = modelAssigned;
            _types = types ?? (() => Array.Empty<FootstepType>());

            if (clipChanged || rangeChanged)
            {
                _viewStart = 0f;
                _viewDuration = Mathf.Max(Duration, 1f);
                _playheadTime = clipChanged
                    ? 0f
                    : Mathf.Clamp(_playheadTime, 0f, Mathf.Max(Duration, 0f));
                ClampView();
            }

            Refresh();
        }

        public void Refresh()
        {
            EndDrag(commit: false);
            Rebuild();
        }

        float Duration => _clipLength > 0f ? _clipLength : (_clip != null ? _clip.length : 0f);

        float FrameRate => _clip != null && _clip.frameRate > 1f ? _clip.frameRate : 60f;

        float MinStep => FootstepEditMath.MinDuration(_clip != null ? _clip.frameRate : 60f);

        float AxisWidth
        {
            get
            {
                float width = _ruler.contentRect.width;
                if (width < 1f)
                    width = _ruler.resolvedStyle.width;
                return width;
            }
        }

        void Rebuild()
        {
            _rows.Clear();
            _ruler.Clear();
            _grids.Clear();

            string help = HelpMessage();
            bool showTracks = help == null;
            _help.text = help ?? string.Empty;
            _help.style.display = showTracks ? DisplayStyle.None : DisplayStyle.Flex;
            _scroll.style.display = showTracks ? DisplayStyle.Flex : DisplayStyle.None;
            if (!showTracks || _session == null)
                return;

            if (_session.tracks == null)
                _session.tracks = new List<FootstepTrackState>();

            for (int i = 0; i < _session.tracks.Count; i++)
            {
                FootstepTrackState track = _session.tracks[i];
                if (track == null)
                    continue;
                if (track.steps == null)
                    track.steps = new List<FootstepMarker>();
                _rows.Add(BuildTrack(track));
            }

            _rows.Add(BuildAddTrackRow());

            Relayout();
            ApplyNameWidth();
            ApplySelectionVisuals();
        }

        string HelpMessage()
        {
            if (!_profileAssigned)
                return "Assign a Footstep Tracks Profile.";
            if (!_modelAssigned)
                return "Assign a model.";
            if (_clip == null)
                return "The model has no animation clips.";
            if (_session == null)
                return "Add tracks to the Footstep Tracks Profile.";
            if (Duration <= 0f)
                return "The animation clip length is zero.";
            return null;
        }

        VisualElement BuildTrack(FootstepTrackState track)
        {
            var row = new VisualElement();
            row.AddToClassList("footstep-track");
            row.userData = track;

            var name = new VisualElement();
            name.AddToClassList("footstep-track-name");
            name.style.borderLeftColor = track.color;
            name.style.backgroundColor = NameBackground(track.color, selected: false);
            name.style.width = _nameWidth;
            name.tooltip = (string.IsNullOrEmpty(track.name) ? "(unnamed)" : track.name) + "\nRight-click to open the track settings.";
            name.AddManipulator(new ContextualMenuManipulator(evt =>
            {
                PopulateTrackSettingsMenu(evt.menu, track);
                evt.StopPropagation();
            }));

            var title = new Label(string.IsNullOrEmpty(track.name) ? "(unnamed)" : track.name);
            title.AddToClassList("footstep-track-title");
            title.pickingMode = PickingMode.Ignore;
            title.style.color = LabelColor(track.color);
            name.Add(title);

            if (FootstepTrackDefinition.UsesCustomCurve(track.name, track.bakeIntoAnimationCurve))
            {
                var bake = new Label("Bakes into " + track.bakeIntoAnimationCurve.Trim());
                bake.AddToClassList("footstep-track-bake");
                bake.pickingMode = PickingMode.Ignore;
                bake.style.color = LabelColor(track.color);
                name.Add(bake);
            }
            name.Add(CreateGutterSplit());
            name.RegisterCallback<PointerDownEvent>(evt =>
            {
                if (evt.button == 1)
                {
                    Select(track, null);
                    return;
                }

                if (evt.button != 0 || _drag != DragMode.None)
                    return;
                if (evt.target is VisualElement target && target.ClassListContains("footstep-gutter-split"))
                    return;

                BeginReorder(evt, row, track);
            });

            var lane = new VisualElement();
            lane.AddToClassList("footstep-lane");
            lane.style.backgroundColor = track.GetBackgroundColor();
            lane.RegisterCallback<PointerDownEvent>(evt => BeginCreate(evt, lane, track));
            lane.AddManipulator(new ContextualMenuManipulator(evt => PopulateTrackMenu(evt, lane, track)));

            var grid = new VisualElement();
            grid.AddToClassList("footstep-frame-grid");
            grid.pickingMode = PickingMode.Ignore;
            grid.generateVisualContent += DrawFrameGrid;
            _grids.Add(grid);
            lane.Add(grid);

            for (int i = 0; i < track.steps.Count; i++)
                lane.Add(BuildStep(lane, track, track.steps[i]));

            row.Add(name);
            row.Add(lane);
            return row;
        }

        VisualElement BuildAddTrackRow()
        {
            var row = new VisualElement();
            row.AddToClassList("footstep-add-track-row");

            var button = new Button(() => AddTrackRequested?.Invoke()) { text = "Add Track" };
            button.AddToClassList("footstep-add-track");
            button.style.width = _nameWidth - 10f;
            row.Add(button);
            return row;
        }

        VisualElement BuildStep(VisualElement lane, FootstepTrackState track, FootstepMarker marker)
        {
            var element = new VisualElement();
            element.AddToClassList("footstep-step");
            ApplyStepFill(element, track.color);
            element.tooltip = "Press and hold the left mouse button to move the element. Press the right mouse button to open the element's settings.";
            element.userData = marker;

            var label = new Label();
            label.AddToClassList("footstep-step-label");
            label.pickingMode = PickingMode.Ignore;
            label.style.color = LabelColor(track.color);
            element.Add(label);
            ApplyTypeAppearance(element, marker);

            var left = new VisualElement();
            left.AddToClassList("footstep-handle");
            left.AddToClassList("footstep-handle-left");
            left.tooltip = "Drag to change the start";
            left.RegisterCallback<PointerDownEvent>(evt =>
                BeginResize(evt, lane, element, track, marker, resizeStart: true));

            var right = new VisualElement();
            right.AddToClassList("footstep-handle");
            right.AddToClassList("footstep-handle-right");
            right.tooltip = "Drag to change the end";
            right.RegisterCallback<PointerDownEvent>(evt =>
                BeginResize(evt, lane, element, track, marker, resizeStart: false));

            element.Add(left);
            element.Add(right);
            element.AddManipulator(new ContextualMenuManipulator(evt =>
            {
                PopulateStepMenu(evt.menu, track, marker);
                evt.StopPropagation();
            }));
            element.RegisterCallback<PointerDownEvent>(evt =>
            {
                if (evt.button == 1)
                {
                    Select(track, marker);
                    evt.StopPropagation();
                    return;
                }

                BeginMove(evt, lane, element, track, marker);
            });

            return element;
        }

        public void RefreshTypePresentation(IReadOnlyList<FootstepType> types)
        {
            _preferredType = MatchLoadedType(_preferredType, types);
            if (_clipboard != null)
                _clipboard.type = MatchLoadedType(_clipboard.type, types);

            if (_session?.tracks != null)
            {
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

                        marker.type = MatchLoadedType(marker.type, types);
                    }
                }
            }

            RefreshStepVisuals(_rows);
        }

        void PopulateStepMenu(DropdownMenu menu, FootstepTrackState track, FootstepMarker marker)
        {
            Select(track, marker);
            IReadOnlyList<FootstepType> types = _types();
            RefreshTypePresentation(types);
            List<FootstepType> ordered = OrderedTypes(types);

            if (ordered.Count == 0)
            {
                menu.AppendAction(
                    "Type/None",
                    _ => { },
                    _ => DropdownMenuAction.Status.Disabled);
            }

            for (int i = 0; i < ordered.Count; i++)
            {
                FootstepType type = ordered[i];
                bool current = marker.type == type;
                menu.AppendAction(
                    "Type/" + type.name,
                    _ => AssignType(marker, type),
                    _ => current
                        ? DropdownMenuAction.Status.Checked | DropdownMenuAction.Status.Disabled
                        : DropdownMenuAction.Status.Normal);
            }

            menu.AppendSeparator();
            menu.AppendAction("Cut", _ => CutMarker(track, marker));
            menu.AppendAction("Copy", _ => CopyMarker(marker));
            menu.AppendAction(
                "Paste Footstep",
                _ => PasteClipboard(),
                _ => _clipboard == null
                    ? DropdownMenuAction.Status.Disabled
                    : DropdownMenuAction.Status.Normal);
            menu.AppendAction("Delete", _ => DeleteStep(track, marker));
        }

        void PopulateTrackSettingsMenu(DropdownMenu menu, FootstepTrackState track)
        {
            int index = _session?.tracks != null ? _session.tracks.IndexOf(track) : -1;
            int count = _session?.tracks != null ? _session.tracks.Count : 0;
            bool canMoveUp = index > 0;
            bool canMoveDown = index >= 0 && index < count - 1;

            menu.AppendAction("Rename Track", _ => RenameTrackRequested?.Invoke(track));
            menu.AppendAction("Set animation curve name", _ => SetAnimationCurveNameRequested?.Invoke(track));
            menu.AppendAction("Change Track Color", _ => ChangeTrackColorRequested?.Invoke(track));
            menu.AppendAction(
                "Move/To top",
                _ => MoveTrackRequested?.Invoke(track, 0),
                _ => canMoveUp ? DropdownMenuAction.Status.Normal : DropdownMenuAction.Status.Disabled);
            menu.AppendAction(
                "Move/Move Up",
                _ => MoveTrackRequested?.Invoke(track, index - 1),
                _ => canMoveUp ? DropdownMenuAction.Status.Normal : DropdownMenuAction.Status.Disabled);
            menu.AppendAction(
                "Move/Move Down",
                _ => MoveTrackRequested?.Invoke(track, index + 1),
                _ => canMoveDown ? DropdownMenuAction.Status.Normal : DropdownMenuAction.Status.Disabled);
            menu.AppendAction(
                "Move/To bottom",
                _ => MoveTrackRequested?.Invoke(track, count - 1),
                _ => canMoveDown ? DropdownMenuAction.Status.Normal : DropdownMenuAction.Status.Disabled);
            menu.AppendSeparator();
            menu.AppendAction("Delete Track", _ => DeleteTrackRequested?.Invoke(track));
        }

        void PopulateTrackMenu(ContextualMenuPopulateEvent evt, VisualElement lane, FootstepTrackState track)
        {
            float time = TimeAt(evt.mousePosition, lane);
            Select(track, null);
            SeekAt(time);
            evt.menu.AppendAction("Create Footstep", _ => CreateFootstep(track, time));
            evt.menu.AppendAction(
                "Paste Footstep",
                _ => PasteAt(track, time),
                _ => _clipboard == null
                    ? DropdownMenuAction.Status.Disabled
                    : DropdownMenuAction.Status.Normal);
            evt.menu.AppendAction(
                "Clear Footsteps",
                _ => ClearFootsteps(track),
                _ => track.steps == null || track.steps.Count == 0
                    ? DropdownMenuAction.Status.Disabled
                    : DropdownMenuAction.Status.Normal);
        }

        void AssignType(FootstepMarker marker, FootstepType type)
        {
            if (_session == null || marker.type == type)
                return;

            RememberEdit();
            marker.type = type;
            if (type != null)
                marker.weight = type.weight;
            _preferredType = type;
            PaintTypeChanged?.Invoke(type);
            _session.hasUnbakedChanges = true;
            EditorUtility.SetDirty(_session);
            Refresh();
            Edited?.Invoke();
        }

        void DeleteStep(FootstepTrackState track, FootstepMarker marker)
        {
            if (_session == null || track.steps == null)
                return;

            RememberEdit();
            track.steps.Remove(marker);
            if (_selectedMarker == marker)
                _selectedMarker = null;
            _session.hasUnbakedChanges = true;
            EditorUtility.SetDirty(_session);
            Refresh();
            Edited?.Invoke();
        }

        void BeginMove(
            PointerDownEvent evt,
            VisualElement lane,
            VisualElement element,
            FootstepTrackState track,
            FootstepMarker marker)
        {
            if (evt.button != 0 || _drag != DragMode.None)
                return;

            Select(track, marker);
            PrepareEditDrag(evt, lane, element, track, marker, DragMode.Move);
            evt.StopPropagation();
        }

        void BeginResize(
            PointerDownEvent evt,
            VisualElement lane,
            VisualElement element,
            FootstepTrackState track,
            FootstepMarker marker,
            bool resizeStart)
        {
            if (evt.button != 0 || _drag != DragMode.None)
                return;

            Select(track, marker);
            PrepareEditDrag(
                evt,
                lane,
                element,
                track,
                marker,
                resizeStart ? DragMode.ResizeStart : DragMode.ResizeEnd);
            evt.StopPropagation();
        }

        void PrepareEditDrag(
            PointerDownEvent evt,
            VisualElement lane,
            VisualElement element,
            FootstepTrackState track,
            FootstepMarker marker,
            DragMode mode)
        {
            FootstepEditMath.GetLimits(track.steps, marker, Duration, out _limitLeft, out _limitRight);
            _drag = mode;
            _activeLane = lane;
            _activeElement = element;
            _activeTrack = track;
            _activeMarker = marker;
            _pressTime = TimeAt(evt.position, lane);
            _originStart = marker.start;
            _originEnd = marker.end;
            _undoRecorded = false;
            _changed = false;
            _pointerId = evt.pointerId;
            this.CapturePointer(evt.pointerId);
        }

        void BeginCreate(PointerDownEvent evt, VisualElement lane, FootstepTrackState track)
        {
            if ((evt.button == 0 || evt.button == 1) && _drag == DragMode.None)
                Select(track, null);

            if (evt.button != 0 || evt.target != lane || _drag != DragMode.None || Duration <= 0f)
                return;

            if (track.steps == null)
                track.steps = new List<FootstepMarker>();

            float time = TimeAt(evt.position, lane);
            FootstepEditMath.GetGap(track.steps, time, Duration, out float gapStart, out float gapEnd);
            if (gapEnd - gapStart < MinStep)
            {
                SeekAt(time);
                evt.StopPropagation();
                return;
            }

            _pressTime = Mathf.Clamp(time, gapStart, gapEnd);
            _pressTime = SnapRange(_pressTime, gapStart, gapEnd);
            _limitLeft = gapStart;
            _limitRight = gapEnd;
            _ghostStart = _pressTime;
            _ghostEnd = _pressTime;
            _pressX = evt.position.x;
            _createVisible = false;
            _activeLane = lane;
            _activeTrack = track;
            _drag = DragMode.Create;
            _pointerId = evt.pointerId;
            _changed = false;
            _undoRecorded = false;
            this.CapturePointer(evt.pointerId);
            evt.StopPropagation();
        }

        void BeginReorder(PointerDownEvent evt, VisualElement row, FootstepTrackState track)
        {
            Select(track, null);
            _drag = DragMode.ReorderTrack;
            _activeElement = row;
            _activeTrack = track;
            _reorderDrop = _session?.tracks != null ? _session.tracks.IndexOf(track) : -1;
            _pointerId = evt.pointerId;
            this.CapturePointer(evt.pointerId);
            evt.StopPropagation();
        }

        int TrackDropIndex(float panelY)
        {
            int index = 0;
            for (int i = 0; i < _rows.childCount; i++)
            {
                VisualElement row = _rows[i];
                if (!row.ClassListContains("footstep-track") || row == _activeElement)
                    continue;
                if (panelY > row.worldBound.center.y)
                    index++;
            }

            return index;
        }

        void PlaceTrackRow(VisualElement row, int trackIndex)
        {
            if (row == null || TrackRowIndex(row) == trackIndex)
                return;

            row.RemoveFromHierarchy();
            _rows.Insert(ChildIndexForTrack(trackIndex), row);
        }

        int TrackRowIndex(VisualElement row)
        {
            int index = 0;
            for (int i = 0; i < _rows.childCount; i++)
            {
                if (!_rows[i].ClassListContains("footstep-track"))
                    continue;
                if (_rows[i] == row)
                    return index;
                index++;
            }

            return -1;
        }

        int ChildIndexForTrack(int trackIndex)
        {
            int seen = 0;
            for (int i = 0; i < _rows.childCount; i++)
            {
                if (!_rows[i].ClassListContains("footstep-track"))
                    continue;
                if (seen == trackIndex)
                    return i;
                seen++;
            }

            for (int i = 0; i < _rows.childCount; i++)
            {
                if (_rows[i].ClassListContains("footstep-add-track-row"))
                    return i;
            }

            return _rows.childCount;
        }

        void OnRulerDown(PointerDownEvent evt)
        {
            if (evt.button != 0 || _drag != DragMode.None || Duration <= 0f)
                return;

            _drag = DragMode.Scrub;
            _pointerId = evt.pointerId;
            this.CapturePointer(evt.pointerId);
            SeekAt(TimeAt(evt.position, _ruler));
            evt.StopPropagation();
        }

        void OnMiddleDown(PointerDownEvent evt)
        {
            if (evt.button != 2 || _drag != DragMode.None || Duration <= 0f)
                return;

            _drag = DragMode.Pan;
            _lastPanX = evt.position.x;
            _pointerId = evt.pointerId;
            this.CapturePointer(evt.pointerId);
            evt.StopPropagation();
        }

        void OnPointerMove(PointerMoveEvent evt)
        {
            if (_drag == DragMode.None || evt.pointerId != _pointerId)
                return;

            if (_drag == DragMode.ResizeGutter)
            {
                _nameWidth = _gutterOrigin + (evt.position.x - _pressX);
                ApplyNameWidth();
                return;
            }

            if (_drag == DragMode.ReorderTrack)
            {
                int drop = TrackDropIndex(evt.position.y);
                if (drop != _reorderDrop)
                {
                    _reorderDrop = drop;
                    PlaceTrackRow(_activeElement, drop);
                }

                return;
            }

            if (_drag == DragMode.Pan)
            {
                float width = AxisWidth;
                if (width > 1f)
                {
                    float dx = evt.position.x - _lastPanX;
                    _lastPanX = evt.position.x;
                    _viewStart -= dx / width * _viewDuration;
                    ClampView();
                    Relayout();
                }

                return;
            }

            if (_drag == DragMode.Scrub)
            {
                SeekAt(TimeAt(evt.position, _ruler));
                return;
            }

            if (_activeLane == null)
                return;

            float time = TimeAt(evt.position, _activeLane);
            switch (_drag)
            {
                case DragMode.Move:
                    ApplyMove(time);
                    break;
                case DragMode.ResizeStart:
                    ApplyResizeStart(time);
                    break;
                case DragMode.ResizeEnd:
                    ApplyResizeEnd(time);
                    break;
                case DragMode.Create:
                    if (Mathf.Abs(evt.position.x - _pressX) < CreateSlop)
                    {
                        HideCreateGhost();
                        return;
                    }

                    if (!_createVisible)
                        ShowCreateGhost();
                    _ghostEnd = SnapRange(time, _limitLeft, _limitRight);
                    _ghostStart = _pressTime;
                    LayoutRange(_ghost, _ghostStart, _ghostEnd);
                    break;
            }
        }

        void ApplyMove(float time)
        {
            float duration = _originEnd - _originStart;
            float rawStart = _originStart + (time - _pressTime);
            float maxStart = Mathf.Max(_limitLeft, _limitRight - duration);
            _activeMarker.start = SnapRange(rawStart, _limitLeft, maxStart);
            _activeMarker.end = _activeMarker.start + duration;
            CommitMarkerChange("Move Footstep");
        }

        void ApplyResizeStart(float time)
        {
            float raw = _originStart + (time - _pressTime);
            float max = _originEnd - MinStep;
            _activeMarker.start = _originStart;
            _activeMarker.end = _originEnd;
            FootstepEditMath.SetStart(
                _activeMarker,
                _limitLeft,
                MinStep,
                SnapRange(raw, _limitLeft, max));
            CommitMarkerChange("Resize Footstep");
        }

        void ApplyResizeEnd(float time)
        {
            float raw = _originEnd + (time - _pressTime);
            float min = _originStart + MinStep;
            _activeMarker.start = _originStart;
            _activeMarker.end = _originEnd;
            FootstepEditMath.SetEnd(
                _activeMarker,
                _limitRight,
                MinStep,
                SnapRange(raw, min, _limitRight));
            CommitMarkerChange("Resize Footstep");
        }

        void CommitMarkerChange(string undoName)
        {
            float start = _activeMarker.start;
            float end = _activeMarker.end;
            bool moved = Mathf.Abs(start - _originStart) > 0.0000001f
                || Mathf.Abs(end - _originEnd) > 0.0000001f;
            if (!moved)
                return;

            if (!_undoRecorded && _session != null)
            {
                _activeMarker.start = _originStart;
                _activeMarker.end = _originEnd;
                RememberEdit();
                _activeMarker.start = start;
                _activeMarker.end = end;
                _undoRecorded = true;
                _session.hasUnbakedChanges = true;
                EditorUtility.SetDirty(_session);
                _changed = true;
            }

            LayoutMarker(_activeElement, _activeMarker.start, _activeMarker.end);
        }

        void OnPointerUp(PointerUpEvent evt)
        {
            if (evt.pointerId != _pointerId || _drag == DragMode.None)
                return;

            EndDrag(commit: true);
        }

        void OnCaptureOut(PointerCaptureOutEvent evt)
        {
            if (evt.pointerId != _pointerId || _drag == DragMode.None)
                return;

            EndDrag(commit: true);
        }

        void EndDrag(bool commit)
        {
            if (_drag == DragMode.None)
                return;

            DragMode mode = _drag;
            bool changed = _changed;
            FootstepTrackState track = _activeTrack;
            FootstepMarker marker = _activeMarker;
            float ghostStart = _ghostStart;
            float ghostEnd = _ghostEnd;
            bool createVisible = _createVisible;
            int pointer = _pointerId;

            int reorderDrop = _reorderDrop;

            _drag = DragMode.None;
            _pointerId = -1;
            _undoRecorded = false;
            _changed = false;
            _createVisible = false;
            _reorderDrop = -1;
            _activeLane = null;
            _activeElement = null;
            _activeTrack = null;
            _activeMarker = null;

            if (_ghost != null)
            {
                _ghost.RemoveFromHierarchy();
                _ghost = null;
            }

            if (pointer >= 0 && this.HasPointerCapture(pointer))
                this.ReleasePointer(pointer);

            if (mode == DragMode.ReorderTrack)
            {
                int origin = _session?.tracks != null ? _session.tracks.IndexOf(track) : -1;
                if (commit && track != null && reorderDrop >= 0 && reorderDrop != origin)
                    MoveTrackRequested?.Invoke(track, reorderDrop);
                return;
            }

            if (!commit || mode == DragMode.Pan || mode == DragMode.Scrub || mode == DragMode.ResizeGutter)
                return;

            if (mode == DragMode.Create && track != null)
            {
                if (!createVisible)
                    SeekAt(_pressTime);
                else
                    CommitCreate(track, ghostStart, ghostEnd);
                return;
            }

            if (changed)
                Edited?.Invoke();
            else if (marker != null)
                Select(track, marker);
        }

        void CommitCreate(FootstepTrackState track, float ghostStart, float ghostEnd)
        {
            float start = Mathf.Min(ghostStart, ghostEnd);
            float end = Mathf.Max(ghostStart, ghostEnd);
            if (end - start < MinStep || _session == null)
                return;

            FootstepType type = DefaultType();
            var marker = new FootstepMarker
            {
                start = start,
                end = end,
                type = type,
                weight = type != null ? type.weight : 1f
            };

            RememberEdit();
            track.steps.Add(marker);
            _selectedTrack = track;
            _selectedMarker = marker;
            _session.hasUnbakedChanges = true;
            EditorUtility.SetDirty(_session);
            Refresh();
            Edited?.Invoke();
        }

        FootstepType DefaultType()
        {
            List<FootstepType> ordered = OrderedTypes(_types());
            if (_preferredType != null)
            {
                for (int i = 0; i < ordered.Count; i++)
                {
                    if (ordered[i] == _preferredType)
                        return _preferredType;
                }
            }

            return ordered.Count > 0 ? ordered[0] : null;
        }

        static FootstepType MatchLoadedType(FootstepType type, IReadOnlyList<FootstepType> types)
        {
            if (type == null || types == null)
                return type;

            for (int i = 0; i < types.Count; i++)
            {
                if (types[i] == type)
                    return type;
            }

            string path = AssetDatabase.GetAssetPath(type);
            if (string.IsNullOrEmpty(path))
                return type;

            for (int i = 0; i < types.Count; i++)
            {
                FootstepType candidate = types[i];
                if (candidate != null && AssetDatabase.GetAssetPath(candidate) == path)
                    return candidate;
            }

            return type;
        }

        void RefreshStepVisuals(VisualElement parent)
        {
            for (int i = 0; i < parent.childCount; i++)
            {
                VisualElement child = parent[i];
                if (child.ClassListContains("footstep-step"))
                {
                    if (child.userData is FootstepMarker marker)
                        ApplyTypeAppearance(child, marker);
                    continue;
                }

                RefreshStepVisuals(child);
            }
        }

        static void ApplyTypeAppearance(VisualElement element, FootstepMarker marker)
        {
            string typeName = marker.type != null ? marker.type.name : "Step";
            Label label = null;
            Image icon = null;
            for (int i = 0; i < element.childCount; i++)
            {
                VisualElement child = element[i];
                if (label == null && child is Label text && child.ClassListContains("footstep-step-label"))
                    label = text;
                else if (icon == null && child is Image image && child.ClassListContains("footstep-step-icon"))
                    icon = image;
            }

            if (label != null)
            {
                if (label.text != typeName)
                    label.text = typeName;
                if (label.tooltip != typeName)
                    label.tooltip = typeName;
            }

            Texture texture = marker.type != null ? marker.type.icon : null;
            if (texture == null)
            {
                icon?.RemoveFromHierarchy();
                return;
            }

            long stamp = texture.GetInstanceID() + (texture.updateCount * 397L);
            if (icon == null)
            {
                icon = new Image
                {
                    image = texture,
                    pickingMode = PickingMode.Ignore,
                    userData = stamp
                };
                icon.AddToClassList("footstep-step-icon");
                element.Insert(0, icon);
                return;
            }

            if (icon.userData is long stored && stored == stamp)
                return;

            icon.userData = stamp;
            if (icon.image == texture)
                icon.image = null;
            icon.image = texture;
        }

        static List<FootstepType> OrderedTypes(IReadOnlyList<FootstepType> types)
        {
            var ordered = new List<FootstepType>();
            if (types == null)
                return ordered;

            for (int i = 0; i < types.Count; i++)
            {
                if (types[i] != null)
                    ordered.Add(types[i]);
            }

            ordered.Sort(FootstepType.CompareByOrder);
            return ordered;
        }

        void OnWheel(WheelEvent evt)
        {
            if (Duration <= 0f)
                return;

            float width = AxisWidth;
            if (width < 1f)
                return;

            Vector2 local = _ruler.WorldToLocal(evt.mousePosition);
            float anchorX = local.x;
            if (anchorX < 0f || anchorX > width)
                anchorX = width * 0.5f;

            float anchorTime = _viewStart + anchorX / width * _viewDuration;
            float factor = evt.delta.y > 0f ? 1.12f : 1f / 1.12f;
            _viewDuration = Mathf.Clamp(_viewDuration * factor, Mathf.Min(Duration, MinStep * 8f), Duration);
            float ratio = anchorX / width;
            _viewStart = anchorTime - ratio * _viewDuration;
            ClampView();
            Relayout();
            evt.StopPropagation();
        }

        void ClampView()
        {
            float length = Duration;
            if (length <= 0f)
            {
                _viewStart = 0f;
                _viewDuration = 1f;
                return;
            }

            float minDuration = Mathf.Min(length, MinStep * 8f);
            _viewDuration = Mathf.Clamp(_viewDuration, minDuration, length);
            _viewStart = Mathf.Clamp(_viewStart, 0f, Mathf.Max(0f, length - _viewDuration));
        }

        void OnRulerGeometry(GeometryChangedEvent evt)
        {
            if (Mathf.Abs(evt.newRect.width - _lastRulerWidth) < 0.5f)
                return;

            _lastRulerWidth = evt.newRect.width;
            Relayout();
        }

        void OnScrollGeometry(GeometryChangedEvent evt)
        {
            SyncRulerInset();
        }

        void SyncRulerInset()
        {
            float inset = ScrollbarInset();
            if (Mathf.Abs(inset - _rulerInset) < 0.5f)
                return;

            _rulerInset = inset;
            _ruler.style.marginRight = inset;
        }

        float ScrollbarInset()
        {
            VisualElement viewport = _scroll.contentViewport;
            Scroller scroller = _scroll.verticalScroller;
            if (viewport == null || scroller == null || scroller.resolvedStyle.display == DisplayStyle.None)
                return 0f;

            float gap = _scroll.worldBound.xMax - viewport.worldBound.xMax;
            if (float.IsNaN(gap) || gap < 0.5f)
                return 0f;

            return gap;
        }

        void Relayout()
        {
            if (_layoutGuard)
                return;

            _layoutGuard = true;
            LayoutSteps(_rows);
            RebuildRuler();
            LayoutPlayhead();
            for (int i = 0; i < _grids.Count; i++)
                _grids[i].MarkDirtyRepaint();
            _layoutGuard = false;
        }

        void LayoutSteps(VisualElement parent)
        {
            if (parent == null)
                return;

            for (int i = 0; i < parent.childCount; i++)
            {
                VisualElement child = parent[i];
                if (child.userData is FootstepMarker marker)
                    LayoutMarker(child, marker.start, marker.end);
                else
                    LayoutSteps(child);
            }
        }

        void RebuildRuler()
        {
            _ruler.Clear();
            _ruler.Add(_rulerGrid);
            float width = AxisWidth;
            if (width < 1f || _viewDuration <= 0f || Duration <= 0f)
                return;

            float fps = FrameRate;
            int tick = ChooseFrameTick(width / (_viewDuration * fps));
            int first = Mathf.CeilToInt(_viewStart * fps / tick) * tick;
            int last = Mathf.FloorToInt((_viewStart + _viewDuration) * fps);
            for (int frame = first; frame <= last; frame += tick)
                AddTick(frame.ToString(CultureInfo.InvariantCulture), frame / fps, width);
        }

        void AddTick(string text, float time, float width)
        {
            var label = new Label(text);
            label.AddToClassList("footstep-tick");
            label.pickingMode = PickingMode.Ignore;
            label.style.left = (time - _viewStart) / _viewDuration * width;
            _ruler.Add(label);
        }

        static int ChooseFrameTick(float pixelsPerFrame)
        {
            int[] ticks = { 1, 2, 5, 10, 15, 30, 60, 120, 300, 600 };
            for (int i = 0; i < ticks.Length; i++)
            {
                if (ticks[i] * pixelsPerFrame >= 56f)
                    return ticks[i];
            }

            return ticks[ticks.Length - 1];
        }

        void DrawFrameGrid(MeshGenerationContext context)
        {
            Rect rect = context.visualElement.contentRect;
            if (rect.width < 1f || rect.height < 1f || _viewDuration <= 0f || Duration <= 0f)
                return;

            float fps = FrameRate;
            float pixelsPerFrame = rect.width / (_viewDuration * fps);
            int stride = 1;
            int[] strides = { 1, 2, 5, 10, 15, 30, 60, 120, 300 };
            for (int i = 0; i < strides.Length; i++)
            {
                stride = strides[i];
                if (stride * pixelsPerFrame >= 6f)
                    break;
            }
            var painter = context.painter2D;
            painter.lineWidth = 1f;
            painter.strokeColor = WAVESPalettePreferences.GridLine;

            int first = Mathf.CeilToInt(_viewStart * fps / stride) * stride;
            int last = Mathf.FloorToInt((_viewStart + _viewDuration) * fps);
            for (int frame = first; frame <= last; frame += stride)
            {
                float x = ((frame / fps) - _viewStart) / _viewDuration * rect.width;
                painter.BeginPath();
                painter.MoveTo(new Vector2(x, 0f));
                painter.LineTo(new Vector2(x, rect.height));
                painter.Stroke();
            }
        }

        static void DrawPlayheadHead(MeshGenerationContext context)
        {
            Rect rect = context.visualElement.contentRect;
            if (rect.width < 1f || rect.height < 1f)
                return;

            Painter2D painter = context.painter2D;
            painter.fillColor = WAVESPalettePreferences.Playhead;
            painter.BeginPath();
            painter.MoveTo(new Vector2(rect.xMin, rect.yMin));
            painter.LineTo(new Vector2(rect.xMax, rect.yMin));
            painter.LineTo(new Vector2(rect.center.x, rect.yMax));
            painter.ClosePath();
            painter.Fill();
        }

        void SeekAt(float time)
        {
            time = SnapToFrame(time);
            SetPlayhead(time);
            SeekRequested?.Invoke(time);
        }

        float SnapToFrame(float time)
        {
            return FootstepEditMath.SnapToFrame(time, FrameRate, 0f, Mathf.Max(Duration, 0f));
        }

        float SnapRange(float time, float min, float max)
        {
            if (!SnapToGrid)
                return Mathf.Clamp(time, min, max);

            return FootstepEditMath.SnapToFrame(time, FrameRate, min, max);
        }

        void LayoutPlayhead()
        {
            _playhead.style.backgroundColor = WAVESPalettePreferences.Playhead;
            float width = AxisWidth;
            bool visible = Duration > 0f && width > 1f;
            float x = visible ? TimeToX(_playheadTime) : -1f;
            visible = visible && x >= 0f && x <= width;
            _playhead.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
            if (!visible)
                return;

            _playhead.style.left = _nameWidth + x - 1f;
        }

        float TimeAt(Vector2 panelPosition, VisualElement lane)
        {
            float width = AxisWidth;
            if (width < 1f)
                return _viewStart;

            float x = lane.WorldToLocal(panelPosition).x;
            return _viewStart + x / width * _viewDuration;
        }

        void LayoutRange(VisualElement element, float start, float end)
        {
            LayoutMarker(element, Mathf.Min(start, end), Mathf.Max(start, end));
        }

        void LayoutMarker(VisualElement element, float start, float end)
        {
            if (element == null)
                return;

            float x = TimeToX(start);
            float width = Mathf.Max(2f, TimeToX(end) - x);
            element.style.left = x;
            element.style.width = width;
        }

        float TimeToX(float time)
        {
            float width = AxisWidth;
            if (width < 1f || _viewDuration <= 0f)
                return 0f;

            return (time - _viewStart) / _viewDuration * width;
        }

        void CreateFootstep(FootstepTrackState track, float time)
        {
            if (track == null)
                return;

            if (track.steps == null)
                track.steps = new List<FootstepMarker>();

            FootstepType type = DefaultType();
            PlaceCopy(track, type, type != null ? type.weight : 1f, time, MinStep, "Create Footstep");
        }

        void ClearFootsteps(FootstepTrackState track)
        {
            if (_session == null || track?.steps == null || track.steps.Count == 0)
                return;

            RememberEdit();
            track.steps.Clear();
            if (_selectedTrack == track)
                _selectedMarker = null;
            _session.hasUnbakedChanges = true;
            EditorUtility.SetDirty(_session);
            Refresh();
            Edited?.Invoke();
        }

        void ShowCreateGhost()
        {
            if (_activeLane == null || _activeTrack == null || _createVisible)
                return;

            _createVisible = true;
            _ghost = new VisualElement();
            _ghost.AddToClassList("footstep-step");
            _ghost.AddToClassList("footstep-step-ghost");
            _ghost.pickingMode = PickingMode.Ignore;
            ApplyStepFill(_ghost, _activeTrack.color);
            _activeLane.Add(_ghost);
            LayoutMarker(_ghost, _pressTime, _pressTime);
        }

        void HideCreateGhost()
        {
            _createVisible = false;
            if (_ghost == null)
                return;

            _ghost.RemoveFromHierarchy();
            _ghost = null;
        }

        VisualElement CreateGutterSplit()
        {
            var split = new VisualElement();
            split.AddToClassList("footstep-gutter-split");
            split.RegisterCallback<PointerDownEvent>(OnGutterDown);
            return split;
        }

        void OnGutterDown(PointerDownEvent evt)
        {
            if (evt.button != 0 || _drag != DragMode.None)
                return;

            _drag = DragMode.ResizeGutter;
            _pressX = evt.position.x;
            _gutterOrigin = _nameWidth;
            _pointerId = evt.pointerId;
            this.CapturePointer(evt.pointerId);
            evt.StopPropagation();
        }

        void OnEmptyDown(PointerDownEvent evt)
        {
            if (evt.button != 0 && evt.button != 1)
                return;

            for (VisualElement ve = evt.target as VisualElement; ve != null && ve != _scroll; ve = ve.parent)
            {
                if (ve.ClassListContains("footstep-track") || ve.ClassListContains("unity-scroller"))
                    return;
            }

            Select(null, null);
        }

        void ApplyNameWidth()
        {
            float available = contentRect.width;
            if (available < 1f)
                available = resolvedStyle.width;
            float max = available - 160f;
            if (max < MinGutterWidth)
                max = MinGutterWidth;
            _nameWidth = Mathf.Clamp(_nameWidth, MinGutterWidth, max);
            _gutter.style.width = _nameWidth;
            for (int i = 0; i < _rows.childCount; i++)
            {
                VisualElement row = _rows[i];
                for (int c = 0; c < row.childCount; c++)
                {
                    if (row[c].ClassListContains("footstep-track-name"))
                        row[c].style.width = _nameWidth;
                    else if (row[c].ClassListContains("footstep-add-track"))
                        row[c].style.width = _nameWidth - 10f;
                }
            }

            LayoutPlayhead();
        }

        void RememberEdit()
        {
            BeforeEdit?.Invoke();
        }

        static Color LabelColor(Color background)
        {
            float luminance = (background.r * 0.2126f) + (background.g * 0.7152f) + (background.b * 0.0722f);
            return luminance > 0.62f ? WAVESPalettePreferences.Ink : Color.white;
        }

        void OnKeyDown(KeyDownEvent evt)
        {
            if (evt.keyCode == KeyCode.Delete)
            {
                DeleteSelection();
                evt.StopPropagation();
                return;
            }

            bool command = evt.ctrlKey || evt.commandKey;
            if (!command || evt.altKey)
                return;

            if (evt.keyCode == KeyCode.Z)
            {
                if (evt.shiftKey)
                    FootstepWindow.Instance?.EditorRedo();
                else
                    FootstepWindow.Instance?.EditorUndo();
                evt.StopPropagation();
                return;
            }

            if (evt.keyCode == KeyCode.Y)
            {
                FootstepWindow.Instance?.EditorRedo();
                evt.StopPropagation();
                return;
            }

            if (evt.keyCode == KeyCode.D)
            {
                DuplicateSelection();
                evt.StopPropagation();
            }
            else if (evt.keyCode == KeyCode.X)
            {
                CutSelection();
                evt.StopPropagation();
            }
            else if (evt.keyCode == KeyCode.C)
            {
                CopySelection();
                evt.StopPropagation();
            }
            else if (evt.keyCode == KeyCode.V)
            {
                PasteClipboard();
                evt.StopPropagation();
            }
        }

        void Select(FootstepTrackState track, FootstepMarker marker)
        {
            _selectedTrack = track;
            _selectedMarker = marker;
            ApplySelectionVisuals();
        }

        void ApplySelectionVisuals()
        {
            PruneSelection();
            for (int i = 0; i < _rows.childCount; i++)
            {
                VisualElement row = _rows[i];
                if (row.userData is not FootstepTrackState track || row.childCount < 2)
                    continue;

                bool selected = track == _selectedTrack;
                VisualElement name = row[0];
                VisualElement lane = row[1];
                name.style.backgroundColor = NameBackground(track.color, selected);
                name.style.borderLeftColor = selected
                    ? Color.Lerp(track.color, Brighter(track.color), 0.5f)
                    : track.color;
                lane.style.backgroundColor = selected
                    ? track.GetSelectedBackgroundColor()
                    : track.GetBackgroundColor();

                Color stepBorder = WAVESPalettePreferences.StepBorder;
                for (int s = 0; s < lane.childCount; s++)
                {
                    VisualElement step = lane[s];
                    if (step.userData is not FootstepMarker)
                        continue;

                    Color border = step.userData == _selectedMarker ? stepBorder : Color.clear;
                    step.style.borderLeftColor = border;
                    step.style.borderRightColor = border;
                    step.style.borderTopColor = border;
                    step.style.borderBottomColor = border;
                }
            }
        }

        static Color NameBackground(Color color, bool selected)
        {
            return new Color(
                color.r,
                color.g,
                color.b,
                selected ? WAVESPalettePreferences.NameSelectedAlpha : WAVESPalettePreferences.NameAlpha);
        }

        static void ApplyStepFill(VisualElement element, Color color)
        {
            element.style.backgroundColor = Color.clear;
            element.style.backgroundImage = Background.FromTexture2D(StepFill(color));
            element.style.unityBackgroundImageTintColor = Color.white;
            element.style.backgroundRepeat = new BackgroundRepeat(Repeat.NoRepeat, Repeat.NoRepeat);
            element.style.backgroundSize = new BackgroundSize(Length.Percent(100f), Length.Percent(100f));
            element.style.backgroundPositionX = new BackgroundPosition(BackgroundPositionKeyword.Left);
            element.style.backgroundPositionY = new BackgroundPosition(BackgroundPositionKeyword.Top);
        }

        static Texture2D StepFill(Color color)
        {
            if (StepFills.TryGetValue(color, out Texture2D cached) && cached != null)
                return cached;

            Color left = Color.Lerp(color, Color.white, StepGradientLighten);
            left.a = color.a;

            const int width = 64;
            var pixels = new Color[width];
            float span = width - 1;
            for (int i = 0; i < width; i++)
            {
                Color sample = Color.Lerp(left, color, i / span);
                sample.a = color.a;
                pixels[i] = sample;
            }

            var texture = new Texture2D(width, 1, TextureFormat.RGBA32, false)
            {
                name = "Footstep Step Fill",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                hideFlags = HideFlags.HideAndDontSave
            };
            texture.SetPixels(pixels);
            texture.Apply(false, true);
            StepFills[color] = texture;
            return texture;
        }

        static Color Brighter(Color color)
        {
            return Color.Lerp(color, Color.white, 0.35f);
        }

        void PruneSelection()
        {
            if (_session?.tracks == null || _selectedTrack == null || !_session.tracks.Contains(_selectedTrack))
            {
                _selectedTrack = null;
                _selectedMarker = null;
                return;
            }

            if (_selectedMarker != null &&
                (_selectedTrack.steps == null || !_selectedTrack.steps.Contains(_selectedMarker)))
                _selectedMarker = null;
        }

        void DeleteSelection()
        {
            if (_selectedTrack == null || _selectedMarker == null)
                return;

            DeleteStep(_selectedTrack, _selectedMarker);
        }

        void DuplicateSelection()
        {
            if (_selectedTrack == null || _selectedMarker == null)
                return;

            float duration = Mathf.Max(MinStep, _selectedMarker.end - _selectedMarker.start);
            float start = FootstepEditMath.DuplicateStart(_selectedMarker.end, FrameRate, SnapToGrid);
            PlaceCopy(_selectedTrack, _selectedMarker.type, _selectedMarker.weight, start, duration, "Duplicate Footstep");
        }

        void CutSelection()
        {
            if (_selectedTrack == null || _selectedMarker == null)
                return;

            CutMarker(_selectedTrack, _selectedMarker);
        }

        void CutMarker(FootstepTrackState track, FootstepMarker marker)
        {
            CopyMarker(marker);
            DeleteStep(track, marker);
        }

        void CopySelection()
        {
            CopyMarker(_selectedMarker);
        }

        void CopyMarker(FootstepMarker marker)
        {
            if (marker == null)
                return;

            _clipboard = new FootstepClipboard
            {
                type = marker.type,
                weight = marker.type != null ? marker.type.weight : marker.weight,
                duration = Mathf.Max(MinStep, marker.end - marker.start)
            };
        }

        void PasteClipboard()
        {
            if (_clipboard == null)
                return;

            if (_selectedMarker != null && _selectedTrack != null)
            {
                float start = _selectedMarker.end;
                InsertStep(
                    _selectedTrack,
                    _clipboard.type,
                    _clipboard.weight,
                    start,
                    _clipboard.duration,
                    "Paste Footstep");
                return;
            }

            if (_selectedTrack == null)
                return;

            PasteAt(_selectedTrack, _playheadTime);
        }

        void PasteAt(FootstepTrackState track, float time)
        {
            if (_clipboard == null || track == null)
                return;

            float start = SnapToGrid
                ? FootstepEditMath.SnapToFrame(time, FrameRate, 0f, Duration)
                : time;
            InsertStep(
                track,
                _clipboard.type,
                _clipboard.weight,
                start,
                _clipboard.duration,
                "Paste Footstep");
        }

        void InsertStep(
            FootstepTrackState track,
            FootstepType type,
            float weight,
            float start,
            float duration,
            string undoName)
        {
            if (_session == null || track == null || duration < MinStep)
                return;

            if (track.steps == null)
                track.steps = new List<FootstepMarker>();

            start = Mathf.Max(0f, start);
            float end = start + duration;
            RememberEdit();
            ShiftOverlaps(track.steps, start, end);
            var marker = new FootstepMarker
            {
                start = start,
                end = end,
                type = type,
                weight = type != null ? type.weight : weight
            };
            track.steps.Add(marker);
            _selectedTrack = track;
            _selectedMarker = marker;
            _session.hasUnbakedChanges = true;
            EditorUtility.SetDirty(_session);
            Refresh();
            Edited?.Invoke();
        }

        static void ShiftOverlaps(List<FootstepMarker> steps, float start, float end)
        {
            float first = float.PositiveInfinity;
            float push = 0f;
            for (int i = 0; i < steps.Count; i++)
            {
                FootstepMarker step = steps[i];
                if (step == null || step.end <= start + 0.0001f)
                    continue;
                if (step.start >= end - 0.0001f)
                    continue;

                if (step.start < first)
                    first = step.start;
                float needed = end - step.start;
                if (needed > push)
                    push = needed;
            }

            if (push <= 0.0001f)
                return;

            for (int i = 0; i < steps.Count; i++)
            {
                FootstepMarker step = steps[i];
                if (step == null || step.start < first - 0.0001f)
                    continue;

                step.start += push;
                step.end += push;
            }
        }

        void PlaceCopy(
            FootstepTrackState track,
            FootstepType type,
            float weight,
            float start,
            float duration,
            string undoName)
        {
            if (_session == null || track?.steps == null || duration < MinStep)
                return;

            float probe = Mathf.Min(start + MinStep * 0.25f, Duration);
            FootstepEditMath.GetGap(track.steps, probe, Duration, out float gapStart, out float gapEnd);
            if (gapEnd - gapStart < MinStep)
                return;

            float maxStart = gapEnd - MinStep;
            float fittedStart = SnapToGrid
                ? SnapRange(start, gapStart, maxStart)
                : Mathf.Clamp(start, gapStart, maxStart);
            float fittedEnd = Mathf.Min(fittedStart + duration, gapEnd);
            if (fittedEnd - fittedStart < MinStep)
                return;

            var marker = new FootstepMarker
            {
                start = fittedStart,
                end = fittedEnd,
                type = type,
                weight = type != null ? type.weight : weight
            };

            RememberEdit();
            track.steps.Add(marker);
            _selectedTrack = track;
            _selectedMarker = marker;
            _session.hasUnbakedChanges = true;
            EditorUtility.SetDirty(_session);
            Refresh();
            Edited?.Invoke();
        }

        sealed class FootstepClipboard
        {
            public FootstepType type;
            public float weight;
            public float duration;
        }
    }
}
