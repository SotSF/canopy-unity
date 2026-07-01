using UnityEngine;

namespace SpaceshipGame
{
    public class SpaceshipGamePlayer
    {
        public string id;
        public PlayerState state;
        public PlayerType playerType;
        public Color color;
        public SpaceshipController ship;
        public short score = 0;
        public short deaths = 0;

        // Canvas players are driven by local input (gamepad/MIDI/oscillator) via
        // ApplyCanvasInput; Web players are driven by a remote websocket client.
        public bool IsCanvas => IsCanvasType(playerType);

        // True only when this player currently has a live ship on the board.
        public bool IsAlive => ship != null && state == PlayerState.Alive;

        public static bool IsCanvasType(PlayerType playerType)
        {
            return playerType == PlayerType.GenericCanvas ||
                   playerType == PlayerType.Oddball ||
                   playerType == PlayerType.Controller;
        }
    }
}