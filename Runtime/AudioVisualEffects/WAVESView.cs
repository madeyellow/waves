using UnityEngine;

namespace MadeYellow.WAVES.AudioVisualEffects
{
    /// <summary>Listener and camera positions, refreshed at most once per frame.</summary>
    static class WAVESView
    {
        static int _frame = -1;
        static AudioListener _listener;
        static Camera _camera;

        /// <summary>True after a listener has been seen this frame.</summary>
        public static bool HasListener { get; private set; }

        /// <summary>World position of the listener this frame.</summary>
        public static Vector3 ListenerPosition { get; private set; }

        /// <summary>True after a camera has been seen this frame.</summary>
        public static bool HasCamera { get; private set; }

        /// <summary>World position of the camera this frame.</summary>
        public static Vector3 CameraPosition { get; private set; }

        /// <summary>True when <paramref name="point"/> is farther than <paramref name="distance"/> from the listener.</summary>
        public static bool BeyondListener(Vector3 point, float distance)
        {
            Tick();
            if (!HasListener)
                return true;

            return Beyond(point, ListenerPosition, distance);
        }

        /// <summary>True when <paramref name="point"/> is farther than <paramref name="distance"/> from the camera.</summary>
        public static bool BeyondCamera(Vector3 point, float distance)
        {
            Tick();
            if (!HasCamera)
                return true;

            return Beyond(point, CameraPosition, distance);
        }

        static void Tick()
        {
            int frame = Time.frameCount;
            if (_frame == frame)
                return;

            _frame = frame;
            if (_listener == null)
                _listener = Object.FindAnyObjectByType<AudioListener>();

            HasListener = _listener != null;
            if (HasListener)
                ListenerPosition = _listener.transform.position;

            if (_camera == null)
                _camera = Camera.main;

            HasCamera = _camera != null;
            if (HasCamera)
                CameraPosition = _camera.transform.position;
        }

        static bool Beyond(Vector3 point, Vector3 view, float distance)
        {
            float dx = point.x - view.x;
            float dy = point.y - view.y;
            float dz = point.z - view.z;
            float limit = distance > 0f ? distance : 0f;
            return dx * dx + dy * dy + dz * dz > limit * limit;
        }
    }
}
