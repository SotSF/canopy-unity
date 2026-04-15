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
    private Dictionary<string, SpaceshipGamePlayer> players;

    public RenderTexture gameBoardTex;
    public RenderTexture fluidVelocityTex;

    private ComputeBuffer playerBuffer;
    private ComputeShader spaceshipRenderShader;
    int gameBoardKernel;
    int fluidVelocityKernel;


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

        fluidVelocityTex = new RenderTexture(gameBoardSize.x, gameBoardSize.y, 0);
        fluidVelocityTex.useMipMap = false;
        fluidVelocityTex.autoGenerateMips = false;
        fluidVelocityTex.enableRandomWrite = true;
        fluidVelocityTex.filterMode = FilterMode.Point;
        fluidVelocityTex.wrapModeU = TextureWrapMode.Repeat;
        fluidVelocityTex.wrapModeV = TextureWrapMode.Clamp;
        fluidVelocityTex.Create();

        // 32 instances, 32 bytes for 2x Vector2 + Vector4 color
        playerBuffer = new ComputeBuffer(32, 32);

        // Initialize shader
        spaceshipRenderShader = Resources.Load<ComputeShader>("NodeShaders/SpaceshipGamePattern");
        gameBoardKernel = spaceshipRenderShader.FindKernel("GameBoardKernel");
        fluidVelocityKernel = spaceshipRenderShader.FindKernel("FluidVelocityKernel");
        spaceshipRenderShader.SetTexture(gameBoardKernel, "GameboardTex", gameBoardTex);
        spaceshipRenderShader.SetBuffer(gameBoardKernel, "PlayerBuffer", playerBuffer);
        spaceshipRenderShader.SetTexture(fluidVelocityKernel, "FluidVelocityTex", fluidVelocityTex);
        spaceshipRenderShader.SetBuffer(fluidVelocityKernel, "PlayerBuffer", playerBuffer);
    }

    void Start()
    {
        
    }

    public static float dragFactor = 0.005f;
    public float playerSize = 2;

    void Update()
    {
        // Clear gameboard
        RenderTexture.active = gameBoardTex;
        GL.Clear(true, true, Color.black);
        RenderTexture.active = fluidVelocityTex;
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
        spaceshipRenderShader.SetFloat("maxSpeed", maxSpeed);
        spaceshipRenderShader.SetFloat("innerRingDist", innerRingDist);
        spaceshipRenderShader.SetFloat("outerRingDist", outerRingDist);

        uint tx, ty, tz;

        spaceshipRenderShader.GetKernelThreadGroupSizes(gameBoardKernel, out tx, out ty, out tz);
        var threadGroupX = Mathf.CeilToInt(((float)gameBoardSize.x) / tx);
        var threadGroupY = Mathf.CeilToInt(((float)gameBoardSize.y) / ty);
        spaceshipRenderShader.Dispatch(gameBoardKernel, threadGroupX, threadGroupY, 1);

        spaceshipRenderShader.GetKernelThreadGroupSizes(fluidVelocityKernel, out tx, out ty, out tz);
        threadGroupX = Mathf.CeilToInt(((float)fluidVelocityTex.width) / tx);
        threadGroupY = Mathf.CeilToInt(((float)fluidVelocityTex.height) / ty);
        spaceshipRenderShader.Dispatch(fluidVelocityKernel, threadGroupX, threadGroupY, 1);
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
            velocity += stick1/instance.velocityScaling;
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
            float r = Vector2.Distance(gameBoardCenter, position);
            float theta = Mathf.Atan2(gameBoardCenter.y - position.y, gameBoardCenter.x - position.x);

            // Reflect off apex
            if (r < instance.innerRingDist)
            {
                velocity = Vector2.Reflect(velocity, (gameBoardCenter-position).normalized);
            }
            // Reflect off outer edge
            else if (r >= instance.outerRingDist)
            {
                velocity = Vector2.Reflect(velocity, (position-gameBoardCenter).normalized);
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