
using NodeEditorFramework;
using NodeEditorFramework.Utilities;
using SecretFire.TextureSynth;

using System.Collections.Generic;
using System.Linq;

using UnityEngine;

[Node(false, "Pattern/Chess")]
public class ChessNode : TickingNode
{
    public override string GetID => "Chess";
    public override string Title { get { return "Chess"; } }

    public override Vector2 DefaultSize { get { return new Vector2(200, 200); } }

    [ValueConnectionKnob("xInput", Direction.In, typeof(float), NodeSide.Left)]
    public ValueConnectionKnob xInputKnob;

    [ValueConnectionKnob("yInput", Direction.In, typeof(float), NodeSide.Left)]
    public ValueConnectionKnob yInputKnob;

    [ValueConnectionKnob("clickInput", Direction.In, typeof(bool), NodeSide.Left)]
    public ValueConnectionKnob clickInputKnob;

    [ValueConnectionKnob("outputTex", Direction.Out, typeof(Texture), NodeSide.Bottom)]
    public ValueConnectionKnob outputTexKnob;

    private Color boardBlack = Color.black;// Color.HSVToRGB(.66f, .7f, .5f);
    private Color pieceBlack = Color.green;// Color.HSVToRGB(.66f, 1, 1);
    private Color boardWhite = Color.white; // Color.HSVToRGB(1, .7f, .7f);
    private Color pieceWhite = Color.HSVToRGB(1, 1, 1);
    private Color highlight = Color.HSVToRGB(.2f, 1, 1);
    private Color candidateMove = Color.HSVToRGB(.3f, 1, 1);

    private Texture2D outputTex;
    private Chess.ChessEngine engine;
    private (int,int)? selection = null;
    private List<(Chess.BoardSpace, Chess.BoardState)> potentialMoves;
    private (int, int) cursor;
    private List<(Chess.BoardSpace, Chess.BoardState)> noMoves;

    private void Awake(){
        outputTex = new Texture2D(16, 16);
        outputTex.filterMode = FilterMode.Point;
        engine = new Chess.ChessEngine();
        noMoves = new List<(Chess.BoardSpace, Chess.BoardState)>();
        potentialMoves = noMoves;
    }

    public void RenderBoard(Chess.BoardState state)
    {
        for (int r = 0; r < 8; r++)
        {
            for (int c = 0; c < 8; c++)
            {
                var space = state.Space((r, c));
                var spaceColor = space.color == Chess.Color.Black ? boardBlack : boardWhite;
                if (potentialMoves.Any( move => move.Item1.coord == (r,c)))
                {
                    spaceColor = candidateMove + Color.HSVToRGB(0, 0, (Mathf.Sin(2 * Mathf.PI * Time.time / 1) / 2 + 0.5f)); ;
                }
                if ((r,c) == cursor)
                {
                    spaceColor = highlight + Color.HSVToRGB(0,0,(Mathf.Sin(2 * Mathf.PI * Time.time / 1) / 2 + 0.5f));
                }
                outputTex.SetPixel(c * 2, r * 2, spaceColor);
                outputTex.SetPixel(c * 2+1, r * 2, spaceColor);
                outputTex.SetPixel(c * 2, r * 2+1, spaceColor);
                outputTex.SetPixel(c * 2+1, r * 2+1, spaceColor);

                if (space.piece != null)
                {
                    var pieceColor = space.piece?.color == Chess.Color.White ? pieceWhite : pieceBlack;
                    switch (space.piece?.type)
                    {
                        case Chess.PieceType.Pawn:
                            outputTex.SetPixel(c * 2, r * 2, pieceColor);
                            break;
                        case Chess.PieceType.Knight:
                            outputTex.SetPixel(c * 2, r * 2, pieceColor);
                            outputTex.SetPixel(c * 2, r * 2+1, pieceColor);
                            break;
                        case Chess.PieceType.Bishop:
                            outputTex.SetPixel(c * 2, r * 2, pieceColor);
                            outputTex.SetPixel(c * 2+1, r * 2+1, pieceColor);
                            break;
                        case Chess.PieceType.Rook:
                            outputTex.SetPixel(c * 2, r * 2, pieceColor);
                            outputTex.SetPixel(c * 2+1, r * 2, pieceColor);
                            break;
                        case Chess.PieceType.Queen:
                            outputTex.SetPixel(c * 2, r * 2, pieceColor);
                            outputTex.SetPixel(c * 2, r * 2+1, pieceColor);
                            outputTex.SetPixel(c * 2+1, r * 2, pieceColor);
                            break;
                        case Chess.PieceType.King:
                            outputTex.SetPixel(c * 2, r * 2, pieceColor);
                            outputTex.SetPixel(c * 2, r * 2 + 1, pieceColor);
                            outputTex.SetPixel(c * 2+1, r * 2+1, pieceColor);
                            break;
                    }
                }

            }
        }
        //outputTex.SetPixel(0, 16, Color.green);
        outputTex.Apply();
    }

