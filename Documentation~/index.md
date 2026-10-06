# Footstep

Visual timeline editor for marking and baking footstep curves on an animation clip in Unity 6.

Open **Window > MadeYellow > WAVES > Footstep Editor**. Create a **Footstep Tracks Profile** and one or more **Footstep Type** assets from **Assets > Create > MadeYellow > WAVES**.

Choose a profile from the dropdown above the track names. Footstep lists every Footstep Tracks Profile in the project and selects the first one when none is chosen yet. Assign an imported model, then pick one of its clips. Each profile track is a lane. Drag on empty space to add a step, drag the body to move it, and drag the edges to resize it. Right-click a step to change its type or delete it.

**Bake** writes a float curve for each track into that clip's **Curves** on the model importer, then reimports the model. Other curves on the clip stay as they are. The curve name is the track name. The curve value is the step type's weight while a step is active, and 0 otherwise. Tangents are constant, so each transition is a single key. Opening the clip reads those curves back onto the timeline. Bake is enabled only after an edit. Switching the model or clip with unbaked edits asks whether to bake first.

Undo and redo use Unity's shortcuts (**Edit > Undo** / **Edit > Redo**).

Runtime sampling is described in [Footsteps](footsteps.md). Jumps and landings are described in [Jumps and lands](jumps-and-lands.md).
