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
/// <summary>
/// Provides a MinMax search plus heuristic in order to allow an AI to plan a turn.
/// </summary>
public class AIEngine
{
	private RaptorDB<string> DB;
	const string DB_PATH = "ai.fdb";
	private GameMaster Master;
	private Player Controlling = Player.P2;
	public Action<object> DebugFn = (blarg) => {};
	private List<Action> Instructions = new List<Action> ();
	private int nextInstruction;

	/// <summary>
	/// A flag set to true whenever Act is called, setting it to false causes the search to be terminated.
	/// </summary>
	/// <remarks>
	/// Note: No guarantees on termination time when the flag is set false - Instructions will change from
	/// an empty to a populated list when calculations are completed, so check that
	/// </remarks>
	/// <value>
	/// <c>true</c> if keep searching; otherwise, <c>false</c>.
	/// </value>
	public bool KeepSearching { get; set; }


	public AIEngine (GameMaster master)
	{
		Master = master;
		DB = RaptorDB<string>.Open (System.IO.Path.GetFullPath (DB_PATH), false);
	}

	private static bool firstRun = true;
	
	/// <summary>
	/// Generate a <c>List<Action></c> of instructions according to the specified phase.
	/// </summary>
	/// <param name='phase'>
	/// Expected to be either "Placement" or "Movement"
	/// </param>
	/// <remarks>
	/// All message passing is disabled whilst the search is underway, as
	/// the boards generated while searching the space are normal board objects
	/// that would otherwise emit billions of confusing messages to clients.
	/// </remarks>
	public void Act (string phase)
	{
		if (firstRun)
		{
			firstRun = false;
			Master.AIPools();
		}
		
		if (Master.Turn.TurnPlayer != Controlling)
		{
			throw new NotSupportedException ("The AI only plays for P2 position currently");
		}
	
		Messenger.Suppress = true;
		NodesEvaluated = 0;
		KeepSearching = true;
		nextInstruction = 0;

		if (phase == "Placement")
		{
			DoPlacement ();
		}
		else
		{
			DoMinMax ();
		}
			
		Messenger.Suppress = false;
	}

	/// <summary>
	/// The placement process involves no search, it always generates the same 5 placement instructions.
	/// </summary>
	void DoPlacement ()
	{
		Instructions.Clear ();
		Instructions.Add (() => {
			Master.Turn.Control ().Place (PieceType.ROUND, new Coords () {x = 5, y = 2});});
		Instructions.Add (() => {
			Master.Turn.Control ().Place (PieceType.ROUND, new Coords () {x = 5, y = 3});});
		Instructions.Add (() => {
			Master.Turn.Control ().Place (PieceType.SQUARE, new Coords () {x = 5, y = 1});});
		Instructions.Add (() => {
			Master.Turn.Control ().Place (PieceType.SQUARE, new Coords () {x = 5, y = 4});});
		Instructions.Add (() => {
			Master.Turn.Control ().Place (PieceType.SQUARE, new Coords () { x = 6, y = 3 });});
	}

		
	const float IS_WIN = 100000;
	const float IS_LOSS = -100000;

	/// <summary>
	/// Query to evaluate how far along the search process has come
	/// </summary>
	/// <value>
	/// Number of terminal nodes reached so far
	/// </value>
	public int NodesEvaluated  { get; private set; }

	/// <summary>
	/// Initiates the MinMax search, verifies the results, then stores the actions to be taken via <c>StoreBestAction</c>
	/// </summary>
	void DoMinMax ()
	{
		DebugFn ("Determining all possible plays");
		Board root = GameMaster.AIClone (Master);
		var bestAction = MinMax0 (new ActionChain () {Board = root});

		DebugFn ("Finished determining " + NodesEvaluated + " plays");		
			
		Board.Pool.Release (root);

		if (bestAction.actions.Count > 3)
		{
			throw new Exception ("Max actions for one turn should be 3, got " + bestAction.actions.Count);
		}
			
		if (bestAction == null)
		{
			throw new Exception ("Best possible action chain should never be null");
		}
					
		DebugFn ("Winning action of score " + bestAction.score + " chosen.");
		DebugFn (bestAction);	
			
		StoreBestAction (bestAction);
	}


