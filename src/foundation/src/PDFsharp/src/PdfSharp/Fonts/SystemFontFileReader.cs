// PDFsharp - A .NET library for processing PDF
// See the LICENSE file in the solution root for more information.

using System.Buffers.Binary;
using System.Text;
using Microsoft.Win32.SafeHandles;

namespace PdfSharp.Fonts
{
    /// <summary>
    /// A font face in a font file, as indexed by the <see cref="SystemFontResolver"/>.
    /// </summary>
    sealed class SystemFontFace
    {
        /// <summary>
        /// The face name that SystemFontResolver.ResolveTypeface returns and GetFont receives.
        /// It is unique for each face of each file.
        /// </summary>
        public required string FaceName { get; init; }

        /// <summary>
        /// The full path of the font file.
        /// </summary>
        public required string FilePath { get; init; }

        /// <summary>
        /// The index of the face in a font collection (.ttc), or -1 if the file is a single font.
        /// </summary>
        public required int CollectionIndex { get; init; }

        /// <summary>
        /// The family names (name ID 1) in all languages of the font. A family of this kind has
        /// up to four members: regular, bold, italic, and bold italic.
        /// </summary>
        public required IReadOnlyList<string> FamilyNames { get; init; }

        /// <summary>
        /// The typographic family names (name ID 16), which group faces of all weights, e.g. "Noto Sans" for
        /// "Noto Sans SemiBold". Empty if the font has none or if they equal the family names.
        /// </summary>
        public required IReadOnlyList<string> TypographicFamilyNames { get; init; }

        /// <summary>
        /// The full names (name ID 4), e.g. "Arial Bold Italic".
        /// </summary>
        public required IReadOnlyList<string> FullNames { get; init; }

        /// <summary>
        /// The weight from 1 to 1000 (OS/2 usWeightClass). 400 is regular, 700 is bold.
        /// </summary>
        public required int Weight { get; init; }

        /// <summary>
        /// True if the face is the bold member of its family.
        /// </summary>
        public required bool IsBold { get; init; }

        /// <summary>
        /// True if the face is italic or oblique.
        /// </summary>
        public required bool IsItalic { get; init; }

        /// <summary>
        /// True if the font is a variable font. PDFsharp uses its default instance.
        /// </summary>
        public required bool IsVariable { get; init; }

        public override string ToString() => $"{FullNames.FirstOrDefault() ?? FamilyNames[0]} (weight {Weight}) in {FilePath}";
    }

    /// <summary>
    /// Reads the information that the SystemFontResolver needs from font files and extracts faces from font collections.
    /// Only the table directory and the small name, OS/2, and head tables are read, not the whole font.
    /// Damaged files are ignored, never cause exceptions.
    /// </summary>
    static class SystemFontFileReader
    {
        const uint TagCollection = 0x74746366;   // 'ttcf': TrueType collection.
        const uint VersionTrueType = 0x00010000; // TrueType outlines.
        const uint TagTrue = 0x74727565;         // 'true': TrueType outlines, used by Apple.
        // 'OTTO' marks OpenType fonts with CFF (PostScript) outlines, which PDFsharp cannot embed.

        const int MaxFacesInCollection = 1024;
        const int MaxTableCount = 512;
        const int MaxNameTableLength = 1 << 20;

        // The tables PDFsharp needs to embed a font. Fonts with CFF outlines have no glyf and loca tables.
        static readonly uint[] RequiredTables =
            [Tag("cmap"), Tag("glyf"), Tag("head"), Tag("hhea"), Tag("hmtx"), Tag("loca"), Tag("maxp"), Tag("name")];

        static readonly uint TagName = Tag("name");
        static readonly uint TagOs2 = Tag("OS/2");
        static readonly uint TagHead = Tag("head");
        static readonly uint TagFvar = Tag("fvar");

