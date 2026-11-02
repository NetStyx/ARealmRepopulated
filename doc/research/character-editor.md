# Npcs ... in style (⌐■_■)

Sooo initially, the intended way of using the plugin was to go around the world, gather npc models and use the build in "copy" function to display them at a location of your choice. That was perfectly fine until it wasnt because doing real customization fell short. While `otter` known modding plugins alliviated these issues, it just does not sit quite right with me that i need to point my users away if they want to have such basic functionallity. At the very least my users should have the ability to comfortably cloth their actors. "How hard can it be?" -.-

Every excerpt in this note is cleaned up decompilation of build 2026.10.16. Names in angle brackets are ours, ClientStructs has no name for that code. The comment on top of an excerpt shows the way there from something it does name.

## Equipment, dyes and glasses

Lukily for us, everything needed is already provided by dalamud and the game. Equipable item data in `Item`, `NpcEquip` and `ENpcBase` stand in to source the items which you see on npcs but can not be aquired by players. Whats left is a lot of imgui code and items which have no name or icon .. aaaaaand done. Dyes are found in the `Stain` sheet, application was already possible through the Stain0 / Stain1 properties in EquipmentModelId and WeaponModelId. Glasses have special handling with an additional sheet (drumrolls .... `Glasses`!) but also nothing complicated. I assume they plan to do more with that sort of decoration.

Then came the edge cases.

### Empty slots

Ids 0 and 1 seemed empty but that assumption was plainly wrong. Only a key (Set, Base, Variant) of all zeros is considered empty. `DrawDataContainer.LoadWeapon` creates no weapon for it, and `CharaView.UnequipGear` and `DrawDataContainer.HideHeadgear` write it for gear. For every other key `Human.ResolveMdlPath` and `Weapon.ResolveMdlPath` build a path. A key without a file shows nothing, in fact 2.555 npcs rows put id 1 on the head and 497 on the hands. With that in mind:
- Id 0 loads `e0000`, so id 0 with a variant looks the same as no gear at all.
- Id 1 is real gear. 180 named items use it. It has models for body, legs, feet and accessories.
- `0xFFFFFFFF` only exists in the sheets. `CharacterSetupContainer.SetupEventNpc` turns it into an empty slot, per slot and for both hands as well:

```c
// CharacterSetupContainer.SetupEventNpc(uint npcId)
uint own = npc->Model[slot];
if (own == 0xFFFFFFFF)
  model = 0;
else if (own != 0 || equip == NULL)
  model = { own, npc->Dye[slot], npc->Dye2[slot] };
else if (equip->Model[slot] == 0xFFFFFFFF)
  model = 0;
else
  model = { equip->Model[slot], equip->Dye[slot], equip->Dye2[slot] };
```

So we list a model without an item only if a file exists for it, under any code `Human.SetupIdsAndVfxScale` can build. That hides the keys which lead nowhere and brings back the 40 npc models with id 1 that were hidden before.

### Sephirots Paired fists

Main hands of the sets 1601 to 1650 carry no weapon in their off hand, which is why their `ModelSub` has no variant and no `w88xx` file exists. All 61 items of these sets hold a gear key there, `id | variant << 16`which belong to the gloves:

```c
// DrawDataContainer.LoadWeapon(WeaponSlot slot, WeaponModelId weaponData, ...)
WeaponModelId model = slot == OffHand && mainHand.Id is 1601 to 1650
    ? mainHand + 50            // the whole key, so the dyes of the main hand come along
    : weaponData;
if (model != 0)
  Weapon.Initialize(new Weapon(), model);

// DrawDataContainer.LoadEquipment(EquipmentSlot slot, EquipmentModelId* modelId, char force)
// reads the gear key through the layout of a weapon key
if (slot == Hands && mainHand.Id is 1601 to 1650) {
  model.Id = offHand.Id;
  model.Variant = offHand.Type | offHand.Variant;
  model.Stain0 = (offHand.Type >> 8) | offHand.Stain0;
  model.Stain1 = offHand.Stain1;
}
```

