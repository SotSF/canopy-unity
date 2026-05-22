using UnityEngine;

using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Collections.Concurrent;
// For parsing the client websocket requests
using System.Text;
using System.Text.RegularExpressions;
// For creating a thread
using System.Threading;


namespace WebSocketServer {

    public enum WebSocketEventType {
        Open,
        Close,
        Text,
        Binary,
    }

    public struct WebSocketMessage {
        public WebSocketMessage(WebSocketConnection connection, string text, byte[] rawdata = null) {
            this.id = Guid.NewGuid().ToString();
            this.connection = connection;
            this.text = text;
            this.rawdata = rawdata;
        }

        public string id { get; }
        public WebSocketConnection connection { get; }
        public string text { get; }
        public byte[] rawdata { get; }
    }

    public struct WebSocketEvent {
        public WebSocketEvent(WebSocketConnection connection, WebSocketEventType type, string text, byte[] rawdata=null) {
            this.id = Guid.NewGuid().ToString();
            this.connection = connection;
            this.type = type;
            this.text = text;
            this.rawdata = rawdata;
        }
        public string id { get; }
        public WebSocketEventType type { get; }
        public WebSocketConnection connection { get; }
        public string text { get; }
        public byte[] rawdata { get; }
    }

    public class WebSocketConnection {

        public string id;
        private TcpClient client;
        private NetworkStream stream;
        private WebSocketServer server;
        private Thread connectionHandler;
        private volatile bool isClosed;
        private readonly object writeLock = new object();
        // 64-bit timestamp; accessed via Interlocked so reads on 32-bit targets can't tear.
        private long lastActivityTicks;

        public bool IsClosed => isClosed;
        public long LastActivityTicks => Interlocked.Read(ref lastActivityTicks);

        public WebSocketConnection(TcpClient client, WebSocketServer server) {
            this.id = Guid.NewGuid().ToString();
            this.client = client;
            this.stream = client.GetStream();
            this.server = server;
            Touch();
        }

        private void Touch() {
            Interlocked.Exchange(ref lastActivityTicks, DateTime.UtcNow.Ticks);
        }

        public bool Establish() {
            try {
                // Wait for enough bytes to be available. Sleep(1) prevents 100% CPU pegging and
                // lets us bail out promptly on shutdown / abrupt client disconnect.
                while (!stream.DataAvailable && !server.IsShuttingDown && !isClosed) Thread.Sleep(1);
                while (client.Connected && client.Available < 3 && !server.IsShuttingDown && !isClosed) Thread.Sleep(1);
                if (server.IsShuttingDown || isClosed || !client.Connected) return false;

                // Translate bytes of request to a RequestHeader object
                Byte[] bytes = new Byte[client.Available];
                stream.Read(bytes, 0, bytes.Length);
                RequestHeader request = new RequestHeader(Encoding.UTF8.GetString(bytes));

                // Check if the request complies with WebSocket protocol.
                if (WebSocketProtocol.CheckConnectionHandshake(request)) {
                    // If so, initiate the connection by sending a reply according to protocol.
                    Byte[] response = WebSocketProtocol.CreateHandshakeReply(request);
                    stream.Write(response, 0, response.Length);

                    Debug.Log("WebSocket client connected.");

                    // Start message handling
                    connectionHandler = new Thread(new ThreadStart(HandleConnection));
                    connectionHandler.IsBackground = true;
                    connectionHandler.Start();

                    // Call the server callback.
                    WebSocketEvent wsEvent = new WebSocketEvent(this, WebSocketEventType.Open, null);
                    server.events.Enqueue(wsEvent);
                    return true;
                } else {
                    return false;
                }
            } catch (Exception e) {
                if (!server.IsShuttingDown) {
                    Debug.LogWarning($"WebSocket handshake failed: {e.Message}");
                }
                return false;
            }
        }

        private void HandleConnection () {
            bool clientSentClose = false;
            try {
                while (!isClosed && !server.IsShuttingDown) {
                    WebSocketDataFrame dataframe;
                    try {
                        dataframe = ReadDataFrame();
                    } catch (Exception) {
                        // Stream closed, socket error, or shutdown — exit the loop cleanly.
                        break;
                    }

                    // Any successfully-read frame counts as activity — keeps the idle timer fresh
                    // even for control frames (Ping, Pong) that don't surface to the application.
                    Touch();

                    if (!dataframe.fin) {
                        Debug.Log("Fragmentation encountered.");
                        continue;
                    }

                    WebSocketOpCode opcode = (WebSocketOpCode)dataframe.opcode;
                    switch (opcode)
                    {
                        case WebSocketOpCode.Text:
                            // Let the server know of the message.
                            string data = WebSocketProtocol.DecodeText(dataframe);
                            server.events.Enqueue(new WebSocketEvent(this, WebSocketEventType.Text, data));
                            break;
                        case WebSocketOpCode.Close:
                            Debug.Log("Client closed the connection.");
                            clientSentClose = true;
                            server.events.Enqueue(new WebSocketEvent(this, WebSocketEventType.Close, null));
                            return;
                        case WebSocketOpCode.Binary:
                            server.events.Enqueue(new WebSocketEvent(this, WebSocketEventType.Binary, null, WebSocketProtocol.DecodeBytes(dataframe)));
                            break;
                        case WebSocketOpCode.Ping:
                            // RFC 6455 §5.5.2: respond to Ping with a Pong echoing the payload.
                            byte[] pingPayload = WebSocketProtocol.DecodeBytes(dataframe) ?? Array.Empty<byte>();
                            SendRawFrame(WebSocketProtocol.EncodePongFrame(pingPayload));
                            break;
                        case WebSocketOpCode.Pong:
                            // Solicited or unsolicited Pong — Touch() above already recorded the activity.
                            break;
                    }
                }

                // Loop exited without a client-initiated Close opcode (abrupt disconnect or shutdown).
                // Emit a Close event so listeners get notified — but skip if the server is tearing down,
                // since Update() won't run again to drain the queue.
                if (!clientSentClose && !server.IsShuttingDown && server.events != null) {
                    try {
                        server.events.Enqueue(new WebSocketEvent(this, WebSocketEventType.Close, null));
                    } catch { }
                }
            } finally {
                Close();
            }
        }

