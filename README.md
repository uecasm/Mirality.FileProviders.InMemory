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

Simply instantiate the provider with your initial set of files (or add them later programmatically), optionally capturing the `IFileInfo` for later comparison:

```cs
var provider = new InMemoryFileProvider();
_ = provider.Write("file.txt", "file content");
var anotherFile = provider.Write("subdir/another.txt", "different content");
_ = provider.Write("subdir/more.bin", new byte[] { 0x42, 0x16, 0x63, 0xA7, 0x2B });
// or
var anotherFile = new InMemoryFileInfo("another.txt", "different content");
var provider = new InMemoryFileProvider
{
    ["file.txt"] = new InMemoryFileInfo("file.txt", "file content"),
    ["subdir/another.txt"] = anotherFile,
    ["subdir/more.bin"] = new InMemoryFileInfo("more.bin", new byte[] { 0x42, 0x16, 0x63, 0xA7, 0x2B }),
};
```

You can create a new file (or overwrite an existing file) via `Write` or simply by reassigning with a new value:

```cs
provider.Write("subdir/another.txt", "overwrite content");
provider.Write("subdir/yet_another.txt", "new file");
// or
provider["subdir/another.txt"] = new InMemoryFileInfo("another.txt", "overwrite content");
provider["subdir/yet_another.txt"] = new InMemoryFileInfo("yet_another.txt", "new file");
```