So the dyes of the **off hand** colors the gloves, and the setting for the Handsequipment of the editor has no effect in game while such a weapon is in the main hand.

### Dyes

The dye window is `AgentColorant` with its addon `ColorantColoring` (id `0xA0`). `<RefreshColorTab>` sends the addon one tab at a time which is then filled from the sheet by calling `<FillColorTab>`:

```c
// AgentColorant.Update(uint frameCount)
//   -> <RefreshColorTab>(AgentColorant* this), case 7 of its switch
//     -> <FillColorTab>(AgentColorant* this, AtkValue* values, int tab, int selectedStain, byte currentStain)
int shade = <TabShades>[tab];     // 2, 4, 5, 6, 7, 8, 9, 10
for (int row = 0; row <= 128; row++) {
  Stain* stain = ExdModule.GetStainRow(row);
  if (stain != NULL && stain->Shade == shade && stain->SubOrder is 1 to 129)
    cells[stain->SubOrder - 1] = row;
}
```

That gives 126 cells, by `Shade` and then `SubOrder`. Row 0 stands for "No Color" for the first one of the first tab. Rows 126 to 128 have no sub order. 
Rendering, however, looks at neither the list nor the item:

```c
// CharacterBase.SetupSlotModel(uint slot)
//   -> MaterialResourceHandle.PrepareColorTable(byte stain0Id, byte stain1Id)
//     -> MaterialResourceHandle.ReadStainingTemplate(ushort* stainTable, byte stainId, short* colorTable, uint stainChannel)
// Flags is the first uint of the AdditionalData of the material
if (stain0 != 0 && material->Flags & 8)
  ReadStainingTemplate(material, dyeTable, stain0, colorTable, 0);
if (stain1 != 0 && material->Flags & 8)
  ReadStainingTemplate(material, dyeTable, stain1, colorTable, 1);

// ReadStainingTemplate, for each of the 32 rows of the dye table
if (row.Flags != 0 && ((row.Template >> 11) & 3) == channel)
  // writes the template values of the stain into this row of the color table
```

`CharacterBase.SetupSlotModel` gets both stains from `Human.GetDyeForSlot`, which hands them over unchecked, so a dye shows up on any material whose dye table has rows for that channel. Quick side note: `Item.DyeCount` is only enforced from the players ui, the renderer does not seem to care. 

Over the named body items the materials go beyond it:

| `DyeCount` | Models | With dye table | Rows for channel 1 | Rows for channel 2 |
| --- | --- | --- | --- | --- |
| 0 | 1,163 | 439 | 434 | 131 |
| 1 | 933 | 933 | 933 | 22 |
| 2 | 730 | 730 | 730 | 730 |

Npcs make use of that. On gear, 27 item models with 250 rows wear a dye that renders on channel 1 although their `DyeCount` is 0, and 2 models with 3 rows do the same on channel 2 with a `DyeCount` of 1. So we are simply ignoring `DyeCount` and offer both channels on every model. The worst that can happen is a dye which shows nothing, because the material has no dye table or no rows for that channel.

Equipment handling finished. Yay. Onwards to detailing the character itself.

## The faces we wear

The goal: Be able to edit the npc as if you were modeling it in the character editor. The problem: ... how -_-
Reading the customization and labeling it correctly was easy because it just sits nicely in the customize struct, and the exact colors sit in the `CustomizeParameter` data behind `Human.CustomizeParameterCBuffer`. Mapping those to the guardrails given by the game when you need to actually care for correct boundaries is more convuluted. The games own character editor is a good starting point. A quick look through the sheets reval that `CharaMakeType` exposes almost everything we need through `Menu`s, as the game calls it. It has one row per clan and gender, and its `CharaMakeStruct` lists every menu of the creator. `Customize` is the byte of the customize struct the menu writes to, `SubMenuType` says whether it is a list, icons, a color or a slider, `SubMenuNum` is the number of entries, `InitVal` the default value, and `Menu` points to its label in `Lobby`. 

