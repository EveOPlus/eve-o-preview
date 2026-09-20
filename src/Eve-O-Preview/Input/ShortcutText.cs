using System;
using System.Collections.Generic;

namespace EveOPreview.Input;

/// <summary>The invariant shortcut grammar already persisted in profiles. Display translation never changes it.</summary>
public static class ShortcutText
{
    private static readonly Dictionary<string, ShortcutKeys> Names = new(StringComparer.Ordinal)
    {
        ["(none)"] = ShortcutKeys.None,
        ["Ctrl"] = ShortcutKeys.Control, ["Enter"] = ShortcutKeys.Enter,
        ["Del"] = ShortcutKeys.Delete, ["Ins"] = ShortcutKeys.Insert,
        ["PgUp"] = ShortcutKeys.PageUp, ["PgDn"] = ShortcutKeys.PageDown,
        ["Backspace"] = ShortcutKeys.Back
    };

    public static ShortcutKeys Parse(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return ShortcutKeys.None;
        ShortcutKeys result = ShortcutKeys.None;
        bool foundCode = false;
        foreach (string token in text.Split('+', StringSplitOptions.TrimEntries))
        {
            ShortcutKeys part;
            if (token.Length == 1 && token[0] is >= '0' and <= '9') part = ShortcutKeys.D0 + (token[0] - '0');
            else if (!Names.TryGetValue(token, out part)) part = Enum.Parse<ShortcutKeys>(token);
            if ((part & ShortcutKeys.KeyCode) != 0)
            {
                if (foundCode) throw new FormatException("A shortcut can contain only one non-modifier key.");
                foundCode = true;
            }
            result |= part;
        }
        return result;
    }

    public static string Format(ShortcutKeys value)
    {
        var parts = new List<string>(4);
        if ((value & ShortcutKeys.Control) != 0) parts.Add("Ctrl");
        if ((value & ShortcutKeys.Alt) != 0) parts.Add("Alt");
        if ((value & ShortcutKeys.Shift) != 0) parts.Add("Shift");
        var key = value & ShortcutKeys.KeyCode;
        string name = key switch
        {
            ShortcutKeys.None => "(none)",
            ShortcutKeys.Enter => "Enter", ShortcutKeys.Delete => "Del", ShortcutKeys.Insert => "Ins",
            ShortcutKeys.PageUp => "PgUp", ShortcutKeys.PageDown => "PgDn", ShortcutKeys.Back => "Back",
            >= ShortcutKeys.D0 and <= ShortcutKeys.D9 => ((int)key - (int)ShortcutKeys.D0).ToString(System.Globalization.CultureInfo.InvariantCulture),
            _ => Enum.GetName(key)
        };
        if (name != null) parts.Add(name);
        return string.Join("+", parts);
    }
}
