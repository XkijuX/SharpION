using System;
using System.Net;
using System.Net.Sockets;
using System.Linq;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Security.Cryptography;
using System.Threading.Tasks;
using System.Text;
using Newtonsoft.Json.Linq;
using System.Collections.Generic;

namespace ClientOnionRouting
{
    internal class Program
    {
        /// <summary>
        /// Create a request to the spesific url and returns a task
        /// </summary>
        /// <param name="url"></param>
        /// <returns>Task that returns the response content</returns>
        async static Task<string> SendRequestAsync(String url)
        {
            using HttpClient httpClient = new HttpClient();
            await httpClient.GetAsync(url);
            using HttpResponseMessage response = await httpClient.GetAsync(url);
            return await response.Content.ReadAsStringAsync();
        }

        static void Main(string[] args)
        {
            // Start Server on port 3000
            TcpListener tcpListener = new TcpListener(IPAddress.Parse("127.0.0.1"), 3000);
            tcpListener.Start();

            // Start waiting for connections
            while(true)
            {
                Console.WriteLine("Waiting for a connection... ");
                TcpClient client = tcpListener.AcceptTcpClient();
                Console.WriteLine("New Connection Accepted");

                // Get nodes from directory server.
                Task<String> apiTask = SendRequestAsync("http://localhost:8080/");
                apiTask.Wait();

                // Convert response to list of nodes
                String response = apiTask.Result;
                JArray nodesList = JArray.Parse(response);
                TunnelNode[] nodes = new TunnelNode[nodesList.Count];
                for(int i = 0; nodesList.Count > i; i++) nodes[i] = nodesList[i].ToObject<TunnelNode>();
               

                // Start a new thread that runs the program
                ConnectionHandler connectionHandler = new ConnectionHandler(client, nodes);
                Thread thread = new Thread(new ThreadStart(connectionHandler.Run));
                thread.Start();
            }
        }
    }

    /// <summary>
    /// Class that manages the tunnel connection and read.
    /// </summary>
    class ConnectionHandler
    {
        TcpClient fromClient;
        TunnelNode[] nodes;

        public ConnectionHandler(TcpClient fromClient, TunnelNode[] nodes)
        {
            this.fromClient = fromClient;
            this.nodes = nodes;
        }

