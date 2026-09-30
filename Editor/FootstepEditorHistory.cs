using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace MadeYellow.WAVES.Editor
{
    sealed class FootstepEditorHistory
    {
        struct Snapshot
        {
            public List<FootstepTrackState> tracks;
            public bool hasUnbakedChanges;
        }

        readonly List<Snapshot> _past = new List<Snapshot>();
        readonly List<Snapshot> _future = new List<Snapshot>();

        public bool CanUndo => _past.Count > 0;

        public bool CanRedo => _future.Count > 0;

        public void Clear()
        {
            _past.Clear();
            _future.Clear();
        }

        public void Push(FootstepEditSession session)
        {
            if (session == null)
                return;

            _past.Add(Capture(session));
            _future.Clear();
        }

        public bool Undo(FootstepEditSession session)
        {
            if (session == null || _past.Count == 0)
                return false;

            _future.Add(Capture(session));
            Apply(_past[_past.Count - 1], session);
            _past.RemoveAt(_past.Count - 1);
            return true;
        }

        public bool Redo(FootstepEditSession session)
        {
            if (session == null || _future.Count == 0)
                return false;

            _past.Add(Capture(session));
            Apply(_future[_future.Count - 1], session);
            _future.RemoveAt(_future.Count - 1);
            return true;
        }

        static Snapshot Capture(FootstepEditSession session)
        {
            return new Snapshot
            {
                tracks = CopyTracks(session.tracks),
                hasUnbakedChanges = session.hasUnbakedChanges
            };
        }

        static void Apply(Snapshot snapshot, FootstepEditSession session)
        {
            session.tracks = CopyTracks(snapshot.tracks);
            session.hasUnbakedChanges = snapshot.hasUnbakedChanges;
            EditorUtility.SetDirty(session);
        }

        static List<FootstepTrackState> CopyTracks(List<FootstepTrackState> tracks)
        {
            var copy = new List<FootstepTrackState>();
            if (tracks == null)
                return copy;

            for (int i = 0; i < tracks.Count; i++)
            {
                FootstepTrackState track = tracks[i];
                if (track == null)
                    continue;

                var steps = new List<FootstepMarker>();
                if (track.steps != null)
                {
                    for (int s = 0; s < track.steps.Count; s++)
                    {
                        FootstepMarker step = track.steps[s];
                        if (step == null)
                            continue;

                        steps.Add(new FootstepMarker
                        {
                            type = step.type,
                            start = step.start,
                            end = step.end,
                            weight = step.weight
                        });
                    }
                }

                copy.Add(new FootstepTrackState
                {
                    name = track.name,
                    bakeIntoAnimationCurve = track.bakeIntoAnimationCurve,
                    color = track.color,
                    steps = steps
                });
            }

            return copy;
        }
    }
}
