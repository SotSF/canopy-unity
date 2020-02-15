using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System;

/* A O(n) dictionary implementation that
 * is serializable. Don't use this if O(1)
 * is crucial. */
[Serializable]
public class SerialDict<Tk, Tv>
{
    private List<Tk> keys;
    private List<Tv> values;

    public IEnumerable<Tk> Keys => keys;
    public IEnumerable<Tv> Values => values;
    public int Count => keys.Count;

    public SerialDict() {
        keys = new List<Tk>();
        values = new List<Tv>();
    }
    
    private int keyIndex(Tk key)
    {
        return keys.IndexOf(key);
    }

    public Tv this[Tk key]
    { 
        get {
            var index = keyIndex(key);
            return index >= 0 ? values[index] : default(Tv);
        }
        set => Add(key, value);
    }

    public void Add(Tk key, Tv value)
    {
        var index = keyIndex(key);
        if (index >= 0)
        {
            values[index] = value;
        } else
        {
            keys.Add(key);
            values.Add(value);
        }
    }

    public bool Remove(Tk key)
    {
        var index = keyIndex(key);
        if (index >= 0)
        {
            keys.RemoveAt(index);
            values.RemoveAt(index);
            return true;
        }
        return false;
    }

    public bool ContainsKey(Tk key)
    {
        return keys.Contains(key);
    }

    public bool ContainsValue(Tv val)
    {
        return values.Contains(val);
    }

    public void Clear()
    {
        keys.Clear();
        values.Clear();
    }
}
