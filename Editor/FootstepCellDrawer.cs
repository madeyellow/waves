using MadeYellow.WAVES.AudioVisualEffects.Modules.FootstepsModule;
using MadeYellow.WAVES.Footsteps;
using UnityEditor;
using UnityEngine;

namespace MadeYellow.WAVES.Editor
{
    /// <summary>Step list for one actor and surface group.</summary>
    [InitializeOnLoad]
    static class FootstepCellDrawer
    {
        const string StepEffectsHelp = "Effect settings for this footstep type.";
        const string AnyEffectsHelp = "Fallback effects. If a footstep type has no effects of its own, these are used.";
        const string GeneralEffectsHelp = "Main effect settings. If Override settings is off for a footstep type, these are used.";

        static FootstepCellDrawer()
        {
            WAVESModuleDrawers.Register(typeof(WAVESFootstepModule), Draw);
        }

        static void Draw(SerializedProperty data, string scope)
        {
            SerializedProperty steps = data != null ? data.FindPropertyRelative("_steps") : null;
            if (steps == null || !steps.isArray)
            {
                EditorGUILayout.HelpBox("This group has no step list.", MessageType.Info);
                return;
            }

            WAVESEffectSlotDrawer.DrawGearHeader(
                "Footstep type effects",
                "Manage footstep types",
                FootstepTypeSettingsWindow.Open);
            for (int i = 0; i < WAVESCatalog.Steps.Count; i++)
            {
                FootstepType step = WAVESCatalog.Steps[i];
                if (step == null)
                    continue;

                DrawStep(steps, scope, step.name, step, false, null, StepEffectsHelp);
            }

            DrawStep(steps, scope, "Any", null, false, "(Fallback)", AnyEffectsHelp);
            EditorGUILayout.Space(8f);
            WAVESEffectSlotDrawer.DrawCommon(data, GeneralEffectsHelp);
        }

        static void DrawStep(
            SerializedProperty steps,
            string scope,
            string title,
            FootstepType step,
            bool openByDefault,
            string caption = null,
            string help = null)
        {
            string stepId = step != null ? step.GetEntityId().ToString() : EntityId.None.ToString();
            string key = scope + ".t." + stepId;
            EditorGUILayout.Space(2f);
            int index = FindStep(steps, step);
            SerializedProperty element = index >= 0 ? steps.GetArrayElementAtIndex(index) : null;
            bool hasAudio = element != null && element.FindPropertyRelative("_audio").objectReferenceValue != null;
            bool hasVisual = element != null && (
                element.FindPropertyRelative("_particles").objectReferenceValue != null ||
                element.FindPropertyRelative("_graph").objectReferenceValue != null);
            if (!WAVESEffectSlotDrawer.DrawFoldout(
                    key,
                    title,
                    openByDefault,
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
                    steps.DeleteArrayElementAtIndex(index);
                return;
            }

            if (index < 0)
            {
                index = steps.arraySize;
                steps.arraySize = index + 1;
                element = steps.GetArrayElementAtIndex(index);
                element.FindPropertyRelative("_step").objectReferenceValue = step;
            }
            else
            {
                element = steps.GetArrayElementAtIndex(index);
            }

            draft.Write(element);
        }

        static int FindStep(SerializedProperty steps, FootstepType step)
        {
            for (int i = 0; i < steps.arraySize; i++)
            {
                if (steps.GetArrayElementAtIndex(i).FindPropertyRelative("_step").objectReferenceValue == step)
                    return i;
            }

            return -1;
        }
    }
}
