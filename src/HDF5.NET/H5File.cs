﻿using System;
using System.IO;

namespace HDF5.NET
{
    public class H5File : IDisposable
    {
        #region Fields

        private string _filePath;
        private bool _deleteOnClose;

        #endregion

        #region Constructors

        private H5File(string filePath, FileMode mode, FileAccess fileAccess, FileShare fileShare, bool deleteOnClose)
            : this(filePath, mode, fileAccess, fileShare)
        {
            _deleteOnClose = deleteOnClose;
        }

        private H5File(string filePath, FileMode mode, FileAccess fileAccess, FileShare fileShare)
        {
            _filePath = filePath;

            if (!BitConverter.IsLittleEndian)
                throw new Exception("This library only works on little endian systems.");

            this.Reader = new BinaryReader(File.Open(filePath, mode, fileAccess, fileShare));

            // superblock
            var signature = this.Reader.ReadBytes(8);
            this.ValidateSignature(signature, Superblock.FormatSignature);

            var version = this.Reader.ReadByte();

            this.Superblock = version switch
            {
                0 => new Superblock01(this.Reader, version),
                1 => new Superblock01(this.Reader, version),
                2 => new Superblock23(this.Reader, version),
                3 => new Superblock23(this.Reader, version),
                _ => throw new NotSupportedException($"The superblock version '{version}' is not supported.")
            };

            var superblock01 = this.Superblock as Superblock01;

            if (superblock01 != null)
            {
                var objectHeader = superblock01.RootGroupSymbolTableEntry.ObjectHeader;

                if (objectHeader != null)
                    this.Root = new H5Group(this, "/", objectHeader);
                else
                    throw new Exception("The root group object header is not allocated.");
            }
            else
            {
                var superblock23 = this.Superblock as Superblock23;

                if (superblock23 != null)
                {
                    var objectHeader = superblock23.RootGroupObjectHeader;
                    this.Root = new H5Group(this, "/", objectHeader);
                }
                else
                {
                    throw new Exception($"The superblock of type '{this.Superblock.GetType().Name}' is not supported.");
                }
            }
        }

        #endregion

        #region Properties

        public Superblock Superblock { get; set; }

        public H5Group Root { get; set; }

        internal BinaryReader Reader { get; set; }

        #endregion

        #region Methods

        internal static H5File Open(string filePath, FileMode mode, FileAccess fileAccess, FileShare fileShare, bool deleteOnClose)
        {
            return new H5File(filePath, mode, fileAccess, fileShare, deleteOnClose);
        }

        public static H5File Open(string filePath, FileMode mode, FileAccess fileAccess, FileShare fileShare)
        {
            return new H5File(filePath, mode, fileAccess, fileShare);
        }

        public void Dispose()
        {
            GlobalHeapCache.Clear(this.Superblock);
            this.Reader.Dispose();

            if (_deleteOnClose)
            {
                try
                {
                    File.Delete(_filePath);
                }
                catch
                {
                    //
                }
            }    
        }

        private void ValidateSignature(byte[] actual, byte[] expected)
        {
            if (actual.Length == expected.Length)
            {
                if (actual[0] == expected[0] && actual[1] == expected[1] && actual[2] == expected[2] && actual[3] == expected[3]
                 && actual[4] == expected[4] && actual[5] == expected[5] && actual[6] == expected[6] && actual[7] == expected[7])
                {
                    return;
                }
            }

            throw new Exception("The file is not a valid HDF 5 file.");
        }

        #endregion
    }
}
