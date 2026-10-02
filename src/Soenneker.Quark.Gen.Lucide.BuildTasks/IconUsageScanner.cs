using System;
using System.Collections.Generic;

namespace Soenneker.Quark.Gen.Lucide.BuildTasks;

internal static class IconUsageScanner
{
    public static void Collect(ReadOnlySpan<char> source, string prefix, HashSet<string> icons)
    {
        var lookup = icons.GetAlternateLookup<ReadOnlySpan<char>>();
        while (true)
        {
            int index = source.IndexOf(prefix, StringComparison.Ordinal);
            if (index < 0)
                return;

            source = source[(index + prefix.Length)..];
            var length = 0;
            while (length < source.Length && source[length] is >= 'A' and <= 'Z' or >= 'a' and <= 'z' or >= '0' and <= '9' or '_')
                length++;

            if (length > 0)
                lookup.Add(source[..length]);

            source = source[length..];
        }
    }
}
