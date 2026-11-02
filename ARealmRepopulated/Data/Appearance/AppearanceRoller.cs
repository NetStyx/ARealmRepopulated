namespace ARealmRepopulated.Data.Appearance;

[Flags]
public enum AppearanceRollLocks {
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
public static class AppearanceRoller {

    /// <summary>
    /// Hair, face and body values are all tied to the clan they were made for, so keeping one of them keeps the clan as well.
    /// </summary>
    public static bool KeepsClan(AppearanceRollLocks locks)
        => (locks & (AppearanceRollLocks.Clan | AppearanceRollLocks.Hair | AppearanceRollLocks.Face | AppearanceRollLocks.Body)) != 0;

    /// <summary>
    /// Returns null when no npc of the clan exists.
    /// </summary>
    public static NpcAppearanceData? Roll(NpcAppearanceData current, IReadOnlyList<NpcAppearanceData> people, IReadOnlyList<NpcAppearanceData> everyone, AppearanceRollLocks locks) {

        if (people.Count == 0)
            return null;

        var keepsClan = KeepsClan(locks);
        var seed = keepsClan ? current : Pick(people);
        var relatives = everyone.Where(p => IsSameClan(p, seed)).ToList();
        if (relatives.Count == 0)
            return null;

        var outfits = people.Where(p => p.Sex == seed.Sex).ToList();
        if (outfits.Count == 0)
            outfits = relatives;

        var rolled = current.Copy();

        rolled.AppearanceId = Guid.NewGuid();
        rolled.BodyType = NpcBodyType.Normal;

        if (!keepsClan) {
            rolled.ModelCharaId = seed.ModelCharaId;
            rolled.ModelSkeletonId = seed.ModelSkeletonId;
            rolled.Race = seed.Race;
            rolled.Tribe = seed.Tribe;
            rolled.Sex = seed.Sex;
            rolled.ExtendedAppearance = null;
            rolled.Voice = null;
        } else if (current.BodyType != NpcBodyType.Normal) {
            rolled.ResetModel();
        }

        if (!locks.HasFlag(AppearanceRollLocks.Hair)) {
            var donor = Pick(relatives);
            rolled.HairStyle = donor.HairStyle;
            rolled.Highlights = donor.Highlights;
            rolled.HairColor = donor.HairColor;
            rolled.HighlightsColor = donor.HighlightsColor;
            ForgetColors(rolled, CustomizeIndex.HairColor, CustomizeIndex.HighlightsColor);
        }

        if (!locks.HasFlag(AppearanceRollLocks.Face)) {
            var donor = Pick(relatives);
            rolled.Face = donor.Face;
            rolled.FacialFeatures = donor.FacialFeatures;
            rolled.TattooColor = donor.TattooColor;
            rolled.Eyebrows = donor.Eyebrows;
            rolled.EyeColorLeft = donor.EyeColorLeft;
            rolled.EyeColorRight = donor.EyeColorRight;
            rolled.EyeShape = donor.EyeShape;
            rolled.Nose = donor.Nose;
            rolled.Jaw = donor.Jaw;
            rolled.Mouth = donor.Mouth;
            rolled.LipColorFurPattern = donor.LipColorFurPattern;
            rolled.FacePaint = donor.FacePaint;
            rolled.FacePaintColor = donor.FacePaintColor;
            ForgetColors(rolled, CustomizeIndex.FacialFeaturesColor, CustomizeIndex.EyeColorLeft, CustomizeIndex.EyeColorRight, CustomizeIndex.LipColorFurPattern);
        }

        if (!locks.HasFlag(AppearanceRollLocks.Body)) {
            var donor = Pick(relatives);
            rolled.Height = donor.Height;
            rolled.SkinColor = donor.SkinColor;
            rolled.MuscleMass = donor.MuscleMass;
            rolled.TailShape = donor.TailShape;
            rolled.BustSize = donor.BustSize;
            ForgetColors(rolled, CustomizeIndex.SkinColor, CustomizeIndex.MuscleMass);
        }

        if (!locks.HasFlag(AppearanceRollLocks.Outfit)) {
            ApplyOutfit(rolled, Pick(outfits));
        }

        return rolled;
    }

    private static void ApplyOutfit(NpcAppearanceData rolled, NpcAppearanceData donor) {
        rolled.MainHand = null;
        rolled.OffHand = null;
        rolled.HideWeapons = true;

        rolled.HeadGear = donor.HeadGear;
        rolled.Body = donor.Body;
        rolled.Hands = donor.Hands;
        rolled.Legs = donor.Legs;
        rolled.Feet = donor.Feet;
        rolled.Ears = donor.Ears;
        rolled.Neck = donor.Neck;
        rolled.Wrists = donor.Wrists;
        rolled.LeftRing = donor.LeftRing;
        rolled.RightRing = donor.RightRing;
        rolled.Glasses = donor.Glasses;
        rolled.HideHeadgear = donor.HideHeadgear;
        rolled.CloseVisor = donor.CloseVisor;
    }

    private static void ForgetColors(NpcAppearanceData appearance, params CustomizeIndex[] indices) {
        foreach (var index in indices) {
            appearance.ExtendedAppearance?.Forget(index);
        }
    }

    private static bool IsSameClan(NpcAppearanceData left, NpcAppearanceData right)
        => left.Tribe == right.Tribe && left.Sex == right.Sex;

    private static NpcAppearanceData Pick(IReadOnlyList<NpcAppearanceData> people)
        => people[Random.Shared.Next(people.Count)];
}
