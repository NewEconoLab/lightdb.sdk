using System;
using System.Collections.Generic;
using System.Text;

namespace LightDB.SDK
{
    public static class protocol_Helper
    {

        public static NetMessage PostMsg(this WebsocketBase socket, LightDB.SDK.NetMessage msg)
        {
            NetMessage __msg = null;
            socket.SendWithOnceCallback(msg, async (msgback) =>
            {
                __msg = msgback;
            });
            while (socket.Connected && __msg == null)
            {
                System.Threading.Thread.Sleep(1);
            }
            return __msg;
        }
        public static byte[] ToBytes_UTF8Encode(this string str)
        {
            return System.Text.Encoding.UTF8.GetBytes(str);
        }
        public static string ToString_UTF8Decode(this byte[] data)
        {
            return System.Text.Encoding.UTF8.GetString(data);
        }
    }
    public static class protocol_Ping
    {

        public static NetMessage CreateSendMsg()
        {
            var msg = LightDB.SDK.NetMessage.Create("_ping");
            return msg;
        }

        public static bool PraseRecvMsg(NetMessage msg)
        {
            if (msg.Params.ContainsKey("_error"))
            {
                throw new Exception("error:" + msg.Params["_error"].ToString_UTF8Decode());
            }
            if (msg.Cmd == "_ping.back")
                return true;
            else
                throw new Exception("error message.");
        }

        public static int Post_Ping(this WebsocketBase socket)
        {
            DateTime t0 = DateTime.Now;

            var msg = protocol_Ping.CreateSendMsg();
            var msgrecv = socket.PostMsg(msg);
            var s = protocol_Ping.PraseRecvMsg(msgrecv);

            DateTime t1 = DateTime.Now;
            return (int)((t1 - t0).TotalMilliseconds);
        }
    }

    public static class protocol_GetDBState
    {
        public static NetMessage CreateSendMsg()
        {
            var msg = LightDB.SDK.NetMessage.Create("_db.state");
            return msg;
        }
        public class message
        {
            public bool dbopen;
            public UInt64 height;
            public List<string> writer = new List<string>();
        }
        public static message PraseRecvMsg(NetMessage msg)
        {
            if (msg.Params.ContainsKey("_error"))
            {
                throw new Exception("error:" + msg.Params["_error"].ToString_UTF8Decode());
            }
            if (msg.Cmd == "_db.state.back")
            {
                message data = new message();
                if (msg.Params["dbopen"][0] == 1)
                    data.dbopen = true;
                if (data.dbopen)
                {
                    data.height = BitConverter.ToUInt64(msg.Params["height"], 0);
                }
                foreach (var key in msg.Params.Keys)
                {
                    if (key.IndexOf("writer") == 0)
                    {
                        data.writer.Add(msg.Params[key].ToString_UTF8Decode());
                    }
                }
                return data;
            }
            else
                throw new Exception("error message.");
        }
        public static message Post_getdbstate(this WebsocketBase socket)
        {
            var msg = CreateSendMsg();
            var msgrecv = socket.PostMsg(msg);
            var s = PraseRecvMsg(msgrecv);
            return s;
        }
    }

