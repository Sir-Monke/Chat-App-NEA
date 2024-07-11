using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Numerics;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;

public static class ProtocolConsts // Header Stuff
{
    public const byte TypePing = 0x01;
    public const byte TypeData = 0x02;
    public const byte TypeOther = 0x03;

    public const int HeaderSize = 5; // MessageType (1 byte) + PayloadLength (4 bytes)
}

class ClientTCPApp
{
    public class ClientData()
    {

        TcpClient client = null;
        NetworkStream Nstream = null;

        private string serverIP = null; // Replace with your server's IP address - dynamic IP as its machines local IP
        private Int32 serverPort = 0; // Replace with your server's port number - port will always be 80 as its default

        private bool CanSendMessage = true; // only for message cool down, false - is in cool down
        private const Int16 MaxMessageLength = 256; // Maximum char length of message, might inc later depends on preformance
        private const Int16 MaxDupeMessage = 2; // Maximum ammount of messages able to send before cooldown
        private int DefaultMessageCoolDown = 5000;
        private string LastMessage = null;

        private bool ServerFound = false;
        private bool Connected = false;
        private bool Disconnected = false;

        // Client Connection / Connection Info
        public string GetServerIP() { return this.serverIP; }
        public Int32 GetServerPort() { return this.serverPort; }
        public bool GetConnected() {  return this.Connected; }
        public bool GetDisconnected() {  return this.Disconnected; } // dont really need as connected is bool and there can only be connected or disconnected

        public void Connect()
        {
            if (ServerFound == true)
            {
                try
                {
                    client = new TcpClient(this.GetServerIP(), this.GetServerPort());
                    Nstream = client.GetStream();// Get a network stream for reading and writing
                    this.Connected = true;
                    Console.WriteLine("Connected to server.\n");
                }
                catch (IOException ex)
                {
                    Console.WriteLine("Connection error: Source:{0} About:{1}\n", ex.Source, ex.Message);
                }
                catch (ObjectDisposedException ex)
                {
                    Console.WriteLine("Connection error: Source:{0} About:{1}\n", ex.Source, ex.Message);
                }
            }
            else
            {
                Console.WriteLine("Please config server before trying to connect.\n");
            }
        }

        public void Disconnect() // not sure if this needs error checking
        {
            if (Connected == true)
            {
                client.Close();
                Nstream.Close();
                this.Disconnected = true;
                Console.WriteLine("Disconnected.\n");
            }
        }

        // Client Init
        public void Init()
        {
            while (!ServerFound)
            {
                Console.WriteLine("Enter Server IP To Connect: ");
                string tempIp = Console.ReadLine();
                Console.WriteLine("Enter Server Port number To Connect: ");
                Int32 tempPort = Convert.ToInt32(Console.ReadLine());

                // Validate and clean server IP address input
                if (ValidateInputText(ref tempIp) && ValidateIPAddr(tempIp) && ValidatePort(tempPort))
                {
                    serverIP = tempIp; // Assign new server IP
                    serverPort = tempPort;
                    ServerFound = true;
                }
                else
                {
                    Console.WriteLine("Server IP is invalid or contains invalid characters.\nPress enter to retry.");
                    Console.ReadLine();
                    Console.Clear();
                }
            }
        }

        //Check Server Status Client Side
        private async Task SendPingAsync()
        {
            try
            {
                // Send a ping message
                byte[] pingMessage = new byte[] { ProtocolConsts.TypePing };
                await Nstream.WriteAsync(pingMessage, 0, pingMessage.Length);

                // Wait for ping response (if any)
                byte[] responseBuffer = new byte[1];
                int bytesRead = await Nstream.ReadAsync(responseBuffer, 0, responseBuffer.Length);

                // Handle ping response
                byte responseType = responseBuffer[0];

                if (responseType == ProtocolConsts.TypePing)
                {
                    Console.WriteLine("Ping response received.");
                }
                else
                {
                    Console.WriteLine("Unexpected response received.");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error communicating with server: {ex.Message}.");
            }
        }

        // Validation
        private bool ValidateInputText(ref string UserText) // could use this for other things rather than just Ip input cba doing port if u cant enter a port num right ur just a mong
        {
            if (string.IsNullOrEmpty(UserText))
            {
                return false;
            }

            UserText = Regex.Replace(UserText, @"\s+", "");

            return true;
        }

        private bool ValidateIPAddr(string tempIP)
        {
            if (string.IsNullOrWhiteSpace(tempIP))
            {
                return false;
            }

            string[] SplitIP = tempIP.Split('.');
            if (SplitIP.Length != 4)
            {
                return false;
            }

            foreach (string Section in SplitIP)
            {
                if (!int.TryParse(Section, out int num) || num < 0 || num > 255)
                {
                    return false;
                }
            }

            return true;
        }

        private bool ValidatePort(Int32 tempPort)
        {
            if (tempPort >= 0 && tempPort <= 65535)
            {
                return true;
            }
            return false;
        }

        // Client Message Stuff
        private bool MessageCoolDown()
        {
            if (this.CanSendMessage == false)
            {
                Console.WriteLine("Cooldown already in progress. Please wait.\n");
                return true;
            }

            this.CanSendMessage = false;
            Console.WriteLine("Cooldown started. You need to wait before sending the next message.\n");

            new Thread(() =>
            {
                int elapsed = 0;
                while (elapsed < this.DefaultMessageCoolDown)
                {
                    Thread.Sleep(1000); // Sleep 1 second interval
                    elapsed += 1000;
                    Console.WriteLine($"Cooldown running... {elapsed / 1000} seconds elapsed.\n");
                }
                this.CanSendMessage = true;
                Console.WriteLine("Cooldown ended. You can now send a message.\n");
            }).Start();

            return false;
        }

        private bool ValidateMessage(string Message)
        {
            Int16 MessageCount = 0;

            if (CanSendMessage == true)
            {
                if (string.IsNullOrWhiteSpace(Message)) // message is empty or poop
                {
                    Console.WriteLine("Message invalid.\n");
                    return false;
                }

                if (Message.Length > MaxMessageLength) // message bigger than 256 chars 
                {
                    Console.WriteLine("Your message is too long.\n");
                    return false;
                }

                if (Message == this.LastMessage)
                {
                    MessageCoolDown();
                }

                MessageCount += 1;
                this.LastMessage = Message;
                return true;
            }
            else
            {
                Console.WriteLine("Cant send message are in cooldown.\n");
                return false;
            }

        }

        public void SendMessage(string Message)
        {
            if (this.Connected)
            {
                try
                {
                    if (ValidateMessage(Message))
                    {
                        Byte[] MessageBytes = Encoding.ASCII.GetBytes(Message);
                        this.Nstream.Write(MessageBytes, 0, MessageBytes.Length);
                        Console.WriteLine("Message Sent.\n");
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine("Try again later.\n");
                    Console.WriteLine("Message error: Source:{0} About:{1}\n", ex.Source, ex.Message);
                }
            }
            else
            {
                Console.WriteLine("Client not connected to server.\n");
            }
        }
    }

    static void Main(string[] args)
    {
        ClientData clientData = new ClientData();
        bool exit = false;

        clientData.Init();
        clientData.Connect();

        while (!exit)
        {
            string userInput = Console.ReadLine();

            if (userInput == "/Exit")
            {
                exit = true;
            }
            else if (!string.IsNullOrEmpty(userInput))
            {
                clientData.SendMessage(userInput);
            }
        }
        clientData.Disconnect();
        Thread.Sleep(3000);
    }
}
