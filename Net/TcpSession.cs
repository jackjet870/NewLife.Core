﻿using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using NewLife.Threading;

namespace NewLife.Net
{
    /// <summary>增强TCP客户端</summary>
    public class TcpSession : SessionBase, ISocketSession, ITransport
    {
        #region 属性
        private TcpClient _Client;
        /// <summary>客户端</summary>
        public TcpClient Client { get { return _Client; } private set { _Client = value; } }

        /// <summary>获取Socket</summary>
        /// <returns></returns>
        internal override Socket GetSocket() { return Client == null ? null : Client.Client; }

        private Boolean _DisconnectWhenEmptyData = true;
        /// <summary>收到空数据时抛出异常并断开连接。</summary>
        public Boolean DisconnectWhenEmptyData { get { return _DisconnectWhenEmptyData; } set { _DisconnectWhenEmptyData = value; } }

        private Stream _Stream;
        /// <summary>会话数据流，供用户程序使用。可用于解决Tcp粘包的问题。</summary>
        public Stream Stream { get { return _Stream; } set { _Stream = value; } }

        ISocketServer _Server;
        /// <summary>Socket服务器。当前通讯所在的Socket服务器，其实是TcpServer/UdpServer。该属性决定本会话是客户端会话还是服务的会话</summary>
        ISocketServer ISocketSession.Server { get { return _Server; } }

        private Boolean _AutoReconnect = true;
        /// <summary>是否自动重连，默认true。发生异常断开连接时，自动重连服务端。</summary>
        public Boolean AutoReconnect { get { return _AutoReconnect; } set { _AutoReconnect = value; } }

        //private Boolean _MessageDgram;
        ///// <summary>使用消息报文。此时解决粘包拆包问题，每个报文前面使用7位压缩编码整数表示后面数据包长度</summary>
        //public Boolean MessageDgram { get { return _MessageDgram; } set { _MessageDgram = value; } }
        #endregion

        #region 构造
        /// <summary>实例化增强UDP</summary>
        public TcpSession()
        {
            Local = new NetUri(ProtocolType.Tcp, IPAddress.Any, 0);
            Remote = new NetUri(ProtocolType.Tcp, IPAddress.Any, 0);
        }

        /// <summary>使用监听口初始化</summary>
        /// <param name="listenPort"></param>
        public TcpSession(Int32 listenPort)
            : this()
        {
            Port = listenPort;
        }

        /// <summary>用TCP客户端初始化</summary>
        /// <param name="client"></param>
        public TcpSession(TcpClient client)
            : this()
        {
            if (client == null) return;

            Client = client;
            if (client.Connected) Stream = client.GetStream();
            var socket = client.Client;
            if (socket.LocalEndPoint != null) Local.EndPoint = (IPEndPoint)socket.LocalEndPoint;
            if (socket.RemoteEndPoint != null) Remote.EndPoint = (IPEndPoint)socket.RemoteEndPoint;
        }

        internal TcpSession(ISocketServer server, TcpClient client)
            : this(client)
        {
            Active = true;
            _Server = server;
        }
        #endregion

        #region 方法
        /// <summary>打开</summary>
        protected override Boolean OnOpen()
        {
            // 服务端会话没有打开
            if (_Server != null) return false;

            if (Client == null || !Client.Client.IsBound)
            {
                // 根据目标地址适配本地IPv4/IPv6
                if (Remote != null && !Remote.Address.IsAny())
                {
                    Local.Address = Local.Address.GetRightAny(Remote.Address.AddressFamily);
                }

                Client = new TcpClient(Local.EndPoint);
                CheckDynamic();

                WriteLog("{0}.Open {1}", Name, this);
            }

            // 打开端口前如果已设定远程地址，则自动连接
            if (Remote == null || Remote.EndPoint.IsAny()) return false;

            //if (Remote != null && !Remote.EndPoint.IsAny())
            {
                try
                {
                    Client.Connect(Remote.EndPoint);
                    Stream = Client.GetStream();
                }
                catch (Exception ex)
                {
                    if (!Disposed && !ex.IsDisposed()) OnError("Connect", ex);
                    if (ThrowException) throw;

                    return false;
                }
            }

            return true;
        }

        /// <summary>关闭</summary>
        protected override Boolean OnClose()
        {
            WriteLog("{0}.Close {1}", Name, this);

            if (Client != null)
            {
                // 提前关闭这个标识，否则Close时可能触发自动重连机制
                Active = false;
                try
                {
                    if (_Async != null && _Async.AsyncWaitHandle != null) _Async.AsyncWaitHandle.Close();

                    Client.Close();
                }
                catch (Exception ex)
                {
                    if (!ex.IsDisposed()) OnError("Close", ex);
                    if (ThrowException) throw;

                    return false;
                }
            }
            Client = null;
            Stream = null;

            return true;
        }

