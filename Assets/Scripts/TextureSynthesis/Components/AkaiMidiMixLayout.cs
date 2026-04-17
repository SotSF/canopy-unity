using System.Collections.Generic;
using UnityEngine;


/// <summary>
/// Simple data class containing configuration and physical layout information for the Akai MIDIMix
/// </summary>
public class AkaiMidiMixLayout 
{
    public enum CcType {knob, skinnyknob, fader};
    public enum CcColor {gold, silver, black, blackred, red, green, blue, white, purple, orange};
    public struct CcDesc {
        public int id; 
        public CcType ccType; 
        public CcColor color; 
        public Vector2Int position;
    };
    
    private Dictionary<int, CcDesc> idMap = new Dictionary<int, CcDesc>();

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

        CcDesc[][] ccDescs = new CcDesc[4][];
        for (int i = 0; i < 4; i++)
        {
            var idRow = ccIds[i];
            var colorRow = ccColors[i];
            var typeRow = ccTypes[i];
            for (int j = 0; j < idRow.Length; j++)
            {
                ccDescs[i][j] = new CcDesc(){id= idRow[j],ccType = typeRow[j],color= colorRow[j], position= new Vector2Int(j,i) };
                idMap[idRow[j]] = ccDescs[i][j];
            }
        }
    }

    public CcDesc GetByCcId(string ccId)
    {
        return new CcDesc(){};
    }
}
