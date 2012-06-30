using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using PushFightLogic;
using NUnit.Framework;

namespace PushFightTest
{
[TestFixture]
class GameTest
{
	[Test]
	public void PlayOutOneTurn ()
	{
		Dictionary<Coords,PieceType> p1Moves = new Dictionary<Coords,PieceType> (){
				{new Coords(){x = 4, y = 1}, PieceType.ROUND},
				{new Coords(){x = 4, y = 2}, PieceType.ROUND},
				{new Coords(){x = 4, y = 3}, PieceType.SQUARE},
				{new Coords(){x = 4, y = 4}, PieceType.SQUARE},
				{new Coords(){x = 3, y = 2}, PieceType.SQUARE}
			};

		GameMaster master = new GameMaster ();
		master.Reset ();
			
		foreach (Coords coord in p1Moves.Keys)
		{
			master.Turn.Control ().Place (p1Moves [coord], coord);
		}
         	
		AIEngine ai = new AIEngine (master);
		ai.Act ("Placement");
			
		master.Turn.Control ().Skip ();
		master.Turn.Control ().Skip ();
			
		ai.Act ("Movement");
	}
   }
}
