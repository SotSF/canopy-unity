using NodeEditorFramework;

using UnityEngine;

public class CanvasStorageManager : MonoBehaviour
{
    public static CanvasStorageManager Instance { get; private set; }

    public NodeCanvas[] canvasSaves;

    public void Start()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }


}
