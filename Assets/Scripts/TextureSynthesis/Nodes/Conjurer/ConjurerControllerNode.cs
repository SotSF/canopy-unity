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
using Conjurer.Api;


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

    private StateUpdateData conjurerState;

    [ValueConnectionKnob("repetitionsPerSpiralTurn", Direction.In, "Float")]
    public ValueConnectionKnob repetitionsPerSpiralTurnKnob;

    private void SetSize()
    {
        _DefaultSize = new Vector2(280, 100+(dynamicConnectionPorts.Count) * 25);
    }

    public enum ConjurerMode
    {
        disconnected,
        vj,
        emcee,
        experienceCreator
    }
    private ConjurerMode currentConjurerMode = ConjurerMode.disconnected;

    public void OnOpenProtocolInit()
    {
        Debug.Log("Conjurer websocket connection opened");
    }

    private void Cleanup()
    {
        try
        {
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

    public void OnConjurerApiEvent(byte[] rawEventBytes)
    {
        var eventMessage = System.Text.Encoding.UTF8.GetString(rawEventBytes);
        Debug.Log($"Conjurer state update received:\n {eventMessage}");
        try
        {
            ConjurerApiEvent evt = JsonConvert.DeserializeObject<ConjurerApiEvent>(eventMessage);
            if (evt.@event == "conjurer_state_update")
            {
                var parsedData = JsonConvert.DeserializeObject<StateUpdateData>(evt.data.ToString());
                OnConjurerStateUpdate(parsedData);
            }
        }
        catch (Exception e)
        {
            Debug.LogError(e);
        }
    }

    public bool OnConjurerStateUpdate(StateUpdateData stateUpdate)
    {
        conjurerState = stateUpdate;
        Debug.Log($"parsed update current mode:\n {stateUpdate.current_mode}");
        currentConjurerMode = Enum.Parse<ConjurerMode>(stateUpdate.current_mode.name);
        switch (currentConjurerMode)
        {
            case ConjurerMode.disconnected:
                break;
            case ConjurerMode.vj:
                OnPatternSelect(stateUpdate.current_mode.current_pattern);
                break;
            case ConjurerMode.emcee:
                break;
            case ConjurerMode.experienceCreator:
                break;  
        }
        Debug.Log($"Set mode to {Enum.GetName(typeof(ConjurerMode), currentConjurerMode)}");
        return false;
    }

    private PatternData currentPattern;
    private bool[] patternValChanged;
    private float[] patternParamVals;
    private bool[] transmitPatternData;

    private bool doUpdatePorts = false;

    public void OnPatternSelect(PatternData pattern)
    {
        // Update ports etc, initialize arrays
        currentPattern = pattern;
        doUpdatePorts = true;
    }

    public void UpdatePorts()
    {
        var numParams = currentPattern.@params.Count;

        patternValChanged = new bool[numParams];
        patternParamVals = new float[numParams];
        transmitPatternData = new bool[numParams];

        // Add / rename ports
        for (int i = 0; i < numParams; i++)
        {
            var param = currentPattern.@params.ElementAt(i).Value;
            patternParamVals[i] = param.value;
            transmitPatternData[i] = false;
            if (i < dynamicConnectionPorts.Count)
            {
                dynamicConnectionPorts[i].name = param.name;
            }
            else
            {
                CreateValueConnectionKnob(new ValueConnectionKnobAttribute(param.name, Direction.In, typeof(float), NodeSide.Left));
            }
        }
        // Remove excess ports
        if (numParams < dynamicConnectionPorts.Count)
        {
            for (int i = dynamicConnectionPorts.Count - 1; i > numParams - 1; i--)
            {
                DeleteConnectionPort(i);
            }
        }
        SetSize();
        doUpdatePorts = false;
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

    private void PatternParamGui(PatternParameter param, int paramIdx)
    {
        var curVal = patternParamVals[paramIdx];
        var knob = (ValueConnectionKnob)dynamicConnectionPorts[paramIdx];
        GUILayout.BeginHorizontal();
        if (param.min.HasValue && param.max.HasValue)
        {
            FloatKnobOrSlider(ref curVal, param.min.Value, param.max.Value, knob);
        }
        else
        {
            FloatKnobOrField(GUIContent.none, ref curVal, knob, GUILayout.Width(60));
        }
        if (curVal != patternParamVals[paramIdx])
        {
            patternValChanged[paramIdx] = true;
            patternParamVals[paramIdx] = curVal;
        }
        transmitPatternData[paramIdx] = GUILayout.Toggle(transmitPatternData[paramIdx], "🆒");
        GUILayout.FlexibleSpace();
        GUILayout.EndHorizontal();
    }

    private void VjModeGui()
    {
        GUILayout.BeginVertical();
        for (int i = 0; i < currentPattern.@params.Count; i++)
        {
            var param = currentPattern.@params.ElementAt(i).Value;
            PatternParamGui(param, i);
            GUILayout.Space(3);
        }

        GUILayout.EndVertical();
    }

    public void EmceeModeGui()
    {
        string msg = null;
        GUILayout.BeginHorizontal();
        //if (GUILayout.Button("Restart"))
        //{
        //    ConjurerCommandInstance nextTrackCmd = new ConjurerCommandInstance("restart");
        //    msg = JsonConvert.SerializeObject(nextTrackCmd);
        //}
        GUILayout.EndHorizontal();
        if (msg != null)
        {
            Debug.Log($"Sending message: {msg}");
            websocket.SendText(msg);
        }
    }

    public override void NodeGUI()
    {
        if (doUpdatePorts)
        {
            UpdatePorts();
        }
        GUILayout.BeginVertical();

        GUILayout.Label("WebSocket state: " + Enum.GetName(typeof(WebSocketState), websocket.State));
        if (GUILayout.Button("Reconnect") && websocket.State != WebSocketState.Open)
        {
            websocket.Connect();
        }
        switch (currentConjurerMode)
        {
            case ConjurerMode.disconnected:
                break;
            case ConjurerMode.vj:
                VjModeGui();
                break;
            case ConjurerMode.emcee:
                EmceeModeGui();
                break;
            case ConjurerMode.experienceCreator:
                break;
        }
        GUILayout.EndVertical();

        if (GUI.changed)
            NodeEditor.curNodeCanvas.OnNodeChange(this);
    }

    public override bool DoCalc()
    {
        if (doUpdatePorts)
        {
            UpdatePorts();
        }
        switch (currentConjurerMode)
        {
            case ConjurerMode.disconnected:
                break;
            case ConjurerMode.vj:
                var paramUpdates = new List<PatternParameter>();
                for (int i = 0; i < currentPattern?.@params.Count; i++)
                {
                    if (dynamicConnectionPorts[i].connected())
                    {
                        var knobVal = ((ValueConnectionKnob)dynamicConnectionPorts[i]).GetValue<float>();
                        if (knobVal != patternParamVals[i])
                        {
                            patternValChanged[i] = true;
                            patternParamVals[i] = knobVal;
                        }
                    }
                    if (transmitPatternData[i] && patternValChanged[i])
                    {
                        var paramId = currentPattern.@params.ElementAt(i).Key;
                        var param = currentPattern.@params.ElementAt(i).Value;
                        var val = patternParamVals[i];
                        paramUpdates.Add(new PatternParameter(paramId, val));
                        patternValChanged[i] = false;
                    }
                }
                if (paramUpdates.Count > 0)
                {
                    CommandMessage updateParams = new CommandMessage("update_parameter", paramUpdates.ToArray());
                    var msgJson = JsonConvert.SerializeObject(updateParams);
                    websocket.SendText(msgJson);
                }
                break;
            case ConjurerMode.emcee:
                break;
            case ConjurerMode.experienceCreator:
                break;
        }

        return true;
    }
}
