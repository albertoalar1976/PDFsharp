// PDFsharp - A .NET library for processing PDF
// See the LICENSE file in the solution root for more information.

using System.Text;
using FluentAssertions;
using PdfSharp.Diagnostics;
using PdfSharp.Pdf;
using PdfSharp.Pdf.IO;
using Xunit;

namespace PdfSharp.Tests.IO
{
    /// <summary>
    /// Saving to and opening from streams as they occur in server applications:
    /// streams that cannot seek, that do not allow synchronous I/O, or that already contain data.
    /// </summary>
    [Collection("PDFsharp")]
    public class StreamTests : IDisposable
    {
        public StreamTests()
        {
            PdfSharpCore.ResetAll();
        }

        public void Dispose()
        {
            PdfSharpCore.ResetAll();
        }

        [Fact]
        public async Task SaveAsync_writes_to_a_stream_like_HttpResponse_Body()
        {
            // Like HttpResponse.Body in ASP.NET Core: no seeking, no synchronous writing.
            // The former implementation failed because PdfWriter reads Stream.Position.
            var responseBody = new ForwardOnlyStream(allowSynchronousIO: false);

            await CreateDocument(3).SaveAsync(responseBody);

            using var document = PdfReader.Open(new MemoryStream(responseBody.ToArray()), PdfDocumentOpenMode.Import);
            document.PageCount.Should().Be(3);
        }

        [Fact]
        public void Save_writes_to_a_stream_that_cannot_seek()
        {
            var stream = new ForwardOnlyStream(allowSynchronousIO: true);

            CreateDocument(2).Save(stream);

            using var document = PdfReader.Open(new MemoryStream(stream.ToArray()), PdfDocumentOpenMode.Import);
            document.PageCount.Should().Be(2);
        }

        [Fact]
        public void Save_writes_correct_offsets_to_a_stream_that_already_contains_data()
        {
            // The cross-reference table must contain offsets relative to the start of the PDF file,
            // not to the start of the stream. Formerly, data written before shifted all offsets.
            var prefix = Encoding.ASCII.GetBytes("Some data written before the PDF file.\n");
            var stream = new MemoryStream();
            stream.Write(prefix);

            CreateDocument(2).Save(stream);

            var pdf = stream.ToArray()[prefix.Length..];
            var startXRef = ReadStartXRef(pdf);
            Encoding.ASCII.GetString(pdf, startXRef, 4).Should().Be("xref", "startxref must point to the cross-reference table");
            using var document = PdfReader.Open(new MemoryStream(pdf), PdfDocumentOpenMode.Import);
            document.PageCount.Should().Be(2);
        }

        [Fact]
        public async Task SaveAsync_observes_cancellation()
        {
            using var cts = new CancellationTokenSource();
            await cts.CancelAsync();

            var save = () => CreateDocument(1).SaveAsync(new MemoryStream(), false, cts.Token);

            await save.Should().ThrowAsync<OperationCanceledException>();
        }

        [Fact]
        public async Task OpenAsync_reads_a_stream_that_cannot_seek()
        {
            var pdf = SaveToArray(CreateDocument(4));

            using var document = await PdfReader.OpenAsync(new ForwardOnlyStream(pdf, allowSynchronousIO: false), PdfDocumentOpenMode.Import);

            document.PageCount.Should().Be(4);
        }

        [Fact]
        public void Open_reads_a_stream_that_cannot_seek()
        {
            // The former implementation threw "PdfReader needs a stream that supports seeking".
            var pdf = SaveToArray(CreateDocument(2));

            using var document = PdfReader.Open(new ForwardOnlyStream(pdf, allowSynchronousIO: true), PdfDocumentOpenMode.Import);

            document.PageCount.Should().Be(2);
        }

        [Fact]
        public async Task OpenAsync_observes_cancellation()
        {
            using var cts = new CancellationTokenSource();
            await cts.CancelAsync();

            var open = () => PdfReader.OpenAsync(new MemoryStream(SaveToArray(CreateDocument(1))), cancellationToken: cts.Token);

            await open.Should().ThrowAsync<OperationCanceledException>();
        }

        static PdfDocument CreateDocument(int pageCount)
        {
            var document = new PdfDocument();
            for (var i = 0; i < pageCount; i++)
                document.AddPage();
            return document;
        }

        static byte[] SaveToArray(PdfDocument document)
        {
            var stream = new MemoryStream();
            document.Save(stream);
            return stream.ToArray();
        }

        static int ReadStartXRef(byte[] pdf)
        {
            var text = Encoding.ASCII.GetString(pdf);
            var index = text.LastIndexOf("startxref", StringComparison.Ordinal) + "startxref".Length;
            var end = text.IndexOf("%%EOF", index, StringComparison.Ordinal);
            return Int32.Parse(text[index..end].Trim(), System.Globalization.CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// A stream that cannot seek, like a network stream or HttpResponse.Body / HttpRequest.Body in ASP.NET Core.
        /// Optionally, it rejects synchronous I/O like Kestrel does by default.
        /// </summary>
        sealed class ForwardOnlyStream : Stream
        {
            public ForwardOnlyStream(bool allowSynchronousIO)
            {
                _inner = new MemoryStream();
                _allowSynchronousIO = allowSynchronousIO;
                _canRead = false;
            }

            public ForwardOnlyStream(byte[] content, bool allowSynchronousIO)
            {
                _inner = new MemoryStream(content);
                _allowSynchronousIO = allowSynchronousIO;
                _canRead = true;
            }

            public byte[] ToArray() => _inner.ToArray();

            public override bool CanRead => _canRead;
            public override bool CanSeek => false;
            public override bool CanWrite => !_canRead;
            public override long Length => throw new NotSupportedException();
            public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }

            public override void Flush()
            {
                CheckSynchronousIO();
            }

            public override Task FlushAsync(CancellationToken cancellationToken) => Task.CompletedTask;

            public override int Read(byte[] buffer, int offset, int count)
            {
                CheckSynchronousIO();
                return _inner.Read(buffer, offset, count);
            }

            public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
                => _inner.ReadAsync(buffer, cancellationToken);

            public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
                => _inner.ReadAsync(buffer, offset, count, cancellationToken);

            public override void Write(byte[] buffer, int offset, int count)
            {
                CheckSynchronousIO();
                _inner.Write(buffer, offset, count);
            }

            public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
                => _inner.WriteAsync(buffer, cancellationToken);

            public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
                => _inner.WriteAsync(buffer, offset, count, cancellationToken);

            public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
            public override void SetLength(long value) => throw new NotSupportedException();

            void CheckSynchronousIO()
            {
                if (!_allowSynchronousIO)
                    throw new InvalidOperationException("Synchronous operations are disallowed.");
            }

            readonly MemoryStream _inner;
            readonly bool _allowSynchronousIO;
            readonly bool _canRead;
        }
    }
}
