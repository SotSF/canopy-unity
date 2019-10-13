using UnityEngine;
using System.Collections;
using NodeEditorFramework;
using System.Collections.Generic;

[Node(false, "Inputs/Textures")]
public class TextureBankNode : Node
{
    public const string ID = "TextureBankNode";
    public override string GetID => "TextureBankNode";
    public override string Title => "Textures";
    public override Vector2 MinSize => new Vector2(120, 200);
    public override bool AutoLayout => true;

    public List<Texture2D> textures;
    [System.NonSerialized]
    private Dictionary<string, ValueConnectionKnob> texKnobs;


    public void LoadTextures()
    {
        ValueConnectionKnobAttribute outKnobAttribs = new ValueConnectionKnobAttribute("Output", Direction.Out, typeof(Texture));
        textures = new List<Texture2D>(Resources.LoadAll<Texture2D>("PatternTextures"));
        texKnobs = new Dictionary<string, ValueConnectionKnob>();
        foreach (var tex in textures)
        {
            texKnobs[tex.name] = CreateValueConnectionKnob(outKnobAttribs);
        }
    }

    public override void NodeGUI()
    {

        GUILayout.BeginVertical();
        foreach (var tex in textures)
        {
            GUILayout.BeginHorizontal();
            NodeUIElements.TexInfo(tex, height: 64);
            texKnobs[tex.name].DisplayLayout();
            GUILayout.EndHorizontal();
        }
        GUILayout.EndVertical();
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
            if (texKnobs.ContainsKey(tex.name))
                texKnobs[tex.name].SetValue(tex);
        }
        return true;
    }
}
