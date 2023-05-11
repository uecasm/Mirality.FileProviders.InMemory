using System.IO;
using System.Threading.Tasks;
using Microsoft.Extensions.FileProviders;

namespace Mirality.FileProviders;

/// <summary>Extension methods for <see cref="IFileProvider"/>.</summary>
public static class FileProviderExtensions
{
    /// <summary>Reads the entire file into a byte array.</summary>
    /// <param name="file">The file to read.</param>
    /// <returns>The contents of the file as a byte array.</returns>
    public static byte[] ReadAsBytes(this IFileInfo file)
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

    /// <summary>Reads the entire file into a byte array.</summary>
    /// <param name="file">The file to read.</param>
    /// <returns>(awaitable) The contents of the file as a byte array.</returns>
    public static async Task<byte[]> ReadAsBytesAsync(this IFileInfo file)
    {
        using var stream = file.CreateReadStream();
        if (stream is MemoryStream ms)
        {
            return ms.ToArray();
        }

        using var output = new MemoryStream();
        await stream.CopyToAsync(output);
        return output.ToArray();
    }

    /// <summary>Reads the entire UTF-8 text file into a string.</summary>
    /// <param name="file">The file to read.</param>
    /// <returns>The file content.</returns>
    public static string ReadAsText(this IFileInfo file)
    {
        using var reader = new StreamReader(file.CreateReadStream());
        return reader.ReadToEnd();
    }

    /// <summary>Reads the entire UTF-8 text file into a string.</summary>
    /// <param name="file">The file to read.</param>
    /// <returns>The file content.</returns>
    /// <exception cref="FileNotFoundException">The file does not exist.</exception>
    public static async Task<string> ReadAsTextAsync(this IFileInfo file)
    {
        using var reader = new StreamReader(file.CreateReadStream());
        return await reader.ReadToEndAsync();
    }
}
