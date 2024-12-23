﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using NodeEditorFramework.Standard;
using NodeEditorFramework;
using System.Linq;
using SecretFire.TextureSynth;

public class TickingNodeManager : MonoBehaviour
{
    public static TickingNodeManager instance;
    private RTCanvasCalculator canvasCalculator;
    private RTNodeEditor nodeEditor;
    private List<TickingNode> nodesToTick;
    float lastTick = 0;

    void Awake()
    {
        instance = this;
        canvasCalculator = GetComponent<RTCanvasCalculator>();
        nodeEditor = GetComponent<RTNodeEditor>();
        nodesToTick = new List<TickingNode>();
    }

    /* Sets calculated = false (ClearCalculation()) for all subgraphs dependent on 
     * a ticking node, then recalculates them in one go. */
    void TickNodes()
    {
        if (nodeEditor?.workingCanvas != null)
        {
            foreach (var node in nodeEditor.workingCanvas.nodes)
            {
                if (node is TickingNode)
                {
                    nodesToTick.Add((TickingNode)node);
                    node.ClearCalculation();
                }
            }
            foreach (var node in nodesToTick)
            {
                canvasCalculator.ContinueCalculation(node);
            }
            nodesToTick.Clear();
        }
    }

    public float targetFPS = 144;
    void Update()
    {
        if (Time.time - lastTick > 1.0f / targetFPS)
        {
            lastTick = Time.time;
            TickNodes();
        }
    }
}