With this in mind we can read all of this once at startup and keeping a compiled object per clan and gender. The sheet is not entirely honest about what goes where though. Option values sit in `SubMenuGraphic`, except for hairstyles and face paint, which use the `FeatureID` of their `CharaMakeCustomize` row. Facial features and tattoos share one byte as a bitmask, and five on/off switches hide in bit 7 of a byte: highlights, legacy tattoo, small iris, lipstick and the face paint flip. The sheet even points Iris Size at the left eye color, and highlights have no menu at all. All of that came out of sheet dumps and ClientStructs, the code behind the creator menus was never read. For the guardrails, tracing `Human.SetupFromCharacterData` and `Human.UpdateDrawData` did the trick. `Human.SetupFromCharacterData` starts with `CustomizeData.Normalize`, and `Human.UpdateDrawData` gets there as soon as race, sex, body type and the tribe of a Hyur match, otherwise it returns false. That is where the guardrail is applied: it copies the data onto the draw object and repairs whatever is invalid. 

```c
// Human.SetupFromCharacterData(DrawData* data)
//   -> CustomizeData.Normalize(CustomizeData* source)
//     -> <FindInvalidBytes>(CustomizeData* customize)
bool CustomizeData.Normalize(CustomizeData* this, CustomizeData* source) {
  *this = *source;

  if (<FindInvalidBytes>(source) == 0)
    return true;

  // [...]  

  // two clans per race: keeps first or second clan, but moves it into the race
  if (this->Race - 1 != (this->Tribe - 1) >> 1)
    this->Tribe = (this->Race * 2) - (this->Tribe & 1);

  // returns an object with the boundaries for each customize entry
  CustomizeLimits* limits = &<CustomizeLimits>[(((this->Tribe * 2) + this->Sex) * 5) + this->BodyType - 11];

  // [...] 
    
  if ((this->EyeShape & 0x7F) > limits->EyeShape)   // bit 7 is small iris
    this->EyeShape = 0;  
  if ((this->Mouth & 0x7F) > limits->Mouth)         // bit 7 is lipstick
    this->Mouth = 0;

  // [...]

  return false;
}

```
Race, sex, tribe and body type get fixed, muscle tone and bust size stay in range, the tail shape is repaired per race, and a height above 100 becomes 255 (which is the only value above 100 that counts as valid). Eyebrows, eye shape, nose, jaw and mouth are checked against the limits table per clan, gender and body type. Face and hairstyle skip that table, which is how npcs get away with values the creator does not offer. They only get repaired when they make no sense at all: 0 becomes 1, and a Hrothgar woman gets face 5 at least. From that copy, `Human.SetupIdsAndVfxScale` derives the model code and the face, hair and tail ids, `<GetHumanModelScale>` the height (see `/doc/research/model-scale.md`) and `<SetupCustomizeColors>` the colors along with the lipstick, highlights and flip switches, while its caller picks the textures for face paint and the legacy tattoo.

A small caveat: `Normalize` never checks whether a model exists. A young rava passes the checks without complaint and takes the client down when trying to load such a model. So for the body types old and young we only offer what the games own npcs use.

With this setup applied, time to display it - best case scenario in a preview so the users do not need to hit apply all the time. That means that we need to see the changed appearance while editing it. ARRP is strictyl seperating the scenario file from its running instance so altering the npc of the already running scenario directly was out of the question. Not that it would have make much sense either when you want to test appearance its rather inconvenient that the npc runs away from you. So the preview needed an actor of its own, and the obvious question was whether the game already has one for us. Ever right clicked a character and hit examine? Yeah, lets try that.

## The games own preview

Try On, the Character window, the glamour plate editor and the portrait editor all sit on `CharaView`. The windows themselves however are of no use. `Try On` takes items and builds on the local player, so it cannot show someone else, and driving an agent is the kind of thing i want to stay away from.

