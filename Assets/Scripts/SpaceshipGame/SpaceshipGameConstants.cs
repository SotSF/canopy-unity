using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using UnityEngine;

using WebSocketServer;

public class SpaceshipGameConstants : Singleton<SpaceshipGameConstants>
{
    public Vector2Int gameBoardSize = new Vector2Int(512, 512);
    public Vector2 gameBoardCenter = new Vector2(255, 255);
    public float velocityScaling = 0.25f;
    public float maxSpeed = 5f;
    public float velocityScalingFactor = .5f;
    // 16 feet (Canopy physical size) to meters (/2 for radius)
    public float boundaryRadius = 4.8768f / 2;
    public float frictionFactor = 0.99f;
    public float shipDragFactor = 0.005f;
    public float playerSize = 2;
    public float projectileInitialSpeed = 2;
    public float projectileDragFactor = 0.001f;

}