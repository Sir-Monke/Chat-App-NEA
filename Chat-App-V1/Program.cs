using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Numerics;
using System.Text;
using System.Threading;

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
            while (ServerFound == false)
            {
                Console.WriteLine("Enter Server Ip To Connect: ");
                string tempIp = Console.ReadLine();
                Console.WriteLine("Enter Server Port number To Connect: ");
                Int32 tempPort = Convert.ToInt32(Console.ReadLine());
                if (ValidateIPAddr(tempIp) && ValidatePort(tempPort))
                {
                    serverIP = tempIp; // Assign new server Ip
                    serverPort = tempPort;
                    ServerFound = true;
                }
                else
                {
                    Console.WriteLine("Server IP too short or is invalid.\nPress enter to retry.");
                    Console.ReadLine();
                    Console.Clear();
                    continue;
                }
            }
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

        private bool ValidatePort (Int32 tempPort)
        {
            if (tempPort >= 0 && tempPort <= 65535)
            {
                return true;
            }
            return false;
        }

        private bool MessageCoolDown()
        {
            if (this.CanSendMessage == false)
            {
                Console.WriteLine("Cooldown already in progress. Please wait.");
                return true;
            }

            this.CanSendMessage = false;
            Console.WriteLine("Cooldown started. You need to wait before sending the next message.");

            new Thread(() =>
            {
                int elapsed = 0;
                while (elapsed < this.DefaultMessageCoolDown)
                {
                    Thread.Sleep(1000); // Sleep for 1 second intervals
                    elapsed += 1000;
                    Console.WriteLine($"Cooldown running... {elapsed / 1000} seconds elapsed.");
                }
                this.CanSendMessage = true;
                Console.WriteLine("Cooldown ended. You can now send a message.");
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
        ClientData ClientData = new ClientData();
        string userin = null;
        bool bExit = false;

        ClientData.Init();
        ClientData.Connect();
        while (bExit != true)
        {
            if (userin == "Exit")
            {
                break;
            }
            if (userin != null)
            {
                userin = null;
            }
            else
            {
                userin = Console.ReadLine();
                ClientData.SendMessage(userin);
            }
        }
        ClientData.Disconnect();
        Thread.Sleep(3000);
    }
}