	/// <summary>
	/// Convenience method that steps through the instructions set for the client
	/// </summary>
	/// <returns>
	/// False, once no more intstructions are available
	/// </returns>
	public bool ExecuteNextInstruction ()
	{
		if (nextInstruction < Instructions.Count)
		{
			Instructions [nextInstruction++] ();
			return true;
		}
		else
		{
			return false;
		}
	}

	/// <summary>
	/// Number of turns to look ahead, this is extremely prohibitive in PushFight.
	/// </summary>
	/// <example>
	/// Setting this to 1 checks all possible moves then all possible responses.
	/// The search space is ~700,000 nodes
	/// </example>
	const int SOFT_PLIES = 1;
	
	/// <summary>
	/// When any board piece is on the edge, nodes will not be considered terminal until they are this deep
	/// </summary>
	const int HARD_PLIES = 2;

	/**
		 * Test win conditions, depth, and external signals to check whether to cut off or not.
		 * 
		 * Return true if terminal, else false.
		 */
	bool ApplyCutoff (ActionChain node, int depth)
	{
		if (node.Board.Winner != null)
		{
			node.score = node.Board.Winner == Controlling ? IS_WIN : IS_LOSS;
			return true;
		}
		else if (depth >= SOFT_PLIES)
		{
			// iterative deepening exception for dangerous positions
			// TODO neaten up the logic
			if (node.Board.Pieces.Values.Any(piece => piece.Occupies.Type == BoardSquareType.EDGE) &&
					depth < HARD_PLIES && KeepSearching)
			{
						return false;
			}
			
			node.score = ScoreBoard (Controlling, node.Board);
			return true;
		}
		else if (KeepSearching == false)
		{
			// Disregard nodes that haven't yet been evaluated fully, fudge it
			// by returning an unfavourable score rather than eliminating it
			node.score = TurnTaker (depth) == Controlling ? IS_LOSS : IS_WIN;
			return true;
		}

		return false;
	}

	/// <summary>
	/// Determine a score by applying the scoring function, looking up the database, or recursing deeper.
	/// </summary>
	void ScoringFn (int depth, ActionChain node)
	{
		byte[] dbrec = new byte[4];

		bool isTerminal = ApplyCutoff (node, depth);

		if (!isTerminal)
		{
			bool storedInDB = DB.Get (node.Board.GUID (), out dbrec);

			if (storedInDB)
			{
				node.score = BitConverter.ToSingle (dbrec, 0);
			}
			else
			{
				node.score = MinMax (depth + 1, node);
							
				// Store the score if it was obtained from a full search only
				if (depth == 0 && KeepSearching)
				{
					RecordNodeInDB (node.Board, node.score);
				}
			}
		}

		node.ClearBoard ();
	}

	/// <summary>
	/// The base level minmax function, which utilises a different return type and parellelises the search
	/// </summary>
	ActionChain MinMax0 (ActionChain baseChain)
	{
		List<ActionChain> turnActions = PlayOneTurn (Controlling, baseChain.Board);
		NodesEvaluated += turnActions.Count;

		// Since we're unlikely to be able to evaluate all nodes on a cold pass, evaluate them in order
		// of immediate potential in order to discover the most fruitful nodes first
		turnActions.ForEach(node => node.score = ScoreBoard(Controlling, node.Board));
		turnActions.Sort ((a,b) => {
			return a.score.CompareTo (b.score);}
		);
		
		// execute in parallel for speed boost, shuffle to introduce nondeterminism between same scores
		int nodesBatch = 100;
		Parallel.ForEach (Partitioner.Create (0, turnActions.Count, nodesBatch), range => {
			for (int i = range.Item1; i < range.Item2; i++)
			{
				ScoringFn (0, turnActions [i]);
			}
		}
		);

		turnActions.Shuffle ();

		turnActions.Sort ((a,b) => {
			return a.score.CompareTo (b.score);}
		);
			
		return turnActions.Last ();
	}

