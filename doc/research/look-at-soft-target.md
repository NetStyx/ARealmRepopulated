# LookAtContainer and the soft target

NPCs are given the ability to track the players position. The way i want to do it is by setting `Character->SoftTargetId` because this replaces the manual hooks i used before which modified the `LookAtContainer` directely and then hooking the reset method to stop it from invalidating the target. Hooks .. Hooks everywhere.

## Why this text?

After observing some concerning discussions about the soft target and how the game uses it in the dalamud discord, I did a short investigation of the system for my use case.

The path for actors on the client object table appears to be safe. If i am using the field directly (not the setter), then there should be no sideeffects at all and it is enough to let the look-at container do its thing.
The setter would be safe too because the checks in the chain (at its core!) require the actor to be targetable. The setter could take an additional path during duty recorder playback because it also syncs the `TargetSystem` for the replay's perspective character (`TargetSystem+0xA0`; ClientStructs names it `IdleCamTarget`?), and that sync ends in the `TargetSystem` change notification ... which sends the target to the server. If that package is actually send (which would make no sense because its a replay) is unknown to me.

Anyway: our actors will never be the local character and never be targetable. But why risk it when we just can set the field and be done with it.

## The setter

`<IsLocalPlayer>(GameObject* obj, bool respectReplay, bool includeGPoseClone)` decompiled from `(longlong param_1, char param_2, char param_3)`.

```c
void Character::SetSoftTargetId(Character *this, GameObjectId id) {
  if (!<IsLocalPlayer>(this, 1, 0)) {
    if (this->TargetId == id) id = 0xe0000000;
    this->SoftTargetId = id;
    if ((ContentsReplayManager.PlaybackControls & InPlayback) != 0 && *(TargetSystem+0xA0) == this) {
      if (TargetSystem::GetSoftTargetObjectId(&TargetSystem_Instance) != this->SoftTargetId)
        <TargetSystem_SetSoftTargetById>(&TargetSystem_Instance);
    }
  }
}
```

## Readers

### Character.GetSoftTargetId

Only the local player answers from `TargetSystem`, anyone else returns the field. `<IsLocalPlayer>` compares against `Control.LocalPlayer` .. lucky us.

```c
GameObjectId Character::GetSoftTargetId(Character *this) {
  if (<IsLocalPlayer>(this, 1, 0))
    return TargetSystem::GetSoftTargetObjectId(&TargetSystem_Instance);
  return this->SoftTargetId;
}
```

### LookAtContainer.UpdateLookAt

Run each frame from `GameObjectManager.UpdateLookAt`. Its target lookup only asks `TargetSystem` for the local player; NPCs of type PC (1) and BattleNpc (2) use the characters own ids, and everything else falls through to the behaviour container, which our actors never fill.

```c
GameObjectId <LookAtContainer_ResolveTarget>(LookAtContainer *this) {
  if (!<IsLocalPlayer>(this->Owner, 1, 0)) {
    if (this->Owner->GetObjectKind() == 1) {
      if (!<lookAtSuppressed>(this->Owner)) {
        id = Character::GetSoftTargetId(this->Owner);
        if (id != 0xe0000000) return id;
        return Character::GetTargetId(this->Owner);
      }
    } else if (this->Owner->GetObjectKind() == 2) {      
      id = Character::GetSoftTargetId(this->Owner);
      if (id != 0xe0000000) return id;
      id = Character::GetTargetId(this->Owner);
      if (id != 0xe0000000) return id;
    }
  } else if (!<lookAtSuppressed>(this->Owner)) {
    return TargetSystem::GetTargetObjectId2(&TargetSystem_Instance);
  }
  return <BehaviourContainer_GetTarget>(this->Owner + 0x1cf0);
}
```

`UpdateLookAt` then resolves that id against the local object table and hands it to the characters own `CharacterLookAtController` as a target param, which then drives the head tracking. Nothing on that path sends or queues a packet.

```c
target = GameObjectManager::ObjectArrays::GetObjectByGameObjectId(&objects, id);
param.vtbl = &CharacterLookAtTargetParam;
param.Type = 1;
param.TargetId = target->GetGameObjectId();
<CharacterLookAtController_SetParam>(&this->Controller, &param, slot, 0);
```

### UI3DModule.UpdateGameObjects

Reads only the soft target of the local player, or of the duty recorder perspective character (`TargetSystem+0xA0`).

```c
player = <GetLocalOrReplayPerspectiveCharacter>(1);
softTarget = Character::GetSoftTargetId(player);
```

### TargetSystem

Only in duty recorder mode (target mode 5), selecting a character makes it the replays perspective character, and the game takes over that character's `TargetId` and `SoftTargetId` as the
`TargetSystem` hard and soft target. Normal targeting never reads a characters `SoftTargetId`. Both setters can end in the target change notification that sends the target to the server, so this is the only reader that is somewhat network adjacent.

```c
if (<CurrentTargetMode>(this) == 5 /* ContentsReplay */) {
  if (*(this+0xA0) != selected && <CanSelect>(...)) {
    *(this+0xA0) = selected;
    chara = GameObject::GetAsCharacter(selected);
    <TargetSystem_SetHardTargetById>(this, Character::GetTargetId(chara), 1);
    <TargetSystem_SetSoftTargetById>(this, Character::GetSoftTargetId(chara));
    ...
  }
}
```

It is the only writer of `TargetSystem+0xA0` - that is also the part that would require targetable characters to switch perspektive and execute the 'dangerous' path.

Ah, and honorable mention to `CharacterSetupContainer.CopyFromCharacter`, which probably also copies the targets.
