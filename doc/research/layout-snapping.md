# Layout snapping

Emotes that interact with layout objects (sit, doze, sleep) put the npc onto a nearby chair or bed. `LayoutWorldService.CheckSnapableLayout` rebuilds the games logic in the plugin.

## What does the game do

The search (`<ResolveSitTarget>`) reads two fields of each layout candidate. ClientStructs does not expose a usable instance that covers them, so to use it we overlay them as `SnapLayoutInstance`:

```csharp
[StructLayout(LayoutKind.Explicit)]
public unsafe struct SnapLayoutInstance {
    [FieldOffset(0x00)] public ILayoutInstance Base;
    [FieldOffset(0x70)] public SnapSideMask AllowedSideMask; // equals to `bVar1 = *(byte *)(candidate + 0x70)`: which sides of the object can be used
    [FieldOffset(0x74)] public byte Type; // `*(char *)(candidate + 0x74) == candidateType`: chair or bed, compared with the requested type
}
```

The same function uses the snap distances `0.42` and `0.75` (`LayoutWorldService.SnapOffsets`). I am unsure what exactly these are, beside them beeing used for resolving distances in relation to the sitting position. For now, we place the actor `0.42` in front of the chosen side, and `NpcActor.PlayEmote` steps back by the same `0.42` when the actor stands up again.

## The first draft: calling the games sit target resolution

The first attempt called the games own resolution instead of rebuilding it. The call chain starts at `EmoteManager.ExecuteEmote`:

```
EmoteManager.ExecuteEmote
  -> <unnamed>(emoteManager, &args)
  -> <unnamed>(emoteManager, &args)
    -> <ResolveSitTarget>(character, inOutLocation, 1, 1, 1, 2.0f)
```

```csharp
[Signature("40 55 53 57 41 54 48 8D AC 24")]
private readonly delegate* unmanaged<Character*, SitTargetLocation*, byte, byte, byte, float, byte> _resolveSitTarget = null!;

[StructLayout(LayoutKind.Explicit, Size = 0x78)]
public unsafe struct SitTargetLocation {
    [FieldOffset(0x00)] public float X;
    [FieldOffset(0x04)] public float Y;
    [FieldOffset(0x08)] public float Z;
    [FieldOffset(0x0C)] public uint Unknown0C;

    [FieldOffset(0x10)] public float Facing;
    [FieldOffset(0x14)] public uint Unknown14;
    [FieldOffset(0x18)] public uint Unknown18;
    [FieldOffset(0x1C)] public uint Unknown1C;

    [FieldOffset(0x20)] public float SnapX;
    [FieldOffset(0x24)] public float SnapY;
    [FieldOffset(0x28)] public float SnapZ;
    [FieldOffset(0x2C)] public uint Unknown2C;

    [FieldOffset(0x30)] public float SnapFacing;
    [FieldOffset(0x34)] public uint Unknown34;

    [FieldOffset(0x38)] public void* TargetObject;
}
```

That seemed promising but did not work out in the end: the function takes the **players** position into account when it picks the nearest snap point, so it cannot resolve a seat for an actor somewhere else.
