# Jumps and lands

The jumps and lands module plays a reaction when a character leaves the ground or hits it. WAVES does not decide those moments. The character controller already knows when it jumped, and how hard it landed, and it publishes that through the dispatcher.

A jump has no intensity of its own. The surface it left, and the actor profile on the dispatcher, choose the effect. A landing also carries a fall type, such as a light fall or a hard fall. Fall types are assets you create. An empty fall type uses the fallback landing for that actor and surface.

Both signals carry the ground collider, the world point, and the normal. The collider is the ground, not the character. WAVES resolves the surface from the collider and the point, plays audio at the point, and aligns visuals to the normal. The module plays nothing when the collider is missing.

`MuteFootsteps` on the dispatcher does not mute jumps or landings.

## Calling it

`PublishJump` and `PublishLand` are extension methods on `WAVESDispatcher`. They copy the dispatcher's actor profile and the identity of its GameObject into the signal, then send it on the dispatcher's bus. Nothing is sent when the dispatcher, the bus, or the actor profile is missing. The dispatcher does not require a Footstep Reader. A reader on the same object still publishes footsteps.

```csharp
using MadeYellow.WAVES.Footsteps;
using MadeYellow.WAVES.Jumps;
using UnityEngine;

public sealed class JumpLandPublisher : MonoBehaviour
{
    [SerializeField] WAVESDispatcher _dispatcher;

    public void OnJumped(Collider ground, Vector3 point, Vector3 normal)
    {
        _dispatcher.PublishJump(ground, point, normal);
    }

    public void OnLanded(ActorFallType fallType, Collider ground, Vector3 point, Vector3 normal)
    {
        _dispatcher.PublishLand(fallType, ground, point, normal);
    }
}
```

Create fall types from **Assets > Create > MadeYellow > WAVES > Fall Type**, or from the gear on **Landing type effects** in the module browser. Pass the type that matches this landing. Pass null to use the fallback landing only.

## In the module browser

Each actor and surface group has two folds.

**Jumping** is one audio and visual effect for leaving that surface.

**Landing** lists every fall type, then **Any**, marked Fallback. If a fall type has no audio of its own, or no visual of its own, the missing part is taken from Any in the same group. A part that the type does set stays its own.

**General Effects Settings** under both folds are the shared playback settings for the group. Audio covers the mixer group, volume, spatial blend, reverb, audible distance, minimum distance, rolloff, and doppler. Visual covers the visible distance. A jump or a landing uses those settings unless **Override settings** is turned on for that part of the slot.
