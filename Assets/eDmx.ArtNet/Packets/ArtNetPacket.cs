using ArtNet.Enums;
using System.IO;
using ArtNet.IO;
using System.Collections.Generic;

namespace ArtNet.Packets
{
    public class ArtNetPacket
    {
        private static Dictionary<int, byte[]> _BufferPool;
        public static Dictionary<int, byte[]> BufferPool
        {
            get
            {
                if (_BufferPool == null)
                {
                    _BufferPool = new Dictionary<int, byte[]>();
                }
                return _BufferPool;
            }
        }

        public static byte[] Buffer(int size)
        {
            if (!BufferPool.ContainsKey(size))
            {
                BufferPool[size] = new byte[size];
            }
            return BufferPool[size];
        }

        public virtual bool KnownSize()
        {
            return false;
        }

        public ArtNetPacket(ArtNetOpCodes opCode)
        {
            OpCode = opCode;
        }

        public ArtNetPacket(ArtNetRecieveData data)
        {
            ArtNetBinaryReader packetReader = new ArtNetBinaryReader(new MemoryStream(data.buffer));
            ReadData(packetReader);
        }

        public byte[] ToArray()
        {
            MemoryStream stream = KnownSize() ? new MemoryStream(Buffer(Size())) : new MemoryStream();
            //MemoryStream stream = new MemoryStream();
            WriteData(new ArtNetBinaryWriter(stream));
            if (KnownSize())
            {
                return Buffer(Size());
                //return stream.ToArray();
            }
            else
            {
                return stream.ToArray();
            }
        }

        #region Packet Properties

        private string protocol = "Art-Net";

        public string Protocol
        {
            get { return protocol; }
            protected set
            {
                if (value.Length > 8)
                    protocol = value.Substring(0, 8);
                else
                    protocol = value;
            }
        }


        private short version = 14;

        public short Version
        {
            get { return version; }
            protected set { version = value; }
        }

        private ArtNetOpCodes opCode = ArtNetOpCodes.None;

        public virtual ArtNetOpCodes OpCode
        {
            get { return opCode; }
            protected set { opCode = value; }
        }

        #endregion

        public virtual void ReadData(ArtNetBinaryReader data)
        {
            Protocol = data.ReadNetworkString(8);
            OpCode = (ArtNetOpCodes)data.ReadNetwork16();

            //For some reason the poll packet header does not include the version.
            if (OpCode != ArtNetOpCodes.PollReply)
                Version = data.ReadNetwork16();

        }

        public virtual int Size()
        {
            return 8 // Protocol string
                 + 2 // OpCode short
                 + (OpCode != ArtNetOpCodes.PollReply ? 2 : 0); // Version short (except for polls)
        }

        public virtual void WriteData(ArtNetBinaryWriter data)
        {
            data.WriteNetwork(Protocol, 8);
            data.WriteNetwork((short)OpCode);

            //For some reason the poll packet header does not include the version.
            if (OpCode != ArtNetOpCodes.PollReply)
                data.WriteNetwork(Version);

        }

        public static ArtNetPacket Create(ArtNetRecieveData data)
        {
            switch ((ArtNetOpCodes)data.OpCode)
            {
                case ArtNetOpCodes.Poll:
                    return new ArtPollPacket(data);
                case ArtNetOpCodes.PollReply:
                    return new ArtPollReplyPacket(data);
                case ArtNetOpCodes.Dmx:
                    return new ArtNetDmxPacket(data);
                case ArtNetOpCodes.TodRequest:
                    return new ArtTodRequestPacket(data);
                case ArtNetOpCodes.TodData:
                    return new ArtTodDataPacket(data);
                case ArtNetOpCodes.TodControl:
                    return new ArtTodControlPacket(data);
            }

            return null;

        }
    }
}