The `CharaView` itself however is much more interesting. It creates its own battle character in a reserved slot of the client object table (`ClientObjectIndex + 0xF0`, which our actors cannot land on, since automatic slots stop at `0xEF`), and it renders that character off-screen into a texture. The render pass runs for every slot whose counter a `CharaView` camera has raised. So maybe a nicely fitted private instance drawn with `ImGui.Image` was the next idea. Then came the code:

```c
// CharaView.vf7(Character* character)
customize.Height = 50;
characterSetup.SetupPlayer(&customize, &equipment);
```

And back to square one. One call to rule them all. Height is forced to the middle, so neither Height nor our scale would show, which would have been acceptable but `SetupPlayer` passes model id 0 down to `SetupRaw`, so there are no monsters. Exact colors are also not part of the data at all. The base class has an empty `Update`, so a changed appearance means releasing and starting over. And how the game draws the texture stayed open: the view hands it to the agent through `AgentCallbackReady`, and no function ties a shader to it, so a plain `ImGui.Image` might get alpha or gamma wrong. To many things that wont work, so i didnt even try.

So the easiest solution at hand is a copy of what we are already doing: A plain ARRP actor in the world. It costs no new game function at all and it shows monsters, Height, scale and exact colors. What we give up is having the character right next to the sliders. ¯\_(ツ)_/¯

A bit of seperation and cleanup here and there aaaaand it works - now quickly integrate the a few sliders and the character colors and ... oh. oh no.

## Colors, swatches and paletts

Everything in this section was read from build 2026.10.16. Strap in, deep dive into our favorite `chara/xls/charaMake/human.cmp` which, of course, has no header. `CharacterUtility.LoadDataFiles` loads it once into `HumanParameterResourceHandle`, and every reader seemingly uses fixed offsets into it. Because why abastract things nicely away. Who does that, right?

| Offset | Size | Content |
| --- | --- | --- |
| `0x00000` | `0x2C800` | 178 pages of 256 colors each |
| `0x2C800` | `0x1180` | 80 size entries of `0x38` bytes each |

### Finding a color

A color is 4 bytes in the order red, green, blue, alpha. A customize byte is an index into a page, and the color is `colors[(page * 256) + index]`. Body type plays no part in this. Race, tribe and sex only select the clan pages:

```c
// Human.SetupFromCharacterData(DrawData* data)
//   -> <SetupCustomizeBuffers>(Human* this), holds the string _decal_%d.tex
//     -> <SetupCustomizeColors>(CharacterUtility* this, ConstantBuffer* parameterBuffer, ConstantBuffer* decalBuffer, CustomizeData* customize)
clan = ((race - 1) * 4) + (((tribe - 1) & 1) * 2) + sex;   // 0 to 31
```

The file holds every palette twice, once for rendering and once for the swatches of the character creator:

| Color | Customize byte | Rendered page | Index | Creator page | Index | Read by |
| --- | --- | --- | --- | --- | --- | --- |
| Eyes | `EyeColorLeft`, `EyeColorRight` | 0 | value | 9 | value | `AddonCharaMakeFeatureColorFaceEye` vf75 |
| Highlights | `HighlightsColor` | 1 | value | 10 | value | `AddonCharaMakeFeatureColorHair` vf76 |
| Lips | `LipColorFurPattern` | 2 | value | 11 | value | `AddonCharaMakeFeatureColorLip` vf75 |
| Tattoos, limbal ring, ear clasps | `TattooColor` | 3 | value | 12 | value | Unnamed color addon vf75 |
| Face paint | `FacePaintColor` | 4 | value | 13 | value | `AddonCharaMakeFeatureColorFacePaint` vf75 |
| Skin | `SkinColor` | 18 + clan * 5 | value | 21 + clan * 5 | value | Unnamed color addon vf75 |
| Hair, fur | `HairColor` | 19 + clan * 5 | value * 2 | 22 + clan * 5 | value | `AddonCharaMakeFeatureColorHair` vf75 |