        /// <summary>发送数据</summary>
        /// <remarks>
        /// 目标地址由<seealso cref="SessionBase.Remote"/>决定，如需精细控制，可直接操作<seealso cref="Client"/>
        /// </remarks>
        /// <param name="buffer">缓冲区</param>
        /// <param name="offset">偏移</param>
        /// <param name="count">数量</param>
        /// <returns>是否成功</returns>
        public override Boolean Send(Byte[] buffer, Int32 offset = 0, Int32 count = -1)
        {
            if (!Open()) return false;

            if (count < 0) count = buffer.Length - offset;

            //var stream = Client.GetStream();
            try
            {
                // 修改发送缓冲区
                if (Client.SendBufferSize < count) Client.SendBufferSize = count;

                //// 特殊处理消息报文，增加字节长度
                //if (!MessageDgram)
                Stream.Write(buffer, offset, count);
                //else
                //{
                //    var ms = new MemoryStream();
                //    ms.WriteEncodedInt(count);

                //    WriteLog("发送报文{0}字节，开头附加长度占用{1}字节", count, ms.Position);

                //    ms.Write(buffer, offset, count);
                //    ms.Position = 0;

                //    stream.Write(ms);
                //}
            }
            catch (Exception ex)
            {
                if (!ex.IsDisposed())
                {
                    OnError("Send", ex);

                    // 发送异常可能是连接出了问题，需要关闭
                    Close();
                    Reconnect();

                    if (ThrowException) throw;
                }

                return false;
            }

            LastTime = DateTime.Now;

            return true;
        }

        /// <summary>接收数据</summary>
        /// <returns>收到的数据。如果没有数据返回0长度数组，如果出错返回null</returns>
        public override Byte[] Receive()
        {
            if (!Open()) return null;

            var size = 1024 * 2;

            // 报文模式调整缓冲区大小。还差这么多数据就足够一个报文
            //if (MessageDgram && PacketSize > 0) size = PacketSize - (Int32)Packet.Length;
            var ps = Stream as PacketStream;
            if (ps != null && ps.Size > 0) size = ps.Size;

            var buf = new Byte[size];

            var count = Receive(buf, 0, buf.Length);
            if (count < 0) return null;
            if (count == 0) return new Byte[0];

            LastTime = DateTime.Now;

            if (count == buf.Length) return buf;

            return buf.ReadBytes(0, count);
        }

        /// <summary>读取指定长度的数据，一般是一帧</summary>
        /// <param name="buffer">缓冲区</param>
        /// <param name="offset">偏移</param>
        /// <param name="count">数量</param>
        /// <returns></returns>
        public override Int32 Receive(Byte[] buffer, Int32 offset = 0, Int32 count = -1)
        {
            if (!Open()) return -1;

            if (count < 0) count = buffer.Length - offset;

            var rs = 0;
            try
            {
                if (count > 0) rs = Stream.Read(buffer, offset, count);
            }
            catch (Exception ex)
            {
                if (!ex.IsDisposed())
                {
                    OnError("Receive", ex);

                    // 发送异常可能是连接出了问题，需要关闭
                    Close();
                    Reconnect();

                    if (ThrowException) throw;
                }

                return -1;
            }

            LastTime = DateTime.Now;

            //if (MessageDgram && rs >= 0)
            //{
            //    WriteLog("收到数据{0}字节", rs);

            //    var data = buffer;
            //    if (offset > 0 && rs > 0) data = buffer.ReadBytes(offset, rs);
            //    // 传入这次收到的数据，返回一个完整报文
            //    data = CheckPacket(data, ref rs);

            //    // 把报文写回缓冲区
            //    if (rs > 0 && data != null && data != buffer)
            //    {
            //        // 如果报文大于缓冲区大小
            //        //if (size > count) size = count;
            //        if (rs > count) throw new XException("报文大小{0}大于缓冲区大小{1}", rs, count);

            //        buffer.Write(offset, data, 0, rs);
            //    }
            //}

            return rs;
        }
        #endregion

        #region 异步接收
        private IAsyncResult _Async;

        /// <summary>开始监听</summary>
        /// <returns>是否成功</returns>
        public override Boolean ReceiveAsync()
        {
            if (Disposed || !Open()) return false;

            if (_Async != null) return true;
            try
            {
                // 开始新的监听
                var buf = new Byte[Client.ReceiveBufferSize];
                _Async = Stream.BeginRead(buf, 0, buf.Length, OnReceive, buf);
            }
            catch (Exception ex)
            {
                if (!ex.IsDisposed())
                {
                    OnError("ReceiveAsync", ex);

                    // 异常一般是网络错误
                    Close();
                    Reconnect();

                    if (ThrowException) throw;
                }
                return false;
            }

            return true;
        }

