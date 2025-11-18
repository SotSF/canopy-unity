using NodeEditorFramework;
using SecretFire.TextureSynth;
using UnityEngine;
using NativeWebSocket;
using System;
using Newtonsoft.Json;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Linq;
using NodeEditorFramework.Utilities;


[Node(false, "Signal/PixLite")]
public class PixLiteNode : TickingNode
{
    public const string ID = "PixLiteNode";
    public override string GetID { get { return ID; } }

    public override string Title { get { return "PixLite"; } }


    WebSocket websocket;

    public float prevBrightness;
    public float brightness;

    [ValueConnectionKnob("Brightness", Direction.In, "Float")]
    public ValueConnectionKnob brightnessKnob;

    private Vector2 _DefaultSize = new Vector2(180, 180);



    public override Vector2 DefaultSize => _DefaultSize;

    public enum PixliteProtocolRequestType
    {
        constantRead = 2,
        configRead,
        statisticRead,
        statusRead,
        statisticSub,
        fileList,
        configChange
    }

    [Serializable]
    public class PixliteRequestParams
    {
    }

    [Serializable]
    public class PixlitePathParams : PixliteRequestParams
    {
        public List<string> path;
    }

    [Serializable]
    public class  PixliteStatSubParams : PixlitePathParams
    {
        public bool sub;
        public int period;
    }

    [Serializable]
    public class PixliteFileListParams : PixliteRequestParams
    {
        public List<string> pattern;
    }

    [Serializable]
    public class PixlitePixConfig
    {
        public string pixType; //: "SK9822",
        public string colorType; //: "RGB",
        public int freq; //: 2500,
        public bool expand; //: false,
        public string inFormat; //: "8Bit",
        public bool gammaOn; //: false,
        public bool ditherOn; //: false,
        public bool holdLastFrm; //: false,
        public int curCtrlGbl; //: 5
    }

    [Serializable]
    public class PixliteConfigChangeParams : PixliteRequestParams
    {
        public string action;
        public Dictionary<string, PixlitePixConfig> config;
    }

    [Serializable]
    public class PixliteProtocolRequest
    {
        public string req;
        public PixliteProtocolRequestType id;
        [JsonProperty(PropertyName = "params")]
        public PixliteRequestParams parameters;

        public PixliteProtocolRequest(PixliteProtocolRequestType reqType, PixliteRequestParams parameters)
        {
            req = Enum.GetName(typeof(PixliteProtocolRequestType), reqType);
            id = reqType;
            this.parameters = parameters;
        }
    }

    public void OnOpenProtocolInit()
    {
        Debug.Log("Pixlite websocket connection opened");
        PixliteProtocolRequest constRead = new PixliteProtocolRequest(
            PixliteProtocolRequestType.constantRead,
            new PixlitePathParams { path = new List<string> { "" } }
        );

        PixliteProtocolRequest configRead = new PixliteProtocolRequest(
            PixliteProtocolRequestType.configRead,
            new PixlitePathParams { path = new List<string> { "" } }
        );

        PixliteProtocolRequest statisticRead = new PixliteProtocolRequest(
            PixliteProtocolRequestType.statisticRead,
            new PixlitePathParams { path = new List<string> { "" } }
        );

        PixliteProtocolRequest statusRead = new PixliteProtocolRequest(
            PixliteProtocolRequestType.statusRead,
            new PixlitePathParams { path = new List<string> { "" } }
        );

        PixliteProtocolRequest statisticSub = new PixliteProtocolRequest(
            PixliteProtocolRequestType.statisticSub,
            new PixliteStatSubParams { path = new List<string> { "" }, sub = true, period = 1 }
        );

        PixliteProtocolRequest fileList = new PixliteProtocolRequest(
            PixliteProtocolRequestType.fileList,
            new PixliteFileListParams { pattern = new List<string> { "*" } }
        );

        var openingProtocolRequests = new List<PixliteProtocolRequest> {
            constRead,
            configRead,
            statisticRead,
            statusRead,
            statisticSub,
            fileList
        };

        foreach (var request in openingProtocolRequests)
        {
            var msg = JsonConvert.SerializeObject(request);
            openRequests[msg] = websocket.SendText(msg);
        }
    }

    private void Cleanup()
    {
        try
        {
            if (websocket == null)
                return;
            websocket.CancelConnection();
            websocket.Close();
        }
        catch (Exception e)
        {
            Debug.LogError(e);
        }
    }

    public void OnDestroy()
    {
        Cleanup();
    }

    public void OnDisable()
    {
        Cleanup();
    }

