using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using NodeEditorFramework.Standard;
using NodeEditorFramework;

public class TickingNodeManager : MonoBehaviour
{
    public static TickingNodeManager instance;
    private RTCanvasCalculator calc;
    private HashSet<Node> nodeSet;
    float lastTick = 0;

    // Start is called before the first frame update
    void Awake()
    {
        instance = this;
        calc = GetComponent<RTCanvasCalculator>();
        nodeSet = new HashSet<Node>();
    }

    // Update is called once per frame
    void Update()
    {
        if (Time.time - lastTick > 1.0f / 60) {
            lastTick = Time.time;
            foreach (var node in nodeSet) {
                node.ClearCalculation();
                calc.ContinueCalculation(node);
            }
        }
    }

    public void Register(Node node)
    {
        if (!nodeSet.Contains(node)) {
            nodeSet.Add(node);
        }
    }
}
