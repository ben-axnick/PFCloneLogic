using System;
using System.Collections.Generic;
using SeeSharpMessenger;
using System.Linq;
using System.Diagnostics;
using RaptorDB;
using System.Threading.Tasks;
using System.Collections.Concurrent;

namespace PushFightLogic
{
	public class AIEngine
	{
      private RaptorDB<string> DB;
     const string DB_PATH = "ai.fdb";
		private GameMaster Master;
		private Player Controlling = Player.P2;
		
		public Action<object> DebugFn = (blarg) => {};
		private List<Action> Instructions = new List<Action>();
		private int nextInstruction;

      public bool KeepSearching {get; set;}

		public AIEngine (GameMaster master)
		{
			Master = master;
         DB = RaptorDB<string>.Open(System.IO.Path.GetFullPath(DB_PATH), false);
		}
		
		public void Act (string phase)
		{
			if (Master.Turn.TurnPlayer != Controlling)
				throw new NotSupportedException ("The AI only plays for P2 position currently");
	
			Messenger.Suppress = true;
			NodesEvaluated = 0;
         	KeepSearching = true;
			nextInstruction = 0;

			if (phase == "Placement")
				DoPlacement ();
			else
				DoMinMax();
			
			Messenger.Suppress = false;
		}
		
		 void DoPlacement ()
		{
         Instructions.Clear();
			Instructions.Add( () => {Master.Turn.Control ().Place (PieceType.ROUND, new Coords () {x = 5, y = 2});});
			Instructions.Add( () => {Master.Turn.Control ().Place (PieceType.ROUND, new Coords () {x = 5, y = 3});});
			Instructions.Add( () => {Master.Turn.Control ().Place (PieceType.SQUARE, new Coords () {x = 5, y = 1});});
			Instructions.Add( () => {Master.Turn.Control ().Place (PieceType.SQUARE, new Coords () {x = 5, y = 4});});
         Instructions.Add( () => {Master.Turn.Control().Place(PieceType.SQUARE, new Coords() { x = 6, y = 3 });});
		}
		
		const float IS_WIN = 100000;
		const float IS_LOSS = -100000;
		
		public int NodesEvaluated  {get; private set;}

		void DoMinMax ()
		{
			DebugFn ("Determining all possible plays");
			Board root = GameMaster.AIClone (Master);
			var bestAction = MinMax0 (new ActionChain () {Board = root});

			DebugFn ("Finished determining " + NodesEvaluated + " plays");		
			
         	Board.Pool.Release(root);

			if (bestAction.actions.Count > 3)
				throw new Exception ("Max actions for one turn should be 3, got " + bestAction.actions.Count);
			
			if (bestAction == null)
				throw new Exception ("Best possible action chain should never be null");
					
			DebugFn ("Winning action of score " + bestAction.score + " chosen.");
			DebugFn (bestAction);	
			
			StoreBestAction (bestAction);
		}

		public bool ExecuteNextInstruction ()
		{
			if (nextInstruction < Instructions.Count) {
				Instructions [nextInstruction++] ();
				return true;
			} else {
				return false;
			}
		}

		const int MAX_PLIES = 1;

		/**
		 * Test win conditions, depth, and external signals to check whether to cut off or not.
		 * 
		 * Return true if terminal, else false.
		 */
		bool ApplyCutoff (ActionChain node, int depth)
		{
			Player? thisNodeWinner = node.Board.Winner ();

			if (thisNodeWinner != null) {
				node.score = thisNodeWinner == Controlling ? IS_WIN : IS_LOSS;
				return true;
			} else if (depth >= MAX_PLIES || KeepSearching == false) {
				node.score = ScoreBoard (Controlling, node.Board);
				return true;
			}

			return false;
		}

		/**
		 * Determine a score by applying the scoring function, looking up the database, or recursing deeper.
		 */
		void ScoringFn (int depth, ActionChain node)
		{
			byte[] dbrec = new byte[4];
			bool storedInDB = DB.Get (node.Board.GUID (), out dbrec);

			bool isTerminal = ApplyCutoff (node, depth);

			if (!isTerminal) {
				if (storedInDB) {
					node.score = BitConverter.ToSingle (dbrec, 0);
				} else {
					node.score = MinMax (depth + 1, node);
							
					// Store the score if it was obtained from a full search only
					if (depth == 0 && KeepSearching) {
						RecordNodeInDB (node.Board, node.score);
					}
				}
			}

			node.ClearBoard ();
		}

