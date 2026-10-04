# Changelog

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