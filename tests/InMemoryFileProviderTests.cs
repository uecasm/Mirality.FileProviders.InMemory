using Is = NUnit.Framework.Is;

namespace Mirality.FileProviders.InMemory.Tests;

public class InMemoryFileProviderTests
{
    [Test]
    public void NonExistingFiles()
    {
        var provider = new InMemoryFileProvider();

        var file1 = provider.GetFileInfo("test.txt");
        var file2 = provider.GetFileInfo("another/file.bin");

        Assert.Multiple(() =>
        {
            Assert.That(file1.Name, Is.EqualTo("test.txt"));
            Assert.That(file1.PhysicalPath, Is.Null);
            Assert.That(file1.IsDirectory, Is.False);
            Assert.That(file1.Exists, Is.False);
            Assert.That(() => file1.CreateReadStream(), Throws.TypeOf<FileNotFoundException>());

            Assert.That(file2.Name, Is.EqualTo("file.bin"));
            Assert.That(file2.PhysicalPath, Is.Null);
            Assert.That(file2.IsDirectory, Is.False);
            Assert.That(file2.Exists, Is.False);
            Assert.That(() => file2.CreateReadStream(), Throws.TypeOf<FileNotFoundException>());
        });
    }

    [Test]
    public void NonExistingDirectories()
    {
        var provider = new InMemoryFileProvider();

        var dir1 = provider.GetDirectoryContents("subdir");

        Assert.Multiple(() =>
        {
            Assert.That(dir1.Exists, Is.False);
            Assert.That(dir1, Is.Empty);
        });
    }

    [Test]
    public void ExistingFiles()
    {
        var provider = new InMemoryFileProvider
        {
            ["test.txt"] = new InMemoryFileInfo("test.txt", "hello world"),
            ["another/file.bin"] = new InMemoryFileInfo("file.bin", new byte[] { 1, 2, 3, 4, 5 }),
        };

        var file1 = provider.GetFileInfo("test.txt");
        var file2 = provider.GetFileInfo("another/file.bin");

        Assert.Multiple(() =>
        {
            Assert.That(file1.Name, Is.EqualTo("test.txt"));
            Assert.That(file1.PhysicalPath, Is.Null);
            Assert.That(file1.IsDirectory, Is.False);
            Assert.That(file1.Exists, Is.True);
            Assert.That(file1.LastModified, Is.EqualTo(DateTimeOffset.Now).Within(TimeSpan.FromSeconds(5)));
            Assert.That(file1.Length, Is.EqualTo(11));
            Assert.That(Read(file1), Is.EqualTo("hello world".ToCharArray().Select(c => (byte) c).ToArray()));

            Assert.That(file2.Name, Is.EqualTo("file.bin"));
            Assert.That(file2.PhysicalPath, Is.Null);
            Assert.That(file2.IsDirectory, Is.False);
            Assert.That(file2.Exists, Is.True);
            Assert.That(file2.LastModified, Is.EqualTo(DateTimeOffset.Now).Within(TimeSpan.FromSeconds(5)));
            Assert.That(file2.Length, Is.EqualTo(5));
            Assert.That(Read(file2), Is.EqualTo(new byte[] { 1, 2, 3, 4, 5 }));
        });
    }

    [Test]
    public void FileInfoForDirectories()
    {
        var provider = new InMemoryFileProvider
        {
            ["test.txt"] = new InMemoryFileInfo("test.txt", "hello world"),
            ["another/file.bin"] = new InMemoryFileInfo("file.bin", new byte[] { 1, 2, 3, 4, 5 }),
        };

        var dir1 = provider.GetFileInfo("");
        var dir2 = provider.GetFileInfo("another");

        Assert.Multiple(() =>
        {
            Assert.That(dir1.Name, Is.EqualTo(""));
            Assert.That(dir1.PhysicalPath, Is.Null);
            Assert.That(dir1.IsDirectory, Is.True);
            Assert.That(dir1.Exists, Is.True);
            Assert.That(() => dir1.CreateReadStream(), Throws.TypeOf<InvalidOperationException>());

            Assert.That(dir2.Name, Is.EqualTo("another"));
            Assert.That(dir2.PhysicalPath, Is.Null);
            Assert.That(dir2.IsDirectory, Is.True);
            Assert.That(dir2.Exists, Is.True);
            Assert.That(() => dir2.CreateReadStream(), Throws.TypeOf<InvalidOperationException>());
        });
    }

