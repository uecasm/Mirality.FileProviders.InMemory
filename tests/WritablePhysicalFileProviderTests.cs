namespace Mirality.FileProviders.InMemory.Tests;

public class WritablePhysicalFileProviderTests
{
    private string _TestDirectory = default!;
    private PhysicalFileProvider _PhysicalFileProvider = default!;

    [SetUp]
    public void SetUp()
    {
        _TestDirectory = Path.Combine(Path.GetTempPath(), "Mirality.FileProviders.InMemory.Tests");
        if (Directory.Exists(_TestDirectory))
        {
            Directory.Delete(_TestDirectory, true);
        }
        Directory.CreateDirectory(_TestDirectory);

        _PhysicalFileProvider = new PhysicalFileProvider(_TestDirectory);
    }

    [TearDown]
    public void TearDown()
    {
        _PhysicalFileProvider.Dispose();

        if (Directory.Exists(_TestDirectory))
        {
            Directory.Delete(_TestDirectory, true);
        }
    }

    [Test]
    public void Create()
    {
        var provider = new WritablePhysicalFileProvider(_TestDirectory, _PhysicalFileProvider);

        using (var stream = provider.Create("test.bin"))
        {
            stream.Write(new byte[] { 1, 2, 3, 4 }, 0, 4);
        }

        Assert.That(_PhysicalFileProvider.GetFileInfo("test.bin").Exists);
    }

    [Test]
    public void WriteBytesSync()
    {
        var provider = new WritablePhysicalFileProvider(_TestDirectory, _PhysicalFileProvider);

        var timestamp = new DateTimeOffset(2021, 12, 25, 12, 34, 56, TimeSpan.Zero);
        var bytes = new byte[] { 1, 2, 3, 4, 5, 6 };
        var file = provider.Write("test.bin", bytes, timestamp);

        Assert.That(file.Exists);
        Assert.That(_PhysicalFileProvider.GetFileInfo("test.bin").Exists);
        Assert.That(_PhysicalFileProvider.GetFileInfo("test.bin").LastModified, Is.EqualTo(timestamp));
    }

    [Test]
    public void WriteTextSync()
    {
        var provider = new WritablePhysicalFileProvider(_TestDirectory, _PhysicalFileProvider);

        var timestamp = new DateTimeOffset(2021, 12, 25, 12, 34, 56, TimeSpan.Zero);
        var file = provider.Write("test.txt", "hello world", timestamp);

        Assert.That(file.Exists);
        Assert.That(_PhysicalFileProvider.GetFileInfo("test.txt").Exists);
        Assert.That(_PhysicalFileProvider.GetFileInfo("test.txt").LastModified, Is.EqualTo(timestamp));
    }

    [Test]
    public async Task WriteBytesAsync()
    {
        var provider = new WritablePhysicalFileProvider(_TestDirectory, _PhysicalFileProvider);

        var timestamp = new DateTimeOffset(2021, 12, 25, 12, 34, 56, TimeSpan.Zero);
        var bytes = new byte[] { 1, 2, 3, 4, 5, 6 };
        var file = await provider.WriteAsync("test.bin", bytes, timestamp);

        Assert.That(file.Exists);
        Assert.That(_PhysicalFileProvider.GetFileInfo("test.bin").Exists);
        Assert.That(_PhysicalFileProvider.GetFileInfo("test.bin").LastModified, Is.EqualTo(timestamp));
    }

    [Test]
    public async Task WriteTextAsync()
    {
        var provider = new WritablePhysicalFileProvider(_TestDirectory, _PhysicalFileProvider);

        var timestamp = new DateTimeOffset(2021, 12, 25, 12, 34, 56, TimeSpan.Zero);
        var file = await provider.WriteAsync("test.txt", "hello world", timestamp);

        Assert.That(file.Exists);
        Assert.That(_PhysicalFileProvider.GetFileInfo("test.txt").Exists);
        Assert.That(_PhysicalFileProvider.GetFileInfo("test.txt").LastModified, Is.EqualTo(timestamp));
    }

    [Test]
    public void Delete()
    {
        File.WriteAllText(Path.Combine(_TestDirectory, "test.txt"), "test");

        var provider = new WritablePhysicalFileProvider(_TestDirectory, _PhysicalFileProvider);

        provider.Delete("test.txt");

        Assert.That(_PhysicalFileProvider.GetFileInfo("test.txt").Exists, Is.False);
    }

    [Test]
    public void GetDirectoryContents()
    {
        File.WriteAllText(Path.Combine(_TestDirectory, "test1.txt"), "test");
        File.WriteAllText(Path.Combine(_TestDirectory, "test2.txt"), "test");

        var provider = new WritablePhysicalFileProvider(_TestDirectory, _PhysicalFileProvider);

        var contents = provider.GetDirectoryContents("");
        Assert.That(contents.Exists);
        Assert.That(contents.Select(f => f.Name), Is.EquivalentTo(new[] { "test1.txt", "test2.txt" }));
    }

    [Test]
    public void Watch()
    {
        var provider = new WritablePhysicalFileProvider(_TestDirectory, _PhysicalFileProvider);

        var token = provider.Watch("*.txt");
        Assert.That(token.HasChanged, Is.False);

        provider.Write("test.txt", "test");

        Thread.Sleep(_PhysicalFileProvider.UsePollingFileWatcher ? TimeSpan.FromSeconds(5) : TimeSpan.FromMilliseconds(25));
        Assert.That(token.HasChanged, Is.True);
    }

    [Test]
    public void CannotEscape()
    {
        var provider = new WritablePhysicalFileProvider(_TestDirectory, _PhysicalFileProvider);

        Assert.Multiple(() =>
        {
            Assert.That(() => provider.Write("..\\test.txt", "test"), Throws.InstanceOf<ArgumentOutOfRangeException>());
            Assert.That(() => provider.Write("foo\\\\.\\..\\test.txt", "test"), Throws.Nothing);
            Assert.That(() => provider.Write("foo\\\\.\\..\\..\\test.txt", "test"), Throws.InstanceOf<ArgumentOutOfRangeException>());
        });
    }

    [Test]
    public void CannotNest()
    {
        var provider = new WritablePhysicalFileProvider(_TestDirectory, _PhysicalFileProvider);

        Assert.That(() => new WritablePhysicalFileProvider(_TestDirectory, provider), Throws.ArgumentException);
    }
}