        /// <summary>
        /// Reads the faces of a font file that PDFsharp can use.
        /// Returns an empty list if the file is no TrueType font or collection, has CFF outlines, or is damaged.
        /// </summary>
        /// <param name="path">The full path of the file.</param>
        /// <param name="faceNamePrefix">The prefix of the face names.</param>
        public static List<SystemFontFace> ReadFaces(string path, string faceNamePrefix)
        {
            var faces = new List<SystemFontFace>();
            using var file = File.OpenHandle(path, FileMode.Open, FileAccess.Read, FileShare.Read, FileOptions.RandomAccess);
            var length = RandomAccess.GetLength(file);

            Span<byte> header = stackalloc byte[12];
            if (!TryRead(file, length, 0, header))
                return faces;

            var tag = BinaryPrimitives.ReadUInt32BigEndian(header);
            if (tag == TagCollection)
            {
                var count = BinaryPrimitives.ReadUInt32BigEndian(header[8..]);
                if (count is 0 or > MaxFacesInCollection)
                    return faces;

                var offsets = new byte[count * 4];
                if (!TryRead(file, length, 12, offsets))
                    return faces;

                for (int index = 0; index < count; index++)
                {
                    var offset = BinaryPrimitives.ReadUInt32BigEndian(offsets.AsSpan(index * 4));
                    var face = ReadFace(file, length, offset, path, index, faceNamePrefix);
                    if (face != null)
                        faces.Add(face);
                }
            }
            else
            {
                var face = ReadFace(file, length, 0, path, -1, faceNamePrefix);
                if (face != null)
                    faces.Add(face);
            }
            return faces;
        }

        static SystemFontFace? ReadFace(SafeFileHandle file, long fileLength, long offset, string path, int collectionIndex, string faceNamePrefix)
        {
            Span<byte> header = stackalloc byte[12];
            if (!TryRead(file, fileLength, offset, header))
                return null;

            var version = BinaryPrimitives.ReadUInt32BigEndian(header);
            if (version is not (VersionTrueType or TagTrue))
                return null;

            int tableCount = BinaryPrimitives.ReadUInt16BigEndian(header[4..]);
            if (tableCount is 0 or > MaxTableCount)
                return null;

            // Table records: tag, checksum, offset, length. The offsets are relative to the start of the file,
            // also in font collections.
            var directory = new byte[tableCount * 16];
            if (!TryRead(file, fileLength, offset + 12, directory))
                return null;

            var tables = new Dictionary<uint, (long Offset, long Length)>(tableCount);
            for (int idx = 0; idx < tableCount; idx++)
            {
                var record = directory.AsSpan(idx * 16, 16);
                tables[BinaryPrimitives.ReadUInt32BigEndian(record)] =
                    (BinaryPrimitives.ReadUInt32BigEndian(record[8..]), BinaryPrimitives.ReadUInt32BigEndian(record[12..]));
            }
            foreach (var required in RequiredTables)
            {
                if (!tables.ContainsKey(required))
                    return null;
            }

            var names = ReadNames(file, fileLength, tables[TagName]);
            if (!names.TryGetValue(1, out var familyNames))
                return null;
            var typographicFamilyNames = names.TryGetValue(16, out var typographic)
                ? typographic.Where(name => !familyNames.Contains(name, StringComparer.OrdinalIgnoreCase)).ToList()
                : [];

            ReadStyle(file, fileLength, tables, out var weight, out var isBold, out var isItalic);

            return new SystemFontFace
            {
                FaceName = collectionIndex < 0 ? faceNamePrefix + path : $"{faceNamePrefix}{path}#{collectionIndex}",
                FilePath = path,
                CollectionIndex = collectionIndex,
                FamilyNames = familyNames,
                TypographicFamilyNames = typographicFamilyNames,
                FullNames = names.TryGetValue(4, out var fullNames) ? fullNames : [],
                Weight = weight,
                IsBold = isBold,
                IsItalic = isItalic,
                IsVariable = tables.ContainsKey(TagFvar)
            };
        }

