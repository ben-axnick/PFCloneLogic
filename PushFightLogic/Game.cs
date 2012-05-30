using System;
using System.Collections.Generic;
using System.Text;

namespace PushFightLogic
{
   public class UnityBoardMap
   {
      private Board Board;

      public UnityBoardMap(Board board)
      {
         Board = board;
      }

      public BoardSquare Square(Coords coords)
      {
         foreach (BoardSquare square in Board.Squares)
         {
            if (square.PosX == coords.x && square.PosY == coords.y)
            {
               return square;
            }
         }

         return null;
      }
   }

   public struct Coords
   {
      public int x {get; set;}
      public int y {get; set;}
   }

   public struct GameToken
   {
      public Coords location {get; set;}
      public PieceType type {get; set;}
      public Player owner {get; set;}
   }
	
   public struct GameBoard
	{
		public GameTile[] tiles;
		public GameToken[] tokens;
	}
	
   public struct GameTile
	{
		public Coords location {get; set;}
		public BoardSquareType type {get; set;}
	}

   public interface GameControls
   {
      bool Place(PieceType piece, Coords location);
      bool Move(Coords piece, Coords target);
      bool Push(Coords piece, Coords target);
      void Skip();
      Coords[] ValidMoves(Coords location);
      Coords[] ValidPushes(Coords location);
		GameBoard ViewBoard();
		string Phase();
   }
   
   public class TurnState : GameControls
   {
      protected GameTurn Context {get; private set;}
      protected UnityBoardMap Map { get; private set; }
      protected Player Controller { get; private set; }
      protected Board Board { get; private set; }
      public String FriendlyName {get; protected set; }

      public TurnState(GameTurn context)
      {
         Context = context;
         Controller = context.TurnPlayer;
         Board = context.Board;
         Map = new UnityBoardMap(Board);
         FriendlyName = "Default";
      }
	  
	  public string Phase ()
		{
			return FriendlyName;	
		}
		
      public virtual bool Place(PieceType piece, Coords location)
      {
         return false; // don't respond to terrorists
      }

      public virtual bool Move(Coords piece, Coords target)
      {
         return false; // don't respond to terrorists
      }

      public virtual bool Push(Coords piece, Coords target)
      {
         return false; // don't respond to terrorists
      }

      public virtual void Skip()
      {
         // ignore by default
      }
		
	  public GameBoard ViewBoard ()
		{
			GameBoard rtn = new GameBoard ();
			rtn.tiles = new GameTile[Board.Squares.Length];
			var iterator = Board.Squares.GetEnumerator ();
			iterator.MoveNext();
			for (int i = 0; i < Board.Squares.Length; i++) {
				BoardSquare tile = iterator.Current as BoardSquare;
				rtn.tiles [i] = new GameTile (){
					location = new Coords(){x = tile.PosX, y = tile.PosY},
					type = tile.Type};
				iterator.MoveNext ();
			}
			
			rtn.tokens = new GameToken[Board.Pieces.Count];
			int j = 0;
			foreach (Piece piece in Board.Pieces.Values) {
				rtn.tokens [j] = new GameToken () {
					location = new Coords() {x = piece.Occupies.PosX, y = piece.Occupies.PosY},
					owner = piece.Owner};
				j++;
			}
			
			return rtn;
	  }
		
      public Coords[] ValidMoves(Coords location)
      {
         return CheckMovesByOwner(location, (piece) => piece.CheckMoves());
      }

      public Coords[] ValidPushes(Coords location)
      {
         return CheckMovesByOwner(location, (piece) => piece.Push.CheckPushes());
      }

      private Coords[] CheckMovesByOwner(Coords location, Func<Piece,List<BoardSquare>> checkMoves)
      {
         BoardSquare square = Map.Square(location);

         if (!Board.Pieces.ContainsKey(square))
         {
            return new Coords[] { };
         }
         else
         {
            Piece piece = Board.Pieces[square];

            if (piece.Owner != Controller)
            {
               return new Coords[] { };
            }
            else
            {
               var moves = checkMoves(piece);
               Coords[] rtn = new Coords[moves.Count];

               for (int i = 0; i < rtn.Length; i++)
               {
                  rtn[i] = new Coords() { x = moves[i].PosX, y = moves[i].PosY };
               }

               return rtn;
            }
         }
      }
   }

   public class PlacementTurnState : TurnState
   {
      public PlacementTurnState(GameTurn context) : base(context) {FriendlyName = "Placement";}

      public override bool Place(PieceType piece, Coords location)
      {
         try
         {
            if (piece == PieceType.ROUND && CountPieces(PieceType.ROUND) >= GameMaster.MAX_ROUND_PIECES)
            {
               return false;
            }
            else if (piece == PieceType.SQUARE && CountPieces(PieceType.SQUARE) >= GameMaster.MAX_SQUARE_PIECES)
            {
               return false;
            }
            else
            {
               return Board.PlacePiece(Controller, piece, Map.Square(location));
            }
         }
         finally
         {
            if (CountPieces() >= GameMaster.MAX_ROUND_PIECES + GameMaster.MAX_SQUARE_PIECES)
            {
               Context.SwapState(new TurnFinishedState(Context));
            }
         }
      }

