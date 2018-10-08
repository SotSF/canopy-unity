using NodeEditorFramework;
using NodeEditorFramework.TextureComposer;
using NodeEditorFramework.Utilities;
using UnityEngine;
using UnityEngine.Video;

[Node(false, "Texture/InputAnimated")]
public class AnimatedNode : Node
{
    public const string ID = "animatedNode";
    public override string GetID { get { return ID; } }

    public override string Title { get { return "Animated"; } }
    public override Vector2 DefaultSize { get { return new Vector2(75, 75); } }

    [ValueConnectionKnob("Out", Direction.Out, typeof(Texture))]
    public ValueConnectionKnob textureOutputKnob;

    public RenderTexture outputTex;
    private Vector2Int outputSize = Vector2Int.zero;
    private VideoPlayer player;

    int nextIndex = 0;
    int currentIndex = 0;
    VideoClip[] animatedTextures;

    private void Awake()
    {
        animatedTextures = Resources.LoadAll<VideoClip>("AnimatedTextures");
        player = GameObject.Find("VideoManager").GetComponent<VideoPlayer>();
        SelectClip();
    }

    public void NextClip()
    {
        SelectClip(currentIndex + 1 % animatedTextures.Length);
    }

    public void SelectClip(int index = 0)
    {
        player.clip = animatedTextures[index];
        player.renderMode = VideoRenderMode.RenderTexture;
        outputSize = new Vector2Int((int)player.clip.width, (int)player.clip.height);
        InitializeRenderTexture();
        player.targetTexture = outputTex;
        currentIndex = index;
        player.Play();
    }

    private void InitializeRenderTexture()
    {
        outputTex = new RenderTexture(outputSize.x, outputSize.y, 24);
        outputTex.enableRandomWrite = true;
        outputTex.Create();
    }

    public override void NodeGUI()
    {
        GUILayout.BeginHorizontal();
        nextIndex = RTEditorGUI.IntSlider(nextIndex, 0, animatedTextures.Length);
        textureOutputKnob.DisplayLayout();

        GUILayout.EndHorizontal();

        if (GUI.changed)
            NodeEditor.curNodeCanvas.OnNodeChange(this);
    }

    public override bool Calculate()
    {
        // Assign output channels
        if (nextIndex != currentIndex)
        {
            SelectClip(nextIndex);
        }
        textureOutputKnob.SetValue(outputTex);
        return true;
    }
}