using System;
using System.Collections.Generic;
using SeeSharpMessenger;
using System.Linq;
using System.Text;
using System.IO;
using System.Reflection;

namespace PushFightLogic
{
public enum Player
{	
	P1,
	P2
}

public enum BoardSquareType
{
	NORMAL,
	EDGE,
	RAIL	
}

	
public enum PieceType
{
	ROUND,
	SQUARE
}

	
public struct Coords : IEquatable<Coords>
{
	public int x { get; set; }


	public int y { get; set; }

		#region IEquatable implementation
	public bool Equals (Coords other)
	{
		return x == other.x && y == other.y;
	}
		#endregion
		
	public override string ToString ()
	{
		return "[" + x + "," + y + "]";	
	}


	public override int GetHashCode ()
	{
		return x * 100 + y;
	}


	public static Coords Next (Coords first, Coords next)
	{
		return new Coords ()
         {
            x = next.x - (first.x - next.x),
            y = next.y - (first.y - next.y)
         };
	}
}


public struct GameToken
{
	public int id { get; set; }


	public Coords location { get; set; }


	public PieceType type { get; set; }


	public Player owner { get; set; }

		
	public static GameToken FromPiece (Piece piece)
	{
		return new GameToken (){
				id = piece.ID,
				location = piece.Occupies.Pos,
				type = piece.Type,
				owner = piece.Owner
			};
	}
}

	
public class BoardSquare : IEquatable<BoardSquare>
{
	public Coords Pos; // { get; private set;} Removed for AI speed boost
	public BoardSquareType Type; // {get; private set;} Removed for AI speed boost
		
	private Board Parent;

		
	public BoardSquare (Board parent, BoardSquareType squareType, Coords pos)
	{
		Parent = parent;
		Pos = pos;
		Type = squareType;
	}


	public List<BoardSquare> Adjacent { get; private set; }


	public List<BoardSquare> AdjacentSquaresInit ()
	{
		Adjacent = new List<BoardSquare> (4);
		var SquareArr = Parent.Squares;

		if (Pos.x - 1 >= 0)
		{
			Adjacent.Add (SquareArr [Pos.x - 1, Pos.y]);
		}
		if (Pos.x + 1 < Parent.Width)
		{
			Adjacent.Add (SquareArr [Pos.x + 1, Pos.y]);
		}
		if (Pos.y - 1 >= 0)
		{
			Adjacent.Add (SquareArr [Pos.x, Pos.y - 1]);
		}
		if (Pos.y + 1 < Parent.Height)
		{
			Adjacent.Add (SquareArr [Pos.x, Pos.y + 1]);
		}

		return Adjacent;
	}


	public bool ContainsPiece ()
	{
		return Parent.Pieces.ContainsKey (this);
	}


	public Piece ContainedPiece ()
	{
		Piece returnPiece = null;
		Parent.Pieces.TryGetValue (this, out returnPiece);

		return returnPiece;
	}


	public bool Equals (BoardSquare other)
	{
		return Pos.Equals (other.Pos);
	}


	public override int GetHashCode ()
	{
		return Pos.GetHashCode ();
	}
}

	
public delegate Player TerritoryFn (BoardSquare square);


public class Board
{

	public Anchor TheAnchor = new Anchor ();
	public BoardSquare[,] Squares;
	private List<Piece> ActualPieces = new List<Piece> (10);
	public TerritoryFn TerritoryOf;

	public int Width { get { return Squares.GetLength (0); } }


	public int Height { get { return Squares.GetLength (1); } }


	private Board ()
	{
		Winner = null;
	}
	
	private void SetSquares (BoardSquare[,] squares, TerritoryFn whatBelongsToWho)
	{
		TerritoryOf = whatBelongsToWho;
		Squares = squares;
		foreach (BoardSquare square in Squares)
			square.AdjacentSquaresInit ();
	}


	public bool PlacePiece (Player owner, PieceType type, BoardSquare target)
	{
		Piece pieceToPlace = new Piece (this, owner, type);

		if (TerritoryOf (target) != pieceToPlace.Owner)
		{
			return false;
		}
		else if (Pieces.ContainsKey (target))
		{
			return false;
		}
		else if (target.Type != BoardSquareType.NORMAL)
		{
			return false;
		}
		else
		{
			pieceToPlace.Place (target);
			ActualPieces.Add (pieceToPlace);
			Messenger<GameToken>.Invoke ("piece.placed", GameToken.FromPiece (pieceToPlace));
			return true;
		}
	}
	
	public Player? Winner { get; private set; }
	public void NotifyWinner (Player winner)
	{
			Winner = winner;
	}

	private bool isDirty = true;
	public void NotifyDirty ()
	{
		isDirty = true;
	}

	private Dictionary<BoardSquare,Piece> _pieces;


	public Dictionary<BoardSquare, Piece> Pieces
	{
		get
		{
			if (isDirty)
			{
				_pieces = new Dictionary<BoardSquare,Piece> ();
         
				foreach (var item in ActualPieces)
				{
					_pieces [item.Occupies] = item;
				}

				isDirty = false;
			}

			return _pieces;
		}
	}

      	
	public static Board Create ()
	{
		Assembly assembly = typeof(Board).Assembly;
		StreamReader boardStream = new StreamReader (assembly.GetManifestResourceStream ("PushFightLogic.board.txt"));
		List<string> lines = new List<string> ();

		while (boardStream.Peek() >= 0)
		{
			lines.Add (boardStream.ReadLine ());
		}

		return CreateFromData (lines.ToArray ());
	}


