using ARealmRepopulated.Core.List;
using Shouldly;

namespace ARealmRepopulated.Tests.Core.List;

public class CircularListTests {

    [Fact]
    public void GetNextItem_CyclesThroughAllItems() {
        var list = new CircularList<int>();
        list.Add(10);
        list.Add(20);
        list.Add(30);

        list.GetNextItem().ShouldBe(10);
        list.GetNextItem().ShouldBe(20);
        list.GetNextItem().ShouldBe(30);
    }

    [Fact]
    public void GetNextItem_WrapsAroundToStart() {
        var list = new CircularList<int>();
        list.Add(1);
        list.Add(2);

        list.GetNextItem().ShouldBe(1);
        list.GetNextItem().ShouldBe(2);
        list.GetNextItem().ShouldBe(1); // wraps
    }

    [Fact]
    public void Add_ResetsIndex() {
        var list = new CircularList<int>();
        list.Add(1);
        list.Add(2);

        list.GetNextItem().ShouldBe(1);
        list.GetNextItem().ShouldBe(2);

        list.Add(3);

        // After Add, index is reset to -1, so GetNextItem increments to 0
        list.GetNextItem().ShouldBe(1);
    }

    [Fact]
    public void Remove_ResetsIndex() {
        var list = new CircularList<int>();
        list.Add(1);
        list.Add(2);
        list.Add(3);

        list.GetNextItem(); // index = 0 -> 1
        list.GetNextItem(); // index = 1 -> 2

        list.Remove(2);

        // After Remove, index is reset to -1, so GetNextItem increments to 0
        list.GetNextItem().ShouldBe(1);
    }

    [Fact]
    public void GetCurrentItem_AfterGetNextItem_ReturnsSameItem() {
        var list = new CircularList<int>();
        list.Add(10);
        list.Add(20);

        var next = list.GetNextItem();
        var current = list.GetCurrentItem();

        current.ShouldBe(next);
    }

    [Fact]
    public void SingleItem_AlwaysReturnsSame() {
        var list = new CircularList<int>();
        list.Add(42);

        list.GetNextItem().ShouldBe(42);
        list.GetNextItem().ShouldBe(42);
        list.GetNextItem().ShouldBe(42);
    }

}
