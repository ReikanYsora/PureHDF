﻿namespace PureHDF.VOL.Native;

internal class RegularHyperslabSelectionInfo : HyperslabSelectionInfo
{
    #region Constructors

    public RegularHyperslabSelectionInfo(H5DriverBase driver, uint rank, byte encodeSize)
    {
        Rank = rank;

        Starts = new ulong[Rank];
        Strides = new ulong[Rank];
        Counts = new ulong[Rank];
        Blocks = new ulong[Rank];

        CompactDimensions = new ulong[Rank];

        for (int i = 0; i < Rank; i++)
        {
            Starts[i] = H5S_SEL.ReadEncodedValue(driver, encodeSize);
            Strides[i] = H5S_SEL.ReadEncodedValue(driver, encodeSize);
            Counts[i] = H5S_SEL.ReadEncodedValue(driver, encodeSize);
            Blocks[i] = H5S_SEL.ReadEncodedValue(driver, encodeSize);

            CompactDimensions[i] = Blocks[i] * Counts[i];
        }
    }

    #endregion

    #region Properties

    public byte Flags { get; set; }
    public uint Length { get; set; }
    public ulong[] Starts { get; set; }
    public ulong[] Strides { get; set; }
    public ulong[] Counts { get; set; }
    public ulong[] Blocks { get; set; }
    public ulong[] CompactDimensions { get; set; }

    #endregion
}