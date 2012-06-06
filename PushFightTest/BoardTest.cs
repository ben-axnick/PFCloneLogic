using System;
using NUnit.Framework;
using PushFightLogic;

namespace PushFightTest
{
	[TestFixture]
	public class BoardTest
	{
		[Test]
		public void TestKeyPointsOfLoadedBoard()
		{
         Board board = Board.CreateFromFile("board.txt");
         Assert.AreEqual(BoardSquareType.RAIL, board.Squares[2, 0].Type);
         Assert.AreEqual(BoardSquareType.EDGE, board.Squares[1, 1].Type);
         Assert.AreEqual(BoardSquareType.NORMAL, board.Squares[3, 2].Type);
		}

      [Test]
      public void TestLoadedSquareCoords()
      {
         Board board = Board.CreateFromFile("board.txt");
         Assert.AreEqual(3, board.Squares[3, 2].Pos.x);
         Assert.AreEqual(2, board.Squares[3, 2].Pos.y);
      }

      [Test]
      public void TestLegalPiecePlacement()
      {
         Board board = Board.CreateFromFile("board.txt");
         bool result = board.PlacePiece(Player.P1, PieceType.ROUND, board.Squares[3,2]);
         Assert.IsTrue(result);
         Assert.IsTrue(board.Pieces.ContainsKey(board.Squares[3,2]));
         Assert.AreSame(board.Pieces[board.Squares[3,2]].Occupies, board.Squares[3,2]);
      }

      [Test]
      public void TestTwoPiecesOneSquare()
      {
         Board board = Board.CreateFromFile("board.txt");
         board.PlacePiece(Player.P1, PieceType.ROUND, board.Squares[3, 2]);
         bool result = board.PlacePiece(Player.P1, PieceType.ROUND, board.Squares[3, 2]);
         Assert.IsFalse(result);
      }

      [Test]
      public void TestPlaceOutOfBounds()
      {
         Board board = Board.CreateFromFile("board.txt");
         bool result = board.PlacePiece(Player.P1, PieceType.ROUND, board.Squares[0, 0]);
         Assert.IsFalse(result);
      }

      [Test]
      public void TestPlaceOutOfTerritory()
      {
         Board board = Board.CreateFromFile("board.txt");
         bool result = board.PlacePiece(Player.P1, PieceType.ROUND, board.Squares[4, 2]);
         Assert.IsFalse(result);

         result = board.PlacePiece(Player.P2, PieceType.ROUND, board.Squares[3, 2]);
         Assert.IsFalse(result);
      }
	}
}

