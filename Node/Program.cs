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
        async static Task<HttpResponseMessage> SendRequestAsync(StringContent content )
        {
            using HttpClient httpClient = new HttpClient();
            return await httpClient.PostAsync("http://localhost:8080/postnode", content);
        }
        
        static void Main(string[] args)
        {
            try
            {
                Random rand = new Random();
                int port = rand.Next(4001, 4999);
                Console.WriteLine(port);
                TcpListener tcpListener = new TcpListener(IPAddress.Parse("127.0.0.1"), port);

                // Create a PublicKey
                RSACryptoServiceProvider rsa = new RSACryptoServiceProvider();
                //RSAParameters keyParms = rsa.ExportParameters(false);
                //byte[] pubkey = keyParms.Modulus;
                //var cert = new X509Certificate2();
                //byte[]pubkey = cert.GetRSAPublicKey();
                
                byte[] pubkey = rsa.ExportRSAPublicKey();

                RSACryptoServiceProvider rsa2 = new RSACryptoServiceProvider();
                int bytesread;
                rsa2.ImportRSAPublicKey(Convert.FromBase64String(Convert.ToBase64String(pubkey)), out bytesread);

                //byte[] text = System.Text.Encoding.Unicode.GetBytes("Dette er en test for å se at RSA fungerer :)");

                //byte[] encrypted = rsa2.Encrypt(text, false);

                //byte[] textBytes = rsa.Decrypt(encrypted, false);

                //Console.WriteLine(System.Text.Encoding.Unicode.GetString(textBytes));

                // Register node on Directory Server
                String address = "127.0.0.1:" + port;
                String keyAsBase64 = Convert.ToBase64String(pubkey);
                StringContent content = new StringContent("{\"pubkey\": \"" + keyAsBase64 + "\", \"address\": \"" + address + "\"}", Encoding.UTF8, "application/json");
                Task<HttpResponseMessage> task = SendRequestAsync(content);
                task.Wait();
                HttpResponseMessage res = task.Result;
                if (res.StatusCode != HttpStatusCode.OK) throw new Exception("Something went wrong. Could not post new node to the directory node");
                
                tcpListener.Start();
                
                while (true)
                {
                    Console.Write("Waiting for a connection... ");

                    TcpClient client = tcpListener.AcceptTcpClient();
                    // Create thread and run it
                    NodeThread nodeThread = new NodeThread(client, rsa);
                    Thread thread = new Thread(new ThreadStart(nodeThread.RunThread));
                    thread.Start();

                    Console.WriteLine("Accepted!");
                }
                
            } catch (Exception e)
            {
                Console.WriteLine(e); //e.StackTrace
            }
        }
        
    }
    class NodeThread
    {
        TcpClient tcpClient;
        RSACryptoServiceProvider rsa;
        byte[] aesKey;

        public NodeThread (TcpClient client,RSACryptoServiceProvider rsa)
        {
            this.tcpClient = client;
            this.rsa = rsa;
        }

        public static TcpState CheckState(TcpClient tcpClient)
        {
            return IPGlobalProperties
                .GetIPGlobalProperties()
                .GetActiveTcpConnections()
                .Where(con => con.LocalEndPoint.Equals(tcpClient.Client.LocalEndPoint) && con.RemoteEndPoint.Equals(tcpClient.Client.RemoteEndPoint))
                .FirstOrDefault()
                .State;
        }

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

        private byte[] encryptData(byte[] bytes, byte[] key)
        {
            byte[] encrypted;

            // Create aes instance
            AesManaged aes = new AesManaged();
            aes.Mode = CipherMode.CFB;
            aes.Padding = PaddingMode.Zeros;
            aes.Key = key;
            aes.IV = new byte[] { 0xE0, 0x4F, 0xD0, 0x20, 0xEA, 0x3A, 0x69, 0x10, 0xA2, 0xD8, 0x8, 0x0, 0x2B, 0x30, 0x30, 0x9D };

            using (MemoryStream ms = new MemoryStream())
            {
                using (CryptoStream cryptoStream = new CryptoStream(ms, aes.CreateEncryptor(), CryptoStreamMode.Write))
                {
                    cryptoStream.Write(bytes, 0, bytes.Length);
                    cryptoStream.FlushFinalBlock();
                    encrypted = ms.ToArray();
                }
            }
            Console.WriteLine("Encrypted: " + bytes.Length + " - " + encrypted.Length);
            return encrypted;
        }

        public void RunThread1(NetworkStream networkStream, NetworkStream stream, TcpClient tcpClient)
        {
            while (true)
            {
                if (networkStream.DataAvailable)
                {
                    byte[] buffer = new byte[8192];
                    int bytesRead;
                    int k = 0 ;
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

                    byte[] res = ms.ToArray();
                    res = encryptData(res, this.aesKey);
                    //Console.WriteLine("Sending Encrypted Data");
                    stream.Write(res, 0, res.Length);
                    stream.Flush();
                    //Thread.Sleep(100);
                    Console.WriteLine("Sending Package - " + k);
                }
                if (stream.DataAvailable)
                {
                    byte[] buffer = new byte[8192];
                    int bytesRead;
                    Console.WriteLine("Data!");
                    while (stream.DataAvailable && (bytesRead = stream.Read(buffer)) > 0)
                    {
                        String data = System.Text.Encoding.UTF8.GetString(buffer, 0, bytesRead);

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

                        AesManaged aes = new AesManaged();
                        aes.Mode = CipherMode.CFB;
                        aes.Padding = PaddingMode.Zeros;
                        aes.Key = aesKey;
                        aes.IV = new byte[] { 0xE0, 0x4F, 0xD0, 0x20, 0xEA, 0x3A, 0x69, 0x10, 0xA2, 0xD8, 0x8, 0x0, 0x2B, 0x30, 0x30, 0x9D };


                        byte[] plainText = new ArraySegment<byte>(buffer, lines[0].Length + extra, bytesRead - lines[0].Length - extra).ToArray();

                        String UnenCrypted;
                        byte[] bytesUnenCrypted;
                        using MemoryStream resMemoryStream = new MemoryStream();
                        using (MemoryStream ms = new MemoryStream(plainText))
                        {
                            using (CryptoStream cryptoStream = new CryptoStream(ms, aes.CreateDecryptor(), CryptoStreamMode.Read))
                            {
                                cryptoStream.CopyTo(resMemoryStream);
                                bytesUnenCrypted = resMemoryStream.ToArray();
                                using (StreamReader srDecrypt = new StreamReader(cryptoStream))
                                {
                                    UnenCrypted = srDecrypt.ReadToEnd();
                                }
                            }
                        }

                        if (tunnelConnection.GetPackageType() == PackageTypeEnum.END)
                        {
                            tcpClient = new TcpClient(tunnelConnection.GetUrl(), int.Parse(tunnelConnection.GetPort()));
                            networkStream = tcpClient.GetStream();
                        }

                        networkStream.Write(bytesUnenCrypted, 0, bytesUnenCrypted.Length);
                        networkStream.Flush();
                        this.RunThread1(networkStream, stream, tcpClient);
                        return;
                    }
                    
                }
                Thread.Sleep(50);
                // Clean up
                /*
                if (CheckState(tcpClient) != TcpState.Established || CheckState(this.tcpClient) != TcpState.Established)
                {
                    await stream.FlushAsync();
                    networkStream.Close();
                    stream.Close();
                    tcpClient.Close();
                    this.tcpClient.Close();
                    networkStream.Dispose();
                    stream.Dispose();
                    tcpClient.Dispose();
                    this.tcpClient.Dispose();
                    Console.WriteLine("Closing connection");
                    break;
                }
                */
            }
        }

        public void RunThread()
        {
            Byte[] bytes = new Byte[4096];
            String data;
            NetworkStream stream = this.tcpClient.GetStream();
            TcpClient tcpClient;
            NetworkStream networkStream;

            // First connection through tunnel
            int i;
            //MemoryStream ms = new MemoryStream();
            while ((i = stream.Read(bytes, 0, bytes.Length)) != 0)
            {
                data = System.Text.Encoding.UTF8.GetString(bytes, 0, i);
                
                if(data.StartsWith("2#"))
                {
                    int Encryptionlenght = int.Parse(data.Split("#")[1].Split(":")[0]);
                    int KeyLength = int.Parse(data.Split("#")[1].Split(":")[1]);
                    byte[] startBytes = Encoding.UTF8.GetBytes("2#" + Encryptionlenght.ToString() + ":" + KeyLength.ToString() + "#");
                    //byte[] pubkey = new ArraySegment<byte>(bytes, startBytes.Length, 128).ToArray();
                    byte[] firstHalf = new ArraySegment<byte>(bytes, startBytes.Length, Encryptionlenght).ToArray();
                    byte[] key = new ArraySegment<byte>(bytes, startBytes.Length + Encryptionlenght, KeyLength).ToArray();
                    firstHalf = rsa.Decrypt(firstHalf, false);

                    // Generate first part of public key
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

                // Parse headers and data
                TunnelConnection tunnelConnection = TunnelConnection.Parse(data.Split("\r\n")[0]);

                AesManaged aes = new AesManaged();              
                aes.Mode = CipherMode.CFB;
                aes.Padding = PaddingMode.Zeros;
                aes.Key = this.aesKey;
                aes.IV = new byte[] { 0xE0, 0x4F, 0xD0, 0x20, 0xEA, 0x3A, 0x69, 0x10, 0xA2, 0xD8, 0x8, 0x0, 0x2B, 0x30, 0x30, 0x9D };

                String[] lines = data.Split("\r\n");
                int extra = "\r\n".ToCharArray().Length;
                byte[] plainText = new ArraySegment<byte>(bytes, lines[0].Length + extra, i - lines[0].Length - extra).ToArray();

                byte[] bytesUnenCrypted;
                using MemoryStream resMemoryStream = new MemoryStream();
                using (MemoryStream ms = new MemoryStream(plainText))
                {
                    using (CryptoStream cryptoStream = new CryptoStream(ms, aes.CreateDecryptor(), CryptoStreamMode.Read))
                    {
                        cryptoStream.CopyTo(resMemoryStream);
                        bytesUnenCrypted = resMemoryStream.ToArray();
                    }
                }

                // Create new connection
                tcpClient = new TcpClient(tunnelConnection.GetUrl(), int.Parse(tunnelConnection.GetPort()));
                networkStream = tcpClient.GetStream();
                
                networkStream.Write(bytesUnenCrypted, 0, bytesUnenCrypted.Length);
                networkStream.Flush();
                this.RunThread1(networkStream, stream, tcpClient);
                Thread.Sleep(50);
                return;
            }
        }
    }

    class TunnelConnection
    {
        private String url;
        private String port;
        private PackageTypeEnum packageType;
        public static TunnelConnection Parse(String tunnel)
        {
            Console.WriteLine("------------");
            Console.WriteLine(tunnel);
            Console.WriteLine("------------");
            TunnelConnection tunnelConnection = new TunnelConnection();
            String[] connections = tunnel.Split("<!!>");
            String[] next = connections[0].Split("#");
            tunnelConnection.packageType = (PackageTypeEnum) Enum.Parse(typeof (PackageTypeEnum), next[0]);
            String[] urlAndPort = next[1].Split(":");

            if(tunnelConnection.packageType == PackageTypeEnum.END && connections[1] == "DNS")
            {
                IPHostEntry hostEntry = Dns.GetHostEntry(urlAndPort[0]);
                tunnelConnection.url = hostEntry.AddressList.FirstOrDefault().ToString();
                tunnelConnection.port = urlAndPort[1];
            } else
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

enum PackageTypeEnum
{
    END = 0,
    TUNNEL = 1,
    KEY = 2,
}
