using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Mono.Cecil;

namespace PclCeRegionPatch;

/// <summary>
///     Rewrites the two boolean methods of <c>PCL.RestrictedFeaturePolicy</c> so that the region
///     restriction is always bypassed:
///     <list type="bullet">
///         <item><c>Check(string, bool)</c> always returns <c>true</c>.</item>
///         <item><c>get_IsRestricted()</c> always returns <c>false</c>.</item>
///     </list>
///     Only the method bodies are touched (3 bytes each); nothing else in the file changes.
/// </summary>
internal static class IlPatcher
{
    public const string TargetTypeName = "PCL.RestrictedFeaturePolicy";

    private const string CheckMethodName = "Check";
    private const string IsRestrictedMethodName = "get_IsRestricted";

    // Tiny method body header: (codeSize << 2) | 0x2, with codeSize = 2.
    private const byte TinyHeaderForTwoIlBytes = 0x0A;
    private const byte LdcI4_1 = 0x17;
    private const byte LdcI4_0 = 0x16;
    private const byte Ret = 0x2A;

    /// <summary>Applies the patch to <paramref name="data"/> in place and returns a report.</summary>
    public static PatchReport Patch(byte[] data, int assemblyStart, PeImage image)
    {
        var report = new PatchReport();

        using (var assembly = ReadAssembly(data, assemblyStart, image))
        {
            var type = FindTargetType(assembly);
            var check = FindMethod(type, CheckMethodName, 2);
            var isRestricted = FindMethod(type, IsRestrictedMethodName, 0);

            PatchBooleanBody(data, assemblyStart, image, check, true, report);
            PatchBooleanBody(data, assemblyStart, image, isRestricted, false, report);
        }

        report.Verification.AddRange(ReadMethodIl(data, assemblyStart, image));
        return report;
    }

    /// <summary>Returns whether both method bodies already contain the patched IL.</summary>
    public static bool IsPatched(byte[] data, int assemblyStart, PeImage image)
    {
        using var assembly = ReadAssembly(data, assemblyStart, image);
        var type = FindTargetType(assembly);
        var check = FindMethod(type, CheckMethodName, 2);
        var isRestricted = FindMethod(type, IsRestrictedMethodName, 0);
        return IsBodyPatched(data, assemblyStart, image, check, true)
               && IsBodyPatched(data, assemblyStart, image, isRestricted, false);
    }

    private static AssemblyDefinition ReadAssembly(byte[] data, int assemblyStart, PeImage image)
    {
        if (assemblyStart < 0 || assemblyStart + image.ImageSize > data.Length)
            throw new InvalidOperationException("embedded assembly is outside of the file bounds");

        var slice = new byte[image.ImageSize];
        Buffer.BlockCopy(data, assemblyStart, slice, 0, image.ImageSize);
        return AssemblyDefinition.ReadAssembly(new MemoryStream(slice, writable: false), new ReaderParameters
        {
            ReadingMode = ReadingMode.Immediate
        });
    }

    private static TypeDefinition FindTargetType(AssemblyDefinition assembly)
    {
        var type = assembly.MainModule.GetType(TargetTypeName);
        if (type is null)
            throw new InvalidOperationException(
                $"type '{TargetTypeName}' was not found; this does not look like a supported PCL CE build.");
        return type;
    }

    private static MethodDefinition FindMethod(TypeDefinition type, string name, int parameterCount)
    {
        var method = type.Methods.FirstOrDefault(m => m.Name == name && m.Parameters.Count == parameterCount);
        if (method is null)
            throw new InvalidOperationException($"method '{name}' was not found in type '{type.FullName}'.");
        return method;
    }

    private static int GetBodyFileOffset(PeImage image, MethodDefinition method)
    {
        if (!method.HasBody)
            throw new InvalidOperationException($"method '{method.Name}' has no IL body");
        return image.RvaToFileOffset(method.RVA);
    }

    private static void PatchBooleanBody(byte[] data, int assemblyStart, PeImage image,
        MethodDefinition method, bool value, PatchReport report)
    {
        var absolute = assemblyStart + GetBodyFileOffset(image, method);
        if (absolute + 2 >= data.Length)
            throw new InvalidOperationException($"method '{method.Name}' body is outside of the file bounds");

        var oldHeader = data[absolute];
        data[absolute] = TinyHeaderForTwoIlBytes;
        data[absolute + 1] = value ? LdcI4_1 : LdcI4_0;
        data[absolute + 2] = Ret;

        report.PatchedMethods.Add(
            $"{method.Name} -> {(value ? "ldc.i4.1" : "ldc.i4.0")}; ret  (file offset 0x{absolute:X}, old header 0x{oldHeader:X2})");
    }

    private static bool IsBodyPatched(byte[] data, int assemblyStart, PeImage image, MethodDefinition method, bool value)
    {
        var absolute = assemblyStart + GetBodyFileOffset(image, method);
        return absolute + 2 < data.Length
               && data[absolute] == TinyHeaderForTwoIlBytes
               && data[absolute + 1] == (value ? LdcI4_1 : LdcI4_0)
               && data[absolute + 2] == Ret;
    }

    /// <summary>Returns human-readable descriptions of every method on the target type.</summary>
    internal static IReadOnlyList<string> DescribeMethods(byte[] data, int assemblyStart, PeImage image)
    {
        using var assembly = ReadAssembly(data, assemblyStart, image);
        var type = FindTargetType(assembly);
        return type.Methods
            .Select(method =>
                $"{method.Name}({string.Join(", ", method.Parameters.Select(parameter => parameter.ParameterType.Name))}) -> {method.ReturnType.Name}")
            .ToList();
    }

    private static IEnumerable<string> ReadMethodIl(byte[] data, int assemblyStart, PeImage image)
    {
        using var assembly = ReadAssembly(data, assemblyStart, image);
        var type = FindTargetType(assembly);
        var targets = new[]
        {
            (Method: FindMethod(type, CheckMethodName, 2), Name: CheckMethodName),
            (Method: FindMethod(type, IsRestrictedMethodName, 0), Name: IsRestrictedMethodName)
        };

        foreach (var (method, name) in targets)
        {
            var il = string.Join("; ", method.Body.Instructions.Select(instruction => instruction.ToString()));
            yield return $"{name}: {il}";
        }
    }
}
