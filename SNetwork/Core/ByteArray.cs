using System;

namespace SNetwork
{
    public class ByteArray
    {
        private const int DEFAULT_SIZE = 1024;

        public byte[] bytes;
        public int readIdx;
        public int writeIdx;
        public int Length => writeIdx - readIdx;
        public int Remain => capacity - writeIdx;

        private int initSize;
        private int capacity;

        public ByteArray(int size = DEFAULT_SIZE)
        {
            bytes = new byte[size];
            initSize = size;
            capacity = size;
            readIdx = 0;
            writeIdx = 0;
        }

        public ByteArray(byte[] defaultBytes)
        {
            bytes = defaultBytes;
            capacity = defaultBytes.Length;
            initSize = defaultBytes.Length;
            readIdx = 0;
            writeIdx = defaultBytes.Length;
        }

        public void Resize(int size)
        {
            if (size < Length || size < initSize) return;
            var n = 1;
            while (n < size)
            {
                n *= 2;
            }

            capacity = n;
            var newBytes = new byte[capacity];
            Array.Copy(bytes, readIdx, newBytes, 0, Length);
            bytes = newBytes;
            writeIdx = Length;
            readIdx = 0;
        }

        public int Write(byte[] bs, int offset, int count)
        {
            if (Remain < count)
            {
                Resize(Length + count);
            }

            Array.Copy(bs, offset, bytes, writeIdx, count);
            writeIdx += count;
            return count;
        }

        public int Read(byte[] bs, int offset, int count)
        {
            count = Math.Min(count, Length);
            Array.Copy(bytes, readIdx, bs, offset, count);
            readIdx += count;
            CheckAndMoveBytes();
            return count;
        }

        public void CheckAndMoveBytes()
        {
            if (Length < 8)
            {
                MoveBytes();
            }
        }

        public void MoveBytes()
        {
            Array.Copy(bytes, readIdx, bytes, 0, Length);
            writeIdx = Length;
            readIdx = 0;
        }

        public override string ToString()
        {
            return BitConverter.ToString(bytes, readIdx, Length);
        }

        public string Debug()
        {
            return $"readIdx{readIdx} writeIndex{writeIdx} bytes {BitConverter.ToString(bytes, 0, bytes.Length)}";
        }
    }
}