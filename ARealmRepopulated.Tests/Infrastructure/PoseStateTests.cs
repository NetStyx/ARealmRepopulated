using ARealmRepopulated.Infrastructure;
using Shouldly;
using PoseType = FFXIVClientStructs.FFXIV.Client.Game.Control.EmoteController.PoseType;

namespace ARealmRepopulated.Tests.Infrastructure;

public class PoseStateTests {

    #region Body Pose Timelines

    [Theory]
    [InlineData("emote/pose01_loop", PoseType.Idle, 1, TestDisplayName = "StandingPose01")]
    [InlineData("emote/pose06_loop", PoseType.Idle, 6, TestDisplayName = "StandingPose06")]
    [InlineData("emote/b_pose01_loop", PoseType.WeaponDrawn, 1, TestDisplayName = "WeaponDrawnPose01")]
    [InlineData("emote/s_pose04_loop", PoseType.Sit, 4, TestDisplayName = "ChairPose04")]
    [InlineData("emote/j_pose03_loop", PoseType.GroundSit, 3, TestDisplayName = "GroundSitPose03")]
    [InlineData("emote/l_pose02_loop", PoseType.Doze, 2, TestDisplayName = "DozePose02")]
    public void TryParsePoseTimeline_BodyPose_ReturnsPoseTypeAndIndex(string timelineKey, PoseType expectedPoseType, int expectedIndex) {
        ArrpDataCache.TryParsePoseTimeline(timelineKey, out var poseType, out var poseIndex).ShouldBeTrue();
        poseType.ShouldBe(expectedPoseType);
        poseIndex.ShouldBe(expectedIndex);
    }

    #endregion

    #region Ornament Pose Timelines

    [Theory]
    [InlineData("ornament_sp/m6001/onm_pose01_loop", PoseType.Umbrella, 1, TestDisplayName = "UmbrellaPose01")]
    [InlineData("ornament_sp/m6001/onm_pose03_loop", PoseType.Umbrella, 3, TestDisplayName = "UmbrellaPose03")]
    [InlineData("ornament_sp/m6016/onm_pose01_loop", PoseType.Accessory, 1, TestDisplayName = "AccessoryPose01")]
    public void TryParsePoseTimeline_OrnamentPose_ReturnsPoseTypeAndIndex(string timelineKey, PoseType expectedPoseType, int expectedIndex) {
        ArrpDataCache.TryParsePoseTimeline(timelineKey, out var poseType, out var poseIndex).ShouldBeTrue();
        poseType.ShouldBe(expectedPoseType);
        poseIndex.ShouldBe(expectedIndex);
    }

    [Fact]
    public void TryParsePoseTimeline_UnknownOrnamentModel_ReturnsFalse() {
        ArrpDataCache.TryParsePoseTimeline("ornament_sp/m9999/onm_pose01_loop", out _, out _).ShouldBeFalse();
    }

    #endregion

    #region Non Pose Timelines

    [Theory]
    [InlineData("", TestDisplayName = "Empty")]
    [InlineData("normal/idle", TestDisplayName = "DefaultIdle")]
    [InlineData("battle/idle", TestDisplayName = "DefaultBattleIdle")]
    [InlineData("emote/pose01_start", TestDisplayName = "PoseTransitionIsNotTheLoop")]
    [InlineData("emote/x_pose01_loop", TestDisplayName = "UnknownPosePrefix")]
    [InlineData("emote/pose00_loop", TestDisplayName = "DefaultPoseHasNoEmote")]
    [InlineData("emote/normal_pose01_loop", TestDisplayName = "PrefixIsNotASingleLetter")]
    public void TryParsePoseTimeline_NotAPoseVariant_ReturnsFalse(string timelineKey) {
        ArrpDataCache.TryParsePoseTimeline(timelineKey, out _, out _).ShouldBeFalse();
    }

    #endregion
}
