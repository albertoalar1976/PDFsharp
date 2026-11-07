// PDFsharp - A .NET library for processing PDF
// See the LICENSE file in the solution root for more information.

namespace PdfSharp.Pdf.Filters
{
    /// <summary>
    /// Implements the LzwDecode filter.
    /// </summary>
    /// <remarks>
    /// This class is stateless and therefore thread-safe. A single instance is shared process-wide
    /// (see <see cref="Filtering.GetFilter"/>), so all decoding state lives in a private object
    /// that is created for each call of <see cref="Decode(byte[], FilterParms?)"/>.
    /// </remarks>
    public class LzwDecode : Filter
    {
        // Reference: 3.3.3  LZWDecode and FlateDecode Filters / Page 71

        /// <summary>
        /// Throws a NotImplementedException because the obsolete LZW encoding is not supported by PDFsharp.
        /// </summary>
        public override byte[] Encode(byte[] data)
        {
            throw new NotImplementedException("PDFsharp does not support LZW encoding.");
        }

        /// <summary>
        /// Decodes the specified data.
        /// </summary>
        public override byte[] Decode(byte[] data, FilterParms? parms)
        {
            if (data.Length >= 2 && data[0] == 0x00 && data[1] == 0x01)
                throw new Exception("LZW flavor not supported.");

            var maxDecodedLength = parms?.MaxDecodedLength ?? Filtering.MaxDecodedLength;
            var outputStream = new Decoder(data, maxDecodedLength).Decode();

            outputStream.Capacity = (int)outputStream.Length;

            if (parms?.DecodeParms != null)
                return StreamDecoder.Decode(outputStream.GetBuffer(), parms.DecodeParms);

            return outputStream.GetBuffer();
        }

        /// <summary>
        /// Holds the state of one decoding operation.
        /// Never share an instance of this class between calls or threads.
        /// </summary>
        sealed class Decoder(byte[] data, int maxDecodedLength)
        {
            public MemoryStream Decode()
            {
                var outputStream = new MemoryStream();

                InitializeDictionary();

                int code, oldCode = 0;
                byte[] str;

                while ((code = NextCode()) != EndOfData)
                {
                    if (code == ClearTable)
                    {
                        InitializeDictionary();
                        code = NextCode();
                        if (code == EndOfData)
                        {
                            break;
                        }
                        Write(outputStream, _stringTable[code]);
                        oldCode = code;
                    }
                    else
                    {
                        if (code < _tableIndex)
                        {
                            str = _stringTable[code];
                            Write(outputStream, str);
                            AddEntry(_stringTable[oldCode], str[0]);
                            oldCode = code;
                        }
                        else
                        {
                            // The 'KwKwK' case: The code is not yet in the table. Its string is the previous
                            // string plus the first byte of the previous string. The former implementation wrote
                            // only the previous string and lost one byte, e.g. it decoded the example of the
                            // PDF reference '-----A---B' as '----A---B'.
                            str = _stringTable[oldCode];
                            AddEntry(str, str[0]);
                            Write(outputStream, _stringTable[_tableIndex - 1]);
                            oldCode = code;
                        }
                    }
                }
                return outputStream;
            }

            /// <summary>
            /// Writes decoded bytes and protects against decompression bombs.
            /// </summary>
            void Write(MemoryStream outputStream, byte[] bytes)
            {
                if (outputStream.Length + bytes.Length > maxDecodedLength)
                    Filtering.ThrowMaxDecodedLengthExceeded(PdfFilterNames.LzwDecode, maxDecodedLength);
                outputStream.Write(bytes, 0, bytes.Length);
            }

            /// <summary>
            /// Initialize the dictionary.
            /// </summary>
            void InitializeDictionary()
            {
                _stringTable = new byte[8192][];

                for (int i = 0; i < 256; i++)
                {
                    _stringTable[i] = new byte[1];
                    _stringTable[i][0] = (byte)i;
                }

                _tableIndex = 258;
                _bitsToGet = 9;
            }

            /// <summary>
            /// Add a new entry to the Dictionary.
            /// </summary>
            void AddEntry(byte[] oldString, byte newByte)
            {
                int length = oldString.Length;
                byte[] str = new byte[length + 1];
                Array.Copy(oldString, 0, str, 0, length);
                str[length] = newByte;

                _stringTable[_tableIndex++] = str;

                if (_tableIndex == 511)
                    _bitsToGet = 10;
                else if (_tableIndex == 1023)
                    _bitsToGet = 11;
                else if (_tableIndex == 2047)
                    _bitsToGet = 12;
            }

            /// <summary>
            /// Returns the next set of bits, or EndOfData if the input is exhausted.
            /// </summary>
            int NextCode()
            {
                // Running out of input is treated as end of data. The former implementation
                // achieved the same with a catch-all exception handler.
                if (_bytePointer >= data.Length)
                    return EndOfData;

                _nextData = (_nextData << 8) | (data[_bytePointer++] & 0xff);
                _nextBits += 8;

                if (_nextBits < _bitsToGet)
                {
                    if (_bytePointer >= data.Length)
                        return EndOfData;

                    _nextData = (_nextData << 8) | (data[_bytePointer++] & 0xff);
                    _nextBits += 8;
                }

                int code = (_nextData >> (_nextBits - _bitsToGet)) & AndTable[_bitsToGet - 9];
                _nextBits -= _bitsToGet;

                return code;
            }

            const int ClearTable = 256;
            const int EndOfData = 257;

            static readonly int[] AndTable = [511, 1023, 2047, 4095];

            byte[][] _stringTable = default!;
            int _tableIndex;
            int _bitsToGet = 9;
            int _bytePointer;
            int _nextData;
            int _nextBits;
        }
    }
}
