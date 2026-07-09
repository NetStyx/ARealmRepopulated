using ARealmRepopulated.Data.Supplementals;
using Shouldly;
using System.Collections.Generic;

namespace ARealmRepopulated.Tests.Data.Supplementals;

public class BNpcLinkParserTests {

    [Fact]
    public void Instance_IsNotNullAndSingleton() {
        var instance1 = BNpcLinkParser.Instance;
        instance1.ShouldNotBeNull();

        var instance2 = BNpcLinkParser.Instance;
        instance1.ShouldBeSameAs(instance2);
    }

    [Fact]
    public void GetNamesFromBase_AlwaysReturnsListNotNull() {
        var nameIds = BNpcLinkParser.Instance.GetNamesFromBase(0);
        nameIds.ShouldNotBeNull();
        nameIds.ShouldBeOfType<List<uint>>();
    }

    [Fact]
    public void GetNamesFromBase_WithValidBaseId_ReturnsNameIds() {
        var nameIds = BNpcLinkParser.Instance.GetNamesFromBase(1);
        nameIds.ShouldNotBeNull();
    }

    [Fact]
    public void GetNamesFromBase_WithLargeBaseId_ReturnsEmptyListIfNotFound() {
        var nameIds = BNpcLinkParser.Instance.GetNamesFromBase(999999999);
        nameIds.ShouldNotBeNull();
        nameIds.ShouldBeEmpty();
    }

    [Fact]
    public void GetBasesFromName_AlwaysReturnsListNotNull() {
        var baseIds = BNpcLinkParser.Instance.GetBasesFromName(0);
        baseIds.ShouldNotBeNull();
        baseIds.ShouldBeOfType<List<uint>>();
    }

    [Fact]
    public void GetBasesFromName_WithValidNameId_ReturnsBaseIds() {
        var baseIds = BNpcLinkParser.Instance.GetBasesFromName(1);
        baseIds.ShouldNotBeNull();
    }

    [Fact]
    public void GetBasesFromName_WithLargeNameId_ReturnsEmptyListIfNotFound() {
        var baseIds = BNpcLinkParser.Instance.GetBasesFromName(999999999);
        baseIds.ShouldNotBeNull();
        baseIds.ShouldBeEmpty();
    }

    [Fact]
    public void ReverseAndForwardMappings_AreConsistent() {
        // For each base ID, get its name IDs, then verify those name IDs map back to the original base
        var baseIds = new uint[] { 1, 4, 5, 6 };

        foreach (var baseId in baseIds) {
            var nameIds = BNpcLinkParser.Instance.GetNamesFromBase(baseId);

            foreach (var nameId in nameIds) {
                var reverseBaseIds = BNpcLinkParser.Instance.GetBasesFromName(nameId);
                reverseBaseIds.ShouldContain(baseId);
            }
        }
    }
}

