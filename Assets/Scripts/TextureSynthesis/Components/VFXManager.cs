using UnityEngine;
using System.Collections;
using UnityEngine.Video;
using UnityEngine.VFX;
using System.Collections.Generic;

public class VFXManager : MonoBehaviour
{
    public Vector3 StartPhysicalOffset = new Vector3(0, 100, 100);
    public float BoundingCubeScale = 10;
    public int CubeSize = 2;
    public static VFXManager Instance { get; private set;}

    public Vector3Int positionIndex = new Vector3Int(0, 0, 0);
    public List<List<List<VisualEffect>>> Effects= new List<List<List<VisualEffect>>>();

    public Vector3Int NextIndex()
    {
        if (positionIndex.x < CubeSize-1)
        {
            positionIndex += new Vector3Int(1, 0, 0);
        }
        else
        {
            if (positionIndex.y < CubeSize-1)
            {
                positionIndex = new Vector3Int(0, positionIndex.y + 1, positionIndex.z);

            }
            else 
            {
                if (positionIndex.z < CubeSize-1)
                {
                    positionIndex = new Vector3Int(0, 0, positionIndex.z + 1);
                }
                else
                {
                    CubeSize++;

                }
            }
        }
        return Vector3Int.zero;
    }

    public VisualEffect InstantiateEffect(string name)
    {
        return null;
    }

    void Start()
    {
        Instance = this;
    }

}
