using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Text;

public class StateObject
{
    public Socket workSocket = null;
    public const int BufferSize = 1024;
    public byte[] buffer = new byte[BufferSize];
    public StringBuilder sb = new StringBuilder();
}

public class AsynchronousClient
{
    private const int port = 1337;
    private static ManualResetEvent connectDone =
        new ManualResetEvent(false);
    private static ManualResetEvent sendDone =
        new ManualResetEvent(false);
    private static ManualResetEvent receiveDone =
        new ManualResetEvent(false);

    private static String response = String.Empty;

    private static void StartClient(string hostname)
    {
        try
        {
            IPHostEntry ipHostInfo = Dns.GetHostEntry(hostname);
            
            IPAddress ipAddress = ipHostInfo.AddressList[0];
            IPEndPoint remoteEP = new IPEndPoint(ipAddress, port);
            Console.WriteLine("File to send: [C:\\descent.mp3)]");
            string fileName = Console.ReadLine();
            if (fileName.Length < 2) { fileName = "C:\\descent.mp3"; }
            Socket client = new Socket(AddressFamily.InterNetwork,
                SocketType.Stream, ProtocolType.Tcp);
            Stopwatch sw = new Stopwatch();
            sw.Start();
            client.BeginConnect(remoteEP,
                new AsyncCallback(ConnectCallback), client);
            connectDone.WaitOne();
            string filePath = "";
            fileName = fileName.Replace("\\", "/");
            while (fileName.IndexOf("/") > -1)
            {
                filePath += fileName.Substring(0, fileName.IndexOf("/") + 1);
                fileName = fileName.Substring(fileName.IndexOf("/") + 1);
            }

            byte[] fileNameByte = Encoding.UTF8.GetBytes(fileName);
            string fullPath = filePath + fileName;
            byte[] fileData = File.ReadAllBytes(fullPath);
            byte[] clientData = new byte[4 + fileNameByte.Length + fileData.Length];
            byte[] fileNameLen = BitConverter.GetBytes(fileNameByte.Length);
            fileNameLen.CopyTo(clientData, 0);
            fileNameByte.CopyTo(clientData, 4);
            fileData.CopyTo(clientData, 4 + fileNameByte.Length);
            SendBinary(client, clientData);
            sendDone.WaitOne();

            sw.Stop();
            long microseconds = sw.ElapsedTicks / (Stopwatch.Frequency / (1000L * 1000L));
            
            Console.WriteLine("Sent data in " + microseconds + " microseconds\n");
            Console.WriteLine("Stick around for response? [y/N]");
            string strInput = Console.ReadLine();
            if (strInput == "Y" || strInput == "y")
            {
                Receive(client);
                receiveDone.WaitOne();
            }
            else
            {
                client.Shutdown(SocketShutdown.Both);
                client.Close();
                System.Environment.Exit(0);
            }

        }
        catch (Exception ex)
        {
            if (ex.Message == "No connection could be made because the target machine actively refused it")
                Console.WriteLine("File Sending fail. Because server not running?");
            else
                Console.WriteLine("File Sending fail. " + ex.Message);
        }
    }

    private static void ConnectCallback(IAsyncResult ar)
    {
        try
        {
            Socket client = (Socket)ar.AsyncState;
            client.EndConnect(ar);
            Console.WriteLine("Socket connected to {0} \n",
                client.RemoteEndPoint.ToString());

            connectDone.Set();
        }
        catch (Exception ex)
        {
            if (ex.Message == "No connection could be made because the target machine actively refused it")
                Console.WriteLine("File Sending fail. Because server not running?");
            else
                Console.WriteLine("File Sending fail. " + ex.Message);
        }
    }

    private static void Receive(Socket client)
    {
        try
        {
            StateObject state = new StateObject();
            state.workSocket = client;

            client.BeginReceive(state.buffer, 0, StateObject.BufferSize, 0,
                new AsyncCallback(ReceiveCallback), state);
        }
        catch (Exception ex)
        {
            if (ex.Message == "No connection could be made because the target machine actively refused it")
                Console.WriteLine("File Sending fail. Because server not running?");
            else
                Console.WriteLine("File Sending fail. " + ex.Message);
        }
    }

    private static void ReceiveCallback(IAsyncResult ar)
    {
        try
        {
            StateObject state = (StateObject)ar.AsyncState;
            Socket client = state.workSocket;

            int bytesRead = client.EndReceive(ar);

            if (bytesRead > 0)
            {
                state.sb.Append(Encoding.ASCII.GetString(state.buffer, 0, bytesRead));

                client.BeginReceive(state.buffer, 0, StateObject.BufferSize, 0,
                    new AsyncCallback(ReceiveCallback), state);
            }
            else
            {
                if (state.sb.Length > 1)
                {
                    response = state.sb.ToString();
                }
                receiveDone.Set();
            }
        }
        catch (Exception ex)
        {
            if (ex.Message == "No connection could be made because the target machine actively refused it")
                Console.WriteLine("File Sending fail. Because server not running?");
            else
                Console.WriteLine("File Sending fail. " + ex.Message);
        }
    }

    private static void Send(Socket client, string data)
    {
        // 0 : DEFAULT
        // 1 : ERROR
        // 2 : INFO
        // 3 : DEBUG
        // 4 : TIME

        byte[] byteData = Encoding.ASCII.GetBytes(data);

        client.BeginSend(byteData, 0, byteData.Length, 0,
            new AsyncCallback(SendCallback), client);
    }

    private static void SendBinary(Socket client, byte[] data)
    {

        byte[] byteData = data;

        client.BeginSend(byteData, 0, byteData.Length, 0,
            new AsyncCallback(SendCallback), client);
    }

    private static void SendCallback(IAsyncResult ar)
    {
        try
        {
            Socket client = (Socket)ar.AsyncState;

            int bytesSent = client.EndSend(ar);
            Console.WriteLine("Sent {0} bytes to server. \n", bytesSent);

            sendDone.Set();
        }
        catch (Exception ex)
        {
            if (ex.Message == "No connection could be made because the target machine actively refused it")
                Console.WriteLine("File Sending fail. Because server not running?");
            else
                Console.WriteLine("File Sending fail. " + ex.Message);
        }
    }

    public static int Main(String[] args)
    {
        Console.WriteLine("===========================================");
        Console.WriteLine("Labs Client");
        Console.WriteLine("Send files over TCP with sockets.");
        Console.WriteLine("Written in C Sharp 4.5");
        Console.WriteLine("===========================================\n\n");
        Console.WriteLine("Server: [dev.seven-labs.com]");
        string hostname = Console.ReadLine();
        if (hostname.Length < 3) { hostname = "dev.seven-labs.com"; }
        Console.WriteLine("Using default port of 1337...\n");
        StartClient(hostname);
        Console.ReadLine();
        return 0;
    }
}