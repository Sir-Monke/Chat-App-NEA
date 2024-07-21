// sorry laptop this program will prob cause a shit ton of network traffic / junk
// might add cool interactive console to this but no need as will use winforms soon to finish up

/*⠀⠀
  ⠀⠀⢀⣀⣀⡀⠀⠀⠀⠀⠀⠀⠀⣠⠾⠛⠶⣄⢀⣠⣤⠴⢦⡀⠀⠀⠀⠀
⠀⠀⠀⢠⡿⠉⠉⠉⠛⠶⠶⠖⠒⠒⣾⠋⠀⢀⣀⣙⣯⡁⠀⠀⠀⣿⠀⠀⠀⠀
⠀⠀⠀⢸⡇⠀⠀⠀⠀⠀⠀⠀⠀⢸⡏⠀⠀⢯⣼⠋⠉⠙⢶⠞⠛⠻⣆⠀⠀⠀
⠀⠀⠀⢸⣧⠆⠀⠀⠀⠀⠀⠀⠀⠀⠻⣦⣤⡤⢿⡀⠀⢀⣼⣷⠀⠀⣽⠀⠀⠀
⠀⠀⠀⣼⠃⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠉⠙⢏⡉⠁⣠⡾⣇⠀⠀⠀
⠀⠀⢰⡏⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠙⠋⠉⠀⢻⡀⠀⠀
⣀⣠⣼⣧⣤⠀⠀⠀⣀⡀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⣀⡀⠀⠀⠐⠖⢻⡟⠓⠒
⠀⠀⠈⣷⣀⡀⠀⠘⠿⠇⠀⠀⠀⢀⣀⣀⠀⠀⠀⠀⠿⠟⠀⠀⠀⠲⣾⠦⢤⠀
⠀⠀⠋⠙⣧⣀⡀⠀⠀⠀⠀⠀⠀⠘⠦⠼⠃⠀⠀⠀⠀⠀⠀⠀⢤⣼⣏⠀⠀⠀
⠀⠀⢀⠴⠚⠻⢧⣄⣀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⣀⣤⠞⠉⠉⠓⠀⠀
⠀⠀⠀⠀⠀⠀⠀⠈⠉⠛⠛⠶⠶⠶⣶⣤⣴⡶⠶⠶⠟⠛⠉⠀⠀⠀⠀⠀⠀⠀
*/

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
        private readonly int MaxMessageLength = 100;
        private readonly int DefaultMessageCoolDown = 5000;
        private readonly int MaxSameMessageCount = 3;
        private string LastMessage;
        private int tempMsgCounter = 0;

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
                        Console.WriteLine("\u001b[33mInvalid IP address or port number. Please try again.\u001b[0m");
                    }
                }
                else
                {
                    Console.WriteLine("\u001b[33mPort number must be a valid integer.\u001b[0m");
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
                    Console.WriteLine("\u001b[32mConnected to server.\u001b[0m");
                }
                catch (Exception ex)
                {
                    Console.WriteLine("\u001b[31mConnection error: server actively refused connection or not valid server ip/port.\u001b[0m");
                }
            }
            else
            {
                Console.WriteLine("\u001b[33mAlready connected.\u001b[0m");
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
                Console.WriteLine("\u001b[33mDisconnected.\u001b[0m");
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
                Console.WriteLine("\u001b[33mCooldown in progress. Please wait.\u001b[0m");
                return false;
            }

            if (string.IsNullOrWhiteSpace(message))
            {
                return false;
            }

            if (message.Length > MaxMessageLength)
            {
                Console.WriteLine("\u001b[31mMessage is too long.\u001b[0m");
                return false;
            }

            if (message == LastMessage)
            {
                tempMsgCounter++;
                if (tempMsgCounter == MaxSameMessageCount) 
                {
                    MessageCoolDown();
                }
            }

            LastMessage = message;
            return true;
        }
        private void MessageCoolDown()
        {
            CanSendMessage = false;
            Console.WriteLine("\u001b[33mCooldown started. Please wait.\u001b[0m");

            Task.Delay(DefaultMessageCoolDown).ContinueWith(t =>
            {
                CanSendMessage = true;
                tempMsgCounter = 0;
                Console.WriteLine("\u001b[33mCooldown ended.\u001b[0m");
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
                        if (CanSendMessage)
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
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine("\u001b[31mMessage error: {0}\u001b[0m", ex.Message);
                }
            }
            else
            {
                Console.WriteLine("\u001b[31mClient is not connected to the server.\u001b[0m");
            }
        }

        // Server Connection State not sure if i need this many funcitons for this but oh well
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
                }
                catch (Exception ex)
                {
                    Disconnected = true;
                    Connected = false;
                }
            }
            else
            {
                Console.WriteLine("\u001b[31mNot connected to the server.\u001b[0m");
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
                        if (buffer[0] != ProtocolConsts.TypePing && BitConverter.ToInt32(buffer, 1) != 0)
                        {
                            Console.WriteLine("\u001b[31mUnexpected response received.\u001b[0m");
                            Disconnected = true;
                            Connected = false;
                        }
                    }
                    else
                    {
                        Console.WriteLine("\u001b[31mNo response received from server.\u001b[0m");
                        Disconnected = true;
                        Connected = false;
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine("\u001b[31mError receiving response: {0}\u001b[0m", ex.Message);
                    Disconnected = true;
                    Connected = false;
                }
            }
            else
            {
                Console.WriteLine("\u001b[31mNot connected to the server.\u001b[0m");
            }
        }
        private async Task ClientServerConnection()
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
                    Console.WriteLine("\u001b[31mError during server connection: {0}\u001b[0m", ex.Message);
                    Disconnected = true;
                    Connected = false;
                }
            }
            else
            {
                Console.WriteLine("\u001b[31mNot connected to the server.\u001b[0m");
                Connected = false;
                Init();
            }
        }
        public async Task ServerConCheck(ClientData clientData)
        {
            while (clientData.Connected)
            {
                try
                {
                    await clientData.ClientServerConnection();
                }
                catch (Exception ex)
                {
                    Console.WriteLine("\u001b[31mError while connection check: {0}\u001b[0m", ex.Message);
                }

                await Task.Delay(5000); // 5 sec int between each server ping - need to find best time for this
            }
        }
    }

    static async Task Main(string[] args)
    {
        ClientData clientData = new ClientData();
        clientData.Init();

        Task periodicTask = clientData.ServerConCheck(clientData);

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
            Console.WriteLine("\u001b[31mLost connection, connected bool changed.\u001b[0m");
        }
    }
}