    [Test]
    public void DirectoryContents()
    {
        var provider = new InMemoryFileProvider
        {
            ["test.txt"] = new InMemoryFileInfo("test.txt", "hello world"),
            ["another/file.bin"] = new InMemoryFileInfo("file.bin", new byte[] { 1, 2, 3, 4, 5 }),
            ["another/child/folder.txt"] = new InMemoryFileInfo("folder.txt", "grandchild"),
        };

        var dir1 = provider.GetDirectoryContents("");
        var dir2 = provider.GetDirectoryContents("another");
        var dir3 = provider.GetDirectoryContents("nope");

        Assert.Multiple(() =>
        {
            Assert.That(dir1.Exists, Is.True);
            Assert.That(dir1.Select(f => f.Name), Is.EquivalentTo(new[] { "test.txt", "another" }));

            Assert.That(dir2.Exists, Is.True);
            Assert.That(dir2.Select(f => f.Name), Is.EquivalentTo(new[] { "file.bin", "child" }));

            Assert.That(dir3.Exists, Is.False);
            Assert.That(dir3.Select(f => f.Name), Is.Empty);
        });
    }

    [Test]
    public void WatchFile()
    {
        const string filter = "another/file.bin";

        var provider = new InMemoryFileProvider();

        var token = provider.Watch(filter);
        Assert.That(token.HasChanged, Is.False);

        provider["another/wrongfile.bin"] = new InMemoryFileInfo("wrongfile.bin", "test");
        Assert.That(token.HasChanged, Is.False);

        provider["another/file.bin"] = new InMemoryFileInfo("file.bin", "test");
        Assert.That(token.HasChanged, Is.True);

        token = provider.Watch(filter);
        Assert.That(token.HasChanged, Is.False);

        provider["another/file.bin"] = null;
        Assert.That(token.HasChanged, Is.True);
    }

    [Test]
    public void WatchDeleteNonExistentFile()
    {
        const string filter = "another/file.bin";

        var provider = new InMemoryFileProvider();

        var token = provider.Watch(filter);
        Assert.That(token.HasChanged, Is.False);

        provider["another/file.bin"] = null;
        Assert.That(token.HasChanged, Is.False);
    }

    [Test]
    public void WatchDoubleDeleteFile()
    {
        const string filter = "another/file.bin";

        var provider = new InMemoryFileProvider()
        {
            ["another/file.bin"] = new InMemoryFileInfo("file.bin", "test"),
        };

        var token = provider.Watch(filter);
        Assert.That(token.HasChanged, Is.False);

        provider["another/file.bin"] = null;
        Assert.That(token.HasChanged, Is.True);

        token = provider.Watch(filter);
        Assert.That(token.HasChanged, Is.False);

        provider["another/file.bin"] = null;
        Assert.That(token.HasChanged, Is.False);
    }

    [Test]
    public void WatchFileWildcard()
    {
        const string filter = "another/*.bin";

        var provider = new InMemoryFileProvider();

        var token = provider.Watch(filter);
        Assert.That(token.HasChanged, Is.False);

        provider["another/file.bad"] = new InMemoryFileInfo("file.bad", "test");
        Assert.That(token.HasChanged, Is.False);

        provider["another/file.bin"] = new InMemoryFileInfo("file.bin", "test");
        Assert.That(token.HasChanged, Is.True);

        token = provider.Watch(filter);
        Assert.That(token.HasChanged, Is.False);

        provider["another/secondfile.bin"] = new InMemoryFileInfo("secondfile.bin", "test");
        Assert.That(token.HasChanged, Is.True);
    }

