﻿using System.Runtime.CompilerServices;

namespace PureHDF
{
    internal class H5D_Chunk123_BTree1 : H5D_Chunk
    {
        #region Fields

        private BTree1Node<BTree1RawDataChunksKey>? _btree1;

        private readonly DataLayoutMessage12? _layout12;
        private readonly DataLayoutMessage3? _layout3;
        private readonly ChunkedStoragePropertyDescription3? _chunked3;

        #endregion

        #region Constructors

        public H5D_Chunk123_BTree1(NativeDataset dataset, DataLayoutMessage12 layout, H5DatasetAccess datasetAccess) :
            base(dataset, datasetAccess)
        {
            _layout12 = layout;
        }

        public H5D_Chunk123_BTree1(NativeDataset dataset, DataLayoutMessage3 layout, H5DatasetAccess datasetAccess) :
            base(dataset, datasetAccess)
        {
            _layout3 = layout;
            _chunked3 = (ChunkedStoragePropertyDescription3)_layout3.Properties;
        }

        #endregion

        #region Methods

        protected override ulong[] GetRawChunkDims()
        {
            if (_layout12 is not null)
                return _layout12
                    .DimensionSizes
                    .Select(value => (ulong)value)
                    .ToArray();

            else if (_layout3 is not null && _chunked3 is not null)
                return _chunked3
                    .DimensionSizes
                    .Select(value => (ulong)value)
                    .ToArray();

            else
                throw new Exception("No layout information found.");
        }

        protected override ChunkInfo GetChunkInfo(ulong[] chunkIndices)
        {
            // load B-Tree 1
            if (_btree1 is null)
            {
                Dataset.Context.Driver.Seek((long)Dataset.DataLayoutMessage.Address, SeekOrigin.Begin);

                BTree1RawDataChunksKey decodeKey() => DecodeRawDataChunksKey(ChunkRank, RawChunkDims);

                _btree1 = new BTree1Node<BTree1RawDataChunksKey>(Dataset.Context, decodeKey);
            }

            // get key and child address
            var extendedChunkIndices = chunkIndices
                .Append(0UL)
                .ToArray();

            var success = _btree1
                        .TryFindUserData(out var userData,
                                        (leftKey, rightKey) => NodeCompare3(ChunkRank, extendedChunkIndices, leftKey, rightKey),
                                        (ulong address, BTree1RawDataChunksKey leftKey, out BTree1RawDataChunkUserData userData)
                                            => NodeFound(ChunkRank, chunkIndices, address, leftKey, out userData));

            return success
                ? new ChunkInfo(userData.ChildAddress, userData.ChunkSize, userData.FilterMask)
                : ChunkInfo.None;
        }

        #endregion

        #region Callbacks

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private BTree1RawDataChunksKey DecodeRawDataChunksKey(byte rank, ulong[] rawChunkDims)
        {
            return new BTree1RawDataChunksKey(Dataset.Context.Driver, rank, rawChunkDims);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int NodeCompare3(byte rank, ulong[] indices, BTree1RawDataChunksKey leftKey, BTree1RawDataChunksKey rightKey)
        {
            // H5Dbtree.c (H5D__btree_cmp3)

            /* Special case for faster checks on 1-D chunks */
            /* (Checking for ndims==2 because last dimension is the datatype size) */
            /* The additional checking for the right key is necessary due to the */
            /* slightly odd way the library initializes the right-most node in the */
            /* indexed storage B-tree... */
            if (rank == 1)
            {
                if (indices[0] > rightKey.ScaledChunkOffsets[0])
                    return 1;

                else if (indices[0] == rightKey.ScaledChunkOffsets[0] &&
                         indices[1] >= rightKey.ScaledChunkOffsets[1])
                    return 1;

                else if (indices[0] < leftKey.ScaledChunkOffsets[0])
                    return -1;
            }

            else
            {
                if (Utils.VectorCompare((byte)(rank + 1), indices, rightKey.ScaledChunkOffsets) >= 0)
                    return 1;

                else if (Utils.VectorCompare((byte)(rank + 1), indices, leftKey.ScaledChunkOffsets) < 0)
                    return -1;
            }

            return 0;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool NodeFound(byte rank, ulong[] indices, ulong address, BTree1RawDataChunksKey leftKey, out BTree1RawDataChunkUserData userData)
        {
            // H5Dbtree.c (H5D__btree_found)

            userData = default;

            /* Is this *really* the requested chunk? */
            for (int i = 0; i < rank; i++)
            {
                if (indices[i] >= leftKey.ScaledChunkOffsets[i] + 1)
                    return false;
            }

            userData.ChildAddress = address;
            userData.ChunkSize = leftKey.ChunkSize;
            userData.FilterMask = leftKey.FilterMask;

            return true;
        }

        #endregion
    }
}
