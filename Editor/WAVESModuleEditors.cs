using System;
using System.Collections.Generic;
using MadeYellow.WAVES.AudioVisualEffects.Modules;
using UnityEditor;
using UnityEngine;

namespace MadeYellow.WAVES.Editor
{
    /// <summary>Whole-module editors plugged into the module browser. A module without one keeps the default view.</summary>
    public static class WAVESModuleEditors
    {
        /// <summary>Draws the body of one module.</summary>
        public delegate void Editor(WAVESModuleBase module, SerializedObject serialized);

        static readonly Dictionary<Type, Editor> Registry = new Dictionary<Type, Editor>();
        static GUIStyle _header;
        static GUIStyle _body;
        static bool _pro;

        /// <summary>Registers <paramref name="editor"/> for <paramref name="moduleType"/>.</summary>
        public static void Register(Type moduleType, Editor editor)
        {
            if (moduleType == null || editor == null)
                return;

            Registry[moduleType] = editor;
        }

        /// <summary>Draws the registered editor. False when this module has none.</summary>
        public static bool TryDraw(WAVESModuleBase module, SerializedObject serialized)
        {
            if (module == null || serialized == null)
                return false;
            if (!Registry.TryGetValue(module.GetType(), out Editor editor) || editor == null)
                return false;

            try
            {
                editor(module, serialized);
            }
            catch (Exception exception)
            {
                EditorGUILayout.HelpBox("This module editor failed.\n" + exception.Message, MessageType.Warning);
            }

            return true;
        }

        /// <summary>Centered header and optional muted paragraph.</summary>
        public static void DrawCenteredNotice(string header, string body)
        {
            GUILayout.FlexibleSpace();
            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            EditorGUILayout.BeginVertical(GUILayout.MaxWidth(480f));
            if (!string.IsNullOrEmpty(header))
                GUILayout.Label(header, HeaderStyle());
            if (!string.IsNullOrEmpty(body))
            {
                if (!string.IsNullOrEmpty(header))
                    GUILayout.Space(8f);
                GUILayout.Label(body, BodyStyle());
            }

            EditorGUILayout.EndVertical();
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();
            GUILayout.FlexibleSpace();
        }

        static GUIStyle HeaderStyle()
        {
            EnsureStyles();
            return _header;
        }

        static GUIStyle BodyStyle()
        {
            EnsureStyles();
            return _body;
        }

        static void EnsureStyles()
        {
            bool pro = EditorGUIUtility.isProSkin;
            if (_header != null && _body != null && _pro == pro)
                return;

            _pro = pro;
            _header = new GUIStyle(EditorStyles.boldLabel)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 16,
                wordWrap = true
            };
            Color ink = WAVESChrome.Ink;
            _header.normal.textColor = ink;
            _header.hover.textColor = ink;
            _header.active.textColor = ink;
            _header.focused.textColor = ink;

            _body = new GUIStyle(EditorStyles.label)
            {
                alignment = TextAnchor.UpperCenter,
                fontSize = 12,
                wordWrap = true
            };
            Color muted = WAVESChrome.Muted;
            _body.normal.textColor = muted;
            _body.hover.textColor = muted;
            _body.active.textColor = muted;
            _body.focused.textColor = muted;
        }
    }
}
