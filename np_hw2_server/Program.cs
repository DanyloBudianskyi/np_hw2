using System.Net;
using System.Net.Sockets;
using System.Text;

namespace np_hw1_server
{
    public class ChatServer
    {
        public List<Client> Clients = new List<Client>();
        private UdpClient Listener { get; set; }
        private Dictionary<string, string> UserInfo = new Dictionary<string, string>() { { "User1", "password1" }, { "User2", "password2" } };
        private int Port { get; set; }

        public ChatServer(int port)
        {
            Port = port;
            Listener = new UdpClient(port);
        }

        public async Task Listen()
        {
            Console.WriteLine("Server started and listening...");
            while (true)
            {
                UdpReceiveResult result = await Listener.ReceiveAsync();
                HandleClientAsync(result);
            }
        }

        private async void HandleClientAsync(UdpReceiveResult result)
        {
            byte[] buffer = result.Buffer;
            IPEndPoint clientEndPoint = result.RemoteEndPoint;
            string message = Encoding.UTF8.GetString(buffer);

            if (!Clients.Any(c => c.EndPoint.Equals(clientEndPoint)))
            {
                string[] userInfo = message.Split(' ');
                if (userInfo.Length == 2 && AuthUser(userInfo[0], userInfo[1]))
                {
                    Clients.Add(new Client { EndPoint = clientEndPoint, Username = userInfo[0] });
                    Console.WriteLine($"{userInfo[0]} has joined");
                }
                else
                {
                    Console.WriteLine($"Authentication failed for user: {userInfo[0]}");
                    return;
                }
            }

            Client client = Clients.First(c => c.EndPoint.Equals(clientEndPoint));
            if (message.StartsWith("/file"))
            {
                string[] fileMessage = message.Split(' ', 3);
                if (fileMessage.Length == 3)
                {
                    string targetUser = fileMessage[1];
                    string filePath = fileMessage[2];
                    Console.WriteLine(filePath);
                    ReceiveAndForwardFile(targetUser, clientEndPoint, filePath);
                }
            }
            else if (message.StartsWith("/pm")) 
            {
                string[] pmMessage = message.Split(' ', 3); 
                if (pmMessage.Length == 3) 
                {
                    string targetUser = pmMessage[1]; 
                    string privateMessage = pmMessage[2]; 
                    SendPrivateMessage(targetUser, $"{client.Username}: {privateMessage}", client.EndPoint); 
                }
            }
            else if(message == "/history")
            {
                SendHistory(clientEndPoint, client.MessageHistory);
            }
            else
            {
                Console.WriteLine($"{client.Username}: {message}");
                client.MessageHistory.Add(message);
                BroadCastMessage($"{client.Username}: {message}", client.EndPoint);
            }

            if (message == "/quit")
            {
                Clients.Remove(client);
                Console.WriteLine($"Client {clientEndPoint} has left the chat.");
            }
        }
        private static byte[] ReceiveFile(int receivePort)
        {
            const int size_buffer = 8192;
            byte[] file_buffer = null;

            UdpClient server = null;
            try
            {
                server = new UdpClient(receivePort);
                IPEndPoint clientReceiveEndPoint = new IPEndPoint(IPAddress.Any, receivePort);

                byte[] file_size_result = server.Receive(ref clientReceiveEndPoint);
                int file_size = BitConverter.ToInt32(file_size_result, 0);

                file_buffer = new byte[file_size];
                int received_bytes = 0;
                while (received_bytes < file_size)
                {
                    byte[] buffer_part = server.Receive(ref clientReceiveEndPoint);
                    Array.Copy(buffer_part, 0, file_buffer, received_bytes, buffer_part.Length);
                    received_bytes += buffer_part.Length;
                }
            }
            finally
            {
                server?.Close();
            }

            return file_buffer;
        }



        private static void ForwardFile(byte[] file_buffer, string targetIp, int forwardPort)
        {
            const int size_buffer = 8192;

            UdpClient forwardClient = null;
            try
            {
                forwardClient = new UdpClient();
                IPEndPoint forwardEndPoint = new IPEndPoint(IPAddress.Parse(targetIp), forwardPort);
                forwardClient.Send(BitConverter.GetBytes(file_buffer.Length), 4, forwardEndPoint);
                for (int i = 0; i < file_buffer.Length; i += size_buffer)
                {
                    int part_size = Math.Min(size_buffer, file_buffer.Length - i);
                    byte[] buffer_part = new byte[part_size];
                    Array.Copy(file_buffer, i, buffer_part, 0, part_size);
                    forwardClient.Send(buffer_part, part_size, forwardEndPoint);
                }
            }
            finally
            {
                forwardClient?.Close();
            }
        }



        private void ReceiveAndForwardFile(string targetUser, IPEndPoint clientEndPoint, string filePath)
        {
            Client targetClient = Clients.FirstOrDefault(c => c.Username == targetUser);
            if (targetClient != null)
            {
                string fileName = Path.GetFileName(filePath);
                string notificationMessage = "file:" + fileName;
                byte[] notificationBuffer = Encoding.UTF8.GetBytes(notificationMessage);
                Listener.Send(notificationBuffer, notificationBuffer.Length, targetClient.EndPoint);

                string targetIp = "127.0.0.1";
                int receivePort = 5001;
                int forwardPort = 5002;
                byte[] file_buffer = ReceiveFile(receivePort);
                Console.WriteLine("File received");
                ForwardFile(file_buffer, targetIp, forwardPort);
                Console.WriteLine("File forwarded");
            }
        }
        private void SendPrivateMessage(string targetUser, string message, IPEndPoint senderEndPoint) 
        {
            Client targetClient = Clients.FirstOrDefault(c => c.Username == targetUser); 
            if (targetClient != null) 
            {
                byte[] buffer = Encoding.UTF8.GetBytes(message); 
                Listener.Send(buffer, buffer.Length, targetClient.EndPoint); 
            } 
        }
        private void SendHistory(IPEndPoint clientEndPoint, List<string> messageHistory) 
        {
            foreach (var message in messageHistory) 
            {
                string historyMessage = "history:" + message; 
                byte[] buffer = Encoding.UTF8.GetBytes(historyMessage); 
                Listener.Send(buffer, buffer.Length, clientEndPoint); 
            } 
        }

        public void BroadCastMessage(string message, IPEndPoint excludeClient)
        {
            byte[] buffer = Encoding.UTF8.GetBytes(message);
            foreach (var c in Clients)
            {
                if (!c.EndPoint.Equals(excludeClient))
                {
                    Listener.Send(buffer, buffer.Length, c.EndPoint);
                }
            }
        }

        public bool AuthUser(string username, string password)
        {
            return UserInfo.TryGetValue(username, out var _password) && _password == password;
        }
    }

    public class Client
    {
        public IPEndPoint EndPoint { get; set; }
        public string Username { get; set; }
        public List<string> MessageHistory { get; set; } = new List<string>();
    }

    public class Program
    {
        static async Task Main(string[] args)
        {
            ChatServer chatServer = new ChatServer(5000);
            await chatServer.Listen();
        }
    }
}
