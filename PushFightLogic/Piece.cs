using System;
using System.Collections.Generic;
using SeeSharpMessenger;

namespace PushFightLogic
{	
	public class Piece : IEquatable<Piece>
	{
		private static int idCurrent = 0;
		
		public int ID {get; private set;}
		public Player Owner {get; private set;}
		public PieceType Type {get; private set;}
		public BoardSquare Occupies {get; private set;}

		public void Place (BoardSquare target)
		{
			Occupies = target;
			NotifyFn();

			if (Occupies.Type == BoardSquareType.EDGE) {
				Messenger<GameToken>.Invoke ("piece.outofbounds", GameToken.FromPiece (this));
			}
		}
		
		public void Move (BoardSquare target)
		{
			if (CanMove (target)) {
				Messenger<GameToken,Coords>.Invoke ("piece.moving", GameToken.FromPiece (this), target.Pos);
				Place (target);
			}
		}
		
      public void Displace (BoardSquare target)
		{
			if (!Occupies.AdjacentSquares ().Contains (target))
				return; // Can only displace to adjacent squares
			else {
				if (target.ContainsPiece ()) {
					target.ContainedPiece ().Displace (NextSquare (Occupies, target));
				}
			
				Messenger<GameToken,Coords>.Invoke ("piece.displacing", GameToken.FromPiece (this), target.Pos);
            	Place(target);
         }
      }

      public List<BoardSquare> CheckMoves()
      {
         List<BoardSquare> explored = new List<BoardSquare>(32);
         Queue<BoardSquare> upcoming = new Queue<BoardSquare>(64);

         Func<List<BoardSquare>,List<BoardSquare>> filter = (adjacent) =>
         {
            adjacent.RemoveAll(square =>
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

            return adjacent;
         };


         List<BoardSquare> viables = filter(Occupies.AdjacentSquares());
         viables.ForEach(i => upcoming.Enqueue(i));

         while(upcoming.Count > 0)
         {
            BoardSquare next = upcoming.Dequeue();
            if (!explored.Contains(next)) explored.Add(next);

            viables = filter(next.AdjacentSquares());
            viables.ForEach(i => {if (!upcoming.Contains(i)) upcoming.Enqueue(i);});
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
		}

      private static BoardSquare NextSquare (BoardSquare pusherSquare, BoardSquare pushee)
		{
			int newX = pushee.Pos.x - (pusherSquare.Pos.x - pushee.Pos.x);
			int newY = pushee.Pos.y - (pusherSquare.Pos.y - pushee.Pos.y);

			var result = pushee.AdjacentSquares ().Find (square => square.Pos.x == newX && square.Pos.y == newY);
			if (result == null)
				throw new Exception ("NextSquare of " + pusherSquare.Pos.ToString() + " and " + pushee.Pos.ToString() + " did not exist.");
			return result;
      }
		
		public PushBehaviour Push {get; private set;}
		
      private Action NotifyFn;
		public Piece (Board board, Player owner, PieceType type)
		{
			Owner = owner;
			Type = type;
			ID = Piece.idCurrent++;
			NotifyFn = board.NotifyDirty;

			if (Type == PieceType.ROUND)
				Push = new RoundPiecePushBehaviour();
			else if (Type == PieceType.SQUARE)
				Push = new SquarePiecePushBehaviour(this, board.TheAnchor);
			
			Occupies = null;
		}

      public override int GetHashCode()
      {
         return ID;
      }

		#region IEquatable implementation
		public bool Equals (Piece other)
		{
			return other.ID == ID;
		}
		#endregion
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
			if (CanPush (target)) {
				Messenger<GameToken,Coords>.Invoke ("piece.pushing", GameToken.FromPiece (MyPiece), target.Pos);
            MyPiece.Displace(target);
            TheAnchor.MoveAnchor(MyPiece);
         }
		}

		public bool CanPush (BoardSquare target)
		{
			if (!target.ContainsPiece ())
				return false;
			else if (MyPiece.Occupies.AdjacentSquares().Find (square => square.Pos.Equals (target.Pos)) == null)
				return false;
         else
            return target.ContainedPiece().CanBePushed(MyPiece);
		}

      public List<BoardSquare> CheckPushes()
      {
         return MyPiece.Occupies.AdjacentSquares().FindAll(square => CanPush(square));
      }

      public bool AmIAnchored ()
		{
			if (TheAnchor.SitsAtop == null)
				return false;
         	else 
				return TheAnchor.SitsAtop.Equals(MyPiece);
      }

		public SquarePiecePushBehaviour(Piece parent, Anchor anchor)
		{
			MyPiece = parent;
			TheAnchor = anchor;	
		}
	}
}

