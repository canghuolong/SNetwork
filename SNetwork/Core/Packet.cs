using System;
using System.IO;

namespace SNetwork
{
    public enum ParserState
    {
        PacketSize,
        PacketBody,
    }

    public static class Packet
    {
        public const int PacketSizeLength = 4;
        public const int MinPacketSize = 2;
        public const int MetaLength = 4;
        public const int MsgLength = 4;
    }

    public class PacketParser
    {
        private readonly ByteArray _buffer;
        private readonly MemoryStream _memoryStream;
        private int _packetSize;
        private ParserState _state;
        private bool _isOK;


        public PacketParser(ByteArray buffer, MemoryStream memoryStream)
        {
            _buffer = buffer;
            _memoryStream = memoryStream;
            _state = ParserState.PacketSize;
        }

        public bool Parse()
        {
            if (_isOK)
            {
                return true;
            }

            var finish = false;
            while (!finish)
            {
                switch (_state)
                {
                    case ParserState.PacketSize:
                        if (_buffer.Length < Packet.PacketSizeLength)
                        {
                            finish = true;
                        }
                        else
                        {
                            _memoryStream.SetLength(Packet.PacketSizeLength);
                            _buffer.Read(_memoryStream.GetBuffer(), 0, Packet.PacketSizeLength);
                            
                            // if (BitConverter.IsLittleEndian)
                            // {
                            //     Array.Reverse(_memoryStream.GetBuffer());
                            // }
                            

                            
                            _packetSize = BitConverter.ToInt32(_memoryStream.GetBuffer(), 0);
                            if (_packetSize > ushort.MaxValue * 16 || _packetSize < Packet.MinPacketSize)
                            {
                                throw new Exception($"recv packet size error,{_packetSize}");
                            }

                            _state = ParserState.PacketBody;
                        }

                        break;
                    case ParserState.PacketBody:
                        var bodyLen = _packetSize-Packet.PacketSizeLength;
                        if (_buffer.Length < bodyLen)
                        {
                            finish = true;
                        }
                        else
                        {
                            _memoryStream.Seek(0, SeekOrigin.Begin);
                            _memoryStream.SetLength(bodyLen);
                            var bytes = _memoryStream.GetBuffer();
                            _buffer.Read(bytes, 0, bodyLen);
                            _isOK = true;
                            _state = ParserState.PacketSize;
                            finish = true;
                        }

                        break;
                }
            }

            return _isOK;
        }


        public MemoryStream GetPacket()
        {
            _isOK = false;
            return _memoryStream;
        }
    }
}