You can delete a file that was previously added via `Delete` or assigning either `null` or explicitly a [`NotFoundFileInfo`](https://learn.microsoft.com/en-us/dotnet/api/microsoft.extensions.fileproviders.notfoundfileinfo):

```cs
provider.Delete("subdir/another.txt");
// or
provider["subdir/another.txt"] = null;
// or
provider["subdir/another.txt"] = new NotFoundFileInfo("another.txt");
```

You can also read back via the indexer; this is equivalent to calling [`GetFileInfo`](https://learn.microsoft.com/en-us/dotnet/api/microsoft.extensions.fileproviders.ifileprovider.getfileinfo).  Attempting to read a path that doesn't exist will never give you a `null`; you will instead get a valid `IFileInfo` with `Exists == false`.

```cs
var file = provider.GetFileInfo("path/to/file.txt");
if (file.Exists) { ... }
// or
var file = provider["path/to/file.txt"];
if (file.Exists) { ... }
```

There is no direct `IEnumerable` support, but you can use [`GetDirectoryContents`](https://learn.microsoft.com/en-us/dotnet/api/microsoft.extensions.fileproviders.ifileprovider.getdirectorycontents) to enumerate as usual.

You can also [`Watch`](https://learn.microsoft.com/en-us/dotnet/api/microsoft.extensions.fileproviders.ifileprovider.watch) for changes.  You can watch either a full filename, a directory (by using a trailing `/`; this triggers if any file in that directory or recursive subdirs are changed), or a wildcard pattern using `*` and `**` globs (specifically, anything supported by [`FileSystemGlobbing`](https://learn.microsoft.com/en-us/dotnet/api/microsoft.extensions.filesystemglobbing.matcher), except for relative paths).  Change tokens are single-shot; once one triggers then you have to `Watch` again to pick up additional changes.

You may also be interested in using the [Mirality.WatchableValue](https://github.com/uecasm/Mirality.WatchableValue) library when watching a specific file for changes (as it introduces a way to combine `GetFileInfo` and `Watch` together), among other things, although that is not a required dependency for this library.

# Notes

* By default, the file provider uses case-insensitive (specifically: [`OrdinalIgnoreCase`](https://learn.microsoft.com/en-us/dotnet/api/system.stringcomparer.ordinalignorecase)) paths, although you can override this when creating it if you wish.
    * Filename case is preserved regardless of this setting, although see below for a caveat about directory name case.
* When using the indexer directly, the `Name` of the `InMemoryFileInfo` for each entry should be the matching filename alone without path.  Nothing in the `InMemoryFileProvider` itself requires or verifies this, but downstream consumers will likely misbehave if you break this rule.  Using the `Write` method will handle this for you.
* As shown in the examples, the preferred style is to use forward slashes for directory separators (as this is the most portable syntax), however it does tolerate backslashes and will treat them interchangeably with forward slashes.
* Other characters normally illegal in file paths are not treated as an error or otherwise filtered or validated, but just stored as-is.  Avoid using them if you want to remain compatible with other file providers that might be more strict.
* `InMemoryFileInfo.PhysicalPath` is always `null`, since these files do not exist on disk.
* Avoid using this in production in a scenario where you're creating and deleting (or watching) files with unique filenames all the time.  While deleting a file removes the associated data, it does not remove some internal storage related to the path itself -- so this usage pattern will lead to memory growth.  Also avoid using this for massive file trees.  Intended usage is for a relatively small number of mostly-fixed filenames -- this is not intended as a general-purpose large cache.

# Directories

The filesystem model does not actually store directories, only files.  This has a few consequences that may at first seem surprising:

* `.` and `..` elements in file paths are not resolved (and will be treated as distinct filenames); it's assumed that you either don't use these or have resolved them yourself prior to calling this provider.
* Empty directories are not currently preserved -- if you delete the last file in a directory then it will delete the directory as well.  Similarly, there is no syntax to create an empty directory -- simply create the first file inside it instead.
* Returned directory name case is unspecified (and can change as you manipulate the filesystem) when using a case-insensitive filesystem (as by default) and using inconsistent case in the path prefixes of filenames.
* While calling `GetFileInfo` on a (non-empty) directory will return a reasonable value for the most part, its `LastModified` timestamp is the max of all contained files, not actually the last modification to the directory as a whole.  In particular, deleting the most recent file in the directory can cause the directory timestamp to go backwards, not forwards.
* Enumeration order is not guaranteed and in particular may be different from the order that files were added.

# Using external `IFileInfo`

Using the indexer syntax (only), you can assign any arbitrary `IFileInfo` to the virtual filesystem -- you're not limited to only `InMemoryFileInfo`.  For example, this allows you to take physical files and virtually restructure them to a different directory layout (although you can't use this to rename them, without using an additional intermediate wrapper).

However note that in this case `Watch` will only report changes to the virtual filesystem itself, not any changes to the underlying files.

To handle this sort of thing, you will need to `Watch` the underlying provider yourself, and then trigger notifications on the in-memory provider by reassigning its indexer.  It will trigger notifications even if you re-assign the same `IFileInfo` instance, although it's recommended to fetch a new one to properly update associated properties such as the `Length` and `LastModified`.

One exception to this is that reassigning a non-existing `IFileInfo` to an already non-existing path (i.e. trying to delete a file that doesn't exist) will not trigger any notifications.

# `IWritableFileProvider` and `WritablePhysicalFileProvider`

Possibly I should put these into a different library (feedback welcome!), but these types are also included here.

The first is an interface which represents an `IFileProvider` that can accept file writes as well as just reads.  It is implemented by both of the providers in this library.

The second is intended as a wrapper around either a `PhysicalFileProvider` or a `CompositeFileProvider` that includes a `PhysicalFileProvider`, enabling writes to the corresponding physical directory.

These may be useful both for test scenarios where you do need real files, as well as in production code.  One advantage of using these over direct file manipulation is that the consumer of the provider need not be aware of where the containing directory is actually located (and indeed if there is a physical directory at all).  And the `WritablePhysicalFileProvider` prevents casual escape from the directory via `..` paths (although escape is technically still possible via mounts/junctions).

`IWritableFileProvider` itself is actually three interfaces -- notably, `Write` is provided by `ISyncWritableFileProvider` and `WriteAsync` is provided by `IAsyncWritableFileProvider`.  A provider is free to provide one or both of these, whichever makes more sense for its underlying implementation (to avoid hinting at asynchrony when always completed synchronously, for example, and vice versa), and consumers can either require a specific derived interface for their intended usage, or can accept the base interface and call the generic `WriteAsync`, which automatically delegates to the appropriate sub-interface.  Providers should not implement *only* the base `IWritableFileProvider` interface.

# Using with the generic or web hosts

While normally you wouldn't use this in your "real" application host, it's sometimes useful to write unit tests that create their own host, and use a virtual filesystem rather than a real one in those tests -- since in particular `Watch` is often slower and less reliable on a real filesystem, especially with the rapid-fire changes that tend to be happening in test cases.

For the generic host:

```cs
var provider = new InMemoryFileProvider();
var host = Host.CreateDefaultBuilder(args)
    .ConfigureAppConfiguration((ctx, cfg) =>
    {
        ctx.HostingEnvironment.ContentRootFileProvider = provider;
        _ = cfg.SetFileProvider(provider);
    })
    // ...
    .Build();
```

For the web application:

```cs
var provider = new InMemoryFileProvider();
var builder = WebApplication.CreateBuilder(args);

builder.Environment.ContentRootFileProvider = provider;
_ = builder.Configuration.SetFileProvider(provider);

// ...
```

This sort of thing will only work for code that does actually make use of the `ContentRootFileProvider` rather than going directly to the filesystem.  This does include your `appsettings.json` file -- if you don't want to virtualize that, you can omit the `Configuration` call above.  For other code, you may need to modify it to use the file provider interfaces instead.

## Making the content root writable, while keeping it real

Similar to the above, you can use `WritablePhysicalFileProvider` instead of `InMemoryFileProvider`, if you want to use the real ContentRoot filesystem while retaining the ability to write to it -- though you should be very careful about doing this in a production app, since the changes will be persistent and improper validation could trash something you care about, possibly including the application files themselves.

```cs
.ConfigureAppConfiguration((ctx, cfg) =>
{
    var provider = new WritablePhysicalFileProvider(ctx.HostingEnvironment.ContentRootPath, ctx.HostingEnvironment.ContentRootFileProvider);
    ctx.HostingEnvironment.ContentRootFileProvider = provider;
    _ = cfg.SetFileProvider(provider);
})
```

## What about the WebRoot?

In a web application, you have an `IWebHostEnvironment`, which also has a `WebRootFileProvider`.  Should you change this too?

The short answer is: usually not.

The longer answer is that if you're certain you want to anyway, then you will typically have to set it to a `CompositeFileProvider` consisting of both your new `InMemoryFileProvider` and the previous `WebRootFileProvider` (in that order).  Otherwise it's very likely that you will break your app.

When using `WritablePhysicalFileProvider`, this already chains to the original provider, so you don't need to add an extra composite.

## What about in a library?

Only the application itself should try to alter a global file provider like this.  Libraries should instead try to make use of the supplied provider if it meets their needs, or throw an exception if not (telling the application author how to configure appropriately to resolve it).  Or they can use their own separate internal providers -- though for testing convenience it's a good idea to provide some way to hook or override it if needed.