      private int CountPieces()
      {
         int count = 0;
         foreach (Piece piece in Board.Pieces.Values)
         {
            if (piece.Owner == Controller) count++;
         }

         return count;
      }

      private int CountPieces (PieceType type)
      {
         int count = 0;
         foreach (Piece piece in Board.Pieces.Values)
         {
            if (piece.Owner == Controller && piece.Type == type) count++;
         }

         return count;
      }
   }
   public class MovePieceState : TurnState
   {
      public MovePieceState(GameTurn context) : base(context) { FriendlyName = "Movement"; }
      private int movements = 0;

      public override bool Move(Coords piece, Coords target)
      {
         bool success;
         if (ValidMoves(piece).Length == 0)
            success = false;
         else
         {
            Piece boardPiece = Board.Pieces[Map.Square(piece)];
            success = boardPiece.CanMove(Map.Square(target));
            boardPiece.Move(Map.Square(target));
         }

         if (success) movements++;
         if (movements >= GameMaster.MOVES_PER_TURN) Context.SwapState(new PushPieceState(Context));

         return success;
      }

      public override void Skip()
      {
         Context.SwapState(new PushPieceState(Context));
      }
   }

   public class PushPieceState : TurnState
   {
      public PushPieceState(GameTurn context) : base(context) { FriendlyName = "Pushing"; }
      private int pushes = 0;

      public override bool Push(Coords piece, Coords target)
      {
         bool success;
         if (ValidPushes(piece).Length == 0)
            success = false;
         else
         {
            Piece boardPiece = Board.Pieces[Map.Square(piece)];
            success = boardPiece.Push.CanPush(Map.Square(target));
            boardPiece.Push.Push(Map.Square(target));
         }

         if (success) pushes++;
         if (pushes >= GameMaster.PUSHES_PER_TURN) Context.SwapState(new PushPieceState(Context));

         return success;
      }

      public override void Skip()
      {
         Context.SwapState(new TurnFinishedState(Context));
      }
   }

   public class TurnFinishedState : TurnState
   {
      public TurnFinishedState(GameTurn context) : base(context) { }
   }

   public class GameTurn
   {
      public Player TurnPlayer {get; private set;}
      public Board Board { get; private set; }
      private TurnState State;
      private List<TurnListener> Listeners = new List<TurnListener>();

      public GameTurn(Board board, Player player, int roundNo)
      {
         Board = board;
         TurnPlayer = player;
         Listeners.ForEach(listener => listener.TurnBegin(TurnPlayer));

         if (roundNo == 0)
            SwapState(new PlacementTurnState(this));
         else
            SwapState(new MovePieceState(this));
      }

      public void SwapState(TurnState newState)
      {
         State = newState;

         if (State.GetType() == typeof (TurnFinishedState))
         {
            Listeners.ForEach(listener => listener.TurnOver());
         }
         else
         {
            Listeners.ForEach(listener => listener.ChangedPhase(State.FriendlyName));
         }
      }

      public GameControls Control()
      {
         return State;
      }

      public void Subscribe(TurnListener listener)
      {
         Listeners.Add(listener);
      }
   }

   public interface TurnListener
   {
      void ChangedPhase(string name);
      void TurnBegin(Player player);
      void TurnOver();
   }

   public interface GameListener
   {
      void GameOver(Player winner);
   }

   public class GameMaster : TurnListener
   {
      private List<GameListener> Listeners = new List<GameListener>();

      public const int MAX_ROUND_PIECES = 2;
      public const int MAX_SQUARE_PIECES = 3;
      public const int MOVES_PER_TURN = 2;
      public const int PUSHES_PER_TURN = 1;

      private Board board;
      private Player roundStarter;
      public GameTurn currentTurn {get; private set;}
      public int round {get; private set;}

      public GameMaster ()
      {
			Reset ();
      }

      public void Reset()
      {
         board = Board.CreateFromFile("board.txt");
         round = 0;
         roundStarter = (Player) Enum.GetValues(typeof (Player)).GetValue(new Random().Next(0, 1));

         currentTurn = new GameTurn(board,roundStarter, round);
         currentTurn.Subscribe(this);
      }

      public GameControls Control()
      {
         return currentTurn.Control();
      }

      public void TurnBegin(Player player)
      {
         // meh.
      }

      public void ChangedPhase(string name)
      {
         // meh?
      }

      public void TurnOver()
      {
         if (board.Winner() != null)
         {
            Listeners.ForEach(listener => listener.GameOver((Player)board.Winner()));
            return;
         }

         Player otherPlayer = roundStarter == Player.P1 ? Player.P2 : Player.P1;

         if (currentTurn.TurnPlayer == roundStarter)
         {
            currentTurn = new GameTurn(board,otherPlayer, round);
         }
         else
         {
            round++;
            currentTurn = new GameTurn(board,roundStarter, round);
         }
      }

      public Player? Winner()
      {
         return board.Winner();
      }

      public void Subscribe(GameListener listener)
      {
         Listeners.Add(listener);
      }
   }
}
