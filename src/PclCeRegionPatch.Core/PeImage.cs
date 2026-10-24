using System;
using System.Collections.Generic;

namespace PclCeRegionPatch;

/// <summary>
///     Minimal PE parser: enough to map RVAs to file offsets and to read the CLR header flags.
/// </summary>
internal sealed class PeImage
{
    private readonly struct Section
    {
        public readonly uint VirtualAddress;
        public readonly uint VirtualSize;
        public readonly uint PointerToRawData;
        public readonly uint SizeOfRawData;

        public Section(uint virtualAddress, uint virtualSize, uint pointerToRawData, uint sizeOfRawData)
        {
            VirtualAddress = virtualAddress;
            VirtualSize = virtualSize;
            PointerToRawData = pointerToRawData;
            SizeOfRawData = sizeOfRawData;
        }
    }

    private readonly List<Section> _sections;

    private PeImage(List<Section> sections, int imageSize, uint corFlags)
    {
        _sections = sections;
        ImageSize = imageSize;
        CorFlags = corFlags;
    }

    /// <summary>Computed size of the PE file (headers + all raw sections).</summary>
    public int ImageSize { get; }

    public int SectionCount => _sections.Count;

    /// <summary>CLR header flags (IMAGE_COR20_HEADER.Flags).</summary>
    public uint CorFlags { get; }

    /// <summary>COMIMAGE_FLAGS_ILONLY: the assembly contains only IL (no ReadyToRun native code).</summary>
    public bool IsIlOnly => (CorFlags & 0x1) != 0;

    /// <summary>Non-zero when the image carries a managed native header (e.g. ReadyToRun).</summary>
    public bool HasManagedNativeHeader { get; private set; }

    /// <summary>Maps a method RVA to an offset relative to the start of the PE file.</summary>
    public int RvaToFileOffset(long rva)
    {
        foreach (var section in _sections)
        {
            long size = Math.Max(section.VirtualSize, section.SizeOfRawData);
            if (rva >= section.VirtualAddress && rva < section.VirtualAddress + size)
                return (int)(section.PointerToRawData + (rva - section.VirtualAddress));
        }

        throw new InvalidOperationException($"RVA 0x{rva:X} is not mapped by any PE section.");
    }

    /// <summary>Parses a PE image that starts at <paramref name="start"/> inside <paramref name="data"/>.</summary>
    public static PeImage Parse(byte[] data, int start)
    {
        if (start < 0 || start + 0x40 > data.Length)
            throw new InvalidOperationException("truncated DOS header");
        if (data[start] != (byte)'M' || data[start + 1] != (byte)'Z')
            throw new InvalidOperationException("missing MZ signature");

        var peOffset = BitConverter.ToInt32(data, start + 0x3C);
        if (peOffset < 0x40 || start + peOffset + 0x18 > data.Length)
            throw new InvalidOperationException("invalid e_lfanew");
        if (data[start + peOffset] != (byte)'P' || data[start + peOffset + 1] != (byte)'E' ||
            data[start + peOffset + 2] != 0 || data[start + peOffset + 3] != 0)
            throw new InvalidOperationException("missing PE signature");

        var numberOfSections = BitConverter.ToUInt16(data, start + peOffset + 6);
        var sizeOfOptionalHeader = BitConverter.ToUInt16(data, start + peOffset + 20);
        if (numberOfSections == 0 || numberOfSections > 96 || sizeOfOptionalHeader == 0)
            throw new InvalidOperationException("suspicious PE header");

        var optionalHeaderOffset = start + peOffset + 24;
        if (BitConverter.ToUInt16(data, optionalHeaderOffset) is not (0x10B or 0x20B))
            throw new InvalidOperationException("unknown optional header magic");

        var isPe32Plus = BitConverter.ToUInt16(data, optionalHeaderOffset) == 0x20B;
        var dataDirectoryOffset = optionalHeaderOffset + (isPe32Plus ? 112 : 96);
        var sectionHeaderOffset = optionalHeaderOffset + sizeOfOptionalHeader;

        var sections = new List<Section>(numberOfSections);
        long imageSize = sectionHeaderOffset - start;
        for (var i = 0; i < numberOfSections; i++)
        {
            var sectionOffset = sectionHeaderOffset + i * 40;
            if (sectionOffset + 40 > data.Length)
                throw new InvalidOperationException("truncated section table");

            var virtualSize = BitConverter.ToUInt32(data, sectionOffset + 8);
            var virtualAddress = BitConverter.ToUInt32(data, sectionOffset + 12);
            var sizeOfRawData = BitConverter.ToUInt32(data, sectionOffset + 16);
            var pointerToRawData = BitConverter.ToUInt32(data, sectionOffset + 20);
            if (virtualSize > int.MaxValue || sizeOfRawData > int.MaxValue)
                throw new InvalidOperationException("section too large");

            sections.Add(new Section(virtualAddress, virtualSize, pointerToRawData, sizeOfRawData));
            var end = (long)pointerToRawData + sizeOfRawData;
            if (end > imageSize) imageSize = end;
        }

        var image = new PeImage(sections, checked((int)imageSize), 0);

        // IMAGE_DIRECTORY_ENTRY_COM_DESCRIPTOR = 14
        if (dataDirectoryOffset + 15 * 8 > data.Length)
            throw new InvalidOperationException("truncated data directory");

        var cliHeaderRva = BitConverter.ToUInt32(data, dataDirectoryOffset + 14 * 8);
        if (cliHeaderRva == 0)
            throw new InvalidOperationException("not a .NET assembly (no CLI header)");

        var cliHeaderOffset = start + image.RvaToFileOffset(cliHeaderRva);
        if (cliHeaderOffset + 72 > data.Length)
            throw new InvalidOperationException("truncated CLI header");

        var corFlags = BitConverter.ToUInt32(data, cliHeaderOffset + 16);
        var managedNativeHeaderRva = BitConverter.ToUInt32(data, cliHeaderOffset + 64);
        var result = new PeImage(sections, image.ImageSize, corFlags)
        {
            HasManagedNativeHeader = managedNativeHeaderRva != 0
        };
        return result;
    }
}
