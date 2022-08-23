using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using UnityEngine;

using WebSocketServer;

public class SpaceshipGameController : MonoBehaviour
{
    private static Vector2Int gameBoardSize = new Vector2Int(750, 960);
    public static SpaceshipGameController instance;

    // No more than 32 players
    private Dictionary<string, SpaceshipGamePlayer> players;

    public RenderTexture gameBoardTex;

    private ComputeBuffer playerBuffer;
    private ComputeShader spaceshipRenderShader;
    int shaderKernel;


    public void Awake()
    {
        if (instance != null)
        {
            Destroy(instance);
        }
        instance = this;
        players = new Dictionary<string, SpaceshipGamePlayer>();

        // Create rendertexture
        gameBoardTex = new RenderTexture(gameBoardSize.x, gameBoardSize.y, 0);
        gameBoardTex.useMipMap = false;
        gameBoardTex.autoGenerateMips = false;
        gameBoardTex.enableRandomWrite = true;
        gameBoardTex.filterMode = FilterMode.Point;
        gameBoardTex.wrapModeU = TextureWrapMode.Repeat;
        gameBoardTex.wrapModeV = TextureWrapMode.Clamp;
        gameBoardTex.Create();

        // 32 instances, 32 bytes for 2x Vector2 + Vector4 color
        playerBuffer = new ComputeBuffer(32, 32);

        // Initialize shader
        spaceshipRenderShader = Resources.Load<ComputeShader>("NodeShaders/SpaceshipGamePattern");
        shaderKernel = spaceshipRenderShader.FindKernel("PatternKernel");
        spaceshipRenderShader.SetTexture(shaderKernel, "OutputTex", gameBoardTex);
        spaceshipRenderShader.SetBuffer(shaderKernel, "PlayerBuffer", playerBuffer);
    }

    void Start()
    {
        
    }

    public static float dragFactor = 0.01f;
    public float playerSize = 32;

    void Update()
    {
        // Clear gameboard
        RenderTexture.active = gameBoardTex;
        GL.Clear(true, true, Color.black);
        RenderTexture.active = null;

        foreach (var pair in players.ToList())
        {
            var player = pair.Value;
            player.Update();
            players[pair.Key] = player;
        }

        playerBuffer.SetData(players.Values.ToList());
        spaceshipRenderShader.SetInt("width", gameBoardSize.x);
        spaceshipRenderShader.SetInt("height", gameBoardSize.y);
        spaceshipRenderShader.SetInt("numPlayers", players.Count);
        spaceshipRenderShader.SetFloat("playerSize", playerSize);
        uint tx, ty, tz;
        spaceshipRenderShader.GetKernelThreadGroupSizes(shaderKernel, out tx, out ty, out tz);
        var threadGroupX = Mathf.CeilToInt(((float)gameBoardSize.x) / tx);
        var threadGroupY = Mathf.CeilToInt(((float)gameBoardSize.y) / ty);
        spaceshipRenderShader.Dispatch(shaderKernel, threadGroupX, threadGroupY, 1);
    }

    struct SpaceshipGamePlayer
    {
        public Vector2 position;
        public Vector2 velocity;
        public Color color;

        internal SpaceshipGamePlayer(Vector2 p, Vector2 v, Color c)
        {
            position = p;
            velocity = v;
            color = c;
        }

        public void OnStickInput(Vector2 stick1, Vector2 stick2)
        {
            velocity += stick1;
        }

        public void OnButtonPress(byte buttonId)
        {
            // Do nothing?
        }

        public void OnColorChange(Color32 c)
        {
            color = c;
        }

        public void Update()
        {
            position += velocity;

            // Continuous loop around gameboard in y (Canopy ring)
            if (position.y < 0)
            {
                position.y += gameBoardSize.y;
            } else if (position.y > gameBoardSize.y)
            {
                position.y -= gameBoardSize.y;
            }
            
            // Bounce off at x boundaries?
            if (position.x < 0)
            {
                position.x = Mathf.Abs(position.x);
                velocity.x = Mathf.Abs(velocity.x);
            } else if (position.x > gameBoardSize.x)
            {
                position.x = gameBoardSize.x - (position.x - gameBoardSize.x);
                velocity.x *= -1;
            }
            velocity *= (1 - dragFactor);
        }
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
        var player = new SpaceshipGamePlayer(
            RandomPosition(),
            Vector2.zero,
            Color.black
        );
        players[connection.id] = player;
        Debug.Log($"Received websocket connection with id {connection.id}");
    }

    public void OnClose(WebSocketConnection connection)
    {
        players.Remove(connection.id);
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
            var player = players[conn];
            switch (evt)
            {
                case SpaceshipGameEventType.ChangeColor:
                    var r = data[1];
                    var g = data[2];
                    var b = data[3];
                    Color32 color = new Color32(r, g, b, 255);
                    player.OnColorChange(color);
                    //Debug.Log($"Received ColorChange event for conn {conn} to {color}");
                    break;
                case SpaceshipGameEventType.Update:
                    float data1 = System.BitConverter.ToSingle(data, 1);
                    float data2 = System.BitConverter.ToSingle(data, 5);
                    float data3 = System.BitConverter.ToSingle(data, 9);
                    float data4 = System.BitConverter.ToSingle(data, 13);
                    player.OnStickInput(new Vector2(data1, data2), new Vector2(data3, data4));
                    //Debug.Log($"Received Update event for conn {conn} with data <{data1:0.00}, {data2:0.00}>, <{data3:0.00}, {data4:0.00}");
                    break;
                case SpaceshipGameEventType.Press:
                    var buttonId = data[1];
                    player.OnButtonPress(buttonId);
                    //Debug.Log($"Received Press event for conn {conn} for button {buttonId}");
                    break;
            }
            players[conn] = player;
        }
    }
}