using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace KnzkLiveCommentViewer
{
    class BouyomiConnection
    {
        string host;
        int port;

        public BouyomiConnection(string host = "127.0.0.1", int port = 50001)
        {
            this.host = host;
            this.port = port;
        }
        public void say(string text)
        {
            try
            {
                var client = new TcpClient(this.host, this.port);
                using (NetworkStream ns = client.GetStream())
                {
                    using (BinaryWriter bw = new BinaryWriter(ns))
                    {
                        bw.Write((Int16)0x0001);
                        bw.Write((Int16)(-1));
                        bw.Write((Int16)(-1));
                        bw.Write((Int16)(-1));
                        bw.Write((Int16)0);
                        bw.Write((byte)0);
                        var utf8Encoded = Encoding.UTF8.GetBytes(text);
                        bw.Write((Int32)utf8Encoded.Length);
                        bw.Write(utf8Encoded);
                        bw.Close();
                    }
                    ns.Close();
                }
                client.Close();
            } catch
            {
                Console.WriteLine("棒読みちゃんに接続できませんでした");
            }
        }
    }
}
