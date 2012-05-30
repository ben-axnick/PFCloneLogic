using System;
using System.Collections.Generic;

namespace PushFightLogic
{	
	public class Piece
	{
		public Player Owner {get; private set;}
		public PieceType Type {get; private set;}
		public BoardSquare Occupies {get; private set;}

		public void Place(BoardSquare target)
		{
			Occupies = target;	
		}
		
		public void Move(BoardSquare target)
		{
         if (CanMove(target))
            Place(target);
		}
		
      public void Displace(BoardSquare target)
      {
         if (!Occupies.AdjacentSquares().Contains(target))
            return; // Can only displace to adjacent squares
         else
         {
            if (target.ContainsPiece())
            {
               target.ContainedPiece().Displace(NextSquare(Occupies, target));
            }

            Place(target);
         }
      }

      public List<BoardSquare> CheckMoves()
      {
         List<BoardSquare> explored = new List<BoardSquare>();
         Queue<BoardSquare> upcoming = new Queue<BoardSquare>();

         Func<List<BoardSquare>,List<BoardSquare>> filter = (adjacent) =>
         {
            List<BoardSquare> viable = new List<BoardSquare>();
            viable.AddRange(adjacent);
            viable.RemoveAll(square =>
            {
               if (square.Type != BoardSquareType.NORMAL)
                  return true;
               else if (square.ContainsPiece())
                  return true;
               else if (explored.Contains(square))
                  return true;
               else
                  return false;
            });

            return viable;
         };


         List<BoardSquare> viables = filter(Occupies.AdjacentSquares());
         viables.ForEach(i => upcoming.Enqueue(i));

         while(upcoming.Count > 0)
         {
            BoardSquare next = upcoming.Dequeue();
            if (!explored.Contains(next)) explored.Add(next);

            viables = filter(next.AdjacentSquares());
            viables.ForEach(i => upcoming.Enqueue(i));
         }

         return explored;
      }

		public bool CanMove(BoardSquare target)
		{
         return CheckMoves().Contains(target);
		}
		
		public bool CanBePushed(Piece pusher)
		{
         if (Push.AmIAnchored())
            return false;
         
         BoardSquare next = NextSquare(pusher.Occupies, Occupies);

         if (next.Type == BoardSquareType.RAIL)
         {
            return false;
         }
         else if (!next.ContainsPiece())
         {
            return true;
         }
         else
         {
            return next.ContainedPiece().CanBePushed(this);
         }

			throw new NotImplementedException();
		}

      private static BoardSquare NextSquare(BoardSquare pusherSquare, BoardSquare pushee)
      {
         int newX = pushee.PosX - (pusherSquare.PosX - pushee.PosX);
         int newY = pushee.PosY - (pusherSquare.PosY - pushee.PosY);

         return pushee.AdjacentSquares().Find(square => square.PosX == newX && square.PosY == newY);
      }
		
		public PushBehaviour Push {get; private set;}
		
		public Piece(Board board, Player owner, PieceType type)
		{
			this.Owner = owner;
			this.Type = type;
			
			if (Type == PieceType.ROUND)
				Push = new RoundPiecePushBehaviour();
			else if (Type == PieceType.SQUARE)
				Push = new SquarePiecePushBehaviour(this, board.TheAnchor);
			
			Occupies = null;
		}
	}

	public interface PushBehaviour
	{
		void Push(BoardSquare target);
		bool CanPush(BoardSquare target);
      List<BoardSquare> CheckPushes();
      bool AmIAnchored();
	}
	
	public class RoundPiecePushBehaviour : PushBehaviour
	{
		public void Push (BoardSquare target)
		{
			return; // Round pieces can't push
		}

		public bool CanPush (BoardSquare target)
		{
			return false;
		}

      public List<BoardSquare> CheckPushes()
      {
         return new List<BoardSquare>();
      }

      public bool AmIAnchored ()
      {
         return false;
      }
	}
	
	public class SquarePiecePushBehaviour : PushBehaviour
	{
		private Anchor TheAnchor;
		private Piece MyPiece;
		
		public void Push (BoardSquare target)
		{
         if (CanPush(target))
         {
            MyPiece.Displace(target);
            TheAnchor.MoveAnchor(MyPiece);
         }
		}

		public bool CanPush (BoardSquare target)
		{
         if (!target.ContainsPiece())
            return false;
         else
            return target.ContainedPiece().CanBePushed(MyPiece);
		}

      public List<BoardSquare> CheckPushes()
      {
         return MyPiece.Occupies.AdjacentSquares().FindAll(square => CanPush(square));
      }

      public bool AmIAnchored()
      {
         return TheAnchor.SitsAtop != null && TheAnchor.SitsAtop.Equals(MyPiece);
      }

		public SquarePiecePushBehaviour(Piece parent, Anchor anchor)
		{
			MyPiece = parent;
			TheAnchor = anchor;	
		}
	}
}

