using System;
using System.Collections.Generic;
using SeeSharpMessenger;
using System.Linq;

namespace PushFightLogic
{
	public enum Player
	{	
		P1, P2
	}
	
	public enum BoardSquareType
	{
		NORMAL, EDGE, RAIL	
	}
	
	public enum PieceType
	{
		ROUND, SQUARE
	}   
	
	public struct Coords : IEquatable<Coords>
   {
      public int x {get; set;}
      public int y {get; set;}

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

      public override int GetHashCode()
      {
         return x * 100 + y;
      }
   }

   public struct GameToken
   {
	  public int id {get; set;}
      public Coords location {get; set;}
      public PieceType type {get; set;}
      public Player owner {get; set;}
		
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

      public List<BoardSquare> AdjacentSquares()
      {
         List<BoardSquare> adjacent = new List<BoardSquare>(4);
         var SquareArr = Parent.Squares;

         if (Pos.x - 1 >= 0)
            adjacent.Add(SquareArr[Pos.x - 1,Pos.y]);
         if (Pos.x + 1 < Parent.Width)
            adjacent.Add(SquareArr[Pos.x + 1, Pos.y]);
         if (Pos.y - 1 >= 0)
            adjacent.Add(SquareArr[Pos.x, Pos.y - 1]);
         if (Pos.y + 1 < Parent.Height)
            adjacent.Add(SquareArr[Pos.x, Pos.y + 1]);

         return adjacent;
      }

      public bool ContainsPiece()
      {
         return Parent.Pieces.ContainsKey(this);
      }

      public Piece ContainedPiece()
      {
         Piece returnPiece = null;
         Parent.Pieces.TryGetValue(this, out returnPiece);

         return returnPiece;
      }
      public bool Equals(BoardSquare other)
      {
         return Pos.Equals(other.Pos);
      }
      public override int GetHashCode()
      {
         return Pos.GetHashCode();
      }
   }
	
	public delegate Player TerritoryFn(BoardSquare square);
	
	public class Board
	{
      public Anchor TheAnchor = new Anchor();
		public BoardSquare[,] Squares;
      
      private List<Piece> ActualPieces = new List<Piece>(10);
		public TerritoryFn TerritoryOf;

      public int Width;
      public int Height;
		private void SetSquares(BoardSquare[,] squares, TerritoryFn whatBelongsToWho)
		{
			TerritoryOf = whatBelongsToWho;
			Squares = squares;

         Width = Squares.GetLength(0);
         Height = Squares.GetLength(1);
		}

		public bool PlacePiece (Player owner, PieceType type, BoardSquare target)
		{
			Piece pieceToPlace = new Piece (this, owner, type);

			if (TerritoryOf (target) != pieceToPlace.Owner)
				return false;
			else if (Pieces.ContainsKey (target))
				return false;
			else if (target.Type != BoardSquareType.NORMAL)
				return false;
			else {
				pieceToPlace.Place (target);
				ActualPieces.Add (pieceToPlace);
				Messenger<GameToken>.Invoke ("piece.placed", GameToken.FromPiece (pieceToPlace));
				return true;
			}
		}


      public Player? Winner()
      {
         Piece losingPiece = ActualPieces
               .FirstOrDefault(piece => piece.Occupies.Type == BoardSquareType.EDGE);

         if (losingPiece == null) 
            return null;
         else
            return losingPiece.Owner == Player.P1 ? Player.P2 : Player.P1;
      }

      private bool isDirty = true;
      public void NotifyDirty()
      {
         isDirty = true;
      }
      private Dictionary<BoardSquare,Piece> _pieces;
      public Dictionary<BoardSquare, Piece> Pieces {get {
      {
         if (isDirty)
         {
            _pieces = new Dictionary<BoardSquare,Piece>();
         
            foreach (var item in ActualPieces)
            {
               _pieces[item.Occupies] = item;
            }

            isDirty = false;
         }

         return _pieces;
      }}}
      	
		public static Board CreateFromFile (string path)
		{
			Board board = new Board ();
			
			string[] boardLines = System.IO.File.ReadAllLines ("board.txt");
			BoardSquare[,] squares = new BoardSquare[boardLines [0].Length, boardLines.Length];
			
			for (int y = 0; y < boardLines.Length; y++) {
				for (int x = 0; x < boardLines[y].Length; x++) {
					char incoming = boardLines [y] [x];
					
					if (incoming.Equals ('_'))
						squares [x, y] = new BoardSquare (board, BoardSquareType.EDGE, new Coords (){x = x, y = y});
					else if (incoming.Equals ('='))
						squares [x, y] = new BoardSquare (board, BoardSquareType.RAIL, new Coords (){x = x, y = y});
					else if (incoming.Equals ('#'))
						squares [x, y] = new BoardSquare (board, BoardSquareType.NORMAL, new Coords (){x = x, y = y});
					else
						throw new NotSupportedException("Board Map includes tile that was not recognised.");
				}
			}
			
			board.SetSquares(squares, 
				(square) => 
				{
					if (square.Pos.x < squares.GetLength(0) / 2)
						return Player.P1;
					else
						return Player.P2;
				}
			);
			return board;
		}
		
		public static Board AIClone (Board board)
		{
			Board newBoard = new Board ();
			
			BoardSquare[,] squares = new BoardSquare[board.Width, board.Height];
			for (int y = 0; y < board.Height; y++) {
				for (int x = 0; x < board.Width; x++) {
					squares [x, y] = new BoardSquare (newBoard, board.Squares [x, y].Type, new Coords () {x = x, y = y});
				}
			}
			newBoard.SetSquares (squares, board.TerritoryOf);
			
			foreach (Piece existingPiece in board.ActualPieces) {
				Coords existingPos = existingPiece.Occupies.Pos;
				
				Piece pieceToPlace = new Piece (newBoard, existingPiece.Owner, existingPiece.Type);
				pieceToPlace.Place (newBoard.Squares [existingPos.x, existingPos.y]);
				newBoard.ActualPieces.Add (pieceToPlace);
			}
			
			if (board.TheAnchor.SitsAtop != null) {
				Coords aPos = board.TheAnchor.SitsAtop.Occupies.Pos;
				newBoard.TheAnchor
					.MoveAnchor (newBoard.Pieces [newBoard.Squares[aPos.x,aPos.y]]);
			}
			return newBoard;
		}
	}
}

