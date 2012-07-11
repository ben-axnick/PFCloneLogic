using System;

namespace PushFightLogic
{
/// <summary>
/// Set of controls exposed to the client for purposes of manipulating the game state
/// </summary>
public interface GameControls
{
	/// <summary>
	/// Place the specified piece of the given type to that location.
	/// </summary>
	/// <param name='piece'>
	/// A round or square piece type
	/// </param>
	bool Place (PieceType piece, Coords location);


	/// <summary>
	/// Move the piece to the target location
	/// </summary>
	/// <param name='piece'>
	/// Board square containing the piece
	/// </param>
	bool Move (Coords piece, Coords target);


	/// <summary>
	/// Attempt to push the piece to target adjacent location
	/// </summary>
	/// <param name='piece'>
	/// Board square containing the piece
	/// </param>
	/// <param name='target'>
	/// The target must be adjacent or this will automatically fail
	/// </param>
	bool Push (Coords piece, Coords target);


	/// <summary>
	/// Attempt no action for this phase, potentially resulting in game loss if this is the push phase.
	/// </summary>
	void Skip ();
}
}