        /// <summary>
        /// Reads the family names (ID 1), full names (ID 4), and typographic family names (ID 16) in all languages.
        /// </summary>
        static Dictionary<int, List<string>> ReadNames(SafeFileHandle file, long fileLength, (long Offset, long Length) table)
        {
            var names = new Dictionary<int, List<string>>();
            if (table.Length is < 6 or > MaxNameTableLength)
                return names;

            var data = new byte[table.Length];
            if (!TryRead(file, fileLength, table.Offset, data))
                return names;

            int count = BinaryPrimitives.ReadUInt16BigEndian(data.AsSpan(2));
            int storageOffset = BinaryPrimitives.ReadUInt16BigEndian(data.AsSpan(4));
            for (int idx = 0; idx < count; idx++)
            {
                int recordOffset = 6 + idx * 12;
                if (recordOffset + 12 > data.Length)
                    break;

                var record = data.AsSpan(recordOffset, 12);
                int platformId = BinaryPrimitives.ReadUInt16BigEndian(record);
                int encodingId = BinaryPrimitives.ReadUInt16BigEndian(record[2..]);
                int languageId = BinaryPrimitives.ReadUInt16BigEndian(record[4..]);
                int nameId = BinaryPrimitives.ReadUInt16BigEndian(record[6..]);
                int length = BinaryPrimitives.ReadUInt16BigEndian(record[8..]);
                int start = storageOffset + BinaryPrimitives.ReadUInt16BigEndian(record[10..]);
                if (nameId is not (1 or 4 or 16) || start + length > data.Length)
                    continue;

                var value = platformId switch
                {
                    // Unicode and Windows platform: UTF-16 big endian, also for symbol fonts.
                    0 or 3 => Encoding.BigEndianUnicode.GetString(data, start, length),
                    // Macintosh platform, Roman encoding, English: the names are ASCII in practice.
                    1 when encodingId == 0 && languageId == 0 => Encoding.Latin1.GetString(data, start, length),
                    _ => null
                };
                value = value?.Trim('\0', ' ');
                if (String.IsNullOrEmpty(value))
                    continue;

                if (!names.TryGetValue(nameId, out var list))
                    names[nameId] = list = [];
                if (!list.Contains(value, StringComparer.OrdinalIgnoreCase))
                    list.Add(value);
            }
            return names;
        }

        /// <summary>
        /// Reads weight, bold, and italic from the OS/2 table, or from the head table if the font has no OS/2 table.
        /// </summary>
        static void ReadStyle(SafeFileHandle file, long fileLength, Dictionary<uint, (long Offset, long Length)> tables,
            out int weight, out bool isBold, out bool isItalic)
        {
            weight = 400;
            isBold = false;
            isItalic = false;

            Span<byte> data = stackalloc byte[64];
            if (tables.TryGetValue(TagOs2, out var os2) && os2.Length >= 64 && TryRead(file, fileLength, os2.Offset, data))
            {
                weight = BinaryPrimitives.ReadUInt16BigEndian(data[4..]);
                int fsSelection = BinaryPrimitives.ReadUInt16BigEndian(data[62..]);
                isItalic = (fsSelection & 0x0201) != 0; // ITALIC (bit 0) or OBLIQUE (bit 9).
                isBold = (fsSelection & 0x0020) != 0;   // BOLD (bit 5).
            }
            else if (tables.TryGetValue(TagHead, out var head) && head.Length >= 54 && TryRead(file, fileLength, head.Offset, data[..54]))
            {
                int macStyle = BinaryPrimitives.ReadUInt16BigEndian(data[44..]);
                isBold = (macStyle & 1) != 0;
                isItalic = (macStyle & 2) != 0;
                weight = isBold ? 700 : 400;
            }

            // Some old fonts use the values 1 to 9.
            if (weight is > 0 and < 10)
                weight *= 100;
            if (weight is <= 0 or > 1000)
                weight = isBold ? 700 : 400;
        }

