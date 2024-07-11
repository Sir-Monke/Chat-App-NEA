using System;
using System.Collections.Generic;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
using System.IO;

public static class ProtocolConsts // Header Stuff
{
    public const byte TypePing = 0x01;
    public const byte TypeData = 0x02;
    public const byte TypeOther = 0x03;

    public const int HeaderSize = 5; // MessageType (1 byte) + PayloadLength (4 bytes)
}

class ServerTCPApp
{
    public static TcpListener Server = null;

    class ChatRoom // for group chats cant use yet
    {
        public readonly Int16 MaxClients = 10;
        private Int16 ClientCount = 0;

        public Int16 GetClientCount()
        {
            return this.ClientCount;
        }
    }

    class ServerTCPHandler
    {

        private static IPAddress LocalAddr = GetServerIPv4(); // Will try to use machine IPv4 Addr if cant will use any Ip Addr
        private static readonly Int32 Port = 80; // Port For Server
        private static bool ServerState = false; // Server.Start(); to toggle listening state 

        // Connected Clients Info Vars
        private static Int16 ConnectedClients = 0; // Connected Clients
        public static readonly Int16 MaxClients = 10; // Max Clients

        // Connected Clients Info Methods
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

        // Assign Server IP for connection
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
            return IPAddress.Any; // might just return 0.0.0.0 idk why
        }

        //Handle Client Connection status / Messages 
        public void HandleClientRequests()
        {
            while (ServerState)
            {
                try
                {
                    TcpClient client = Server.AcceptTcpClient();
                    IncConnectedClients();
                    HandleClient(client);
                }
                catch (SocketException e)
                {
                    Console.WriteLine($"SocketException: {e.Message}");
                }
                catch (Exception e)
                {
                    Console.WriteLine($"Exception: {e.Message}");
                }
            }
        }
        private void HandleClient(TcpClient client)
        {
            IPAddress clientIP = ((IPEndPoint)client.Client.RemoteEndPoint).Address;
            int clientPort = ((IPEndPoint)client.Client.RemoteEndPoint).Port;
            Console.WriteLine("Client Connected! Client Info: {0} {1}", clientIP, clientPort);

            try
            {
                NetworkStream stream = client.GetStream();
                //ProcessClientRequests(client, stream, clientIP, clientPort);
                HandleClientAsync(client, stream, clientIP, clientPort);
            }
            catch (IOException)
            {
                Console.WriteLine("{0}:{1} disconnected unexpectedly.",clientIP,clientPort);
            }
            catch (ObjectDisposedException)
            {
                Console.WriteLine("{0}:{1} disconnected unexpectedly.", clientIP, clientPort);
            }
            finally
            {
                DecConnectedClients();
                client.Close();
            }
        }
        private void ProcessClientRequests(TcpClient client, NetworkStream stream, IPAddress clientIP, int clientPort)
        {
            Byte[] bytes = new Byte[256];
            String data = null;

            int i; // byte count index
            while ((i = stream.Read(bytes, 0, bytes.Length)) != 0)
            {
                data = Encoding.ASCII.GetString(bytes, 0, i);
                Console.WriteLine("Message from {0}: {1}", clientIP, data);
            }

            Console.WriteLine("Client {0}:{1} disconnected.", clientIP, clientPort);
        }

        // TCP Server Configs
        public bool InitServer()
        {
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
                    Console.WriteLine("SocketException: {0}",e);
                    return false;
                }
                catch (Exception e)
                {
                    Console.WriteLine("Exception: {0}",e);
                    return false;
                }
            }
            return false;
        }
        private static void ToggleServer() // just toggles the state of the server - havent tested the turn of server thing but.....
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

        private async Task HandleClientAsync(TcpClient client, NetworkStream stream, IPAddress clientIP, int clientPort)
        {
            try
            {
                byte[] headerBuffer = new byte[ProtocolConsts.HeaderSize];
                int bytesRead = await stream.ReadAsync(headerBuffer, 0, headerBuffer.Length);

                if (bytesRead > 0)
                {
                    byte messageType = headerBuffer[0];
                    int payloadLength = BitConverter.ToInt32(headerBuffer, 1);

                    byte[] payload = new byte[payloadLength];
                    bytesRead = await stream.ReadAsync(payload, 0, payload.Length);

                    if (bytesRead > 0)
                    {
                        switch (messageType)
                        {
                            case ProtocolConsts.TypePing:
                                Console.WriteLine("Received Ping from client");

                                // Send a response (acknowledgment)
                                byte[] responseBuffer = new byte[] { ProtocolConsts.TypePing }; // Ping response
                                await stream.WriteAsync(responseBuffer, 0, responseBuffer.Length);
                                break;
                            case ProtocolConsts.TypeData:
                                Byte[] bytes = new Byte[256];
                                String data = null;

                                int i; // byte count index
                                while ((i = stream.Read(bytes, 0, bytes.Length)) != 0)
                                {
                                    data = Encoding.ASCII.GetString(bytes, 0, i);
                                    Console.WriteLine("Message from {0}: {1}", clientIP, data);
                                }
                                Console.WriteLine("Client {0}:{1} disconnected.", clientIP, clientPort);
                                break;
                            case ProtocolConsts.TypeOther:
                                // not sure what other stuff this could handle with but its here soooooo :)
                                break;
                            default:
                                Console.WriteLine("Unknown message type: {0}", messageType);
                                break;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error handling client: {ex.Message}");
            }

        }
    }

    private static void Main(string[] args)
    {
        ServerTCPHandler ServerHdl = new ServerTCPHandler();

        ServerHdl.InitServer();

        while (true) // shitty temp loop just to keep running
        {

        }
    }
}
