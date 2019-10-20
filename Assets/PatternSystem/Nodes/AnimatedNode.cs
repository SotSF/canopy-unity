using NodeEditorFramework;
using NodeEditorFramework.TextureComposer;
using NodeEditorFramework.Utilities;
using UnityEngine;
using UnityEngine.Video;

[Node(false, "Texture/InputAnimated")]
public class AnimatedNode : TickingNode
{
    public const string ID = "animatedNode";
    public override string GetID { get { return ID; } }
    public override string Title { get { return "Animated"; } }
    public override Vector2 DefaultSize { get { return new Vector2(150, 150); } }

    [ValueConnectionKnob("Out", Direction.Out, typeof(Texture), NodeSide.Bottom, 40)]
    public ValueConnectionKnob textureOutputKnob;

    public RenderTexture outputTex;
    private Vector2Int outputSize = Vector2Int.zero;
    private VideoPlayer player;

    float playbackSpeed = 1;

    int nextIndex = 0;
    int currentIndex = 0;
    VideoClip[] animatedTextures;

    private void Awake()
    {
        animatedTextures = Resources.LoadAll<VideoClip>("AnimatedTextures");
        if (Application.isPlaying)
        {
            player = GameObject.Find("VideoManager").GetComponent<VideoPlayer>();
            player.prepareCompleted += (e) => { player.Play(); };
            SelectClip();
        }

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
        player.Prepare();
    }

    private void InitializeRenderTexture()
    {
        outputTex = new RenderTexture(outputSize.x, outputSize.y, 24);
        outputTex.enableRandomWrite = true;
        outputTex.Create();
    }

    public override void NodeGUI()
    {
        GUILayout.BeginVertical();
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("Previous"))
        {
            nextIndex = currentIndex == 0 ? animatedTextures.Length - 1 : currentIndex - 1;
        };
        if (GUILayout.Button("Next"))
        {
            nextIndex = (currentIndex + 1) % animatedTextures.Length;
        };
        GUILayout.EndHorizontal();
        GUILayout.Box(outputTex, GUILayout.MaxHeight(64));
        var newSpeed = RTEditorGUI.Slider(playbackSpeed, 0.1f, 4);
        if (newSpeed != playbackSpeed)
        {
            playbackSpeed = newSpeed;
            player.playbackSpeed = playbackSpeed;
        }
        textureOutputKnob.DisplayLayout();
        GUILayout.EndVertical();

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