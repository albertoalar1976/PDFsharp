// PDFsharp - A .NET library for processing PDF
// See the LICENSE file in the solution root for more information.

using System.Buffers.Binary;
using System.IO.Compression;

namespace PdfSharp.Internal.Imaging
{
    /// <summary>
    /// Imports TIFF images. The first page of a multi-page file is used.
    /// Supported: bilevel, gray, palette, RGB, and CMYK images with 1, 2, 4, 8, or 16 bits per sample,
    /// with or without alpha channel, in strips or tiles, chunky or planar, uncompressed or compressed with
    /// PackBits, LZW, or Deflate, with or without horizontal predictor.
    /// Bilevel images compressed with CCITT Group 4, CCITT Group 3 one-dimensional, or CCITT modified Huffman
    /// are written to PDF without decoding, because PDF supports these compressions directly.
    /// Not supported: JPEG compression, YCbCr and CIE L*a*b* images, floating point samples.
    /// </summary>
    sealed class TiffImageImporter : BitmapImageImporter
    {
        public override bool TryImport(byte[] bitmap, [MaybeNullWhen(false)] out ImportedImage importedImage)
        {
            importedImage = null!;
            if (!IsTiff(bitmap))
                return false;

            try
            {
                importedImage = Decode(bitmap);
                return true;
            }
            catch (Exception ex)
            {
                // Eat exceptions to have this image importer skipped.
                LogTryOpenFailure(nameof(TiffImageImporter), ex);
            }
            return false;
        }

        static bool IsTiff(byte[] data)
            => data.Length >= 8
               && ((data[0] == 'I' && data[1] == 'I' && data[2] == 42 && data[3] == 0)
                   || (data[0] == 'M' && data[1] == 'M' && data[2] == 0 && data[3] == 42));

        // TIFF tags.
        const int TagImageWidth = 256;
        const int TagImageLength = 257;
        const int TagBitsPerSample = 258;
        const int TagCompression = 259;
        const int TagPhotometric = 262;
        const int TagFillOrder = 266;
        const int TagStripOffsets = 273;
        const int TagSamplesPerPixel = 277;
        const int TagRowsPerStrip = 278;
        const int TagStripByteCounts = 279;
        const int TagXResolution = 282;
        const int TagYResolution = 283;
        const int TagPlanarConfiguration = 284;
        const int TagT4Options = 292;
        const int TagResolutionUnit = 296;
        const int TagPredictor = 317;
        const int TagColorMap = 320;
        const int TagTileWidth = 322;
        const int TagTileLength = 323;
        const int TagTileOffsets = 324;
        const int TagTileByteCounts = 325;
        const int TagInkSet = 332;
        const int TagExtraSamples = 338;
        const int TagSampleFormat = 339;

        // Compressions.
        const int CompressionNone = 1;
        const int CompressionCcittRle = 2;
        const int CompressionCcittT4 = 3;
        const int CompressionCcittT6 = 4;
        const int CompressionLzw = 5;
        const int CompressionDeflate = 8;
        const int CompressionPackBits = 32773;
        const int CompressionDeflateObsolete = 32946;

        // Photometric interpretations.
        const int WhiteIsZero = 0;
        const int BlackIsZero = 1;
        const int Rgb = 2;
        const int Palette = 3;
        const int Separated = 5;

        // The decoded data of an image must not exceed 1 GiB, e.g. 16384 × 16384 RGBA.
        const long MaxDecodedBytes = 1L << 30;

