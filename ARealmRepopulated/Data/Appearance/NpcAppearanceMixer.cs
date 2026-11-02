namespace ARealmRepopulated.Data.Appearance;

[Flags]
public enum AppearanceMixLocks {
    None = 0,
    Clan = 1 << 0,
    Hair = 1 << 1,
    Face = 1 << 2,
    Body = 1 << 3,
    Outfit = 1 << 4
}

/// <summary>
/// Builds a new character appearance out of existing ones.
/// </summary>
public static class NpcAppearanceMixer {

    /// <summary>
    /// Hair, face and body values are all tied to the clan they were made for so keeping one of them keeps the clan as well.
    /// </summary>
    public static bool KeepsClan(AppearanceMixLocks locks)
        => (locks & (AppearanceMixLocks.Clan | AppearanceMixLocks.Hair | AppearanceMixLocks.Face | AppearanceMixLocks.Body)) != 0;

    public static NpcAppearanceData? Mix(NpcAppearanceData current, IReadOnlyList<NpcAppearanceData> people, IReadOnlyList<NpcAppearanceData> everyone, AppearanceMixLocks locks) {

        if (people.Count == 0)
            return null;

        var keepsClan = KeepsClan(locks);
        var seed = keepsClan ? current : PickRandomAppearance(people);
        var relatives = everyone.Where(p => IsSameClan(p, seed)).ToList();
        if (relatives.Count == 0)
            return null;

        var outfits = people.Where(p => p.Sex == seed.Sex).ToList();
        if (outfits.Count == 0)
            outfits = relatives;

        var mixed = current.Copy();

        mixed.AppearanceId = Guid.NewGuid();
        mixed.BodyType = NpcBodyType.Normal;

        if (!keepsClan) {
            mixed.ModelCharaId = seed.ModelCharaId;
            mixed.ModelSkeletonId = seed.ModelSkeletonId;
            mixed.Race = seed.Race;
            mixed.Tribe = seed.Tribe;
            mixed.Sex = seed.Sex;
            mixed.ExtendedAppearance = null;
            mixed.Voice = null;
        } else if (current.BodyType != NpcBodyType.Normal) {
            mixed.ResetModel();
        }

        if (!locks.HasFlag(AppearanceMixLocks.Hair)) {
            var donor = PickRandomAppearance(relatives);
            mixed.HairStyle = donor.HairStyle;
            mixed.Highlights = donor.Highlights;
            mixed.HairColor = donor.HairColor;
            mixed.HighlightsColor = donor.HighlightsColor;
            ForgetColors(mixed, CustomizeIndex.HairColor, CustomizeIndex.HighlightsColor);
        }

        if (!locks.HasFlag(AppearanceMixLocks.Face)) {
            var donor = PickRandomAppearance(relatives);
            mixed.Face = donor.Face;
            mixed.FacialFeatures = donor.FacialFeatures;
            mixed.TattooColor = donor.TattooColor;
            mixed.Eyebrows = donor.Eyebrows;
            mixed.EyeColorLeft = donor.EyeColorLeft;
            mixed.EyeColorRight = donor.EyeColorRight;
            mixed.EyeShape = donor.EyeShape;
            mixed.Nose = donor.Nose;
            mixed.Jaw = donor.Jaw;
            mixed.Mouth = donor.Mouth;
            mixed.LipColorFurPattern = donor.LipColorFurPattern;
            mixed.FacePaint = donor.FacePaint;
            mixed.FacePaintColor = donor.FacePaintColor;
            ForgetColors(mixed, CustomizeIndex.FacialFeaturesColor, CustomizeIndex.EyeColorLeft, CustomizeIndex.EyeColorRight, CustomizeIndex.LipColorFurPattern);
        }

        if (!locks.HasFlag(AppearanceMixLocks.Body)) {
            var donor = PickRandomAppearance(relatives);
            mixed.Height = donor.Height;
            mixed.SkinColor = donor.SkinColor;
            mixed.MuscleMass = donor.MuscleMass;
            mixed.TailShape = donor.TailShape;
            mixed.BustSize = donor.BustSize;
            ForgetColors(mixed, CustomizeIndex.SkinColor, CustomizeIndex.MuscleMass);
        }

        if (!locks.HasFlag(AppearanceMixLocks.Outfit)) {
            ApplyOutfit(mixed, PickRandomAppearance(outfits));
        }

        return mixed;
    }

    private static void ApplyOutfit(NpcAppearanceData mixed, NpcAppearanceData donor) {
        mixed.MainHand = null;
        mixed.OffHand = null;
        mixed.HideWeapons = true;

        mixed.HeadGear = donor.HeadGear;
        mixed.Body = donor.Body;
        mixed.Hands = donor.Hands;
        mixed.Legs = donor.Legs;
        mixed.Feet = donor.Feet;
        mixed.Ears = donor.Ears;
        mixed.Neck = donor.Neck;
        mixed.Wrists = donor.Wrists;
        mixed.LeftRing = donor.LeftRing;
        mixed.RightRing = donor.RightRing;
        mixed.Glasses = donor.Glasses;
        mixed.HideHeadgear = donor.HideHeadgear;
        mixed.CloseVisor = donor.CloseVisor;
    }

    private static void ForgetColors(NpcAppearanceData appearance, params CustomizeIndex[] indices) {
        foreach (var index in indices) {
            appearance.ExtendedAppearance?.Forget(index);
        }
    }

    private static bool IsSameClan(NpcAppearanceData left, NpcAppearanceData right)
        => left.Tribe == right.Tribe && left.Sex == right.Sex;

    private static NpcAppearanceData PickRandomAppearance(IReadOnlyList<NpcAppearanceData> people)
        => people[Random.Shared.Next(people.Count)];
}
