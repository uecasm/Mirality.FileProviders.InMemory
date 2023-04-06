using System.Diagnostics.CodeAnalysis;
using System.IO;

namespace Mirality.FileProviders.InMemory
{
    [ExcludeFromCodeCoverage]
    internal sealed class InMemoryFileStream : Stream
    {
        private readonly ISyncWritableFileProvider _Provider;
        private readonly string _Path;
        private readonly MemoryStream _Stream;

        public InMemoryFileStream(ISyncWritableFileProvider provider, string path)
        {
            _Provider = provider;
            _Path = path;
            _Stream = new MemoryStream();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                Flush();
            }

            base.Dispose(disposing);
        }

        public override void Flush()
        {
            _Provider.Write(_Path, _Stream.ToArray());
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            return _Stream.Read(buffer, offset, count);
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            return _Stream.Seek(offset, origin);
        }

        public override void SetLength(long value)
        {
            _Stream.SetLength(value);
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            _Stream.Write(buffer, offset, count);
        }

        public override bool CanRead => _Stream.CanRead;

        public override bool CanSeek => _Stream.CanSeek;

        public override bool CanWrite => _Stream.CanWrite;

        public override long Length => _Stream.Length;

        public override long Position
        {
            get => _Stream.Position;
            set => _Stream.Position = value;
        }
    }
}
