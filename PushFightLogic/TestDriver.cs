using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace PushFightLogic
{
   class TestDriver
   {
      public static void Main(string[] args)
      {
         Dictionary<Coords, PieceType> p1Moves = new Dictionary<Coords, PieceType>(){
				{new Coords(){x = 4, y = 1}, PieceType.ROUND},
				{new Coords(){x = 4, y = 2}, PieceType.ROUND},
				{new Coords(){x = 4, y = 3}, PieceType.SQUARE},
				{new Coords(){x = 4, y = 4}, PieceType.SQUARE},
				{new Coords(){x = 3, y = 2}, PieceType.SQUARE}
			};

         GameMaster master = new GameMaster();
         master.Reset();

         foreach (Coords coord in p1Moves.Keys)
         {
            master.Turn.Control().Place(p1Moves[coord], coord);
         }

         
         AIEngine ai = new AIEngine(master);
         ai.DebugFn = Debug;

         ai.Act("Placement");

         master.Turn.Control().Skip();
         master.Turn.Control().Skip();

         ai.Act("Movement");
         Console.ReadLine();
      }

      static void Debug(object obj)
      {
         string output = obj as string;

         if (output != null) Console.WriteLine(output);
      }
   }
}
