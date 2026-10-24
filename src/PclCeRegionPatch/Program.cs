using System;
using System.IO;

namespace PclCeRegionPatch;

internal static class Program
{
    private const string ToolName = "pcl-ce-regionpatch";
    private const string ToolVersion = "1.0.0";

    private static int Main(string[] args)
    {
        try
        {
            return Run(args);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"error: {ex.Message}");
            return 10;
        }
    }

    private static int Run(string[] args)
    {
        string? input = null;
        string? output = null;
        var checkOnly = false;

        for (var i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "-h" or "--help":
                    PrintHelp();
                    return 0;
                case "-v" or "--version":
                    Console.WriteLine($"{ToolName} {ToolVersion}");
                    return 0;
                case "-c" or "--check":
                    checkOnly = true;
                    break;
                case "-o" or "--output":
                    if (i + 1 >= args.Length) throw new ArgumentException("--output requires a value");
                    output = args[++i];
                    break;
                default:
                    if (args[i].StartsWith('-')) throw new ArgumentException($"unknown option: {args[i]}");
                    if (input is not null) throw new ArgumentException("only one input file is supported");
                    input = args[i];
                    break;
            }
        }

        if (input is null)
        {
            PrintHelp();
            return 2;
        }

        if (!File.Exists(input))
        {
            Console.Error.WriteLine($"error: input file not found: {input}");
            return 2;
        }

        var data = File.ReadAllBytes(input);
        Console.WriteLine($"input     : {Path.GetFullPath(input)} ({data.Length:N0} bytes)");

        var assemblyStart = AssemblyLocator.Locate(data);
        if (assemblyStart < 0)
        {
            Console.Error.WriteLine(
                "error: could not find the embedded PCL CE main assembly. " +
                "Make sure the input is an official PCL CE single-file build (not a folder build or an already repacked file).");
            return 3;
        }

        var image = PeImage.Parse(data, assemblyStart);
        Console.WriteLine($"assembly  : file offset 0x{assemblyStart:X}, image 0x{image.ImageSize:X}, " +
                          $"sections {image.SectionCount}, IL-only {image.IsIlOnly}, managed-native-header {image.HasManagedNativeHeader}");

        if (!image.IsIlOnly)
        {
            Console.Error.WriteLine("error: the embedded assembly is not IL-only (ReadyToRun?); refusing to patch.");
            return 3;
        }

        if (checkOnly)
        {
            var patched = IlPatcher.IsPatched(data, assemblyStart, image);
            Console.WriteLine(patched
                ? "status    : PATCHED (the region restriction is disabled)"
                : "status    : NOT PATCHED (the region restriction is active)");
            return patched ? 0 : 1;
        }

        output ??= Path.Combine(Path.GetDirectoryName(Path.GetFullPath(input))!,
            Path.GetFileNameWithoutExtension(input) + ".patched.exe");
        output = Path.GetFullPath(output);

        if (string.Equals(Path.GetFullPath(input), output, StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("input and output must be different files");

        var report = IlPatcher.Patch(data, assemblyStart, image);
        File.WriteAllBytes(output, data);

        Console.WriteLine($"output    : {output} ({data.Length:N0} bytes)");
        foreach (var line in report.PatchedMethods) Console.WriteLine($"patched   : {line}");
        foreach (var line in report.Verification) Console.WriteLine($"verify    : {line}");
        Console.WriteLine();
        Console.WriteLine("done. The output is a MODIFIED copy of the official build: keep it for personal use only,");
        Console.WriteLine("do not redistribute it, and never share a copy that contains upstream credentials.");
        return 0;
    }

    private static void PrintHelp()
    {
        Console.WriteLine($"""
{ToolName} {ToolVersion} - remove the mainland-China region gate from a local copy of PCL CE.

Usage:
  {ToolName} [options] <official-pcl-ce.exe>

Options:
  -o, --output <path>   output path (default: <input>.patched.exe next to the input)
  -c, --check           only check whether the input is already patched (exit 0 = patched)
  -h, --help            show this help
  -v, --version         show version

Exit codes:
  0   success / already patched (with --check)
  1   not patched (with --check)
  2   invalid arguments or missing input
  3   unsupported input file
  10  unexpected error

Notes:
  * Only official single-file PCL CE builds with an IL-only main assembly are supported.
  * The tool does not extract, embed or modify any credentials.
  * The patched file is a modified copy of the upstream software.
    Do not redistribute it. See DISCLAIMER.md.
""");
    }
}
