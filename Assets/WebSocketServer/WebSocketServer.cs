using System;
// Networking libs
using System.Net;
using System.Net.Sockets;
// For creating a thread
using System.Threading;
// For List & ConcurrentQueue
using System.Collections.Generic;
using System.Collections.Concurrent;
// Unity & Unity events
using UnityEngine;
using UnityEngine.Events;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace WebSocketServer {
    [System.Serializable]
    public class WebSocketOpenEvent : UnityEvent<WebSocketConnection> {}

    [System.Serializable]
    public class WebSocketMessageEvent : UnityEvent<WebSocketMessage> {}

    [System.Serializable]
    public class WebSocketCloseEvent : UnityEvent<WebSocketConnection> {}

    public class WebSocketServer : MonoBehaviour
    {
        // The tcpListenerThread listens for incoming WebSocket connections, then assigns the client to handler threads;
        private TcpListener tcpListener;
        private Thread tcpListenerThread;
        private readonly Dictionary<string, WebSocketConnection> connectionsById = new Dictionary<string, WebSocketConnection>();
        private readonly object connectionsLock = new object();
        private volatile bool isShuttingDown;
        private TcpClient connectedTcpClient;

        public ConcurrentQueue<WebSocketEvent> events;

        public string address;
        public int port;
        [Tooltip("Auto-close a connection after this many seconds of inactivity. The server pings idle clients at timeout/3 to detect dead sockets early. Set to 0 to disable.")]
        public float connectionTimeoutSeconds = 60f;
        public WebSocketOpenEvent onOpen;
        public WebSocketMessageEvent onMessage;
        public WebSocketCloseEvent onClose;

        private float lastSweepTime;
        private const float SweepIntervalSeconds = 1f;

        public bool IsShuttingDown => isShuttingDown;

        void Awake() {
            if (onMessage == null) onMessage = new WebSocketMessageEvent();
        }

        void Start() {
            events = new ConcurrentQueue<WebSocketEvent>();

            tcpListenerThread = new Thread (new ThreadStart(ListenForTcpConnection));
            tcpListenerThread.IsBackground = true;
            tcpListenerThread.Start();

#if UNITY_EDITOR
            // Domain reloads on script recompile destroy managed state but can leave the listener
            // thread mid-syscall with the native socket still bound. Tear down first.
            AssemblyReloadEvents.beforeAssemblyReload += Shutdown;
#endif
        }

        void Update() {
            if (events == null || isShuttingDown) return;
            WebSocketEvent wsEvent;
            while (events.TryDequeue(out wsEvent)) {
                switch (wsEvent.type)
                {
                    case WebSocketEventType.Open:
                        onOpen.Invoke(wsEvent.connection);
                        this.OnOpen(wsEvent.connection);
                        break;
                    case WebSocketEventType.Close:
                        onClose.Invoke(wsEvent.connection);
                        this.OnClose(wsEvent.connection);
                        break;
                    case WebSocketEventType.Text:
                    case WebSocketEventType.Binary:
                        WebSocketMessage message = new WebSocketMessage(wsEvent.connection, wsEvent.text, wsEvent.rawdata);
                        onMessage.Invoke(message);
                        this.OnMessage(message);
                        break;
                }
            }

            if (connectionTimeoutSeconds > 0f && Time.unscaledTime - lastSweepTime >= SweepIntervalSeconds) {
                lastSweepTime = Time.unscaledTime;
                SweepIdleConnections();
            }
        }

        // Walks active connections and pings the idle ones / closes the dead ones.
        // - idle >= timeout: graceful close with code 1001 (Going Away).
        // - idle >= timeout/3: send a Ping. If the client is alive it'll Pong and reset the timer;
        //   if dead, the Write fails and SendRawFrame closes the connection immediately.
        private void SweepIdleConnections() {
            long nowTicks = DateTime.UtcNow.Ticks;
            long timeoutTicks = (long)(connectionTimeoutSeconds * TimeSpan.TicksPerSecond);
            long pingTicks = timeoutTicks / 3;

            WebSocketConnection[] snapshot;
            lock (connectionsLock) {
                snapshot = new WebSocketConnection[connectionsById.Count];
                connectionsById.Values.CopyTo(snapshot, 0);
            }

            foreach (var conn in snapshot) {
                if (conn.IsClosed) continue;
                long idle = nowTicks - conn.LastActivityTicks;
                if (idle >= timeoutTicks) {
                    Debug.Log($"WebSocket: idle timeout on {conn.id} ({idle / TimeSpan.TicksPerSecond}s) — closing");
                    conn.CloseGracefully(1001);
                    continue;
                }
                if (idle >= pingTicks) {
                    conn.SendPing();
                }
            }
        }

        // Closes a specific connection by ID — useful for application-level eviction
        // (e.g. kicking an in-game player who's gone idle in game terms, even if their
        // socket is still chatting). Returns false if no connection with that id is tracked.
        public bool CloseConnection(string connectionId, ushort statusCode = 1000) {
            if (!TryGetConnection(connectionId, out WebSocketConnection conn)) {
                Debug.LogWarning($"WebSocket: no active connection with id {connectionId} to close");
                return false;
            }
            conn.CloseGracefully(statusCode);
            return true;
        }

        private void ListenForTcpConnection () {
            try {
                // Create listener on <address>:<port>.
                tcpListener = new TcpListener(IPAddress.Parse(address), port);
                tcpListener.Start();
                Debug.Log($"WebSocket server is listening on {address}:{port}.");
                while (!isShuttingDown) {
                    // Accept a new client, then open a stream for reading and writing.
                    connectedTcpClient = tcpListener.AcceptTcpClient();
                    if (isShuttingDown) {
                        try { connectedTcpClient.Close(); } catch { }
                        break;
                    }
                    // Create a new connection
                    WebSocketConnection connection = new WebSocketConnection(connectedTcpClient, this);
                    RegisterConnection(connection);
                    // Establish connection (handshake). If it fails or shutdown begins mid-handshake,
                    // the connection cleans itself up via Close().
                    if (!connection.Establish()) {
                        connection.Close();
                    }
                }
            }
            catch (SocketException socketException) {
                if (!isShuttingDown) {
                    Debug.LogError($"Got SocketException trying to start on {address}:{port}:\n" + socketException);
                }
            }
            catch (ObjectDisposedException) {
                // Expected: listener was disposed during shutdown.
            }
            finally {
                // Defense in depth: ensure the listener socket is released regardless of how we exit.
                try { tcpListener?.Stop(); } catch { }
            }
        }

        internal void RegisterConnection(WebSocketConnection connection) {
            lock (connectionsLock) {
                connectionsById[connection.id] = connection;
            }
        }

        internal void UnregisterConnection(WebSocketConnection connection) {
            lock (connectionsLock) {
                connectionsById.Remove(connection.id);
            }
        }

        public bool TryGetConnection(string connectionId, out WebSocketConnection connection) {
            lock (connectionsLock) {
                return connectionsById.TryGetValue(connectionId, out connection);
            }
        }

        public bool SendBinary(string connectionId, byte[] data) {
            if (!TryGetConnection(connectionId, out WebSocketConnection connection)) {
                Debug.LogWarning($"WebSocket: no active connection with id {connectionId}");
                return false;
            }
            return connection.SendBinary(data);
        }

        public void Shutdown() {
            if (isShuttingDown) return;
            isShuttingDown = true;

            Debug.Log($"Shutting down websocket on {address}:{port}");

#if UNITY_EDITOR
            AssemblyReloadEvents.beforeAssemblyReload -= Shutdown;
#endif

            // Close all active client connections so their handler threads exit and their sockets release.
            WebSocketConnection[] toClose;
            lock (connectionsLock) {
                toClose = new WebSocketConnection[connectionsById.Count];
                connectionsById.Values.CopyTo(toClose, 0);
                connectionsById.Clear();
            }
            foreach (var c in toClose) {
                try { c.Close(); } catch (Exception e) { Debug.LogWarning($"Error closing connection: {e.Message}"); }
            }

            // Stop the listener — this interrupts AcceptTcpClient on the listener thread with a SocketException.
            try { tcpListener?.Stop(); } catch (Exception e) { Debug.LogWarning($"Error stopping listener: {e.Message}"); }
            tcpListener = null;

            // Wait briefly for the listener thread to exit cleanly; don't block the main thread indefinitely.
            if (tcpListenerThread != null && tcpListenerThread.IsAlive) {
                tcpListenerThread.Join(500);
            }
            tcpListenerThread = null;
        }

        void OnDisable() {
            Shutdown();
        }

        void OnApplicationQuit() {
            Shutdown();
        }

        public void OnDestroy() {
            Shutdown();
        }

        public virtual void OnOpen(WebSocketConnection connection) {}

        public virtual void OnMessage(WebSocketMessage message) {}

        public virtual void OnClose(WebSocketConnection connection) {}

        public virtual void OnError(WebSocketConnection connection) {}
    }
}