        public void Run()
        {
            // Connect to next node
            String firstNode = nodes[0].Address;
            String[] addresstAndPort = firstNode.Split(":");
            TcpClient nextNode = new TcpClient(addresstAndPort[0], int.Parse(addresstAndPort[1])); // Fix the parse stuff
            
            NetworkStream nextNetworkStream = nextNode.GetStream();
            NetworkStream fromNetworkStream = fromClient.GetStream();

            // Establish tunnel
            for(int i = 0; nodes.Length > i; i++)
            {
                TunnelNode node = nodes[i];
                // Create RSA provider and set public key
                RSACryptoServiceProvider nodeRSA = new RSACryptoServiceProvider();
                int bytesread;
                nodeRSA.ImportRSAPublicKey(Convert.FromBase64String(node.Pubkey), out bytesread);

                // Create RSA provider and generate key
                RSACryptoServiceProvider clientRSA = new RSACryptoServiceProvider();
                byte[] clientPublicKey = clientRSA.ExportRSAPublicKey();

                // Generate first part of public key
                byte[] firstHalf = new byte[8];
                RNGCryptoServiceProvider random = new RNGCryptoServiceProvider();
                random.GetBytes(firstHalf);

                byte[] encrypted = nodeRSA.Encrypt(firstHalf, false);

                // Add 2# and pubkey
                byte[] keyAndEncrypt = ConcatArray(Encoding.UTF8.GetBytes("2#" + encrypted.Length + ":" + clientPublicKey.Length + "#"), encrypted);
                byte[] message = ConcatArray(keyAndEncrypt, clientPublicKey);
                
                for(int j = 0; i > j; j++)
                {
                    //Encrypt Data
                    byte[] encryptedDataLayer = EncryptData(message, nodes[i - j - 1].AesKey);
                    message = ConcatArray(Encoding.UTF8.GetBytes("1#" + nodes[i - j ].Address + "\r\n"), encryptedDataLayer);
                }
                
                // Send to next node
                nextNetworkStream.Write(message, 0, message.Length);
                nextNetworkStream.Flush();
                byte[] encryptedResponse = new byte[1024];

                // Wait and read response to byte array
                Console.WriteLine("Thread: " + Thread.GetCurrentProcessorId() + " Waiting on key response.");
                int resSize = nextNetworkStream.Read(encryptedResponse, 0, encryptedResponse.Length);
                encryptedResponse = new ArraySegment<byte>(encryptedResponse,  0,resSize).ToArray();

                // Decrypt layers
                for (int j = 0; (i - 1) >= j; j++)
                {
                    encryptedResponse = DecryptData(encryptedResponse, nodes[j].AesKey);
                }
                
                // Decrypt the second part of the key
                byte[] secondHalf = clientRSA.Decrypt(new ArraySegment<byte>(encryptedResponse, 0, resSize).ToArray(), false);

                // Save key to node class
                node.AesKey = ConcatArray(firstHalf, secondHalf);
                Console.WriteLine("Thread: " + Thread.GetCurrentProcessorId() + " AES key exchange completed.");
            }

            // Listen data sent in the tunnel
            while(nextNode.Connected)
            {
                // Listen to data sent from proxy connection
                if(fromNetworkStream.DataAvailable)
                {
                    String res = ReadDataString(fromNetworkStream);
                    
                    // Create HttpConnection class
                    HttpConnection httpConnection = HttpConnection.Parse(res);

                    // Deny Connect Requests (not supported)
                    if (httpConnection.method.Equals("Connect")) return;
                    
                    // Get Http Request Header
                    byte[] httpHeader = Encoding.UTF8.GetBytes(httpConnection.getFullHeader());

                    // Encrypt data
                    byte[] encryptedData = EncryptData(httpHeader, nodes[nodes.Length - 1].AesKey);
                    byte[] layer = ConcatArray(Encoding.UTF8.GetBytes("0#" + httpConnection.url.Host + ":" + httpConnection.url.Port + "<!!>DNS:<!!>\r\n"), encryptedData);
                    
                    // Create layers
                    for(int i = 1; nodes.Length > i; i++)
                    {
                        TunnelNode node = nodes[nodes.Length - i - 1];
                        byte[] encryptedLayer = EncryptData(layer, node.AesKey);
                        String[] hostPort = node.Address.Split(":");
                        layer = ConcatArray(Encoding.UTF8.GetBytes("1#" + hostPort[0] + ":" + hostPort[1] + "\r\n"), encryptedLayer);
                    }

                    // Send data through tunnel
                    nextNetworkStream.Write(layer, 0, layer.Length);
                    nextNetworkStream.Flush();
                }

                // Listen to date from the tunnel
                if (nextNetworkStream.DataAvailable)
                {
                    byte[] buffer = new byte[8192];
                    int bytesRead;
                    int k = 0;
                    
                    // Read the data from the tunnel
                    MemoryStream ms = new MemoryStream();

                    // Read data
                    lp:
                    while (nextNetworkStream.DataAvailable && (bytesRead = nextNetworkStream.Read(buffer, 0, buffer.Length)) > 0)
                    {
                        k += bytesRead;
                        ms.Write(new ArraySegment<byte>(buffer, 0, bytesRead).ToArray());
                      
                    }

                    // Wait and check if there is more data
                    Thread.Sleep(100);
                    if (nextNetworkStream.DataAvailable) goto lp;

                    // Decrypt data from tunnel
                    byte[] res = ms.ToArray();
                    for (int i = 0; nodes.Length > i; i++)
                    {
                        res = DecryptData(res, nodes[i].AesKey);
                    }
                    
                    // Send data through the proxy
                    fromNetworkStream.Write(res, 0, res.Length);
                    fromNetworkStream.Flush();
                    Console.WriteLine("Thread: " + Thread.GetCurrentProcessorId() + " Sending package with " + k + " bytes");

                }
                Thread.Sleep(50);
            }
        }

        /// <summary>
        /// Create a AesManaged Instance
        /// </summary>
        /// <param name="key">AES key</param>
        /// <returns>AesManaged object</returns>
        private AesManaged CreateAas(byte[] key)
        {
            AesManaged aes = new AesManaged();
            aes.Mode = CipherMode.CFB;
            aes.Padding = PaddingMode.Zeros;
            aes.Key = key;
            aes.IV = new byte[] { 0xE0, 0x4F, 0xD0, 0x20, 0xEA, 0x3A, 0x69, 0x10, 0xA2, 0xD8, 0x8, 0x0, 0x2B, 0x30, 0x30, 0x9D };

            return aes;
        }

