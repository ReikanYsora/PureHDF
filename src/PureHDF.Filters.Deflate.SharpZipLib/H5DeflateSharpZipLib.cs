﻿using ICSharpCode.SharpZipLib.Zip.Compression;
using ICSharpCode.SharpZipLib.Zip.Compression.Streams;

namespace PureHDF.Filters;

/// <summary>
/// Contains a function to enable support for the Deflate filter based on SharpZipLib.
/// </summary>
public static class H5DeflateSharpZipLib
{
    /// <summary>
    /// Gets the filter function.
    /// </summary>
    public unsafe static FilterFunction FilterFunction { get; } = (flags, parameters, buffer) =>
    {
        /* We're decompressing */
        if (flags.HasFlag(H5FilterFlags.Decompress))
        {
            using var sourceStream = new MemorySpanStream(buffer);
            using var tar = new MemoryStream(buffer.Length /* minimum size to expect */);

            // skip ZLIB header to get only the DEFLATE stream
            sourceStream.Seek(2, SeekOrigin.Begin);

            using var decompressionStream = new InflaterInputStream(sourceStream, new Inflater(noHeader: true))
            {
                IsStreamOwner = false
            };

            decompressionStream.CopyTo(tar);

            return tar
                .GetBuffer()
                .AsMemory(0, (int)tar.Length);
        }

        /* We're compressing */
        else
        {
            throw new Exception("Writing data chunks is not yet supported by PureHDF.");
        }
    };
}