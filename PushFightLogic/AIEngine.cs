using System;
using System.Collections.Generic;
using SeeSharpMessenger;
using System.Linq;
using System.Diagnostics;

namespace PushFightLogic
{
	public class AIEngine
	{
		private GameMaster Master;
		private Player Controlling = Player.P2;
		
		public Action<object> DebugFn = (blarg) => {};
		
		public AIEngine (GameMaster master)
		{
			Master = master;
		}
		
		public void Act (string phase)
		{
			if (Master.Turn.TurnPlayer != Controlling)
				throw new NotSupportedException ("The AI only plays for P2 position currently");
			
			if (phase == "Placement")
				DoPlacement ();
			else
				DoMinMax();
		}
		
		 void DoPlacement ()
		{
			Master.Turn.Control ().Place (PieceType.ROUND, new Coords () {x = 5, y = 2});
			Master.Turn.Control ().Place (PieceType.ROUND, new Coords () {x = 5, y = 3});
			Master.Turn.Control ().Place (PieceType.SQUARE, new Coords () {x = 5, y = 1});
			Master.Turn.Control ().Place (PieceType.SQUARE, new Coords () {x = 5, y = 4});
			Master.Turn.Control ().Place (PieceType.SQUARE, new Coords () {x = 6, y = 3});
		}
		
		const float IS_WIN = 100000;
		const float IS_LOSS = -100000;
		
		private static int nodesEvaluated = 0;
		void DoMinMax ()
		{
			DebugFn ("Begin message suppression, determine all possible plays");
			Messenger.Suppress = true;
			nodesEvaluated = 0;
			
			Board root = GameMaster.AIClone (Master);
			MinMax (0, new ActionChain () {board = root});
			var bestAction = Candidate;
			Messenger.Suppress = false;
			DebugFn ("Finished determining " + nodesEvaluated + " plays");		
			
			if (bestAction.actions.Count > 3)
				throw new Exception ("Max actions for one turn should be 3, got " + bestAction.actions.Count);
			
			if (bestAction == null)
				throw new Exception ("Best possible action chain should never be null");
					
			DebugFn ("Winning action of score " + bestAction.score + " chosen.");
			DebugFn (bestAction);	
			
			CarryOutBestAction (bestAction);
		}

		const int MAX_PLIES = 1;
		static ActionChain Candidate;
		float MinMax (int depth, ActionChain continuesChain)
		{
			Player nextTurnTaker = (depth % 2) == 1 ? Player.P1 : Player.P2; // first turn to evaluate is P2
			
			List<ActionChain> turnActions = PlayOneTurn (nextTurnTaker, continuesChain.board);
         
         Action<ActionChain> ScoringFn = (node) => {
				nodesEvaluated++;
				
				if (node.board.Winner () != null) {
					node.score = node.board.Winner () == Controlling ? IS_WIN : IS_LOSS;
				} else if (depth >= MAX_PLIES) {
					node.score = ScoreBoard (Controlling, node.board);
				} else {
					node.score = MinMax (depth + 1, node);
				}
			};

         if (depth == 0)
			   turnActions.AsParallel<ActionChain>().ForAll(node => ScoringFn(node));
         else
            turnActions.ForEach(node => ScoringFn(node));

			turnActions.Sort ((a,b) => {
				return a.score.CompareTo (b.score);}
			);
			
			if (Controlling == nextTurnTaker) {
				Candidate = turnActions.Last ();
			} else {
				Candidate = turnActions.First ();
			}
			
			DebugFn (nodesEvaluated);
			return Candidate.score;
		}
		
		void CarryOutBestAction (ActionChain best)
		{
			for (int i = 0; i < 3 && i < best.actions.Count; i++) {
				ActionPair pair = best.actions [i];
				if (pair.action == ActionTaken.SKIPPED) {
					DebugFn ("AI SKIP " + Master.Turn.Phase ());
					Master.Turn.Control ().Skip ();
				} else if (pair.action == ActionTaken.MOVED) {
					DebugFn ("AI MOVE " + pair.fromLoc.ToString () + " " + pair.toLoc.ToString ());
					Master.Turn.Control ().Move (pair.fromLoc, pair.toLoc);	
				} else if (pair.action == ActionTaken.PUSHED) {
					DebugFn ("AI PUSH " + pair.fromLoc.ToString () + " " + pair.toLoc.ToString ());
					Master.Turn.Control().Push(pair.fromLoc, pair.toLoc);	
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
			public Board board;
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
				List<ActionPair> newAction = new List<ActionPair> ();
				newAction.AddRange (previous.actions);
				newAction.AddRange (actions);
				actions = newAction;
			}
			
			public bool LastMoveSkipped ()
			{
				return actions [actions.Count - 1].action == ActionTaken.SKIPPED;
			}
		}
		
      bool IsStupidMove(ActionChain chain)
      {
         ActionPair moveProposal = chain.actions.Last();

         if (moveProposal.action == ActionTaken.SKIPPED)
         {
            return false;
         }

         Board board = chain.board;
         Piece movedPiece = board.Pieces[board.Squares[moveProposal.toLoc.x, moveProposal.toLoc.y]];

         if (movedPiece.Occupies.AdjacentSquares()
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
			List<ActionChain> allActions = new List<ActionChain> (8192);
         List<ActionChain> moveActions = new List<ActionChain> (4096);
			
			foreach (ActionChain firstMove in DoMovement(p, root)) {
				if (firstMove.LastMoveSkipped ()) {
					moveActions.Add (firstMove);
					continue;
				}

            if (IsStupidMove(firstMove))
            {
               continue;
            }

				foreach (ActionChain secondMove in DoMovement(p, firstMove.board)) {
               if (secondMove.actions.Last().fromLoc.Equals(firstMove.actions.Last().toLoc))
                  {
                     continue;
                  }

                  if (IsStupidMove(secondMove))
                  {
                     continue;
                  }
					secondMove.Link (firstMove);
					moveActions.Add (secondMove);
				}
			}
			
			foreach (ActionChain moveSet in moveActions) {
				foreach (ActionChain pushSet in DoPushes(p, moveSet.board)) {
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
				chain.board = moveBoard;
				
				actions.Add (chain);
			}
			
			ActionChain skipChain = new ActionChain ();
			skipChain.actions.Add (new ActionPair () {action = ActionTaken.SKIPPED});
			skipChain.board = board;
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
			Coords position = pc.Occupies.Pos;
			if (
				(position.x == 4 || position.x == 5) &&
				(position.y == 2 || position.y == 3))
				value = 50;
			else if (pc.Occupies.AdjacentSquares().Find (tile => tile.Type == BoardSquareType.EDGE) != null) {
				if (pc.Push.AmIAnchored())
					value = -50;
				else
					value = -100;
			} else {
				value = 20;
			}
			
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