    /// <summary>
    /// 获取快照协议
    /// </summary>
    public static class protocol_UseSnapshot
    {
        public static NetMessage CreateSendMsg(UInt64? height = null)
        {
            var msg = LightDB.SDK.NetMessage.Create("_db.usesnapshot");
            if (height != null)
            {
                msg.Params["snapheight"] = BitConverter.GetBytes(height.Value);
            }
            return msg;
        }
        public class message
        {
            public UInt64 snapheight;
        }
        public static message PraseRecvMsg(NetMessage msg)
        {
            if (msg.Params.ContainsKey("_error"))
            {
                throw new Exception("error:" + msg.Params["_error"].ToString_UTF8Decode());
            }
            if (msg.Cmd == "_db.usesnapshot.back")
            {
                message data = new message();
                data.snapheight = BitConverter.ToUInt64(msg.Params["snapheight"], 0);
                return data;
            }
            else
                throw new Exception("error message.");
        }
        public static message Post_usesnapshot(this WebsocketBase socket, UInt64? height = null)
        {
            var msg = CreateSendMsg(height);
            var msgrecv = socket.PostMsg(msg);
            var s = PraseRecvMsg(msgrecv);
            return s;
        }
    }
    public static class protocol_UnuseSnapshot
    {
        public static NetMessage CreateSendMsg(UInt64 height)
        {
            var msg = LightDB.SDK.NetMessage.Create("_db.unusesnapshot");
            msg.Params["snapheight"] = BitConverter.GetBytes(height);
            return msg;
        }
        public class message
        {
            public bool remove;
        }
        public static message PraseRecvMsg(NetMessage msg)
        {
            if (msg.Params.ContainsKey("_error"))
            {
                throw new Exception("error:" + msg.Params["_error"].ToString_UTF8Decode());
            }
            if (msg.Cmd == "_db.unusesnapshot.back")
            {
                message data = new message();
                data.remove = msg.Params["remove"][0] > 0;
                return data;
            }
            else
                throw new Exception("error message.");
        }
        public static message Post_unusesnapshot(this WebsocketBase socket, UInt64 height)
        {
            var msg = CreateSendMsg(height);
            var msgrecv = socket.PostMsg(msg);
            var s = PraseRecvMsg(msgrecv);
            return s;
        }
    }
    public static class protocol_SnapshotDataHeight
    {
        public static NetMessage CreateSendMsg(UInt64 height)
        {
            var msg = LightDB.SDK.NetMessage.Create("_db.snapshot.dataheight");
            msg.Params["snapheight"] = BitConverter.GetBytes(height);
            return msg;
        }
        public class message
        {
            public UInt64 dataheight;
        }
        public static message PraseRecvMsg(NetMessage msg)
        {
            if (msg.Params.ContainsKey("_error"))
            {
                throw new Exception("error:" + msg.Params["_error"].ToString_UTF8Decode());
            }
            if (msg.Cmd == "_db.snapshot.dataheight.back")
            {
                message data = new message();
                data.dataheight = BitConverter.ToUInt64(msg.Params["dataheight"], 0);
                return data;
            }
            else
                throw new Exception("error message.");
        }
        public static message Post_snapshot_dataheight(this WebsocketBase socket, UInt64 height)
        {
            var msg = CreateSendMsg(height);
            var msgrecv = socket.PostMsg(msg);
            var s = PraseRecvMsg(msgrecv);
            return s;
        }
    }
    public static class protocol_SnapshotGetValue
    {
        public static NetMessage CreateSendMsg(UInt64 snapid, byte[] tableid, byte[] key)
        {
            var msg = LightDB.SDK.NetMessage.Create("_db.snapshot.getvalue");
            msg.Params["snapheight"] = BitConverter.GetBytes(snapid);
            msg.Params["tableid"] = tableid;
            msg.Params["key"] = key;
            return msg;
        }
        public class message
        {
            public byte[] data;
        }
        public static message PraseRecvMsg(NetMessage msg)
        {
            if (msg.Params.ContainsKey("_error"))
            {
                throw new Exception("error:" + msg.Params["_error"].ToString_UTF8Decode());
            }
            if (msg.Cmd == "_db.snapshot.getvalue.back")
            {
                message data = new message();
                data.data = msg.Params["data"];
                return data;
            }
            else
                throw new Exception("error message.");
        }
        public static message Post_snapshot_getvalue(this WebsocketBase socket, UInt64 snapid, byte[] tableid, byte[] key)
        {
            var msg = CreateSendMsg(snapid, tableid, key);
            var msgrecv = socket.PostMsg(msg);
            var s = PraseRecvMsg(msgrecv);
            return s;
        }
    }
    public static class protocol_SnapshotGetBlock
    {
        public static NetMessage CreateSendMsg(UInt64 snapid, UInt64 blockid)
        {
            var msg = LightDB.SDK.NetMessage.Create("_db.snapshot.getblock");
            msg.Params["snapheight"] = BitConverter.GetBytes(snapid);
            msg.Params["blockid"] = BitConverter.GetBytes(blockid);
            return msg;
        }
        public class message
        {
            public byte[] data;
        }
        public static message PraseRecvMsg(NetMessage msg)
        {
            if (msg.Params.ContainsKey("_error"))
            {
                throw new Exception("error:" + msg.Params["_error"].ToString_UTF8Decode());
            }
            if (msg.Cmd == "_db.snapshot.getblock.back")
            {
                message data = new message();
                data.data = msg.Params["data"];
                return data;
            }
            else
                throw new Exception("error message.");
        }
        public static message Post_snapshot_getblock(this WebsocketBase socket, UInt64 snapid, UInt64 blockid)
        {
            var msg = CreateSendMsg(snapid, blockid);
            var msgrecv = socket.PostMsg(msg);
            var s = PraseRecvMsg(msgrecv);
            return s;
        }
    }
    public static class protocol_SnapshotGetBlockHash
    {
        public static NetMessage CreateSendMsg(UInt64 snapid, UInt64 blockid)
        {
            var msg = LightDB.SDK.NetMessage.Create("_db.snapshot.getblockhash");
            msg.Params["snapheight"] = BitConverter.GetBytes(snapid);
            msg.Params["blockid"] = BitConverter.GetBytes(blockid);
            return msg;
        }
        public class message
        {
            public byte[] data;
        }
        public static message PraseRecvMsg(NetMessage msg)
        {
            if (msg.Params.ContainsKey("_error"))
            {
                throw new Exception("error:" + msg.Params["_error"].ToString_UTF8Decode());
            }
            if (msg.Cmd == "_db.snapshot.getblockhash.back")
            {
                message data = new message();
                data.data = msg.Params["data"];
                return data;
            }
            else
                throw new Exception("error message.");
        }
        public static message Post_snapshot_getblockhash(this WebsocketBase socket, UInt64 snapid, UInt64 blockid)
        {
            var msg = CreateSendMsg(snapid, blockid);
            var msgrecv = socket.PostMsg(msg);
            var s = PraseRecvMsg(msgrecv);
            return s;
        }
    }
    public static class protocol_SnapshotGetWriter
    {
        public static NetMessage CreateSendMsg(UInt64 snapid)
        {
            var msg = LightDB.SDK.NetMessage.Create("_db.snapshot.getwriter");
            msg.Params["snapheight"] = BitConverter.GetBytes(snapid);
            return msg;
        }
        public class message
        {
            public List<string> writer;
        }
        public static message PraseRecvMsg(NetMessage msg)
        {
            if (msg.Params.ContainsKey("_error"))
            {
                throw new Exception("error:" + msg.Params["_error"].ToString_UTF8Decode());
            }
            if (msg.Cmd == "_db.snapshot.getwriter.back")
            {
                message data = new message();
                data.writer = new List<string>();
                foreach (var key in msg.Params.Keys)
                {
                    if (key.IndexOf("writer") == 0)
                    {
                        data.writer.Add(msg.Params[key].ToString_UTF8Decode());
                    }
                }
                return data;
            }
            else
                throw new Exception("error message.");
        }
        public static message Post_snapshot_getwriter(this WebsocketBase socket, UInt64 snapid)
        {
            var msg = CreateSendMsg(snapid);
            var msgrecv = socket.PostMsg(msg);
            var s = PraseRecvMsg(msgrecv);
            return s;
        }
    }
    /// <summary>
    /// 写入协议
    /// </summary>
    public static class protocol_Write
    {
        public static NetMessage CreateSendMsg(byte[] tasksrcdata, SignData signdata)
        {
            var msg = LightDB.SDK.NetMessage.Create("_db.write");
            msg.Params["taskdata"] = tasksrcdata;
            msg.Params["signdata"] = signdata.ToBytes();
            return msg;
        }
        public class message
        {
            public UInt64 blockid;
            public byte[] blockhash;
        }
        public static message PraseRecvMsg(NetMessage msg)
        {
            if (msg.Params.ContainsKey("_error"))
            {
                throw new Exception("error:" + msg.Params["_error"].ToString_UTF8Decode());
            }
            if (msg.Cmd == "_db.write.back")
            {
                message data = new message();

                data.blockid = BitConverter.ToUInt64(msg.Params["blockid"], 0);
                data.blockhash = msg.Params["blockhash"];
                return data;
            }
            else
                throw new Exception("error message.");
        }
        public static message Post_write(this WebsocketBase socket, byte[] tasksrcdata, SignData signdata)
        {
            var msg = CreateSendMsg(tasksrcdata, signdata);
            var msgrecv = socket.PostMsg(msg);
            var s = PraseRecvMsg(msgrecv);
            return s;
        }
    }
}
