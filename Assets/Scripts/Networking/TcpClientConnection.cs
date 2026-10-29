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

    private TcpClient connectionToExplorer;
    private StreamWriter explorerWriter;
    private Thread connectionThread;

    private volatile bool isConnected;

    private readonly object connectionLock = new();
    private readonly ConcurrentQueue<string> messageQueue = new();

    [Header("Gameplay References")]
    [SerializeField] private ReceiverController navigatorReceiver;
    [SerializeField] private FurnaceDisplay furnaceDisplay;

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
                connectionToExplorer = newClient;
                explorerWriter = newWriter;
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
                explorerWriter = null;
                connectionToExplorer = null;
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

        // Temporary test: press number keys to send Symbol.
        if (isConnected && Keyboard.current != null)
        {
            if (Keyboard.current.digit1Key.wasPressedThisFrame)
            {
                SendToExplorer(
                    MessageProtocol.CreateSymbolMessage(MessageProtocol.Light)
                );
            }

            if (Keyboard.current.digit2Key.wasPressedThisFrame)
            {
                SendToExplorer(
                    MessageProtocol.CreateSymbolMessage(MessageProtocol.Door)
                );
            }

            if (Keyboard.current.digit3Key.wasPressedThisFrame)
            {
                SendToExplorer(
                    MessageProtocol.CreateSymbolMessage(MessageProtocol.Enemy)
                );
            }

            if (Keyboard.current.digit4Key.wasPressedThisFrame)
            {
                SendToExplorer(
                    MessageProtocol.CreateSymbolMessage(MessageProtocol.Return)
                );
            }
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

                // Update the Navigator's sent-message history.
                navigatorReceiver?.AddSymbol(symbol);
            }

            return;
        }


        if (MessageProtocol.TryDecodeFurnaceState(
            message,
            out FurnaceState state))
        {
            if (furnaceDisplay != null)
            {
                furnaceDisplay.ShowState(state);

                Debug.Log(
                    $"Navigator furnace updated: " +
                    $"Cubes {state.totalCubes}/{state.cubeGoal}, " +
                    $"Pyramids {state.totalPyramids}/{state.pyramidGoal}"
                );
            }
            else
            {
                Debug.LogWarning(
                    "Navigator FurnaceDisplay is not assigned."
                );
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

    public void SendToExplorer(string message)
    {
        lock (connectionLock)
        {
            if (!isConnected || explorerWriter == null)
                return;

            try
            {
                explorerWriter.WriteLine(message);

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

    public void DisconnectFromHost()
    {
        isConnected = false;

        lock (connectionLock)
        {
            connectionToExplorer?.Close();
        }
    }

    private void OnDestroy()
    {
        DisconnectFromHost();
    }
}