using Microsoft.Extensions.FileProviders;
using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using System.Threading;

namespace Mirality.FileProviders
{
    /// <summary>Abstract interface for <see cref="IFileProvider"/> that can accept file writes as well.</summary>
    public interface IWritableFileProvider : IFileProvider
    {
        /// <summary>Creates a file and returns a writable stream for it.</summary>
        /// <param name="path">The full relative file path.</param>
        /// <returns>A writable stream for the file.  The caller should dispose the stream when done.</returns>
        Stream Create(string path);

        /// <summary>Deletes a file.</summary>
        /// <remarks>It's not an error if the file already does not exist.</remarks>
        /// <param name="path">The full relative file path.</param>
        void Delete(string path);
    }

    /// <summary>An <see cref="IWritableFileProvider"/> that provides synchronous write operations.</summary>
    public interface ISyncWritableFileProvider : IWritableFileProvider
    {
        /// <summary>Creates or overwrites a file.</summary>
        /// <param name="path">The full relative file path.</param>
        /// <param name="content">The file content.</param>
        /// <param name="lastModified">When the file was last modified.  Defaults to <see cref="DateTimeOffset.Now" />.</param>
        /// <returns>The <see cref="IFileInfo" /> for this file.</returns>
        IFileInfo Write(string path, byte[] content, DateTimeOffset? lastModified = null);
    }

    /// <summary>An <see cref="IWritableFileProvider"/> that provides asynchronous write operations.</summary>
    public interface IAsyncWritableFileProvider : IWritableFileProvider
    {
        /// <summary>Creates or overwrites a file.</summary>
        /// <param name="path">The full relative file path.</param>
        /// <param name="content">The file content.</param>
        /// <param name="lastModified">When the file was last modified.  Defaults to <see cref="DateTimeOffset.Now" />.</param>
        /// <param name="cancel">Cancellation token.</param>
        /// <returns>The <see cref="IFileInfo" /> for this file.</returns>
        Task<IFileInfo> WriteAsync(string path, byte[] content, DateTimeOffset? lastModified = null, CancellationToken cancel = default);
    }

    /// <summary>Extension helper methods for <see cref="IWritableFileProvider"/>.</summary>
    public static class WritableFileProviderExtensions
    {
        /// <summary>Creates or overwrites a file.</summary>
        /// <param name="provider">The writable file provider.</param>
        /// <param name="path">The full relative file path.</param>
        /// <param name="content">The file content (assumes UTF-8 encoding).</param>
        /// <param name="lastModified">When the file was last modified.  Defaults to <see cref="DateTimeOffset.Now" />.</param>
        public static IFileInfo Write(this ISyncWritableFileProvider provider, string path, string content,
            DateTimeOffset? lastModified = null)
        {
            return provider.Write(path, Encoding.UTF8.GetBytes(content), lastModified);
        }

        /// <summary>Creates or overwrites a file.</summary>
        /// <param name="provider">The writable file provider.</param>
        /// <param name="path">The full relative file path.</param>
        /// <param name="content">The file content (assumes UTF-8 encoding).</param>
        /// <param name="lastModified">When the file was last modified.  Defaults to <see cref="DateTimeOffset.Now" />.</param>
        /// <param name="cancel">Cancellation token.</param>
        public static Task<IFileInfo> WriteAsync(this IAsyncWritableFileProvider provider, string path, string content,
            DateTimeOffset? lastModified = null, CancellationToken cancel = default)
        {
            return provider.WriteAsync(path, Encoding.UTF8.GetBytes(content), lastModified, cancel);
        }

        /// <summary>Creates or overwrites a file.</summary>
        /// <param name="provider">The writable file provider.</param>
        /// <param name="path">The full relative file path.</param>
        /// <param name="content">The file content.</param>
        /// <param name="lastModified">When the file was last modified.  Defaults to <see cref="DateTimeOffset.Now" />.</param>
        /// <param name="cancel">Cancellation token.</param>
        public static async ValueTask<IFileInfo> WriteAsync(this IWritableFileProvider provider, string path, byte[] content,
            DateTimeOffset? lastModified = null, CancellationToken cancel = default)
        {
            return provider switch
            {
                IAsyncWritableFileProvider asyncProvider => await asyncProvider.WriteAsync(path, content, lastModified, cancel),
                ISyncWritableFileProvider syncProvider => syncProvider.Write(path, content, lastModified),
                _ => throw new InvalidOperationException("Expecting either ISyncWritableFileProvider or IAsyncWritableFileProvider")
            };
        }

        /// <summary>Creates or overwrites a file.</summary>
        /// <param name="provider">The writable file provider.</param>
        /// <param name="path">The full relative file path.</param>
        /// <param name="content">The file content (assumes UTF-8 encoding).</param>
        /// <param name="lastModified">When the file was last modified.  Defaults to <see cref="DateTimeOffset.Now" />.</param>
        /// <param name="cancel">Cancellation token.</param>
        public static async ValueTask<IFileInfo> WriteAsync(this IWritableFileProvider provider, string path, string content,
            DateTimeOffset? lastModified = null, CancellationToken cancel = default)
        {
            return provider switch
            {
                IAsyncWritableFileProvider asyncProvider => await asyncProvider.WriteAsync(path, content, lastModified, cancel),
                ISyncWritableFileProvider syncProvider => syncProvider.Write(path, content, lastModified),
                _ => throw new InvalidOperationException("Expecting either ISyncWritableFileProvider or IAsyncWritableFileProvider")
            };
        }
    }
}
