using System;
using System.IO;
using Microsoft.Extensions.FileProviders;

namespace Mirality.FileProviders.InMemory;

internal class InMemoryDirectoryInfo : IFileInfo
{
    public InMemoryDirectoryInfo(string name, DateTimeOffset lastModified)
    {
        Name = name;
        LastModified = lastModified;
    }

    public string? Name { get; }
    public DateTimeOffset LastModified { get; }

    public bool Exists => true;
    public bool IsDirectory => true;
    public long Length => -1;
    public string? PhysicalPath => null;

    public Stream CreateReadStream()
    {
        throw new InvalidOperationException();
    }
}