        static ImportedImage Decode(byte[] data)
        {
            var ifd = new Ifd(data);

            int width = ifd.GetInt(TagImageWidth);
            int height = ifd.GetInt(TagImageLength);
            if (width <= 0 || height <= 0)
                throw new InvalidOperationException("TIFF image has no size.");

            int compression = ifd.GetInt(TagCompression, CompressionNone);
            int samplesPerPixel = ifd.GetInt(TagSamplesPerPixel, 1);
            var bitsPerSampleValues = ifd.GetInts(TagBitsPerSample) ?? [1];
            int bitsPerSample = bitsPerSampleValues[0];
            if (bitsPerSampleValues.Any(bits => bits != bitsPerSample))
                throw new NotSupportedException("TIFF images with different bits per sample are not supported.");
            if (bitsPerSample is not (1 or 2 or 4 or 8 or 16))
                throw new NotSupportedException($"TIFF images with {bitsPerSample} bits per sample are not supported.");
            if (samplesPerPixel is < 1 or > 8)
                throw new NotSupportedException($"TIFF images with {samplesPerPixel} samples per pixel are not supported.");
            if (ifd.GetInt(TagSampleFormat, 1) != 1)
                throw new NotSupportedException("TIFF images with signed integer or floating point samples are not supported.");

            int defaultPhotometric = compression is CompressionCcittRle or CompressionCcittT4 or CompressionCcittT6 ? WhiteIsZero
                : samplesPerPixel >= 3 ? Rgb : BlackIsZero;
            int photometric = ifd.GetInt(TagPhotometric, defaultPhotometric);

            var (dpiX, dpiY) = GetResolution(ifd);

            ImportedImage image = compression is CompressionCcittRle or CompressionCcittT4 or CompressionCcittT6
                ? CreateCcittImage(ifd, data, compression, width, height, samplesPerPixel, bitsPerSample, photometric)
                : CreateRasterImage(ifd, data, compression, width, height, samplesPerPixel, bitsPerSample, photometric);

            // For CCITT images, ImageData already contains the compressed data.
            if (image is not ImportedCcittImage)
                image.ImageData = data;
            image.PixelWidth = width;
            image.PixelHeight = height;
            image.DpiX = dpiX;
            image.DpiY = dpiY;
            image.Width = width / dpiX * 72;
            image.Height = height / dpiY * 72;
            return image;
        }

        static (float_ DpiX, float_ DpiY) GetResolution(Ifd ifd)
        {
            double x = ifd.GetRational(TagXResolution);
            double y = ifd.GetRational(TagYResolution);
            if (x <= 0 || y <= 0 || Double.IsNaN(x) || Double.IsNaN(y))
                return (96, 96);
            switch (ifd.GetInt(TagResolutionUnit, 2))
            {
                case 2: // Inch.
                    return ((float_)x, (float_)y);
                case 3: // Centimeter.
                    return ((float_)(x * 2.54), (float_)(y * 2.54));
                default: // No absolute unit, only the aspect ratio.
                    return (96, (float_)(96 * y / x));
            }
        }

        static ImportedCcittImage CreateCcittImage(Ifd ifd, byte[] data, int compression, int width, int height,
            int samplesPerPixel, int bitsPerSample, int photometric)
        {
            if (samplesPerPixel != 1 || bitsPerSample != 1 || photometric is not (WhiteIsZero or BlackIsZero))
                throw new NotSupportedException("CCITT compressed TIFF images must be bilevel.");
            if (ifd.Has(TagTileOffsets))
                throw new NotSupportedException("Tiled CCITT compressed TIFF images are not supported.");

            int k;
            bool byteAlign;
            switch (compression)
            {
                case CompressionCcittRle:
                    // Modified Huffman: one-dimensional coding, each row begins on a byte boundary, no EOL codes.
                    k = 0;
                    byteAlign = true;
                    break;

                case CompressionCcittT4:
                    {
                        int options = ifd.GetInt(TagT4Options, 0);
                        if ((options & 1) != 0)
                            throw new NotSupportedException("CCITT Group 3 two-dimensional TIFF images are not supported.");
                        k = 0;
                        byteAlign = (options & 4) != 0;
                        break;
                    }

                default:
                    k = -1;
                    byteAlign = false;
                    break;
            }

            var offsets = ifd.GetLongs(TagStripOffsets) ?? throw new InvalidOperationException("TIFF image has no strip offsets.");
            var counts = ifd.GetLongs(TagStripByteCounts) ?? throw new InvalidOperationException("TIFF image has no strip byte counts.");
            // In one-dimensional coding the rows are independent, so the strips can be concatenated.
            // In Group 4 coding each strip starts with a new reference line, so only one strip is possible.
            if (k < 0 && offsets.Length != 1)
                throw new NotSupportedException("CCITT Group 4 TIFF images with more than one strip are not supported.");

            var ccitt = new MemoryStream();
            for (int idx = 0; idx < offsets.Length; idx++)
                ccitt.Write(GetSegment(data, offsets[idx], idx < counts.Length ? counts[idx] : data.Length - offsets[idx]));
            var bytes = ccitt.ToArray();

            if (ifd.GetInt(TagFillOrder, 1) == 2)
            {
                // The least significant bit comes first. PDF needs the most significant bit first.
                for (int idx = 0; idx < bytes.Length; idx++)
                    bytes[idx] = ReverseBits(bytes[idx]);
            }

            return new ImportedCcittImage
            {
                ImageFormat = ImageFormats.CcittFax,
                K = k,
                EncodedByteAlign = byteAlign,
                // The CCITT white runs are white if 0 means white, i.e. for WhiteIsZero.
                Inverted = photometric == BlackIsZero,
                ImageData = bytes
            };
        }

