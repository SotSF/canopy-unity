using System.Collections.Generic;
using UnityEngine;


/// <summary>
/// Simple data class containing configuration and physical layout information for the Akai MIDIMix.
/// Grid positions: knobs are rows 0-2 (top to bottom), faders are row 3; columns 0-8 left to right
/// (column 8 is the master fader). MidiLayoutRenderer maps these to a device picture.
/// </summary>
public class AkaiMidiMixLayout
{
    public enum CcType { knob, skinnyknob, fader };
    public enum CcColor { gold, silver, black, blackred, red, green, blue, white, purple, orange };
    public struct CcDesc
    {
        public int id;
        public CcType ccType;
        public CcColor color;
        public Vector2Int position;
    };

    private readonly Dictionary<int, CcDesc> idMap = new Dictionary<int, CcDesc>();

    public IReadOnlyCollection<CcDesc> AllControls => idMap.Values;

    public AkaiMidiMixLayout()
    {
        /*  Layout:
            [ 8x3 knob grid   ][1x4 button column ]
            [ 8x2 button grid ][                  ]
            [ 9x1 fader array                     ]
        */

        int[][] ccIds = new int[][]
        {
            new int[] {16, 20, 24, 28, 46, 50, 54, 58},
            new int[] {17, 21, 25, 29, 47, 51, 55, 59},
            new int[] {18, 22, 26, 30, 48, 52, 56, 60},
            new int[] {19, 23, 27, 31, 49, 53, 57, 61, 62}
        };

        CcColor[][] ccColors = new CcColor[][]
        {
            new CcColor[] {CcColor.gold, CcColor.silver, CcColor.black, CcColor.gold, CcColor.silver, CcColor.black, CcColor.gold, CcColor.silver},
            new CcColor[] {CcColor.blackred, CcColor.silver, CcColor.blackred, CcColor.gold, CcColor.silver, CcColor.blackred, CcColor.gold, CcColor.blackred},
            new CcColor[] {CcColor.gold, CcColor.silver, CcColor.black, CcColor.gold, CcColor.silver, CcColor.black, CcColor.gold, CcColor.silver},
            new CcColor[] {CcColor.red, CcColor.green, CcColor.blue, CcColor.white, CcColor.white, CcColor.purple, CcColor.orange, CcColor.orange, CcColor.orange}
        };

        CcType[][] ccTypes = new CcType[][]
        {
            new CcType[] {CcType.knob, CcType.knob,CcType.knob,CcType.knob,CcType.knob,CcType.knob,CcType.knob,CcType.knob},
            new CcType[] {CcType.knob, CcType.knob,CcType.knob,CcType.knob,CcType.knob,CcType.knob,CcType.knob,CcType.knob},
            new CcType[] {CcType.knob, CcType.knob,CcType.knob,CcType.knob,CcType.knob,CcType.knob,CcType.knob,CcType.knob},
            new CcType[] {CcType.fader,CcType.fader,CcType.fader,CcType.fader,CcType.fader,CcType.fader,CcType.fader,CcType.fader,CcType.fader}
        };

        for (int i = 0; i < ccIds.Length; i++)
        {
            var idRow = ccIds[i];
            var colorRow = ccColors[i];
            var typeRow = ccTypes[i];
            for (int j = 0; j < idRow.Length; j++)
            {
                idMap[idRow[j]] = new CcDesc()
                {
                    id = idRow[j],
                    ccType = typeRow[j],
                    color = colorRow[j],
                    position = new Vector2Int(j, i)
                };
            }
        }
    }

    public bool TryGetByCcId(int ccId, out CcDesc desc)
    {
        return idMap.TryGetValue(ccId, out desc);
    }

    public bool ContainsCcId(int ccId)
    {
        return idMap.ContainsKey(ccId);
    }
}
