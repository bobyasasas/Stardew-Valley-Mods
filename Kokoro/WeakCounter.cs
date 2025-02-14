﻿using System;
using System.Runtime.CompilerServices;

namespace Shockah.Kokoro;

public sealed class WeakCounter<TKey> where TKey : class
{
	private readonly ConditionalWeakTable<TKey, StructRef<uint>> Table = new();

	public uint Get(TKey key)
		=> Table.TryGetValue(key, out var counter) ? counter.Value : 0;

	public uint Push(TKey key)
	{
		if (!Table.TryGetValue(key, out var counter))
		{
			counter = new(0);
			Table.Add(key, counter);
		}
		counter.Value++;
		return counter.Value;
	}

	public uint Pop(TKey key)
	{
		if (!Table.TryGetValue(key, out var counter) || counter.Value == 0)
			throw new InvalidOperationException($"Cannot pop the counter for {key}, as its value is already 0.");
		counter.Value--;
		if (counter.Value == 0)
			Table.Remove(key);
		return counter.Value;
	}
}