using System;
using System.Collections.Generic;
using System.Linq;

namespace PclCeRegionPatch;

/// <summary>Public entry points for inspecting and patching an official PCL CE single-file build.</summary>
public static class PatchEngine
{
    public const string TargetTypeName = "PCL.RestrictedFeaturePolicy";

    /// <summary>
    ///     Inspects <paramref name="data"/> (the bytes of an official single-file PCL CE executable).
    /// </summary>
    /// <exception cref="PatchNotSupportedException">The input is not a supported PCL CE build.</exception>
    public static PatchInspection Inspect(byte[] data)
    {
        var assemblyStart = LocateAssembly(data);
        var image = PeImage.Parse(data, assemblyStart);

        return new PatchInspection
        {
            AssemblyStart = assemblyStart,
            ImageSize = image.ImageSize,
            SectionCount = image.SectionCount,
            IsIlOnly = image.IsIlOnly,
            HasManagedNativeHeader = image.HasManagedNativeHeader,
            IsPatched = IlPatcher.IsPatched(data, assemblyStart, image),
            Methods = IlPatcher.DescribeMethods(data, assemblyStart, image)
        };
    }

    /// <summary>
    ///     Applies the patch to <paramref name="data"/> in place.
    /// </summary>
    /// <exception cref="PatchNotSupportedException">The input is not a supported PCL CE build.</exception>
    public static PatchResult Apply(byte[] data)
    {
        var assemblyStart = LocateAssembly(data);
        var image = PeImage.Parse(data, assemblyStart);
        var report = IlPatcher.Patch(data, assemblyStart, image);

        return new PatchResult
        {
            PatchedMethods = report.PatchedMethods.ToList(),
            Verification = report.Verification.ToList()
        };
    }

    private static int LocateAssembly(byte[] data)
    {
        var assemblyStart = AssemblyLocator.Locate(data);
        if (assemblyStart < 0)
            throw new PatchNotSupportedException(
                "没有在文件中找到 PCL CE 主程序集。请确认输入是官方 PCL CE 单文件 exe（从官方 Releases 下载）。");

        var image = PeImage.Parse(data, assemblyStart);
        if (!image.IsIlOnly)
            throw new PatchNotSupportedException(
                "内嵌主程序集不是 IL-only（可能是 ReadyToRun 构建），本工具暂不支持该版本。");

        return assemblyStart;
    }
}

/// <summary>Thrown when the input file is not a supported official PCL CE single-file build.</summary>
public sealed class PatchNotSupportedException : Exception
{
    public PatchNotSupportedException(string message) : base(message)
    {
    }
}

/// <summary>Result of <see cref="PatchEngine.Inspect" />.</summary>
public sealed class PatchInspection
{
    public int AssemblyStart { get; init; }
    public int ImageSize { get; init; }
    public int SectionCount { get; init; }
    public bool IsIlOnly { get; init; }
    public bool HasManagedNativeHeader { get; init; }
    public bool IsPatched { get; init; }
    public IReadOnlyList<string> Methods { get; init; } = [];
    public int MethodCount => Methods.Count;
}

/// <summary>Result of <see cref="PatchEngine.Apply" />.</summary>
public sealed class PatchResult
{
    public IReadOnlyList<string> PatchedMethods { get; init; } = [];
    public IReadOnlyList<string> Verification { get; init; } = [];
}
