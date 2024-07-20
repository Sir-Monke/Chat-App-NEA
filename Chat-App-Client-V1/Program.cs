// sorry laptop this program will prob cause a shit ton of network traffic / junk
using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

public static class ProtocolConsts
{
    public const byte TypePing = 0x1;
    public const byte TypeData = 0x2;
    public const byte TypeOther = 0x3;

    public const int HeaderSize = 5; // MessageType (1 byte) + PayloadLength (4 bytes)
}

class ClientTCPApp
{
    public class ClientData
    {
        private TcpClient client;
        private NetworkStream Nstream;
        private string serverIP;
        private int serverPort;
        private bool CanSendMessage = true;
        private const int MaxMessageLength = 100;
        private const int DefaultMessageCoolDown = 5000;
        private string LastMessage;

        public bool Connected { get; private set; }
        public bool Disconnected { get; private set; }

        // Client Connection State / Init Conncection
        public void Init()
        {
            while (!Connected)
            {
                Console.WriteLine("Enter Server IP To Connect: ");
                string tempIp = Console.ReadLine();
                Console.WriteLine("Enter Server Port number To Connect: ");
                if (int.TryParse(Console.ReadLine(), out int tempPort))
                {
                    if (ValidateInputText(ref tempIp) && ValidateIPAddr(tempIp) && ValidatePort(tempPort))
                    {
                        serverIP = tempIp;
                        serverPort = tempPort;
                        Connect();
                    }
                    else
                    {
                        Console.WriteLine("Invalid IP address or port number. Please try again.");
                    }
                }
                else
                {
                    Console.WriteLine("Port number must be a valid integer.");
                }
            }
        }
        public void Connect()
        {
            if (!Connected)
            {
                try
                {
                    client = new TcpClient(serverIP, serverPort);
                    Nstream = client.GetStream();
                    Connected = true;
                    Console.WriteLine("Connected to server.");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Connection error: {ex.Message}");
                }
            }
            else
            {
                Console.WriteLine("Already connected."); // shoudnt get here as no way to get back to connection text once connected
            }
        }
        public void Disconnect()
        {
            if (Connected)
            {
                client.Close();
                Nstream.Close();
                Connected = false;
                Disconnected = true;
                Console.WriteLine("Disconnected.");
            }
        }

