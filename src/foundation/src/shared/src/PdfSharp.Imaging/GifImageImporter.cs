// PDFsharp - A .NET library for processing PDF
// See the LICENSE file in the solution root for more information.

namespace PdfSharp.Internal.Imaging
{
    /// <summary>
    /// Imports GIF images (GIF87a and GIF89a). The first image of the file is used,
    /// i.e. the first frame of an animated GIF. Interlaced images and transparency are supported.
    /// The image is converted to an indexed image with 8 bits per pixel and an alpha mask if it has transparent pixels.
    /// </summary>
    sealed class GifImageImporter : BitmapImageImporter
    {
        public override bool TryImport(byte[] bitmap, [MaybeNullWhen(false)] out ImportedImage importedImage)
        {
            importedImage = null!;
            if (!IsGif(bitmap))
                return false;

            try
            {
                importedImage = Decode(bitmap);
                return true;
            }
            catch (Exception ex)
            {
                // Eat exceptions to have this image importer skipped.
                LogTryOpenFailure(nameof(GifImageImporter), ex);
            }
            return false;
        }

        static bool IsGif(byte[] data)
            => data.Length >= 13 && data[0] == 'G' && data[1] == 'I' && data[2] == 'F' && data[3] == '8'
               && (data[4] == '7' || data[4] == '9') && data[5] == 'a';

        static ImportedGifImage Decode(byte[] data)
        {
            var reader = new Reader(data, 6);

            // Logical screen descriptor.
            int screenWidth = reader.Word();
            int screenHeight = reader.Word();
            int flags = reader.Byte();
            int backgroundIndex = reader.Byte();
            reader.Byte(); // Pixel aspect ratio, ignored like most applications do.
            if (screenWidth == 0 || screenHeight == 0)
                throw new InvalidOperationException("GIF image has no size.");
            CheckSize(screenWidth, screenHeight);

            byte[]? globalColorTable = (flags & 0x80) != 0 ? reader.Bytes(3 << ((flags & 7) + 1)) : null;

            int transparentIndex = -1;
            while (true)
            {
                int blockType = reader.Byte();
                switch (blockType)
                {
                    case 0x21: // Extension.
                        {
                            int label = reader.Byte();
                            if (label == 0xF9)
                            {
                                // Graphic control extension: the transparent color applies to the next image.
                                var block = reader.SubBlock();
                                if (block.Length >= 4 && (block[0] & 1) != 0)
                                    transparentIndex = block[3];
                                reader.SkipSubBlocks();
                            }
                            else
                            {
                                reader.SkipSubBlocks();
                            }
                            break;
                        }

                    case 0x2C: // Image descriptor: the first image is used.
                        return DecodeImage(data, ref reader, screenWidth, screenHeight, globalColorTable, backgroundIndex, transparentIndex);

                    default: // Trailer (0x3B) or garbage before any image.
                        throw new InvalidOperationException("GIF file contains no image.");
                }
            }
        }