	/// <summary>
	/// Recursively called to determine the utility of every move available up to MAX_PLIES
	/// </summary>
	float MinMax (int depth, ActionChain continuesChain)
	{
		List<ActionChain> turnActions = PlayOneTurn (TurnTaker (depth), continuesChain.Board);
		NodesEvaluated += turnActions.Count;

		turnActions.ForEach (node => ScoringFn (depth, node));

		if (Controlling == TurnTaker (depth))
		{
			return turnActions.Max (node => node.score);
		}
		else
		{
			return turnActions.Min (node => node.score);
		}
	}

	private Player TurnTaker (int depth)
	{
		return (depth % 2) == 1 ? Controlling.Other() : Controlling; // first turn to evaluate is P2
	}

	private void RecordNodeInDB (Board board, float p)
	{
		byte[] dbrec = BitConverter.GetBytes (p);
		foreach (string guid in board.AllGUIDs())
		{
			DB.Set (guid, dbrec);
		}

		dbrec = BitConverter.GetBytes (-p);         
		foreach (string guid in board.AllGUIDs(true))
		{
			DB.Set (guid, dbrec);
		}
	}

		
	void StoreBestAction (ActionChain best)
	{
		Instructions.Clear ();
		for (int i = 0; i < 3 && i < best.actions.Count; i++)
		{
			ActionPair pair = best.actions [i];
			if (pair.action == ActionTaken.SKIPPED)
			{
				DebugFn ("AI SKIP " + Master.Turn.Phase ());
				Instructions.Add (() => {
					Master.Turn.Control ().Skip ();});
			}
			else if (pair.action == ActionTaken.MOVED)
			{
				DebugFn ("AI MOVE " + pair.fromLoc.ToString () + " " + pair.toLoc.ToString ());
				Instructions.Add (() => {
					Master.Turn.Control ().Move (pair.fromLoc, pair.toLoc);});
			}
			else if (pair.action == ActionTaken.PUSHED)
			{
				DebugFn ("AI PUSH " + pair.fromLoc.ToString () + " " + pair.toLoc.ToString ());
				Instructions.Add (() => {
					Master.Turn.Control ().Push (pair.fromLoc, pair.toLoc); });	
			}
			else
			{
				throw new Exception ("Unknown AI action");
			}
		}
	}
		
	enum ActionTaken
	{
		SKIPPED,
		PUSHED,
		MOVED	
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


		public Board Board
		{
			get { return board;}
			set
			{ 
				if (board != null)
				{
					ClearBoard ();
				}
				board = value;
			}
		}


		public float score;


		public ActionChain ()
		{
			actions = new List<ActionPair> ();	
		}

			
		public ActionChain (ActionChain link)
		{
			actions.AddRange (link.actions);
		}

			
		public void Link (ActionChain previous)
		{
			previous.ClearBoard ();
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
			if (board == null)
			{
				return;
			}
			Board.Pool.Release (board);
			board = null;
		}
	}
		
