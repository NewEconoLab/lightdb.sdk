using System;
using System.Collections.Generic;
using System.Text;
using ThinNeo.VM;

namespace LightDB.SDK
{
    //签名用和NEO兼容的套路
    public class SignData
    {
        public byte[] addressScript;
        public byte[] signScript;//push signdata
        public string GetAddress()
        {
            return ThinNeo.Helper_NEO.GetAddress_FromScriptHash(ThinNeo.Helper_NEO.CalcHash160(addressScript));
        }
        Dictionary<string, byte[]> extData = new Dictionary<string, byte[]>();
        public static SignData Sign(byte[] prikey, byte[] srcdata)
        {
            var pubkey = ThinNeo.Helper_NEO.GetPublicKey_FromPrivateKey(prikey);
            SignData sdata = new SignData();
            sdata.addressScript = ThinNeo.Helper_NEO.GetAddressScript_FromPublicKey(pubkey);
            var signdata = ThinNeo.Helper_NEO.Sign(srcdata, prikey);
            sdata.signScript = MakePushScript(signdata);
            return sdata;
        }
        public static byte[] MakePushScript(byte[] data)
        {
            if (data == null)
                throw new ArgumentNullException();
            using (System.IO.MemoryStream writer = new System.IO.MemoryStream())
            {
                if (data.Length <= (int)OpCode.PUSHBYTES75)
                {
                    writer.WriteByte((byte)data.Length);
                    writer.Write(data, 0, data.Length);
                }
                else if (data.Length < 0x100)
                {
                    writer.WriteByte((byte)OpCode.PUSHDATA1);
                    writer.WriteByte((byte)data.Length);
                    writer.Write(data, 0, data.Length);
                }
                else if (data.Length < 0x10000)
                {
                    writer.WriteByte((byte)OpCode.PUSHDATA2);
                    writer.Write(BitConverter.GetBytes((ushort)data.Length), 0, 2);
                    writer.Write(data, 0, data.Length);
                }
                else// if (data.Length < 0x100000000L)
                {
                    writer.WriteByte((byte)OpCode.PUSHDATA4);
                    writer.Write(BitConverter.GetBytes((UInt32)data.Length), 0, 4);
                    writer.Write(data, 0, data.Length);
                }
                return writer.ToArray();
            }
        }


