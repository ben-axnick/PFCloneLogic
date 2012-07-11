// The low level data types used for communication between a client and the underlying game logic.
using System;

namespace PushFightLogic
{
public enum Player
{	
	P1,
	P2
}

public enum BoardSquareType
{
	NORMAL,
	EDGE,
	RAIL	
}

	
public enum PieceType
{
	ROUND,
	SQUARE
}

/// <summary>
/// Cartesian 2D co-ordinates that can be equated with each other
/// </summary>
public struct Coords : IEquatable<Coords>
{
	public int x { get; set; }


	public int y { get; set; }

		#region IEquatable implementation
	public bool Equals (Coords other)
	{
		return x == other.x && y == other.y;
	}
		#endregion
		
	public override string ToString ()
	{
		return "[" + x + "," + y + "]";	
	}


	public override int GetHashCode ()
	{
		return x * 100 + y;
	}


	public static Coords Next (Coords first, Coords next)
	{
		return new Coords ()
         {
            x = next.x - (first.x - next.x),
            y = next.y - (first.y - next.y)
         };
	}
}

/// <summary>
/// A representation of a game piece without the functions for manipulating game state.
/// </summary>
public struct GameToken
{
	public int id { get; set; }


	public Coords location { get; set; }


	public PieceType type { get; set; }


	public Player owner { get; set; }

		
	public static GameToken FromPiece (Piece piece)
	{
		return new GameToken (){
				id = piece.ID,
				location = piece.Occupies.Pos,
				type = piece.Type,
				owner = piece.Owner
			};
	}
}
}
