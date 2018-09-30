using UnityEngine;

namespace NodeEditorFramework.TextureComposer
{
	[Node(false, "Texture/Info")]
	public class TextureInfoNode : Node
	{
		public const string ID = "texInfoNode";
		public override string GetID { get { return ID; } }

		public override string Title { get { return "Texture Info"; } }
		public override Vector2 MinSize { get { return new Vector2(150, 50); } }
		public override bool AutoLayout { get { return true; } }

		[ValueConnectionKnob("Texture", Direction.In, typeof(Texture))]
		public ValueConnectionKnob inputKnob;

		[System.NonSerialized]
		public Texture tex;

		public override void NodeGUI()
		{
			inputKnob.DisplayLayout(new GUIContent("Texture" + (tex != null ? ":" : " (null)"), "The texture to display information about."));
			if (tex != null)
			{
				RTTextureViz.DrawTexture(tex, 64);
				GUILayout.Label("'" + tex.name + "'");
				GUILayout.Label("Size: " + tex.width + "x" + tex.height + "");
			}
		}

		public override bool Calculate()
		{
			tex = inputKnob.connected() ? inputKnob.GetValue<Texture>() : null;
			return true;
		}
	}

}