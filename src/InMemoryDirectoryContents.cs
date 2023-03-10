using System.Collections;
using System.Collections.Generic;
using Microsoft.Extensions.FileProviders;

namespace Mirality.FileProviders.InMemory;

internal class InMemoryDirectoryContents : IDirectoryContents
{
    private readonly IEnumerable<IFileInfo> _Files;

    public InMemoryDirectoryContents(IEnumerable<IFileInfo> files)
    {
        _Files = files;
    }

    public bool Exists => true;

    public IEnumerator<IFileInfo> GetEnumerator()
    {
        return _Files.GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }
}