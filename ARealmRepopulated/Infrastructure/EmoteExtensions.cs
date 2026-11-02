using FFXIVClientStructs.FFXIV.Client.Game.Character;
using FFXIVClientStructs.FFXIV.Client.LayoutEngine.Layer;
using Lumina.Excel.Sheets;
using System.Diagnostics.CodeAnalysis;

namespace ARealmRepopulated.Infrastructure;

public static class EmoteExtensions {

    /// <summary>
    /// There are emotes which have interactions with nearby layout objects, such as sitting on a chair or lying on a bed.
    /// Notably: 
    /// 0xD (Doze -> If a bed is near, prevents execution and instead plays 0x58)
    /// 0x58 (Sleep -> If a bed is near, you lie on it)
    /// 0x32 (Sit -> If a sitable position is near, you sit on it)
    /// </summary>  
    private static readonly Dictionary<uint, EmoteLayoutInteraction> LayoutInteractionOverrides = new() {
        { 0xD, new EmoteLayoutInteraction(0xD, 0x58, ChairMarkerObjectType.Bed) },
        { 0x58, new EmoteLayoutInteraction(0x58, 0x58, ChairMarkerObjectType.Bed) },
        { 0x32, new EmoteLayoutInteraction(0x32, 0x32, ChairMarkerObjectType.Chair) }
    };

    /// <summary>
    /// Emotes that park the actor in one of the game's pose types. Everything not listed here leaves
    /// the actor standing, which is <see cref="PoseType.Idle"/>    
    /// </summary>
    private static readonly Dictionary<uint, PoseType> EmotePoseType = new(){
        { 0xD, PoseType.Doze },
        { 0x58, PoseType.Doze },
        { 0x32, PoseType.Sit },
        { 0x34, PoseType.GroundSit }
    };

    public static PoseType GetPoseType(this Emote emote)
        => EmotePoseType.TryGetValue(emote.RowId, out var poseType) ? poseType : PoseType.Idle;
    
    public static bool TryGetPoseType(this Emote emote, out PoseType poseType)
        => EmotePoseType.TryGetValue(emote.RowId, out poseType);

    // Lumina names these ActionTimeline columns wrong: the sequencer slot is Stance (not Slot), the loop flag is Pause (not IsLoop).
    public static byte GetSlot(this ActionTimeline timeline)
        => timeline.Stance;

    public static bool IsLooping(this ActionTimeline timeline)
        => timeline.Pause;

    public static bool IsLooping(this Emote emote) {
        if (!emote.EmoteMode.IsValid)
            return false;

        var emoteCondition = (CharacterModes)emote.EmoteMode.Value.ConditionMode;
        return emoteCondition == CharacterModes.EmoteLoop || emoteCondition == CharacterModes.InPositionLoop;
    }

    public static bool InteractsWithLayout(this Emote emote)
        => InteractsWithLayout(emote.RowId);

    public static bool InteractsWithLayout(uint emoteId) {
        return LayoutInteractionOverrides.ContainsKey(emoteId);
    }

    public static bool InteractsWithLayout(this Emote emote, [NotNullWhen(true)] out EmoteLayoutInteraction? layoutInteraction) {
        if (LayoutInteractionOverrides.TryGetValue(emote.RowId, out var emoteOverride)) {
            layoutInteraction = emoteOverride;
            return true;
        }

        layoutInteraction = null;
        return false;
    }

    public record EmoteLayoutInteraction(uint OriginalEmoteId, uint LayoutInteractionEmoteId, ChairMarkerObjectType LayoutObjectTarget);
}
