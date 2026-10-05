# Changelog

## [0.1.4] - 2026-10-05

### Fixed

- A step placed at the start of a clip is detected again when that clip uses Loop Pose. Loop Pose spreads the difference between the first and last curve values across the whole clip. A step on the first frame made that difference the whole step weight, so the animator never held the plateau and never returned to silence. Footstep Reader then raised neither the start nor the finish. Baking now gives the last key the same value as the first. That key is the loop point, not a second step, and the step in the editor stays where it was. Clips without Loop Pose still end in silence, so a one-shot does not hold the step after it finishes. Bake the clip again to rewrite a curve that was saved before this fix.

## [0.1.3] - 2026-10-05

### Added

- A footstep effect can route its sound through an Audio Mixer Group and set Volume, Spatial Blend, and Reverb Zone Mix. Those values are applied to the AudioSource that plays the step. Volume, spatial blend, and reverb mix run from 0 to 1. Spatial blend is 2D at 0 and 3D at 1. A new effect starts at full volume, fully 3D, and full reverb mix.

- Each actor and surface group has General Effects Settings for that playback, including audible distance, minimum distance, rolloff, and doppler. A footstep type uses those settings unless Override settings is turned on for that type.

- The step list ends with Any, marked Fallback. If a footstep type has no audio of its own, or no visual of its own, the missing part is taken from Any in the same group. A part that the type does set stays its own.

- Footstep types are opened from a gear on the Footstep type effects header. Help icons on that header, on each footstep type, and on Any explain which settings apply.

### Changed

- An effect saved before these settings existed keeps a custom audible distance, minimum distance, rolloff, or doppler. That step is switched to Override settings so it does not fall back to the shared defaults.

- Package links now point at the waves repository.

## [0.1.2] - 2026-10-04

### Changed

- The Footstep Editor Bake button stays a normal button when there is nothing to bake, and turns green when the clip has unbaked changes.

### Fixed

- Number fields on Footstep Reader, such as Raycast Offset and Gizmo Size, can be clicked and typed into again. Each section header drew its title with `EditorGUI.LabelField`, which claims a control id, and it only ran on repaint. That shifted the id of every control below the header between the click and the redraw, so the field that was clicked was never the field that took the keyboard.

- Footstep Reader no longer reports a step from a curve sample that is only passing through another footstep weight. A contact starts after the curve holds near one weight for two samples in a row, so a blend or ramp between silence and Run is not reported as Walk. The start event waits for that second sample. If the curve goes quiet before then, no event is raised.

## [0.1.1] - 2026-10-04

### Changed

- The package now requires Unity 6000.5. `Object.GetInstanceID` is obsolete there, so object identity uses `EntityId` instead.
- The Visual Effect Graph dependency is now 17.5.0.

### Fixed

- Baking a footstep track now writes a silence key at the start of the clip when the first step does not begin there.
- Baking a footstep track now writes a silence key at the end of the clip when no step reaches the last frame. Without these keys the importer treated the first and last step keys as the clip bounds and stretched the curve.