	bool IsStupidMove (ActionChain chain)
	{
		ActionPair moveProposal = chain.actions.Last ();

		if (moveProposal.action == ActionTaken.SKIPPED)
		{
			return false;
		}

		Board board = chain.Board;
		Piece movedPiece = board.Pieces [board.Squares [moveProposal.toLoc.x, moveProposal.toLoc.y]];

		if (movedPiece.Occupies.Adjacent
               .Any (square => square.Type == BoardSquareType.EDGE) &&
			movedPiece.Push.CheckPushes ().Count == 0)
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
			
		foreach (ActionChain firstMove in DoMovement(p, root))
		{
			if (firstMove.LastMoveSkipped ())
			{
				moveActions.Add (firstMove);
				continue;
			}

			if (IsStupidMove (firstMove))
			{
				firstMove.ClearBoard ();
				continue;
			}

			foreach (ActionChain secondMove in DoMovement(p, firstMove.Board))
			{
				secondMove.Link (firstMove);
				if (secondMove.actions.Last ().fromLoc.Equals (firstMove.actions.Last ().toLoc))
				{
					secondMove.ClearBoard ();
					continue;
				}

				if (IsStupidMove (secondMove))
				{
					secondMove.ClearBoard ();
					continue;
				}
				moveActions.Add (secondMove);
			}
		}
			
		foreach (ActionChain moveSet in moveActions)
		{
			foreach (ActionChain pushSet in DoPushes(p, moveSet.Board))
			{
				pushSet.Link (moveSet);
				if (pushSet.actions.Last().action == ActionTaken.SKIPPED) pushSet.Board.NotifyWinner(p.Other());
				
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
		foreach (Piece pc in board.Pieces.Values)
		{
			if (pc.Owner == p)
			{
				myPieces.Add (pc);
			}
		}
			
		foreach (ActionPair move in checkActionsFn(myPieces))
		{
			ActionChain chain = new ActionChain ();
			chain.actions.Add (move);
			Board moveBoard = Board.AIClone (board);
			actionFn (moveBoard, move);
			chain.Board = moveBoard;
				
			actions.Add (chain);
		}
			
		ActionChain skipChain = new ActionChain ();
		skipChain.actions.Add (new ActionPair () {action = ActionTaken.SKIPPED});
		skipChain.Board = Board.AIClone (board);
		actions.Add (skipChain);
			
		return actions;		
	}

		
	void PerformPush (Board moveBoard, ActionPair move)
	{
		moveBoard.Pieces [moveBoard.Squares [move.fromLoc.x, move.fromLoc.y]]
				.Push.Push (moveBoard.Squares [move.toLoc.x, move.toLoc.y]);
	}

		
	IEnumerable<ActionPair> AllPushes (List<Piece> pieces)
	{
		var result = pieces.SelectMany (pc => pc.Push.CheckPushes ().Select (square => new ActionPair () {
				action = ActionTaken.PUSHED,
				fromLoc = pc.Occupies.Pos,
				toLoc = square.Pos}
		)
		);

		return result;
	}

		
	void PerformMove (Board moveBoard, ActionPair move)
	{
		moveBoard.Pieces [moveBoard.Squares [move.fromLoc.x, move.fromLoc.y]]
				.Move (moveBoard.Squares [move.toLoc.x, move.toLoc.y]);
	}

		
	IEnumerable<ActionPair> AllMoves (List<Piece> pieces)
	{
		var result = pieces.SelectMany (pc => pc.CheckMoves ().Select (square => new ActionPair ()
         {
            action = ActionTaken.MOVED,
            fromLoc = pc.Occupies.Pos,
            toLoc = square.Pos
         }
		)
		);

		return result;
	}
		

	static float CalculateValue (Piece pc)
	{
		int value = 0;
		int possibleMovesCount = pc.CheckMoves ().Count;
		int alliesCount = pc.Occupies.Adjacent.Count (square => square.ContainsPiece () && square.ContainedPiece ().Owner == pc.Owner);

		Coords position = pc.Occupies.Pos;
		if (
			(position.x == 4 || position.x == 5) &&
			(position.y == 2 || position.y == 3))
		{
			if (pc.Type == PieceType.ROUND)
			{
				value = 20;
			}
			else
			{
				value = 10;
			}
		}
		else if (pc.Occupies.Adjacent.Find (tile => tile.Type == BoardSquareType.EDGE) != null)
		{
			if (pc.Push.AmIAnchored ())
			{
				value = -5;
			}
			else if (possibleMovesCount == 0 && pc.Push.CheckPushes ().Count == 0)
			{
				value = -50;
			}
			else
			{
				value = -20;
			}
		}
		else
		{
			value = 0;
		}


		value += possibleMovesCount * 1;
		value += alliesCount * 3;

		return value;
	}

		
	static float ScoreBoard (Player p, Board brd)
	{			
		if (brd.Winner == p)
		{
			return IS_WIN;
		}
		else if (brd.Winner == p.Other ())
		{
			return IS_LOSS;
		}
		
		List<Piece> myPieces = new List<Piece> ();
		List<Piece> theirPieces = new List<Piece> ();
		foreach (Piece pc in brd.Pieces.Values)
		{
			if (pc.Owner == p)
			{
				myPieces.Add (pc);
			}
			else
			{
				theirPieces.Add (pc);
			}
		}

			
		float score = 0;
			
		myPieces.ForEach (piece => score += CalculateValue (piece));
		theirPieces.ForEach (piece => score -= CalculateValue (piece));
			
		return score;
	}
	}
}

