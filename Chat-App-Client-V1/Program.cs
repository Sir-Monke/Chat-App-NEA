//Server App
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;

// ERROR [1]:
// ERROR [2]:
// ERROR [3]:

class ServerTCPApp {
    public static TcpListener Server = null;


    class ChatRoom()
    {
        public readonly Int16 MaxClients = 10;
        private Int16 ClientCount = 0;

        public Int16 GetClientCount()
        {
            return this.ClientCount;
        }

    }

    class ServerTCPHandler {

        private static IPAddress LocalAddr = GetServerIPv4(); // Will try to use machine IPv4 Addr if cant will use any Ip Addr
        private static readonly Int32 Port = 80; // Port For Server
        private static bool ServerState = false; // Server.Start(); to toggle listening state 

        private static Int16 ConnectedClients = 0; // Connected Clients
        public static readonly Int16 MaxClients = 10; // Max Clients

        public void IncConnectedClients()
        {
            if (ConnectedClients < MaxClients)
            {
                ConnectedClients++;
            }
        }

        public void DecConnectedClients()
        {
            if (ConnectedClients > 0)
            {
                ConnectedClients--;
            }
        }

        private static void ToggleServer()
        {
            ServerState = !ServerState;
            if (ServerState == true)
            {
                Server.Start();
                Console.WriteLine("Server started, TCP listening on {0}:{1}", LocalAddr, Port);
            }
            else if (ServerState == false) 
            {
                Server.Stop();
                Console.WriteLine("Server stopped on: {0}:{1}", LocalAddr, Port);
            }
            return;
        }

        private static IPAddress GetServerIPv4() // just gets local IPv4 ADDR if cant obtain will return random IP
        {
            foreach (NetworkInterface Ni in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (Ni.OperationalStatus == OperationalStatus.Up)
                {
                    foreach (UnicastIPAddressInformation ip in Ni.GetIPProperties().UnicastAddresses)
                    {
                        if (ip.Address.AddressFamily == AddressFamily.InterNetwork && !IPAddress.IsLoopback(ip.Address))
                        {
                            return ip.Address;
                        }
                    }
                }
            }
            return IPAddress.Any; 
        }

        public void HandleClientRequests() // should add a max count for times a users can connect every min
        {
            Byte[] bytes = new Byte[256]; // max message length
            String data = null; // processed data sent from client

            while (ServerState)
            {
                TcpClient client = Server.AcceptTcpClient();
                NetworkStream stream = client.GetStream();

                if (stream != null) 
                {
                    IPAddress clientIP = ((IPEndPoint)client.Client.RemoteEndPoint).Address;
                    int clientPort = ((IPEndPoint)client.Client.RemoteEndPoint).Port;

                    Console.WriteLine("Client Connected! Client Info: {0} {1}", clientIP, clientPort);

                    int i; // byte count index

                    try
                    {
                        while ((i = stream.Read(bytes, 0, bytes.Length)) != 0)
                        {
                            data = System.Text.Encoding.ASCII.GetString(bytes, 0, i);
                            Console.WriteLine(data);
                        }
                    }
                    catch (IOException e)
                    {
                        Console.WriteLine($"IOException: {e.Message}");
                    }
                    finally
                    {
                        client.Close();
                    }
                }
            }
        }

        public bool InitServer() {
            if (Server == null)
            {
                Server = new TcpListener(LocalAddr, Port);
                try
                {
                    ToggleServer();
                    HandleClientRequests();
                    return true;
                }
                catch (SocketException e)
                {
                    Console.WriteLine($"SocketException: {e}");
                    return false;
                }
                catch (Exception e)
                {
                    Console.WriteLine($"Exception: {e}");
                    return false;
                }
            }
            return false;
        }
    }

    private static void Main(string[] args)
    {
        ServerTCPHandler ServerHdl = new ServerTCPHandler();

        ServerHdl.InitServer();
        
        while (true)
        {

        }
    }
}