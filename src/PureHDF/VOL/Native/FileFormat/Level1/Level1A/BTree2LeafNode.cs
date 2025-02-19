﻿using System.Text;

namespace PureHDF.VOL.Native;

internal class BTree2LeafNode<T> : BTree2Node<T> where T : struct, IBTree2Record
{
    #region Constructors

    public BTree2LeafNode(H5DriverBase driver, BTree2Header<T> header, ushort recordCount, Func<T> decodeKey)
        : base(driver, header, recordCount, BTree2LeafNode<T>.Signature, decodeKey)
    {
        // checksum
        Checksum = driver.ReadUInt32();
    }

    #endregion

    #region Properties

    public static byte[] Signature { get; } = Encoding.ASCII.GetBytes("BTLF");

    #endregion
}
