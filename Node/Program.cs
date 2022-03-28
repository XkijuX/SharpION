using System;
using System.Net;
using System.Net.Sockets;
using System.Linq;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Net.NetworkInformation;
using System.Security.Cryptography;
using System.Threading.Tasks;
using System.Text;

namespace NettworkProg
{
    class PostNode
    {
        public string Ip { get; set; }
        public string Key { get; set; }
    }
    class Program
    {
        async static Task<HttpResponseMessage> SendRequestAsync(StringContent content)
        {
            using HttpClient httpClient = new HttpClient();
            return await httpClient.PostAsync("http://localhost:8080/postnode", content);
        }

        static void Main(string[] args)
        {
            Random rand = new Random();
            int port = rand.Next(4001, 4999);
            Console.WriteLine("Running on port: " + port);

            // Create a PublicKey
            RSACryptoServiceProvider rsa = new RSACryptoServiceProvider();
            byte[] pubkey = rsa.ExportRSAPublicKey();

            // Send Public key to Directory Server
            String address = "127.0.0.1:" + port;
            String keyAsBase64 = Convert.ToBase64String(pubkey);
            StringContent content = new StringContent("{\"pubkey\": \"" + keyAsBase64 + "\", \"address\": \"" + address + "\"}", Encoding.UTF8, "application/json");
            Task<HttpResponseMessage> task = SendRequestAsync(content);

            // Wait for api response
            task.Wait();
            HttpResponseMessage res = task.Result;

            // Check for http status
            if (res.StatusCode != HttpStatusCode.OK) throw new Exception("Something went wrong. Could not post new node to the directory node");

            // Start Tcp Listener
            TcpListener tcpListener = new TcpListener(IPAddress.Parse("127.0.0.1"), port);
            tcpListener.Start();

            while (true)
            {
                Console.Write("Waiting for a connection... ");

                // Accept new connection
                TcpClient client = tcpListener.AcceptTcpClient();

                // Create thread and start it
                NodeThread nodeThread = new NodeThread(client, rsa);
                Thread thread = new Thread(new ThreadStart(nodeThread.StartTunnel));
                thread.Start();

                Console.WriteLine("Accepted!");
            }
        }

    }
    class NodeThread
    {
        TcpClient tcpClient;
        RSACryptoServiceProvider rsa;
        byte[] aesKey;

        public NodeThread(TcpClient client, RSACryptoServiceProvider rsa)
        {
            this.tcpClient = client;
            this.rsa = rsa;
        }


        /// <summary>
        /// Method that joins two arrays
        /// </summary>
        /// <param name="arr1">Array 1</param>
        /// <param name="arr2">Array 2</param>
        /// <returns>One array containing both arrays inserted</returns>
        public static byte[] JoinArrays(byte[] arr1, byte[] arr2)
        {
            byte[] bytes = new byte[arr1.Length + arr2.Length];
            for (int i = 0; i < arr1.Length; i++)
            {
                bytes[i] = arr1[i];
            }
            for (int i = 0; i < arr2.Length; i++)
            {
                bytes[i + arr1.Length] = arr2[i];
            }
            return bytes;
        }

        /// <summary>
        /// Method to encrypt data
        /// </summary>
        /// <param name="bytes"></param>
        /// <param name="key"></param>
        /// <returns>Encrypted data</returns>
        private byte[] EncryptData(byte[] bytes, byte[] key)
        {
            byte[] encrypted;

            // Create aes instance
            AesManaged aes = CreateAas(key);

            using (MemoryStream ms = new MemoryStream())
            {
                using CryptoStream cryptoStream = new CryptoStream(ms, aes.CreateEncryptor(), CryptoStreamMode.Write);
                cryptoStream.Write(bytes, 0, bytes.Length);
                cryptoStream.FlushFinalBlock();
                encrypted = ms.ToArray();
            }
            return JoinArrays(aes.IV, encrypted);
        }

