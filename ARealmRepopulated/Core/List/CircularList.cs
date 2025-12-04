namespace ARealmRepopulated.Core.List;

public class CircularList<T> : List<T> {

    private int _currentIndex = 0;

    public new void Add(T item) {
        base.Add(item);
        _currentIndex = -1;
    }

    public new void Remove(T item) {
        base.Remove(item);
        _currentIndex = -1;
    }

    public T GetCurrentItem() {
        EnsureCorrectIndex();
        return this[_currentIndex];
    }

    public T GetNextItem() {
        _currentIndex++;
        return GetCurrentItem();
    }

    private void EnsureCorrectIndex() {
        if (_currentIndex > this.Count - 1)
            _currentIndex = 0;
    }

}