	private static Board CreateFromData (string[] boardLines)
	{
		Board board = new Board ();

		BoardSquare[,] squares = new BoardSquare[boardLines [0].Length, boardLines.Length];
			
		for (int y = 0; y < boardLines.Length; y++)
		{
			for (int x = 0; x < boardLines[y].Length; x++)
			{
				char incoming = boardLines [y] [x];
					
				if (incoming.Equals ('_'))
				{
					squares [x, y] = new BoardSquare (board, BoardSquareType.EDGE, new Coords (){x = x, y = y});
				}
				else if (incoming.Equals ('='))
				{
					squares [x, y] = new BoardSquare (board, BoardSquareType.RAIL, new Coords (){x = x, y = y});
				}
				else if (incoming.Equals ('#'))
				{
					squares [x, y] = new BoardSquare (board, BoardSquareType.NORMAL, new Coords (){x = x, y = y});
				}
				else
				{
					throw new NotSupportedException ("Board Map includes tile that was not recognised.");
				}
			}
		}
			
		board.SetSquares (squares, 
				(square) => 
		{
			if (square.Pos.x < squares.GetLength (0) / 2)
			{
				return Player.P1;
			}
			else
			{
				return Player.P2;
			}
		}
		);
		return board;
	}

	  
	public const int AMT_BOARDS_NEEDED_PER_SEARCH = 4000;
	public static Pooling.Pool<Board> Pool = null;

      
	public static void SetupBoardPool (Board template)
	{
		if (Pool != null)
		{
			return;
		}
		Pool = new Pooling.Pool<Board> (AMT_BOARDS_NEEDED_PER_SEARCH * Environment.ProcessorCount, pool => {
			Board newBoard = new Board ();

			BoardSquare[,] squares = new BoardSquare[template.Width, template.Height];
			for (int y = 0; y < template.Height; y++)
			{
				for (int x = 0; x < template.Width; x++)
				{
					squares [x, y] = new BoardSquare (newBoard, template.Squares [x, y].Type, new Coords () { x = x, y = y });
				}
			}
			newBoard.SetSquares (squares, template.TerritoryOf);
			return newBoard;
		},
         Pooling.LoadingMode.Eager,
         Pooling.AccessMode.FIFO);
	}


	private static void ReclaimPooledBoard (Board newBoard)
	{
		newBoard.ActualPieces.ForEach (piece => Piece.ReturnPooledPiece (piece));
		newBoard.ActualPieces.Clear ();
		newBoard.TheAnchor.Reset ();
		newBoard.NotifyDirty ();
		newBoard.Winner = null;
	}


	public static Board AIClone (Board board)
	{
		Board newBoard = Pool.Acquire ();
		ReclaimPooledBoard (newBoard);

		foreach (Piece existingPiece in board.ActualPieces)
		{
			Coords existingPos = existingPiece.Occupies.Pos;
				
			Piece pieceToPlace = Piece.AcquirePooledPiece (existingPiece.Type);
			pieceToPlace.Reclaim (newBoard, existingPiece.Owner);
			pieceToPlace.Place (newBoard.Squares [existingPos.x, existingPos.y]);
			newBoard.ActualPieces.Add (pieceToPlace);
		}
			
		if (board.TheAnchor.SitsAtop != null)
		{
			Coords aPos = board.TheAnchor.SitsAtop.Occupies.Pos;
			newBoard.TheAnchor
					.MoveAnchor (newBoard.Pieces [newBoard.Squares [aPos.x, aPos.y]]);
		}

		return newBoard;
	}


	public string GUID ()
	{
		return GUID (false, coord => coord);
	}

	  
	public List<string> AllGUIDs ()
	{
		return AllGUIDs (false);
	}


	public List<string> AllGUIDs (bool inverted)
	{
		List<string> guids = new List<string> (4);
		guids.Add (GUID (inverted, coord => coord));

		// A board rotated 180 degrees is exactly the same board!
		guids.Add (GUID (inverted, coord => new Coords () { x = (Width - 1) - coord.x, y = (Height - 1) - coord.y }));
            
		return guids;
	}


	public string GUID (bool inverted, Func<Coords,Coords> CoordFilter)
	{
		StringBuilder outStr = new StringBuilder (64, 64);

		List<Piece> orderedPieces = new List<Piece> (10);

		for (int i = 0; i <= 1; i++)
		{
			Player color = (Player)Enum.GetValues (typeof(Player)).GetValue (i);

			var myPieces = ActualPieces.Where (pc => pc.Owner == color);
			var squares = myPieces.Where (pc => pc.Type == PieceType.SQUARE);
			var rounds = myPieces.Where (pc => pc.Type == PieceType.ROUND);
			
			if (inverted)
			{
				orderedPieces.AddRange (rounds.OrderBy (piece => piece.Occupies.Pos.GetHashCode ()));
				orderedPieces.AddRange (squares.OrderBy (piece => piece.Occupies.Pos.GetHashCode ()));
			}
			else
			{
				orderedPieces.AddRange (squares.OrderBy (piece => piece.Occupies.Pos.GetHashCode ()));
				orderedPieces.AddRange (rounds.OrderBy (piece => piece.Occupies.Pos.GetHashCode ()));
			}
		}

		foreach (Piece pc in orderedPieces)
		{
			var FilteredLoc = CoordFilter (pc.Occupies.Pos);
			outStr.Append (String.Format ("{0:d2}", FilteredLoc.x));
			outStr.Append (String.Format ("{0:d2}", FilteredLoc.y));
		}

		if (TheAnchor.SitsAtop != null)
		{
			var FilteredLoc = CoordFilter (TheAnchor.SitsAtop.Occupies.Pos);
			outStr.Append (String.Format ("{0:d2}", FilteredLoc.x));
			outStr.Append (String.Format ("{0:d2}", FilteredLoc.y));
		}

		return outStr.ToString ();

	}
}
}

