﻿using HarmonyLib;
using Microsoft.Xna.Framework;
using Shockah.Kokoro;
using Shockah.Kokoro.Stardew;
using Shockah.Kokoro.UI;
using StardewValley;
using System;
using System.Collections.Generic;
using System.Linq;
using SObject = StardewValley.Object;

namespace Shockah.SeasonAffixes.Affixes.Negative
{
	internal sealed class HurricaneAffix : BaseSeasonAffix
	{
		private static bool IsHarmonySetup = false;
		private static readonly WeakCounter<GameLocation> DayUpdateCallCounter = new();

		private static string ShortID => "Hurricane";
		public override string UniqueID => $"{Mod.ModManifest.UniqueID}.{ShortID}";
		public override string LocalizedName => Mod.Helper.Translation.Get($"affix.negative.{ShortID}.name");
		public override string LocalizedDescription => Mod.Helper.Translation.Get($"affix.negative.{ShortID}.description");
		public override TextureRectangle Icon => new(Game1.objectSpriteSheet, new(368, 224, 16, 16));

		public override int GetPositivity(OrdinalSeason season)
			=> 0;

		public override int GetNegativity(OrdinalSeason season)
			=> 1;

		public override IReadOnlySet<string> Tags
			=> new HashSet<string> { VanillaSkill.Foraging.UniqueID };

		public override void OnRegister()
			=> Apply(Mod.Harmony);

		public override void OnActivate()
		{
			Mod.Helper.GameContent.InvalidateCache("Data\\Locations");
		}

		public override void OnDeactivate()
		{
			Mod.Helper.GameContent.InvalidateCache("Data\\Locations");
		}

		private void Apply(Harmony harmony)
		{
			if (IsHarmonySetup)
				return;
			IsHarmonySetup = true;

			harmony.TryPatchVirtual(
				monitor: Mod.Monitor,
				original: () => AccessTools.Method(typeof(GameLocation), nameof(GameLocation.DayUpdate)),
				prefix: new HarmonyMethod(AccessTools.Method(typeof(HurricaneAffix), nameof(GameLocation_DayUpdate_Prefix)), priority: Priority.First),
				finalizer: new HarmonyMethod(AccessTools.Method(typeof(HurricaneAffix), nameof(GameLocation_DayUpdate_Finalizer)), priority: Priority.Last)
			);
			harmony.TryPatch(
				monitor: Mod.Monitor,
				original: () => AccessTools.Method(typeof(GameLocation), nameof(GameLocation.dropObject), new Type[] { typeof(SObject), typeof(Vector2), typeof(xTile.Dimensions.Rectangle), typeof(bool), typeof(Farmer) }),
				prefix: new HarmonyMethod(AccessTools.Method(typeof(HurricaneAffix), nameof(GameLocation_dropObject_Prefix)))
			);

			if (Mod.Helper.ModRegistry.IsLoaded("Esca.FarmTypeManager"))
			{
				harmony.TryPatch(
					monitor: Mod.Monitor,
					original: () => AccessTools.Method(AccessTools.Inner(AccessTools.TypeByName("FarmTypeManager.ModEntry, FarmTypeManager"), "Generation"), "ForageGeneration"),
					prefix: new HarmonyMethod(AccessTools.Method(typeof(HurricaneAffix), nameof(FarmTypeManager_ModEntry_Generation_ForageGeneration_Prefix)))
				);
			}
		}

		private static void GameLocation_DayUpdate_Prefix(GameLocation __instance)
			=> DayUpdateCallCounter.Push(__instance);

		private static void GameLocation_DayUpdate_Finalizer(GameLocation __instance)
			=> DayUpdateCallCounter.Pop(__instance);

		private static bool GameLocation_dropObject_Prefix(GameLocation __instance, SObject obj, bool initialPlacement)
		{
			if (!initialPlacement)
				return true;
			if (!(obj.Category is SObject.FruitsCategory or SObject.VegetableCategory or SObject.GreensCategory or SObject.sellAtFishShopCategory or SObject.FishCategory))
				return true;
			if (DayUpdateCallCounter.Get(__instance) == 0)
				return true;
			return false;
		}

		private static bool FarmTypeManager_ModEntry_Generation_ForageGeneration_Prefix()
		{
			return !Mod.ActiveAffixes.Any(a => a is HurricaneAffix);
		}
	}
}