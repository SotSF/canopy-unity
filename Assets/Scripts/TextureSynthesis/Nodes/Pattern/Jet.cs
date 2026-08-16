using UnityEngine;

namespace SecretFire.TextureSynth
{
    /// <summary>
    /// A single fluid jet definition. Flows through the node canvas as Jet[]
    /// (JetInstance emits a length-1 array; replicator nodes transform
    /// Jet[] -> Jet[]; FluidJetGeneratorNode renders the final array into
    /// velocity + dye textures for the fluid sim).
    ///
    /// Values are pushed between knobs by reference, so nodes must never
    /// mutate an input Jet[] - use Clone() and emit fresh arrays.
    /// </summary>
    public class Jet
    {
        public Vector2 position;      // normalized UV over the output texture, [0,1]
        public float angle;           // direction in radians, 0 = +x, CCW
        public float intensity = 1;   // force magnitude at the nozzle, HSV value units
        public float width = 0.04f;   // nozzle half-width, fraction of min texture dimension
        public float reach = 0.35f;   // penetration distance, fraction of min texture dimension
        public float spread = 0.15f;  // cone half-angle, radians
        public Color color = Color.white;  // dye color emitted at the nozzle

        public Jet Clone()
        {
            return (Jet)MemberwiseClone();
        }
    }
}
