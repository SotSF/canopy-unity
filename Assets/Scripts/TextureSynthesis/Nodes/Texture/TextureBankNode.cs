using UnityEngine;
using System.Collections;
using NodeEditorFramework;
using System.Collections.Generic;
using System.Linq;
using NodeEditorFramework.TextureComposer;
using SecretFire.TextureSynth;

[Node(false, "Texture/TextureBank")]
public class TextureBankNode : TextureSynthNode
{
    public const string ID = "TextureBankNode";
    public override string GetID => "TextureBankNode";
    public override string Title => "Textures";
    public override Vector2 MinSize => textures != null ? new Vector2(textures.Count * 70, 100) : new Vector2(200, 100);
    //public override Vector2 MinSize => new Vector2(200, 100);
    public override bool AutoLayout => true;

    public List<string> texNames;
    private List<Texture2D> textures;

    public override void DoInit() {
        Debug.Log("TexBank DoInit() called");
        LoadTextures();
        DoCalc();
    }

    protected override void OnAddConnection(ConnectionPort port, ConnectionPort connection)
    {
        base.OnAddConnection(port, connection);
        Debug.Log("TexBank OnAddConnection called");
    }

    public void LoadTextures()
    {
        textures = new List<Texture2D>(Resources.LoadAll<Texture2D>("StaticTextures"));
        if (texNames == null)
            texNames = new List<string>();
        List<string> loadedValues = new List<string>(textures.Select(t => t.name));
        HashSet<string> removedValues = new HashSet<string>(texNames);
        removedValues.ExceptWith(loadedValues);
        HashSet<string> addedValues = new HashSet<string>(loadedValues);
        addedValues.ExceptWith(texNames);
        //Remove any ports for textures that were removed
        foreach (string texName in removedValues)
        {
            int index = texNames.IndexOf(texName);
            dynamicConnectionPorts.RemoveAt(index);
        }
        foreach (string texName in addedValues)
        {
            texNames.Add(texName);
        }
        // Add ports for any textures that were added
        while (dynamicConnectionPorts.Count < texNames.Count)
        {
            int createdIndex = dynamicConnectionPorts.Count;
            ValueConnectionKnobAttribute outKnobAttribs = new ValueConnectionKnobAttribute(texNames[createdIndex], Direction.Out, typeof(Texture), NodeSide.Bottom);
            var texStyle = ConnectionPortStyles.GetPortStyle(outKnobAttribs.StyleID);
            texStyle.SetColor(Color.yellow);
            var outKnob = CreateValueConnectionKnob(outKnobAttribs);
        }
    }

    private ValueConnectionKnob texKnobs(string texName) => ((ValueConnectionKnob)dynamicConnectionPorts[texNames.IndexOf(texName)]);

    public override void NodeGUI()
    {
        GUILayout.BeginVertical();
        if (GUILayout.Button("Reinitialize")){
            DoInit();
        }
        GUILayout.BeginHorizontal();
        int i = 0;
        foreach (var tex in textures)
        {
            GUILayout.BeginVertical();
            NodeUIElements.TexInfo(tex, width:64, height: 64);
            texKnobs(tex.name).SetPosition(i * 70 + 35);
            GUILayout.EndVertical();
            i++;
        }
        GUILayout.EndHorizontal();
        GUILayout.EndVertical();
    }
    private bool texturesSet = false;

    private bool NeedLoadTextures => !texturesSet || dynamicConnectionPorts.Where(p => ((ValueConnectionKnob)p).IsValueNull).Count() > 0;

    public override bool DoCalc()
    {
        if (NeedLoadTextures)
        {
            if (textures == null)
            {
                try
                {
                    LoadTextures();
                } catch (UnityException e)
                {
                    Debug.Log(e+":\n\n"+e.Message);
                }
            }
            foreach (var tex in textures)
            {
                if (texNames.Contains(tex.name))
                {
                    var knob = texKnobs(tex.name);
                    knob.SetValue(tex);
                    var texStyle = ConnectionPortStyles.GetPortStyle(knob.styleID);
                    texStyle.SetColor(Color.yellow);
                    //Debug.Log("Set tex "+tex.name+" for knob");
                }
                else
                {
                    Debug.Log("Couldn't find knob for "+tex.name);
                }
                texturesSet = true;
            }
        }
        return true;
    }
}
