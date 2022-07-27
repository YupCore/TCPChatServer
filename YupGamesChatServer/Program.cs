using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using Newtonsoft.Json;

namespace YupGamesChatServer
{
    public static class ClientsList
    {
        public static List<Client> clients = new List<Client>();
    }

    struct HandshakeStruct
    {
        public long clientUID;
        public float gameVersion;
        public string passwordHash;
        public string roomName;
    };

    public class Client
    {
        HandshakeStruct handshakeStruct;
        bool handshakeSuccsesful = false;
        TcpClient theClient;
        DateTime connectionDate;
        public string GetMD5FromString(string toGet)
        {
            using(var md5 = MD5.Create())
            {
                var hashB = md5.ComputeHash(Encoding.UTF8.GetBytes(toGet));
                return BitConverter.ToString(hashB);
            }
        }
        public void WriteNewMessage(string message)
        {
            if(string.IsNullOrEmpty(message) || string.IsNullOrWhiteSpace(message))
            {
                return;
            }
            byte[] msg = Encoding.UTF8.GetBytes(message);
            List<byte> vs = new List<byte>();
            for (int i = 0; i < 1024; i++)
            {
                vs.Add(255);
            }

            for (int i = 0; i < msg.Length; i++)
            {
                vs[i] = msg[i];
            }

            theClient.GetStream().Write(vs.ToArray(), 0, 1024);
            WriteLog("writing a message " + message);
        }
        void WriteLog(string msg)
        {
            var formatted = "[" + DateTime.Now.ToString() + "]:" + msg + "\r\n";
            var path = ("./logs/" + theClient.Client.RemoteEndPoint.ToString() + " : " + connectionDate.ToString()).Replace(':', '-') + ".log";
            Console.Write(formatted);
            if(!File.Exists(path))
            {
                using (var fs = File.Create(path))
                {
                    fs.Write(Encoding.UTF8.GetBytes(formatted));
                }
            }
            else
            {
                File.AppendAllText(path, formatted);
            }
        }
        public Client(TcpClient Client)
        {
            try
            {
                connectionDate = DateTime.Now;
                theClient = Client;
                WriteLog("new connection from " + ((IPEndPoint)Client.Client.RemoteEndPoint).Address.ToString());

                WriteLog("receiving");
                ClientsList.clients.Add(this);

                string content_str2 = "";
                try
                {
                    while(!Client.GetStream().DataAvailable) { }
                    byte[] buffer2 = new byte[1024];

                    Client.GetStream().Read(buffer2,0,1024);
                    List<byte> trueBytes2 = new List<byte>();
                    foreach (var b in buffer2)
                    {
                        if (b != 255)
                        {
                            trueBytes2.Add(b);
                        }
                    }
                    content_str2 = Encoding.UTF8.GetString(trueBytes2.ToArray());
                }
                catch
                {
                    if (Client.Connected)
                    {
                        Client.Close();
                    }
                }
                if (content_str2.Contains("HANDSHAKE>"))
                {
                    var hnds = JsonConvert.DeserializeObject<HandshakeStruct>(content_str2.Split('>')[1]);
                    float gameVer;
                    using (var wc = new WebClient())
                    {
                        gameVer = float.Parse(wc.DownloadString("https://yourdomain/gamever.txt"));
                    }
                    if (hnds.gameVersion == gameVer)
                    {
                        if (hnds.passwordHash == GetMD5FromString("ENTER PASSWORD HERE"))
                        {
                            handshakeSuccsesful = true;
                            byte[] resp = Encoding.UTF8.GetBytes("HANDSHAKE SUCCESSFUL");
                            List<byte> vs = new List<byte>();
                            for (int i = 0; i < 1024; i++)
                            {
                                vs.Add(255);
                            }

                            for (int i = 0; i < resp.Length; i++)
                            {
                                vs[i] = resp[i];
                            }

                            Client.GetStream().Write(vs.ToArray(), 0, 1024);
                            handshakeStruct = hnds;
                            if (!ClientsList.clients.Contains(this))
                            {
                                ClientsList.clients.Add(this);
                            }
                        }
                        else
                        {
                            handshakeSuccsesful = false;
                            byte[] resp = Encoding.UTF8.GetBytes("HANDSHAKE FAILED : WRONG HASH");
                            List<byte> vs = new List<byte>();
                            for (int i = 0; i < 1024; i++)
                            {
                                vs.Add(255);
                            }

                            for (int i = 0; i < resp.Length; i++)
                            {
                                vs[i] = resp[i];
                            }

                            Client.GetStream().Write(vs.ToArray(), 0, 1024);
                            Client.Close();
                            ClientsList.clients.Remove(this);
                            return;
                        }
                    }
                    else
                    {
                        handshakeSuccsesful = false;
                        byte[] resp = Encoding.UTF8.GetBytes("HANDSHAKE FAILED : WRONG VERSION");
                        List<byte> vs = new List<byte>();
                        for (int i = 0; i < 1024; i++)
                        {
                            vs.Add(255);
                        }

                        for (int i = 0; i < resp.Length; i++)
                        {
                            vs[i] = resp[i];
                        }

                        Client.GetStream().Write(vs.ToArray(), 0, 1024);
                        Client.Close();
                        ClientsList.clients.Remove(this);
                        return;
                    }
                }
                else
                {
                    handshakeSuccsesful = false;
                    byte[] resp = Encoding.UTF8.GetBytes("HANDSHAKE FAILED : NO HANDSHAKE PROVIDED");
                    List<byte> vs = new List<byte>();
                    for (int i = 0; i < 1024; i++)
                    {
                        vs.Add(255);
                    }

                    for (int i = 0; i < resp.Length; i++)
                    {
                        vs[i] = resp[i];
                    }
                    WriteLog("Disconnecting");
                    Client.GetStream().Write(vs.ToArray(), 0, 1024);
                    Client.Close();
                    ClientsList.clients.Remove(this);
                    return;
                }



                while (Client.Connected)
                {
                    string content_str = "";
                    try
                    {
                        byte[] buffer = new byte[1024];

                        Client.GetStream().Read(buffer);
                        List<byte> trueBytes = new List<byte>();
                        foreach (var b in buffer)
                        {
                            if (b != 255)
                            {
                                trueBytes.Add(b);
                            }
                        }
                        content_str = Encoding.UTF8.GetString(trueBytes.ToArray());
                    }
                    catch
                    {
                        if (Client.Connected)
                        {
                            Client.Close();
                        }
                        break;
                    }

                    try
                    {
                        if (!content_str.Contains("STPPOINT"))
                        {
                            if (handshakeSuccsesful)
                            {
                                foreach (var cl in ClientsList.clients)
                                {
                                    if (cl.handshakeStruct.roomName == this.handshakeStruct.roomName)
                                    {
                                        cl.WriteNewMessage(content_str);
                                    }
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        byte[] exByteString = Encoding.UTF8.GetBytes(ex.ToString());
                        List<byte> vs = new List<byte>();
                        for (int i = 0; i < 1024; i++)
                        {
                            vs.Add(255);
                        }

                        for (int i = 0; i < exByteString.Length; i++)
                        {
                            vs[i] = exByteString[i];
                        }

                        Client.GetStream().Write(vs.ToArray(), 0, 1024);
                        continue;
                    }
                }
                WriteLog("Disconnecting");
                ClientsList.clients.Remove(this);
            }
            catch(Exception ex)
            {
                Console.WriteLine("Unexpected behauvor: " + ex.Message + " \r\n Diconnecting");
                if(this.theClient.Connected)
                {
                    theClient.Close();
                }
                ClientsList.clients.Remove(this);
            }
        }
        ~Client()
        {
            ClientsList.clients.Remove(this);
        }
    }

    class Server
    {
        TcpListener Listener; 

        // Запуск сервера
        public Server(int Port)
        {
            Listener = new TcpListener(IPAddress.Any, Port); 
            Listener.Start(); 
            
            while (true)
            {
                try
                {
                  
                    ThreadPool.QueueUserWorkItem(new WaitCallback(ClientThread), Listener.AcceptTcpClient());
                }
                catch
                {
                    continue;
                }
            }
        }

        static void ClientThread(Object StateInfo)
        {
            new Client((TcpClient)StateInfo);
        }

        ~Server()
        {
            if (Listener != null)
            {
                Listener.Stop();
            }
        }

        internal class Program
        {
            static void Main(string[] args)
            {
                Directory.CreateDirectory("./logs");
                int MaxThreadsCount = Environment.ProcessorCount * 4;
                ThreadPool.SetMaxThreads(MaxThreadsCount, MaxThreadsCount);
                ThreadPool.SetMinThreads(2, 2);
                new Server(1420);
            }
        }
    }
}
