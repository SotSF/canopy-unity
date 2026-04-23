using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using UnityEngine;

using WebSocketServer;

public class SpaceshipGameController : MonoBehaviour
{
    private static Vector2Int gameBoardSize = new Vector2Int(512, 512);
    private static Vector2 gameBoardCenter = new Vector2(255, 255);
    public float innerRingDist = 256 / 8;
    public float outerRingDist = 256;
    public float velocityScaling = 0.25f;
    public float maxSpeed = 10;
    public static SpaceshipGameController instance;

    // No more than 32 players
    private Dictionary<string, SpaceshipController> ships;

    public RenderTexture gameBoardTex;
    public RenderTexture fluidVelocityTex;

    public SpaceshipController spaceshipPrefab;
    public GameObject gameBoard;

    int fluidVelocityKernel;


    public void Awake()
    {
        if (instance != null)
        {
            Destroy(instance);
        }
        instance = this;
        ships = new Dictionary<string, SpaceshipController>();

        fluidVelocityTex = new RenderTexture(gameBoardSize.x, gameBoardSize.y, 0);
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

    public static float dragFactor = 0.005f;
    public float playerSize = 2;

    void Update()
    {

    }

    enum SpaceshipGameEventType
    {
        Update = 1,
        ChangeColor,
        Press
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
        Destroy(leavingPlayer.gameObject);
        ships.Remove(connection.id);
    }

    private Vector2 RandomPosition()
    {
        return new Vector2(Random.Range(0, gameBoardSize.x), Random.Range(0, gameBoardSize.y));
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
            }
            ships[conn] = ship;
        }
    }
}