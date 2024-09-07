// sorry laptop this program will prob cause a shit ton of network traffic / junk
// might add cool interactive console to this but no need as will use winforms soon to finish up

// NO WORK WITH SOME VPN CONFIGS

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

using System.Globalization;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;

public static class ProtocolConsts
{
    public const byte TypePing = 0x01;
    public const byte TypeData = 0x02;
    public const byte TypePrivateGroup = 0x03;
    public const byte TypePrivateManage = 0x04; // for group creation and deletion and porb name changes idk
    public const byte TypeOther = 0x05; // mainly for client id sending

    public const int HeaderSize = 5; // MessageType (1 byte) + PayloadLength (4 bytes)
}

class ClientTCPApp
{
    public class ClientData
    {
        private static readonly char[] GroupIdChars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789!@#%&*".ToCharArray();

        private TcpClient client;
        private NetworkStream Nstream;

        // Client Info
        public string ClientIp { get; private set; }
        public int ClientPort { get; private set; }
        public string ClientId { get; private set; }
        public string DominantGroupId;
        public Queue<string> ClientGroupIds = new Queue<string>();

        // Server Info
        private string serverIP { get; set; }
        private int serverPort { get; set; }

        // Client msg info / params
        private bool CanSendMessage = true;
        private readonly int MaxMessageLength = 100;
        private readonly int DefaultMessageCoolDown = 5000;
        private readonly int MaxSameMessageCount = 3;
        private string LastMessage;
        private int tempMsgCounter = 0;

        // Client Connection State
        public bool Connected { get; private set; }
        public bool Disconnected { get; private set; }

