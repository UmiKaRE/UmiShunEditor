using Godot;
using System;
using System.Collections;
using System.Collections.Generic;

namespace UmiShunEditor.Utils;

public class BidirectionalDictionary<T,U> : IEnumerable<KeyValuePair<T, U>>
{
    private readonly Dictionary<T,U> _forward = new Dictionary<T, U>();
    private readonly Dictionary<U,T> _backward = new Dictionary<U, T>();


    public void Add(T key, U value)
    {
        _forward.Add(key, value);
        _backward.Add(value, key);
    }

    public bool TryGet(T key, out U value)
    {
        return _forward.TryGetValue(key, out value);
    }

    public bool TryGetReverse(U key, out T value)
    {
        return _backward.TryGetValue(key, out value);
    }


    public void Remove(T key)
    {
        if (_forward.TryGetValue(key, out U value))
        {
            _forward.Remove(key);
            _backward.Remove(value);
        }
    }

    public void RemoveReverse(U key)
    {
        if (_backward.TryGetValue(key, out T value))
        {
            _backward.Remove(key);
            _forward.Remove(value);
        }
    }

    public void Clear()
    {
        _forward.Clear();
        _backward.Clear();
    }


    public IEnumerator<KeyValuePair<T, U>> GetEnumerator()
    {
        return _forward.GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }
}
