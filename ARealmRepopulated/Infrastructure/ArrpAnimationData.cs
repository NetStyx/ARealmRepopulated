using Dalamud.Plugin.Services;
using Lumina.Excel;
using Lumina.Excel.Sheets;
using System.Text.RegularExpressions;

namespace ARealmRepopulated.Infrastructure;

/// <summary>
/// What an actor can play: emotes, action timelines and pose variants.
/// </summary>
public partial class ArrpAnimationData(IPluginLog log, IDataManager dataManager) {
    private ExcelSheet<Emote> _emoteTypeSheet = null!;
    private ExcelSheet<ActionTimeline> _actionTimelineSheet = null!;
    private Dictionary<PoseType, ushort[]> _poseStateEmotes = [];

    public void Populate() {
        _actionTimelineSheet = dataManager.GetExcelSheet<ActionTimeline>();
        _emoteTypeSheet = dataManager.GetExcelSheet<Emote>();

        _poseStateEmotes = BuildPoseStateEmotes();
    }

    private const uint PoseEmoteCategory = 4;

    /// if its stupid but it works, its not stupid. Maybe someone has found a better way to correlate this but for now, this stays.    
    /// a pose variant animation is named after the pose it belongs to: emote/pose03_loop is the third standing pose, 
    /// emote/j_pose02_loop the second one while sitting on the ground. That holds true unless square decides to change the naming convention.
    [GeneratedRegex(@"^emote/(?:([bsjl])_)?pose(\d+)_loop$", RegexOptions.Compiled)]
    private static partial Regex PoseTimelineRegex();
    [GeneratedRegex(@"^ornament_sp/m(\d+)/onm_pose(\d+)_loop$", RegexOptions.Compiled)]
    private static partial Regex OrnamentPoseTimelineRegex();

    private static readonly Regex PoseTimelineExpression = PoseTimelineRegex();
    private static readonly Regex OrnamentPoseTimelineExpression = OrnamentPoseTimelineRegex();

    private static readonly Dictionary<string, PoseType> PoseTimelinePrefixes = new() {
        { "", PoseType.Idle },
        { "b", PoseType.WeaponDrawn },
        { "s", PoseType.Sit },
        { "j", PoseType.GroundSit },
        { "l", PoseType.Doze }
    };

    private static readonly Dictionary<string, PoseType> OrnamentPoseModels = new() {
        { "6001", PoseType.Umbrella },
        { "6016", PoseType.Accessory }
    };

    public static bool TryParsePoseTimeline(string timelineKey, out PoseType poseType, out int poseIndex) {

        poseType = PoseType.Idle;
        poseIndex = 0;

        if (PoseTimelineExpression.Match(timelineKey) is { Success: true } pose)
            return PoseTimelinePrefixes.TryGetValue(pose.Groups[1].Value, out poseType)
                && int.TryParse(pose.Groups[2].Value, out poseIndex)
                && poseIndex > 0;

        if (OrnamentPoseTimelineExpression.Match(timelineKey) is { Success: true } ornamentPose)
            return OrnamentPoseModels.TryGetValue(ornamentPose.Groups[1].Value, out poseType)
                && int.TryParse(ornamentPose.Groups[2].Value, out poseIndex)
                && poseIndex > 0;

        return false;
    }

    private Dictionary<PoseType, ushort[]> BuildPoseStateEmotes() {

        var poses = new Dictionary<PoseType, SortedDictionary<int, ushort>>();

        foreach (var emote in _emoteTypeSheet) {
            if (emote.EmoteCategory.RowId != PoseEmoteCategory || emote.ActionTimeline.Count == 0)
                continue;

            var loopTimeline = emote.ActionTimeline[0];
            if (!loopTimeline.IsValid || !TryParsePoseTimeline(loopTimeline.Value.Key.ToString(), out var poseType, out var poseIndex))
                continue;

            if (!poses.TryGetValue(poseType, out var variants))
                poses[poseType] = variants = [];

            variants[poseIndex] = (ushort)emote.RowId;
        }

        var poseStateEmotes = new Dictionary<PoseType, ushort[]>();
        foreach (var (poseType, variants) in poses) {
            poseStateEmotes[poseType] = [0, .. variants.Values];
        }

        log.Verbose($"Discovered pose variants: {string.Join(", ", poseStateEmotes.Select(p => $"{p.Key}={p.Value.Length}"))}");

        return poseStateEmotes;
    }

    public int GetPoseStateCount(PoseType poseType)
        => _poseStateEmotes.TryGetValue(poseType, out var emotes) ? emotes.Length : 1;

    public byte ClampPoseState(PoseType poseType, byte poseState)
        => (byte)Math.Clamp(poseState, 0, GetPoseStateCount(poseType) - 1);

    public ushort GetPoseStateEmote(PoseType poseType, byte poseState)
        => _poseStateEmotes.TryGetValue(poseType, out var emotes) && poseState > 0 && poseState < emotes.Length
            ? emotes[poseState]
            : (ushort)0;

    public ushort GetPoseStateEmote(ushort emoteId, byte poseState)
        => GetEmote(emoteId).TryGetPoseType(out var poseType) ? GetPoseStateEmote(poseType, poseState) : (ushort)0;

    public ActionTimeline GetActionTimeline(ushort actionTimelineId)
        => _actionTimelineSheet.GetRow(actionTimelineId);

    public List<ActionTimeline> GetActionTimelines()
        => [.. _actionTimelineSheet];

    public byte GetTimelineSlot(ushort actionTimelineId)
        => _actionTimelineSheet.GetRowOrDefault(actionTimelineId)?.GetSlot() ?? 0;

    public bool IsLoopingTimeline(ushort actionTimelineId)
        => _actionTimelineSheet.GetRowOrDefault(actionTimelineId)?.IsLooping() ?? false;

    public Emote GetEmote(uint emoteId)
        => _emoteTypeSheet.GetRow(emoteId);

    public List<Emote> GetEmotes()
        => [.. _emoteTypeSheet];
}
