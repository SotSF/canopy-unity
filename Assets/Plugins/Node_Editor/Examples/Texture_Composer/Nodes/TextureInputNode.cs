﻿using UnityEngine;
using NodeEditorFramework.Utilities;

namespace NodeEditorFramework.TextureComposer
{
	[Node(true, "Texture/Input")]
	public class TextureInputNode : Node
	{
		public const string ID = "texInNode";
		public override string GetID { get { return ID; } }

		public override string Title { get { return "Texture Input"; } }
		public override Vector2 DefaultSize { get { return new Vector2(100, 100); } }

		[ValueConnectionKnob("Texture", Direction.Out, typeof(Texture))]
		public ValueConnectionKnob outputKnob;

		public Texture2D tex;

		public override void NodeGUI()
		{
			outputKnob.DisplayLayout();

			Texture2D newTex = RTEditorGUI.ObjectField(tex, false);

			if (GUI.changed)
			{ // Texture has been changed
				try
				{ // Check for readability and update tex
					newTex.GetPixel(0, 0);
					tex = newTex;
				}
				catch (UnityException e)
				{ // Texture is not readable
					Debug.LogError("Texture is not readable!");
                    Debug.Log(e.Message);
                    Debug.Log(e.StackTrace);
				}
				NodeEditor.curNodeCanvas.OnNodeChange(this);
			}
		}

		public override bool Calculate()
		{
			outputKnob.SetValue(tex);
			return true;
		}
	}
}