        /// <summary>
        /// Method to decrypt data using AES
        /// </summary>
        /// <param name="bytes">Data to decrypt</param>
        /// <param name="key">AES key</param>
        /// <returns>Decrypted data</returns>
        private byte[] DecryptData(byte[] bytes, byte[] key)
        {
            AesManaged aes = CreateAas(key);
            
            byte[] result;
            using MemoryStream resMs = new MemoryStream();

            // Decrypt data
            using (MemoryStream ms = new MemoryStream(bytes))
            {
                using CryptoStream cryptoStream = new CryptoStream(ms, aes.CreateDecryptor(), CryptoStreamMode.Read);
                cryptoStream.CopyTo(resMs);
                result = resMs.ToArray();
            }
            return result;
        }

        /// <summary>
        /// Method to encrypt data using AES
        /// </summary>
        /// <param name="bytes">Data to decrypt</param>
        /// <param name="key">AES key</param>
        /// <returns>Encrypted data</returns>
        private byte[] EncryptData(byte[] bytes, byte[] key)
        {
            byte[] encrypted;
            AesManaged aes = CreateAas(key);

            // Encrypt data
            using (MemoryStream ms = new MemoryStream())
            {
                using CryptoStream cryptoStream = new CryptoStream(ms, aes.CreateEncryptor(), CryptoStreamMode.Write);
                cryptoStream.Write(bytes, 0, bytes.Length);
                cryptoStream.FlushFinalBlock();
                encrypted = ms.ToArray();
            }
            return encrypted;
        }

        /// <summary>
        /// Method that reads data from a stream and returns a string;
        /// </summary>
        /// <param name="stream"></param>
        /// <returns>Data read</returns>
        private String ReadDataString(NetworkStream stream)
        {
            return Encoding.UTF8.GetString(ReadDataByte(stream));
        }

        /// <summary>
        /// Method that reads data from a stream and returns the byte array
        /// </summary>
        /// <param name="stream"></param>
        /// <returns>Bytes read</returns>
        private byte[] ReadDataByte(NetworkStream stream)
        {
            MemoryStream ms = new MemoryStream();
            byte[] buffer = new byte[8192];
            int bytesRead;
            while (stream.DataAvailable && (bytesRead = stream.Read(buffer, 0, buffer.Length)) > 0)
            {
                ms.Write(buffer, 0, bytesRead);
            }
            return ms.ToArray();
        }

        /// <summary>
        /// Method that joins two arrays
        /// </summary>
        /// <param name="arr1">Array 1</param>
        /// <param name="arr2">Array 2</param>
        /// <returns>One array containing both arrays inserted</returns>
        private static byte[] ConcatArray(byte[] arr1, byte[] arr2)
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
    }

    /// <summary>
    /// Class Representing a Onion Routing Node
    /// </summary>
    class TunnelNode
    {
        public string Address { get; set; }
        public int Port { get; set; }
        public string Pubkey { get; set; }
        public byte[] AesKey { get; set; }
    }

    /// <summary>
    /// Class that parses a http header and removes proxy information. 
    /// </summary>
    class HttpConnection
    {
        public Dictionary<String, String> headerMap;
        public String method;
        public Uri url;
        public int port;
        public String protocol;

        public HttpConnection() { }

        /// <summary>
        /// Method that parses a HTTP header and returns an instance of the HttpConnection class.
        /// </summary>
        /// <param name="header"></param>
        /// <returns>An instance of the HttpConnection class</returns>
        public static HttpConnection Parse(String header)
        {
            HttpConnection connection = new HttpConnection();
            String[] headerLines = header.Split("\r\n");

            // Set values
            String[] firstLine = headerLines[0].Split(" ");
            connection.method = firstLine[0];
            connection.url = new Uri(firstLine[1]);
            connection.protocol = firstLine[2];
            connection.headerMap = new Dictionary<String, String>();

            // Loop through all the lines and skip the first line
            for(int i = 1; headerLines.Length - 2 > i; i++)
            {
                String[] keyValue = headerLines[i].Split(":");
                connection.headerMap.Add(keyValue[0], keyValue[1]);
            }

            // Remove headers added by proxy
            connection.headerMap.Remove("Host");
            connection.headerMap.Remove("Proxy-Connection");
            connection.headerMap.Remove("Upgrade-Insecure-Requests");
            connection.headerMap.Add("Host", " " + connection.url.Host);

            return connection;
        }

        /// <summary>
        /// Method that generates a HTTP header.
        /// </summary>
        /// <returns>The generated HTTP header</returns>
        public String getFullHeader()
        {
            return method + " " + url.PathAndQuery + " " + protocol + "\r\n" + String.Join("", headerMap.Keys.ToArray().Select(k => k + ":" + headerMap.GetValueOrDefault(k) + "\r\n")) + "\r\n";
        }
    }
}
