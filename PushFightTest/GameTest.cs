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
      public void PlayOutOneTurn()
      {
         GameMaster master = new GameMaster();
         master.Control().ViewBoard();
         master.Control().Place(PieceType.ROUND, new Coords(){x = 3, y = 3});
      }
   }
}
