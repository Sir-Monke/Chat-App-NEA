using System;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

public static class ProtocolConsts
{
    public const byte TypePing = 0x01;
    public const byte TypeData = 0x02;
    public const byte TypeOther = 0x03;

    public const int HeaderSize = 5; // MessageType (1 byte) + PayloadLength (4 bytes)
}

class ServerTCPApp
{
    public static TcpListener Server;

    class ServerTCPHandler
    {
        private static readonly IPAddress LocalAddr = GetServerIPv4();
        private static readonly int Port = 80;
        private static bool ServerState = false;

        private static IPAddress GetServerIPv4()
        {
            foreach (var ni in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (ni.OperationalStatus == OperationalStatus.Up)
                {
                    foreach (var ip in ni.GetIPProperties().UnicastAddresses)
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

        public bool InitServer()
        {
            if (Server == null)
            {
                Server = new TcpListener(LocalAddr, Port);
                try
                {
                    ToggleServer();
                    Task.Run(() => HandleClientRequests()); // Run client handling in a separate task
                    return true;
                }
                catch (Exception e)
                {
                    Console.WriteLine("Initialization error: {0}", e.Message);
                    return false;
                }
            }
            return false;
        }

        private static void ToggleServer()
        {
            ServerState = !ServerState;
            if (ServerState)
            {
                Server.Start();
                Console.WriteLine("Server started, TCP listening on {0}:{1}", LocalAddr, Port);
            }
            else
            {
                Server.Stop();
                Console.WriteLine("Server stopped on {0}:{1}", LocalAddr, Port);
            }
        }

        private async Task HandleClientRequests()
        {
            while (ServerState)
            {
                try
                {
                    var client = await Server.AcceptTcpClientAsync();
                    Console.WriteLine("Client connected.");
                    _ = Task.Run(() => HandleClientAsync(client));
                }
                catch (Exception e)
                {
                    Console.WriteLine("Accept error: {0}", e.Message);
                }
            }
        }

        private async Task HandleClientAsync(TcpClient client)
        {
            IPAddress clientIP = ((IPEndPoint)client.Client.RemoteEndPoint).Address;
            int clientPort = ((IPEndPoint)client.Client.RemoteEndPoint).Port;
            Console.WriteLine($"Handling client {clientIP}:{clientPort}");

            try
            {
                using (var stream = client.GetStream())
                {
                    while (client.Connected)
                    {
                        // Read header
                        byte[] headerBuffer = new byte[ProtocolConsts.HeaderSize];
                        int headerBytesRead = 0;
                        while (headerBytesRead < headerBuffer.Length)
                        {
                            int bytesRead = await stream.ReadAsync(headerBuffer, headerBytesRead, headerBuffer.Length - headerBytesRead);
                            if (bytesRead == 0) break; // End of stream
                            headerBytesRead += bytesRead;
                        }

                        if (headerBytesRead < ProtocolConsts.HeaderSize)
                        {
                            Console.WriteLine("Error: Incomplete header read.\n Client lost: {0}:{1}", clientIP, clientPort);
                            break;
                        }

                        byte messageType = headerBuffer[0];
                        int payloadLength = BitConverter.ToInt32(headerBuffer, 1);

                        // Read payload if needed
                        byte[] payload = new byte[payloadLength];
                        int payloadBytesRead = 0;
                        if (payloadLength > 0)
                        {
                            while (payloadBytesRead < payload.Length)
                            {
                                int bytesRead = await stream.ReadAsync(payload, payloadBytesRead, payload.Length - payloadBytesRead);
                                if (bytesRead == 0) break; // End of stream
                                payloadBytesRead += bytesRead;
                            }

                            if (payloadBytesRead < payloadLength)
                            {
                                Console.WriteLine("Error: Incomplete payload read.");
                                break;
                            }
                        }

                        // Process message
                        switch (messageType)
                        {
                            case ProtocolConsts.TypePing:
                                Console.WriteLine("Received Ping from client");

                                byte[] responseBuffer = new byte[ProtocolConsts.HeaderSize];
                                responseBuffer[0] = ProtocolConsts.TypePing;
                                BitConverter.GetBytes(0).CopyTo(responseBuffer, 1);

                                await stream.WriteAsync(responseBuffer, 0, responseBuffer.Length);
                                Console.WriteLine("Ping response sent.");
                                break;
                            case ProtocolConsts.TypeData:
                                string data = Encoding.ASCII.GetString(payload);
                                Console.WriteLine($"Message from {clientIP}:{clientPort}: {data}");
                                break;
                            case ProtocolConsts.TypeOther:
                                // Handle other message types if needed
                                break;
                            default:
                                Console.WriteLine($"Unknown message type: {messageType}");
                                break;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error handling client: {ex.Message}");
            }
            finally
            {
                client.Close();
                Console.WriteLine($"Client {clientIP}:{clientPort} disconnected.");
            }
        }
    }

    private static void Main(string[] args)
    {
        var serverHandler = new ServerTCPHandler();
        serverHandler.InitServer();

        Console.WriteLine("Press Enter to exit...");
        Console.ReadLine();
    }
}
