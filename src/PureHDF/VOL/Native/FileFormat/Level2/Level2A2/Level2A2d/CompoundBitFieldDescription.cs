﻿namespace PureHDF.VOL.Native;

internal class CompoundBitFieldDescription : DatatypeBitFieldDescription
{
    #region Constructors

    public CompoundBitFieldDescription(H5DriverBase driver) : base(driver)
    {
        //
    }

    #endregion

    #region Properties

    public ushort MemberCount
    {
        get
        {
            return (ushort)(Data[0] + (Data[1] << 8));
        }
        set
        {
            Data[0] = (byte)(value & 0x00FF);
            Data[1] = (byte)((value & 0xFF00) >> 8);
        }
    }

    #endregion
}