        static ImportedTiffImage CreateRasterImage(Ifd ifd, byte[] data, int compression, int width, int height,
            int samplesPerPixel, int bitsPerSample, int photometric)
        {
            if (compression is not (CompressionNone or CompressionLzw or CompressionDeflate or CompressionDeflateObsolete or CompressionPackBits))
                throw new NotSupportedException($"TIFF compression {compression} is not supported.");

            var extraSamples = ifd.GetInts(TagExtraSamples) ?? [];
            int colorSamples = photometric switch
            {
                WhiteIsZero or BlackIsZero or Palette => 1,
                Rgb => 3,
                Separated when ifd.GetInt(TagInkSet, 1) == 1 => 4,
                _ => throw new NotSupportedException($"TIFF photometric interpretation {photometric} is not supported.")
            };
            if (samplesPerPixel < colorSamples)
                throw new InvalidOperationException("TIFF image has too few samples per pixel.");
            // ExtraSamples: 1 is associated (premultiplied) alpha, 2 is unassociated alpha. Other extra samples are ignored.
            int alphaKind = samplesPerPixel > colorSamples && extraSamples.Length > 0 ? extraSamples[0] : 0;
            bool hasAlpha = alphaKind is 1 or 2;

            var samples = new SampleData(ifd, data, compression, width, height, samplesPerPixel, bitsPerSample);
            var image = new ImportedTiffImage { BitCount = bitsPerSample * colorSamples };
            int pixelCount = width * height;
            var alpha = hasAlpha ? new byte[pixelCount] : null;

            switch (photometric)
            {
                case WhiteIsZero:
                case BlackIsZero:
                    if (bitsPerSample == 1 && !hasAlpha)
                    {
                        // Bilevel image: keep 1 bit per pixel and use a palette with black and white.
                        image.ImageFormat = ImageFormats.Indexed1;
                        image.ExtractedImageData = samples.GetPackedRows();
                        image.PaletteData = photometric == WhiteIsZero ? [255, 255, 255, 0, 0, 0] : [0, 0, 0, 255, 255, 255];
                        image.PaletteColors = image.ColorsUsed = 2;
                    }
                    else
                    {
                        image.ImageFormat = ImageFormats.Gray8;
                        var gray = new byte[pixelCount];
                        for (int y = 0, pixel = 0; y < height; y++)
                        {
                            for (int x = 0; x < width; x++, pixel++)
                            {
                                int value = samples.Get8(x, y, 0);
                                gray[pixel] = (byte)(photometric == WhiteIsZero ? 255 - value : value);
                                if (alpha != null)
                                    alpha[pixel] = (byte)samples.Get8(x, y, colorSamples);
                            }
                        }
                        if (alphaKind == 1)
                            Unpremultiply(gray, 1, alpha!);
                        image.ExtractedImageData = gray;
                    }
                    break;

                case Palette:
                    {
                        var colorMap = ifd.GetInts(TagColorMap);
                        int colors = 1 << bitsPerSample;
                        if (colorMap == null || colorMap.Length < 3 * colors || bitsPerSample == 16)
                            throw new NotSupportedException("TIFF palette image has no valid color map.");
                        var palette = new byte[3 * colors];
                        for (int idx = 0; idx < colors; idx++)
                        {
                            palette[3 * idx] = (byte)(colorMap[idx] >> 8);
                            palette[3 * idx + 1] = (byte)(colorMap[colors + idx] >> 8);
                            palette[3 * idx + 2] = (byte)(colorMap[2 * colors + idx] >> 8);
                        }
                        image.PaletteData = palette;
                        image.PaletteColors = image.ColorsUsed = colors;
                        if (bitsPerSample is 1 or 4 or 8 && !hasAlpha && samplesPerPixel == 1)
                        {
                            // The rows have the same layout as in PDF.
                            image.ImageFormat = bitsPerSample switch { 1 => ImageFormats.Indexed1, 4 => ImageFormats.Indexed4, _ => ImageFormats.Indexed8 };
                            image.ExtractedImageData = samples.GetPackedRows();
                        }
                        else
                        {
                            image.ImageFormat = ImageFormats.Indexed8;
                            var indices = new byte[pixelCount];
                            for (int y = 0, pixel = 0; y < height; y++)
                            {
                                for (int x = 0; x < width; x++, pixel++)
                                {
                                    indices[pixel] = (byte)samples.GetRaw(x, y, 0);
                                    if (alpha != null)
                                        alpha[pixel] = (byte)samples.Get8(x, y, 1);
                                }
                            }
                            image.ExtractedImageData = indices;
                        }
                        break;
                    }

                case Rgb:
                case Separated:
                    {
                        image.ImageFormat = photometric == Rgb ? (hasAlpha ? ImageFormats.Bgra32 : ImageFormats.Bgr32) : ImageFormats.Cmyk32;
                        var pixels = new byte[pixelCount * colorSamples];
                        for (int y = 0, pixel = 0; y < height; y++)
                        {
                            for (int x = 0; x < width; x++, pixel++)
                            {
                                for (int sample = 0; sample < colorSamples; sample++)
                                    pixels[pixel * colorSamples + sample] = (byte)samples.Get8(x, y, sample);
                                if (alpha != null)
                                    alpha[pixel] = (byte)samples.Get8(x, y, colorSamples);
                            }
                        }
                        if (alphaKind == 1 && photometric == Rgb)
                            Unpremultiply(pixels, colorSamples, alpha!);
                        image.ExtractedImageData = pixels;
                        break;
                    }
            }

            // Like the PNG importer: no alpha mask if all pixels are opaque.
            if (alpha != null && Array.TrueForAll(alpha, value => value == 255))
                alpha = null;
            image.AlphaMaskData = alpha;
            return image;
        }

