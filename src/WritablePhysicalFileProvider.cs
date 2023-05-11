using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Primitives;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Threading;

namespace Mirality.FileProviders
{
    /// <summary>This is an <see cref="IWritableFileProvider"/> that wraps an existing <see cref="IFileProvider"/>
    /// with the ability to also write files to a physical path.</summary>
    /// <remarks>It assumes that the provided <see cref="IFileProvider"/> is either a PhysicalFileProvider
    /// pointed at the same path, or is a CompositeFileProvider that includes such a provider.</remarks>
    public class WritablePhysicalFileProvider : ISyncWritableFileProvider, IAsyncWritableFileProvider
    {
        private readonly string _Path;
        private readonly IFileProvider _BaseProvider;

        /// <summary>Constructor</summary>
        /// <remarks>This does <b>not</b> take ownership of <paramref name="baseProvider"/>; it is still your
        /// responsibility to <see cref="IDisposable.Dispose"/> it if it needs to be.</remarks>
        /// <param name="path">The full physical path where to store the files.</param>
        /// <param name="baseProvider">The base <see cref="IFileProvider"/> used to read back files.</param>
        public WritablePhysicalFileProvider(string path, IFileProvider baseProvider)
        {
            if (baseProvider is IWritableFileProvider)
            {
                throw new ArgumentException("The base provider is already writable; use it directly instead of wrapping it.", nameof(baseProvider));
            }

            _Path = path;
            _BaseProvider = baseProvider;
        }

        /// <inheritdoc />
        public IDirectoryContents GetDirectoryContents(string subpath)
        {
            return _BaseProvider.GetDirectoryContents(subpath);
        }

        /// <inheritdoc />
        public IFileInfo GetFileInfo(string subpath)
        {
            return _BaseProvider.GetFileInfo(subpath);
        }

        /// <inheritdoc />
        public IChangeToken Watch(string filter)
        {
            return _BaseProvider.Watch(filter);
        }

        private static readonly char[] PathSeparators = { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar };

        private static bool PathNavigatesAboveRoot(string path)
        {
            var pathTokenizer = new StringTokenizer(path, PathSeparators);
            var depth = 0;
            foreach (var segment in pathTokenizer.Where(s => !(s.Equals(".") || s.Equals(""))))
            {
                if (segment.Equals(".."))
                {
                    if (--depth < 0)
                    {
                        return true;
                    }
                }
                else
                {
                    ++depth;
                }
            }
            return false;
        }

        private FileInfo GetPhysicalFileInfo(string path)
        {
            if (PathNavigatesAboveRoot(path)) { throw new ArgumentOutOfRangeException(nameof(path), path, $"{path} would escape root directory"); }
            return new FileInfo(Path.Combine(_Path, path));
        }

        /// <inheritdoc />
        public Stream Create(string path)
        {
            var file = GetPhysicalFileInfo(path);

            return file.Create();
        }

        /// <inheritdoc />
        public IFileInfo Write(string path, byte[] content, DateTimeOffset? lastModified = null)
        {
            var file = GetPhysicalFileInfo(path);

            using (var stream = file.Create())
            {
                stream.Write(content, 0, content.Length);
            }

            if (lastModified != null)
            {
                file.LastWriteTimeUtc = lastModified.Value.UtcDateTime;
            }

            return GetFileInfo(path);
        }

        /// <inheritdoc />
        public async Task<IFileInfo> WriteAsync(string path, byte[] content, DateTimeOffset? lastModified = null, CancellationToken cancel = default)
        {
            var file = GetPhysicalFileInfo(path);

#if NETSTANDARD2_0
            using (var stream = file.Create())
            {
                await stream.WriteAsync(content, 0, content.Length, cancel);
            }
#else
            await using (var stream = file.Create())
            {
                await stream.WriteAsync(content, 0, content.Length, cancel);
            }
#endif

            if (lastModified != null)
            {
                file.LastWriteTimeUtc = lastModified.Value.UtcDateTime;
            }

            return GetFileInfo(path);
        }

        /// <inheritdoc />
        public void Delete(string path)
        {
            var file = GetPhysicalFileInfo(path);

            file.Delete();
        }
    }
}
