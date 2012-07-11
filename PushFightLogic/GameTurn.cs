using System;
using System.Collections.Generic;
using SeeSharpMessenger;

namespace PushFightLogic
{

/// <summary>
/// Represents the current turn that is underway inside GameMaster
/// </summary>
public class GameTurn
{
	/// <summary>
	/// A private channel for notifying GameMaster that the turn is over.
	/// </summary>
	// It is being used due to deficiencies in the messaging model that we causing
	// out-of-order message chaos
	private Action OnTurnEnded;


	private GameMaster Parent { get; set; }

	/// <summary>
	/// Tool for converting Coords to BoardSquares
	/// </summary>
	protected UnityBoardMap Map { get; private set; }


	/// <summary>
	/// The player who is undertaking this turn
	/// </summary>
	public Player TurnPlayer { get; private set; }


	public Board Board { get; private set; }


	private TurnState State;
	private int RoundNo;

	  	
	public GameTurn (Action action, Board board, Player player, int roundNo)
	{
		OnTurnEnded = action;
		Board = board;
		Map = new UnityBoardMap (board);
		TurnPlayer = player;
		RoundNo = roundNo;
	}

	/// <summary>
	/// Set the start state appropriately depending on whether this is the first round, then broadcasts a message.
	/// </summary>
	public void Begin ()
	{
		if (RoundNo == 0)
		{
			SwapState (new PlacementTurnState (this));
		}
		else
		{
			SwapState (new MovePieceState (this));
		}
			
		Messenger<Player>.Invoke ("turn.begin", TurnPlayer);
	}

	
	public void SwapState (TurnState newState)
	{
		State = newState;

		Messenger<Player,string>.Invoke ("turn.phase", TurnPlayer, State.FriendlyName);
		if (State.GetType () == typeof(TurnFinishedState))
		{
			OnTurnEnded ();
		}
	}


	public GameControls Control ()
	{
		return State;
	}

		
	public string Phase ()
	{
		return State.FriendlyName;	
	}


	public List<Coords> ValidMoves (Coords location)
	{
		return CheckMovesByOwner (location, (piece) => piece.CheckMoves ());
	}


	public List<Coords> ValidPushes (Coords location)
	{
		return CheckMovesByOwner (location, (piece) => piece.Push.CheckPushes ());
	}


	private List<Coords> CheckMovesByOwner (Coords location, Func<Piece,List<BoardSquare>> checkMoves)
	{
		BoardSquare square = Map.Square (location);

		if (!Board.Pieces.ContainsKey (square))
		{
			return new List<Coords> ();
		}
		else
		{
			Piece piece = Board.Pieces [square];

			if (piece.Owner != TurnPlayer)
			{
				return new List<Coords> ();
			}
			else
			{
				var moves = checkMoves (piece);
				List<Coords> rtn = new List<Coords> ();

				for (int i = 0; i < moves.Count; i++)
				{
					rtn.Add (new Coords () { x = moves[i].Pos.x, y = moves[i].Pos.y });
				}

				return rtn;
			}
		}
	}
}
   
public class TurnState : GameControls
{
	protected GameTurn Context { get; private set; }


	protected Player Controller { get; private set; }


	protected Board Board { get; private set; }


	protected UnityBoardMap Map { get; private set; }


	public String FriendlyName { get; protected set; }


	public TurnState (GameTurn context)
	{
		Context = context;
		Controller = context.TurnPlayer;
		Board = context.Board;
		Map = new UnityBoardMap (Board);
		FriendlyName = "Default";
	}

		
	public virtual bool Place (PieceType piece, Coords location)
	{
		return false; // don't respond to terrorists
	}


	public virtual bool Move (Coords piece, Coords target)
	{
		return false; // don't respond to terrorists
	}


	public virtual bool Push (Coords piece, Coords target)
	{
		return false; // don't respond to terrorists
	}


	public virtual void Skip ()
	{
		// ignore by default
	}
}


public class PlacementTurnState : TurnState
{
	public PlacementTurnState (GameTurn context) : base(context)
	{
		FriendlyName = "Placement";
	}


	public override bool Place (PieceType piece, Coords location)
	{
		try
		{
			if (piece == PieceType.ROUND && CountPieces (PieceType.ROUND) >= GameMaster.MAX_ROUND_PIECES)
			{
				return false;
			}
			else if (piece == PieceType.SQUARE && CountPieces (PieceType.SQUARE) >= GameMaster.MAX_SQUARE_PIECES)
			{
				return false;
			}
			else
			{
				return Board.PlacePiece (Controller, piece, Map.Square (location));
			}
		}
		finally
		{
			if (CountPieces () >= GameMaster.MAX_ROUND_PIECES + GameMaster.MAX_SQUARE_PIECES)
			{
				Context.SwapState (new TurnFinishedState (Context));
			}
		}
	}


	private int CountPieces ()
	{
		int count = 0;
		foreach (Piece piece in Board.Pieces.Values)
		{
			if (piece.Owner == Controller)
			{
				count++;
			}
		}

		return count;
	}


	private int CountPieces (PieceType type)
	{
		int count = 0;
		foreach (Piece piece in Board.Pieces.Values)
		{
			if (piece.Owner == Controller && piece.Type == type)
			{
				count++;
			}
		}

		return count;
	}
}


public class MovePieceState : TurnState
{
	public MovePieceState (GameTurn context) : base(context)
	{
		FriendlyName = "Movement";
	}


	private int movements = 0;


	public override bool Move (Coords piece, Coords target)
	{
		bool success;
		if (Context.ValidMoves (piece).Count == 0)
		{
			success = false;
		}
		else
		{
			Piece boardPiece = Board.Pieces [Map.Square (piece)];
			success = boardPiece.CanMove (Map.Square (target));
			boardPiece.Move (Map.Square (target));
		}

		if (success)
		{
			movements++;
		}
		if (movements >= GameMaster.MOVES_PER_TURN)
		{
			Context.SwapState (new PushPieceState (Context));
		}

		return success;
	}


	public override void Skip ()
	{
		Context.SwapState (new PushPieceState (Context));
	}
}


public class PushPieceState : TurnState
{
	public PushPieceState (GameTurn context) : base(context)
	{
		FriendlyName = "Pushing";
	}


	private int pushes = 0;


	public override bool Push (Coords piece, Coords target)
	{
		bool success;
		if (Context.ValidPushes (piece).Count == 0)
		{
			success = false;
		}
		else
		{
			Piece boardPiece = Board.Pieces [Map.Square (piece)];
			success = boardPiece.Push.CanPush (Map.Square (target));
			boardPiece.Push.Push (Map.Square (target));
		}

		if (success)
		{
			pushes++;
		}
		if (pushes >= GameMaster.PUSHES_PER_TURN)
		{
			Context.SwapState (new TurnFinishedState (Context));
		}

		return success;
	}


	public override void Skip ()
	{
		Board.NotifyWinner(Context.TurnPlayer.Other());
		Context.SwapState (new TurnFinishedState (Context));
	}
}


public class TurnFinishedState : TurnState
{
	public TurnFinishedState (GameTurn context) : base(context)
	{
		FriendlyName = "Ended";
	}
}



	public class UnityBoardMap
	{
		private Board Board;
	
	
		public UnityBoardMap (Board board)
		{
			Board = board;
		}
	
	
		public BoardSquare Square (Coords coords)
		{
			foreach (BoardSquare square in Board.Squares)
			{
				if (square.Pos.Equals (coords))
				{
					return square;
				}
			}
	
			return null;
		}
	}
}

