using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;

namespace PasswordManager.Maui.Utils;

// ObservableCollection<T>.Add/Insert/RemoveAt each fire their own CollectionChanged notification,
// so mutating a CollectionView-bound list one item at a time (e.g. inserting a folder's newly
// expanded children, or repopulating an entries list) forces one UI layout pass per item - slow
// once more than a handful of items are involved. These Range methods batch the mutation and
// raise a single Reset notification instead.
public class ObservableRangeCollection<T> : ObservableCollection<T>
{
    public void AddRange(IEnumerable<T> items) => ReplaceCore(() =>
    {
        foreach (var item in items) Items.Add(item);
    });

    public void InsertRange(int index, IEnumerable<T> items) => ReplaceCore(() =>
    {
        foreach (var item in items) Items.Insert(index++, item);
    });

    public void RemoveRange(int index, int count) => ReplaceCore(() =>
    {
        for (var i = 0; i < count; i++) Items.RemoveAt(index);
    });

    public void ReplaceAll(IEnumerable<T> items) => ReplaceCore(() =>
    {
        Items.Clear();
        foreach (var item in items) Items.Add(item);
    });

    private void ReplaceCore(Action mutate)
    {
        mutate();
        OnPropertyChanged(new PropertyChangedEventArgs(nameof(Count)));
        OnPropertyChanged(new PropertyChangedEventArgs("Item[]"));
        OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
    }
}