        /// <summary>
        /// Converts colors with associated (premultiplied) alpha to unassociated alpha, as PDF soft masks require.
        /// </summary>
        static void Unpremultiply(byte[] pixels, int components, byte[] alpha)
        {
            for (int pixel = 0; pixel < alpha.Length; pixel++)
            {
                int a = alpha[pixel];
                if (a is 0 or 255)
                    continue;
                for (int c = 0; c < components; c++)
                {
                    int index = pixel * components + c;
                    pixels[index] = (byte)Math.Min(255, pixels[index] * 255 / a);  // Like Pillow.
                }
            }
        }

        static ReadOnlySpan<byte> GetSegment(byte[] data, long offset, long count)
        {
            if (offset < 0 || offset > data.Length)
                throw new InvalidOperationException("TIFF segment offset is outside the file.");
            // Some writers store wrong byte counts. Use the data that is there.
            count = Math.Clamp(count, 0, data.Length - offset);
            return data.AsSpan((int)offset, (int)count);
        }

        static byte ReverseBits(byte value)
        {
            value = (byte)((value & 0xF0) >> 4 | (value & 0x0F) << 4);
            value = (byte)((value & 0xCC) >> 2 | (value & 0x33) << 2);
            return (byte)((value & 0xAA) >> 1 | (value & 0x55) << 1);
        }

