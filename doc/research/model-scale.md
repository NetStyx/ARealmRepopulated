# Model scale and the customize Height

Our actors used to carry two size sliders: the actor scale and a "height multiplier". The multiplier came from .chara files, which store the draw objects model scale. I assumed at that time that this would be an variable in the calculation and not the result, so i wrote it straight into `CharacterBase.ModelScale`, and that turned out to replace a value the game computes itself. As a side effect, the Height setting of an actors appearance did nothing at all.

## Human heights

A human draw object gets its model scale in two places, `Human.SetupFromCharacterData` when it is created and `Human.UpdateDrawData` when its customize data changes. Both look it up from the customize data:

```c
// Human.SetupFromCharacterData, Human.UpdateDrawData
this->CharacterBase.ModelScale =
    <GetHumanModelScale>(CharacterUtility.Instance, Customize.Tribe, Customize.Sex, Customize.BodyType, Customize.Height);
```

Every other model type keeps 1f. The lookup reads `chara/xls/charaMake/human.cmp`, which contains a size range for each race, body type and clan, with one min/max pair for males and one for females:

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

8 races × 10 entries × 0x38 bytes from `0x2C800`

## Calculate the scaling

`GameObject.UpdateVisualScale` copies `GameObject.Scale` into the draw objects `Object.Scale`, and the skeleton scale multiplies them:

```c
// CharacterBase vf98
skeletonScale = Object.Scale * ModelScale * <a third factor i did not bother to check>;
```

So the goal is to migrate the existing height modifier to the model scale and on import we extract the correct value by reverse calculating from the given .chara value and the human.cmp table by reproducing the lookup in managed code. Thats static game data and will most likely break with the additons of Evercold. But everything else breaks anyway so - whatever.

The first take kept the stored Height and folded `multiplier / model scale` into the actor scale. The size seemed right, but i stored the height with zero and an .. "odd" actor scale, both of them beyond use. Since the lookup is a plain lerp, it can be reversed (`(multiplier - min) / (max - min) * 100`), rounded and clamped between 0 and 100. Happy values, happy game. It can then calculate as it is used to and we can scale the actor with the scaling slider.
