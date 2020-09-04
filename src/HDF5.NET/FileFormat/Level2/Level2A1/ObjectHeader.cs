using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace HDF5.NET
{
    public abstract class ObjectHeader : FileBlock
    {
        private ulong gapSize;
        #region Constructors

        public ObjectHeader(BinaryReader reader) : base(reader)
        {
            this.HeaderMessages = new List<HeaderMessage>();
        }

        #endregion

        #region Properties

        public List<HeaderMessage> HeaderMessages { get; }

        public H5ObjectType ObjectType { get; protected set; }

        #endregion

        #region Methods

        public T GetMessage<T>() where T : Message
        {
            return (T)this.HeaderMessages
                .First(message => message.Data.GetType() == typeof(T))
                .Data;
        }

        public IEnumerable<T> GetMessages<T>() where T : Message
        {
            return this.HeaderMessages
                .Where(message => message.Data.GetType() == typeof(T))
                .Select(message => message.Data)
                .Cast<T>();
        }

        public static ObjectHeader Construct(BinaryReader reader, Superblock superblock)
        {
            // get version
            var version = reader.ReadByte();

            // must be a version 2+ object header
            if (version != 1)
            {
                var signature = new byte[] { version }.Concat(reader.ReadBytes(3)).ToArray();
                H5Utils.ValidateSignature(signature, ObjectHeader2.Signature);
                version = reader.ReadByte();
            }

            return version switch
            {
                1 => new ObjectHeader1(reader, superblock, version),
                2 => new ObjectHeader2(reader, superblock, version),
                _ => throw new NotSupportedException($"The object header version '{version}' is not supported.")
            };
        }

        protected List<HeaderMessage> ReadHeaderMessages(BinaryReader reader,
                                                         Superblock superblock,
                                                         ulong objectHeaderSize,
                                                         byte version,
                                                         bool withCreationOrder = false)
        {
            var headerMessages = new List<HeaderMessage>();
            var continuationMessages = new List<ObjectHeaderContinuationMessage>();
            var remainingBytes = objectHeaderSize;

            ulong prefixSize;
            ulong gapSize;

            if (version == 1)
            {
                prefixSize = 8UL;
                gapSize = 0;
            }    
            else if (version == 2)
            {
                prefixSize = 4UL + (withCreationOrder ? 2UL : 0UL);
                gapSize = prefixSize;
            }
            else
            {
                throw new Exception("The object header version number must be in the range of 1..2.");
            }

            while (remainingBytes > gapSize)
            {
                var message = new HeaderMessage(reader, superblock, version, withCreationOrder);

                remainingBytes -= message.DataSize + prefixSize;

                if (message.Type == HeaderMessageType.ObjectHeaderContinuation)
                    continuationMessages.Add((ObjectHeaderContinuationMessage)message.Data);
                else
                    headerMessages.Add(message);
            }

            foreach (var continuationMessage in continuationMessages)
            {
                reader.BaseStream.Seek((long)continuationMessage.Offset, SeekOrigin.Begin);

                if (version == 1)
                {
                    var messages = this.ReadHeaderMessages(reader, superblock, continuationMessage.Length, version);
                    headerMessages.AddRange(messages);
                }
                else if (version == 2)
                {
                    var continuationBlock = new ObjectHeaderContinuationBlock2(reader, superblock, continuationMessage.Length, version, withCreationOrder);
                    var messages = continuationBlock.HeaderMessages;
                    headerMessages.AddRange(messages);
                }
            }

            this.ObjectType = this.DetermineObjectType(headerMessages);

            return headerMessages;
        }

        private H5ObjectType DetermineObjectType(List<HeaderMessage> headerMessages)
        {
            foreach (var message in headerMessages)
            {
                switch (message.Type)
                {
                    case HeaderMessageType.LinkInfo:
                    case HeaderMessageType.Link:
                    case HeaderMessageType.GroupInfo:
                    case HeaderMessageType.SymbolTable:
                        return H5ObjectType.Group;

                    case HeaderMessageType.DataLayout:
                        return H5ObjectType.Dataset;

                    default:
                        break;
                }
            }

            var condition = headerMessages.Count == 1 &&
                            headerMessages[0].Type == HeaderMessageType.DataType;

            if (condition)
                return H5ObjectType.CommitedDataType;
            else
                return H5ObjectType.Undefined;
        }

        #endregion
    }
}
