﻿using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace VM.OS.Network
{
    using System;
    using System.Buffers.Text;
    using System.Net;
    using System.Threading.Tasks;
    using System.Windows;

    public class NetworkConfiguration
    {
        private TcpClient client;
        private NetworkStream stream;

        const int DEFAULT_PORT = 8080;
        public static string LAST_KNOWN_SERVER_IP => "192.168.0.138";

        public static IPAddress SERVER_IP = IPAddress.Parse(LAST_KNOWN_SERVER_IP);

        public Action<byte[]>? OnMessageRecieved;

        public Thread receiveThread;

        public NetworkConfiguration(Computer computer)
        {
            if (computer?.OS?.Config?.Value<bool>("ALWAYS_CONNECT") is bool connect && connect)
            {
                if (computer?.OS?.Config?.Value<string>("DEFAULT_SERVER_IP") is string _IP && IPAddress.Parse(_IP) is IPAddress ip)
                {
                    StartClient(ip);
                }
                else
                {
                    StartClient(SERVER_IP);
                }
            }
        }
        public void StartClient(IPAddress ip)
        {
            try
            {
                var ip_str = ip.ToString();
                client = new TcpClient(ip_str, DEFAULT_PORT);
                stream = client.GetStream();
                receiveThread = new Thread(ReceiveMessages);
                receiveThread.Start();
            }
            catch (Exception e)
            {
                Console.WriteLine("Error connecting to the server: " + e.Message);
            }
        }

        public void ReceiveMessages()
        {
            try
            {
                // MUSTARD
                byte[] buffer = new byte[1024];
                int bytesRead;

                while ((bytesRead = stream.Read(buffer, 0, buffer.Length)) > 0)
                {
                    OnMessageRecieved?.Invoke(buffer);
                }
            }
            catch (Exception e)
            {
            }
            finally
            {
                // server disconnect
                stream.Close();
                client.Close();
            }
        }


        internal void OnSendMessage(byte[] obj)
        {
            MessageBox.Show($"Sent message on channel {obj[0]}");
            stream.Write(obj);
        }

        internal void TryHaltCurrentConnection()
        {
            client?.Dispose();
            stream?.Dispose();
            Task.Run(()=>receiveThread?.Join());
        }
    }
}
