using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using System.Linq;
namespace LightDB.SDK
{
    public delegate Task OnClientRecv(NetMessage msg);
    public delegate Task OnDisConnect();

    public class WebsocketBase
    {
        public event OnClientRecv OnRecv;
        public event OnDisConnect OnDisconnect;


        System.Net.WebSockets.ClientWebSocket websocket;

        System.Collections.Concurrent.ConcurrentQueue<byte[]> wantsend
            = new System.Collections.Concurrent.ConcurrentQueue<byte[]>();

        ulong sendMsgID = 0;

        System.Collections.Concurrent.ConcurrentDictionary<UInt64, OnClientRecv> mapRecv
    = new System.Collections.Concurrent.ConcurrentDictionary<UInt64, OnClientRecv>();

        //public void AddOnceCallback(UInt64 id, OnClientRecv callback)
        //{
        //    this.mapRecv[id] = callback;
        //}
        public bool Connected
        {
            get;
            private set;
        }
        public async Task Connect(Uri uri)
        {
            this.websocket = new System.Net.WebSockets.ClientWebSocket();
            try
            {
                await websocket.ConnectAsync(uri, System.Threading.CancellationToken.None);
                //peer.OnConnect(websocket);
                Connected = true;
            }
            catch (Exception err)
            {
                Console.CursorLeft = 0;
                Console.WriteLine("error on connect." + err.Message);
            }
            //此时调用一个不等待的msgprocessr
            MessageProcesser();
            MessageSender();

            return;
        }
        public UInt64 Send(LightDB.SDK.NetMessage msg)
        {
            UInt64 _id = 0;
            lock (this)
            {
                _id = this.sendMsgID;
                this.sendMsgID++;
            }
            msg.Params["_id"] = BitConverter.GetBytes(_id);

            wantsend.Enqueue(msg.ToBytes());
            return _id;
        }

        public void SendWithOnceCallback(LightDB.SDK.NetMessage msg, OnClientRecv callback)
        {
            var _id = Send(msg);
            this.mapRecv[_id] = callback;
        }
        async void MessageSender()
        {
            try
            {

                while (websocket.State == System.Net.WebSockets.WebSocketState.Open)
                {
                    if (wantsend.TryDequeue(out byte[] data))
                    {
                        ArraySegment<byte> buffer = new ArraySegment<byte>(data);
                        await websocket.SendAsync(buffer, System.Net.WebSockets.WebSocketMessageType.Binary, true, System.Threading.CancellationToken.None);
                    }
                    else
                    {
                        await Task.Yield();
                    }
                }
            }
            catch (Exception err)
            {
                Console.WriteLine("somthing wrong.");
            }
            finally
            {
                this.Connected = false;
            }
        }
        async void MessageProcesser()
        {
            //recv
            try
            {
                using (System.IO.MemoryStream ms = new System.IO.MemoryStream(1024 * 1024))
                {
                    byte[] buf = new byte[1024];
                    ArraySegment<byte> buffer = new ArraySegment<byte>(buf);
                    while (websocket.State == System.Net.WebSockets.WebSocketState.Open)
                    {
                        var recv = await websocket.ReceiveAsync(buffer, System.Threading.CancellationToken.None);
                        //Task.WaitAll(_recv);
                        //var recv = _recv.Result;

                        ms.Write(buf, 0, recv.Count);
                        if (recv.EndOfMessage)
                        {
                            var count = ms.Position;
                            ms.Position = 0;
                            var msg = NetMessage.Unpack(ms);
                            var posend = ms.Position;
                            if (posend != count)
                                throw new Exception("error msg.");

                            //重置pos
                            ms.Position = 0;

                            var iddata = msg.Params["_id"];
                            ulong id = BitConverter.ToUInt64(iddata, 0);
                            if (mapRecv.TryRemove(id, out OnClientRecv onRecvOnce))
                            {
                                await onRecvOnce(msg);
                            }
                            else
                            {
                                await OnRecv(msg);// .onEvent(httpserver.WebsocketEventType.Recieve, websocket, bytes);
                            }
                        }
                        //Console.WriteLine("recv=" + recv.Count + " end=" + recv.EndOfMessage);
                    }
                }
            }
            catch (Exception err)
            {
                Console.CursorLeft = 0;

                Console.WriteLine("error on recv." + err.Message);
            }
            //disconnect
            try
            {
                await this?.OnDisconnect();
            }
            catch (Exception err)
            {
                Console.CursorLeft = 0;

                Console.WriteLine("error on disconnect." + err.Message);
            }
            finally
            {
                this.Connected = false;
            }
        }
    }

}
