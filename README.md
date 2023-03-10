![.NET Standard 2.0](https://img.shields.io/static/v1?label=.NET&message=Std2.0&color=blue) [![NuGet version (Mirality.FileProviders.InMemory)](https://img.shields.io/nuget/v/Mirality.FileProviders.InMemory.svg?logo=nuget)](https://www.nuget.org/packages/Mirality.FileProviders.InMemory/)

This extends [`IFileProvider`](https://learn.microsoft.com/en-us/dotnet/api/microsoft.extensions.fileproviders.ifileprovider) with a purely in-memory implementation (`InMemoryFileProvider`).

This is primarily intended for testing code that uses `IFileProvider` (and where a simple stub is insufficient or too messy, and you don't want to touch the real filesystem); however this is implemented reasonably robustly and can be used in production code if you find a need -- presumably in conjunction with [`CompositeFileProvider`](https://learn.microsoft.com/en-us/dotnet/api/microsoft.extensions.fileproviders.compositefileprovider) and another provider (either as a fallback or an override, depending on order).

The `InMemoryFileProvider` is fully mutable and thread-safe, and supports `Watch`ing files, directories, and wildcards.

It is compiled for .NET Standard 2.0, so it can be used from .NET Framework 4.7.2+ as well as .NET Core/5+.  As of this writing, it is primarily tested with .NET 6.

# Installation

Simply add the NuGet package as usual.

In some cases you may get compiler warnings about package version conflicts for some `Microsoft.Extensions.*` packages.  The best known way to resolve these is to add explicit package references to your application project for whichever version you want to actually include (typically the highest).

# Usage

All public classes and methods have XML documentation, so your IDE can give you more specific reference docs.

Simply instantiate the provider with your initial set of files (or add them later programmatically):

```cs
var provider = new InMemoryFileProvider
{
    ["file.txt"] = new InMemoryFileInfo("file.txt", "file content"),
    ["subdir/another.txt"] = new InMemoryFileInfo("another.txt", "different content"),
    ["subdir/more.bin"] = new InMemoryFileInfo("more.bin", new byte[] { 0x42, 0x16, 0x63, 0xA7, 0x2B }),
};
```

You can create a new file (or overwrite an existing file) simply by reassigning with a new value:

```cs
provider["subdir/another.txt"] = new InMemoryFileInfo("another.txt", "overwrite content");
provider["subdir/yet_another.txt"] = new InMemoryFileInfo("yet_another.txt", "new file");
```

You can delete a file that was previously added either by assigning `null` or explicitly a [`NotFoundFileInfo`](https://learn.microsoft.com/en-us/dotnet/api/microsoft.extensions.fileproviders.notfoundfileinfo):

```cs
provider["subdir/another.txt"] = null;
```

You can also read back via the indexer; this is equivalent to calling [`GetFileInfo`](https://learn.microsoft.com/en-us/dotnet/api/microsoft.extensions.fileproviders.ifileprovider.getfileinfo).  Attempting to read a path that doesn't exist will never give you a `null`; you will instead get a valid `IFileInfo` with `Exists == false`.

```cs
var file = provider["path/to/file.txt"];
if (file.Exists) { ... }
```

There is no direct `IEnumerable` support, but you can use [`GetDirectoryContents`](https://learn.microsoft.com/en-us/dotnet/api/microsoft.extensions.fileproviders.ifileprovider.getdirectorycontents) to enumerate as usual.

You can also [`Watch`](https://learn.microsoft.com/en-us/dotnet/api/microsoft.extensions.fileproviders.ifileprovider.watch) for changes.  You can watch either a full filename, a directory (by using a trailing `/`; this triggers if any file in that directory or recursive subdirs are changed), or a wildcard pattern using `*` and `**` globs (specifically, anything supported by [`FileSystemGlobbing`](https://learn.microsoft.com/en-us/dotnet/api/microsoft.extensions.filesystemglobbing.matcher), except for relative paths).  Change tokens are single-shot; once one triggers then you have to `Watch` again to pick up additional changes.

# Notes

* By default, the file provider uses case-insensitive (specifically: [`OrdinalIgnoreCase`](https://learn.microsoft.com/en-us/dotnet/api/system.stringcomparer.ordinalignorecase)) paths, although you can override this when creating it if you wish.
    * Filename case is preserved regardless of this setting, although directory name case is unspecified if you use more than one variation in your file paths combined with a case-insensitive comparison.
* `.` and `..` elements in file paths are not resolved (and will be treated as distinct filenames); it's assumed that you either don't use these or have resolved them yourself prior to calling this provider.
* The `Name` of the `InMemoryFileInfo` for each entry should be the matching filename alone without path.  Nothing in the `InMemoryFileProvider` itself requires or verifies this, but downstream consumers will likely misbehave if you break this rule.
* As shown in the examples, the preferred style is to use forward slashes for directory separators (as this is the most portable syntax), however it does tolerate backslashes and will treat them interchangeably with forward slashes.
* Other characters normally illegal in file paths are not treated as an error or otherwise filtered or validated, but just stored as-is.  Avoid using them if you want to remain compatible with other file providers that might be more strict.
* Empty directories are not currently preserved -- if you delete the last file in a directory then it will delete the directory as well.  Similarly, there is no syntax to create an empty directory -- simply create the first file inside it instead.
* Enumeration order is not guaranteed and in particular may be different from the order that files were added.
* `InMemoryFileInfo.PhysicalPath` is always `null`, since these files do not exist on disk.  However, it's legal to store other `IFileInfo` that do represent physical (or otherwise) files into the `InMemoryFileProvider` and effectively rearrange them to a virtual filesystem, although note that `Watch` will only tell you when the virtual filesystem itself is updated, not if the underlying file is changed in any other way.
    * To handle this sort of thing, you will need to `Watch` the underlying provider yourself, and then trigger notifications on the in-memory provider by reassigning its indexer.  It will trigger notifications even if you re-assign the same `IFileInfo` instance, although it's recommended to fetch a new one to properly update associated properties such as the `Length` and `LastModified`.
    * One exception to this is that reassigning a non-existing path to another non-existing file (i.e. trying to delete a file that doesn't exist) will not trigger any notifications.
* Avoid using this in production in a scenario where you're creating and deleting (or watching) files with unique filenames all the time.  While deleting a file removes the associated data, it does not remove some internal storage related to the path itself -- so this usage pattern will lead to memory growth.  Also avoid using this for massive file trees.  Intended usage is for a relatively small number of mostly-fixed filenames.
