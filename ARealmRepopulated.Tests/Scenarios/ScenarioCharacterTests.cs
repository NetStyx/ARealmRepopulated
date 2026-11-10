using ARealmRepopulated.Core.Services.Characters;
using ARealmRepopulated.Data.Characters;
using ARealmRepopulated.Data.Scenarios;
using ARealmRepopulated.Tests.Infrastructure;
using Shouldly;
using System;
using System.Collections.Generic;

namespace ARealmRepopulated.Tests.Scenarios;

public class ScenarioCharacterTests {

    private static CharacterData Shopkeeper(int revision = 3) {
        var character = new CharacterData { Revision = revision, Name = "Mira Ashford" };
        character.AdditionalData["Integration.General.Actor.Name"] = "Arrp Mira";
        return character;
    }

    [Fact]
    public void MatchImport_NoLocalCharacter_IsNew()
        => Shopkeeper().MatchImport(null).ShouldBe(CharacterImportMatch.New);

    [Fact]
    public void MatchImport_LowerRevision_IsKnownEvenWhenChanged() {
        var local = Shopkeeper(revision: 5);
        var imported = local.Copy();
        imported.Revision = 4;
        imported.Name = "Mira";

        imported.MatchImport(local).ShouldBe(CharacterImportMatch.Known);
    }

    [Fact]
    public void MatchImport_SameRevisionAndContent_IsKnown() {
        var local = Shopkeeper();

        local.Copy().MatchImport(local).ShouldBe(CharacterImportMatch.Known);
    }

    [Fact]
    public void MatchImport_HigherRevision_IsNewer() {
        var local = Shopkeeper();
        var imported = local.Copy();
        imported.Revision++;

        imported.MatchImport(local).ShouldBe(CharacterImportMatch.Newer);
    }

    [Fact]
    public void MatchImport_SameRevisionWithOtherAppearance_IsConflicting() {
        var local = Shopkeeper();
        var imported = local.Copy();
        imported.Appearance.Scale = 1.2f;

        imported.MatchImport(local).ShouldBe(CharacterImportMatch.Conflicting);
    }

    [Fact]
    public void ApplyCharacters_LinkedActors_TakeTheCharacterAndKeepTheStoredLookOfMissingOnes() {
        var character = Shopkeeper();
        character.Appearance.Scale = 1.2f;
        var missingId = Guid.NewGuid();

        var scenario = new ScenarioData();
        scenario.Npcs.Add(new ScenarioNpcData { CharacterId = character.Id });
        scenario.Npcs.Add(new ScenarioNpcData { CharacterId = missingId, Name = "Bramblefox" });

        var missing = scenario.ApplyCharacters(new Dictionary<Guid, CharacterData> { [character.Id] = character });

        scenario.Npcs[0].HasSameCharacterAs(character).ShouldBeTrue();
        scenario.Npcs[0].Appearance.ShouldNotBeSameAs(character.Appearance);
        scenario.Npcs[1].Name.ShouldBe("Bramblefox");
        missing.ShouldBe([missingId]);
    }

    [Fact]
    public void ResolveDefinition_MissingCharacter_ShowsTheDefaultUntilUnlinked() {
        using var characters = new CharacterFileManager(null!, NullPluginLog.Instance, null!);
        var npc = new ScenarioNpcData { CharacterId = Guid.NewGuid(), Name = "Bramblefox" };

        characters.ResolveDefinition(npc).Name.ShouldBeEmpty();
        npc.Name.ShouldBe("Bramblefox");

        npc.CharacterId = null;
        characters.ResolveDefinition(npc).ShouldBeSameAs(npc);
    }
}