        void OnReceive(IAsyncResult ar)
        {
            _Async = null;

            if (!Active) return;

            var client = Client;
            if (client == null || !client.Connected) return;

            // 接收数据
            var data = (Byte[])ar.AsyncState;
            // 数据长度，0表示收到空数据，-1表示收到部分包，后续跳过处理
            var count = 0;
            try
            {
                //var stream = client.GetStream();
                count = Stream.EndRead(ar);
            }
            catch (Exception ex)
            {
                if (!ex.IsDisposed())
                {
                    OnError("EndReceive", ex);

                    // 异常一般是网络错误
                    Close();
                    Reconnect();
                }
                return;
            }

            if (DisconnectWhenEmptyData && count == 0)
            {
                Close();
            }

            // 最后收到有效数据的时间
            LastTime = DateTime.Now;

            // 数据长度，0表示收到空数据，-1表示收到部分包，后续跳过处理
            if (count >= 0)
            {
                //if (!MessageDgram)
                // 在用户线程池里面处理数据
                ThreadPoolX.QueueUserWorkItem(() => OnReceive(data, count), ex => OnError("OnReceive", ex));
                //else
                //{
                //    try
                //    {
                //        //OnReceive(data, count);
                //        WriteLog("收到数据包{0}字节", count);
                //        // 注意，很有可能一次收到多个包的数据，此时需要多次触发接收
                //        while (true)
                //        {
                //            data = CheckPacket(data, ref count);
                //            if (count <= 0) break;

                //            OnReceive(data, count);

                //            // 如果没有剩余数据，则直接退出
                //            if (PacketSize <= 0) break;

                //            // 为下一次调用准备数据
                //            data = null;
                //            count = 0;
                //        }
                //    }
                //    catch (Exception ex)
                //    {
                //        OnError("OnReceive", ex);
                //        if (Disposed) return;
                //    }
                //}
            }

            // 开始新的监听
            ReceiveAsync();
        }

        /// <summary>处理收到的数据</summary>
        /// <param name="data"></param>
        /// <param name="count"></param>
        protected virtual void OnReceive(Byte[] data, Int32 count)
        {
            // 分析处理
            var e = new ReceivedEventArgs();
            e.Data = data;
            e.Length = count;

            RaiseReceive(this, e);

            // 数据发回去
            if (e.Feedback) Send(e.Data, 0, e.Length);
        }
        #endregion

        #region 粘包处理
        private MemoryStream _Packet;
        /// <summary>用于粘包处理的数据流缓冲区，指针位于末端，便于附加数据。前面是7位压缩编码整数表示的报文长度</summary>
        private MemoryStream Packet { get { return _Packet; } set { _Packet = value; } }

        private Int32 _PacketSize;
        /// <summary>所期望的报文大小</summary>
        private Int32 PacketSize { get { return _PacketSize; } set { _PacketSize = value; } }

        private Byte[] CheckPacket(Byte[] data, ref Int32 count)
        {
            var ms = Packet;

            // 检查上一次接收数据是否有剩余
            if (ms == null || ms.Length == 0)
            {
                // 空包，并且上次没有剩余
                if (count <= 0) return data;

                // 先转为数据流
                ms = new MemoryStream(data, 0, count);

                var len = ms.ReadEncodedInt();
                // 如果长度足够整包，可以返回，剩余部分留下
                if (len <= ms.Length - ms.Position)
                {
                    WriteLog("得到报文{0}字节", len);
                    count = len;
                    data = ReadPacket(ms, len);
                }
                else
                {
                    WriteLog("数据包大小{0}字节不能满足报文大小{1}字节", count, len);
                    count = -1;

                    PacketSize = len;
                    Packet = new MemoryStream();
                    Packet.Write(ms);
                }
            }
            else
            {
                // 如果上一次有剩余，则附加data到后面
                if (data != null && data.Length > 0 && count > 0)
                {
                    WriteLog("附加数据包{0}字节到上一次剩余数据包{1}字节", count, PacketSize);
                    ms.Write(data, 0, count);
                }

                var len = PacketSize;
                // 如果长度足够整包，可以返回，剩余部分留下
                if (len <= ms.Length)
                {
                    WriteLog("凑够报文{0}字节", len);
                    ms.Position = 0;
                    count = len;
                    data = ReadPacket(ms, len);
                }
                else
                {
                    WriteLog("仍然无法满足报文大小{0}字节", len);
                    count = -1;
                }
            }

            return data;
        }

        Byte[] ReadPacket(Stream ms, Int32 len)
        {
            var data = ms.ReadBytes(len);

            // 剩余部分先读取长度，然后数据放到Packet里面
            if (ms.Position < ms.Length)
            {
                PacketSize = ms.ReadEncodedInt();
                Packet = new MemoryStream();
                Packet.Write(ms);
            }
            else
            {
                PacketSize = 0;
                Packet = null;
            }

            return data;
        }
        #endregion

        #region 自动重连
        void Reconnect()
        {
            if (!AutoReconnect || Disposed) return;

            WriteLog("{0}.Reconnect {1}", Name, this);

            Open();
        }
        #endregion

        #region 辅助
        /// <summary>已重载。</summary>
        /// <returns></returns>
        public override string ToString()
        {
            if (Remote != null && !Remote.EndPoint.IsAny())
                return String.Format("{0}=>{1}", Local, Remote.EndPoint);
            else
                return Local.ToString();
        }
        #endregion
    }
}