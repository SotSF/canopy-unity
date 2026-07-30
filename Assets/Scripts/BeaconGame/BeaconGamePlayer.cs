using UnityEngine;

namespace BeaconGame
{
    public enum BeaconPlayerState { Alive, Spawning }

    public class BeaconGamePlayer
    {
        public string id;
        public BeaconPlayerState state;
        public Color color;
        public BeaconShipController ship;

        public bool IsAlive => ship != null && state == BeaconPlayerState.Alive;
    }
}
