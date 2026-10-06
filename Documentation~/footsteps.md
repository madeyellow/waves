# Footsteps

The footsteps module turns authored steps on an animation clip into runtime contacts. Each contact names the track, the footstep type, the foot rotation, and the ground: a collider, a world point, and a normal. WAVES uses that contact to resolve a surface and play the step.

Steps are marked in the [Footstep Editor](index.md) and baked into float curves on the clip. At runtime, **Footstep Reader** samples those curves once per frame.

## Footstep Reader

`FootstepReader` sits on the same object as the Animator. It reads one float parameter per track in the assigned Footstep Tracks Profile and raises a start event when a foot enters contact and a finish event when it leaves.

The reader is built for a frame update. **Late Update** is the default, so the sample runs after the Animator has applied the pose. **Update** samples earlier in the frame. **Manual** does not sample on its own: call `SampleFootsteps` from your own frame update. A contact starts only after the curve holds one footstep weight for two samples in a row, so a blend between two weights is not reported as a step.

Each sample can find the ground with a ray down from the foot, or from a collider you assign when raycasting is off. Events exist for every track at once, and again on each track. The latest sample stays available until the next step on that track.

The component turns itself off when the profile, the Animator, or the Animator controller is missing.

### API

#### SampleTiming

Chooses when curves are read. `LateUpdate` reads after the pose. `Update` reads in Update. `Manual` waits for `SampleFootsteps`.

```csharp
using MadeYellow.WAVES.Footsteps;
using UnityEngine;

public sealed class ManualFootsteps : MonoBehaviour
{
    [SerializeField] FootstepReader _reader;

    void Awake()
    {
        _reader.SampleTiming = FootstepSampleTiming.Manual;
    }

    void LateUpdate()
    {
        _reader.SampleFootsteps();
    }
}
```

#### SampleFootsteps

Reads every track curve and opens, updates, or closes its contact. Call it only when `SampleTiming` is `Manual`. Update and Late Update call it themselves.

A start event waits for the second sample of a plateau. If the curve goes quiet before then, no event is raised.

```csharp
_reader.SampleFootsteps();
```

#### GetFootstep

Returns the latest `FootstepData` for a profile track name, or for the animator hash of that name. The result is the default value when the track is missing or has not stepped yet. After the foot lifts, the value stays at the sample sent with the finish event.

`channelHash` is the animator hash of the track name. `type` is the footstep type, or null when no types exist. `collider` is null when the ray missed, or when raycasting is off and no surface collider is set. `point` is the hit point, the closest point on that collider, or the foot position. `normal` is the hit normal, or up when the sample did not come from a ray. `rotation` is the foot rotation.

```csharp
FootstepData step = _reader.GetFootstep("Left Foot");
if (step.collider != null)
    Debug.Log(step.point);
```

#### GetTrack

Returns the live `FootstepTrack` for a profile track name, a `FootstepTrackDefinition`, or an animator hash. The result is null when that track is not in the current profile. Read the track from Start onward and keep the reference. The reader rebuilds handles when it awakens.

`Name` and `Hash` identify the track. `InContactPhase` is true while the foot is down. `CurrentValue` is the same sample `GetFootstep` returns. `Events` is never null.

```csharp
FootstepTrack left = _reader.GetTrack("Left Foot");
if (left != null && left.InContactPhase)
    Debug.Log(left.CurrentValue.point);
```

#### UseRaycasting, RaycastMask, and RaycastOffset

`UseRaycasting` casts a ray down from the foot while it is in contact and stores the collider, point, and normal. The ray starts `RaycastOffset` meters above the foot and travels that offset plus 2 meters downward. Triggers are ignored. `RaycastMask` chooses the layers. Include the ground and exclude the character, or the ray stops on the body.

```csharp
_reader.UseRaycasting = true;
_reader.RaycastMask = LayerMask.GetMask("Ground");
_reader.RaycastOffset = 0.15f;
```

#### SurfaceCollider

Ground collider used when `UseRaycasting` is off. The contact point is the closest point on this collider to the foot, and the normal is up. The same collider is shared by every track. It is ignored while raycasting is on. Leave it empty to publish a step with no collider.

```csharp
_reader.UseRaycasting = false;
_reader.SurfaceCollider = deck;
```

#### OnFootstepStarted and OnFootstepFinished

`OnFootstepStarted` is raised when any track enters contact. `OnFootstepFinished` is raised when any track leaves it. Both pass a `FootstepData` sample. Remove the listener when the subscriber is disabled.

```csharp
using MadeYellow.WAVES.Footsteps;
using UnityEngine;

public sealed class FootstepListener : MonoBehaviour
{
    [SerializeField] FootstepReader _reader;

    void OnEnable()
    {
        _reader.OnFootstepStarted.AddListener(OnStep);
    }

    void OnDisable()
    {
        _reader.OnFootstepStarted.RemoveListener(OnStep);
    }

    void OnStep(FootstepData step)
    {
        Debug.Log(step.point);
    }
}
```

The same two events exist on one track through `GetTrack`. Listeners added in code are not saved. Listeners added in the inspector are.

```csharp
void OnEnable()
{
    _reader.GetTrack("Left Foot").Events.OnFootstepStarted.AddListener(OnLeftStep);
}

void OnDisable()
{
    _reader.GetTrack("Left Foot").Events.OnFootstepStarted.RemoveListener(OnLeftStep);
}

void OnLeftStep(FootstepData step)
{
    Debug.Log(step.rotation);
}
```

#### Profile

The Footstep Tracks Profile whose track names match the baked curves. This property is read-only. Assign the profile on the component.
