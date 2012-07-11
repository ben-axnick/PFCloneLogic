using System;
using System.Collections.Generic;

namespace PushFightLogic
{
/// <summary>
/// A single cell on the Board, is considered inconsistent until AdjacentSquaresInit is called.
/// </summary>
public class BoardSquare : IEquatable<BoardSquare>
{
	public Coords Pos; // { get; private set;} Removed for AI speed boost
	public BoardSquareType Type; // {get; private set;} Removed for AI speed boost
		
	private Board Parent;

		
	public BoardSquare (Board parent, BoardSquareType squareType, Coords pos)
	{
		Parent = parent;
		Pos = pos;
		Type = squareType;
	}


	public List<BoardSquare> Adjacent { get; private set; }

	/// <summary>
	/// Required call after all squares have been initialised, caches adjacent squares in a list for AI speed.
	/// </summary>
	public void AdjacentSquaresInit ()
	{
		Adjacent = new List<BoardSquare> (4);
		var SquareArr = Parent.Squares;

		if (Pos.x - 1 >= 0)
		{
			Adjacent.Add (SquareArr [Pos.x - 1, Pos.y]);
		}
		if (Pos.x + 1 < Parent.Width)
		{
			Adjacent.Add (SquareArr [Pos.x + 1, Pos.y]);
		}
		if (Pos.y - 1 >= 0)
		{
			Adjacent.Add (SquareArr [Pos.x, Pos.y - 1]);
		}
		if (Pos.y + 1 < Parent.Height)
		{
			Adjacent.Add (SquareArr [Pos.x, Pos.y + 1]);
		}
	}

	/// <summary>
	/// Check if this square contains a piece
	/// </summary>
	/// <returns>
	/// True, if a piece is contained.
	/// </returns>
	public bool ContainsPiece ()
	{
		return Parent.Pieces.ContainsKey (this);
	}

	/// <summary>
	/// Get the piece this square contains
	/// </summary>
	/// <returns>
	/// The piece this square contains, null if it does not have one.
	/// </returns>
	public Piece ContainedPiece ()
	{
		Piece returnPiece = null;
		Parent.Pieces.TryGetValue (this, out returnPiece);

		return returnPiece;
	}

	/// <summary>
	/// Determines whether the specified square shares the same Coords
	/// </summary>
	/// <param name='other'>
	/// The <see cref="PushFightLogic.BoardSquare"/> to compare with the current <see cref="PushFightLogic.BoardSquare"/>.
	/// </param>
	/// <returns>
	/// <c>true</c> if the specified <see cref="PushFightLogic.BoardSquare"/> is equal to the current
	/// <see cref="PushFightLogic.BoardSquare"/>; otherwise, <c>false</c>.
	/// </returns>
	public bool Equals (BoardSquare other)
	{
		return Pos.Equals (other.Pos);
	}


	public override int GetHashCode ()
	{
		return Pos.GetHashCode ();
	}
}
}

