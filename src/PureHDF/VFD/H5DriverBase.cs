﻿namespace PureHDF.VFD;

internal abstract class H5DriverBase : IH5ReadStream
{
    private ulong _baseAddress;

    public H5DriverBase(long length)
    {
        Length = length;
    }

    public ulong BaseAddress { get => _baseAddress; }

    public abstract long Position { get; }
    public long Length { get; }

    public abstract void ReadDataset(Memory<byte> buffer);
    public abstract ValueTask ReadDatasetAsync(Memory<byte> buffer, CancellationToken cancellationToken);
    public abstract void Seek(long offset, SeekOrigin origin);

    public abstract byte ReadByte();
    public abstract byte[] ReadBytes(long count);
    public abstract ushort ReadUInt16();
    public abstract short ReadInt16();
    public abstract uint ReadUInt32();
    public abstract ulong ReadUInt64();

    public void SetBaseAddress(ulong baseAddress)
    {
        _baseAddress = baseAddress;
    }

    #region IDisposable

    private bool _disposedValue;

    protected virtual void Dispose(bool disposing)
    {
        if (!_disposedValue)
            _disposedValue = true;
    }

    public void Dispose()
    {
        Dispose(disposing: true);
    }

    #endregion
}