        static ImportedGifImage DecodeImage(byte[] data, ref Reader reader, int screenWidth, int screenHeight,
            byte[]? globalColorTable, int backgroundIndex, int transparentIndex)
        {
            int left = reader.Word();
            int top = reader.Word();
            int width = reader.Word();
            int height = reader.Word();
            int flags = reader.Byte();
            bool interlaced = (flags & 0x40) != 0;
            byte[]? localColorTable = (flags & 0x80) != 0 ? reader.Bytes(3 << ((flags & 7) + 1)) : null;
            CheckSize(width, height);

            int minCodeSize = reader.Byte();
            if (minCodeSize is < 1 or > 11)
                throw new InvalidOperationException($"Invalid LZW code size {minCodeSize} in GIF image.");

            var colorTable = localColorTable ?? globalColorTable ?? DefaultColorTable(minCodeSize);
            int colors = colorTable.Length / 3;

            var indices = DecodeLzw(reader.ConcatenateSubBlocks(), minCodeSize, width * height);
            if (interlaced)
                indices = Deinterlace(indices, width, height);

            // Compose the image on the logical screen. Pixels outside the image are transparent.
            bool coversScreen = left == 0 && top == 0 && width >= screenWidth && height >= screenHeight;
            var pixels = new byte[screenWidth * screenHeight];
            byte[]? alpha = null;
            if (!coversScreen)
            {
                alpha = new byte[pixels.Length];
                if (backgroundIndex < colors)
                    Array.Fill(pixels, (byte)backgroundIndex);
            }

            for (int y = 0; y < height; y++)
            {
                int screenY = top + y;
                if (screenY >= screenHeight)
                    break;
                for (int x = 0; x < width; x++)
                {
                    int screenX = left + x;
                    if (screenX >= screenWidth)
                        break;
                    int index = indices[y * width + x];
                    int target = screenY * screenWidth + screenX;
                    if (index == transparentIndex)
                    {
                        alpha ??= CreateOpaqueMask(pixels.Length);
                        alpha[target] = 0;
                        continue;
                    }
                    // Indices beyond the color table are invalid. Use the first color like most viewers do.
                    pixels[target] = (byte)(index < colors ? index : 0);
                    if (alpha != null)
                        alpha[target] = 255;
                }
            }

            var image = new ImportedGifImage
            {
                ImageData = data,
                ImageFormat = ImageFormats.Indexed8,
                PixelWidth = screenWidth,
                PixelHeight = screenHeight,
                ExtractedImageData = pixels,
                PaletteData = colorTable,
                PaletteColors = colors,
                ColorsUsed = colors,
                BitCount = 8,
                AlphaMaskData = alpha
            };
            image.Width = screenWidth / image.DpiX * 72;
            image.Height = screenHeight / image.DpiY * 72;
            return image;
        }

        static byte[] CreateOpaqueMask(int length)
        {
            var mask = new byte[length];
            Array.Fill(mask, (byte)255);
            return mask;
        }

        /// <summary>
        /// The variable-length LZW decoding of GIF: codes are packed with the least significant bit first,
        /// start with minCodeSize + 1 bits, and grow up to 12 bits.
        /// </summary>
        internal static byte[] DecodeLzw(byte[] data, int minCodeSize, int pixelCount)
        {
            const int maxCodes = 4096;
            var prefix = new short[maxCodes];
            var suffix = new byte[maxCodes];
            var first = new byte[maxCodes];
            var length = new short[maxCodes];

            int clearCode = 1 << minCodeSize;
            int endCode = clearCode + 1;
            for (int code = 0; code < clearCode; code++)
            {
                prefix[code] = -1;
                suffix[code] = (byte)code;
                first[code] = (byte)code;
                length[code] = 1;
            }

            var output = new byte[pixelCount];
            int position = 0;
            int codeSize = minCodeSize + 1;
            int nextCode = endCode + 1;
            int previousCode = -1;

            long bitBuffer = 0;
            int bitCount = 0;
            int dataIndex = 0;

            while (position < pixelCount)
            {
                while (bitCount < codeSize && dataIndex < data.Length)
                {
                    bitBuffer |= (long)data[dataIndex++] << bitCount;
                    bitCount += 8;
                }
                if (bitCount < codeSize)
                    break;  // Truncated data: the remaining pixels keep index 0.

                int code = (int)(bitBuffer & ((1 << codeSize) - 1));
                bitBuffer >>= codeSize;
                bitCount -= codeSize;

                if (code == clearCode)
                {
                    codeSize = minCodeSize + 1;
                    nextCode = endCode + 1;
                    previousCode = -1;
                    continue;
                }
                if (code == endCode)
                    break;

                if (previousCode == -1)
                {
                    if (code >= clearCode)
                        break;  // Invalid first code.
                    output[position++] = (byte)code;
                    previousCode = code;
                    continue;
                }

                int firstByte;
                if (code < nextCode)
                {
                    firstByte = first[code];
                    position = WriteString(code, output, position, prefix, suffix, length);
                }
                else if (code == nextCode && nextCode < maxCodes)
                {
                    // The KwKwK case: the code is the previous string plus its own first byte.
                    firstByte = first[previousCode];
                    position = WriteString(previousCode, output, position, prefix, suffix, length);
                    if (position < pixelCount)
                        output[position++] = (byte)firstByte;
                }
                else
                {
                    break;  // Invalid code.
                }

                if (nextCode < maxCodes)
                {
                    prefix[nextCode] = (short)previousCode;
                    suffix[nextCode] = (byte)firstByte;
                    first[nextCode] = first[previousCode];
                    length[nextCode] = (short)(length[previousCode] + 1);
                    nextCode++;
                    if (nextCode == 1 << codeSize && codeSize < 12)
                        codeSize++;
                }
                previousCode = code;
            }
            return output;
        }

