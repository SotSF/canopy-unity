using UnityEngine;

// Input bundle for a single canvas-driven SpaceshipGame player. Produced by
// SpaceshipGamePlayerNode (from gamepad/MIDI/oscillator signals) and consumed by
// SpaceshipGameNode, which forwards it to SpaceshipGameController.ApplyCanvasInput.
//
// playerId is assigned once by the source node and travels with the data so the
// controller can track a stable ship per player; a default (null/empty) playerId
// marks "no real player" (e.g. an unconnected port).
[System.Serializable]
public struct SpaceshipGamePlayerData
{
    public string playerId;
    public Vector2 leftStick;   // (lx, ly) — thrust
    public Vector2 rightStick;  // (rx, ry) — currently unused by the game
    public bool fire;           // L button
    public bool altFire;        // R button
    public bool hasColor;       // true only when a color source is connected; leaves the
                                // ship's default color alone otherwise
    public Color color;         // applied to the ship when it changes (see ApplyCanvasInput)
}
