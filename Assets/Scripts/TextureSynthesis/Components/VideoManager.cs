using UnityEngine;
using System.Collections;
using UnityEngine.Video;

public class VideoManager : MonoBehaviour
{
    private VideoClip[] animatedTextures;


    // Use this for initialization
    void Start()
    {
        animatedTextures = Resources.LoadAll<VideoClip>("AnimatedTextures");
        foreach (var anim in animatedTextures)
        {
            var player = gameObject.AddComponent<VideoPlayer>();
            player.clip = anim;
            player.renderMode = VideoRenderMode.RenderTexture;

        }
    }

    // Update is called once per frame
    void Update()
    {

    }
}
