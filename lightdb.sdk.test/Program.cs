using LightDB.SDK;
using LightDB;
using System;
using System.IO;
using System.Threading.Tasks;

namespace LightDB.testclient
{
    class Program
    {

        static LightDB.SDK.WebsocketBase client = new LightDB.SDK.WebsocketBase();
        static System.Collections.Generic.Dictionary<string, Action<string[]>> menuItem = new System.Collections.Generic.Dictionary<string, Action<string[]>>();
        static System.Collections.Generic.Dictionary<string, string> menuDesc = new System.Collections.Generic.Dictionary<string, string>();
        static void AddMenu(string cmd, string desc, Action<string[]> onMenu)
        {
            menuItem[cmd.ToLower()] = onMenu;
            menuDesc[cmd.ToLower()] = desc;
        }
        static void InitMenu()
        {
            AddMenu("exit", "exit application", (words) => { Environment.Exit(0); });
            AddMenu("help", "show help", ShowMenu);
            AddMenu("ping", "ping server.", Ping);
            AddMenu("db.state", "show db state", DBState);
            AddMenu("db.usesnap", "open a db snap.", DBUseSnap);
            AddMenu("db.unusesnap", "close a db snap.", DBUnuseSnap);
            AddMenu("db.snapheight", "check snapheight.", DBsnapheight);
            AddMenu("db.block", "show cur dbblock ,use db.block [n].", DBGetBlock);
            AddMenu("db.blockhash", "show cur dbblock ,use db.blockhash [n].", DBGetBlockHash);
            AddMenu("db.getwriter", "get all writers.", DBGetWriter);
            AddMenu("newtable", "newtable", NewTable);
            AddMenu("writetable", "writetable", WriteTable);
        }
        static void ShowMenu(string[] words = null)
        {
            Console.WriteLine("==Menu==");
            foreach (var key in menuItem.Keys)
            {
                var line = "  " + key + " - ";
                if (menuDesc.ContainsKey(key))
                    line += menuDesc[key];
                Console.WriteLine(line);
            }
        }
        static void MenuLoop()
        {
            while (true)
            {
                try
                {
                    Console.Write("-->");
                    var line = Console.ReadLine();
                    var words = line.Split(" ", StringSplitOptions.RemoveEmptyEntries);
                    if (words.Length > 0)
                    {
                        var cmd = words[0].ToLower();
                        if (cmd == "?")
                        {
                            ShowMenu();
                        }
                        else if (menuItem.ContainsKey(cmd))
                        {
                            menuItem[cmd](words);
                        }
                    }
                }
                catch (Exception err)
                {
                    Console.WriteLine("err:" + err.Message);
                }
            }
        }
        static void Main(string[] args)
        {
            TaskScheduler.UnobservedTaskException += (s, e) =>
              {
                  Console.WriteLine("error on ============>" + e.ToString());
              };
            InitMenu();
            StartClient();
            MenuLoop();
            //Loops();
        }
        static void Ping(string[] words)
        {
            var pingms = client.Post_Ping();
            Console.WriteLine("ping=" + pingms);

        }
        static void DBState(string[] words)
        {
            var msg = client.Post_getdbstate();
            Console.WriteLine("msg. open=" + msg.dbopen);
            Console.WriteLine("height=" + msg.height);
            foreach (var w in msg.writer)
            {
                Console.WriteLine("writer=" + w);
            }
        }

