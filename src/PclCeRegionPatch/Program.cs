using System;
using System.IO;

namespace PclCeRegionPatch;

internal static class Program
{
    private const string ToolName = "pcl-ce-regionpatch";
    private const string ToolVersion = "1.2.1";

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

        PatchInspection inspection;
        try
        {
            inspection = PatchEngine.Inspect(data);
        }
        catch (PatchNotSupportedException ex)
        {
            Console.Error.WriteLine($"error: {ex.Message}");
            return 3;
        }

        Console.WriteLine($"target    : {inspection.ProductName} {inspection.Version}");
        foreach (var detail in inspection.Details)
            Console.WriteLine($"info      : {detail}");

        if (checkOnly)
        {
            Console.WriteLine(inspection.IsPatched
                ? "status    : PATCHED (the download restriction is disabled)"
                : "status    : NOT PATCHED (the download restriction is active)");
            return inspection.IsPatched ? 0 : 1;
        }

        output ??= Path.Combine(Path.GetDirectoryName(Path.GetFullPath(input))!,
            Path.GetFileNameWithoutExtension(input) + ".patched.exe");
        output = Path.GetFullPath(output);

        if (string.Equals(Path.GetFullPath(input), output, StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("input and output must be different files");

        PatchResult result;
        try
        {
            result = PatchEngine.Apply(data);
        }
        catch (PatchNotSupportedException ex)
        {
            Console.Error.WriteLine($"error: {ex.Message}");
            return 3;
        }

        File.WriteAllBytes(output, result.Output);
        Console.WriteLine($"output    : {output} ({result.Output.Length:N0} bytes)");
        foreach (var step in result.Steps) Console.WriteLine($"step      : {step}");
        foreach (var line in result.Verification) Console.WriteLine($"verify    : {line}");
        Console.WriteLine();
        Console.WriteLine("done. The output is a MODIFIED copy of the official build: keep it for personal use only,");
        Console.WriteLine("do not redistribute it, and never share a copy that contains upstream credentials.");
        return 0;
    }

    private static void PrintHelp()
    {
        Console.WriteLine($"""
{ToolName} {ToolVersion} - remove the mainland-China download restriction from a local copy of PCL.

Supported builds:
  * PCL CE (community edition) official single-file exe
  * PCL official (.NET Framework) official exe, version 2.99.0.0 currently tested

Usage:
  {ToolName} [options] <official-pcl.exe>

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
  * PCL CE: only single-file builds with an IL-only main assembly are supported.
  * PCL official: the restriction was implemented with a compile-time constant, so this tool
    rebuilds the download-page entry points. Modpack installation entry points were removed by
    the upstream compiler and are NOT restored.
  * The tool does not extract, embed or modify any credentials.
  * The patched file is a modified copy of the upstream software.
    Do not redistribute it. See DISCLAIMER.md.
""");
    }
}
