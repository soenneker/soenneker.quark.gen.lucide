using System;
using System.Buffers;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;

namespace Soenneker.Quark.Gen.Lucide.BuildTasks;

internal static class MetadataHash
{
    public static string Compute(List<string> entries)
    {
        var bufferSize = 16384;
        foreach (string entry in entries)
            bufferSize = Math.Max(bufferSize, Encoding.UTF8.GetMaxByteCount(entry.Length) + 1);

        byte[] buffer = ArrayPool<byte>.Shared.Rent(bufferSize);
        try
        {
            using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
            var written = 0;
            for (var i = 0; i < entries.Count; i++)
            {
                // Preserve the existing UTF-8, newline-separated manifest while batching hash updates.
                if (buffer.Length - written < Encoding.UTF8.GetMaxByteCount(entries[i].Length) + 1)
                {
                    hash.AppendData(buffer.AsSpan(0, written));
                    written = 0;
                }

                if (i != 0)
                    buffer[written++] = (byte)'\n';

                written += Encoding.UTF8.GetBytes(entries[i].AsSpan(), buffer.AsSpan(written));
            }

            hash.AppendData(buffer.AsSpan(0, written));
            return Convert.ToHexString(hash.GetHashAndReset());
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }
}
