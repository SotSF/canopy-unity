using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using UnityEngine;

using WebSocketServer;

public class SpaceshipGameController : MonoBehaviour
{


    public static SpaceshipGameController instance;

    // Fast Enter Play Mode keeps statics alive between sessions; clear the stale
    // singleton ref so Awake repopulates it cleanly on the next play entry.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStaticState()
    {
        instance = null;
    }

    // No more than 32 players
    private Dictionary<string, SpaceshipController> ships;

    // Players driven from the node canvas rather than a websocket connection. Kept
    // separate from `ships` so the Update() loop never tries to send ship-position
    // packets to a non-existent socket. Keyed by the player id carried in the input bundle.
    private Dictionary<string, SpaceshipController> canvasShips;
    // Last-seen input state per canvas player: button states for rising-edge ("fire on
    // press") detection, and the last applied color so we only push color changes.
    private Dictionary<string, CanvasPlayerState> canvasState;

    private struct CanvasPlayerState
    {
        public bool fire;
        public bool altFire;
        public bool colorApplied;
        public Color color;
    }

    public RenderTexture gameBoardTex;
    public RenderTexture fluidVelocityTex;

    public SpaceshipController spaceshipPrefab;
    public GameObject gameBoard;

    public WebSocketServer.WebSocketServer server;

    int fluidVelocityKernel;


    public void Awake()
    {
        if (instance != null)
        {
            Destroy(instance);
        }
        instance = this;
        ships = new Dictionary<string, SpaceshipController>();
        canvasShips = new Dictionary<string, SpaceshipController>();
        canvasState = new Dictionary<string, CanvasPlayerState>();

        fluidVelocityTex = new RenderTexture(SpaceshipGameConstants.Instance.gameBoardSize.x, SpaceshipGameConstants.Instance.gameBoardSize.y, 0);
        fluidVelocityTex.useMipMap = false;
        fluidVelocityTex.autoGenerateMips = false;
        fluidVelocityTex.enableRandomWrite = true;
        fluidVelocityTex.filterMode = FilterMode.Point;
        fluidVelocityTex.wrapModeU = TextureWrapMode.Repeat;
        fluidVelocityTex.wrapModeV = TextureWrapMode.Clamp;
        fluidVelocityTex.Create();
    }

    void Start()
    {
        
    }

    public static readonly float dragFactor = 0.005f;
    public float playerSize = 2;
    // 1 byte for event id, 4 bytes for two floats r & theta. Pre-initialize with the position event type representation
    private byte[] shipPositionEventBuffer = new byte[1 + 4 * 2] { (byte)SpaceshipGameEventType.ShipPosition,0,0,0,0,0,0,0,0 }; 
    void Update()
    {
        foreach (var kvPair in ships)
        {
            var id = kvPair.Key;
            var ship = kvPair.Value;

            float r = ship.transform.localPosition.magnitude / SpaceshipGameConstants.Instance.boundaryRadius;
            float theta = Mathf.Atan2(ship.transform.localPosition.z, ship.transform.localPosition.x);
            byte[] rBytes = BitConverter.GetBytes(r);
            byte[] thetaBytes = BitConverter.GetBytes(theta);
            Buffer.BlockCopy(rBytes, 0, shipPositionEventBuffer, 1, 4);
            Buffer.BlockCopy(thetaBytes, 0, shipPositionEventBuffer, 5, 4);
            server.SendBinary(id, shipPositionEventBuffer);
        }
    }

    // Gets (or lazily creates) the ship for a canvas player. Idempotent, so it's safe to
    // call every frame and after a Play restart (when canvasShips starts empty again).
    public SpaceshipController AddCanvasPlayer(string id)
    {
        if (string.IsNullOrEmpty(id))
            return null;
        if (!canvasShips.TryGetValue(id, out var ship) || ship == null)
        {
            ship = SpaceshipController.Create(spaceshipPrefab, gameObject);
            canvasShips[id] = ship;
            Debug.Log($"Added canvas player with id {id}");
        }
        return ship;
    }

    // Applies one frame of bundled canvas input to the matching ship.
    public void ApplyCanvasInput(SpaceshipGamePlayerData data)
    {
        if (string.IsNullOrEmpty(data.playerId))
            return;
        var ship = AddCanvasPlayer(data.playerId);
        if (ship == null)
            return;

        ship.OnStickInput(data.leftStick, data.rightStick);

        canvasState.TryGetValue(data.playerId, out var prev);
        var next = prev;

        // Rising-edge so a held button fires once (mirrors the web Press event).
        if (data.fire && !prev.fire)
            ship.OnButtonPress(0);
        if (data.altFire && !prev.altFire)
            ship.OnButtonPress(1);
        next.fire = data.fire;
        next.altFire = data.altFire;

        // Only push color on first sight or when it changes (mirrors the web ChangeColor event).
        if (data.hasColor && (!prev.colorApplied || prev.color != data.color))
        {
            ship.OnUpdateColor(data.color);
            next.colorApplied = true;
            next.color = data.color;
        }

        canvasState[data.playerId] = next;
    }

