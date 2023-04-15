﻿using Shockah.Kokoro.Stardew;
using Shockah.Kokoro.UI;
using StardewModdingAPI.Events;
using StardewValley;
using StardewValley.Objects;
using StardewValley.TerrainFeatures;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace Shockah.SeasonAffixes.Affixes.Negative
{
	internal sealed class PoorYieldsAffix : BaseSeasonAffix, ISeasonAffix
	{
		private static string ShortID => "PoorYields";
		public override string UniqueID => $"{Mod.ModManifest.UniqueID}.{ShortID}";
		public override string LocalizedName => Mod.Helper.Translation.Get($"affix.negative.{ShortID}.name");
		public override string LocalizedDescription => Mod.Helper.Translation.Get($"affix.negative.{ShortID}.description");
		public override TextureRectangle Icon => new(Game1.objectSpriteSheet, new(0, 0, 16, 16));

		[MethodImpl(MethodImplOptions.NoInlining)]
		public override int GetPositivity(OrdinalSeason season)
			=> 0;

		[MethodImpl(MethodImplOptions.NoInlining)]
		public override int GetNegativity(OrdinalSeason season)
			=> 1;

		[MethodImpl(MethodImplOptions.NoInlining)]
		public double GetProbabilityWeight(OrdinalSeason season)
			=> season.Season == Season.Winter ? 0 : 1;

		public override void OnActivate()
		{
			Mod.Helper.Events.Content.AssetRequested += OnAssetRequested;
			Mod.Helper.GameContent.InvalidateCache("Data\\Crops");
			UpdateExistingCrops();
		}

		public override void OnDeactivate()
		{
			Mod.Helper.Events.Content.AssetRequested -= OnAssetRequested;
			Mod.Helper.GameContent.InvalidateCache("Data\\Crops");
			UpdateExistingCrops();
		}

		private void OnAssetRequested(object? sender, AssetRequestedEventArgs e)
		{
			if (!e.Name.IsEquivalentTo("Data\\Crops"))
				return;
			e.Edit(asset =>
			{
				var data = asset.AsDictionary<string, string>();
				foreach (var kvp in data.Data)
				{
					string[] split = kvp.Value.Split('/');
					split[4] = "-1";
					data.Data[kvp.Key] = string.Join("/", split);
				}
			}, priority: AssetEditPriority.Late);
		}

		private static void UpdateExistingCrops()
		{
			foreach (var location in GameExt.GetAllLocations())
			{
				foreach (var terrainFeature in location.terrainFeatures.Values)
					if (terrainFeature is HoeDirt dirt)
						if (dirt.crop is not null)
							UpdateCrop(dirt.crop);
				foreach (var @object in location.Objects.Values)
					if (@object is IndoorPot pot)
						if (pot.hoeDirt.Value?.crop is not null)
							UpdateCrop(pot.hoeDirt.Value.crop);
			}
		}

		private static void UpdateCrop(Crop crop)
		{
			Dictionary<int, string> allCropData = Game1.content.Load<Dictionary<int, string>>("Data\\Crops");
			if (!allCropData.TryGetValue(crop.netSeedIndex.Value, out var cropData))
				return;
			string[] split = cropData.Split('/');
			crop.regrowAfterHarvest.Value = Convert.ToInt32(split[4]);
		}
	}
}