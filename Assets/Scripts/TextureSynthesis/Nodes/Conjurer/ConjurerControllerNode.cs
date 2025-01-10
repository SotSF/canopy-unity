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
using static ConjurerControllerNode;

namespace System.Runtime.CompilerServices
{
    // Dummy class to make the compiler happy :')
    // Enables using some C# 9 features like record types
    internal static class IsExternalInit { }
}

[Node(false, "Conjurer/ConjurerApi")]
public class ConjurerControllerNode : TickingNode
{
    public const string ID = "ConjurerControllerNode";
    public override string GetID { get { return ID; } }

    public override string Title { get { return "ConjurerController"; } }

    WebSocket websocket;
    private const string CONJURER_API_URL = "ws://localhost:8081";

    private Vector2 _DefaultSize = new Vector2(250, 180);

    public override Vector2 DefaultSize => _DefaultSize;

    private IEnumerable<ConnectionPort> connectedPorts => dynamicConnectionPorts.Where(port => port.connected());
    private int activePortCount => connectedPorts.Count();
    private int openPortIndex => activePortCount;
    private int targetPortCount => activePortCount + 1;

    [ValueConnectionKnob("repetitionsPerSpiralTurn", Direction.In, "Float")]
    public ValueConnectionKnob repetitionsPerSpiralTurnKnob;

    private void SetSize()
    {
        _DefaultSize = new Vector2(160, (1 + targetPortCount) * 120);
    }

    public enum ConjurerParameterType
    {
        @string,
        number
    }


    public enum ConjurerMode
    {
        disconnected,
        vj,
        emcee,
        experienceCreator
    }
    public ConjurerMode currentConjurerMode = ConjurerMode.emcee;

    [Serializable]
    public class ConjurerApiEvent
    {
        public string @event;
        public Dictionary<string, object> data;
    }

    public record ConjurerApiEventRecord
    {
        public string @event;
    }

    public record ConjurerApiEventData
    {
        public record ConjurerStateUpdate(string Foo) : ConjurerApiEventData();

        private ConjurerApiEventData() { }
    }

    [Serializable]
    public class ConjurerCommandDescription
    {
        public string name;
        public Dictionary<string, ConjurerParameterType> @params;
    }

    [Serializable]
    public class ConjurerCommandInstance : ConjurerApiEvent
    {
        public ConjurerCommandInstance(string commandName, params ConjurerParameterState[] @params)
        {
            @event = "command";
            data = new Dictionary<string, object>
            {
                { "command", commandName },
                { "params", @params }
            };
        }
    }

    [Serializable]
    public class ConjurerModeDescription
    {
        public string name;
        public List<ConjurerCommandDescription> commands;
    }

    [Serializable]
    public class ConjurerParameterDescription : Dictionary<string, string>
    {
        // Description represents the names and types of values to be passed as a paremeter,
        // eg for "update_parameter" command, there is a COMMAND parameter with fields
        // "name" => "string" (the type) and "value" => "number" (the type)
    }

    [Serializable]
    public class ConjurerParameterState : Dictionary<string, object>
    {
        // Parameter state represents the actual value of a parameter, where the V of the KV is
        // probably a float or string, eg "name" => "u_meander_factor", "value" => "0.51", etc
    }

    [Serializable]
    public class ConjurerPatternDescription
    {
        public string name;
        public Dictionary<string, ConjurerParameterState> @params;
    }

    [Serializable]
    public class ConjurerPlaygroundMode : ConjurerModeDescription
    {
        public List<string> patterns_available;
        public ConjurerPatternDescription current_pattern;

        public ConjurerPlaygroundMode()
        {
            name = "playground";
        }
    }

    [Serializable]
    public class ConjurerStateUpdate : ConjurerApiEvent
    {
        public string browser_tab_state;
        public List<string> modes_available;
        public ConjurerModeDescription current_mode;
        public ConjurerStateUpdate()
        {
            @event = "conjurer_state_update";
        }
    }

    public void OnOpenProtocolInit()
    {
        Debug.Log("Conjurer websocket connection opened");
    }

    private void Cleanup()
    {
        websocket.CancelConnection();
        websocket.Close();
    }

    public void OnDestroy()
    {
        Cleanup();
    }

    public void OnDisable()
    {
        Cleanup();
    }

    public void OnConjurerApiEvent(byte[] rawEventBytes)
    {
        var eventMessage = System.Text.Encoding.UTF8.GetString(rawEventBytes);
        ConjurerStateUpdate stateUpdate = JsonConvert.DeserializeObject<ConjurerStateUpdate>(eventMessage);
        Debug.Log($"Conjurer state update received:\n {eventMessage}");
        // Do something with Conjurer message!
    }

