using UnityEngine;
using System.Collections;
using NodeEditorFramework;
using System.Collections.Generic;
using System.Linq;
using NodeEditorFramework.TextureComposer;

[Node(false, "Texture/TextureBank")]
public class TextureBankNode : Node
{
    public const string ID = "TextureBankNode";
    public override string GetID => "TextureBankNode";
    public override string Title => "Textures";
    public override Vector2 MinSize => textures != null ? new Vector2(textures.Count * 70, 100) : new Vector2(200, 100);
    //public override Vector2 MinSize => new Vector2(200, 100);
    public override bool AutoLayout => true;

    public List<string> texNames;
    public SerialDict<string, ValueConnectionKnob> texKnobs;
    private List<Texture2D> textures;

    public void Awake() {
        if (textures == null || texKnobs == null)
        {
            try
            {
                LoadTextures();
            } catch (UnityException e)
            {
                Debug.Log(e+":\n\n"+e.Message);
            }
        }
        Calculate();
    }

    public void LoadTextures()
    {
        //var foo = ConnectionPortStyles.GetPortStyle("tex");
        ValueConnectionKnobAttribute outKnobAttribs = new ValueConnectionKnobAttribute("Output", Direction.Out, typeof(Texture), NodeSide.Bottom);
        var texStyle = ConnectionPortStyles.GetPortStyle(outKnobAttribs.StyleID);
        this.TimedDebug("texStyleID: " + outKnobAttribs.StyleID,2);
        texStyle.SetColor(Color.yellow);
        textures = new List<Texture2D>(Resources.LoadAll<Texture2D>("StaticTextures"));
        //Debug.Log("Num tex: "+textures.Count);
        if (texKnobs == null){
            texKnobs = new SerialDict<string, ValueConnectionKnob>();
            //Debug.Log("Empty tex knobs");
        }
        if (texNames == null)
            texNames = new List<string>();
        List<string> loadedValues = new List<string>(textures.Select(t => t.name));
        HashSet<string> removedValues = new HashSet<string>(texNames);
        removedValues.ExceptWith(loadedValues);
        HashSet<string> addedValues = new HashSet<string>(loadedValues);
        addedValues.ExceptWith(texNames);
        //Rewire connection ports from loaded strings
        foreach (string texName in texNames)
        {
            //Debug.Log("name: "+texName+" texture");
            texKnobs[texName] = (ValueConnectionKnob)dynamicConnectionPorts[texNames.IndexOf(texName)];
        }
        //Remove any ports for textures that were removed
        foreach (string texName in removedValues)
        {
            dynamicConnectionPorts.Remove(texKnobs[texName]);
        }
        // Add ports for any textures that were added
        foreach (string texName in addedValues)
        {
            texKnobs[texName] = CreateValueConnectionKnob(outKnobAttribs);
            texNames.Add(texName);
        }
    }

    public override void NodeGUI()
    {

        GUILayout.BeginHorizontal();
        int i = 0;
        foreach (var tex in textures)
        {
            GUILayout.BeginVertical();
            NodeUIElements.TexInfo(tex, width:64, height: 64);
            texKnobs[tex.name].SetPosition(i * 70 + 35);
            GUILayout.EndVertical();
            i++;
        }
        GUILayout.EndHorizontal();
    }

    public override bool Calculate()
    {
        if (textures == null || texKnobs == null)
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
            if (texKnobs.ContainsKey(tex.name)){
                texKnobs[tex.name].SetValue(tex);
                //Debug.Log("Set tex "+tex.name+" for knob");
            }
            else{
                Debug.Log("Couldn't find knob for "+tex.name);
            }
        }
        return true;
    }
}
