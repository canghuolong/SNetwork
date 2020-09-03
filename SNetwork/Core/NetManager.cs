#define ENABLE_LOG
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net.Sockets;

namespace SNetwork
{
    public static class NetManager
    {
        public static Action<string> LogEvent;

        private static Socket _socket;

        //读取缓冲区
        private static ByteArray _readBuffer;

        private static MemoryStream _memoryStream;
        private static PacketParser _parser;

        //写入队列
        private static Queue<ByteArray> _writeQueue;

        //正在连接
        private static bool _isConnecting = false;

        //正在关闭
        private static bool _isClosing = false;

        //消息列表
        private static Queue<MsgBase> _msgQueue = new Queue<MsgBase>();
        private static int _msgCount;
        private static int MAX_MSG = 10;


        #region 消息分发

        public enum NetEvent : int
        {
            ConnectSuc = 1,
            ConnectFailed = 2,
            Close = 3,
        }

        public delegate void NetEventListener(string message);

        private static Dictionary<NetEvent, NetEventListener> _eventListeners =
            new Dictionary<NetEvent, NetEventListener>();

        public static void AddNetEventListener(NetEvent netEvent, NetEventListener listener)
        {
            if (_eventListeners.ContainsKey(netEvent))
            {
                _eventListeners[netEvent] += listener;
                return;
            }

            _eventListeners[netEvent] = listener;
        }

        public static void RemoveNetEventListener(NetEvent netEvent, NetEventListener listener)
        {
            if (!_eventListeners.ContainsKey(netEvent) || listener == null) return;
            _eventListeners[netEvent] -= listener;
        }

        private static void FireEvent(NetEvent netEvent, string message)
        {
            if (_eventListeners.ContainsKey(netEvent))
            {
                _eventListeners[netEvent](message);
            }
        }

        public delegate void MsgListener(MsgBase msgBase);

        private static Dictionary<int, MsgListener> _msgListeners = new Dictionary<int, MsgListener>();

        public static void AddMsgListener(int msgId, MsgListener listener)
        {
            if (_msgListeners.ContainsKey(msgId))
            {
                _msgListeners[msgId] += listener;
                return;
            }

            _msgListeners[msgId] = listener;
        }

        public static void RemoveMsgListener(int msgId, MsgListener listener)
        {
            if (!_msgListeners.ContainsKey(msgId) || listener == null) return;


            _msgListeners[msgId] -= listener;
        }

        private static void FireMsg(int msgId, MsgBase msgBase)
        {
            if (_msgListeners.ContainsKey(msgId))
            {
                _msgListeners[msgId](msgBase);
            }
        }

        #endregion

        private static void Init()
        {
            _socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

            _readBuffer = new ByteArray();
            _writeQueue = new Queue<ByteArray>();
            _isConnecting = false;
            _isClosing = false;

            _msgQueue = new Queue<MsgBase>();
            _msgCount = 0;
            _memoryStream = new MemoryStream();
            _parser = new PacketParser(_readBuffer, _memoryStream);
        }


        public static void Connect(string ip, int port)
        {
            if (_socket != null && _socket.Connected)
            {
                Log("连接失败,已经处于连接状态");
                return;
            }

            if (_isConnecting)
            {
                Log("连接失败,正在连接...");
                return;
            }

            Init();
            _isConnecting = true;
            _socket.BeginConnect(ip, port, OnConnect, _socket);
        }

        private static void OnConnect(IAsyncResult result)
        {
            try
            {
                var socket = (Socket) result.AsyncState;
                _socket.EndConnect(result);
                Log("连接成功");
                FireEvent(NetEvent.ConnectSuc, string.Empty);
                _isConnecting = false;
                socket.BeginReceive(_readBuffer.bytes, _readBuffer.writeIdx, _readBuffer.Remain, SocketFlags.None,
                    OnReceive, socket);
            }
            catch (SocketException e)
            {
                Log($"连接失败:{e}");
                FireEvent(NetEvent.ConnectFailed, e.ToString());
                _isConnecting = false;
            }
        }


