using UnityEngine;
using System.Collections;
using System.Linq;
using System;
using System.Collections.Generic;
using Lightsale.Animation;
using Lightsale.Utility;

public class Canopy: MonoBehaviour
{
    public static Canopy instance;

    public Mesh pixelBase;
    public int renderTextureSize = 128;
    public Material canopyMaterial;

    public Transform start;
    public Transform end;

    private Transform pixels;
    private Coroutine animRoutine;
    private Quaternion originalRotation;

    private Vector3 controlViewCanopyPosition = new Vector3(.75f, 1.6f, 3.3f);
    private Vector3 controlViewCameraPosition = new Vector3(0, 1.6f, 0);

    public int numStrips = 96;
    public int pixelsPerStrip = 75;
    
    const int pixelsPerMeter = 30;

    const int maxVerts = 65000;

    //private int numLEDs = numStrips * pixelsPerStrip;

    const float apexRadius = 0.332f;
    private void Awake()
    {
        originalRotation = Quaternion.identity;
        instance = this;
    }
    public void EnterSimulationMode()
    {
        Controls.instance.ResetRotation();
        originalRotation = Quaternion.identity;
        var rotate = Animations.LocalQuatLerp(transform, originalRotation);
        var trans = Animations.LocalPositionLerp(transform, new Vector3(0, 4, 0));
        this.CheckedRoutine(ref animRoutine, Animations.CubicTimedAnimator(1.2f, rotate, trans));
    }
    public void EnterControllerMode()
    {
        //originalRotation = Quaternion.Euler(90, 0, 0);
        Controls.instance.ResetRotation();
        originalRotation = Quaternion.LookRotation(controlViewCanopyPosition - controlViewCameraPosition) * Quaternion.Euler(90,0,0);
        var rotate = Animations.LocalQuatLerp(transform, originalRotation);
        var trans = Animations.LocalPositionLerp(transform, controlViewCanopyPosition);
        this.CheckedRoutine(ref animRoutine, Animations.CubicTimedAnimator(1.2f, rotate, trans));
    }
    public void UpdateRotation(Quaternion rotation)
    {
        transform.localRotation = originalRotation * rotation;
    }

    private Vector3 PixelToOffset(int stripIndex, int pixelIndex)
    {
        float distance = (1f / pixelsPerMeter) * pixelIndex;
        return new Vector3(0,0,distance);
    }

    private MeshFilter GeneratePixelMeshGameObject(Transform parent, int meshcount)
    {
        GameObject pixelObject = new GameObject("pixelmesh-" + meshcount);
        pixelObject.transform.parent = parent;
        pixelObject.transform.localPosition = Vector3.zero;
        MeshRenderer renderer = pixelObject.AddComponent<MeshRenderer>();
        MeshFilter filter = pixelObject.AddComponent<MeshFilter>();
        renderer.sharedMaterial = canopyMaterial;
        return filter;
    }

    private void SaveMesh(MeshFilter filter, List<Vector3> verts, List<Vector2> uvs, List<int> tris)
    {
        Mesh mesh = new Mesh
        {
            vertices = verts.ToArray(),
            uv = uvs.ToArray(),
            triangles = tris.ToArray()
        };
        filter.sharedMesh = mesh;
        verts.Clear();
        uvs.Clear();
        tris.Clear();
    }

    public void ClearStrips()
    {
        if (pixels == null)
        {
            pixels = transform.Find("Apex/Pixels");
        }
        int count = pixels.childCount;
        List<Transform> children = new Transform[count].Select( (t, i) => pixels.GetChild(i)).ToList();
        foreach( Transform child in children)
        {
            if (child.name.StartsWith("pixelmesh"))
            {
                DestroyImmediate(child.gameObject);
            }
        }
    }

    public void RotatePixelBase()
    {
        Quaternion rotation = Quaternion.Euler(0, 90, 0);
        pixelBase.vertices = pixelBase.vertices.Select(v => rotation * v).ToArray();
    }

    private IEnumerable<Vector2> GetUVs(int pixelIndex, int stripIndex)
    {
        //int low = 0;
        //int high = 20;

        float u = (float)pixelIndex / (pixelsPerStrip-1);
        float v = (float)stripIndex / (numStrips-1);
        Vector2 emissiveUV = new Vector2(u, v);
        //Vector2 dimUV = new Vector2(1, 1);
        //return new Vector2[pixelBase.vertexCount].Select((x,i) => i >= low && i < high ? emissiveUV : dimUV);
        return new Vector2[pixelBase.vertexCount].Select((x, i) => emissiveUV);
    }

    public void GenerateStrips()
    {
        pixels = transform.Find("Apex/Pixels");

        int meshcount = 0;

        MeshFilter filter = GeneratePixelMeshGameObject(pixels, meshcount);

        List<Vector3> verts = new List<Vector3>();
        List<Vector2> uvs = new List<Vector2>();
        List<int> tris = new List<int>();

        Vector2[] catenary = MathUtils.Catenary(Vector2.zero, new Vector2(end.position.x-start.position.x, end.position.y-start.position.y), 2.5f, 75);

        for (int stripIndex = 0; stripIndex < numStrips; stripIndex++)
        {
            for (int pixelIndex = 0; pixelIndex < pixelsPerStrip; pixelIndex++)
            {
                var numverts = verts.Count;
                uvs.AddRange(GetUVs(pixelIndex, stripIndex));
                verts.AddRange(GetVerts(stripIndex, pixelIndex, catenary));;
                tris.AddRange(pixelBase.triangles.Select(x => x + numverts));
            }
            if (verts.Count >= maxVerts - (pixelBase.vertexCount * 75))
            {
                SaveMesh(filter, verts, uvs, tris);
                meshcount++;
                filter = GeneratePixelMeshGameObject(pixels, meshcount);
            }
        }
        SaveMesh(filter, verts, uvs, tris);
    }

    private IEnumerable<Vector3> GetVerts(int stripIndex, int pixelIndex, Vector2[] catenaryOffsets)
    {
        Quaternion stripRotation = GetRotation(stripIndex);
        //int a = pixelIndex > 0 ? pixelIndex - 1 : pixelIndex;
        //int b = pixelIndex > 0 ? pixelIndex : pixelIndex + 1;
        //var diff = catenaryOffsets[b] - catenaryOffsets[a];

        //float angle = -Mathf.Atan2(diff.y, diff.x) * Mathf.Rad2Deg;
        
        //Quaternion catenaryRotation = Quaternion.Euler(0, 0, 0);
        //Vector3 offset = PixelToOffset(stripIndex, pixelIndex);
        Vector3 offset = new Vector3(0, catenaryOffsets[pixelIndex].y, catenaryOffsets[pixelIndex].x) + apexRadius*Vector3.forward;
        //var newverts = pixelBase.vertices.Select(vert => stripRotation * ((catenaryRotation * vert) + offset));
        var newverts = pixelBase.vertices.Select(vert => stripRotation * (vert + offset));
        return newverts;
    }

    private Quaternion GetRotation(int stripIndex)
    {
        return Quaternion.Euler(0, ((float)360*stripIndex)/numStrips, 0);
    }
}