    [Test]
    public void WatchDirectory()
    {
        const string filter = "another/";

        var provider = new InMemoryFileProvider();

        var token = provider.Watch(filter);
        Assert.That(token.HasChanged, Is.False);

        provider["wrong/file.bin"] = new InMemoryFileInfo("file.bin", "test");
        Assert.That(token.HasChanged, Is.False);

        provider["another/file.bin"] = new InMemoryFileInfo("file.bin", "test");
        Assert.That(token.HasChanged, Is.True);

        token = provider.Watch(filter);
        Assert.That(token.HasChanged, Is.False);

        provider["another/secondfile.bin"] = new InMemoryFileInfo("secondfile.bin", "test");
        Assert.That(token.HasChanged, Is.True);

        token = provider.Watch(filter);
        Assert.That(token.HasChanged, Is.False);

        provider["another/child/file.bin"] = new InMemoryFileInfo("file.bin", "test");
        Assert.That(token.HasChanged, Is.True);
    }

    [Test]
    public void WatchComplexWildcard()
    {
        const string filter = "**/f*.bin";

        var provider = new InMemoryFileProvider();

        var token = provider.Watch(filter);
        Assert.That(token.HasChanged, Is.False);

        provider["subdir/wrong.bin"] = new InMemoryFileInfo("wrong.bin", "test");
        Assert.That(token.HasChanged, Is.False);

        provider["subdir/file.bin"] = new InMemoryFileInfo("file.bin", "test");
        Assert.That(token.HasChanged, Is.True);

        token = provider.Watch(filter);
        Assert.That(token.HasChanged, Is.False);

        provider["another/fred.bin"] = new InMemoryFileInfo("fred.bin", "test");
        Assert.That(token.HasChanged, Is.True);
    }

    [Test]
    public void OverlappingWatches()
    {
        const string filter1 = "**/f*.bin";
        const string filter2 = "another/";

        var provider = new InMemoryFileProvider();

        var token1 = provider.Watch(filter1);
        var token2 = provider.Watch(filter2);
        Assert.Multiple(() =>
        {
            Assert.That(token1.HasChanged, Is.False);
            Assert.That(token2.HasChanged, Is.False);
        });

        provider["subdir/wrong.bin"] = new InMemoryFileInfo("wrong.bin", "test");
        Assert.Multiple(() =>
        {
            Assert.That(token1.HasChanged, Is.False);
            Assert.That(token2.HasChanged, Is.False);
        });

        provider["another/fred.bin"] = new InMemoryFileInfo("fred.bin", "test");
        Assert.Multiple(() =>
        {
            Assert.That(token1.HasChanged, Is.True);
            Assert.That(token2.HasChanged, Is.True);
        });
    }

    [Test]
    public void WatchDoesTriggerForExplicitlyReassigningSameExistingFile()  // but not non-existing files
    {
        const string filter = "test.txt";

        var file = new InMemoryFileInfo("test.txt", "content");

        var provider = new InMemoryFileProvider()
        {
            ["test.txt"] = file,
        };

        var token = provider.Watch(filter);
        Assert.That(token.HasChanged, Is.False);

        provider["test.txt"] = file;
        Assert.That(token.HasChanged, Is.True);
    }

    [Test]
    public void CaseInsensitivity()
    {
        var provider = new InMemoryFileProvider
        {
            ["Test.txt"] = new InMemoryFileInfo("test.Txt", "hello world"),
            ["Another/File.bin"] = new InMemoryFileInfo("file.Bin", new byte[] { 1, 2, 3, 4, 5 }),
            ["ANOTher/fILE.biN"] = new InMemoryFileInfo("fILE.biN", new byte[] { 1, 2, 3 }),
        };

        var file1 = provider.GetFileInfo("test.TXT");
        var file2 = provider.GetFileInfo("ANOTHER/file.BIN");

        Assert.Multiple(() =>
        {
            Assert.That(file1.Exists, Is.True);
            Assert.That(file1.Name, Is.EqualTo("test.Txt"));

            Assert.That(file2.Exists, Is.True);
            Assert.That(file2.Name, Is.EqualTo("fILE.biN"));
        });

        var dir2 = provider.GetDirectoryContents("ANOTHER");
        Assert.That(dir2.Select(f => f.Name), Is.EquivalentTo(new[] { "fILE.biN" }));

        var token = provider.Watch("AnOtHeR/");
        Assert.That(token.HasChanged, Is.False);

        provider["ANOTHer/fOO"] = new InMemoryFileInfo("fOO", "test");
        Assert.That(token.HasChanged, Is.True);
    }