- Each clan has 5 pages: skin, 2 pages of hair, the skin swatches and the hair swatches. Hair takes 2 pages because each of its 256 values holds 2 colors. Curious: Only the first one is read with no known reader for the  second. Huh.
- Pages 5 to 8 and 14 to 17 also have no known reader.
- vf75, vf76 and vf77 are the slots at `+0x258`, `+0x260` and `+0x268` of the vtable of an addon.
- The unnamed color addons derives from `AddonCharaMakeFeatureColor`. It picks the page by creator menu: `0xCA` for skin, and `0x416`, `0x6D0`, `0x6D1`, `0x6D4`, `0x6D6` or `0x6DC` for tattoos. Any other menu gets black. It can be found by starting at `AddonCharaMakeFeatureColorLip.ctor` and inspecting the callers for the base constructor where, among the callers, are the functions which create the addons. It allocates `0x368` bytes for face paint, `0x370` for lips and `0x350` for itself.

### Rendered colors

`<SetupCustomizeColors>` fills the `CustomizeParameter` buffer and the decal buffer of a `Human`. It runs from `Human.SetupFromCharacterData` and `Human.UpdateDrawData`, through `<SetupCustomizeBuffers>`, which creates both buffers and picks the face paint and legacy tattoo textures. Some findings:

- Red, green and blue are divided by 255 and squared. Alpha is divided by 255 only.
- Any byte value is valid. Nothing limits a value to what a palette lists. Which is actively used btw, some npcs use values outside of them.
- Without lipstick, `LipColor` stays 0.
- Without highlights, `MeshColor` is `MainColor`.
- Face paint goes into the decal buffer, not into `CustomizeParameter`, and is rounded down to 8 bits per channel.

The W values are mostly not palette data:

| Field | W |
| --- | --- |
| `SkinColor` | Muscle tone for Hyur and Roegadyn, 1.0 for everyone else |
| `LipColor` | Palette alpha, the lip opacity |
| `MainColor` | Face paint U multiplier: 1.0, flipped -1.0 |
| `MeshColor` | Face paint U offset: 0.0, flipped 1.0 |
| `LeftColor` | Au Ra: facial feature bit 6. Everyone else: 0.0 |
| `RightColor` | Au Ra: facial feature bit 5. Everyone else: 0.0 |
| `OptionColor` | Palette alpha, always 1.0 in the file |

cs documents the face paint values on `LeftColor` and `RightColor` which are written to `MainColor` and `MeshColor` in this build.

### Creator swatches

- Swatches are the raw red, green and blue of the creator page. They are not squared, and alpha is forced to `0xFF`.
- A swatch has the same index as its value, including hair, which has no second color on its swatch page.
- `SubMenuNum` in `CharaMakeType` holds the number of swatches: 192 for skin, eyes, hair and tattoos, 96 for lips and face paint. The addons take the length of their list from the menu data they are handed, the field behind that was not traced.
- Highlights have no menu. `AddonCharaMakeFeatureColorHair` shows them next to the hair.
- Lips and face paint have a second palette. Their addons add `0x80` to the cell while it is selected, so it covers the values 128 to 223. It repeats the colors of the first palette. On the rendered pages only the alpha is lower.

Rendered and creator colors are not the same. Eyes are identical, 152 of 192 highlights are, while skin, hair, tattoos, lips and face paint differ in almost every entry, by up to 104 per channel on lips. On the creator pages lips and face paint are identical, on the rendered pages they are not.

### Size entries

The table has 10 entries per race, 2 clans for each of the 5 body types:

```c
// Human.UpdateDrawData(DrawData* data, char skipEquipmentArrays)
//   -> <GetHumanModelScale>(CharacterUtility* this, int tribe, int sex, int bodyType, int height)
//   -> <SetupPostBoneDeformer>(Human* this, byte mask)
//     -> <GetBustScale>(CharacterUtility* this, float3* result, int tribe, int sex, int bodyType, int value)
//     -> <GetMuscleTone>(ResourceHandle* humanParameter, int race, int sex, int secondClan, int bodyType, int value)
entry = ((race - 1) * 10) + ((bodyType - 1) * 2) + ((tribe - 1) & 1);
```

