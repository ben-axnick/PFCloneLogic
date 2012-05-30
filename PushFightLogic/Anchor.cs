using System;

namespace PushFightLogic
{	
	public class Anchor
	{
		public Piece SitsAtop {get; private set;}
		
		public void MoveAnchor(Piece newPiece)
		{
			SitsAtop = newPiece;
		}
	}
}

