﻿namespace PureHDF.VOL.Native;

[Flags]
internal enum DataspaceMessageFlags : byte
{
    DimensionMaxSizes = 1,
    PermuationIndices = 2
}