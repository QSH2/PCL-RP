using System.Collections.Generic;

namespace PclCeRegionPatch;

/// <summary>Result of a patch run, used for console output.</summary>
internal sealed class PatchReport
{
    public List<string> PatchedMethods { get; } = [];

    public List<string> Verification { get; } = [];
}
