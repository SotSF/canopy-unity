using UnityEngine;

namespace Lightsale.Products.Smartsticks
{
    [System.Serializable]
    [CreateAssetMenu(menuName = "Lightsale/Smartsticks/PoiPatternSet")]
    public class PoiPatternSet : ScriptableObject
    {
        public FirstOrderPoiPath leftPath;
        public FirstOrderPoiPath rightPath;
    }
}