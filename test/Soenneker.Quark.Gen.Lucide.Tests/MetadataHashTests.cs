using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Soenneker.Quark.Gen.Lucide.BuildTasks;

namespace Soenneker.Quark.Gen.Lucide.Tests;

public sealed class MetadataHashTests
{
    [Test]
    public void MatchesJoinedUtf8Manifest()
    {
        foreach (List<string> entries in new List<string>[] { [], [""], ["a", "b"], ["", ""], ["\u65e5\u672c\u8a9e/\ud83d\ude00.svg", new string('x', 10000)], ["\ud800", "\udc00"], Enumerable.Repeat("\u65e5/path/file.cs|123|456", 10000).ToList() })
        {
            string expected = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(string.Join('\n', entries))));
            if (MetadataHash.Compute(entries) != expected)
                throw new InvalidOperationException("The metadata hash changed.");
        }
    }
}
