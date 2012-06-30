using System;
using System.Collections.Generic;
using SeeSharpMessenger;
using Pooling;

namespace PushFightLogic
{	
public class Piece : IEquatable<Piece>
{
	private static int idCurrent = 0;

		
	public int ID { get; private set; }


	public Player Owner { get; private set; }


	public PieceType Type { get; private set; }


	public BoardSquare Occupies { get; private set; }


	private Action NotifyFn;
	private Action<Player> WinnerFn;


	public void Place (BoardSquare target)
	{
		Occupies = target;
		NotifyFn ();

		if (Occupies.Type == BoardSquareType.EDGE)
		{
			Messenger<GameToken>.Invoke ("piece.outofbounds", GameToken.FromPiece (this));
			WinnerFn(this.Owner == Player.P1 ? Player.P2 : Player.P1);
		}
	}

		
	public void Move (BoardSquare target)
	{
		if (CanMove (target))
		{
			Messenger<GameToken,Coords>.Invoke ("piece.moving", GameToken.FromPiece (this), target.Pos);
			Place (target);
		}
	}

		
	public void Displace (BoardSquare target)
	{
		if (!Occupies.Adjacent.Contains (target))
		{
			return;
		} // Can only displace to adjacent squares
			else
		{
			if (target.ContainsPiece ())
			{
				target.ContainedPiece ().Displace (NextSquare (Occupies, target));
			}
			
			Messenger<GameToken,Coords>.Invoke ("piece.displacing", GameToken.FromPiece (this), target.Pos);
			Place (target);
		}
	}


	private static List<BoardSquare> NavigationFilter (List<BoardSquare> adjacent, List<BoardSquare> explored)
	{
		return adjacent.FindAll (square =>
		{
			if (square.Type != BoardSquareType.NORMAL)
			{
				return false;
			}
			else if (square.ContainsPiece ())
			{
				return false;
			}
			else if (explored.Contains (square))
			{
				return false;
			}
			else
			{
				return true;
			}
			
		}
		);
	}

		
	public List<BoardSquare> CheckMoves ()
	{	  
		List<BoardSquare> explored = new List<BoardSquare> (16);
		Queue<BoardSquare> upcoming = new Queue<BoardSquare> (32);

		List<BoardSquare> viables = NavigationFilter (Occupies.Adjacent, explored);
		viables.ForEach (i => upcoming.Enqueue (i));

		while (upcoming.Count > 0)
		{
			BoardSquare next = upcoming.Dequeue ();
			if (!explored.Contains (next))
			{
				explored.Add (next);
			}

			viables = NavigationFilter (next.Adjacent, explored);
			viables.ForEach (i => {
				if (!upcoming.Contains (i))
				{
					upcoming.Enqueue (i);
				}
			}
			);
		}

		return explored;
	}


	public bool CanMove (BoardSquare target)
	{
		return CheckMoves ().Contains (target);
	}

		
	public bool CanBePushed (Piece pusher)
	{
		if (Push.AmIAnchored ())
		{
			return false;
		}
         
		BoardSquare next = NextSquare (pusher.Occupies, Occupies);

		if (next.Type == BoardSquareType.RAIL)
		{
			return false;
		}
		else if (!next.ContainsPiece ())
		{
			return true;
		}
		else
		{
			return next.ContainedPiece ().CanBePushed (this);
		}
	}


	private static BoardSquare NextSquare (BoardSquare pusherSquare, BoardSquare pushee)
	{
		int newX = pushee.Pos.x - (pusherSquare.Pos.x - pushee.Pos.x);
		int newY = pushee.Pos.y - (pusherSquare.Pos.y - pushee.Pos.y);

		var result = pushee.Adjacent.Find (square => square.Pos.x == newX && square.Pos.y == newY);
		if (result == null)
		{
			throw new Exception ("NextSquare of " + pusherSquare.Pos.ToString () + " and " + pushee.Pos.ToString () + " did not exist.");
		}
		return result;
	}

		
	public PushBehaviour Push { get; private set; }

		
	public Piece (Board board, Player owner, PieceType type)
	{
		Owner = owner;
		Type = type;
		ID = Piece.idCurrent++;
		NotifyFn = board.NotifyDirty;
		WinnerFn = board.NotifyWinner;
		
		if (Type == PieceType.ROUND)
		{
			Push = new RoundPiecePushBehaviour ();
		}
		else if (Type == PieceType.SQUARE)
		{
			Push = new SquarePiecePushBehaviour (this, board.TheAnchor);
		}
			
		Occupies = null;
	}