        /// <summary>
        /// Method to create a instance of the AesManaged class
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
        private AesManaged CreateAas(byte[] key)
        {
            AesManaged aes = new AesManaged();
            aes.Mode = CipherMode.CFB;
            aes.Padding = PaddingMode.Zeros;
            aes.Key = key;
            aes.GenerateIV();

            return aes;
        }

        /// <summary>
        /// Creates a AesManaged with IV
        /// </summary>
        /// <param name="key">AES key</param>
        /// <returns>AesManaged object</returns>
        private AesManaged CreateAas(byte[] key, byte[] iv)
        {
            AesManaged aes = CreateAas(key);
            aes.IV = iv;
            return aes;
        }

        /// <summary>
        /// Method that watches a connection between two servers and passes data through the tunnel.
        /// </summary>
        /// <param name="networkStream"></param>
        /// <param name="stream"></param>
        /// <param name="tcpClient"></param>
        public void RunTunnel(NetworkStream networkStream, NetworkStream stream, TcpClient tcpClient)
        {
            // Liste to data
            while (true)
            {
                if (networkStream.DataAvailable)
                {
                    byte[] buffer = new byte[8192];
                    int bytesRead;
                    int k = 0;
                    MemoryStream ms = new MemoryStream();
                    lp:
                    while (networkStream.DataAvailable && (bytesRead = networkStream.Read(buffer)) > 0)
                    {
                        ms.Write(new ArraySegment<byte>(buffer, 0, bytesRead).ToArray());
                        k += bytesRead;
                    }

                    // Sleep and wait for more data
                    Thread.Sleep(200);
                    if (networkStream.DataAvailable) goto lp;

                    // Encrypt the data send in the network stream
                    byte[] res = ms.ToArray();
                    res = EncryptData(res, this.aesKey);

                    // Send data
                    stream.Write(res, 0, res.Length);
                    stream.Flush();
                    Console.WriteLine("Sending Package - " + k);
                }

                if (stream.DataAvailable)
                {
                    byte[] buffer = new byte[8192];
                    int bytesRead;
                    while (stream.DataAvailable && (bytesRead = stream.Read(buffer)) > 0)
                    {
                        byte[] trimmedBytes = new ArraySegment<byte>(buffer, 0, bytesRead).ToArray();
                        String data = System.Text.Encoding.UTF8.GetString(trimmedBytes, 0, trimmedBytes.Length);

                        if(!data.StartsWith("2#"))
                        {

                            byte[] iv = new ArraySegment<byte>(trimmedBytes, 0, 16).ToArray();
                            trimmedBytes = new ArraySegment<byte>(trimmedBytes, iv.Length, trimmedBytes.Length - iv.Length).ToArray();
                            AesManaged aes = CreateAas(aesKey, iv);

                            // Decrypt Data
                            byte[] bytesUnenCrypted;
                            buffer = new ArraySegment<byte>(buffer, 0, bytesRead).ToArray();
                            using MemoryStream resMemoryStream = new MemoryStream();
                            using (MemoryStream ms = new MemoryStream(trimmedBytes))
                            {
                                using (CryptoStream cryptoStream = new CryptoStream(ms, aes.CreateDecryptor(), CryptoStreamMode.Read))
                                {
                                    cryptoStream.CopyTo(resMemoryStream);
                                    bytesUnenCrypted = resMemoryStream.ToArray();
                                }
                            }
                            trimmedBytes = bytesUnenCrypted;
                            data = System.Text.Encoding.UTF8.GetString(bytesUnenCrypted, 0, bytesUnenCrypted.Length);
                        }

                        // Check for new key exchange
                        if (data.StartsWith("2#"))
                        {
                            int Encryptionlenght = int.Parse(data.Split("#")[1].Split(":")[0]);
                            int KeyLength = int.Parse(data.Split("#")[1].Split(":")[1]);
                            byte[] startBytes = Encoding.UTF8.GetBytes("2#" + Encryptionlenght.ToString() + ":" + KeyLength.ToString() + "#");
                            byte[] firstHalf = new ArraySegment<byte>(buffer, startBytes.Length, Encryptionlenght).ToArray();
                            byte[] key = new ArraySegment<byte>(buffer, startBytes.Length + Encryptionlenght, KeyLength).ToArray();
                            firstHalf = rsa.Decrypt(firstHalf, false);

                            // Generate first part of public key
                            byte[] secondHalf = new byte[8];
                            RNGCryptoServiceProvider random = new RNGCryptoServiceProvider();
                            random.GetBytes(secondHalf);

                            RSACryptoServiceProvider rsa1 = new RSACryptoServiceProvider();
                            rsa1.ImportRSAPublicKey(key, out int number);
                            byte[] res = rsa1.Encrypt(secondHalf, false);
                            stream.Write(res);
                            aesKey = JoinArrays(firstHalf, secondHalf);
                            continue;
                        }

                        TunnelConnection tunnelConnection = TunnelConnection.Parse(data.Split("\r\n")[0]);

                        // Close old connection that was used before this.
                        if (tunnelConnection.GetPackageType() == PackageTypeEnum.END)
                        {
                            networkStream.Close();
                            tcpClient.Close();
                            networkStream.Dispose();
                            tcpClient.Dispose();
                        }

                        String[] lines = data.Split("\r\n");
                        int extra = "\r\n".ToCharArray().Length;

                        byte[] layer = new ArraySegment<byte>(trimmedBytes, lines[0].Length + extra, trimmedBytes.Length - lines[0].Length - extra).ToArray();


                        if (tunnelConnection.GetPackageType() == PackageTypeEnum.END)
                        {
                            tcpClient = new TcpClient(tunnelConnection.GetUrl(), int.Parse(tunnelConnection.GetPort()));
                            networkStream = tcpClient.GetStream();
                        }

                        networkStream.Write(layer, 0, layer.Length);
                        networkStream.Flush();
                        this.RunTunnel(networkStream, stream, tcpClient);
                        return;
                    }

                }
                Thread.Sleep(50);
            }
        }