        // Client connection / connection setup / connection break down / other connection state function toggles
        public async Task Init()
        {
            while (!Connected)
            {
                Console.WriteLine("Enter Server IP and Port number to Connect (IP:PORT): ");
                string input = Console.ReadLine();

                string[] parts = input.Split(':');

                if (parts.Length == 2)
                {
                    string tempIp = parts[0].Trim();
                    if (int.TryParse(parts[1].Trim(), out int tempPort))
                    {
                        if (ValidateInputText(ref tempIp) && ValidateIPAddr(tempIp) && ValidatePort(tempPort))
                        {
                            serverIP = tempIp;
                            serverPort = tempPort;
                            await Connect();
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
                else
                {
                    Console.WriteLine("\u001b[33mInput must be in the format <IP>:<Port>. Please try again.\u001b[0m");
                }
            }
        }
        public async Task Connect()
        {
            if (!Connected)
            {
                try
                {
                    client = new TcpClient(serverIP, serverPort);
                    Nstream = client.GetStream();
                    GetClientIpAddress(); GetClientPort();
                    ReceiveIdAsync().Wait();
                    Connected = true;
                    Disconnected = false;
                    Console.WriteLine("\u001b[32mConnected to server.\u001b[0m");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"\u001b[31mConnection error: {ex.Message}\u001b[0m");
                    Connected = false;
                    Disconnected = true;
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
                try
                {
                    client.Close();
                    Nstream.Close();
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"\u001b[31mError closing connection: {ex.Message}\u001b[0m");
                }
                finally
                {
                    Connected = false;
                    Disconnected = true;
                    Console.WriteLine("\u001b[33mDisconnected.\u001b[0m");
                }
            }
        }
        private async Task ReceiveIdAsync()
        {
            try
            {
                // Read the header
                byte[] headerBuffer = new byte[ProtocolConsts.HeaderSize];
                int headerBytesRead = await Nstream.ReadAsync(headerBuffer, 0, ProtocolConsts.HeaderSize);
                if (headerBytesRead < ProtocolConsts.HeaderSize)
                {
                    throw new Exception("Invalid client info message: Incomplete header.");
                }

                byte messageType = headerBuffer[0];
                int payloadLength = BitConverter.ToInt32(headerBuffer, 1);

                if (messageType == ProtocolConsts.TypeOther)
                {
                    // Read the payload
                    byte[] payloadBuffer = new byte[payloadLength];
                    int payloadBytesRead = await Nstream.ReadAsync(payloadBuffer, 0, payloadLength);
                    if (payloadBytesRead < payloadLength)
                    {
                        throw new Exception("Invalid client info message: Incomplete payload.");
                    }

                    int offset = 0;

                    // Read ClientId, assuming the format is XXX-XXX with a fixed length of 7
                    int idLength = 7;
                    if (payloadLength < idLength)
                    {
                        throw new Exception("Invalid payload length for ClientId.");
                    }
                    ClientId = Encoding.ASCII.GetString(payloadBuffer, offset, idLength);
                    offset += idLength;

                    // Validate the ClientId
                    if (!ValidateClientId(ClientId))
                    {
                        throw new Exception($"Invalid ClientId received: {ClientId}");
                    }

                    // Validate IP address length
                    int ipLength = payloadLength - idLength - 4; // 4 bytes reserved for port
                    if (ipLength <= 0 || offset + ipLength > payloadLength)
                    {
                        throw new Exception("Invalid IP address length.");
                    }
                    string receivedIp = Encoding.ASCII.GetString(payloadBuffer, offset, ipLength);
                    if (!ValidateIPAddr(receivedIp))
                    {
                        throw new Exception($"Invalid IP address received: {receivedIp}");
                    }
                    offset += ipLength;

                    if (receivedIp != ClientIp)
                    {
                        Console.WriteLine($"Received IP doesn't match the client's IP. Client IP: {ClientIp}, Received IP: {receivedIp}");
                        return;
                    }

                    // Validate port number
                    if (offset + 4 > payloadLength)
                    {
                        throw new Exception("Invalid payload length for port number.");
                    }
                    int receivedPort = BitConverter.ToInt32(payloadBuffer, offset);
                    if (!ValidatePort(receivedPort))
                    {
                        throw new Exception($"Invalid port number received: {receivedPort}");
                    }

                    if (receivedPort != ClientPort)
                    {
                        Console.WriteLine("Received port doesn't match the client's port.");
                        return;
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error receiving client info: {ex.Message}");
            }
        }

        private void GetClientIpAddress() // MIGHT BE A FUCKIN RETARD BUT CANT GET THE CLIENTS IPV4 ADDR FOR THE LIFE OF ME 
        {
        }
        private void GetClientPort()
        {
            ClientPort = client?.Client?.LocalEndPoint is IPEndPoint endPoint ? endPoint.Port : -1;
        }//RANDOM CODE FOUND ON STACKOVERFLOW

        // Messaging Validate Functions
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

        //Other Validate Functions
        private bool ValidateGroupId()
        {
            if (ClientGroupIds.Count > 0)
            {
                foreach (string groupId in ClientGroupIds)
                {
                    if (groupId.Length == 17 && groupId[5] == '-' && groupId[11] == '-')
                    {
                        string tempGroupId = groupId.Replace("-", ""); // can use string.Empty instead of "" but poop balls cheese 
                        if (tempGroupId.Length == 15)
                        {
                            bool isValid = true;
                            foreach (char c in tempGroupId)
                            {
                                if (Array.IndexOf(GroupIdChars, c) == -1)
                                {
                                    isValid = false;
                                    break;
                                }
                            }

                            if (isValid)
                            {
                                return true;
                            }
                        }
                    }
                }
            }
            return false;
        }
        private bool ValidateClientId(string clientId)
        {
            char[] validChars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789-".ToCharArray();
            if (string.IsNullOrWhiteSpace(clientId))
            {
                return false;
            }

            if (clientId.Length != 7)
            {
                return false;
            }

            return clientId.All(c => validChars.Contains(c));
        }

        // Messaging Functions
        public async Task SendMessage(string groupId, string message) // might be hard with encryption as the packets are combined with header. im not sure tho as im stupid and dont know anything.
        {
            if (Connected)
            {
                try
                {
                    if (ValidateMessage(message))
                    {
                        if (CanSendMessage)
                        {
                            byte[] payload = Encoding.ASCII.GetBytes(ClientId + "-" + groupId + "-" + message); // make sure when sending message the group id is outlined and the sender is too
                            int payloadLength = payload.Length;

                            // Create a combined array for header and payload
                            byte[] messageData = new byte[ProtocolConsts.HeaderSize + payloadLength];

                            // Create the header
                            byte[] header = new byte[ProtocolConsts.HeaderSize];
                            header[0] = ProtocolConsts.TypePrivateGroup; // all messages between clients will be considerd as groups coz its just easier as the way its been set up with the ids
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
        public async Task RecieveMessage() // only when a client is in a group will be better doing this server side
        {
            if (Connected)
            {

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
                    Console.WriteLine($"\u001b[31mPing error: {ex.Message}\u001b[0m");
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
                        if (buffer[0] != ProtocolConsts.TypePing || BitConverter.ToInt32(buffer, 1) != 0)
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
                    Console.WriteLine($"\u001b[31mError receiving response: {ex.Message}\u001b[0m");
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
                    Console.WriteLine($"\u001b[31mError during server connection: {ex.Message}\u001b[0m");
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
        public async Task ServerConCheck()
        {
            while (Connected)
            {
                try
                {
                    await ClientServerConnection();
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"\u001b[31mError while connection check: {ex.Message}\u001b[0m");
                }

                await Task.Delay(5000); // 5 sec interval between each server ping
            }
        }

        private static readonly object lockObj = new object();
        private static Random random = new Random();
        public string GenGroupId()
        {
            char[] id = new char[15];
            lock (lockObj)
            {
                for (int i = 0; i < id.Length; i++)
                {
                    id[i] = GroupIdChars[random.Next(GroupIdChars.Length)];
                }
            }

            // Convert char array to string and insert dashes
            string idString = new string(id);
            for (int i = 5; i < idString.Length; i += 6)
            {
                idString = idString.Insert(i, "-");
            }

            return idString; // Should be something like XXXXX-XXXXX-XXXXX
        }
    }

    class ClientUI : ClientData
    {
        private readonly ClientData clientData;

        public ClientUI()
        {
            clientData = this; // Assuming this is the intended instance
        }

        ConsoleKeyInfo key;
        int index = 1;
        bool isSelected = false;
        string GreenColour = "\u001b[94m";
        bool bExit = false;
        bool[] toggledOptions = new bool[6];

        private void Header()
        {
            Console.WriteLine("Bean Client Console UI - Made By Le Monke");
            Console.WriteLine("Use Arrow Keys to navigate UI and use Enter/Return to select.\n");
        }

        public void Main()
        {
            while (!bExit)
            {
                Console.Clear();

                (int Left, int Top) = Console.GetCursorPosition();
                Console.SetCursorPosition(Left, Top);

                Header();

                Console.WriteLine($"{(index == 1 ? GreenColour : "")}Connect To Server{(Connected ? " \u001b[92mX Connected To Server" : " \u001b[31mX Not Connected To Server")}\u001b[0m");
                Console.WriteLine($"{(index == 2 ? GreenColour : "")}Get Client/Group Info{(toggledOptions[1] ? " \u001b[92mX" : "")}\u001b[0m");
                Console.WriteLine($"{(index == 3 ? GreenColour : "")}Start Group{(toggledOptions[2] ? " \u001b[92mX" : "")}\u001b[0m");
                Console.WriteLine($"{(index == 4 ? GreenColour : "")}Send Message{(toggledOptions[3] ? " \u001b[92mX" : "")}\u001b[0m");
                Console.WriteLine($"{(index == 5 ? GreenColour : "")}Leave Group{(toggledOptions[4] ? " \u001b[92mX" : "")}\u001b[0m");
                Console.WriteLine($"{(index == 6 ? GreenColour : "")}Exit{(toggledOptions[5] ? " \u001b[92mX" : "")}\u001b[0m");

                key = Console.ReadKey(true);

                switch (key.Key)
                {
                    case ConsoleKey.DownArrow:
                        index = (index == 6) ? 1 : index + 1;
                        break;
                    case ConsoleKey.UpArrow:
                        index = (index == 1) ? 6 : index - 1;
                        break;
                    case ConsoleKey.Enter:
                        isSelected = true;
                        HandleSelection();
                        isSelected = false;
                        break;
                }
            }
        }

        private void SelectGroup()
        {
            Console.Clear();
            Header();
            if (Connected)
            {
                if (ClientGroupIds.Count != 0)
                {
                    while (string.IsNullOrEmpty(DominantGroupId))
                    {
                        DominantGroupId = "";
                        Console.WriteLine("Connected Groups: {0}", ClientGroupIds.Count);
                        foreach (var group in ClientGroupIds)
                        {
                            Console.WriteLine("Group Id: {0}", group);
                        }
                        string inGroupId = Console.ReadLine();
                        if (!string.IsNullOrWhiteSpace(inGroupId) && ClientGroupIds.Contains(inGroupId))
                        {
                            DominantGroupId = inGroupId;
                            Console.WriteLine("Selected Group: {0}", DominantGroupId);
                        }
                        else
                        {
                            Console.WriteLine("Invalid Group Id. Try Again");
                        }
                    }
                }
                else
                {
                    Console.WriteLine("Join a group or create one to send messages.");
                }
            }
            Console.ReadKey(true);
        }

        private void LocalClientGroupInfo()
        {
            Console.Clear();
            Header();
            Console.WriteLine("Client IP: {0}", clientData.ClientIp);
            Console.WriteLine("Client Port: {0}", clientData.ClientPort);
            Console.WriteLine("Client ID: {0}", clientData.ClientId);
            Console.WriteLine("Dominant Group ID: {0}", DominantGroupId ?? "None");

            if (ClientGroupIds.Count > 0)
            {
                Console.WriteLine("Groups:");
                foreach (string groupId in ClientGroupIds)
                {
                    Console.WriteLine("- {0}", groupId);
                }
            }
            else
            {
                Console.WriteLine("Client in no groups.");
            }
            Console.ReadKey(true);
        }

        private void StartGroup()
        {
            if (Connected)
            {
                string newGroupId = GenGroupId();
                ClientGroupIds.Enqueue(newGroupId);
                DominantGroupId = newGroupId;
                Console.WriteLine("Group {0} started and set as the dominant group.", newGroupId);
            }
            else
            {
                Console.WriteLine("You need to be connected to the server to start a group.");
            }
            Console.ReadKey(true);
        }

        private void SendMessage()
        {
            Console.Clear();
            Header();
            if (Connected)
            {
                if (string.IsNullOrEmpty(DominantGroupId))
                {
                    Console.WriteLine("You need to select a group first.");
                }
                else
                {
                    Console.WriteLine("Enter your message:");
                    string message = Console.ReadLine();
                    if (message.Length > 0)
                    {
                        SendMessage(DominantGroupId, message).Wait();
                    }
                    else
                    {
                        Console.WriteLine("Message cannot be empty.");
                    }
                }
            }
            else
            {
                Console.WriteLine("You need to be connected to the server to send a message.");
            }
            Console.ReadKey(true);
        }

        private void LeaveGroup()
        {
            Console.Clear();
            Header();
            Console.ReadKey(true);
        }

        private async void HandleSelection()
        {
            switch (index)
            {
                case 1:
                    await Init();
                    break;
                case 2:
                    LocalClientGroupInfo();
                    break;
                case 3:
                    StartGroup();
                    break;
                case 4:
                    SendMessage();
                    break;
                case 5:
                    LeaveGroup();
                    break;
                case 6:
                    bExit = true;
                    break;
            }
        }
    }


    static Task Main(string[] args)
    {
        ClientUI clientUI = new ClientUI();
        clientUI.Main();

        clientUI.Disconnect();
        return Task.CompletedTask;
    }
}