        private static void OnReceive(IAsyncResult result)
        {
            try
            {
                var socket = (Socket) result.AsyncState;
                var count = socket.EndReceive(result);
                _readBuffer.writeIdx += count;

                OnReceiveData();

                if (_readBuffer.Remain < 8)
                {
                    _readBuffer.MoveBytes();
                    _readBuffer.Resize(_readBuffer.Length * 2);
                }

                socket.BeginReceive(_readBuffer.bytes, _readBuffer.writeIdx, _readBuffer.Remain, SocketFlags.None,
                    OnReceive, socket);
            }
            catch (SocketException e)
            {
                Log($"接收消息错误 {e}");
            }
        }

        private static void OnReceiveData()
        {
            while (true)
            {
                try
                {
                    if (!_parser.Parse())
                    {
                        break;
                    }
                }
                catch (Exception e)
                {
                    Log($"ip:{e}");
                    return;
                }

                try
                {
                    AddMsg(_parser.GetPacket());
                }
                catch (Exception e)
                {
                    Log($"get packet {e}");
                }
            }
        }


        public static void Send(byte[] content)
        {
            if (_socket == null || !_socket.Connected)
            {
                return;
            }

            if (_isConnecting)
            {
                return;
            }

            if (_isClosing)
            {
                return;
            }

            _socket.BeginSend(content, 0, content.Length, SocketFlags.None, OnSendCallback, _socket);
        }

        public static void Send(MsgBase msgBase)
        {
            if (_socket == null || !_socket.Connected)
            {
                return;
            }

            if (_isConnecting)
            {
                return;
            }

            if (_isClosing)
            {
                return;
            }

            var ba = new ByteArray(msgBase.GetBuffer());
            var count = 0;
            lock (_writeQueue)
            {
                _writeQueue.Enqueue(ba);
                count = _writeQueue.Count;
            }

            if (count == 1)
            {
                var sendBuffer = msgBase.GetBuffer();
                _socket.BeginSend(sendBuffer, 0, sendBuffer.Length, 0, OnSendCallback, _socket);
            }
        }

        private static void OnSendCallback(IAsyncResult result)
        {
            try
            {
                var socket = (Socket) result.AsyncState;

                if (socket == null || !socket.Connected)
                {
                    return;
                }

                var count = socket.EndSend(result);
                ByteArray ba;
                lock (_writeQueue)
                {
                    ba = _writeQueue.Peek();
                }

                ba.readIdx += count;
                if (ba.Length == 0)
                {
                    lock (_writeQueue)
                    {
                        _writeQueue.Dequeue();
                        ba = _writeQueue.Peek();
                    }
                }

                if (ba != null)
                {
                    socket.BeginSend(ba.bytes, ba.readIdx, ba.Length, SocketFlags.None, OnSendCallback, socket);
                }
                else if (_isClosing)
                {
                    socket.Close();
                }

                Log($"[客户端] Send成功 {count}");
            }
            catch (SocketException e)
            {
                Log($"[客户端] Send失败 {e}");
            }
        }

        private static void AddMsg(MemoryStream ms)
        {
            var msg = MsgBase.Decode(ms);
            lock (_msgQueue)
            {
                _msgQueue.Enqueue(msg);
                _msgCount++;
            }
        }

        public static void MsgUpdate()
        {
            if (_msgCount == 0) return;
            for (var i = 0; i < MAX_MSG; i++)
            {
                MsgBase msg = null;
                lock (_msgQueue)
                {
                    if (_msgQueue.Count > 0)
                    {
                        msg = _msgQueue.Dequeue();
                        _msgCount--;
                    }
                }

                if (msg != null)
                {
                    FireMsg(msg.MsgId, msg);
                }
                else
                {
                    break;
                }
            }
        }


        [Conditional("ENABLE_LOG")]
        private static void Log(string log)
        {
            LogEvent($"[NetManager] {log}");
        }
    }
}