        static int WriteString(int code, byte[] output, int position, short[] prefix, byte[] suffix, short[] length)
        {
            int count = length[code];
            int end = position + count;
            // Write from the last byte backwards, following the prefix chain. Bytes beyond the image are dropped.
            for (int idx = end - 1; idx >= position; idx--)
            {
                if (idx < output.Length)
                    output[idx] = suffix[code];
                code = prefix[code];
            }
            return Math.Min(end, output.Length);
        }

        static byte[] Deinterlace(byte[] indices, int width, int height)
        {
            var result = new byte[indices.Length];
            int source = 0;
            // Pass 1: every 8th row from row 0; pass 2: every 8th row from row 4;
            // pass 3: every 4th row from row 2; pass 4: every 2nd row from row 1.
            foreach (var (start, step) in new[] { (0, 8), (4, 8), (2, 4), (1, 2) })
            {
                for (int y = start; y < height; y += step)
                {
                    Array.Copy(indices, source, result, y * width, width);
                    source += width;
                }
            }
            return result;
        }

        static byte[] DefaultColorTable(int minCodeSize)
        {
            // GIF files without color table are rare. Use a gray ramp.
            int colors = Math.Min(1 << minCodeSize, 256);
            var table = new byte[colors * 3];
            for (int idx = 0; idx < colors; idx++)
                table[3 * idx] = table[3 * idx + 1] = table[3 * idx + 2] = (byte)(idx * 255 / Math.Max(colors - 1, 1));
            return table;
        }

        static void CheckSize(int width, int height)
        {
            if ((long)width * height > MaxPixels)
                throw new InvalidOperationException($"GIF image with {width} × {height} pixels is too large.");
        }

        // 2^28 pixels, e.g. 16384 × 16384. GIF allows 65535 × 65535, which would need 4 GB.
        const long MaxPixels = 1L << 28;

        /// <summary>
        /// Reads the GIF structure. Throws IndexOutOfRangeException for truncated files, which TryImport handles.
        /// </summary>
        struct Reader(byte[] data, int position)
        {
            public int Byte() => data[position++];

            public int Word()
            {
                int value = data[position] | data[position + 1] << 8;
                position += 2;
                return value;
            }

            public byte[] Bytes(int count)
            {
                if (position + count > data.Length)
                    throw new InvalidOperationException("Truncated GIF file.");
                var bytes = data.AsSpan(position, count).ToArray();
                position += count;
                return bytes;
            }

            public byte[] SubBlock() => Bytes(Byte());

            public void SkipSubBlocks()
            {
                int size;
                while ((size = Byte()) != 0)
                    position += size;
            }

            public byte[] ConcatenateSubBlocks()
            {
                var result = new MemoryStream();
                // A truncated last block is used as far as it goes.
                while (position < data.Length)
                {
                    int size = data[position++];
                    if (size == 0)
                        break;
                    int count = Math.Min(size, data.Length - position);
                    result.Write(data, position, count);
                    position += count;
                }
                return result.ToArray();
            }
        }
    }
}