	public override int GetHashCode ()
	{
		return ID;
	}

		#region IEquatable implementation
	public bool Equals (Piece other)
	{
		return other.ID == ID;
	}
		#endregion

	public void Reclaim (Board board, Player owner)
	{
		Owner = owner;
		NotifyFn = board.NotifyDirty;
		WinnerFn = board.NotifyWinner;
		Occupies = null;

		SquarePiecePushBehaviour squareBehaviour = Push as SquarePiecePushBehaviour;
		if (squareBehaviour != null)
		{
			squareBehaviour.Reclaim (this, board.TheAnchor);
		}
	}

	class RoundPiecePushBehaviour : PushBehaviour
	{
		public void Push (BoardSquare target)
		{
			return; // Round pieces can't push
		}


		public bool CanPush (BoardSquare target)
		{
			return false;
		}


		public List<BoardSquare> CheckPushes ()
		{
			return new List<BoardSquare> ();
		}


		public bool AmIAnchored ()
		{
			return false;
		}
	}


	class SquarePiecePushBehaviour : PushBehaviour
	{
		private Anchor TheAnchor;
		private Piece MyPiece;


		public void Push (BoardSquare target)
		{
			if (CanPush (target))
			{
				Messenger<GameToken, Coords>.Invoke ("piece.pushing", GameToken.FromPiece (MyPiece), target.Pos);
				MyPiece.Displace (target);
				TheAnchor.MoveAnchor (MyPiece);
			}
		}


		public bool CanPush (BoardSquare target)
		{
			if (!target.ContainsPiece ())
			{
				return false;
			}
			else if (MyPiece.Occupies.Adjacent.Find (square => square.Pos.Equals (target.Pos)) == null)
			{
				return false;
			}
			else
			{
				return target.ContainedPiece ().CanBePushed (MyPiece);
			}
		}


		public List<BoardSquare> CheckPushes ()
		{
			return MyPiece.Occupies.Adjacent.FindAll (square => CanPush (square));
		}


		public bool AmIAnchored ()
		{
			if (TheAnchor.SitsAtop == null)
			{
				return false;
			}
			else
			{
				return TheAnchor.SitsAtop.Equals (MyPiece);
			}
		}


		public SquarePiecePushBehaviour (Piece parent, Anchor anchor)
		{
			Reclaim (parent, anchor);
		}


		public void Reclaim (Piece parent, Anchor anchor)
		{
			MyPiece = parent;
			TheAnchor = anchor;
		}
	}

	public static Pool<Piece> SquarePieces = null;
	public static Pool<Piece> RoundPieces = null;


	public static void SetupPiecePools (Board template)
	{
		if (SquarePieces != null)
		{
			return;
		}
		SquarePieces = new Pool<Piece> (Board.AMT_BOARDS_NEEDED_PER_SEARCH * Environment.ProcessorCount * 6 + 100, pool =>
		{
			return new Piece (template, Player.P1, PieceType.SQUARE);
		},
         Pooling.LoadingMode.Eager,
         Pooling.AccessMode.FIFO);

         
		RoundPieces = new Pool<Piece> (Board.AMT_BOARDS_NEEDED_PER_SEARCH * Environment.ProcessorCount * 4 + 100, pool =>
		{
			return new Piece (template, Player.P1, PieceType.ROUND);
		},
         Pooling.LoadingMode.Eager,
         Pooling.AccessMode.FIFO);
	}


	public static void ReturnPooledPiece (Piece piece)
	{
		if (piece.Type == PieceType.ROUND)
		{
			RoundPieces.Release (piece);
		}
		else
		{
			SquarePieces.Release (piece);
		}
	}


	public static Piece AcquirePooledPiece (PieceType pieceType)
	{
		if (pieceType == PieceType.ROUND)
		{
			return RoundPieces.Acquire ();
		}
		else
		{
			return SquarePieces.Acquire ();
		}
	}
	}


public interface PushBehaviour
{
	void Push (BoardSquare target);


	bool CanPush (BoardSquare target);


	List<BoardSquare> CheckPushes ();


	bool AmIAnchored ();
}
}

