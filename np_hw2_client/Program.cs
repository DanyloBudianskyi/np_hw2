using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace ls1_client
{
    public class Client
    {
        private static UdpClient client { get; set; } = new UdpClient();
        private static IPEndPoint serverEndpoint { get; set; } = new IPEndPoint(IPAddress.Parse("127.0.0.1"), 5000);
        private static string Username { get; set; }
        private static string Password { get; set; }

        static async Task Main(string[] args)
        {
            Console.Write("Enter your username: ");
            Username = Console.ReadLine();
            Console.Write("Enter your password: ");
            Password = Console.ReadLine();

            string userInfo = $"{Username} {Password}";
            byte[] buffer = Encoding.UTF8.GetBytes(userInfo);
            await client.SendAsync(buffer, buffer.Length, serverEndpoint);

            Task receiveTask = ReceiveMessageAsync();
            Console.Write("Connected to server.\n/quit - to exit.\n/pm username and your message - for private message.\n/file username filepath - send file to user \n/history to check your message history\nEnter your first message: ");
            while (true)
            {
                try
                {
                    string message = Console.ReadLine();
                    buffer = Encoding.UTF8.GetBytes(message);
                    await client.SendAsync(buffer, buffer.Length, serverEndpoint);

                    if (message == "/quit")
                    {
                        break;
                    }
                    else if (message.StartsWith("/file"))
                    {
                        string[] command = message.Split(' ', 3);
                        if (command.Length >= 3)
                        {
                            string targetUser = command[1];
                            string path = command[2];
                            if (File.Exists(path))
                            {
                                string fileName = Path.GetFileName(path);
                                byte[] msgBuf = Encoding.UTF8.GetBytes($"/file {targetUser} {fileName}");
                                await client.SendAsync(msgBuf, msgBuf.Length, serverEndpoint);
                                SendFile(path);
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.Message);
                }
            }
            client.Close();
        }

        static async Task ReceiveMessageAsync()
        {
            try
            {
                while (true)
                {
                    UdpReceiveResult result = await client.ReceiveAsync();
                    string received = Encoding.UTF8.GetString(result.Buffer, 0, result.Buffer.Length);
                    if (received.StartsWith("file:"))
                    {
                        string fileName = received.Substring(5).Trim();
                        ReceiveFile(fileName, 5002);
                    }
                    else
                    {
                        Console.WriteLine(received);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
        }

        private static void ReceiveFile(string path, int port)
        {
            using (UdpClient client = new UdpClient(port))
            {
                IPEndPoint iPEndPoint = new IPEndPoint(IPAddress.Any, port);

                byte[] buffer_size = client.Receive(ref iPEndPoint);
                int file_size = BitConverter.ToInt32(buffer_size, 0);
                Console.WriteLine($"Got size file: {file_size}");

                byte[] file_buffer = new byte[file_size];
                int receivedBytes = 0;
                while (receivedBytes < file_size)
                {
                    byte[] part_buffer = client.Receive(ref iPEndPoint);
                    Array.Copy(part_buffer, 0, file_buffer, receivedBytes, part_buffer.Length);
                    receivedBytes += part_buffer.Length;
                    Console.WriteLine($"Got: {receivedBytes} - {file_size}");
                }

                File.WriteAllBytes(path, file_buffer);
                Console.WriteLine($"File saved successfully at {path}");
            }
        }







        private static void SendFile(string path)
        {
            const int size_buffer = 8192;
            using (UdpClient client = new UdpClient())
            {
                IPEndPoint serverEndPoint = new IPEndPoint(IPAddress.Parse("127.0.0.1"), 5001); // Порт сервера для приема файлов
                byte[] file_buffer = File.ReadAllBytes(path);
                Console.WriteLine($"Sending file of size: {file_buffer.Length} bytes");
                client.Send(BitConverter.GetBytes(file_buffer.Length), 4, serverEndPoint); // Отправляем размер файла

                for (int i = 0; i < file_buffer.Length; i += size_buffer)
                {
                    int part_size = Math.Min(size_buffer, file_buffer.Length - i);
                    byte[] buffer_part = new byte[part_size];
                    Array.Copy(file_buffer, i, buffer_part, 0, part_size);
                    client.Send(buffer_part, part_size, serverEndPoint);
                    Console.WriteLine($"Sent: {i + part_size} - {file_buffer.Length} bytes...");
                }
            }
        }




    }
}