| Offset | Content | Used for |
| --- | --- | --- |
| `+0x00` | Male height min, max | `CharacterBase.ModelScale`, see `/doc/research/model-scale.md` |
| `+0x08` | Male muscle min, max | See below |
| `+0x10` | Female height min, max | `CharacterBase.ModelScale` |
| `+0x18` | Female muscle min, max | See below |
| `+0x20` | Bust min, 3 floats | `PostBoneDeformer + 0x68`, women only |
| `+0x2C` | Bust max, 3 floats | `PostBoneDeformer + 0x68`, women only |

All of them are linear between min and max over a customize byte from 0 to 100. The muscle range is read from `MuscleMass`, and what it does depends on the race. Hyur and Roegadyn get it as `SkinColor.W`, with a range from 0.4 to 2.5. Elezen, Lalafell, Miqote, Au Ra, Hrothgar and Viera get it at `PostBoneDeformer + 0x74`, the byte the creator labels ear size, ear length or tail length for them.

### Where was that from?

With that every part of the character editor sufficently layed out and can be implemented. Bread and butter was extracted from `<SetupCustomizeColors>` 

```c
// our names, the layouts come from the reads further down
struct Color {
  byte R, G, B, A;
};

struct SizeEntry {
  float MaleHeightMin, MaleHeightMax;
  float MaleMuscleMin, MaleMuscleMax;
  float FemaleHeightMin, FemaleHeightMax;
  float FemaleMuscleMin, FemaleMuscleMax;
  float3 BustMin, BustMax;
};

// no function in the game, <SetupCustomizeColors> writes this out inline for every color
float4 <ToShader>(Color color) {
  float r = color.R / 255.0;
  float g = color.G / 255.0;
  float b = color.B / 255.0;
  return { r * r, g * g, b * b, color.A / 255.0 };
}

// Human.SetupFromCharacterData(DrawData* data)
//   -> <SetupCustomizeBuffers>(Human* this), holds the string _decal_%d.tex
//     -> <SetupCustomizeColors>(CharacterUtility* this, ConstantBuffer* parameterBuffer, ConstantBuffer* decalBuffer, CustomizeData* customize)
void <SetupCustomizeColors>(CharacterUtility* this, ConstantBuffer* parameterBuffer,
                            ConstantBuffer* decalBuffer, CustomizeData* customize) {
  Color* colors = this->HumanParameterResourceHandle->GetData();

  int race = customize->Race is 1 to 8 ? customize->Race : 1;
  int sex = customize->Sex < 2 ? customize->Sex : 0;
  int secondClan = (customize->Tribe - 1) & 1;
  int clan = ((race - 1) * 4) + (secondClan * 2) + sex;

  CustomizeParameter* parameter = parameterBuffer->LoadSourcePointer(0, 0x70, 2);
  if (parameter != NULL) {
    *parameter = {};

    parameter->SkinColor = <ToShader>(colors[((18 + (clan * 5)) * 256) + customize->SkinColor]);
    parameter->SkinColor.W = race is Hyur or Roegadyn
        ? <GetMuscleTone>(this->HumanParameterResourceHandle, race, sex, secondClan, customize->BodyType, customize->MuscleMass)
        : 1.0;

    if (customize->Mouth & 0x80)
      parameter->LipColor = <ToShader>(colors[(2 * 256) + customize->LipColorFurPattern]);

    parameter->MainColor = <ToShader>(colors[((19 + (clan * 5)) * 256) + (customize->HairColor * 2)]);
    parameter->MeshColor = customize->Highlights & 0x80
        ? <ToShader>(colors[(1 * 256) + customize->HighlightsColor])
        : parameter->MainColor;

    parameter->LeftColor = <ToShader>(colors[customize->EyeColorLeft]);
    parameter->RightColor = <ToShader>(colors[customize->EyeColorRight]);
    parameter->OptionColor = <ToShader>(colors[(3 * 256) + customize->TattooColor]);

    bool flipped = customize->FacePaint & 0x80;
    parameter->MainColor.W = flipped ? -1.0 : 1.0;
    parameter->MeshColor.W = flipped ? 1.0 : 0.0;

    parameter->LeftColor.W = race == AuRa && customize->FacialFeatures & 0x40 ? 1.0 : 0.0;
    parameter->RightColor.W = race == AuRa && customize->FacialFeatures & 0x20 ? 1.0 : 0.0;
  }

  float4* decal = decalBuffer->LoadSourcePointer(0, 0x10, 2);
  if (decal != NULL)
    *decal = <ToShader>(colors[(4 * 256) + customize->FacePaintColor]);   // rounded down to 8 bits per channel
}

// AddonCharaMakeFeatureColor.OnRefresh()
//   -> <GetSwatch>(AddonCharaMakeFeatureColor* this, Color* result, int cell), vf75
// not one function, every color addon has its own: the swatch of a cell, with alpha forced to 0xFF
Color <GetSwatch>(cell) {
  // unnamed color addon, by menu
  skin:       colors[((21 + (clan * 5)) * 256) + cell];
  tattoos:    colors[(12 * 256) + cell];
  // AddonCharaMakeFeatureColorFaceEye
  eyes:       colors[(9 * 256) + cell];
  // AddonCharaMakeFeatureColorHair, vf76 for the highlights
  hair:       colors[((22 + (clan * 5)) * 256) + cell];
  highlights: colors[(10 * 256) + cell];
  // the cell moves to the second palette first, see <GetLipValue>
  lips:       colors[(11 * 256) + cell];
  face paint: colors[(13 * 256) + cell];
}

// AddonCharaMakeFeatureColor.ReceiveEvent(short eventType, int eventParam, AtkEvent* event, void* data)
//   -> <GetLipValue>(AddonCharaMakeFeatureColorLip* this, int cell), vf77
// from cell to value, 0x100 does not fit the customize byte. Tab is the int at +0x368 of the addon
int <GetLipValue>(AddonCharaMakeFeatureColorLip* this, int cell) {
  if (cell < 0)
    return 0;
  if (this->Tab == 0)
    return cell | 0x100;
  if (this->Tab == 1)
    return (cell + 0x80) | 0x100;
  return cell;
}

// Human.UpdateDrawData(DrawData* data, char skipEquipmentArrays)
//   -> <SetupPostBoneDeformer>(Human* this, byte mask)
//     -> <GetMuscleTone>(ResourceHandle* humanParameter, int race, int sex, int secondClan, int bodyType, int value)
float <GetMuscleTone>(ResourceHandle* humanParameter, int race, int sex, int secondClan, int bodyType, int value) {
  if (value > 100)
    value = 0;
  if (bodyType < 1 || bodyType > 5)
    bodyType = 1;

  SizeEntry* entries = humanParameter->GetData() + 0x2C800;
  SizeEntry* size = &entries[((race - 1) * 10) + ((bodyType - 1) * 2) + secondClan];
  float* range = sex == 0 ? &size->MaleMuscleMin : &size->FemaleMuscleMin;
  return (value / 100.0) * (range[1] - range[0]) + range[0];
}

// Human.UpdateDrawData(DrawData* data, char skipEquipmentArrays)
//   -> <SetupPostBoneDeformer>(Human* this, byte mask)
//     -> <GetBustScale>(CharacterUtility* this, float3* result, int tribe, int sex, int bodyType, int value)
// only reads the file for women, men keep 1.0
float3* <GetBustScale>(CharacterUtility* this, float3* result, int tribe, int sex, int bodyType, int value) {
  if (value > 100)
    value = 0;
  // same entry as <GetMuscleTone>
  *result = size->BustMin + (size->BustMax - size->BustMin) * (value / 100.0);
  return result;
}
```
