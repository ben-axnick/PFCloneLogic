using System;
using System.Collections.Generic;
using System.Text;
using NUnit.Framework;
using PushFightLogic;

namespace PushFightTest
{
   [TestFixture]
   class PieceTest
   {
      private Board board;

      [SetUp]
      public void SetupBoardForTests()
      {
         board = Board.CreateFromFile("board.txt");

         board.PlacePiece(Player.P1, PieceType.ROUND, board.Squares[3, 1]);
         board.PlacePiece(Player.P2, PieceType.ROUND, board.Squares[4, 4]);

         board.PlacePiece(Player.P1, PieceType.ROUND, board.Squares[3, 2]);
         board.PlacePiece(Player.P2, PieceType.ROUND, board.Squares[4, 2]);
         board.PlacePiece(Player.P1, PieceType.SQUARE, board.Squares[3, 3]);
         board.PlacePiece(Player.P2, PieceType.SQUARE, board.Squares[4, 3]);
      }

//__=====_
//__#R###_
//###RR###
//###SS###
//_###R#__
//_=====__

      [Test]
      public void RoundPiecePushFails()
      {
         Piece round = board.Pieces[board.Squares[3,2]];
         bool result = round.Push.CanPush(board.Squares[4,2]);
         Assert.IsFalse(result);
      }

      [Test]
      public void RoundCheckPushesTest()
      {
         Piece round = board.Pieces[board.Squares[3, 2]];
         List<BoardSquare> moves = round.Push.CheckPushes();
         Assert.AreEqual(0, moves.Count);
      }

      [Test]
      public void SquarePushBlankFails()
      {
         Piece square = board.Pieces[board.Squares[3, 3]];
         bool result = square.Push.CanPush(board.Squares[2, 3]);
         Assert.IsFalse(result);
      }

      [Test]
      public void SquarePushChainIntoRail()
      {
         Piece square = board.Pieces[board.Squares[3, 3]];
         bool result = square.Push.CanPush(board.Squares[3, 2]);
         Assert.IsFalse(result);
      }

      [Test]
      public void SquareValidPush()
      {
         Piece square = board.Pieces[board.Squares[3, 3]];
         bool result = square.Push.CanPush(board.Squares[4, 3]);
         Assert.IsTrue(result);

         square.Push.Push(board.Squares[4, 3]);
         Assert.AreEqual(square, board.Pieces[board.Squares[4, 3]]);
      }

      [Test]
      public void AnchoredAfterPush()
      {
         Piece square = board.Pieces[board.Squares[3, 3]];
         bool result = square.Push.CanPush(board.Squares[4, 3]);
         Assert.IsTrue(result);

         square.Push.Push(board.Squares[4, 3]);
         Assert.AreEqual(square, board.TheAnchor.SitsAtop);
      }

      [Test]
      public void PieceCheckMovesTest()
      {
         Piece square = board.Pieces[board.Squares[3, 3]];
         List<BoardSquare> moves = square.CheckMoves();
         Assert.AreEqual(10, moves.Count);
      }

      [Test]
      public void PieceCheckMovesAfterMoving()
      {
         Piece square = board.Pieces[board.Squares[3, 3]];
         List<BoardSquare> moves = square.CheckMoves();
         Assert.AreEqual(10, moves.Count);

         board.Pieces[board.Squares[3, 1]].Move(board.Squares[1, 3]);
         moves = square.CheckMoves();
         Assert.AreEqual(20, moves.Count);
      }

      [Test]
      public void SquareCheckPushesTest()
      {
         Piece square = board.Pieces[board.Squares[3, 3]];
         List<BoardSquare> moves = square.Push.CheckPushes();
         Assert.AreEqual(1, moves.Count);
      }

      [Test]
      public void SquareValidMove()
      {
         Piece square = board.Pieces[board.Squares[3, 3]];
         bool result = square.CanMove(board.Squares[1, 2]);
         Assert.IsTrue(result);

         square.Move(board.Squares[1, 2]);
         Assert.AreEqual(square, board.Pieces[board.Squares[1, 2]]);
      }

      [Test]
      public void SquareIllegalMoveStacked()
      {
         Piece square = board.Pieces[board.Squares[3, 3]];
         bool result = square.CanMove(board.Squares[4, 3]);
         Assert.IsFalse(result);

         square.Move(board.Squares[4, 3]);

         Piece movedPiece = null;
         board.Pieces.TryGetValue(board.Squares[4, 3], out movedPiece);
         Assert.AreNotEqual(square, movedPiece);
      }

      [Test]
      public void SquareIllegalMoveImpassable()
      {
         Piece square = board.Pieces[board.Squares[3, 3]];
         bool result = square.CanMove(board.Squares[5, 4]);
         Assert.IsFalse(result);

         square.Move(board.Squares[5, 4]);

         Piece movedPiece = null;
         board.Pieces.TryGetValue(board.Squares[5,4], out movedPiece);
         Assert.AreNotEqual(square, movedPiece);
      }
   }
}