        /// <summary>
        /// The decoded samples of the image: one buffer for chunky images, one buffer per sample for planar images.
        /// Rows start on byte boundaries.
        /// </summary>
        sealed class SampleData
        {
            public SampleData(Ifd ifd, byte[] data, int compression, int width, int height, int samplesPerPixel, int bitsPerSample)
            {
                _width = width;
                _bitsPerSample = bitsPerSample;
                _bigEndian = ifd.BigEndian;
                _planar = samplesPerPixel > 1 && ifd.GetInt(TagPlanarConfiguration, 1) == 2;
                _samplesPerUnit = _planar ? 1 : samplesPerPixel;
                int planes = _planar ? samplesPerPixel : 1;
                _rowBytes = (int)(((long)width * _samplesPerUnit * bitsPerSample + 7) / 8);
                if ((long)_rowBytes * height * planes > MaxDecodedBytes)
                    throw new InvalidOperationException($"TIFF image with {width} × {height} pixels is too large.");

                int predictor = ifd.GetInt(TagPredictor, 1);
                if (predictor is not (1 or 2))
                    throw new NotSupportedException($"TIFF predictor {predictor} is not supported.");
                if (predictor == 2 && bitsPerSample is not (8 or 16))
                    throw new NotSupportedException("TIFF horizontal predictor is only supported with 8 or 16 bits per sample.");

                _planes = new byte[planes][];
                for (int plane = 0; plane < planes; plane++)
                    _planes[plane] = new byte[_rowBytes * height];

                if (ifd.Has(TagTileOffsets))
                    ReadTiles(ifd, data, compression, predictor, height, planes);
                else
                    ReadStrips(ifd, data, compression, predictor, height, planes);
            }

            void ReadStrips(Ifd ifd, byte[] data, int compression, int predictor, int height, int planes)
            {
                int rowsPerStrip = ifd.GetInt(TagRowsPerStrip, height);
                if (rowsPerStrip <= 0 || rowsPerStrip > height)
                    rowsPerStrip = height;
                int stripsPerPlane = (height + rowsPerStrip - 1) / rowsPerStrip;
                var offsets = ifd.GetLongs(TagStripOffsets) ?? throw new InvalidOperationException("TIFF image has no strip offsets.");
                var counts = ifd.GetLongs(TagStripByteCounts);
                if (offsets.Length < stripsPerPlane * planes)
                    throw new InvalidOperationException("TIFF image has too few strips.");

                for (int plane = 0; plane < planes; plane++)
                {
                    for (int strip = 0; strip < stripsPerPlane; strip++)
                    {
                        int index = plane * stripsPerPlane + strip;
                        int firstRow = strip * rowsPerStrip;
                        int rows = Math.Min(rowsPerStrip, height - firstRow);
                        long count = counts != null && index < counts.Length ? counts[index] : (long)rows * _rowBytes;
                        var decoded = Decompress(compression, GetSegment(data, offsets[index], count), rows * _rowBytes);
                        if (predictor == 2)
                            UndoPredictor(decoded, _rowBytes, _width);
                        decoded.AsSpan(0, Math.Min(decoded.Length, rows * _rowBytes)).CopyTo(_planes[plane].AsSpan(firstRow * _rowBytes));
                    }
                }
            }

            void ReadTiles(Ifd ifd, byte[] data, int compression, int predictor, int height, int planes)
            {
                int tileWidth = ifd.GetInt(TagTileWidth);
                int tileLength = ifd.GetInt(TagTileLength);
                if (tileWidth <= 0 || tileLength <= 0 || (long)tileWidth * tileLength > MaxDecodedBytes)
                    throw new InvalidOperationException("TIFF image has invalid tiles.");
                int tilesAcross = (_width + tileWidth - 1) / tileWidth;
                int tilesDown = (height + tileLength - 1) / tileLength;
                int tilesPerPlane = tilesAcross * tilesDown;
                int tileRowBytes = (int)(((long)tileWidth * _samplesPerUnit * _bitsPerSample + 7) / 8);
                var offsets = ifd.GetLongs(TagTileOffsets) ?? throw new InvalidOperationException("TIFF image has no tile offsets.");
                var counts = ifd.GetLongs(TagTileByteCounts);
                if (offsets.Length < tilesPerPlane * planes)
                    throw new InvalidOperationException("TIFF image has too few tiles.");

                for (int plane = 0; plane < planes; plane++)
                {
                    for (int tile = 0; tile < tilesPerPlane; tile++)
                    {
                        int index = plane * tilesPerPlane + tile;
                        int tileX = tile % tilesAcross;
                        int tileY = tile / tilesAcross;
                        long count = counts != null && index < counts.Length ? counts[index] : (long)tileLength * tileRowBytes;
                        var decoded = Decompress(compression, GetSegment(data, offsets[index], count), tileLength * tileRowBytes);
                        if (predictor == 2)
                            UndoPredictor(decoded, tileRowBytes, tileWidth);

                        // Copy the part of the tile that lies inside the image. Tiles at the right and bottom edge extend beyond it.
                        int targetByte = (int)((long)tileX * tileWidth * _samplesPerUnit * _bitsPerSample / 8);
                        int bytes = Math.Min(tileRowBytes, _rowBytes - targetByte);
                        for (int row = 0; row < tileLength; row++)
                        {
                            int y = tileY * tileLength + row;
                            if (y >= height)
                                break;
                            int source = row * tileRowBytes;
                            if (source >= decoded.Length)
                                break;
                            decoded.AsSpan(source, Math.Min(bytes, decoded.Length - source)).CopyTo(_planes[plane].AsSpan(y * _rowBytes + targetByte));
                        }
                    }
                }
            }