        public void Close() {
            if (isClosed) return;
            isClosed = true;
            try { stream?.Close(); } catch { }
            try { client?.Close(); } catch { }
            try { server?.UnregisterConnection(this); } catch { }
        }

        // Sends a binary WebSocket frame to this client. Safe to call from any thread; concurrent
        // sends are serialized via writeLock so frames can't interleave at the byte level on the
        // shared NetworkStream. Returns false if the connection is closed or the send fails (in
        // which case the connection is closed and the caller will receive a Close event).
        public bool SendBinary(byte[] data) {
            if (isClosed) return false;
            return SendRawFrame(WebSocketProtocol.EncodeBinaryFrame(data));
        }

        public bool SendPing(byte[] payload = null) {
            if (isClosed) return false;
            return SendRawFrame(WebSocketProtocol.EncodePingFrame(payload));
        }

        // Graceful close: send a Close frame so the client gets a clean disconnect, then tear down.
        // Falls back to a hard Close() if the Close frame send fails (e.g. socket already half-dead).
        public void CloseGracefully(ushort statusCode = 1000) {
            if (isClosed) return;
            try {
                byte[] frame = WebSocketProtocol.EncodeCloseFrame(statusCode);
                lock (writeLock) {
                    if (!isClosed) stream.Write(frame, 0, frame.Length);
                }
            } catch { }
            Close();
        }

        private bool SendRawFrame(byte[] frame) {
            if (isClosed) return false;
            try {
                lock (writeLock) {
                    if (isClosed) return false;
                    stream.Write(frame, 0, frame.Length);
                }
                return true;
            } catch (Exception e) {
                if (!server.IsShuttingDown) {
                    Debug.LogWarning($"WebSocket send failed on {id}: {e.Message}");
                }
                Close();
                return false;
            }
        }


        private WebSocketDataFrame ReadDataFrame() {
            const int DataframeHead = 2;        // Length of dataframe head
            const int ShortPayloadLength = 2;   // Length of a short payload length field
            const int LongPayloadLength = 8;    // Length of a long payload length field
            const int Mask = 4;                 // Length of the payload mask

            // Wait for a dataframe head to be available, then read the data.
            while (!stream.DataAvailable && client.Available < DataframeHead) {
                if (isClosed || server.IsShuttingDown) throw new IOException("Connection closing");
                Thread.Sleep(1);
            }
            Byte[] bytes = new Byte[DataframeHead];
            stream.Read(bytes, 0, DataframeHead);

            // Decode the message head, including FIN, OpCode, and initial byte of the payload length.
            WebSocketDataFrame dataframe = WebSocketProtocol.CreateDataFrame();
            WebSocketProtocol.ParseDataFrameHead(bytes, ref dataframe);

            // Depending on the dataframe length, read & decode the next bytes for payload length
            if (dataframe.length == 126) {
                while (client.Available < ShortPayloadLength) {
                    if (isClosed || server.IsShuttingDown) throw new IOException("Connection closing");
                    Thread.Sleep(1);
                }
                Array.Resize(ref bytes, bytes.Length + ShortPayloadLength);
                stream.Read(bytes, bytes.Length - ShortPayloadLength, ShortPayloadLength);   // Read the next two bytes for length
            } else if (dataframe.length == 127) {
                while (client.Available < LongPayloadLength) {
                    if (isClosed || server.IsShuttingDown) throw new IOException("Connection closing");
                    Thread.Sleep(1);
                }
                Array.Resize(ref bytes, bytes.Length + LongPayloadLength);
                stream.Read(bytes, bytes.Length - LongPayloadLength, LongPayloadLength);   // Read the next two bytes for length
            }
            WebSocketProtocol.ParseDataFrameLength(bytes, ref dataframe);    // Parse the length

            if (dataframe.mask) {
                while (client.Available < Mask) {
                    if (isClosed || server.IsShuttingDown) throw new IOException("Connection closing");
                    Thread.Sleep(1);
                }
                Array.Resize(ref bytes, bytes.Length + Mask);
                stream.Read(bytes, bytes.Length - Mask, Mask);   // Read the next four bytes for mask
            }

            while (client.Available < dataframe.length) {
                if (isClosed || server.IsShuttingDown) throw new IOException("Connection closing");
                Thread.Sleep(1);
            }
            Array.Resize(ref bytes, bytes.Length + dataframe.length);
            stream.Read(bytes, bytes.Length - dataframe.length, dataframe.length);    // Read the payload
            dataframe.data = bytes;

            return dataframe;
        }
    }

}