    [Test]
    public void CaseSensitivity()
    {
        var provider = new InMemoryFileProvider(StringComparison.Ordinal)
        {
            ["Test.txt"] = new InMemoryFileInfo("test.Txt", "hello world"),
            ["Another/File.bin"] = new InMemoryFileInfo("file.Bin", new byte[] { 1, 2, 3, 4, 5 }),
            ["ANOTher/fILE.biN"] = new InMemoryFileInfo("fILE.biN", new byte[] { 1, 2, 3 }),
        };

        var file1 = provider.GetFileInfo("test.TXT");
        var file2 = provider.GetFileInfo("ANOTHER/file.BIN");

        Assert.Multiple(() =>
        {
            Assert.That(file1.Exists, Is.False);
            Assert.That(file2.Exists, Is.False);
        });

        var dir2 = provider.GetDirectoryContents("ANOTHER");
        Assert.That(dir2.Exists, Is.False);

        var token = provider.Watch("AnOtHeR/");
        Assert.That(token.HasChanged, Is.False);

        provider["ANOTHer/fOO"] = new InMemoryFileInfo("fOO", "test");
        Assert.That(token.HasChanged, Is.False);

        var dir2a = provider.GetDirectoryContents("Another");
        var dir2b = provider.GetDirectoryContents("ANOTher");

        Assert.Multiple(() =>
        {
            Assert.That(dir2a.Select(f => f.Name), Is.EquivalentTo(new[] { "file.Bin" }));
            Assert.That(dir2b.Select(f => f.Name), Is.EquivalentTo(new[] { "fILE.biN" }));
        });
    }

    [Test]
    public void Backslashes()
    {
        var provider = new InMemoryFileProvider
        {
            ["another\\file.bin"] = new InMemoryFileInfo("file.bin", new byte[] { 1, 2, 3, 4, 5 }),
        };

        var file1 = provider.GetFileInfo("another/file.bin");
        var file2 = provider.GetFileInfo("another\\file.bin");

        Assert.Multiple(() =>
        {
            Assert.That(file1, Is.SameAs(file2));
            Assert.That(file2.Exists, Is.True);
        });
    }

    [Test]
    public void DeleteDirectory()
    {
        var provider = new InMemoryFileProvider
        {
            ["subdir/one.txt"] = new InMemoryFileInfo("one.txt", "test1"),
            ["subdir/two.txt"] = new InMemoryFileInfo("two.txt", "test2"),
            ["zero.txt"] = new InMemoryFileInfo("zero.txt", "test0"),
        };

        var dir = provider.GetDirectoryContents("");
        Assert.That(dir.Select(f => f.Name), Is.EquivalentTo(new[] { "subdir", "zero.txt" }));

        provider["subdir/one.txt"] = null;
        
        dir = provider.GetDirectoryContents("");
        Assert.That(dir.Select(f => f.Name), Is.EquivalentTo(new[] { "subdir", "zero.txt" }));

        provider["subdir/two.txt"] = null;
        
        dir = provider.GetDirectoryContents("");
        Assert.That(dir.Select(f => f.Name), Is.EquivalentTo(new[] { "zero.txt" }));
        
        dir = provider.GetDirectoryContents("subdir");
        Assert.That(dir.Exists, Is.False);
    }

    private static byte[] Read(IFileInfo file)
    {
        using var stream = file.CreateReadStream();
        if (stream is MemoryStream ms)
        {
            return ms.ToArray();
        }

        using var output = new MemoryStream();
        stream.CopyTo(output);
        return output.ToArray();
    }
}