        /// <summary>
        /// Method ran to initialize the tunnel
        /// </summary>
        public void StartTunnel()
        {
            Byte[] bytes = new Byte[4096];
            String data;
            NetworkStream stream = this.tcpClient.GetStream();
            TcpClient tcpClient;
            NetworkStream networkStream;

            // First connection through tunnel
            int i;
            while ((i = stream.Read(bytes, 0, bytes.Length)) != 0)
            {
                byte[] trimmedBytes = new ArraySegment<byte>(bytes, 0, i).ToArray();

                if (this.aesKey is not null)
                {
                    byte[] bytesUnenCrypted;
                    byte[] iv = new ArraySegment<byte>(trimmedBytes, 0, 16).ToArray();
                    trimmedBytes = new ArraySegment<byte>(trimmedBytes, iv.Length, trimmedBytes.Length - iv.Length).ToArray();
                    AesManaged aes = CreateAas(this.aesKey, iv);

                    // Decrypt data
                    using MemoryStream resMemoryStream = new MemoryStream();
                    using (MemoryStream ms = new MemoryStream(trimmedBytes))
                    {
                        using CryptoStream cryptoStream = new CryptoStream(ms, aes.CreateDecryptor(), CryptoStreamMode.Read);
                        cryptoStream.CopyTo(resMemoryStream);
                        bytesUnenCrypted = resMemoryStream.ToArray();
                    }
                    trimmedBytes = bytesUnenCrypted;
                    data = System.Text.Encoding.UTF8.GetString(bytesUnenCrypted, 0, bytesUnenCrypted.Length);
                } else
                {
                    data = System.Text.Encoding.UTF8.GetString(trimmedBytes, 0, i);
                }
               
                // Check if the new connection requires KeyExchange
                if (data.StartsWith("2#"))
                {
                    int Encryptionlenght = int.Parse(data.Split("#")[1].Split(":")[0]);
                    int KeyLength = int.Parse(data.Split("#")[1].Split(":")[1]);
                    byte[] startBytes = Encoding.UTF8.GetBytes("2#" + Encryptionlenght.ToString() + ":" + KeyLength.ToString() + "#");

                    // Extract and decrypt first half of the session key
                    byte[] firstHalf = new ArraySegment<byte>(bytes, startBytes.Length, Encryptionlenght).ToArray();
                    byte[] key = new ArraySegment<byte>(bytes, startBytes.Length + Encryptionlenght, KeyLength).ToArray();
                    firstHalf = rsa.Decrypt(firstHalf, false);

                    // Generate second part of public key
                    byte[] secondHalf = new byte[8];
                    RNGCryptoServiceProvider random = new RNGCryptoServiceProvider();
                    random.GetBytes(secondHalf);

                    RSACryptoServiceProvider rsa1 = new RSACryptoServiceProvider();
                    rsa1.ImportRSAPublicKey(key, out int number);
                    byte[] res = rsa1.Encrypt(secondHalf, false);
                    this.aesKey = JoinArrays(firstHalf, secondHalf);
                    stream.Write(res);
                    stream.Flush();
                    continue;
                }

                // Create tunnel class
                TunnelConnection tunnelConnection = TunnelConnection.Parse(data.Split("\r\n")[0]);

                String[] lines = data.Split("\r\n");
                int extra = "\r\n".ToCharArray().Length;

                // Remove data added by the last node
                byte[] layer = new ArraySegment<byte>(trimmedBytes, lines[0].Length + extra, trimmedBytes.Length - lines[0].Length - extra).ToArray();

                // Create new connection
                tcpClient = new TcpClient(tunnelConnection.GetUrl(), int.Parse(tunnelConnection.GetPort()));
                networkStream = tcpClient.GetStream();

                networkStream.Write(layer, 0, layer.Length);
                networkStream.Flush();

                // Watch tunnel
                this.RunTunnel(networkStream, stream, tcpClient);
                return;
            }
        }
    }

