using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using UnityEngine;

using WebSocketServer;

public class ConjurerController : MonoBehaviour
{
    private static Vector2Int textureSize = new Vector2Int(96, 150);
    private static Vector2 gameBoardCenter = new Vector2(255, 255);

    public static ConjurerController instance;

    public Texture2D inputTex;

    int gameBoardKernel;

    public void Awake()
    {
        if (instance != null)
        {
            Cleanup();
            Destroy(instance);
        }
        instance = this;

        // Create texture
        inputTex = new Texture2D(textureSize.x, textureSize.y, TextureFormat.RGBA32, false);
        inputTex.filterMode = FilterMode.Point;
        inputTex.wrapModeU = TextureWrapMode.Repeat;
        inputTex.wrapModeV = TextureWrapMode.Clamp;
    }

    private void Cleanup()
    {

    }

    public void OnDestroy()
    {
        Cleanup();
    }

    public void OnDisable()
    {
        Cleanup();
    }

    public static float dragFactor = 0.005f;
    public float playerSize = 2;

    void Update()
    {
        // Clear gameboard


        //uint tx, ty, tz;

        //spaceshipRenderShader.GetKernelThreadGroupSizes(gameBoardKernel, out tx, out ty, out tz);
        //var threadGroupX = Mathf.CeilToInt(((float)textureSize.x) / tx);
        //var threadGroupY = Mathf.CeilToInt(((float)textureSize.y) / ty);
        //spaceshipRenderShader.Dispatch(gameBoardKernel, threadGroupX, threadGroupY, 1);

        //spaceshipRenderShader.GetKernelThreadGroupSizes(fluidVelocityKernel, out tx, out ty, out tz);
        //threadGroupX = Mathf.CeilToInt(((float)fluidVelocityTex.width) / tx);
        //threadGroupY = Mathf.CeilToInt(((float)fluidVelocityTex.height) / ty);
        //spaceshipRenderShader.Dispatch(fluidVelocityKernel, threadGroupX, threadGroupY, 1);
    }

    public void OnOpen(WebSocketConnection connection)
    {
        Debug.Log($"Received websocket connection open with id {connection.id}");
    }

    public void OnClose(WebSocketConnection connection)
    {
        Debug.Log($"Received websocket connection close for id {connection.id}");
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

    enum ConjurerMessageType
    {
        InitializeTexture = 1,
        TransmitFrame,
        InitializeParameters,
        TransmitParameters
    }

    struct ConjurerWebsocketMessage
    {
        int width;
        int height;
        int namelength;
        char[] name;
    }

    // Current setup:
    //   - Hardcoded 96x150
    //

    public void OnMessage(WebSocketMessage message)
    {
        //message.connection;
        if (message.text != null)
            Debug.Log(message.text);
        if (message.rawdata != null)
        {
            //ConjurerMessageType evt = (ConjurerMessageType)message.rawdata[0];
            var data = message.rawdata;
            inputTex.LoadRawTextureData(data);
            inputTex.Apply();
            //switch (evt)
            //{
            //    case ConjurerMessageType.InitializeTexture:
            //        var r = data[1];
            //        var g = data[2];
            //        var b = data[3];
            //        Color32 color = new Color32(r, g, b, 255);
            //        //Debug.Log($"Received ColorChange event for conn {conn} to {color}");
            //        break;
            //    case ConjurerMessageType.TransmitFrame:
            //        float data1 = System.BitConverter.ToSingle(data, 1);
            //        float data2 = System.BitConverter.ToSingle(data, 5);
            //        float data3 = System.BitConverter.ToSingle(data, 9);
            //        float data4 = System.BitConverter.ToSingle(data, 13);
            //        player.OnStickInput(new Vector2(data1, data2), new Vector2(data3, data4));
            //        //Debug.Log($"Received Update event for conn {conn} with data <{data1:0.00}, {data2:0.00}>, <{data3:0.00}, {data4:0.00}");
            //        break;
            //    case InitializeParameters.Press:
            //        var buttonId = data[1];
            //        player.OnButtonPress(buttonId);
            //        //Debug.Log($"Received Press event for conn {conn} for button {buttonId}");
            //        break;
            //}
            //players[conn] = player;
        }
    }
}