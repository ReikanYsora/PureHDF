﻿namespace PureHDF.VOL.Native;

internal class NativeCommitedDatatype : NativeAttributableObject, IH5CommitedDatatype
{
    #region Constructors

    internal NativeCommitedDatatype(NativeContext context, NativeNamedReference reference, ObjectHeader header)
        : base(context, reference, header)
    {
        Datatype = header.GetMessage<DatatypeMessage>();
    }

    #endregion

    #region Properties

    internal DatatypeMessage Datatype { get; }

    #endregion
}