    public void MoveCursor( (int,int) newPosition)
    {
        if (new Chess.Coord(newPosition).onBoard())
        {
            Debug.LogFormat("New cursor position: [{0}, {1}]", newPosition.Item1, newPosition.Item2);
            var x = engine.board.PieceAt(newPosition);
            cursor = newPosition;
            if (selection == null)
            {
                potentialMoves = engine.MovesForSpace(cursor).ToList();
                if (x != null)
                {
                    Debug.LogFormat("Piece at cursor: {0} {1}, {2} moves", x?.color, x?.type, potentialMoves.Count);
                }
            }
        }
    }

    public void Select()
    {
        if (selection == null)
        {
            // Set cursor
            selection = cursor;
        } else if (selection == cursor)
        {
            // Deselect
            selection = null;
            potentialMoves = noMoves;
        } else
        {
            // Take potential move
            if (potentialMoves.Any(move => move.Item1.coord == cursor))
            {
                var taken = potentialMoves.Where(move => move.Item1.coord==cursor).First();
                engine.TakeMove(taken);
                potentialMoves = noMoves;
                selection = null;
            }

            // Click on piece
            else if (engine.board.PieceAt(cursor) != null) {
                // Select own piece
                if (engine.board.PieceAt(cursor)?.color == engine.toMove)
                {
                    selection = cursor; 
                    potentialMoves = engine.MovesForSpace(cursor).ToList();
                }
                // Select enemy piece?
                else
                {
                    selection = null;
                    potentialMoves = noMoves;
                }
            }

            // Click on empty space
            else
            {
                selection = null;
                potentialMoves = noMoves;
            }
        }

    }

    public override void NodeGUI()
    {
        GUILayout.BeginHorizontal();

        GUILayout.BeginVertical();
        xInputKnob.DisplayLayout();
        yInputKnob.DisplayLayout();
        clickInputKnob.DisplayLayout();
        GUILayout.EndVertical();

        GUILayout.BeginVertical();

        GUILayout.BeginHorizontal();
        GUILayout.BeginVertical();
        if (GUILayout.Button("Up")){
            MoveCursor((cursor.Item1+1, cursor.Item2));
        }
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("Left"))
        {
            MoveCursor((cursor.Item1, cursor.Item2-1));
        }
        if (GUILayout.Button("Right"))
        {
            MoveCursor((cursor.Item1, cursor.Item2+1));
        }
        GUILayout.EndHorizontal();
        if (GUILayout.Button("Down"))
        {
            MoveCursor((cursor.Item1-1, cursor.Item2));
        }
        GUILayout.EndVertical();


        GUILayout.BeginHorizontal();
        if (GUILayout.Button("Click")){
            Select();
        }
        if (GUILayout.Button("Reset")){
            engine.Reset();
        }
        GUILayout.EndHorizontal();
        GUILayout.EndHorizontal();

        GUILayout.BeginHorizontal();
        GUILayout.FlexibleSpace();
        GUILayout.Box(outputTex, GUILayout.MaxWidth(64), GUILayout.MaxHeight(64));
        GUILayout.Space(4);
        GUILayout.EndHorizontal();
        GUILayout.Space(4);
        GUILayout.EndVertical();

        outputTexKnob.SetPosition(DefaultSize.x-20);
        if (GUI.changed)
            NodeEditor.curNodeCanvas.OnNodeChange(this);
    }
    
    public override bool Calculate()
    {
        RenderBoard(engine.board);
        outputTexKnob.SetValue<Texture>(outputTex);
        return true;
    }
}
