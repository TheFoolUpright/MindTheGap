
using System;
using System.Collections.Concurrent;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using UnityEngine;

public class TcpHost : MonoBehaviour
{
    [Header("Connection Settings")]
    [SerializeField] private int port = 8856;

    [Header("Gameplay References")]
    [SerializeField] private ReceiverController explorerReceiver;

    // FURNACE: Reference to the Explorer's furnace.
    [SerializeField] private FurnaceController furnace;

    private TcpListener listener;
    private TcpClient navigatorClient;
    private StreamWriter navigatorWriter;

    private Thread listenerThread;
    private volatile bool isRunning;

    private readonly object connectionLock = new();
    private readonly ConcurrentQueue<string> messageQueue = new();

    // FURNACE: Listen for changes to furnace state.
    private void OnEnable()
    {
        if (furnace != null)
        {
            furnace.StateChanged += OnFurnaceStateChanged;
        }
    }

    private void OnDisable()
    {
        if (furnace != null)
        {
            furnace.StateChanged -= OnFurnaceStateChanged;
        }
    }

    private void Start()
    {
        // Temporary: later the lobby will start the host.
        StartHost();
    }

    public void StartHost()
    {
        if (isRunning)
            return;

        try
        {
            listener = new TcpListener(IPAddress.Any, port);
            listener.Start();

            isRunning = true;

            listenerThread = new Thread(ListenForClients)
            {
                IsBackground = true
            };

            listenerThread.Start();

            Debug.Log($"TCP host started on port {port}.");
        }
        catch (SocketException exception)
        {
            Debug.LogError($"Host failed: {exception.Message}");
            listener?.Stop();
            listener = null;
            isRunning = false;
        }
    }

    private void ListenForClients()
    {
        while (isRunning)
        {
            try
            {
                TcpClient client = listener.AcceptTcpClient();

                lock (connectionLock)
                {
                    if (navigatorClient != null)
                    {
                        client.Close();
                        continue;
                    }

                    navigatorClient = client;
                }

                // Handle this client's messages
                // until it disconnects.
                ReceiveMessages(client);
            }
            catch (SocketException)
            {
                if (!isRunning)
                    break;
            }
            catch (ObjectDisposedException)
            {
                break;
            }
        }
    }

    private void ReceiveMessages(TcpClient client)
    {
        try
        {
            NetworkStream stream = client.GetStream();

            StreamReader reader = new(
                stream,
                new UTF8Encoding(false)
            );

            StreamWriter writer = new(
                stream,
                new UTF8Encoding(false)
            )
            {
                AutoFlush = true,
                NewLine = "\n"
            };

            lock (connectionLock)
            {
                navigatorWriter = writer;
            }

            // FURNACE:
            // Notify the main thread only after
            // the writer is ready to send messages.
            messageQueue.Enqueue(
                $"CONNECTED:{client.Client.RemoteEndPoint}"
            );

            string line;

            while (isRunning &&
                   (line = reader.ReadLine()) != null)
            {
                messageQueue.Enqueue(line);
            }
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
                if (navigatorClient == client)
                {
                    navigatorClient = null;
                    navigatorWriter = null;
                }
            }

            client.Close();

            if (isRunning)
                messageQueue.Enqueue("DISCONNECTED");
        }
    }

    private void Update()
    {
        // All messages are processed on
        // Unity's main thread.
        while (messageQueue.TryDequeue(out string message))
        {
            HandleMessage(message);
        }
    }

    private void HandleMessage(string message)
    {
        if (message.StartsWith("CONNECTED:"))
        {
            Debug.Log(
                $"Navigator connected: {message.Substring(10)}"
            );

            // FURNACE:
            // Send the current state when
            // the Navigator connects.
            SendCurrentFurnaceState();

            return;
        }

        if (message == "DISCONNECTED")
        {
            Debug.Log("Navigator disconnected.");
            return;
        }

        if (message.StartsWith("NETWORK_ERROR:"))
        {
            Debug.LogWarning(message);
            return;
        }

        Debug.Log($"Host received: {message}");

        // Existing receiver-symbol communication.
        if (MessageProtocol.TryParseSymbolMessage(
            message,
            out string messageType,
            out string symbol))
        {
            if (messageType == MessageProtocol.SymbolSend)
            {
                Debug.Log(
                    $"Explorer received symbol: {symbol}"
                );

                if (explorerReceiver != null)
                {
                    explorerReceiver.AddSymbol(symbol);
                }
                else
                {
                    Debug.LogWarning(
                        "TcpHost has no Explorer Receiver assigned."
                    );
                }

                string confirmation =
                    MessageProtocol.CreateSymbolConfirmation(symbol);

                SendToNavigator(confirmation);
            }

            return;
        }
    }

    public void SendToNavigator(string message)
    {
        lock (connectionLock)
        {
            if (navigatorWriter == null)
                return;

            try
            {
                navigatorWriter.WriteLine(message);
            }
            catch (IOException exception)
            {
                Debug.LogWarning(
                    $"Failed to send message: {exception.Message}"
                );
            }
        }
    }

    // FURNACE:
    // Called whenever resources enter, leave,
    // or are processed by the furnace.
    private void OnFurnaceStateChanged(FurnaceState state)
    {
        string message =
            MessageProtocol.EncodeFurnaceState(state);

        SendToNavigator(message);

        Debug.Log($"Furnace state sent: {message}");
    }

    // FURNACE:
    // Send a complete snapshot when a
    // Navigator connects or reconnects.
    private void SendCurrentFurnaceState()
    {
        if (furnace == null)
        {
            Debug.LogWarning(
                "TcpHost has no FurnaceController assigned."
            );
            return;
        }

        OnFurnaceStateChanged(furnace.State);
    }

    public void StopHost()
    {
        isRunning = false;

        listener?.Stop();

        lock (connectionLock)
        {
            navigatorClient?.Close();
            navigatorClient = null;
            navigatorWriter = null;
        }

        listener = null;

        Debug.Log("TCP host stopped.");
    }

    private void OnDestroy()
    {
        StopHost();
    }
}
