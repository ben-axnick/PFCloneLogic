﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace PushFightLogic
{
public static class Extension
{
	public static void Shuffle<T> (this IList<T> list)
	{
		Random rng = new Random ();
		int n = list.Count;
		while (n > 1)
		{
			n--;
			int k = rng.Next (n + 1);
			T value = list [k];
			list [k] = list [n];
			list [n] = value;
		}
	}    
	
	public static Player Other (this Player me)
	{
		return me == Player.P1 ? Player.P2 : Player.P1;
	}
}
}
