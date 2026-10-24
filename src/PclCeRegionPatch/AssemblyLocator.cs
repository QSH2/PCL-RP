using System;

namespace PclCeRegionPatch;

/// <summary>
///     Locates the main PCL CE assembly that is embedded in the official single-file executable.
///     The lookup is based on metadata strings that only exist in that assembly, and every hit is
///     validated by parsing the candidate PE image.
/// </summary>
internal static class AssemblyLocator
{
    private static readonly byte[][] Markers =
    [
        "RestrictedFeaturePolicy"u8.ToArray(),
        "get_IsRestricted"u8.ToArray()
    ];

    /// <summary>
    ///     Returns the file offset where the embedded main assembly starts, or -1 when it cannot be found.
    /// </summary>
    public static int Locate(byte[] data)
    {
        foreach (var marker in Markers)
        {
            var from = 0;
            while (from <= data.Length - marker.Length)
            {
                var index = data.AsSpan(from).IndexOf(marker);
                if (index < 0) break;

                var occurrence = from + index;
                var assemblyStart = FindContainingPe(data, occurrence);
                if (assemblyStart >= 0) return assemblyStart;

                from = occurrence + 1;
            }
        }

        return -1;
    }

    private static int FindContainingPe(byte[] data, int occurrence)
    {
        // Scan backwards to the nearest valid PE file that fully contains the marker.
        for (var i = occurrence - 1; i >= 0; i--)
        {
            if (data[i] != (byte)'M' || i + 1 >= data.Length || data[i + 1] != (byte)'Z') continue;

            try
            {
                var image = PeImage.Parse(data, i);
                if (image.ImageSize > 0 && i + image.ImageSize > occurrence && image.IsIlOnly)
                    return i;
            }
            catch
            {
                // Not a PE, or not a .NET assembly; keep scanning.
            }
        }

        return -1;
    }
}
