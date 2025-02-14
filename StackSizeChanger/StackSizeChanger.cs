﻿using HarmonyLib;
using Shockah.CommonModCode.GMCM;
using Shockah.Kokoro;
using Shockah.Kokoro.GMCM;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using SObject = StardewValley.Object;

namespace Shockah.StackSizeChanger
{
	public class StackSizeChanger : Mod
	{
		private static StackSizeChanger Instance = null!;
		internal ModConfig Config { get; private set; } = null!;

		public override void Entry(IModHelper helper)
		{
			Instance = this;
			Config = helper.ReadConfig<ModConfig>();

			helper.Events.GameLoop.GameLaunched += OnGameLaunched;

			var harmony = new Harmony(ModManifest.UniqueID);
			harmony.TryPatchVirtual(
				monitor: Monitor,
				original: () => AccessTools.Method(typeof(SObject), nameof(SObject.maximumStackSize)),
				postfix: new HarmonyMethod(typeof(StackSizeChanger), nameof(SObject_maximumStackSize_Postfix))
			);
		}

		private void OnGameLaunched(object? sender, GameLaunchedEventArgs e)
		{
			SetupConfig();
		}

		private void SetupConfig()
		{
			var api = Helper.ModRegistry.GetApi<IGenericModConfigMenuApi>("spacechase0.GenericModConfigMenu");
			if (api is null)
				return;
			GMCMI18nHelper helper = new(api, ModManifest, Helper.Translation);

			api.Register(
				ModManifest,
				reset: () => Config = new(),
				save: () => Helper.WriteConfig(Config)
			);

			helper.AddNumberOption("config.size", () => Config.Size, min: 1);
		}

		private static void SObject_maximumStackSize_Postfix(ref int __result)
		{
			if (__result > 1)
				__result = Instance.Config.Size;
		}
	}
}