    public override void DoInit()
    {
        // Open WebSocket connection to Conjurer API
        websocket = new WebSocket(CONJURER_API_URL);
        websocket.OnOpen += OnOpenProtocolInit;

        websocket.OnError += (e) =>
        {
            Debug.Log("Conjurer websocket error: " + e);
        };

        websocket.OnClose += (e) =>
        {
            WebSocketCloseCode x;
            Debug.Log($"Conjurer websocket connection closed: {e.ToString()}");
        };

        websocket.OnMessage += OnConjurerApiEvent;

        websocket.Connect();
    }

    public void EmceeModeGui()
    {
        string msg = null;
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("Previous track"))
        {
            ConjurerCommandInstance prevCmd = new ConjurerCommandInstance("previous_track");

            msg = JsonConvert.SerializeObject(prevCmd);

        }
        if (GUILayout.Button("Play/Pause"))
        {
            ConjurerCommandInstance playPauseCmd = new ConjurerCommandInstance("toggle_playing");
            msg = JsonConvert.SerializeObject(playPauseCmd);
        }
        if (GUILayout.Button("Next track"))
        {
            ConjurerCommandInstance nextTrackCmd = new ConjurerCommandInstance("next_track");
            msg = JsonConvert.SerializeObject(nextTrackCmd);
        }
        GUILayout.EndHorizontal();

        GUILayout.BeginHorizontal();
        if (GUILayout.Button("Shuffle"))
        {
            ConjurerCommandInstance prevCmd = new ConjurerCommandInstance("shuffle");

            msg = JsonConvert.SerializeObject(prevCmd);

        }
        if (GUILayout.Button("Loop all"))
        {
            ConjurerCommandInstance playPauseCmd = new ConjurerCommandInstance("loop_all");
            msg = JsonConvert.SerializeObject(playPauseCmd);
        }
        if (GUILayout.Button("Restart"))
        {
            ConjurerCommandInstance nextTrackCmd = new ConjurerCommandInstance("restart");
            msg = JsonConvert.SerializeObject(nextTrackCmd);
        }
        GUILayout.EndHorizontal();
        if (msg != null)
        {
            Debug.Log($"Sending message: {msg}");
            openRequests[msg] = websocket.SendText(msg);
        }
    }

    public void VjModeGui()
    {

    }

    public override void NodeGUI()
    {
        GUILayout.BeginVertical();
        // Rate limit requests to Conjurer? Probably not necessary
        MAX_OPEN_REQUESTS = RTEditorGUI.IntField("Max reqs", MAX_OPEN_REQUESTS);
        MAX_REQUESTS_PER_SECOND = RTEditorGUI.IntField("Max rps", MAX_REQUESTS_PER_SECOND);

        GUILayout.Label("WebSocket state: " + Enum.GetName(typeof(WebSocketState), websocket.State));
        if (GUILayout.Button("Reconnect") && websocket.State != WebSocketState.Open)
        {
            websocket.Connect();
        }
        EmceeModeGui();
        //switch (currentConjurerMode)
        //{
        //    case ConjurerMode.disconnected:
        //        break;
        //    case ConjurerMode.vj:
        //        VjModeGui();
        //        break;
        //    case ConjurerMode.emcee:
        //        EmceeModeGui();
        //        break;
        //    case ConjurerMode.experienceCreator:
        //        break;
        //}
        GUILayout.EndVertical();

        if (GUI.changed)
            NodeEditor.curNodeCanvas.OnNodeChange(this);
    }
    public float repetitionsPerSpiralTurn;
    public int MAX_OPEN_REQUESTS = 1;
    public int MAX_REQUESTS_PER_SECOND = 22;
    private float lastRequestTime = 0;
    private Dictionary<string, Task> openRequests = new Dictionary<string, Task>();
    public override bool DoCalc()
    {
        var completedRequests = openRequests.Where(req => req.Value.IsCompleted).ToList();
        foreach (var req in completedRequests)
        {
            //Debug.Log($"Completed request: {req.Key}: {req.Value.Status}");
        }
        openRequests = openRequests.Where(req => !req.Value.IsCompleted).ToDictionary(req => req.Key, req => req.Value);
        //Parameter target: "repetitionsPerSpiralTurn"

        if (repetitionsPerSpiralTurnKnob.connected())
        {
            repetitionsPerSpiralTurn = repetitionsPerSpiralTurnKnob.GetValue<float>();
            ConjurerCommandInstance updateParamCmd = new ConjurerCommandInstance("update_parameter",
                new ConjurerParameterState() { 
                    { "name", "u_repetitionsPerSpiralTurn" },
                    { "value", repetitionsPerSpiralTurn } 
                }
            );
            var msg = JsonConvert.SerializeObject(updateParamCmd);
            openRequests[msg] = websocket.SendText(msg);
        }

        var impliedRps = 1 / (Time.time - lastRequestTime);
        //if (openRequests.Count < MAX_OPEN_REQUESTS && impliedRps < MAX_REQUESTS_PER_SECOND)
        //{
        //    if (websocket.State == WebSocketState.Open)
        //    {

        //        lastRequestTime = Time.time;
        //    }
        //}

        return true;
    }
}
