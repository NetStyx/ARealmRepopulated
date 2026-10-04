# Model scale and the customize Height

Our actors used to carry two size sliders: the actor scale and a "height multiplier". The multiplier came from .chara files, which store the draw objects model scale. To my shame, i did not verify if the value is the output or the input for the calculation, so i wrote it straight into `CharacterBase.ModelScale`, and that turned out to replace a value the game computes itself. As a side effect, the Height setting of an actors appearance did nothing at all.

## Where the model scale comes from

A human draw object gets its model scale in two places, `Human.SetupFromCharacterData` when it is created and `Human.UpdateDrawData` when its customize data changes. Both look it up from the customize data:

```c
// Human.SetupFromCharacterData, Human.UpdateDrawData
this->CharacterBase.ModelScale =
    <GetHumanModelScale>(CharacterUtility.Instance, Customize.Tribe, Customize.Sex, Customize.BodyType, Customize.Height);
```

Every other model type keeps 1f. The lookup reads `chara/xls/charaMake/human.cmp`, which holds a size range for each race, body type and clan, with one min/max pair for males and one for females:

```c
float <GetHumanModelScale>(CharacterUtility* this, byte tribe, byte sex, byte bodyType, byte height) {
  uint race = (tribe - 1) >> 1;            // two tribes per race
  if (race >= 8) race = 0;
  int body = (bodyType - 1 < 5) ? bodyType : 1;
  if (height > 100) {
    if (height == 0xFF) return 1.0;
    height = 0;                             // everything else above 100 is the minimum
  }
  float* range = cmpData + (sex & 1 ? 0x2C810 : 0x2C800)
               + (race * 10 + (body - 1) * 2 + ((tribe - 1) & 1)) * 0x38;
  return range[0] + (range[1] - range[0]) * (height / 100.0);
}
```

8 races × 10 entries × 0x38 bytes from `0x2C800`. Putting in example values give the expected resuts: Highlanders are taller than Midlanders, and young Miqote and AuRa are much smaller.

## Why folding into the actor scale is exact

`GameObject.UpdateVisualScale` copies `GameObject.Scale` into the draw object's `Object.Scale`, and the skeleton scale multiplies them:

```c
// CharacterBase vf98
skeletonScale = Object.Scale * ModelScale * <a third factor i did not bother to check>;
```

A factor in `GameObject.Scale` therefore looks exactly like the same factor in `ModelScale`. To fix it correctly, we no longer write `ModelScale` directly and the game puts the Height-derived value back. Anything that wants to replace it, like an old scenario's height multiplier or a .chara file `HeightMultiplier`, is folded into the actor scale as `Scale * multiplier / gameValue`. `HumanHeightTable` reproduces the lookup in managed code from the same file. That is static game data, so it needs no memory read and no hook, but will most likely break with the additons of Evercold. But everything else breaks anyway so - whatever.