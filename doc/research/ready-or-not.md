# RenderFlags, TargetableFlags ... and Sounds?

Why the dive? Because of a bug of course. If the first action that an actor plays is an emote with a voice line attached to it (e.g. `/chuckle`) it can (and most likely will) happen that that emote is silent. Subsequent runs of the scenario, or even the same emote repeated, are working correctly. The why was easy: probably because its not finished loading. But what says an actor is "ready" then?

## The sound gate

`VfxContainer.LoadCharacterSound` asks its owner very early whether it is drawn at all, and returns without a sound when it is not. That question is modeled as a single mask over `GameObject.RenderFlags`:

```c
// VfxContainer.LoadCharacterSound
if (!OwnerObject->vf43()) return 0;

// Character.vf43
if (GetObjectKind() == Mount) { /* asks the mount owner instead */ }
if (BattleNpcSubKind == RaceChocobo)
  return (RenderFlags & ~0x280008) == 0;
return (RenderFlags & ~0x8) == 0;
```

Up until now our `NpcActor.IsReady` only waited for the idle timeline, which can start before the flags are clear. 

## What are RenderFlags even?

The field is not a state but a set of "reasons", one bit for each. Two vfs on `GameObject` add and remove a reason:

```c
bool GameObject::<ShowForReason>(int reason, bool immediate) {   // vf39
  var old = RenderFlags;
  RenderFlags &= ~(1 << reason);
  if (old != 0 && RenderFlags == 0) { <AddToScene>(); return true; }
  return false;
}

bool GameObject::<HideForReason>(int reason, bool immediate) {   // vf41
  var old = RenderFlags;
  RenderFlags |= 1 << reason;
  if (old == 0 && RenderFlags != 0) { <RemoveFromScene>(); return true; }
  return false;
}
```

ClientStructs, for the 2026.10.06.0000.0000 client at the time of writing, has only two bits defined (`Model` = 1, `Nameplate` = 11). The ones that matter for us:

- Bit 3 (0x8) is a "hidden by the camera" marker. The camera checks around 20 objects per frame with `CameraBase.ShouldDrawGameObject`, which says no when the camera sits inside the radius of the object. Models flagged in `ModelChara` are exempt.
- Bit 8 (0x100) controls event visibility. Its controlled by `GameObject.vf38` through the event handlers of the object (= event objects that are only there parts of the time are controlled by this).
- Bit 11 (0x800) means something like "visuals not loaded". Its removed once the visuals are, in fact, loaded. Who would have guessed?

`GameObject.Initialize` starts every object at `RenderFlags = 0x900`, so bits 8 and 11 are what a fresh actor has to get rid of.

## So what about the sound gate now?

That check (presumably! not entirely sure...) in there is for making sure that if the camera clipped into a character, even though it should not be rendered, it should still be heard. While that also would be a candidate for providing the new IsReady check ... tying readiness to the camera feels wrong. Also, every game has some form of "MySetupIsFinishedIAmReadyNowLoveMe" flag somewhere. But where? `GameObject.GetIsTargetable` seems to copy that logic by checking the mask `~0x1808` which ignores bits 3, 11 and 12, and on top of that requires `TargetableStatus & 0x08`, so maybe the TargetableStatus has more for us then the ability to target a character?

## Loading an object

`GameObject.Update` runs every frame and walks the actor through its setup. Next to `RenderFlags` every finished step marks itself in `TargetableStatus`, of which ClientStructs only names `IsTargetable` (0x02) and `ReadyToDraw` (0x40):

```c
// GameObject.Update
if ((TargetableStatus & ReadyToDraw) == 0 && this->vf63())
  TargetableStatus |= ReadyToDraw;
if ((TargetableStatus & 0x10) == 0 && (TargetableStatus & ReadyToDraw) != 0 && <EventHandlersReady>())
  TargetableStatus |= 0x10;
if ((TargetableStatus & 0x20) == 0 && this->vf33()) {   // Character: model, equipment and attached objects loaded
  TargetableStatus |= 0x20;
  this->vf32();
}
if ((TargetableStatus & 0x08) == 0 && (TargetableStatus & 0x70) == 0x70 && this->vf33() && <EventHandlersReady>()) {
  TargetableStatus |= 0x08;
  this->vf31();
}
...
this->vf38();                                           // event visibility is checked every frame

// GameObject.vf32, visuals loaded
UpdateVisualPosition(); UpdateVisualRotation(); UpdateVisualScale();
<ShowForReason>(11, ...);
field_0x9B |= 0x8;                                      // from now on vf38 can flip bit 8

// GameObject.vf31, setup completed
field_0x9B |= 0x8;
this->vf38();                                           // decides bit 8 right away
// Character.vf31 then applies the base pose of the character

// GameObject.vf38
if ((field_0x9B & 0x8) == 0) return;
if (!(no zone connection || LocalPlayer != null)) return;
if ((TargetableStatus & 0x10) == 0) return;
if (layout instance != null && !layout instance->IsPrimaryReady()) return;
... <ShowForReason>(8) or <HideForReason>(8)
```

One catch: 0x08 is set only once per object. `DisableDraw` destroys the draw object, hides for reason 11 again and clears 0x20 (and 0x80), but keeps 0x08. After a despawn and spawn of the same actor, 0x08 alone would report ready while the model is still loading again. 0x20 only comes back once the new draw object has loaded, so we need both.

## Are we there yet?

Waiting for an actor to settle into its idle timeline and for both `TargetableStatus` bits (0x08 [setup completed] and 0x20 [visuals loaded]) to be set fixes the issue and gives a pretty good status about the readiness of an actor. By then bits 11 and 8 of `RenderFlags` are cleared ( as long as it gets past the checks for vf38 - which needs a local player to be available and the owner of the object to be visible), which solves the sound gate issue as well.
