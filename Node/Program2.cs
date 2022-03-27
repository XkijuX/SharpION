using System;
using System.Net;
using System.Net.Sockets;
using System.Linq;
using System.IO;
using System.Threading;
using System.Net.NetworkInformation;
using System.Threading.Tasks;
using System.Buffers;

namespace NettworkProg
{
    class Program2
    {
        
        public static void MainABC(string[] args)
        {
            int port = 4001;
            IPAddress nodeIP = IPAddress.Parse("127.0.0.1");
            TcpListener node = new TcpListener(nodeIP, port);

            node.Start();

            while(true)
            {
                Console.WriteLine("Waiting for a connection...");
                TcpClient client = node.AcceptTcpClient();
                NetworkStream clientStream = client.GetStream();
                Console.WriteLine("Accepted connection!");
               
                // Setup Tunnel                
                Task.Run(async () =>
                {
                    // Get inital connection properties
                    Byte[] bytes = new Byte[8192];
                    int i = await clientStream.ReadAsync(bytes, 0, bytes.Length).ConfigureAwait(false);
                    String data = System.Text.Encoding.UTF8.GetString(bytes, 0, i);
                    Console.WriteLine(data);
                    String[] url = data.Split("<!!>")[0].Split(":");
                    String[] s = data.Split("<!!>");
                    String newMsg = String.Join("<!!>", new ArraySegment<string>(s, 1, s.Length - 1).ToArray());
                    if (newMsg.StartsWith("DNS:"))
                    {
                        Uri uri = new Uri(newMsg.Split("<!!>")[1]);
                        IPHostEntry hostEntry = Dns.GetHostEntry(uri.DnsSafeHost);
                        newMsg = "GET " + uri.PathAndQuery + " HTTP/1.1\r\nHost: " + uri.Host + "\r\n" + String.Join("<!!>", new ArraySegment<string>(s, 3, s.Length - 3).ToArray());//Cache-Control: no-cache\r\nAccept-Encoding: gzip, deflate\r\nAccept: text/html,application/xhtml+xml,application/xml;q=0.9,image/webp,image/apng,*/*;q=0.8,application/signed-exchange;v=b3;q=0.9\r\n\r\n";
                        Console.WriteLine(newMsg);
                        url[0] = hostEntry.AddressList.FirstOrDefault().ToString();
                        url[1] = uri.Port.ToString();
                    };

                    TcpClient server = new TcpClient(url[0], int.Parse(url[1]));
                    NetworkStream serverStream = server.GetStream();
                    Byte[] nextMsg = System.Text.Encoding.UTF8.GetBytes(newMsg.ToCharArray());
                    serverStream.Write(nextMsg, 0, nextMsg.Length);

                    // After setup start thread
                    ConnectionThread connectionThread = new ConnectionThread(client, clientStream, server, serverStream);
                    Thread thread = new Thread(new ThreadStart(connectionThread.Run));
                    thread.Start();
                });
            }

        }
    }
    class ConnectionThread
    {
        TcpClient client;
        TcpClient server;
        NetworkStream clientStream;
        NetworkStream serverStream;
        CancellationTokenSource cancellationTokenSource;

        public ConnectionThread(TcpClient client, NetworkStream clientStream, TcpClient server, NetworkStream serverStream)
        {
            this.client = client;
            this.clientStream = clientStream;
            this.server = server;
            this.serverStream = serverStream;
            this.cancellationTokenSource = new CancellationTokenSource();
        }

        public async void Run ()
        {
            using (this.cancellationTokenSource.Token.Register(() =>
            {
                serverStream.Close();
                clientStream.Close();
            }, true)) {
                byte[] buffer = ArrayPool<byte>.Shared.Rent(16384);
                try
                {
                    while (true)
                    {
                        int bytesRead = await this.serverStream.ReadAsync(new Memory<byte>(buffer), this.cancellationTokenSource.Token).ConfigureAwait(false);
                        if (bytesRead == 0) break;
                        await clientStream.WriteAsync(new ReadOnlyMemory<byte>(buffer, 0, bytesRead), this.cancellationTokenSource.Token).ConfigureAwait(false);
                        Console.WriteLine("Writeing - " + bytesRead);
                    }
                    await clientStream.FlushAsync().ConfigureAwait(false);
                    serverStream.Close();
                    clientStream.Close();
                    Console.WriteLine("Closed Connection");
                }
                finally
                {
                    ArrayPool<byte>.Shared.Return(buffer);
                }
            }
        }
    }
}
