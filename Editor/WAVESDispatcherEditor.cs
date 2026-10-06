using MadeYellow.EventBus;
using MadeYellow.WAVES.Actors;
using MadeYellow.WAVES.Footsteps;
using UnityEditor;
using UnityEngine;

namespace MadeYellow.WAVES.Editor
{
    [CustomEditor(typeof(WAVESDispatcher))]
    public sealed class WAVESDispatcherEditor : UnityEditor.Editor
    {
        SerializedProperty _bus;
        SerializedProperty _actor;

        void OnEnable()
        {
            _bus = serializedObject.FindProperty("_bus");
            _actor = serializedObject.FindProperty("_actor");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            if (_bus.objectReferenceValue == null)
            {
                EditorGUILayout.HelpBox(
                    "An Event Bus is required. Without it this component disables itself on Start and does not publish signals.",
                    MessageType.Warning);
            }

            EditorGUILayout.PropertyField(
                _bus,
                new GUIContent("Event Bus", "Bus that receives published signals. Required."));
            if (_bus.objectReferenceValue == null && DrawAssetButton("Create Event Bus"))
                CreateEventBus();

            if (_actor.objectReferenceValue == null)
            {
                EditorGUILayout.HelpBox(
                    "An Actor Profile is required. Without it this component disables itself on Start and does not publish footsteps.",
                    MessageType.Warning);
            }

            EditorGUILayout.PropertyField(
                _actor,
                new GUIContent("Actor Profile", "Actor kind copied into each Actor Footstep Started event. Required."));
            if (DrawAssetButton("Create Actor Profile"))
                CreateProfile();

            serializedObject.ApplyModifiedProperties();
        }

        void CreateEventBus()
        {
            string path = EditorUtility.SaveFilePanelInProject(
                "Create Event Bus",
                "FootstepEventBus",
                "asset",
                "Choose where to save the Event Bus.");
            if (string.IsNullOrEmpty(path))
                return;

            var bus = ScriptableObject.CreateInstance<HighPubLowSubEventBus>();
            AssetDatabase.CreateAsset(bus, path);
            AssetDatabase.SaveAssets();

            serializedObject.Update();
            _bus.objectReferenceValue = bus;
            serializedObject.ApplyModifiedProperties();
        }

        void CreateProfile()
        {
            string path = EditorUtility.SaveFilePanelInProject(
                "Create Actor Profile",
                "ActorProfile",
                "asset",
                "Choose where to save the Actor Profile.");
            if (string.IsNullOrEmpty(path))
                return;

            var profile = ScriptableObject.CreateInstance<ActorProfile>();
            AssetDatabase.CreateAsset(profile, path);
            AssetDatabase.SaveAssets();

            serializedObject.Update();
            _actor.objectReferenceValue = profile;
            serializedObject.ApplyModifiedProperties();
        }

        static bool DrawAssetButton(string label)
        {
            Rect row = EditorGUILayout.GetControlRect(true);
            float labelWidth = EditorGUIUtility.labelWidth;
            float width = Mathf.Max(0f, row.width - labelWidth);
            var field = new Rect(row.x + labelWidth, row.y, width, row.height);
            return GUI.Button(field, label);
        }
    }
}
