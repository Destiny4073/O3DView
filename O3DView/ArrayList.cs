using System.Collections;
using System.Numerics;
using System.Runtime.CompilerServices;

namespace O3DView;

/// <summary>
/// A simple array list which exposes it's backing array to users.
/// </summary>
/// <typeparam name="T"></typeparam>
public class ArrayList<T> : IList<T>
{
    /// <summary>
    /// The backing array of this list, please note that it is only valid as long as the length of the list is not mutated.
    /// </summary>
    public T[] items;
    /// <summary>
    /// The number of items in the list.
    /// </summary>
    public int count;
    /// <summary>
    /// The version number of this list. It is incremented whenever the list is mutated.
    /// </summary>
    public int version;
    public T? defaultValue = default;

    public T this[int index] { get => items[index]; set => items[index] = value; }
    public int Count => count;
    public bool IsReadOnly => false;

    public ArrayList()
    {
        items = [];
    }

    public ArrayList(int capacity) : this()
    {
        EnsureCapacity(capacity);
    }

    public ArrayList(IEnumerable<T> items) : this()
    {
        if (items is ICollection<T> collection)
        {
            EnsureCapacity(collection.Count);
            foreach (var item in collection)
                Add(item);
        } 
        else
        {
            foreach (var item in items)
                Add(item);
        }
    }

    public void EnsureCapacity(int capacity)
    {
        if (capacity <= items.Length)
            return;

        capacity = (int)BitOperations.RoundUpToPowerOf2((uint)capacity);
        var newArr = new T[capacity];
        Array.Copy(items, newArr, count);
        var old = items;
        if (defaultValue != null && count < capacity)
        {
            Array.Fill(newArr, defaultValue, count, capacity - count);
        }
        items = newArr;

        if (RuntimeHelpers.IsReferenceOrContainsReferences<T>())
            Array.Clear(old);
    }

    public void Add(T item)
    {
        EnsureCapacity(count + 1);
        items[count++] = item;
        version++;
    }

    public void AddRange(IEnumerable<T> items)
    {
        switch (items)
        {
            case T[] array:
                EnsureCapacity(count + array.Length);
                Array.Copy(array, 0, this.items, count, array.Length);
                count += array.Length;
                version++;
                break;
            case ICollection<T> collection:
                EnsureCapacity(collection.Count);
                foreach (var item in collection)
                    Add(item);
                break;
            default:
                foreach (var item in items)
                    Add(item);
                break;
        }
    }

    public void Clear()
    {
        count = 0;
        if (RuntimeHelpers.IsReferenceOrContainsReferences<T>())
        {
            if (defaultValue == null)
                Array.Clear(items);
            else
                Array.Fill(items, defaultValue);
        }
        version++;
    }

    public bool Contains(T item)
    {
        return Array.IndexOf(items, item, 0, count) != -1;
    }

    public void CopyTo(T[] array, int arrayIndex)
    {
        Array.Copy(items, 0, array, arrayIndex, count);
    }

    public IEnumerator<T> GetEnumerator()
    {
        int startVersion = version;
        for (int i = 0; i < count; i++)
        {
            if (version != startVersion)
                throw new InvalidOperationException("List has been mutated since iteration began!");
            yield return items[i];
        }
    }

    public int IndexOf(T item)
    {
        return Array.IndexOf(items, item, 0, count);
    }

    public void Insert(int index, T item)
    {
        if (index < 0 || index > count)
            throw new ArgumentOutOfRangeException(nameof(index));
        if (index == count)
        {
            Add(item);
            return;
        }
        EnsureCapacity(count + 1);
        Array.Copy(items, index, items, index + 1, count - index);
        items[index] = item;
        count++;
        version++;
    }

    public bool Remove(T item)
    {
        int ind = IndexOf(item);
        if (ind == -1)
            return false;
        RemoveAt(ind);
        return true;
    }

    public void RemoveAt(int index)
    {
        if (index == count - 1)
        {
            count--;
            version++;
            return;
        }

        Array.Copy(items, index + 1, items, index, count - index - 1);

        count--;
        version++;
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