    // Destroys canvas ships whose players are no longer being driven (port disconnected or
    // source node removed), so the active id set is the single source of truth each frame.
    public void ReconcileCanvasPlayers(HashSet<string> activeIds)
    {
        if (canvasShips.Count == 0)
            return;
        var stale = new List<string>();
        foreach (var id in canvasShips.Keys)
        {
            if (!activeIds.Contains(id))
                stale.Add(id);
        }
        foreach (var id in stale)
        {
            var ship = canvasShips[id];
            canvasShips.Remove(id);
            canvasState.Remove(id);
            if (ship != null)
                Destroy(ship.gameObject);
        }
    }

    enum SpaceshipGameEventType
    {
        Update = 1,
        ChangeColor,
        Press,
        Gyro,
        Rotate,
        CalibrationStatus,
        ShipPosition,
        TouchPosition
    }

    struct SpaceshipGameEvent
    {
        SpaceshipGameEventType evt;
        Color32 player;
        float[] data;
    }

    public void OnOpen(WebSocketConnection connection)
    {
        var playerShip = SpaceshipController.Create(spaceshipPrefab, gameObject);
        ships[connection.id] = playerShip;
        Debug.Log($"Received websocket connection with id {connection.id}");
    }

    public void OnClose(WebSocketConnection connection)
    {
        var leavingPlayer = ships[connection.id];
        ships.Remove(connection.id);
        Destroy(leavingPlayer.gameObject);
    }

/*
      Binary format

      EventType.ChangeColor:
        0x00                < Event type
        0x00 0x00 0x00      < Player hex color

      EventType.Press:
        0x00                < Event type
        0x00                < Button id

      EventType.Update:
        0x00                < Event type
        0x00 0x00 0x00 0x00 < float data 0 (lx)
        0x00 0x00 0x00 0x00 < float data 1 (ly)
        0x00 0x00 0x00 0x00 < float data 2 (rx)
        0x00 0x00 0x00 0x00 < float data 3 (ry)
*/
    public void OnMessage(WebSocketMessage message)
    {
        //message.connection;
        if (message.text != null)
            Debug.Log(message.text);
        if (message.rawdata != null)
        {
            
            SpaceshipGameEventType evt = (SpaceshipGameEventType)message.rawdata[0];
            var data = message.rawdata;
            var conn = message.connection.id;
            var ship = ships[conn];
            switch (evt)
            {
                case SpaceshipGameEventType.ChangeColor:
                    var r = data[1];
                    var g = data[2];
                    var b = data[3];
                    Color32 color = new Color32(r, g, b, 255);
                    ship.OnUpdateColor(color);
                    Debug.Log($"Received ColorChange event for conn {conn} to {color}");
                    break;
                case SpaceshipGameEventType.Update:
                    float data1 = System.BitConverter.ToSingle(data, 1);
                    float data2 = System.BitConverter.ToSingle(data, 5);
                    float data3 = System.BitConverter.ToSingle(data, 9);
                    float data4 = System.BitConverter.ToSingle(data, 13);
                    ship.OnStickInput(new Vector2(data1, data2), new Vector2(data3, data4));
                    //Debug.Log($"Received Update event for conn {conn} with data <{data1:0.00}, {data2:0.00}>, <{data3:0.00}, {data4:0.00}");
                    break;
                case SpaceshipGameEventType.Press:
                    var buttonId = data[1];
                    ship.OnButtonPress(buttonId);
                    Debug.Log($"Received Press event for conn {conn} for button {buttonId}");
                    break;
                case SpaceshipGameEventType.Rotate:
                    float radians = System.BitConverter.ToSingle(data, 1);
                    ship.OnCalibrateRotation(radians);
                    break;
                case SpaceshipGameEventType.CalibrationStatus:
                    byte status = data[1];
                    ship.OnCalibrationStatus(status);
                    break;
                case SpaceshipGameEventType.TouchPosition:
                    float touchR = System.BitConverter.ToSingle(data, 1);
                    float touchTheta = System.BitConverter.ToSingle(data, 5);
                    ship.OnTouchInput(touchR, touchTheta);
                    break;
            }
            ships[conn] = ship;
        }
    }
}