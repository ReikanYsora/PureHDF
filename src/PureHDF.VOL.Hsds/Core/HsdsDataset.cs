using System.Buffers;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Hsds.Api;
using PureHDF.VOL.Native;

namespace PureHDF.VOL.Hsds
{
    internal class HsdsDataset : HsdsAttributableObject, IH5Dataset
    {
        private IH5Dataspace? _space;
        private IH5DataType? _type;
        private IH5DataLayout? _layout;
        private readonly GetDatasetResponse _dataset;

        public HsdsDataset(InternalHsdsConnector connector, HsdsNamedReference reference) : base(connector, reference)
        {
            _dataset = connector.Client.Dataset.GetDataset(Id, connector.DomainName);
        }

        public IH5Dataspace Space
        {
            get 
            {
                _space ??= new HsdsDataspace(_dataset.Shape);
                return _space;
            }
        }

        public IH5DataType Type
        {
            get 
            {
                _type ??= new HsdsDataType(_dataset.Type);
                return _type;
            }
        }

        public IH5DataLayout Layout
        {
            get 
            {
                _layout ??= new HsdsDataLayout(_dataset.Layout);
                return _layout;
            }
        }

        public IH5FillValue FillValue => throw new NotImplementedException();

        public byte[] Read(
            Selection? fileSelection = null, 
            Selection? memorySelection = null, 
            ulong[]? memoryDims = null)
        {
            throw new NotImplementedException("This method is not (yet) implemented in the HSDS VOL connector.");
        }

        public T[] Read<T>(
            Selection? fileSelection = null, 
            Selection? memorySelection = null, 
            ulong[]? memoryDims = null) where T : unmanaged
        {
            return ReadCoreValueAsync<T>(default, useAsync: false, fileSelection, memorySelection, memoryDims)
                .GetAwaiter()
                .GetResult()!;
        }

        public void Read<T>(
            Memory<T> buffer, 
            Selection? fileSelection = null, 
            Selection? memorySelection = null, 
            ulong[]? memoryDims = null) where T : unmanaged
        {
            ReadCoreValueAsync(buffer, useAsync: false, fileSelection, memorySelection, memoryDims)
                .GetAwaiter()
                .GetResult();
        }

        public Task<byte[]> ReadAsync(
            Selection? fileSelection = null,
            Selection? memorySelection = null, 
            ulong[]? memoryDims = null,
            CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException("This method is not (yet) implemented in the HSDS VOL connector.");
        }

        public Task<T[]> ReadAsync<T>(
            Selection? fileSelection = null, 
            Selection? memorySelection = null, 
            ulong[]? memoryDims = null,
            CancellationToken cancellationToken = default) where T : unmanaged
        {
            return ReadCoreValueAsync<T>(default, useAsync: true, fileSelection, memorySelection, memoryDims, cancellationToken)!;
        }

        public Task ReadAsync<T>(
            Memory<T> buffer, 
            Selection? fileSelection = null, 
            Selection? memorySelection = null, 
            ulong[]? memoryDims = null,
            CancellationToken cancellationToken = default) where T : unmanaged
        {
            return ReadCoreValueAsync(buffer, useAsync: true, fileSelection, memorySelection, memoryDims, cancellationToken)!;
        }

        public T[] ReadCompound<T>(
            Func<FieldInfo, string>? getName = null, 
            Selection? fileSelection = null, 
            Selection? memorySelection = null, 
            ulong[]? memoryDims = null) where T : struct
        {
            throw new NotImplementedException("This method is not (yet) implemented in the HSDS VOL connector.");
        }

        public Dictionary<string, object?>[] ReadCompound(
            Selection? fileSelection = null, 
            Selection? memorySelection = null, 
            ulong[]? memoryDims = null)
        {
            throw new NotImplementedException("This method is not (yet) implemented in the HSDS VOL connector.");
        }

        public Task<T[]> ReadCompoundAsync<T>(
            Func<FieldInfo, string>? getName = null, 
            Selection? fileSelection = null, 
            Selection? memorySelection = null, 
            ulong[]? memoryDims = null,
            CancellationToken cancellationToken = default) where T : struct
        {
            throw new NotImplementedException("This method is not (yet) implemented in the HSDS VOL connector.");
        }

        public Task<Dictionary<string, object?>[]> ReadCompoundAsync(
            Selection? fileSelection = null, 
            Selection? memorySelection = null, 
            ulong[]? memoryDims = null,
            CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException("This method is not (yet) implemented in the HSDS VOL connector.");
        }

        public string?[] ReadString(
            Selection? fileSelection = null, 
            Selection? memorySelection = null, 
            ulong[]? memoryDims = null)
        {
            throw new NotImplementedException("This method is not (yet) implemented in the HSDS VOL connector.");
        }

        public Task<string?[]> ReadStringAsync(
            Selection? fileSelection = null, 
            Selection? memorySelection = null, 
            ulong[]? memoryDims = null,
            CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException("This method is not (yet) implemented in the HSDS VOL connector.");
        }

