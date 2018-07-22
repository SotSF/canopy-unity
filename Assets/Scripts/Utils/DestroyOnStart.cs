using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Lightsale.Utility
{
    public class DestroyOnStart : MonoBehaviour
    {

        #region Lifecycle
        void Start()
        {
            Destroy(gameObject);
        }
        #endregion
    }
}