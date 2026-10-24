using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Mono.Cecil;

namespace PclCeRegionPatch;

/// <summary>Supported products.</summary>
public enum PatchTarget
{
    PclCe,
    PclOfficial
}

/// <summary>Result of inspecting an input executable.</summary>
public sealed class PatchInspection
{
    public PatchTarget Target { get; init; }
    public string ProductName { get; init; } = "";
    public string Version { get; init; } = "";
    public bool IsPatched { get; init; }
    public IReadOnlyList<string> Details { get; init; } = [];
}

/// <summary>Result of applying a patch. <see cref="Output" /> contains the patched executable bytes.</summary>
public sealed class PatchResult
{
    public byte[] Output { get; init; } = [];
    public IReadOnlyList<string> Steps { get; init; } = [];
    public IReadOnlyList<string> Verification { get; init; } = [];
}

/// <summary>Thrown when the input file is not a supported executable.</summary>
public sealed class PatchNotSupportedException : Exception
{
    public PatchNotSupportedException(string message) : base(message)
    {
    }
}

/// <summary>
///     Entry point for inspecting and patching supported launchers:
///     PCL CE (community edition, .NET single-file) and PCL official (.NET Framework).
/// </summary>
public static class PatchEngine
{
    /// <summary>Inspects <paramref name="data"/> and returns product information.</summary>
    /// <exception cref="PatchNotSupportedException">The input is not a supported launcher build.</exception>
    public static PatchInspection Inspect(byte[] data)
    {
        var ceAssemblyStart = AssemblyLocator.Locate(data);
        return ceAssemblyStart >= 0
            ? InspectPclCe(data, ceAssemblyStart)
            : OfficialPclPatcher.Inspect(data);
    }

    /// <summary>Applies the matching patch to <paramref name="data"/> and returns the patched bytes.</summary>
    /// <exception cref="PatchNotSupportedException">The input is not a supported launcher build.</exception>
    public static PatchResult Apply(byte[] data)
    {
        var ceAssemblyStart = AssemblyLocator.Locate(data);
        return ceAssemblyStart >= 0
            ? ApplyPclCe(data, ceAssemblyStart)
            : OfficialPclPatcher.Apply(data);
    }

    private static PatchInspection InspectPclCe(byte[] data, int assemblyStart)
    {
        var image = PeImage.Parse(data, assemblyStart);
        var isPatched = IlPatcher.IsPatched(data, assemblyStart, image);
        var methods = IlPatcher.DescribeMethods(data, assemblyStart, image);

        var details = new List<string>
        {
            $"内嵌程序集：文件偏移 0x{assemblyStart:X}，{image.ImageSize:N0} 字节，{image.SectionCount} 个节",
            $"IL-only：{image.IsIlOnly}，托管原生头（ReadyToRun）：{image.HasManagedNativeHeader}",
            $"目标类型：{IlPatcher.TargetTypeName}"
        };
        details.AddRange(methods.Select(method => "目标方法：" + method));
        details.Add(isPatched ? "地区限制：已解除" : "地区限制：尚未解除");

        return new PatchInspection
        {
            Target = PatchTarget.PclCe,
            ProductName = "PCL CE（社区版，.NET 单文件）",
            Version = TryGetCeVersion(data, assemblyStart, image),
            IsPatched = isPatched,
            Details = details
        };
    }

    private static string TryGetCeVersion(byte[] data, int assemblyStart, PeImage image)
    {
        try
        {
            var slice = new byte[image.ImageSize];
            Buffer.BlockCopy(data, assemblyStart, slice, 0, image.ImageSize);
            using var assembly = AssemblyDefinition.ReadAssembly(new MemoryStream(slice, writable: false));
            return assembly.Name.Version.ToString();
        }
        catch
        {
            return "未知";
        }
    }

    private static PatchResult ApplyPclCe(byte[] data, int assemblyStart)
    {
        var image = PeImage.Parse(data, assemblyStart);
        var copy = (byte[])data.Clone();
        var report = IlPatcher.Patch(copy, assemblyStart, image);

        return new PatchResult
        {
            Output = copy,
            Steps = report.PatchedMethods.Select(line => "改写方法体：" + line).ToList(),
            Verification = report.Verification.ToList()
        };
    }
}
