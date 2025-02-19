﻿using System.Buffers;
using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using Amazon.S3;
using Amazon.S3.Model;

namespace PureHDF.VFD.AmazonS3;

/// <summary>
/// A stream reads data from Amazon S3.
/// </summary>
public class AmazonS3Stream : Stream, IDatasetStream, IDisposable
{
    private readonly ConcurrentDictionary<long, IMemoryOwner<byte>> _cache = new();
    private readonly int _cacheSlotSize;
    private readonly string _bucketName;
    private readonly string _key;
    private readonly AmazonS3Client _client;

    private readonly ThreadLocal<long> _position = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="AmazonS3Stream" /> instance.
    /// </summary>
    /// <param name="client">The Amazon S3 client.</param>
    /// <param name="bucketName">The bucket name.</param>
    /// <param name="key">The key that identifies the object in the bucket.</param>
    /// <param name="cacheSlotSize">The size of a single cache slot.</param>
    public AmazonS3Stream(AmazonS3Client client, string bucketName, string key, int cacheSlotSize = 1 * 1024 * 1024)
    {
        if (cacheSlotSize <= 0)
            throw new Exception("Cache slot size must be > 0");

        _client = client;
        _bucketName = bucketName;
        _key = key;
        _cacheSlotSize = cacheSlotSize;

        // https://registry.opendata.aws/nrel-pds-wtk/
        Length = client
            .GetObjectMetadataAsync(bucketName, key)
            .ConfigureAwait(false)
            .GetAwaiter()
            .GetResult()
            .ContentLength;
    }

    /// <inheritdoc />
    public override bool CanRead => true;

    /// <inheritdoc />
    public override bool CanSeek => true;

    /// <inheritdoc />
    public override bool CanWrite => false;

    /// <inheritdoc />
    public override long Length { get; }

    /// <inheritdoc />
    public override long Position
    {
        get => _position.Value;
        set => _position.Value = value;
    }

    /// <inheritdoc />
    public override int Read(byte[] buffer, int offset, int count)
    {
        var valueTask = ReadCachedAsync(buffer.AsMemory(offset, count), useAsync: false);

        if (!valueTask.IsCompleted)
            throw new Exception("This should never happen.");

        return valueTask
            .GetAwaiter()
            .GetResult();
    }

    /// <inheritdoc />
    public void ReadDataset(Memory<byte> buffer)
    {
        var valueTask = ReadUncachedAsync(buffer, useAsync: false);

        if (!valueTask.IsCompleted)
            throw new Exception("This should never happen.");

        _ = valueTask
            .GetAwaiter()
            .GetResult();
    }

    /// <inheritdoc />
#if NETSTANDARD2_0
    public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }
#else
    public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken)
    {
        return ReadCachedAsync(buffer, useAsync: true, cancellationToken);
    }
