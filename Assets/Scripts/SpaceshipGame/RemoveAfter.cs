using UnityEngine;

public class RemoveAfter : MonoBehaviour
{
    public float duration = 1;
    void Start()
    {
        Invoke("DestroyThis", duration);
    }

    void DestroyThis()
    {
        Destroy(this.gameObject);
    }
}