        /// <summary>
        /// Creates a standalone font from a face of a TrueType collection.
        /// The tables are copied, the table offsets and the checksum adjustment of the head table are recalculated.
        /// </summary>
        public static byte[] ExtractFromCollection(byte[] collection, int index)
        {
            var source = collection.AsSpan();
            if (source.Length < 12 || BinaryPrimitives.ReadUInt32BigEndian(source) != TagCollection)
                throw new InvalidOperationException("The data is no TrueType collection.");

            var count = BinaryPrimitives.ReadUInt32BigEndian(source[8..]);
            if (index < 0 || index >= count || 12 + 4L * count > source.Length)
                throw new InvalidOperationException($"The TrueType collection has no face with index {index}.");

            int offset = CheckedInt(BinaryPrimitives.ReadUInt32BigEndian(source[(12 + 4 * index)..]), source.Length - 12);
            int tableCount = BinaryPrimitives.ReadUInt16BigEndian(source[(offset + 4)..]);
            int directoryLength = 16 * tableCount;
            if (offset + 12 + directoryLength > source.Length)
                throw new InvalidOperationException("The table directory exceeds the TrueType collection.");

            // Compute the size of the new font. Each table starts at a multiple of 4.
            long size = 12 + directoryLength;
            for (int idx = 0; idx < tableCount; idx++)
            {
                var record = source.Slice(offset + 12 + idx * 16, 16);
                long tableOffset = BinaryPrimitives.ReadUInt32BigEndian(record[8..]);
                long tableLength = BinaryPrimitives.ReadUInt32BigEndian(record[12..]);
                if (tableOffset + tableLength > source.Length)
                    throw new InvalidOperationException("A table exceeds the TrueType collection.");
                size += (tableLength + 3) & ~3L;
            }

            var font = new byte[CheckedInt(size, Int32.MaxValue)];
            // Copy sfnt version, table count, search range, entry selector, and range shift.
            source.Slice(offset, 12).CopyTo(font);

            int position = 12 + directoryLength;
            int headPosition = -1;
            for (int idx = 0; idx < tableCount; idx++)
            {
                var record = source.Slice(offset + 12 + idx * 16, 16);
                var target = font.AsSpan(12 + idx * 16, 16);
                record[..8].CopyTo(target);  // Tag and checksum.
                BinaryPrimitives.WriteUInt32BigEndian(target[8..], (uint)position);
                record[12..].CopyTo(target[12..]);  // Length.

                int tableOffset = (int)BinaryPrimitives.ReadUInt32BigEndian(record[8..]);
                int tableLength = (int)BinaryPrimitives.ReadUInt32BigEndian(record[12..]);
                source.Slice(tableOffset, tableLength).CopyTo(font.AsSpan(position));
                if (BinaryPrimitives.ReadUInt32BigEndian(record) == TagHead && tableLength >= 12)
                    headPosition = position;
                position += (tableLength + 3) & ~3;
            }

            // The checksum adjustment in the head table makes the checksum of the whole file 0xB1B0AFBA.
            if (headPosition >= 0)
            {
                var adjustment = font.AsSpan(headPosition + 8, 4);
                BinaryPrimitives.WriteUInt32BigEndian(adjustment, 0);
                uint sum = 0;
                for (int idx = 0; idx + 4 <= font.Length; idx += 4)
                    sum = unchecked(sum + BinaryPrimitives.ReadUInt32BigEndian(font.AsSpan(idx)));
                BinaryPrimitives.WriteUInt32BigEndian(adjustment, unchecked(0xB1B0AFBA - sum));
            }
            return font;
        }

        static int CheckedInt(long value, long max)
        {
            if (value < 0 || value > max)
                throw new InvalidOperationException("Invalid offset or size in TrueType collection.");
            return (int)value;
        }

        static bool TryRead(SafeFileHandle file, long fileLength, long offset, Span<byte> buffer)
        {
            if (offset < 0 || offset + buffer.Length > fileLength)
                return false;
            while (buffer.Length > 0)
            {
                int read = RandomAccess.Read(file, buffer, offset);
                if (read <= 0)
                    return false;
                buffer = buffer[read..];
                offset += read;
            }
            return true;
        }

        static uint Tag(string tag) => (uint)(tag[0] << 24 | tag[1] << 16 | tag[2] << 8 | tag[3]);
    }
}
