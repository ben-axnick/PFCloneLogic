using System;
using SeeSharpMessenger;

namespace PushFightLogic
{	
	public class Anchor
	{
		public Piece SitsAtop {get; private set;}
		
		public Anchor ()
		{
			SitsAtop = null;	
		}
		
		public void MoveAnchor (Piece newPiece)
		{
			SitsAtop = newPiece;
			Messenger<Coords>.Invoke ("piece.anchored", newPiece.Occupies.Pos);
		}
		
		public void Reset ()
		{
			Messenger.Invoke ("piece.unanchored");
			SitsAtop = null;	
		}
	}
}

