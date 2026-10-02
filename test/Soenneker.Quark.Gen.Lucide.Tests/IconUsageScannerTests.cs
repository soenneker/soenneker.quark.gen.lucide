using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Soenneker.Quark.Gen.Lucide.BuildTasks;

namespace Soenneker.Quark.Gen.Lucide.Tests;

public sealed class IconUsageScannerTests
{
    [Test]
    public void MatchesRegexForOverlappingInvalidAndRepeatedUsages()
    {
        foreach (string prefix in new[] { "LucideIcon.", "SimpleIcon." })
        {
            var regex = new Regex(Regex.Escape(prefix) + "([A-Za-z0-9_]+)", RegexOptions.CultureInvariant);
            foreach (string input in new[] { "", prefix, prefix + "!", prefix + "9_Test", prefix + "A " + prefix + "A", prefix + prefix + "A", "X" + prefix + "A", prefix + "A-B", prefix + "\u00e9A", prefix + "A\u00e9", prefix.ToLowerInvariant() + "A" })
            {
                var expected = new HashSet<string>(StringComparer.Ordinal);
                foreach (Match match in regex.Matches(input)) expected.Add(match.Groups[1].Value);
                var actual = new HashSet<string>(StringComparer.Ordinal);
                IconUsageScanner.Collect(input, prefix, actual);
                if (!actual.SetEquals(expected)) throw new Exception("Icon scanner mismatch: " + input);
            }
        }
    }
}