        public static ulong ReadVarInt(byte[] code, int addr, out int addrlen)
        {
            byte fb = code[addr];
            ulong value;
            if (fb == 0xFD)
            {
                value = BitConverter.ToUInt16(code, addr + 1);
                addrlen = 1 + 2;
            }
            else if (fb == 0xFE)
            {
                value = BitConverter.ToUInt32(code, addr + 1);
                addrlen = 1 + 4;
            }
            else if (fb == 0xFF)
            {
                value = BitConverter.ToUInt64(code, addr + 1);
                addrlen = 1 + 8;
            }
            else
            {
                value = fb;
                addrlen = 1;
            }
            return value;
        }
        static void SimSysCall(byte[] callname, Stack<byte[]> callstack, byte[] trandata)
        {
            var strcallname = System.Text.Encoding.UTF8.GetString(callname);
            throw new Exception("not write code here: call:" + strcallname);
            //byte[] pubkey = callstack.Pop();
            //byte[] signdata = callstack.Pop();
            //var b = ThinNeo.Helper_NEO.VerifySignature(trandata, signdata, pubkey);
            //callstack.Push(new byte[1] { (byte)(b ? 1 : 0) });
        }
        static void SimCheckSig(Stack<byte[]> callstack, byte[] trandata)
        {
            byte[] pubkey = callstack.Pop();
            byte[] signdata = callstack.Pop();
            var b = ThinNeo.Helper_NEO.VerifySignature(trandata, signdata, pubkey);
            callstack.Push(new byte[1] { (byte)(b ? 1 : 0) });
        }
        static int SimOpLite(byte[] code, UInt16 pos, Stack<byte[]> calcstack, byte[] trandata)
        {
            //read opcode
            var opcode = (OpCode)code[pos];
            int addr = pos + 1;
            if (opcode == OpCode.NOP)
            {
                return addr;
            }
            if (opcode >= OpCode.PUSHBYTES1 && opcode <= OpCode.PUSHBYTES75)
            {
                //o.paramType = ParamType.ByteArray;

                //read data;
                var count = (int)opcode;
                byte[] data = new byte[count];
                for (var i = 0; i < count; i++)
                {
                    data[i] = code[addr + i];
                }
                addr += count;

                calcstack.Push(data);
                return addr;
            }
            else if (opcode == OpCode.SYSCALL)
            {
                //read callword
                var callwordlen = ReadVarInt(code, addr, out int addradd);
                addr += addradd;
                byte[] data = new byte[addradd];
                for (var i = 0; i < addradd; i++)
                {
                    data[i] = code[addr + i];
                }
                addr += addradd;
                SimSysCall(data, calcstack, trandata);
                return addr;
            }
            else if (opcode == OpCode.CHECKSIG)
            {
                SimCheckSig(calcstack, trandata);
                return addr;
            }
            else if (opcode == OpCode.RET)
            {
                return -1;
            }
            return -1;
        }
        public bool VerifySign(string address, byte[] data)
        {
            if (address != this.GetAddress())
                return false;
            Stack<byte[]> ministack = new Stack<byte[]>();

            //push signdata
            for (UInt16 i = 0; i < this.signScript.Length;)
            {
                var ret = SimOpLite(this.signScript, i, ministack, data);
                if (ret < 0)
                    break;
                i = (UInt16)ret;
            }
            //push pubkey and check
            for (UInt16 i = 0; i < this.addressScript.Length;)
            {
                var ret = SimOpLite(this.addressScript, i, ministack, data);
                if (ret < 0)
                    break;
                i = (UInt16)ret;
            }
            var lastboolinstack = ministack.Pop()[0] > 0;
            return lastboolinstack;
        }
        public byte[] ToBytes()
        {
            using (var ms = new System.IO.MemoryStream())
            {
                this.Pack(ms);
                return ms.ToArray();
            }
        }
        public static SignData FromRaw(byte[] data)
        {
            using (System.IO.MemoryStream ms = new System.IO.MemoryStream(data))
            {
                return SignData.Unpack(ms);
            }
        }
        public void Pack(System.IO.Stream stream)
        {
            //emit addressScript
            byte[] buflenaddressScript = BitConverter.GetBytes((UInt16)addressScript.Length);
            stream.Write(buflenaddressScript, 0, 2);
            stream.Write(addressScript, 0, addressScript.Length);
            //emit signScript
            byte[] buflensignScript = BitConverter.GetBytes((UInt16)signScript.Length);
            stream.Write(signScript, 0, 2);
            stream.Write(signScript, 0, signScript.Length);

            //emit extdata
            stream.WriteByte((byte)this.extData.Count);
            foreach (var item in extData)
            {
                var keybuf = System.Text.Encoding.UTF8.GetBytes(item.Key);
                var data = item.Value;
                var datalenbuf = BitConverter.GetBytes((UInt32)data.Length);
                stream.WriteByte((byte)keybuf.Length);
                stream.Write(keybuf, 0, keybuf.Length);
                stream.Write(datalenbuf, 0, 4);
                stream.Write(data, 0, data.Length);
            }

        }
        public static SignData Unpack(System.IO.Stream stream)
        {
            SignData signData = new SignData();

            //read addressScript
            var buflenaddressScript = new byte[2];
            stream.Read(buflenaddressScript, 0, 2);
            UInt16 lenaddressScript = BitConverter.ToUInt16(buflenaddressScript, 0);
            signData.addressScript = new byte[lenaddressScript];
            stream.Read(signData.addressScript, 0, lenaddressScript);
            //read signScript
            var buflensignScript = new byte[2];
            stream.Read(buflensignScript, 0, 2);
            UInt16 lensignScript = BitConverter.ToUInt16(buflensignScript, 0);
            signData.signScript = new byte[lensignScript];
            stream.Read(signData.signScript, 0, lensignScript);


            var pcount = stream.ReadByte();
            for (var i = 0; i < pcount; i++)
            {
                var keylen = stream.ReadByte();
                var keybuf = new byte[keylen];
                stream.Read(keybuf, 0, keylen);
                var key = System.Text.Encoding.UTF8.GetString(keybuf);
                var datalenbuf = new byte[4];
                stream.Read(datalenbuf, 0, 4);
                var datalen = BitConverter.ToUInt32(datalenbuf, 0);
                var value = new byte[datalen];
                stream.Read(value, 0, (int)datalen);
                signData.extData[key] = value;
            }

            return signData;
        }
    }

}
