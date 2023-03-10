using System;
using System.IO;
using System.Text;
using Microsoft.Extensions.FileProviders;

namespace Mirality.FileProviders.InMemory;

/// <summary>A purely in-memory <see cref="IFileInfo"/>.</summary>
public class InMemoryFileInfo : IFileInfo
{
    private readonly byte[] _Content;

    /// <summary>Constructor.</summary>
    /// <param name="name">The filename (without path).</param>
    /// <param name="content">The file content.</param>
    /// <param name="lastModified">When the file was last modified.  Defaults to <see cref="DateTimeOffset.Now"/>.</param>
    public InMemoryFileInfo(string name, byte[] content, DateTimeOffset? lastModified = null)
    {
        Name = name;
        _Content = content;
        LastModified = lastModified ?? DateTimeOffset.Now;
    }

    /// <summary>Constructor.</summary>
    /// <param name="name">The filename (without path).</param>
    /// <param name="content">The file content (assumes UTF-8 encoding).</param>
    /// <param name="lastModified">When the file was last modified.  Defaults to <see cref="DateTimeOffset.Now"/>.</param>
    public InMemoryFileInfo(string name, string content, DateTimeOffset? lastModified = null)
        : this(name, Encoding.UTF8.GetBytes(content), lastModified)
    {
    }

    /// <inheritdoc />
    public string? Name { get; }
    /// <inheritdoc />
    public DateTimeOffset LastModified { get; }

    /// <inheritdoc />
    public bool Exists => true;
    /// <inheritdoc />
    public bool IsDirectory => false;
    /// <inheritdoc />
    public long Length => _Content.Length;
    /// <inheritdoc />
    public string? PhysicalPath => null;

    /// <inheritdoc />
    public Stream CreateReadStream()
    {
        return new MemoryStream(_Content);
    }
}