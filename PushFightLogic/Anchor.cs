using System;
using SeeSharpMessenger;

namespace PushFightLogic
{	
	/// <summary>
	/// Tracks the piece the anchor currently sits on top of.
	/// </summary>
	// The purpose of the anchor is 
	public class Anchor
	{
		public Piece SitsAtop {get; private set;}
		
		public Anchor ()
		{
			SitsAtop = null;	
		}
		
		/// <summary>
		/// Moves the anchor.
		/// </summary>
		/// <param name='newPiece'>
		/// A valid piece placed on the board.
		/// </param>
		public void MoveAnchor (Piece newPiece)
		{
			SitsAtop = newPiece;
			Messenger<Coords>.Invoke ("piece.anchored", newPiece.Occupies.Pos);
		}
		
		/// <summary>
		/// Reset this instance.
		/// </summary>
		public void Reset ()
		{
			Messenger.Invoke ("piece.unanchored");
			SitsAtop = null;	
		}
	}
}

