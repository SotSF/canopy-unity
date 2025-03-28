﻿using NodeEditorFramework;
using NodeEditorFramework.TextureComposer;
using NodeEditorFramework.Utilities;

using SecretFire.TextureSynth;

using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Net;

using UnityEngine;
using UnityEngine.Networking;
using System.Threading;

[Node(false, "Signal/API")]
public class ApiNode : TickingNode
{
    public const string ID = "apiNode";
    public override string GetID { get { return ID; } }

    public override string Title { get { return "API"; } }

    private Vector2 _DefaultSize = new Vector2(180, 100);
    public override Vector2 DefaultSize => _DefaultSize;

    public override bool AutoLayout => true;

    [ValueConnectionKnob("PollHZ", Direction.In, typeof(float))]
    public ValueConnectionKnob pollingInputKnob;

    [ValueConnectionKnob("Endpoint", Direction.In, typeof(string))]
    public ValueConnectionKnob endpointInputKnob;

    private float lastCheck = 0;

    public int activeSignalIndex = 0;
    public string endpoint = "http://localhost:5000/api/values";
    public float pollhz;

    public bool check;

    private Dictionary<string, float> values;
    private Dictionary<string, ValueConnectionKnob> outKnobs;

    private List<UnityWebRequest> requests;
    private const int maxOpenRequests = 20;

#pragma warning disable 0649
    [System.Serializable]
    struct ApiResponse
    {
        public List<ApiValuePair> values;
    }

    [System.Serializable]
    struct ApiValuePair
    {
        public string name;
        public float value;
    }
#pragma warning restore 0649
    public override void DoInit()
    {
        outKnobs = new Dictionary<string, ValueConnectionKnob>();
        values = new Dictionary<string, float>();
        requests = new List<UnityWebRequest>();
    }

    private void ProcessResponse(string apiResponse)
    {
        var responseObj = JsonUtility.FromJson<ApiResponse>(apiResponse);
        bool changed = false;
        Debug.LogFormat("Parsed response: {0}", responseObj.values);
        if (responseObj.values.Count == dynamicConnectionPorts.Count)
        {
            for (int i = 0; i < responseObj.values.Count; i++)
            {
                if (dynamicConnectionPorts[i].name != responseObj.values[i].name)
                {
                    changed = true;
                    break;
                }
            }
        } else
        {
            changed = true;
        }
        if (changed)
        {
            for (int i = dynamicConnectionPorts.Count-1; i >= 0; i--)
            {
                DeleteConnectionPort(i);
            }
            outKnobs.Clear();
            foreach (var pair in responseObj.values)
            {
                ValueConnectionKnobAttribute outKnobAttribs = new ValueConnectionKnobAttribute(pair.name, Direction.Out, typeof(float));
                var knob = CreateValueConnectionKnob(outKnobAttribs);
                outKnobs[pair.name] = knob;
            }
            _DefaultSize = new Vector2(180, (1 + dynamicConnectionPorts.Count) * 100);
        } 
        {
            foreach (var pair in responseObj.values)
            {
                values[pair.name] = pair.value;
            }
        }
    }

    public override void NodeGUI()
    {
        GUILayout.BeginHorizontal();
        GUILayout.BeginVertical();
        pollingInputKnob.DisplayLayout(new GUIContent("PollHZ", "Number of times per second to poll the endpoint"));
        if (!pollingInputKnob.connected())
        {
            pollhz = RTEditorGUI.Slider(pollhz, 1, 30);
        }
        endpointInputKnob.DisplayLayout(new GUIContent("Endpoint", "API endpoint to poll"));
        if (!endpointInputKnob.connected())
        {
            endpoint = RTEditorGUI.TextField(endpoint);
        }
        check = RTEditorGUI.Toggle(check, "Update");
        GUILayout.EndVertical();
        //Dynamic JSON output here

        GUILayout.BeginVertical();
        for (int i = 0; i < dynamicConnectionPorts.Count ; i++)
        {
            GUILayout.BeginHorizontal();
            var port = (ValueConnectionKnob)dynamicConnectionPorts[i];
            GUILayout.Space(4);
            GUILayout.Label(string.Format("{0}: {1:0.00}", port.name, port.GetValue<float>()));
            port.SetPosition();
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
        }
        GUILayout.EndVertical();

        GUILayout.EndHorizontal();

        if (GUI.changed)
            NodeEditor.curNodeCanvas.OnNodeChange(this);
    }

    void Request(string endpoint)
    {
        var requestObj = UnityWebRequest.Get(endpoint);
        var requestTask = requestObj.SendWebRequest();
        //requestObj.Send();
        requests.Add(requestObj);
    }

    public override bool DoCalc()
    {
        if (endpoint == null || endpoint == "")
        {
            Debug.LogFormat("No endpoint");
            return true;
        }

        if (check && Time.time - lastCheck > (1 / pollhz))
        {
            if (requests.Count < maxOpenRequests)
            {
                Request(endpoint);
                lastCheck = Time.time;
            }
            else
            {
                this.TimedDebug($"APINode stopping outbound requests, exceeded max open requests of {maxOpenRequests}");
            }
        }

        for (int i = 0; i < requests.Count; i++)
        {
            if (requests[i].isDone)
            {
                ProcessResponse(requests[i].downloadHandler.text);
                requests.RemoveAt(i);
                break;
            }
        }

        foreach (var pair in outKnobs)
        {
            pair.Value.SetValue(values[pair.Key]);
        }
        return true;
    }
}