		/**
		 * The base level minmax function, which utilises a different return type and parellelises the search
		 */
		ActionChain MinMax0 (ActionChain baseChain)
		{
			List<ActionChain> turnActions = PlayOneTurn (Controlling, baseChain.Board);
			NodesEvaluated += turnActions.Count;

			// execute in parallel for speed boost, shuffle to introduce nondeterminism between same scores
			int nodesBatch = 100;
			Parallel.ForEach(Partitioner.Create(0, turnActions.Count, nodesBatch), range => {
				for(int i = range.Item1; i < range.Item2; i++)
				{
					ScoringFn(0, turnActions[i]);
				}
			});

            turnActions.Shuffle();

			turnActions.Sort ((a,b) => {
				return a.score.CompareTo (b.score);}
			);
			
			return turnActions.Last();
		}

		/**
		 * Recursively called to determine the utility of every move available up to MAX_PLIES
		 */
		float MinMax (int depth, ActionChain continuesChain)
		{
			Player nextTurnTaker = (depth % 2) == 1 ? Player.P1 : Player.P2; // first turn to evaluate is P2
			
			List<ActionChain> turnActions = PlayOneTurn (nextTurnTaker, continuesChain.Board);
			NodesEvaluated += turnActions.Count;

			turnActions.ForEach(node => ScoringFn(depth, node));

			if (Controlling == nextTurnTaker) {
				return turnActions.Max(node => node.score);
			} else {
				return turnActions.Min(node => node.score);
			}
		}

      private void RecordNodeInDB(Board board, float p)
      {
         byte[] dbrec = BitConverter.GetBytes(p);
         foreach (string guid in board.AllGUIDs())
         {
            DB.Set(guid, dbrec);
         }

		 dbrec = BitConverter.GetBytes(-p);         
		 foreach (string guid in board.AllGUIDs(true))
         {
            DB.Set(guid, dbrec);
         }
      }
		
		void StoreBestAction (ActionChain best)
		{
         Instructions.Clear();
			for (int i = 0; i < 3 && i < best.actions.Count; i++) {
				ActionPair pair = best.actions [i];
				if (pair.action == ActionTaken.SKIPPED) {
					DebugFn ("AI SKIP " + Master.Turn.Phase ());
					Instructions.Add( () => {Master.Turn.Control ().Skip ();});
				} else if (pair.action == ActionTaken.MOVED) {
					DebugFn ("AI MOVE " + pair.fromLoc.ToString () + " " + pair.toLoc.ToString ());
					Instructions.Add( () => {Master.Turn.Control ().Move (pair.fromLoc, pair.toLoc);});
				} else if (pair.action == ActionTaken.PUSHED) {
					DebugFn ("AI PUSH " + pair.fromLoc.ToString () + " " + pair.toLoc.ToString ());
               Instructions.Add(() => { Master.Turn.Control().Push(pair.fromLoc, pair.toLoc); });	
				}
            else
            {
               throw new Exception("Unknown AI action");
            }
			}
		}
		
		enum ActionTaken {
			SKIPPED, PUSHED, MOVED	
		}
		
		struct ActionPair
		{	
			public ActionTaken action;
			public Coords fromLoc;
			public Coords toLoc;
		}
		
		class ActionChain
		{
			public List<ActionPair> actions;

         private Board board = null;
			public Board Board {get {return board;} set { 
               if (board != null) ClearBoard();
               board = value;}}

			public float score;
			public ActionChain()
			{
			 	actions = new List<ActionPair> ();	
			}
			
			public ActionChain(ActionChain link)
			{
				actions.AddRange(link.actions);
			}
			
			public void Link (ActionChain previous)
			{
            previous.ClearBoard();
				List<ActionPair> newAction = new List<ActionPair> ();
				newAction.AddRange (previous.actions);
				newAction.AddRange (actions);
				actions = newAction;
			}
			
			public bool LastMoveSkipped ()
			{
				return actions [actions.Count - 1].action == ActionTaken.SKIPPED;
			}

         public void ClearBoard ()
         {
            if (board == null) return;
            Board.Pool.Release(board);
            board = null;
         }
		}
		
      bool IsStupidMove(ActionChain chain)
      {
         ActionPair moveProposal = chain.actions.Last();

         if (moveProposal.action == ActionTaken.SKIPPED)
         {
            return false;
         }

         Board board = chain.Board;
         Piece movedPiece = board.Pieces[board.Squares[moveProposal.toLoc.x, moveProposal.toLoc.y]];

         if (movedPiece.Occupies.Adjacent
               .Any(square => square.Type == BoardSquareType.EDGE) &&
               movedPiece.Push.CheckPushes().Count == 0)
         {
            return true;
         }
         else
         {
            return false;
         }
      }

