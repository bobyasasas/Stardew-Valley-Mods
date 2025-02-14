﻿using HarmonyLib;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewValley;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Shockah.Kokoro.Stardew;

public enum MultiplayerMode { SinglePlayer, Client, Server }

public static class GameExt
{
	private static readonly Lazy<Texture2D> LazyPixel = new(() =>
	{
		var texture = new Texture2D(Game1.graphics.GraphicsDevice, 1, 1);
		texture.SetData(new[] { Color.White });
		return texture;
	});

	private static readonly Lazy<Func<Multiplayer>> MultiplayerGetter = new(() => AccessTools.Field(typeof(Game1), "multiplayer").EmitStaticGetter<Multiplayer>());

	public static Texture2D Pixel
		=> LazyPixel.Value;

	public static Multiplayer Multiplayer
		=> MultiplayerGetter.Value();

	public static MultiplayerMode GetMultiplayerMode()
		=> (MultiplayerMode)Game1.multiplayerMode;

	public static Farmer GetHostPlayer()
		=> Game1.getAllFarmers().First(p => p.slotCanHost);

	public static IReadOnlyList<GameLocation> GetAllLocations()
	{
		List<GameLocation> locations = new();
		Utility.ForAllLocations(l =>
		{
			if (l is not null)
				locations.Add(l);
		});
		return locations;
	}
}