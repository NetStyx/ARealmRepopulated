# Nameplates

Someone wanted to have their actors present their name on top of the model. Welp.. cant hide from Atk* stuff forever. 
To stay with the "do NOT make your actors targetable" doctrine and still show something resembling a nameplate, we show actor names with custom text nodes inside the games `NamePlate` addon and copy the games placement. Huzzah! Weil muss ja und so.

## Why not the games own plates

- `UI3DModule.UpdateGameObjects` only lists characters it could target
- Other way in is by setting npc sub kind to 6 (`RaceChocobo`) or 8, which would make other systems treat our actors as something else.
- A game plate is a click target. Hovering it goes through `RaptureAtkModule.HandleNameplateHover` into `TargetSystem.SetMouseOverEventObj`.
- All terrible choices for what we do.

```c
// <IsNamePlateCandidate>(GameObject* obj), the gate in UI3DModule.UpdateGameObjects
if (((obj->SubKind - 6) & 0xFD) != 0 && obj->BaseId != 0x1E9820 && obj->BaseId != 0x1E9821 && !obj->vf74())
    return obj->GetIsTargetable() && obj->vf43();   // vf74 needs GetIsTargetable as well
return true;
```

## Placing the plates on screen

- `AtkUnitBase.Update` computes the screen transforms of every node which seems to be somewhat where we want to be as well, so we place in `PreUpdate`.
- Doing it in `PostUpdate` or `PreDraw` and the label shows up a frame late and trails the camera. Visually interesting to watch - still wrong.

```c
void AtkUnitBase::Update(AtkUnitBase* this, float delta) {
    UldManager.Update();
    UldManager.UpdateFromParentNode(RootNode, RootNode->ParentNode, 1);
}
```

## Anchor

- The game fills `NameplateOffset` for our actors by itself.
- `NameplateOffsetScaleMultiplier` comes from `SetupRaw`, which our actors never run, because that resulted in game crashes when placing the actors in water.

```c
Vector3* GameObject::GetNamePlateWorldPosition(GameObject* this, Vector3* out) {
    *out = this->Position;
    if (this->GimmickId == 0 && (this->Flags9C & 4) == 0)
        *out += <RotateY>(this->NameplateOffset, this->RotationOffset + this->Rotation);
    out->Y += this->NameplateOffsetScaleMultiplier * this->Scale;   // SetupRaw: ModelChara.Unknown6 * 0.1
}
```

## Emiyas Projection Magic

- `UI3DModule.CalculateIsInScreen` keeps x and y in -1..1 and depth in 0..1.
- `TargetSystem.IsObjectOnScreen` also requires W > 0. Without it, points behind the camera pass.
- The cs helper `Camera.WorldToScreen` divides by abs(W) and mirrors those points back onto the screen.
- The game never reads the last column of the view matrix. A full 4×4 product with the stored values leaves the depth at 0.

```c
Vector4* Camera::WorldToViewportPoint(Camera* this, Vector4* out, Vector3* world) {
    matrix = <MultiplyAffine>(&SceneCamera.ViewMatrix, &SceneCamera.RenderCamera->ProjectionMatrix);
    clip = <Transform>(Vector4(*world, 1), &matrix);
    *out = Vector4(clip.X / clip.W, clip.Y / clip.W, clip.Z / clip.W, 0);
}
```

## Platesize, fade distance and depth

- The distance is the squared ground distance (x, z, without y) from object to the player.
- `UI3DModule.UpdateGameObjects` rebuilds its list every frame and stops at 50 plates which it holds in its own array.
- Headache
- We can ignore it! The array the game uses is filled using objects from the client object table, starting with the player, his target and the soft target and then anything else that is targetable. Which means we are golden.


```c
int UI3DModule::CalculateNamePlateScale(float distanceSquared) {
    if (distanceSquared > 2500) return 0;   // 50 yalms
    if (distanceSquared > 2304) return (int)(50 - (distanceSquared - 2304) / 196 * 50);
    if (distanceSquared < 25) return 100;
    return (int)(100 - (distanceSquared - 25) / 2279 * 50);
}

// AddonNamePlate::OnRequestedUpdate, per plate
s = data->Scale * 0.01;
SetAlpha(plate, s * 255);
SetScale(plate, max(s, 0.05) * GetGlobalUIScale() * GetScale() * numbers->NamePlateSize / 100);
SetPositionShort(plate, data->X - width / 2, data->Y - height);
SetDepth(plate, data->Depth);                                // viewport depth
SetUseDepthBasedPriority(plate, (data->DrawFlags >> 3) & 1); // cleared only for the players target

// AtkUldManager::UpdateFromParentNode
node->Depth_2 = node->Depth + parent->Depth_2;
```