            /// <summary>
            /// Horizontal differencing: each sample is stored as the difference to the same sample of the previous pixel.
            /// </summary>
            void UndoPredictor(byte[] buffer, int rowBytes, int pixelsPerRow)
            {
                for (int start = 0; start + rowBytes <= buffer.Length; start += rowBytes)
                {
                    var row = buffer.AsSpan(start, rowBytes);
                    if (_bitsPerSample == 8)
                    {
                        for (int idx = _samplesPerUnit; idx < pixelsPerRow * _samplesPerUnit; idx++)
                            row[idx] = (byte)(row[idx] + row[idx - _samplesPerUnit]);
                    }
                    else
                    {
                        for (int idx = _samplesPerUnit; idx < pixelsPerRow * _samplesPerUnit; idx++)
                        {
                            var current = row.Slice(2 * idx, 2);
                            var previous = row.Slice(2 * (idx - _samplesPerUnit), 2);
                            ushort value = (ushort)(Read16(current) + Read16(previous));
                            if (_bigEndian)
                                BinaryPrimitives.WriteUInt16BigEndian(current, value);
                            else
                                BinaryPrimitives.WriteUInt16LittleEndian(current, value);
                        }
                    }
                }
            }

            ushort Read16(ReadOnlySpan<byte> bytes)
                => _bigEndian ? BinaryPrimitives.ReadUInt16BigEndian(bytes) : BinaryPrimitives.ReadUInt16LittleEndian(bytes);

            /// <summary>
            /// Gets the rows of a single-sample image, as they are: 1, 4, or 8 bits per pixel, each row padded to a byte.
            /// </summary>
            public byte[] GetPackedRows() => _planes[0];

            /// <summary>
            /// Gets the raw value of a sample.
            /// </summary>
            public int GetRaw(int x, int y, int sample)
            {
                var buffer = _planar ? _planes[sample] : _planes[0];
                int sampleIndex = _planar ? x : x * _samplesPerUnit + sample;
                int rowStart = y * _rowBytes;
                switch (_bitsPerSample)
                {
                    case 8:
                        return buffer[rowStart + sampleIndex];
                    case 16:
                        return Read16(buffer.AsSpan(rowStart + 2 * sampleIndex, 2));
                    default:
                        {
                            long bit = (long)sampleIndex * _bitsPerSample;
                            int value = buffer[rowStart + (int)(bit / 8)];
                            int shift = 8 - _bitsPerSample - (int)(bit % 8);
                            return (value >> shift) & ((1 << _bitsPerSample) - 1);
                        }
                }
            }

            /// <summary>
            /// Gets a sample scaled to 8 bits.
            /// </summary>
            public int Get8(int x, int y, int sample)
            {
                int value = GetRaw(x, y, sample);
                return _bitsPerSample switch
                {
                    8 => value,
                    16 => value >> 8,
                    _ => value * 255 / ((1 << _bitsPerSample) - 1)
                };
            }

            readonly byte[][] _planes;
            readonly int _width;
            readonly int _rowBytes;
            readonly int _bitsPerSample;
            readonly int _samplesPerUnit;
            readonly bool _planar;
            readonly bool _bigEndian;
        }

