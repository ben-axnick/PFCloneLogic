using System;
using System.Collections.Generic;
using System.Text;
using SeeSharpMessenger;

namespace PushFightLogic
{
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

	
public struct GameBoard
{
	public List<GameTile> tiles;
	public List<GameToken> tokens;
}

	
public struct GameTile
{
	public Coords location { get; set; }


	public BoardSquareType type { get; set; }
}


public interface GameControls
{
	bool Place (PieceType piece, Coords location);


	bool Move (Coords piece, Coords target);


	bool Push (Coords piece, Coords target);


	void Skip ();
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


public class GameTurn
{
	private Action OnTurnEnded;


	private GameMaster Parent { get; set; }


	protected UnityBoardMap Map { get; private set; }


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


public class GameMaster
{
	public const int MAX_ROUND_PIECES = 2;
	public const int MAX_SQUARE_PIECES = 3;
	public const int MOVES_PER_TURN = 2;
	public const int PUSHES_PER_TURN = 1;
	private Board Board;
	private Player roundStarter;


	public GameTurn Turn { get; private set; }


	public int round { get; private set; }

		
	private static bool firstRun = true;


	public void Reset ()
	{
		Board = Board.Create ();

		if (firstRun)
		{
			firstRun = false;
			Board.SetupBoardPool (Board);
			Piece.SetupPiecePools (Board);
		}

		round = 0;
		roundStarter = Player.P1;
			
		Messenger.Invoke ("game.begin");
		newTurn (roundStarter);	
	}


	private void newTurn (Player player)
	{
		Turn = new GameTurn (TurnOver, Board, player, round);
		Turn.Begin ();
	}

		
	private void TurnOver ()
	{
		if (Board.Winner != null)
		{
			Messenger<Player>.Invoke ("game.over", Board.Winner.GetValueOrDefault ());
			return;
		}

		Player otherPlayer = roundStarter.Other();

		if (Turn.TurnPlayer == roundStarter)
		{
			newTurn (otherPlayer);
		}
		else
		{
			round++;
			newTurn (roundStarter);
		}
	}


	public Player? Winner ()
	{
		return Board.Winner;
	}

						
	public GameBoard ViewBoard ()
	{
		GameBoard rtn = new GameBoard ();
		rtn.tiles = new List<GameTile> ();
		var iterator = Board.Squares.GetEnumerator ();
		iterator.MoveNext ();
		for (int i = 0; i < Board.Squares.Length; i++)
		{
			BoardSquare tile = iterator.Current as BoardSquare;
			rtn.tiles.Add (new GameTile (){
					location = new Coords(){x = tile.Pos.x, y = tile.Pos.y},
					type = tile.Type}
			);
			iterator.MoveNext ();
		}
			
		rtn.tokens = new List<GameToken> ();
		int j = 0;
		foreach (Piece piece in Board.Pieces.Values)
		{
			rtn.tokens.Add (new GameToken () {
					location = new Coords() {x = piece.Occupies.Pos.x, y = piece.Occupies.Pos.y},
					owner = piece.Owner}
			);
			j++;
		}
			
		return rtn;
	}
		
					
	public static Board AIClone (GameMaster master)
	{
		Board newBoard = Board.AIClone (master.Board);
			
		return newBoard;
	}
}
}