    public override void DoInit()
    {
        // Open WebSocket connection to PixLite
        websocket = new WebSocket("ws://192.168.1.71/v1.5?user=admin&auth=47DEQpj8HBSa-_TImW-5JCeuQeRkm5NMpJWZG3hSuFU",
            headers: new Dictionary<string, string>
            {
                { "Host", "192.168.1.71" },
                { "User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64; rv:132.0) Gecko / 20100101 Firefox / 132.0" },
                { "Accept", "*/*" },
                { "Accept-Language", "en-US,en;q=0.5" },
                { "Accept-Encoding", "gzip, deflate" },
                { "Sec-WebSocket-Version", "13" },
                { "Origin", "http://192.168.1.71" },
                { "Sec-WebSocket-Extensions", "permessage-deflate" },
                { "Sec-WebSocket-Key", "rc7NY1dy8mQ7Sxhyoy+wrQ==" },
                { "Connection", "keep-alive, Upgrade" },
                { "Pragma", "no-cache" },
                { "Cache-Control", "no-cache" },
                { "Upgrade", "websocket" }
            }
        );
        websocket.OnOpen += OnOpenProtocolInit;

        websocket.OnError += (e) =>
        {
            Debug.Log("Pixlite websocket error: " + e);
        };

        websocket.OnClose += (e) =>
        {
            WebSocketCloseCode x;
            Debug.Log($"Pixlite websocket connection closed: {e.ToString()}");
        };

        websocket.OnMessage += (bytes) =>
        {
            //var message = System.Text.Encoding.UTF8.GetString(bytes);
            //Debug.Log("Pixlite websocket message: " + message);
        };

        websocket.Connect();
    }

    public override void NodeGUI()
    {
        GUILayout.BeginVertical();
        FloatKnobOrSlider(ref brightness, 0, 1, brightnessKnob);
        MAX_OPEN_REQUESTS = RTEditorGUI.IntField("Max reqs", MAX_OPEN_REQUESTS);
        MAX_REQUESTS_PER_SECOND = RTEditorGUI.IntField("Max rps", MAX_REQUESTS_PER_SECOND);
        GUILayout.Label("Brightness: " + brightness);
        GUILayout.Label("Brightness to PixLite: " + (int)(brightness * 31));
        GUILayout.Label("WebSocket state: " + Enum.GetName(typeof(WebSocketState), websocket.State));
        if (GUILayout.Button("Reconnect") && websocket.State != WebSocketState.Open)
        {
            websocket.Connect();
        }
        GUILayout.EndVertical();

        if (GUI.changed)
            NodeEditor.curNodeCanvas.OnNodeChange(this);
    }

    public int MAX_OPEN_REQUESTS = 1;
    public int MAX_REQUESTS_PER_SECOND = 22;
    private float lastRequestTime = 0;
    private Dictionary<string, Task> openRequests = new Dictionary<string, Task>();
    public override bool DoCalc()
    {
        if (brightnessKnob.connected())
        {
            brightness = brightnessKnob.GetValue<float>();
        }
        // Scale brightness to 0-31
        var brightnessInt = (int)(brightness * 31);
        var prevBrightnessInt = (int)(prevBrightness * 31);

        var completedRequests = openRequests.Where(req => req.Value.IsCompleted).ToList();
        foreach (var req in completedRequests)
        {
            //Debug.Log($"Completed request: {req.Key}: {req.Value.Status}");
        }
        openRequests = openRequests.Where(req => !req.Value.IsCompleted).ToDictionary(req => req.Key, req => req.Value);

        var impliedRps = 1 / (Time.time - lastRequestTime);
        if (
            brightnessInt != prevBrightnessInt 
            && openRequests.Count < MAX_OPEN_REQUESTS 
            && impliedRps < MAX_REQUESTS_PER_SECOND)
        {
            prevBrightness = brightness;
            if (websocket.State == WebSocketState.Open)
            {
                PixliteProtocolRequest brightnessChange = new PixliteProtocolRequest(
                    PixliteProtocolRequestType.configChange,
                    new PixliteConfigChangeParams
                    {
                        action = "apply",
                        config = new Dictionary<string, PixlitePixConfig>
                        {
                            {
                                "pix",
                                new PixlitePixConfig
                                {
                                    pixType = "SK9822",
                                    colorType = "RGB",
                                    freq = 2500,
                                    expand = false,
                                    inFormat = "8Bit",
                                    gammaOn = false,
                                    ditherOn = false,
                                    holdLastFrm = false,
                                    curCtrlGbl = brightnessInt
                                }
                            }
                        }
                    }
                );
                var msg = JsonConvert.SerializeObject(brightnessChange);
                //Debug.Log($"Sending message: {msg}");
                openRequests[msg] = websocket.SendText(msg);
                lastRequestTime = Time.time;
            }
        }

        return true;
    }
}
