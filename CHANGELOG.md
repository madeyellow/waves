# Changelog

## [0.1.11] - 2026-10-07

### Added

- Surface Type Manager lists every surface type and edits the one you select. Open it from Window > MadeYellow > WAVES > Surface Type Manager, or choose Manage on a surface type in WAVES Module Browser. The right-click menu for a surface type there is only Manage and Remove. Add Surface Type creates a new asset at the end of the list. Drag a row to reorder it. The detail shows the color, the name, keywords, and the materials and textures that identify the type.

- A keyword matches a material or texture name when that name contains it, ignoring case. A * stands for any text, including none. Auto Search adds the matches and leaves entries that are already listed. It stays off while the type has no keywords. Dropping a material or texture adds that asset. Dropping a folder adds every material or texture in the folder and in the folders inside it, with no keyword filter. Clear empties that list on the surface type and leaves the project assets where they are. A row shows the asset in an open Project window and, when an Inspector is open, selects it there. The cross on the row removes it from the type.

- A collider with no Surface Marker and no terrain map resolves from the renderer's materials, then from the textures on those materials. The renderer on the collider is used first, then one on a child, then one on a parent. That includes a skinned mesh. The first matching material wins, and a listed material wins over a texture. When the same asset is listed on more than one type, the lower Order wins, and the same Order uses the name. A marker still wins over a terrain sample. A terrain map that misses does not fall through to materials, and terrain is still sampled at the point every time.

### Changed

- Outline buttons, including Add Surface Type, use a pointer cursor.

## [0.1.10] - 2026-10-07

### Added

- WAVES remembers surface lookups for colliders that are not terrain. The result is kept per collider until Surface Cache Lifetime ends, including a collider with no surface. Terrain is sampled at the point every time, and a miss on one part of a terrain is not reused for the rest of it. When a marker and a terrain share a collider, the marker is remembered and the terrain is used again after that memory ends.

- The WAVES inspector groups this under Caching. Use Caching turns it on. Surface Cache Lifetime is hidden while that is off, and it cannot be shorter than 0.01 unscaled seconds. A new component starts with caching on and a lifetime of 60 seconds. The Modules label uses the same header style as Caching.

## [0.1.9] - 2026-10-06

### Added

- Jumps & Lands plays a reaction when a character leaves the ground or hits it. The character controller publishes the moment. `PublishJump` and `PublishLand` on WAVES Dispatcher copy the actor profile and the object identity into the signal and send it on the dispatcher's bus. A jump carries the ground collider, the takeoff point, and the normal. A landing carries those and a Fall Type. Create Fall Types from Assets > Create > MadeYellow > WAVES > Fall Type. An empty fall type uses the fallback landing. Nothing is sent when the dispatcher, its bus, or its actor profile is missing. The module plays nothing when the collider is missing.

- In the module browser, each actor and surface group has a Jumping fold and a Landing fold. Landing lists every fall type, then Any. A jump does not use landing effects, and a landing does not use the jump slot. Audio and visual blocks, including Override settings, match the footstep group. How to call the methods is documented in Documentation~/jumps-and-lands.md.

### Changed

- WAVES Dispatcher no longer requires a Footstep Reader. Footsteps still publish when a reader is on the same object. A character that only jumps does not get a reader. Mute Footsteps still mutes footsteps only.

- General Effects Settings include a visible distance for visuals, on footsteps and on jumps and landings. A slot uses that distance unless Override settings is turned on under its visual. Particles and the graph stay on the slot. A visible distance saved before this stays on that slot.

- Footstep types no longer have an icon. The type name is what the module browser and the footstep editor show. Fall types have no icon either.

- The footstep group editor draws its audio and visual blocks through the same controls as jumps and landings. Spatial blend and doppler describe the contact point.

## [0.1.7] - 2026-10-06

### Changed

- WAVES now requires Event Bus 1.1.0. The dispatcher and the WAVES component accept any bus asset, so an existing Event Bus stays assigned, and a High Pub Low Sub Event Bus assigns as well. Create Event Bus on the dispatcher creates a High Pub Low Sub Event Bus. Publishing on that bus does not allocate. A signal passed to Publish Event must be a struct.

## [0.1.6] - 2026-10-06

### Added

- Footstep Reader has a Surface Collider, used when raycasting is off. The contact point is the closest point on that collider to the foot, and the normal is up. Every track shares this collider. It is hidden in the inspector while raycasting is on, and ignored in that mode. Leave it empty to publish a step with no collider.

- The footsteps module is documented in Documentation~/footsteps.md, including the Footstep Reader API. The footstep editor page links to it, and the readme links to the package documentation.

### Changed

- A footstep sample no longer carries a RaycastHit. FootstepData and ActorFootstepStarted now have a collider, a world point, and a normal, along with the foot rotation they already had. While raycasting is on, Footstep Reader fills those from the ray. A missed ray leaves the collider empty, uses the foot position, and uses an up normal. WAVES resolves the surface from the collider and the point, plays audio at that point, and aligns visuals to the normal. TryGetSurface(RaycastHit) still works and reads the same two values from the hit.

- Footstep Reader no longer samples curves in Fixed Update. Steps are read in Update, in Late Update after the Animator applies the pose, or when SampleFootsteps is called. A reader saved on Fixed Update moves to Late Update. Manual is unchanged.

## [0.1.5] - 2026-10-06

### Added

- WAVES Module Browser shows one slot for every module type in the project. Built-in modules come first: Footsteps, Jumps & Lands, and Directional Actions. Every other module, including ones added by the project, follows in name order. A module with no readable name uses its class name.

- An empty slot creates a preset named "New {Module Name} Preset", or takes one that already exists from the dropdown. When that module has no presets, the dropdown is disabled and reads "No presets". Choosing a preset, or clicking a filled slot, edits that module. Until one is selected, the browser explains that WAVES is set up by adding module presets.

- Jumps & Lands and Directional Actions ship with the package. Their actor and surface matrix can create groups. The group editor says "WIP: Coming soon" until those effects exist. A custom module with no editor of its own still gets a slot, and a selected group says there is nothing to edit yet.

### Changed

- The window is WAVES Module Browser, opened from Window > MadeYellow > WAVES > Module Browser. The Add Module button is gone, because every module already has a slot. The module name on an empty slot is gold, so it stays separate from the create-preset label.

- The WAVES component lists only modules already assigned to it. Clicking one opens WAVES Module Browser on that component and that preset. When none are assigned, a button opens the browser. Removing a module there updates an open browser: that slot becomes empty, and the editor closes if that module was selected.

### Fixed

- The horizontal scrollbar on the module strip no longer covers the preset dropdowns when the window is too narrow to show every slot.

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