using System;
using System.Text;
using SeeSharpMessenger;

namespace PushFightLogic
{
/// <summary>
/// Manages the complete progression of the game from start to finish.
/// </summary>
public class GameMaster
{
	// Fundamental game rules, not likely to ever change
	public const int MAX_ROUND_PIECES = 2;
	public const int MAX_SQUARE_PIECES = 3;
	public const int MOVES_PER_TURN = 2;
	public const int PUSHES_PER_TURN = 1;
	
	
	private Board Board;
	private Player roundStarter;


	public GameTurn Turn { get; private set; }

	/// <summary>
	/// The current game round, starting at 0 and incrementing whenever a turn is finished.
	/// </summary>
	public int round { get; private set; }

	/// <summary>
	/// Recreates the game board and state in preparation of a new game
	/// </summary>
	public void Reset ()
	{
		Board = Board.Create ();

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

	/// <summary>
	/// Passes on the underlying Board's winning player
	/// </summary>
	public Player? Winner ()
	{
		return Board.Winner;
	}
	
	/// <summary>
	/// Redirects to the static Board AIClone method, is required as the client has no Board reference
	/// </summary>			
	public static Board AIClone (GameMaster master)
	{
		Board newBoard = Board.AIClone (master.Board);
			
		return newBoard;
	}
	
	public void AIPools()
	{
		Board.SetupBoardPool(Board);
		Piece.SetupPiecePools(Board);
	}
}
}
