using ARealmRepopulated.Infrastructure;
using Shouldly;

namespace ARealmRepopulated.Tests.Infrastructure;

public class CharacterNameValidationTests {

    #region Valid Name Tests

    [Theory]
    [InlineData("John Smith", TestDisplayName = "TypicalValidName")]
    [InlineData("Jo Do", TestDisplayName = "MinimumValidLength")]
    [InlineData("Alexandervicto Smith", TestDisplayName = "MaximumValidLength")]
    [InlineData("Alexandervicto Jo", TestDisplayName = "FirstName15Characters")]
    [InlineData("Jo Alexandervicto", TestDisplayName = "LastName15Characters")]
    public void IsValidUserName_Validity_ReturnsTrue(string name) {
        ArrpCharacterCreationData.IsValidPlayerName(name).ShouldBeTrue();
    }

    #endregion

    #region Boundry Tests

    [Theory]
    [InlineData("A Smith", TestDisplayName = "FirstNameTooShort")]
    [InlineData("John S", TestDisplayName = "LastNameTooShort")]
    [InlineData("A B", TestDisplayName = "BothNamesTooShort")]
    public void IsValidUserName_TooShort_ReturnsFalse(string data) {
        ArrpCharacterCreationData.IsValidPlayerName(data).ShouldBeFalse();
    }

    [Theory]
    [InlineData("Alexandervictoryb Jo", TestDisplayName = "FirstName16Characters")]
    [InlineData("Jo Alexandervictoryb", TestDisplayName = "LastName16Characters")]
    [InlineData("Alexandervictory Jo ", TestDisplayName = "TotalLength21")]
    [InlineData("Alexandros Fitzgeralds", TestDisplayName = "CombinedNameExceedsLimit")]
    [InlineData("Alexandervictory Alexandervictory", TestDisplayName = "BothNamesAt15CharactersExceedsLimit")]
    public void IsValidUserName_TooLong_ReturnsFalse(string name) {
        ArrpCharacterCreationData.IsValidPlayerName(name).ShouldBeFalse();
    }

    #endregion

    #region Invalid: Format Tests

    [Theory]
    [InlineData("John", TestDisplayName = "NoLastName")]
    [InlineData("John Q Smith", TestDisplayName = "ThreeNameParts")]
    [InlineData("", TestDisplayName = "EmptyString")]
    [InlineData(null!, TestDisplayName = "NullString")]
    [InlineData("   ", TestDisplayName = "WhitespaceOnly")]
    [InlineData("John ", TestDisplayName = "OnlyFirstName")]
    [InlineData(" Smith", TestDisplayName = "OnlyLastName")]
    [InlineData("John\tSmith", TestDisplayName = "TabSeparatedNames")]
    [InlineData("John  Smith", TestDisplayName = "MultipleSpacesSeparation")]
    [InlineData("John123 Smith", TestDisplayName = "ContainsNumbers")]
    [InlineData("John--Doe Smith", TestDisplayName = "ContainsConsecutiveHypens")]
    [InlineData("John''Doe Smith", TestDisplayName = "ContainsConsecutiveApostrophes")]
    [InlineData("-John Smith", TestDisplayName = "HyphensAtBeginning")]
    [InlineData("John- Smith", TestDisplayName = "HyphensAtEnd")]
    [InlineData("'John Smith", TestDisplayName = "ApostrophesAtBeginngin")]
    [InlineData("John' Smith", TestDisplayName = "ApostrophesAtEnd")]
    [InlineData("J@ke Smith", TestDisplayName = "SpecialCharacterInName")]
    [InlineData("  John Smith  ", TestDisplayName = "WithLeadingTrailingSpaces")]
    public void IsValidUserName_Invalid_ReturnsFalse(string? name) {
        ArrpCharacterCreationData.IsValidPlayerName(name!).ShouldBeFalse();
    }

    #endregion
}