        static void DBGetBlock(string[] words)
        {
            UInt64 blockid = UInt64.Parse(words[1]);
            var msg = client.Post_snapshot_getblock(lastSnapheight.Value, blockid);
            //var msg = client.Post_snapshot_getvalue(lastSnapheight.Value, protocol_Helper.systemtable_block, BitConverter.GetBytes(blockid));
            var v = LightDB.DBValue.FromRaw(msg.data);
            var task = LightDB.WriteTask.FromRaw(v.value);
            Console.WriteLine("got info=" + msg.ToString());
            foreach (var i in task.items)
            {
                Console.WriteLine("item=" + i.ToString());
            }
            if (task.extData != null)
            {
                foreach (var e in task.extData)
                {
                    Console.WriteLine("extdata=" + e.Key + " len=" + e.Value.Length);
                }
            }
        }
        static byte[] lasthash;
        static void DBGetBlockHash(string[] words)
        {
            UInt64 blockid = UInt64.Parse(words[1]);
            var msg = client.Post_snapshot_getblockhash(lastSnapheight.Value, blockid);
            //var msg = client.Post_snapshot_getvalue(lastSnapheight.Value, protocol_Helper.systemtable_block, BitConverter.GetBytes(blockid));
            var v = LightDB.DBValue.FromRaw(msg.data);
            lasthash = v.value;
            Console.WriteLine("got hash=" + v.value.ToString_Hex());
        }
        static void DBGetWriter(string[] words)
        {
            var msg = client.Post_snapshot_getwriter(lastSnapheight.Value);
            Console.WriteLine("got writer count=" + msg.writer.Count);
            foreach (var item in msg.writer)
            {
                Console.WriteLine("writer=" + item);
            }

        }
        static UInt64? lastSnapheight;
        static void DBUseSnap(string[] words)
        {
            var msg = client.Post_usesnapshot();
            Console.WriteLine("snapshot got height=" + msg.snapheight);
            lastSnapheight = msg.snapheight;
        }
        static void DBUnuseSnap(string[] words)
        {
            var msg = client.Post_unusesnapshot(lastSnapheight.Value);
            Console.WriteLine("snapshot free=" + msg.remove);
            lastSnapheight = null;
        }
        static void DBsnapheight(string[] words)
        {
            var msg = client.Post_snapshot_dataheight(lastSnapheight.Value);
            Console.WriteLine("snapshot height=" + lastSnapheight + "," + msg.dataheight);
        }
        static void NewTable(string[] words)
        {
            if (lasthash == null)
            {
                Console.WriteLine("get block hash first.");
                return;
            }
            LightDB.WriteTask write = new LightDB.WriteTask();

            //必须添加上一个块的hash，要不然服务器不会接受的
            write.extData = new System.Collections.Generic.Dictionary<string, byte[]>();
            write.extData["lasthash"] = lasthash;

            //createtable 123;

            write.CreateTable(new TableInfo(new byte[] { 0x01, 0x02, 0x03 }, "hello world", "", DBValue.Type.String));

            write.Put(new byte[] { 0x01, 0x02, 0x03 }, "123".ToBytes_UTF8Encode(), DBValue.FromValue(DBValue.Type.String, "balabala"));

            var srcdata = write.ToBytes();


            var wiftest = "L2CmHCqgeNHL1i9XFhTLzUXsdr5LGjag4d56YY98FqEi4j5d83Mv";//对应地址 AdsNmzKPPG7HfmQpacZ4ixbv9XJHJs2ACz 作为服务器配置的writer
            var prikey = ThinNeo.Helper_NEO.GetPrivateKeyFromWIF(wiftest);
            var pubkey = ThinNeo.Helper_NEO.GetPublicKey_FromPrivateKey(prikey);
            var address = ThinNeo.Helper_NEO.GetAddress_FromPublicKey(pubkey);

            SignData signdata = SignData.Sign(prikey, srcdata);

            var b = signdata.VerifySign(address, srcdata);
            Console.WriteLine("sign result=" + b);
            var msg = client.Post_write(srcdata, signdata);
            Console.WriteLine("post write result block[" + msg.blockid + "] = " + msg.blockhash.ToString_Hex());

        }
        static void WriteTable(string[] words)
        {
            if(lasthash==null)
            {
                Console.WriteLine("get block hash first.");
                return;
            }
            LightDB.WriteTask write = new LightDB.WriteTask();

            //必须添加上一个块的hash，要不然服务器不会接受的
            write.extData = new System.Collections.Generic.Dictionary<string, byte[]>();
            write.extData["lasthash"] = lasthash;

            //createtable 123;

            //write.CreateTable(new TableInfo(new byte[] { 0x01, 0x02, 0x03 }, "hello world", "", DBValue.Type.String));

            write.Put(new byte[] { 0x01, 0x02, 0x03 }, "123".ToBytes_UTF8Encode(), DBValue.FromValue(DBValue.Type.String, "balabala"));

            var srcdata = write.ToBytes();


            var wiftest = "L2CmHCqgeNHL1i9XFhTLzUXsdr5LGjag4d56YY98FqEi4j5d83Mv";//对应地址 AdsNmzKPPG7HfmQpacZ4ixbv9XJHJs2ACz 作为服务器配置的writer
            var prikey = ThinNeo.Helper_NEO.GetPrivateKeyFromWIF(wiftest);
            var pubkey = ThinNeo.Helper_NEO.GetPublicKey_FromPrivateKey(prikey);
            var address = ThinNeo.Helper_NEO.GetAddress_FromPublicKey(pubkey);

            SignData signdata = SignData.Sign(prikey, srcdata);

            var b = signdata.VerifySign(address, srcdata);
            Console.WriteLine("sign result=" + b);
            var msg = client.Post_write(srcdata, signdata);
            Console.WriteLine("post write result block[" + msg.blockid + "] = " + msg.blockhash.ToString_Hex());

        }

        static async void StartClient()
        {
            client.OnDisconnect += async () =>
            {
                Console.WriteLine("OnDisConnect.");
            };
            //client.OnRecv_Unknown += async (msg) =>
            //  {
            //      Console.WriteLine("got unknown msg:" + msg.Cmd);
            //  };
            await client.Connect(new Uri("ws://127.0.0.1:80/ws"));
            Console.WriteLine("connected.");

            try
            {
                for (var i = 0; i < 100; i++)
                {
                    var pingms = client.Post_Ping();
                    Console.WriteLine("ping=" + pingms);

                }
            }
            catch (Exception err)
            {
                Console.WriteLine("error on ping:" + err.Message);
            }
        }


    }


}
