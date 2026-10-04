# Changelog

## [0.1.1] - 2026-10-04

### Changed

- The package now requires Unity 6000.5. `Object.GetInstanceID` is obsolete there, so object identity uses `EntityId` instead.
- The Visual Effect Graph dependency is now 17.5.0.

### Fixed

- Baking a footstep track now writes a silence key at the start of the clip when the first step does not begin there.
- Baking a footstep track now writes a silence key at the end of the clip when no step reaches the last frame. Without these keys the importer treated the first and last step keys as the clip bounds and stretched the curve.