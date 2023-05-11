using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Threading;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.FileSystemGlobbing;
using Microsoft.Extensions.Primitives;

namespace Mirality.FileProviders.InMemory;

/// <summary>An <see cref="IWritableFileProvider"/> that stores "files" in-memory rather than in a real filesystem.
/// It supports <see cref="Watch">watching for changes</see>.</summary>
/// <remarks>
/// <para>While this is primarily intended for unit testing code that uses either that interface or
/// <see cref="IFileProvider"/> (and that has more complex requirements than using a simple stub), this can also
/// be used in production apps if required, typically in conjunction with <c>CompositeFileProvider</c> and some
/// other provider.</para>
/// <para>Currently, empty directories are not supported; if you remove all files within a directory it will
/// automatically remove the directory as well.  This might change in a future release if people ask for it.</para>
/// <para>This makes no attempt to resolve <c>..</c> or reject otherwise illegal characters in filenames.</para>
/// </remarks>
public class InMemoryFileProvider : ISyncWritableFileProvider
{
    private readonly StringComparison _CaseSensitivity;
    private readonly StringComparer _CaseSensitivityComparer;
    private readonly ConcurrentDictionary<string, (IFileInfo File, CancellationTokenSource Changed, IChangeToken Token)> _Files;
    private readonly ConcurrentDictionary<string, (Matcher Matcher, CancellationTokenSource Changed, IChangeToken Token)> _Wildcards;

#if NETSTANDARD2_0
    private static readonly Dictionary<StringComparison, Func<StringComparer>> StringComparers = new()
    {
        [StringComparison.CurrentCulture] = () => StringComparer.CurrentCulture,
        [StringComparison.CurrentCultureIgnoreCase] = () => StringComparer.CurrentCultureIgnoreCase,
        [StringComparison.InvariantCulture] = () => StringComparer.InvariantCulture,
        [StringComparison.InvariantCultureIgnoreCase] = () => StringComparer.InvariantCultureIgnoreCase,
        [StringComparison.Ordinal] = () => StringComparer.Ordinal,
        [StringComparison.OrdinalIgnoreCase] = () => StringComparer.OrdinalIgnoreCase,
    };
#endif

    /// <summary>Constructor.</summary>
    /// <param name="caseSensitivity">By default, the filesystem is treated as case-insensitive, but you can override that if you wish.</param>
    public InMemoryFileProvider(StringComparison caseSensitivity = StringComparison.OrdinalIgnoreCase)
    {
        _CaseSensitivity = caseSensitivity;
#if NETSTANDARD2_0
        _CaseSensitivityComparer = StringComparers[_CaseSensitivity]();
#else
        _CaseSensitivityComparer = StringComparer.FromComparison(_CaseSensitivity);
#endif
        _Files = new ConcurrentDictionary<string, (IFileInfo File, CancellationTokenSource Changed, IChangeToken Token)>(_CaseSensitivityComparer);
        _Wildcards = new ConcurrentDictionary<string, (Matcher Matcher, CancellationTokenSource Changed, IChangeToken Token)>(_CaseSensitivityComparer);
    }

    /// <summary>Gets an existing file; or creates, overwrites, or deletes a file.</summary>
    /// <param name="path">The full relative file path.</param>
    /// <returns>
    /// <para>The <see cref="IFileInfo"/> for this path.</para>
    /// <para>The getter will never return <see langword="null"/> (but instead an object with
    /// <c><see cref="IFileInfo.Exists">Exists</see> == <see langword="false"/></c>).</para>
    /// <para>The setter will accept <see langword="null"/> and interprets this as a delete of the corresponding file, if it exists.</para>
    /// </returns>
    [AllowNull]
    public IFileInfo this[string path]
    {
        get
        {
            path = NormalizePath(path);
            return _Files.TryGetValue(path, out var value) ? value.File : TryGetDirectory(path);
        }
        set => _ = Change(NormalizePath(path), value);
    }

    /// <inheritdoc />
    public Stream Create(string path)
    {
        return new InMemoryFileStream(this, path);
    }

    /// <inheritdoc />
    public IFileInfo Write(string path, byte[] content, DateTimeOffset? lastModified = null)
    {
        var fileInfo = new InMemoryFileInfo(Path.GetFileName(path), content, lastModified);
        this[path] = fileInfo;
        return fileInfo;
    }

    /// <inheritdoc />
    public void Delete(string path)
    {
        this[path] = null!;
    }

    private IChangeToken Change(string path, IFileInfo? value)
    {
        value ??= new NotFoundFileInfo(Path.GetFileName(path));

        CancellationTokenSource? oldChanged = null;

        if (_Files.TryGetValue(path, out var file))
        {
            if (!file.File.Exists && !value.Exists)
            {
                return file.Token;
            }

            oldChanged = file.Changed;
        }

        var changed = new CancellationTokenSource();
        var token = new CancellationChangeToken(changed.Token);
        _Files[path] = (value, changed, token);

        oldChanged?.Cancel();

        foreach (var wildcard in _Wildcards)
        {
            if (wildcard.Value.Matcher.Match(path).HasMatches)
            {
                _ = _Wildcards.TryRemove(wildcard.Key, out _);
                wildcard.Value.Changed.Cancel();
            }
        }

        return token;
    }

    private IFileInfo TryGetDirectory(string path)
    {
        path = NormalizePath(path);
        var name = Path.GetFileName(path);
        var files = GetDirectoryFiles(path);
        return files.Any() ? new InMemoryDirectoryInfo(name, files.Max(f => f.LastModified)) : new NotFoundFileInfo(name);
    }

    /// <inheritdoc />
    public IDirectoryContents GetDirectoryContents(string subpath)
    {
        subpath = NormalizePath(subpath);
        var files = GetDirectoryFiles(subpath);
        return files.Any() ? new InMemoryDirectoryContents(files) : NotFoundDirectoryContents.Singleton;
    }

    /// <inheritdoc />
    public IFileInfo GetFileInfo(string subpath)
    {
        return this[subpath];
    }

    /// <inheritdoc />
    public IChangeToken Watch(string filter)
    {
        filter = NormalizePath(filter);
        if (filter.Contains('*') || (filter.Length > 0 && filter[filter.Length - 1] == '/'))
        {
            var wildcard = _Wildcards.GetOrAdd(filter, f =>
            {
                var matcher = new Matcher(_CaseSensitivity).AddInclude(f);
                var changed = new CancellationTokenSource();
                var token = new CancellationChangeToken(changed.Token);
                return (matcher, changed, token);
            });
            return wildcard.Token;
        }

        return _Files.TryGetValue(filter, out var file) ? file.Token : Change(filter, new NotFoundFileInfo(Path.GetFileName(filter)));
    }

    private List<IFileInfo> GetDirectoryFiles(string path)
    {
        var subpath = path != string.Empty && path[path.Length - 1] != '/' ? path + '/' : path;
        var files = new List<IFileInfo>();
        var directories = new HashSet<string>(_CaseSensitivityComparer);

        foreach (var file in _Files.Where(f => f.Key.StartsWith(subpath, _CaseSensitivity) && f.Value.File.Exists))
        {
            var index = file.Key.IndexOf('/', subpath.Length + 1);
            if (index >= 0)
            {
                var name = file.Key.Substring(0, index);
                if (directories.Add(name))
                {
                    files.Add(TryGetDirectory(name));
                }
            }
            else
            {
                files.Add(file.Value.File);
            }
        }

        return files;
    }

    private static string NormalizePath(string path)
    {
        if (path == null) { throw new ArgumentNullException(nameof(path)); }
        return path.Replace("\\", "/").TrimStart('/');
    }
}