        private async Task<TResult[]?> ReadCoreValueAsync<TResult>(
            Memory<TResult> destination,
            bool useAsync,
            Selection? fileSelection = null,
            Selection? memorySelection = null,
            ulong[]? memoryDims = null,
            CancellationToken cancellationToken = default) where TResult : unmanaged
        {
            // TODO: enable this block of code and make use of "factor" (see NativeDataset.cs)

            // only allow size of T that matches bytesOfType or size of T = 1
            // var sizeOfT = (ulong)Unsafe.SizeOf<TResult>();
            // var bytesOfType = DataTypeMessage.Size;

            // if (bytesOfType % sizeOfT != 0)
            //     throw new Exception("The size of the generic parameter must be a multiple of the HDF5 file internal data type size.");

            // var factor = (int)(bytesOfType / sizeOfT);

            var result = await ReadCoreAsync(
                destination, 
                useAsync, 
                fileSelection, 
                memorySelection, 
                memoryDims, 
                cancellationToken).ConfigureAwait(false);

            /* ensure correct endianness */
            if (Type.Class == H5DataTypeClass.FixedPoint) // TODO are there other types that are byte order aware?
            {
                ByteOrder byteOrder;

                // TODO: is this reliable?
                if (_dataset.Type.Base!.EndsWith("LE"))
                    byteOrder = ByteOrder.LittleEndian;

                else if (_dataset.Type.Base!.EndsWith("BE"))
                    byteOrder = ByteOrder.BigEndian;

                else
                    byteOrder = ByteOrder.VaxEndian;

                Utils.EnsureEndianness(
                    source: MemoryMarshal.AsBytes(result.AsSpan()).ToArray() /* make copy of array */,
                    destination: MemoryMarshal.AsBytes(result.AsSpan()),
                    byteOrder,
                    (uint)Unsafe.SizeOf<TResult>()); // TODO: this is not 100% correct, it should be this.Type.Size.
            }

            return result;
        }

        private async Task<TResult[]?> ReadCoreAsync<TResult>(
            Memory<TResult> destination,
            bool useAsync,
            Selection? fileSelection = null, 
            Selection? memorySelection = null,
            ulong[]? memoryDims = null,
            CancellationToken cancellationToken = default) where TResult : unmanaged
        {
            // fast path for null dataspace
            if (Space.Type == H5DataspaceType.Null)
                return Array.Empty<TResult>();

            /* file selection */
            if (fileSelection is null)
            {
                switch (Space.Type)
                {
                    case H5DataspaceType.Scalar:
                    case H5DataspaceType.Simple:

                        var starts = Space.Dimensions.ToArray();
                        starts.AsSpan().Clear();

                        var stridesAndBlocks = Space.Dimensions.ToArray();
                        stridesAndBlocks.AsSpan().Fill(1);

                        fileSelection = new HyperslabSelection(
                            rank: Space.Dimensions.Length, 
                            starts: starts,
                            strides: stridesAndBlocks,
                            counts: Space.Dimensions,
                            blocks: stridesAndBlocks);

                        break;

                    case H5DataspaceType.Null:
                    default:
                        throw new Exception($"Unsupported data space type '{Space.Type}'.");
                }
            }

            string? select;

            if (fileSelection is HyperslabSelection hs)
            {
                var selectionStrings = Enumerable
                    .Range(0, hs.Rank)
                    .Select(dimension =>
                    {
                        if (hs.Blocks[dimension] != 1)
                            throw new Exception($"The HSDS selection API requires a hyperslab block size of 1.");

                        var start = hs.Starts[dimension];
                        var end = hs.Starts[dimension] + hs.Strides[dimension] * hs.Counts[dimension];
                        var step = hs.Strides[dimension];

                        return $"{start}:{end}:{step}";
                    });

                select = $"[{string.Join(',', selectionStrings)}]";
            }

            else
            {
                throw new Exception($"The selection of type {fileSelection.GetType().Name} is not supported.");
            }
            

            /* memory dims */
            var sourceElementCount = fileSelection.TotalElementCount;

            if (memorySelection is not null && memoryDims is null)
                throw new Exception("If a memory selection is specified, the memory dimensions must be specified, too.");

            memoryDims ??= new ulong[] { sourceElementCount };

            /* memory selection */
            memorySelection ??= new HyperslabSelection(start: 0, block: sourceElementCount);

            /* target buffer */
            var destinationElementCount = Utils.CalculateSize(memoryDims);

            EnsureBuffer(destination, destinationElementCount, out var optionalDestinationArray);
            var destinationMemory = optionalDestinationArray ?? destination;

            // TODO make use of selections
            var streamResponse = useAsync
                ? await Connector.Client.Dataset.GetValuesAsStreamAsync(_dataset.Id, Connector.DomainName, select: select, cancellationToken: cancellationToken)
                : Connector.Client.Dataset.GetValuesAsStream(_dataset.Id, Connector.DomainName, select: select);

            var stream = useAsync
                ? await streamResponse.Content.ReadAsStreamAsync(cancellationToken)
                : streamResponse.Content.ReadAsStream(cancellationToken);

            var byteMemory = new CastMemoryManager<TResult, byte>(destinationMemory).Memory;
            await ReadExactlyAsync(stream, buffer: byteMemory, useAsync: useAsync, cancellationToken);

            return optionalDestinationArray;
        }

        internal static void EnsureBuffer<TResult>(Memory<TResult> destination, ulong destinationElementCount, out TResult[]? newArray)
        {
            newArray = default;

            // user did not provide buffer
            if (destination.Equals(default))
            {
                // create the buffer
                newArray = new TResult[destinationElementCount];
            }

            // user provided buffer is too small
            else if (destination.Length < (int)destinationElementCount)
            {
                throw new Exception("The provided target buffer is too small.");
            }
        }

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
}