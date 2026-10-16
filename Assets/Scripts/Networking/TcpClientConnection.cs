
using System;
using System.Collections.Concurrent;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using UnityEngine;
using UnityEngine.InputSystem;

public class TcpClientConnection : MonoBehaviour
{
    [Header("Connection Settings")]
    [SerializeField] private string serverIp = "127.0.0.1";
    [SerializeField] private int port = 8856;

    private TcpClient client;
    private StreamWriter writer;
    private Thread connectionThread;

    private volatile bool isConnected;

    private readonly object connectionLock = new();
    private readonly ConcurrentQueue<string> messageQueue = new();

    private void Start()
    {
        // Temporary: later matchmaking will trigger this.
        ConnectToHost();
    }

    public void ConnectToHost()
    {
        if (connectionThread != null && connectionThread.IsAlive)
            return;

        connectionThread = new Thread(ConnectAndReceive)
        {
            IsBackground = true
        };

        connectionThread.Start();
    }

    private void ConnectAndReceive()
    {
        TcpClient newClient = new TcpClient();

        try
        {
            newClient.Connect(serverIp, port);

            NetworkStream stream = newClient.GetStream();

            StreamReader reader = new(
                stream,
                new UTF8Encoding(false)
            );

            StreamWriter newWriter = new(
                stream,
                new UTF8Encoding(false)
            )
            {
                AutoFlush = true,
                NewLine = "\n"
            };

            lock (connectionLock)
            {
                client = newClient;
                writer = newWriter;
                isConnected = true;
            }

            messageQueue.Enqueue("CONNECTED");

            string line;

            while (isConnected && (line = reader.ReadLine()) != null)
            {
                messageQueue.Enqueue(line);
            }
        }
        catch (SocketException exception)
        {
            messageQueue.Enqueue(
                $"NETWORK_ERROR:{exception.Message}"
            );
        }
        catch (IOException exception)
        {
            messageQueue.Enqueue(
                $"NETWORK_ERROR:{exception.Message}"
            );
        }
        catch (ObjectDisposedException)
        {
            // Expected during shutdown.
        }
        finally
        {
            lock (connectionLock)
            {
                isConnected = false;
                writer = null;
                client = null;
            }

            newClient.Close();
            messageQueue.Enqueue("DISCONNECTED");
        }
    }

    private void Update()
    {
        // Process network messages on Unity's main thread.
        while (messageQueue.TryDequeue(out string message))
        {
            HandleMessage(message);
        }

        // Temporary test: press P to send PING.
        if (isConnected &&
            Keyboard.current != null &&
            Keyboard.current.pKey.wasPressedThisFrame)
        {
            string message = MessageProtocol.CreateSymbolMessage(MessageProtocol.Light);

            SendMessageToHost(message);
        }
    }

    private void HandleMessage(string message)
    {
        if (MessageProtocol.TryParseSymbolMessage(message,
                                                  out string messageType,
                                                  out string symbol))
        {
            if (messageType == MessageProtocol.SymbolReceived)
            {
                Debug.Log($"Navigator symbol confirmed: {symbol}");
            }

            return;
        }

        switch (message)
        {
            case "CONNECTED":
                Debug.Log("Connected to Explorer host.");
                break;

            case "DISCONNECTED":
                Debug.Log("Disconnected from Explorer host.");
                break;

            case "PONG|":
                Debug.Log("Navigator received: PONG|");
                break;

            default:
                Debug.Log($"Navigator received: {message}");
                break;
        }
    }

    public void SendMessageToHost(string message)
    {
        lock (connectionLock)
        {
            if (!isConnected || writer == null)
                return;

            try
            {
                writer.WriteLine(message);

                Debug.Log($"Navigator sent: {message}");
            }
            catch (IOException exception)
            {
                Debug.LogWarning(
                    $"Failed to send message: {exception.Message}"
                );
            }
        }
    }

    public void Disconnect()
    {
        isConnected = false;

        lock (connectionLock)
        {
            client?.Close();
        }
    }

    private void OnDestroy()
    {
        Disconnect();
    }
}