    /// <summary>
    /// Class that represents a tunnel connection. This class checks the information added by other nodes.
    /// </summary>
    class TunnelConnection
    {
        private String url;
        private String port;
        private PackageTypeEnum packageType;
        public static TunnelConnection Parse(String tunnel)
        {
            TunnelConnection tunnelConnection = new TunnelConnection();
            String[] connections = tunnel.Split("<!!>");
            String[] next = connections[0].Split("#");
            tunnelConnection.packageType = (PackageTypeEnum)Enum.Parse(typeof(PackageTypeEnum), next[0]);
            String[] urlAndPort = next[1].Split(":");

            if (tunnelConnection.packageType == PackageTypeEnum.END && connections[1] == "DNS")
            {
                IPHostEntry hostEntry = Dns.GetHostEntry(urlAndPort[0]);
                tunnelConnection.url = hostEntry.AddressList.FirstOrDefault().ToString();
                tunnelConnection.port = urlAndPort[1];
            }
            else
            {
                tunnelConnection.url = urlAndPort[0];
                tunnelConnection.port = urlAndPort[1];
            }

            return tunnelConnection;
        }

        public String GetUrl()
        {
            return this.url;
        }

        public String GetPort()
        {
            return this.port;
        }

        public PackageTypeEnum GetPackageType()
        {
            return this.packageType;
        }
    }
}

/*
 * Enum used for the header that says where and what the package sent is used for
 * End = 0 -> Used to identify end of tunnel
 * Tunnel = 1 -> Used to identify that the package next destination is further in the tunnel
 * Key = 2 -> Used to tell the node that the package is for key exchange
 */
enum PackageTypeEnum
{
    END = 0,
    TUNNEL = 1,
    KEY = 2,
}
