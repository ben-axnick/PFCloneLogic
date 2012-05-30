using System;
using System.Collections.Generic;

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
	
	public class BoardSquare : IEquatable<BoardSquare>
	{
		public int PosX {get; private set;}
		public int PosY {get; private set;}
		public BoardSquareType Type {get; private set;}
		
		private Board Parent;
		
		public BoardSquare(Board parent, BoardSquareType squareType, int xPos, int yPos)
		{
			this.Parent = parent;
			this.PosX = xPos;
			this.PosY = yPos;
			this.Type = squareType;
		}

      public List<BoardSquare> AdjacentSquares()
      {
         List<BoardSquare> adjacent = new List<BoardSquare>();

         if (PosX - 1 >= 0)
            adjacent.Add(Parent.Squares[PosX - 1,PosY]);
         if (PosX + 1 < Parent.Squares.GetLength(0))
            adjacent.Add(Parent.Squares[PosX + 1, PosY]);
         if (PosY - 1 >= 0)
            adjacent.Add(Parent.Squares[PosX, PosY - 1]);
         if (PosY + 1 < Parent.Squares.GetLength(1))
            adjacent.Add(Parent.Squares[PosX, PosY + 1]);

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
         return PosX == other.PosX && PosY == other.PosY;
      }
   }
	
	public delegate Player TerritoryFn(BoardSquare square);
	
	public class Board
	{
      public Anchor TheAnchor = new Anchor();
		public BoardSquare[,] Squares;
      
      private List<Piece> ActualPieces = new List<Piece>();
		public TerritoryFn TerritoryOf;

		private void SetSquares(BoardSquare[,] squares, TerritoryFn whatBelongsToWho)
		{
			TerritoryOf = whatBelongsToWho;
			Squares = squares;
		}

		public bool PlacePiece(Player owner, PieceType type, BoardSquare target)
		{
         Piece pieceToPlace = new Piece(this, owner, type);

			if (TerritoryOf(target) != pieceToPlace.Owner)
				return false;
			else if (Pieces.ContainsKey(target))
				return false;
			else if (target.Type != BoardSquareType.NORMAL)
				return false;
			else
			{
				pieceToPlace.Place(target);
				ActualPieces.Add(pieceToPlace);
				return true;
			}
		}


      public Player? Winner()
      {
         Piece losingPiece = ActualPieces
               .Find(piece => piece.Occupies.Type == BoardSquareType.EDGE);

         if (losingPiece == null) 
            return null;
         else
            return losingPiece.Owner == Player.P1 ? Player.P2 : Player.P1;
      }

      public Dictionary<BoardSquare, Piece> Pieces {get {
      {
         var Pieces = new Dictionary<BoardSquare,Piece>();

         ActualPieces.ForEach(piece => Pieces.Add(piece.Occupies, piece));
         return Pieces;
      }}}
      	
		public static Board CreateFromFile(string path)
		{
			Board board = new Board();
			
			string[] boardLines = System.IO.File.ReadAllLines("board.txt");
			BoardSquare[,] squares = new BoardSquare[boardLines[0].Length,boardLines.Length];
			
			for (int y = 0; y < boardLines.Length; y++)
			{
				for(int x = 0; x < boardLines[y].Length; x++)
				{
					char incoming = boardLines[y][x];
					
					if (incoming.Equals('_'))
						squares[x,y] = new BoardSquare(board,BoardSquareType.EDGE, x, y);
					else if (incoming.Equals('='))
						squares[x,y] = new BoardSquare(board,BoardSquareType.RAIL, x, y);
					else if (incoming.Equals('#'))
						squares[x,y] = new BoardSquare(board,BoardSquareType.NORMAL, x, y);
					else
						throw new NotSupportedException("Board Map includes tile that was not recognised.");
				}
			}
			
			board.SetSquares(squares, 
				(square) => 
				{
					if (square.PosX <= 3)
						return Player.P1;
					else
						return Player.P2;
				}
			);
			return board;
		}
	}
}

