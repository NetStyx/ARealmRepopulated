using ARealmRepopulated.Core.Services.Npcs;
using ARealmRepopulated.Infrastructure;
using Shouldly;
using System.Text;

namespace ARealmRepopulated.Tests.Core.Npcs;

public class ActorNameTests {

    #region Prefix Tests

    [Theory]
    [InlineData("Arrp Bramblefox", "Bramblefox", TestDisplayName = "PrefixedName")]
    [InlineData("Arrp ", "", TestDisplayName = "PrefixWithNothingBehindIt")]
    public void WithoutPrefix_PrefixedName_ReturnsTheHalfBehindIt(string name, string expected) {
        ActorName.HasPrefix(name).ShouldBeTrue();
        ActorName.WithoutPrefix(name).ShouldBe(expected);
    }

    [Theory]
    [InlineData("光之战士", TestDisplayName = "ChineseName")]
    [InlineData("Bramblefox", TestDisplayName = "BareName")]
    [InlineData("Arrp", TestDisplayName = "PrefixWithoutItsSeparator")]
    [InlineData("Arrpington Smith", TestDisplayName = "NameMerelyStartingWithTheSameLetters")]
    [InlineData("arrp Bramblefox", TestDisplayName = "PrefixInTheWrongCase")]
    [InlineData("", TestDisplayName = "EmptyName")]
    [InlineData(null!, TestDisplayName = "NullName")]
    public void WithoutPrefix_UnprefixedName_IsLeftIntact(string? name) {
        ActorName.HasPrefix(name!).ShouldBeFalse();
        ActorName.WithoutPrefix(name!).ShouldBe(name ?? "");
    }

    #endregion

    #region Correction Tests

    [Theory]
    [InlineData("光之战士", "", TestDisplayName = "ChineseNameHasNothingToKeep")]
    [InlineData("Bram blefox", "Bramblefox", TestDisplayName = "WhitespaceIsDropped")]
    [InlineData("  Bramblefox  ", "Bramblefox", TestDisplayName = "SurroundingWhitespaceIsDropped")]
    [InlineData("bramblefox", "Bramblefox", TestDisplayName = "FirstLetterIsRaised")]
    [InlineData("BRAMBLEFOX", "Bramblefox", TestDisplayName = "ShoutedNameIsLowered")]
    [InlineData("Br4mbl3f0x", "Brmblfx", TestDisplayName = "DigitsAreDropped")]
    [InlineData("Bramble_fox!", "Bramblefox", TestDisplayName = "SymbolsAreDropped")]
    [InlineData("Bramble-fox", "Bramble-fox", TestDisplayName = "HyphenBetweenLettersIsKept")]
    [InlineData("O'bramblefox", "O'bramblefox", TestDisplayName = "ApostropheBetweenLettersIsKept")]
    [InlineData("-Bramblefox", "Bramblefox", TestDisplayName = "LeadingSeparatorIsDropped")]
    [InlineData("Bramblefox-", "Bramblefox", TestDisplayName = "TrailingSeparatorIsDropped")]
    [InlineData("Bramble--fox", "Bramble-fox", TestDisplayName = "RepeatedSeparatorIsCollapsed")]
    [InlineData("Bramble-'fox", "Bramble-fox", TestDisplayName = "MixedSeparatorsAreCollapsed")]
    [InlineData("Bramblefoxglove", "Bramblefoxglove", TestDisplayName = "LongestNameThatFits")]
    [InlineData("Bramblefoxgloves", "Bramblefoxglove", TestDisplayName = "OverlongNameIsCut")]
    [InlineData("Bramblefoxglov-es", "Bramblefoxglov", TestDisplayName = "CutExposingASeparatorDropsIt")]
    [InlineData("", "", TestDisplayName = "EmptyName")]
    [InlineData("   ", "", TestDisplayName = "WhitespaceOnlyName")]
    [InlineData(null!, "", TestDisplayName = "NullName")]
    public void Filter_AnyName_IsCorrectedIntoOneTheGameAccepts(string? name, string expected) {
        ActorName.Filter(name!).ShouldBe(expected);
    }

    [Theory]
    [InlineData("Bram blefox", TestDisplayName = "WhitespaceInName")]
    [InlineData("光之战士 Bramblefox", TestDisplayName = "MixedScriptName")]
    [InlineData("br4mble--fox!", TestDisplayName = "ThoroughlyInvalidName")]
    [InlineData("Bramblefoxgloves", TestDisplayName = "OverlongName")]
    public void Filter_CorrectedName_PassesNameValidationBehindThePrefix(string name) {
        var corrected = ActorName.IntegrationPrefix + ActorName.Filter(name);

        ArrpCharacterCreationData.IsValidPlayerName(corrected).ShouldBeTrue();
    }

    [Fact]
    public void Clean_SeparatorJustTyped_IsKeptSoTypingCanContinue() {
        // Filter would drop it, which would stop the user typing a hyphen at all.
        ActorName.Clean("Bramble-").ShouldBe("Bramble-");
        ActorName.Filter("Bramble-").ShouldBe("Bramble");
    }

    #endregion

    #region Truncation Tests

    [Fact]
    public void TruncateToBytes_NameWithinLimit_IsUnchanged() {
        ActorName.TruncateToBytes("Arrp Smith", ActorName.MaxNameBytes).ShouldBe("Arrp Smith");
    }

    [Fact]
    public void TruncateToBytes_CountsBytesNotCharacters() {
        // Six characters are the chinese client's limit and already take 18 bytes.
        ActorName.ByteLength("光之战士光之").ShouldBe(18);
        ActorName.TruncateToBytes("光之战士光之", 18).ShouldBe("光之战士光之");
    }

    [Fact]
    public void TruncateToBytes_LimitSplitsCharacter_DropsWholeCharacter() {
        ActorName.TruncateToBytes("光之战士", 8).ShouldBe("光之");
    }

    [Fact]
    public void TruncateToBytes_SurrogatePair_IsNotCutInHalf() {
        var truncated = ActorName.TruncateToBytes("a🍎b", 4);

        truncated.ShouldBe("a");
        ActorName.ByteLength(truncated).ShouldBe(1);
    }

    [Theory]
    [InlineData("", TestDisplayName = "EmptyName")]
    [InlineData(null!, TestDisplayName = "NullName")]
    public void TruncateToBytes_WithoutName_ReturnsEmpty(string? name) {
        ActorName.TruncateToBytes(name!, ActorName.MaxNameBytes).ShouldBeEmpty();
    }

    [Theory]
    [InlineData(0, TestDisplayName = "ZeroBytes")]
    [InlineData(-1, TestDisplayName = "NegativeBytes")]
    public void TruncateToBytes_NoBudget_ReturnsEmpty(int maxBytes) {
        ActorName.TruncateToBytes("Arrp Smith", maxBytes).ShouldBeEmpty();
    }

    #endregion

    #region Encoding Tests

    [Fact]
    public void Encode_NonLatinName_IsUtf8Encoded() {
        ActorName.Encode("光之战士").ShouldBe(Encoding.UTF8.GetBytes("光之战士"));
    }

    [Fact]
    public void Encode_OverlongName_LeavesRoomForTerminator() {
        var encoded = ActorName.Encode(new string('光', 64));

        encoded.Length.ShouldBeLessThanOrEqualTo(ActorName.MaxNameBytes);
        Encoding.UTF8.GetString(encoded).ShouldBe(new string('光', ActorName.MaxNameBytes / 3));
    }

    #endregion
}
