using System;
using System.IO;

namespace SNetwork
{
    public class MsgBase
    {
        public int MsgId { get; }

        public byte[] Body { get; }


        public int Length => Packet.PacketSizeLength + Packet.MetaLength + Packet.MsgLength + Body.Length;

        public MsgBase(int msgId, byte[] body)
        {
            MsgId = msgId;
            Body = body;
        }


        public byte[] GetBuffer()
        {
            var totalBytes = new byte[Length];

            var dataLenBytes = BitConverter.GetBytes(Length);
            var metaBytes = BitConverter.GetBytes(Packet.MetaLength);
            var cmdBytes = BitConverter.GetBytes(MsgId);


            dataLenBytes.CopyTo(totalBytes, 0);
            metaBytes.CopyTo(totalBytes, Packet.PacketSizeLength);
            cmdBytes.CopyTo(totalBytes, Packet.PacketSizeLength + Packet.MetaLength);
            Body.CopyTo(totalBytes, Packet.PacketSizeLength + Packet.MetaLength + Packet.MsgLength);

            return totalBytes;
        }

        public override string ToString()
        {
            return $"消息ID:{MsgId}--->消息体:{System.Text.Encoding.Default.GetString(Body)}";
        }

        public static MsgBase Decode(MemoryStream ms)
        {
            var streamLen = ms.Length;
            var msgBytes = new byte[Packet.MsgLength];
            var msgBody = new byte[streamLen - Packet.MetaLength - Packet.MsgLength];


            var buffer = ms.GetBuffer();
            Array.Copy(buffer, Packet.MetaLength, msgBytes, 0, Packet.MsgLength);
            var msgId = BitConverter.ToInt32(msgBytes, 0);

            Array.Copy(buffer, Packet.MetaLength + Packet.MsgLength, msgBody, 0, msgBody.Length);


            return new MsgBase(msgId, msgBody);
        }

        public static MsgBase CreateMsg(int msgId, byte[] content)
        {
            return new MsgBase(msgId, content);
        }
    }
}