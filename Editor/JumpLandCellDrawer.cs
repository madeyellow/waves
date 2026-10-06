using MadeYellow.WAVES.AudioVisualEffects.Modules.JumpsAndLandsModule;
using MadeYellow.WAVES.Jumps;
using UnityEditor;
using UnityEngine;

namespace MadeYellow.WAVES.Editor
{
    /// <summary>Jump slot and landing list for one actor and surface group.</summary>
    [InitializeOnLoad]
    static class JumpLandCellDrawer
    {
        const string JumpEffectsHelp = "Effect settings for a jump off this surface.";
        const string LandingEffectsHelp = "Landing types. The character chooses the type when it hits the ground.";
        const string LandEffectsHelp = "Effect settings for this landing type.";
        const string AnyEffectsHelp = "Fallback effects. If a landing type has no effects of its own, these are used.";
        const string GeneralEffectsHelp = "Main effect settings. If Override settings is off for a jump or a landing type, these are used.";

        static JumpLandCellDrawer()
        {
            WAVESModuleDrawers.Register(typeof(WAVESJumpLandModule), Draw);
        }

        static void Draw(SerializedProperty data, string scope)
        {
            SerializedProperty jump = data != null ? data.FindPropertyRelative("_jump") : null;
            SerializedProperty lands = data != null ? data.FindPropertyRelative("_lands") : null;
            if (jump == null || lands == null || !lands.isArray)
            {
                EditorGUILayout.HelpBox("This group has no jump or landing list.", MessageType.Info);
                return;
            }

            DrawJump(jump, scope);
            EditorGUILayout.Space(6f);
            DrawLanding(lands, scope);
            EditorGUILayout.Space(8f);
            DrawCommon(data);
        }

        static void DrawJump(SerializedProperty jump, string scope)
        {
            bool hasAudio = jump.FindPropertyRelative("_audio").objectReferenceValue != null;
            bool hasVisual = jump.FindPropertyRelative("_particles").objectReferenceValue != null
                || jump.FindPropertyRelative("_graph").objectReferenceValue != null;
            EditorGUILayout.Space(2f);
            if (!WAVESEffectSlotDrawer.DrawFoldout(
                    scope + ".jump",
                    "Jumping",
                    false,
                    WAVESEffectSlotDrawer.StatusIconReserve,
                    null,
                    JumpEffectsHelp,
                    out Rect header))
            {
                WAVESEffectSlotDrawer.DrawStatusIcons(header, hasAudio, hasVisual);
                return;
            }

            WAVESEffectSlotDrawer.DrawStatusIcons(header, hasAudio, hasVisual);
            WAVESEffectSlotDrawer.BeginContent(header);
            WAVESEffectSlotDrawer.EffectDraft draft = WAVESEffectSlotDrawer.EffectDraft.Read(jump);
            EditorGUI.BeginChangeCheck();
            WAVESEffectSlotDrawer.DrawEffectBody(ref draft);
            WAVESEffectSlotDrawer.EndContent();
            if (EditorGUI.EndChangeCheck())
                draft.Write(jump);
        }

        static void DrawLanding(SerializedProperty lands, string scope)
        {
            bool hasAudio = false;
            bool hasVisual = false;
            for (int i = 0; i < lands.arraySize; i++)
            {
                SerializedProperty element = lands.GetArrayElementAtIndex(i);
                if (element.FindPropertyRelative("_audio").objectReferenceValue != null)
                    hasAudio = true;
                if (element.FindPropertyRelative("_particles").objectReferenceValue != null
                    || element.FindPropertyRelative("_graph").objectReferenceValue != null)
                    hasVisual = true;
            }

            EditorGUILayout.Space(2f);
            if (!WAVESEffectSlotDrawer.DrawFoldout(
                    scope + ".land",
                    "Landing",
                    false,
                    WAVESEffectSlotDrawer.StatusIconReserve,
                    null,
                    LandingEffectsHelp,
                    out Rect header))
            {
                WAVESEffectSlotDrawer.DrawStatusIcons(header, hasAudio, hasVisual);
                return;
            }

            WAVESEffectSlotDrawer.DrawStatusIcons(header, hasAudio, hasVisual);
            WAVESEffectSlotDrawer.BeginContent(header);
            WAVESEffectSlotDrawer.DrawGearHeader(
                "Landing type effects",
                "Manage landing types",
                FallTypeSettingsWindow.Open);
            for (int i = 0; i < WAVESCatalog.Falls.Count; i++)
            {
                ActorFallType fall = WAVESCatalog.Falls[i];
                if (fall == null)
                    continue;

                DrawLand(lands, scope, fall.name, fall, null, LandEffectsHelp);
            }

            DrawLand(lands, scope, "Any", null, "(Fallback)", AnyEffectsHelp);
            WAVESEffectSlotDrawer.EndContent();
        }

        static void DrawLand(
            SerializedProperty lands,
            string scope,
            string title,
            ActorFallType fall,
            string caption,
            string help)
        {
            string fallId = fall != null ? fall.GetEntityId().ToString() : EntityId.None.ToString();
            string key = scope + ".f." + fallId;
            EditorGUILayout.Space(2f);
            int index = FindLand(lands, fall);
            SerializedProperty element = index >= 0 ? lands.GetArrayElementAtIndex(index) : null;
            bool hasAudio = element != null && element.FindPropertyRelative("_audio").objectReferenceValue != null;
            bool hasVisual = element != null && (
                element.FindPropertyRelative("_particles").objectReferenceValue != null ||
                element.FindPropertyRelative("_graph").objectReferenceValue != null);
            if (!WAVESEffectSlotDrawer.DrawFoldout(
                    key,
                    title,
                    false,
                    WAVESEffectSlotDrawer.StatusIconReserve,
                    caption,
                    help,
                    out Rect header))
            {
                WAVESEffectSlotDrawer.DrawStatusIcons(header, hasAudio, hasVisual);
                return;
            }

            WAVESEffectSlotDrawer.DrawStatusIcons(header, hasAudio, hasVisual);
            WAVESEffectSlotDrawer.BeginContent(header);
            WAVESEffectSlotDrawer.EffectDraft draft = WAVESEffectSlotDrawer.EffectDraft.Read(element);
            EditorGUI.BeginChangeCheck();
            WAVESEffectSlotDrawer.DrawEffectBody(ref draft);
            WAVESEffectSlotDrawer.EndContent();
            if (!EditorGUI.EndChangeCheck())
                return;

            if (!draft.Filled)
            {
                if (index >= 0)
                    lands.DeleteArrayElementAtIndex(index);
                return;
            }

            if (index < 0)
            {
                index = lands.arraySize;
                lands.arraySize = index + 1;
                element = lands.GetArrayElementAtIndex(index);
                element.FindPropertyRelative("_fall").objectReferenceValue = fall;
            }
            else
            {
                element = lands.GetArrayElementAtIndex(index);
            }

            draft.Write(element);
        }

        static void DrawCommon(SerializedProperty data)
        {
            WAVESEffectSlotDrawer.DrawCommon(data, GeneralEffectsHelp);
        }

        static int FindLand(SerializedProperty lands, ActorFallType fall)
        {
            for (int i = 0; i < lands.arraySize; i++)
            {
                if (lands.GetArrayElementAtIndex(i).FindPropertyRelative("_fall").objectReferenceValue == fall)
                    return i;
            }

            return -1;
        }
    }
}