#endif

    /// <inheritdoc />
    public async ValueTask ReadDatasetAsync(Memory<byte> buffer, CancellationToken cancellationToken)
    {
        await ReadUncachedAsync(buffer, useAsync: true, cancellationToken);
        return;
    }

    /// <inheritdoc />
    public override long Seek(long offset, SeekOrigin origin)
    {
        switch (origin)
        {
            case SeekOrigin.Begin:

                _position.Value = offset;

                if (!(0 <= _position.Value && _position.Value < Length))
                    throw new Exception("The offset exceeds the stream length.");

                return _position.Value;

            case SeekOrigin.Current:

                _position.Value += offset;

                if (!(0 <= _position.Value && _position.Value < Length))
                    throw new Exception("The offset exceeds the stream length.");

                return _position.Value;
        }

        throw new Exception($"Seek origin '{origin}' is not supported.");
    }

    /// <inheritdoc />
    public override void SetLength(long value) => throw new NotImplementedException();

    /// <inheritdoc />
    public override void Flush() => throw new NotImplementedException();

    /// <inheritdoc />
    public override void Write(byte[] buffer, int offset, int count) => throw new NotImplementedException();

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            foreach (var entry in _cache)
            {
                entry.Value.Dispose();
            }
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private async ValueTask<int> ReadUncachedAsync(Memory<byte> buffer, bool useAsync, CancellationToken cancellationToken = default)
    {
        var stream = await ReadDataFromS3Async(
            start: Position,
            end: Position + buffer.Length,
            useAsync,
            cancellationToken)
            .ConfigureAwait(false);

        await ReadExactlyAsync(stream, buffer, useAsync, cancellationToken);

        return buffer.Length;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private async ValueTask<int> ReadCachedAsync(Memory<byte> buffer, bool useAsync, CancellationToken cancellationToken = default)
    {
        // TODO issue parallel requests
        var s3UpperLength = Math.Max(_cacheSlotSize, buffer.Length);
        var s3Remaining = Length - _position.Value;
        var s3ActualLength = (int)Math.Min(s3UpperLength, s3Remaining);
        var s3Processed = 0;
        var s3StartIndex = -1L;
        var remainingBuffer = buffer;

        bool loadFromS3;

        while (s3Processed < s3ActualLength)
        {
            var currentIndex = (_position.Value + s3Processed) / _cacheSlotSize;
            loadFromS3 = false;

            // determine if data is cached
            var owner = _cache.GetOrAdd(currentIndex, currentIndex =>
            {
                var owner = MemoryPool<byte>.Shared.Rent(_cacheSlotSize);

                // first index for which data will be requested
                if (s3StartIndex == -1)
                    s3StartIndex = currentIndex;

                loadFromS3 = true;

                return owner;
            });

            if (!loadFromS3 /* i.e. data is in cache */)
            {
                // is there a not yet loaded range of data?
                if (s3StartIndex != -1)
                {
                    var s3EndIndex = currentIndex + 1;
                    remainingBuffer = await LoadFromS3ToCacheAndBufferAsync(s3StartIndex, s3EndIndex, remainingBuffer, useAsync: useAsync, cancellationToken);
                    s3StartIndex = -1;
                }

                // copy from cache
                remainingBuffer = CopyFromCacheToBuffer(currentIndex, owner, remainingBuffer);
            }

            s3Processed += _cacheSlotSize;
        }

        // TODO code duplication
        // is there a not yet loaded range of data?
        if (s3StartIndex != -1)
        {
            var s3EndIndex = s3StartIndex + s3ActualLength / _cacheSlotSize;
            remainingBuffer = await LoadFromS3ToCacheAndBufferAsync(s3StartIndex, s3EndIndex, remainingBuffer, useAsync: useAsync, cancellationToken);
            s3StartIndex = -1;
        }

        return buffer.Length;
    }

    private async Task<Memory<byte>> LoadFromS3ToCacheAndBufferAsync(
        long s3StartIndex,
        long s3EndIndex,
        Memory<byte> remainingBuffer,
        bool useAsync,
        CancellationToken cancellationToken)
    {
        // get S3 stream
        var s3Start = s3StartIndex * _cacheSlotSize;
        var s3End = Math.Min(s3EndIndex * _cacheSlotSize, Length);

        var stream = await ReadDataFromS3Async(
            start: s3Start,
            end: s3End,
            useAsync,
            cancellationToken)
            .ConfigureAwait(false);

        // copy
        for (long currentIndex = s3StartIndex; currentIndex < s3EndIndex; currentIndex++)
        {
            var owner = _cache.GetOrAdd(currentIndex, _ => throw new Exception("This should never happen."));

            // copy to cache
            var buffer = owner.Memory[..(int)Math.Min(_cacheSlotSize, Length - Position)];
            await ReadExactlyAsync(stream, buffer, useAsync, cancellationToken);

            // copy to request buffer
            remainingBuffer = CopyFromCacheToBuffer(currentIndex, owner, remainingBuffer);
        }

        return remainingBuffer;
    }

    private Memory<byte> CopyFromCacheToBuffer(long currentIndex, IMemoryOwner<byte> owner, Memory<byte> remainingBuffer)
    {
        var s3Position = currentIndex * _cacheSlotSize;

        var cacheSlotOffset = _position.Value > s3Position
            ? (int)(_position.Value - s3Position)
            : 0;

        var remainingCacheSlotSize = _cacheSlotSize - cacheSlotOffset;

        var slicedMemory = owner.Memory
            .Slice(cacheSlotOffset, Math.Min(remainingCacheSlotSize, remainingBuffer.Length));

        slicedMemory.Span.CopyTo(remainingBuffer.Span);

        remainingBuffer = remainingBuffer[slicedMemory.Length..];
        _position.Value += slicedMemory.Length;

        return remainingBuffer;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private async ValueTask<Stream> ReadDataFromS3Async(long start, long end, bool useAsync, CancellationToken cancellationToken)
    {
        var request = new GetObjectRequest()
        {
            BucketName = _bucketName,
            Key = _key,
            ByteRange = new ByteRange(start, end)
        };

        var task = _client.GetObjectAsync(request, cancellationToken);

        var response = useAsync
            ? await task.ConfigureAwait(false)
            : task.GetAwaiter().GetResult();

        return response.ResponseStream;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static async Task ReadExactlyAsync(Stream stream, Memory<byte> buffer, bool useAsync, CancellationToken cancellationToken)
    {
        var slicedBuffer = buffer;

        while (slicedBuffer.Length > 0)
        {
            var readBytes = useAsync

                ? await stream
                    .ReadAsync(slicedBuffer, cancellationToken)
                    .ConfigureAwait(false)

                : stream.Read(slicedBuffer.Span);

            slicedBuffer = slicedBuffer[readBytes..];
        };
    }
}