        // Messaging Functions
        private bool ValidateInputText(ref string userText)
        {
            if (string.IsNullOrEmpty(userText))
            {
                return false;
            }

            userText = Regex.Replace(userText, @"\s+", "");
            return true;
        }
        private bool ValidateIPAddr(string ip)
        {
            if (string.IsNullOrWhiteSpace(ip))
            {
                return false;
            }

            var parts = ip.Split('.');
            if (parts.Length != 4)
            {
                return false;
            }

            foreach (var part in parts)
            {
                if (!int.TryParse(part, out int num) || num < 0 || num > 255)
                {
                    return false;
                }
            }

            return true;
        }
        private bool ValidatePort(int port)
        {
            return port >= 0 && port <= 65535;
        }
        private bool ValidateMessage(string message)
        {
            if (!CanSendMessage)
            {
                Console.WriteLine("Cooldown in progress. Please wait.");
                return false;
            }

            if (string.IsNullOrWhiteSpace(message))
            {
                Console.WriteLine("Message is empty.");
                return false;
            }

            if (message.Length > MaxMessageLength)
            {
                Console.WriteLine("Message is too long.");
                return false;
            }

            if (message == LastMessage)
            {
                MessageCoolDown();
            }

            LastMessage = message;
            return true;
        }
        private void MessageCoolDown()
        {
            CanSendMessage = false;
            Console.WriteLine("Cooldown started. Please wait.");

            Task.Delay(DefaultMessageCoolDown).ContinueWith(t =>
            {
                CanSendMessage = true;
                Console.WriteLine("Cooldown ended. You can now send a message.");
            });
        }
        public async Task SendMessage(string message) // might be hard with encryption as the packets are combined with header. im not sure tho as im stupid and dont know anything.
        {
            if (Connected)
            {
                try
                {
                    if (ValidateMessage(message))
                    {
                        byte[] payload = Encoding.ASCII.GetBytes(message);
                        int payloadLength = payload.Length;

                        // Create a combined array for header and payload
                        byte[] messageData = new byte[ProtocolConsts.HeaderSize + payloadLength];

                        // Create the header
                        byte[] header = new byte[ProtocolConsts.HeaderSize];
                        header[0] = ProtocolConsts.TypeData;
                        BitConverter.GetBytes(payloadLength).CopyTo(header, 1);

                        // Makea du herdar an du payroad one, togefur forevwer
                        Array.Copy(header, 0, messageData, 0, ProtocolConsts.HeaderSize);
                        Array.Copy(payload, 0, messageData, ProtocolConsts.HeaderSize, payloadLength);

                        // Send the combined packet to seber
                        await Nstream.WriteAsync(messageData, 0, messageData.Length);

                        Console.WriteLine("Message sent.");
                    }
                    else
                    {
                        Console.WriteLine("Message validation failed.");
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine("Message error: {0}", ex.Message);
                }
            }
            else
            {
                Console.WriteLine("Client is not connected to the server.");
            }
        }

        private async Task SendServerPing()
        {
            if (Connected)
            {
                try
                {
                    byte[] pingPacket = new byte[ProtocolConsts.HeaderSize];
                    pingPacket[0] = ProtocolConsts.TypePing;
                    BitConverter.GetBytes(0).CopyTo(pingPacket, 1);

                    await Nstream.WriteAsync(pingPacket, 0, pingPacket.Length);
                    Console.WriteLine("Ping packet sent.");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error sending ping: {ex.Message}");
                    Disconnected = true;
                    Connected = false;
                }
            }
            else
            {
                Console.WriteLine("Not connected to the server.");
            }
        }
        private async Task ReceiveServerPing()
        {
            if (Connected)
            {
                try
                {
                    byte[] buffer = new byte[ProtocolConsts.HeaderSize];

                    int bytesRead = await Nstream.ReadAsync(buffer, 0, buffer.Length);

                    if (bytesRead > 0)
                    {
                        if (buffer[0] == ProtocolConsts.TypePing && BitConverter.ToInt32(buffer, 1) == 0)
                        {
                            Console.WriteLine("Ping response received.");
                        }
                        else
                        {
                            Console.WriteLine("Unexpected response received.");
                            Disconnected = true;
                            Connected = false;
                        }
                    }
                    else
                    {
                        Console.WriteLine("No response received from server.");
                        Disconnected = true;
                        Connected = false;
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error receiving response: {ex.Message}");
                    Disconnected = true;
                    Connected = false;
                }
            }
            else
            {
                Console.WriteLine("Not connected to the server.");
            }
        }
        public async Task ClientServerConnection()
        {
            if (Connected)
            {
                try
                {
                    await SendServerPing();
                    await ReceiveServerPing();
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error during server connection: {ex.Message}");
                    Disconnected = true;
                    Connected = false;
                }
            }
            else
            {
                Console.WriteLine("Not connected to the server.");
            }
        }

    }

    static async Task Main(string[] args)
    {
        ClientData clientData = new ClientData();
        clientData.Init();

        Task periodicTask = PeriodicClientServerConnection(clientData);

        try
        {
            while (clientData.Connected)
            {
                string userInput = Console.ReadLine();
                if (userInput == "/Exit")
                {
                    clientData.Disconnect();
                    break;
                }
                else if (!string.IsNullOrEmpty(userInput))
                {
                    if (clientData.Connected)
                    {
                        await clientData.SendMessage(userInput);
                    }
                }
            }
        }
        finally
        {
            clientData.Disconnect();
            await periodicTask;
            Console.WriteLine("Lost connection, connected bool changed.");
        }
    }

    static async Task PeriodicClientServerConnection(ClientData clientData)
    {
        while (clientData.Connected)
        {
            try
            {
                await clientData.ClientServerConnection();
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error while connection check: {0}", ex.Message);
            }

            await Task.Delay(10000); // 10 sec int between each server ping
        }
    }
}