        static byte[] Decompress(int compression, ReadOnlySpan<byte> source, int expectedLength)
        {
            switch (compression)
            {
                case CompressionNone:
                    {
                        var result = new byte[expectedLength];
                        source[..Math.Min(source.Length, expectedLength)].CopyTo(result);
                        return result;
                    }

                case CompressionPackBits:
                    return DecodePackBits(source, expectedLength);

                case CompressionLzw:
                    return DecodeLzw(source, expectedLength);

                default: // Deflate with zlib header.
                    {
                        var result = new byte[expectedLength];
                        using var zlib = new ZLibStream(new MemoryStream(source.ToArray()), CompressionMode.Decompress);
                        int total = 0;
                        try
                        {
                            int read;
                            while (total < expectedLength && (read = zlib.Read(result, total, expectedLength - total)) > 0)
                                total += read;
                        }
                        catch (InvalidDataException) when (total > 0)
                        {
                            // Damaged end of the data: use what was decoded.
                        }
                        return result;
                    }
            }
        }

        static byte[] DecodePackBits(ReadOnlySpan<byte> source, int expectedLength)
        {
            var result = new byte[expectedLength];
            int src = 0, dst = 0;
            while (src < source.Length && dst < expectedLength)
            {
                int header = (sbyte)source[src++];
                if (header >= 0)
                {
                    int count = Math.Min(Math.Min(header + 1, source.Length - src), expectedLength - dst);
                    source.Slice(src, count).CopyTo(result.AsSpan(dst));
                    src += header + 1;
                    dst += count;
                }
                else if (header != -128 && src < source.Length)
                {
                    int count = Math.Min(1 - header, expectedLength - dst);
                    result.AsSpan(dst, count).Fill(source[src++]);
                    dst += count;
                }
            }
            return result;
        }

        /// <summary>
        /// The LZW decoding of TIFF: codes are packed with the most significant bit first, start with 9 bits,
        /// and grow up to 12 bits one code earlier than in GIF ("early change").
        /// </summary>
        internal static byte[] DecodeLzw(ReadOnlySpan<byte> source, int expectedLength)
        {
            if (source.Length >= 2 && source[0] == 0 && (source[1] & 1) != 0)
                throw new NotSupportedException("Old-style TIFF LZW compression is not supported.");

            const int clearCode = 256;
            const int endCode = 257;
            const int maxCodes = 4096;
            var prefix = new short[maxCodes];
            var suffix = new byte[maxCodes];
            var first = new byte[maxCodes];
            var length = new short[maxCodes];
            for (int code = 0; code < 256; code++)
            {
                prefix[code] = -1;
                suffix[code] = (byte)code;
                first[code] = (byte)code;
                length[code] = 1;
            }

            var output = new byte[expectedLength];
            int position = 0;
            int codeSize = 9;
            int nextCode = endCode + 1;
            int previousCode = -1;
            long bitBuffer = 0;
            int bitCount = 0;
            int index = 0;

            while (position < expectedLength)
            {
                while (bitCount < codeSize && index < source.Length)
                {
                    bitBuffer = (bitBuffer << 8) | source[index++];
                    bitCount += 8;
                }
                if (bitCount < codeSize)
                    break;
                int code = (int)(bitBuffer >> (bitCount - codeSize)) & ((1 << codeSize) - 1);
                bitCount -= codeSize;
                bitBuffer &= (1L << bitCount) - 1;

                if (code == clearCode)
                {
                    codeSize = 9;
                    nextCode = endCode + 1;
                    previousCode = -1;
                    continue;
                }
                if (code == endCode)
                    break;

                if (previousCode == -1)
                {
                    if (code >= 256)
                        break;
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
                    firstByte = first[previousCode];
                    position = WriteString(previousCode, output, position, prefix, suffix, length);
                    if (position < expectedLength)
                        output[position++] = (byte)firstByte;
                }
                else
                {
                    break;
                }

                if (nextCode < maxCodes)
                {
                    prefix[nextCode] = (short)previousCode;
                    suffix[nextCode] = (byte)firstByte;
                    first[nextCode] = first[previousCode];
                    length[nextCode] = (short)(length[previousCode] + 1);
                    nextCode++;
                    if (nextCode == (1 << codeSize) - 1 && codeSize < 12)
                        codeSize++;
                }
                previousCode = code;
            }
            return output;
        }