		List<ActionChain> PlayOneTurn (Player p, Board root)
		{
			List<ActionChain> allActions = new List<ActionChain> (3000);
         List<ActionChain> moveActions = new List<ActionChain> (1500);
			
			foreach (ActionChain firstMove in DoMovement(p, root)) {
				if (firstMove.LastMoveSkipped ()) {
					moveActions.Add (firstMove);
					continue;
				}

            if (IsStupidMove(firstMove))
            {
               firstMove.ClearBoard();
               continue;
            }

            foreach (ActionChain secondMove in DoMovement(p, firstMove.Board))
            {
               secondMove.Link(firstMove);
               if (secondMove.actions.Last().fromLoc.Equals(firstMove.actions.Last().toLoc))
                  {
                     secondMove.ClearBoard();
                     continue;
                  }

                  if (IsStupidMove(secondMove))
                  {
                     secondMove.ClearBoard();
                     continue;
                  }
					moveActions.Add (secondMove);
				}
			}
			
			foreach (ActionChain moveSet in moveActions) {
				foreach (ActionChain pushSet in DoPushes(p, moveSet.Board)) {
					pushSet.Link (moveSet);

					allActions.Add (pushSet);
				}
			}
			
			return allActions;
		}
				
		List<ActionChain> DoPushes (Player p, Board board)
		{
			return DoAction (p, board, AllPushes, PerformPush);
		}
		
		List<ActionChain> DoMovement (Player p, Board board)
		{
			return DoAction (p, board, AllMoves, PerformMove);
		}
		
		List<ActionChain> DoAction (Player p, Board board, 
		                            Func<List<Piece>,IEnumerable<ActionPair>> checkActionsFn, 
		                            Action<Board,ActionPair> actionFn)
		{
			List<ActionChain> actions = new List<ActionChain> (4096);
			
			List<Piece> myPieces = new List<Piece> ();
			foreach (Piece pc in board.Pieces.Values) {
				if (pc.Owner == p)
					myPieces.Add (pc);
			}
			
			foreach (ActionPair move in checkActionsFn(myPieces)) {
				ActionChain chain = new ActionChain ();
				chain.actions.Add (move);
				Board moveBoard = Board.AIClone (board);
				actionFn (moveBoard, move);
				chain.Board = moveBoard;
				
				actions.Add (chain);
			}
			
			ActionChain skipChain = new ActionChain ();
			skipChain.actions.Add (new ActionPair () {action = ActionTaken.SKIPPED});
			skipChain.Board = Board.AIClone(board);
			actions.Add (skipChain);
			
			return actions;		
		}
		
		void PerformPush (Board moveBoard, ActionPair move)
		{
			moveBoard.Pieces [moveBoard.Squares [move.fromLoc.x, move.fromLoc.y]]
				.Push.Push(moveBoard.Squares [move.toLoc.x, move.toLoc.y]);
		}
		
		IEnumerable<ActionPair> AllPushes (List<Piece> pieces)
		{
         var result = pieces.SelectMany(pc => pc.Push.CheckPushes().Select(square => new ActionPair() {
				action = ActionTaken.PUSHED,
				fromLoc = pc.Occupies.Pos,
				toLoc = square.Pos}));

         return result;
		}
		
		void PerformMove (Board moveBoard, ActionPair move)
		{
			moveBoard.Pieces [moveBoard.Squares [move.fromLoc.x, move.fromLoc.y]]
				.Move (moveBoard.Squares [move.toLoc.x, move.toLoc.y]);
		}
		
		IEnumerable<ActionPair> AllMoves (List<Piece> pieces)
      {
         var result = pieces.SelectMany(pc => pc.CheckMoves().Select(square => new ActionPair()
         {
            action = ActionTaken.MOVED,
            fromLoc = pc.Occupies.Pos,
            toLoc = square.Pos
         }));

         return result;
		}
		

		static float CalculateValue (Piece pc)
		{
			int value = 0;
			int possibleMovesCount = pc.CheckMoves ().Count;
			
			Coords position = pc.Occupies.Pos;
			if (
				(position.x == 4 || position.x == 5) &&
				(position.y == 2 || position.y == 3))
				value = 100;
			else if (pc.Occupies.Adjacent.Find (tile => tile.Type == BoardSquareType.EDGE) != null) {
				if (pc.Push.AmIAnchored ())
					value = -20;
				else if (possibleMovesCount == 0)
					value = -1000;
				else
					value = -500;
			} else {
				value = 0;
			}
			
			value += possibleMovesCount * 4;
			return value;
		}
		
		static float ScoreBoard (Player p, Board brd)
		{
			List<Piece> myPieces = new List<Piece> ();
			List<Piece> theirPieces = new List<Piece> ();
			foreach (Piece pc in brd.Pieces.Values) {
				if (pc.Owner == p)
					myPieces.Add (pc);
				else
					theirPieces.Add (pc);
			}
			
			if (brd.Winner () == p)
				return IS_WIN;
			
			float score = 0;
			
			myPieces.ForEach (piece => score += CalculateValue (piece));
			theirPieces.ForEach (piece => score -= CalculateValue (piece));
			
			return score;
		}
	}
}

