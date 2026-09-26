using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Atelier.Core.Properties;

namespace Atelier.Core.Styling;

public class Setter
{
    public BindableProperty Property { get; set; }
    public object? Value { get; set; }

    public Setter(BindableProperty property, object? value)
    {
        Property = property ?? throw new ArgumentNullException(nameof(property));
        property.ThrowIfReadOnly();
        property.ValidateValue(value);
        Value = value;
    }
}

public class Style : IEnumerable<Setter>
{
    public Type? TargetType { get; set; }
    public string? Key { get; set; }
    public Style? BasedOn { get; set; }
    public List<Setter> Setters { get; } = new();

    public Style() { }

    public Style(Type targetType)
    {
        TargetType = targetType;
    }

    public Style(string key, Type? targetType = null, Style? basedOn = null)
    {
        Key = key;
        TargetType = targetType;
        BasedOn = basedOn;
    }

    public Style Set<T>(BindableProperty<T> property, T value)
    {
        Setters.Add(new Setter(property, value));
        return this;
    }

    public Style Add(Setter setter)
    {
        Setters.Add(setter);
        return this;
    }

    public void Add(BindableProperty property, object? value)
    {
        Setters.Add(new Setter(property, value));
    }

    public IEnumerator<Setter> GetEnumerator() => Setters.GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}

public class StyleCollection : Collection<Style>
{
    public event Action? StylesChanged;

    protected override void InsertItem(int index, Style item)
    {
        base.InsertItem(index, item);
        StylesChanged?.Invoke();
    }

    protected override void SetItem(int index, Style item)
    {
        base.SetItem(index, item);
        StylesChanged?.Invoke();
    }

    protected override void RemoveItem(int index)
    {
        base.RemoveItem(index);
        StylesChanged?.Invoke();
    }

    protected override void ClearItems()
    {
        base.ClearItems();
        StylesChanged?.Invoke();
    }

    public void AddRange(IEnumerable<Style> styles)
    {
        foreach (var s in styles)
        {
            Add(s);
        }
    }
}

public static class StyleManager
{
    public static StyleCollection GlobalStyles { get; } = new();
}