        static int WriteString(int code, byte[] output, int position, short[] prefix, byte[] suffix, short[] length)
        {
            int end = position + length[code];
            for (int idx = end - 1; idx >= position; idx--)
            {
                if (idx < output.Length)
                    output[idx] = suffix[code];
                code = prefix[code];
            }
            return Math.Min(end, output.Length);
        }

        /// <summary>
        /// The first image file directory of a TIFF file.
        /// </summary>
        sealed class Ifd
        {
            public Ifd(byte[] data)
            {
                _data = data;
                BigEndian = data[0] == 'M';
                long offset = U32(4);
                if (offset < 8 || offset + 2 > data.Length)
                    throw new InvalidOperationException("Invalid TIFF directory offset.");
                int count = U16(offset);
                if (offset + 2 + 12L * count > data.Length)
                    throw new InvalidOperationException("Truncated TIFF directory.");
                for (int idx = 0; idx < count; idx++)
                {
                    long entry = offset + 2 + 12L * idx;
                    int tag = U16(entry);
                    int type = U16(entry + 2);
                    long valueCount = U32(entry + 4);
                    int size = TypeSize(type);
                    if (size == 0)
                        continue;
                    long byteCount = size * valueCount;
                    long valueOffset = byteCount <= 4 ? entry + 8 : U32(entry + 8);
                    if (valueOffset + byteCount > data.Length || valueCount > MaxValues)
                        continue; // Ignore broken entries; required ones are reported as missing.
                    _entries[tag] = (type, valueCount, valueOffset);
                }
            }

            public bool BigEndian { get; }

            public bool Has(int tag) => _entries.ContainsKey(tag);

            public long[]? GetLongs(int tag)
            {
                if (!_entries.TryGetValue(tag, out var entry))
                    return null;
                var values = new long[entry.Count];
                int size = TypeSize(entry.Type);
                for (int idx = 0; idx < values.Length; idx++)
                {
                    long position = entry.Offset + (long)idx * size;
                    values[idx] = entry.Type switch
                    {
                        1 or 7 => _data[position],         // BYTE, UNDEFINED
                        6 => (sbyte)_data[position],       // SBYTE
                        3 => U16(position),                // SHORT
                        8 => (short)U16(position),         // SSHORT
                        4 => U32(position),                // LONG
                        9 => (int)U32(position),           // SLONG
                        5 or 10 => U32(position),          // RATIONAL: numerator only
                        _ => 0
                    };
                }
                return values;
            }

            public int[]? GetInts(int tag) => GetLongs(tag)?.Select(value => (int)Math.Clamp(value, Int32.MinValue, Int32.MaxValue)).ToArray();

            public int GetInt(int tag, int defaultValue) => GetInts(tag) is { Length: > 0 } values ? values[0] : defaultValue;

            public int GetInt(int tag) => GetInts(tag) is { Length: > 0 } values
                ? values[0]
                : throw new InvalidOperationException($"TIFF tag {tag} is missing.");

            public double GetRational(int tag)
            {
                if (!_entries.TryGetValue(tag, out var entry) || entry.Count < 1)
                    return 0;
                if (entry.Type is 3 or 4)
                    return GetLongs(tag)![0];
                if (entry.Type != 5)
                    return 0;
                double numerator = U32(entry.Offset);
                double denominator = U32(entry.Offset + 4);
                return denominator == 0 ? 0 : numerator / denominator;
            }

            static int TypeSize(int type) => type switch
            {
                1 or 2 or 6 or 7 => 1,
                3 or 8 => 2,
                4 or 9 or 11 => 4,
                5 or 10 or 12 => 8,
                _ => 0
            };

            int U16(long position) => BigEndian
                ? BinaryPrimitives.ReadUInt16BigEndian(_data.AsSpan((int)position, 2))
                : BinaryPrimitives.ReadUInt16LittleEndian(_data.AsSpan((int)position, 2));

            long U32(long position) => BigEndian
                ? BinaryPrimitives.ReadUInt32BigEndian(_data.AsSpan((int)position, 4))
                : BinaryPrimitives.ReadUInt32LittleEndian(_data.AsSpan((int)position, 4));

            const long MaxValues = 1 << 24;

            readonly byte[] _data;
            readonly Dictionary<int, (int Type, long Count, long Offset)> _entries = new();
        }
    }
}
