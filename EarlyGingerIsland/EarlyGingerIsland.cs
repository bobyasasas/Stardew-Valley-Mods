﻿using HarmonyLib;
using Shockah.CommonModCode;
using Shockah.CommonModCode.GMCM;
using Shockah.CommonModCode.IL;
using Shockah.CommonModCode.Stardew;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using StardewValley.BellsAndWhistles;
using StardewValley.Inventories;
using StardewValley.Locations;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;

namespace Shockah.EarlyGingerIsland
{
	public class EarlyGingerIsland : BaseMod<ModConfig>
	{
		public static EarlyGingerIsland Instance { get; private set; } = null!;
		private bool IsConfigRegistered { get; set; } = false;
		private UnlockCondition NewUnlockCondition = new();

		public override void MigrateConfig(ISemanticVersion? configVersion, ISemanticVersion modVersion)
		{
			// no migration required, for now
		}

		public override void OnEntry(IModHelper helper)
		{
			Instance = this;

			helper.Events.GameLoop.GameLaunched += OnGameLaunched;
			helper.Events.GameLoop.DayStarted += OnDayStarted;
			helper.Events.Content.AssetRequested += OnAssetRequested;

			var harmony = new Harmony(ModManifest.UniqueID);
			harmony.TryPatch(
				monitor: Monitor,
				original: () => AccessTools.Method(typeof(BoatTunnel), nameof(BoatTunnel.checkAction)),
				transpiler: new HarmonyMethod(AccessTools.Method(typeof(EarlyGingerIsland), nameof(BoatTunnel_checkAction_Transpiler)))
			);
			harmony.TryPatch(
				monitor: Monitor,
				original: () => AccessTools.Method(typeof(BoatTunnel), nameof(BoatTunnel.answerDialogue)),
				transpiler: new HarmonyMethod(AccessTools.Method(typeof(EarlyGingerIsland), nameof(BoatTunnel_answerDialogue_Transpiler)))
			);
			harmony.TryPatch(
				monitor: Monitor,
				original: () => AccessTools.PropertyGetter(typeof(BoatTunnel), nameof(BoatTunnel.TicketPrice)),
				postfix: new HarmonyMethod(AccessTools.Method(typeof(EarlyGingerIsland), nameof(BoatTunnel_TicketPrice_Postfix)))
			);
			harmony.TryPatch(
				monitor: Monitor,
				original: () => AccessTools.Method(typeof(ParrotUpgradePerch), nameof(ParrotUpgradePerch.IsAvailable)),
				postfix: new HarmonyMethod(AccessTools.Method(typeof(EarlyGingerIsland), nameof(ParrotUpgradePerch_IsAvailable_Postfix))),
				transpiler: new HarmonyMethod(AccessTools.Method(typeof(EarlyGingerIsland), nameof(ParrotUpgradePerch_IsAvailable_Transpiler)))
			);
		}

		private void OnGameLaunched(object? sender, GameLaunchedEventArgs e)
		{
			SetupConfig();
		}

		private void OnDayStarted(object? sender, DayStartedEventArgs e)
		{
			if (ShouldGingerIslandBeUnlocked())
			{
				if (!Game1.player.hasOrWillReceiveMail("willyBackRoomInvitation"))
					Game1.addMail("willyBackRoomInvitation");
				if (Config.BoatFixHardwoodRequired <= 0 && !Game1.player.hasOrWillReceiveMail("willyBoatHull"))
					Game1.addMail("willyBoatHull", noLetter: true);
				if (Config.BoatFixIridiumBarsRequired <= 0 && !Game1.player.hasOrWillReceiveMail("willyBoatAnchor"))
					Game1.addMail("willyBoatAnchor", noLetter: true);
				if (Config.BoatFixBatteryPacksRequired <= 0 && !Game1.player.hasOrWillReceiveMail("willyBoatTicketMachine"))
					Game1.addMail("willyBoatTicketMachine", noLetter: true);
				if (Config.BoatFixHardwoodRequired <= 0 && Config.BoatFixIridiumBarsRequired <= 0 && Config.BoatFixBatteryPacksRequired <= 0 && !Game1.player.hasOrWillReceiveMail("willyBoatFixed"))
					Game1.addMail("willyBoatFixed", noLetter: true);
			}
		}

		private void OnAssetRequested(object? sender, AssetRequestedEventArgs e)
		{
			if (e.Name.IsEquivalentTo("Strings/Locations"))
			{
				e.Edit(rawAsset =>
				{
					var asset = rawAsset.AsDictionary<string, string>();
					asset.Data["BoatTunnel_DonateBatteries"] = asset.Data["BoatTunnel_DonateBatteries"].Replace("5", $"{Config.BoatFixBatteryPacksRequired}");
					asset.Data["BoatTunnel_DonateHardwood"] = asset.Data["BoatTunnel_DonateHardwood"].Replace("200", $"{Config.BoatFixHardwoodRequired}");
					asset.Data["BoatTunnel_DonateIridium"] = asset.Data["BoatTunnel_DonateIridium"].Replace("5", $"{Config.BoatFixIridiumBarsRequired}");
					asset.Data["BoatTunnel_DonateBatteriesHint"] = asset.Data["BoatTunnel_DonateBatteriesHint"].Replace("5", $"{Config.BoatFixBatteryPacksRequired}");
					asset.Data["BoatTunnel_DonateHardwoodHint"] = asset.Data["BoatTunnel_DonateHardwoodHint"].Replace("200", $"{Config.BoatFixHardwoodRequired}");
					asset.Data["BoatTunnel_DonateIridiumHint"] = asset.Data["BoatTunnel_DonateIridiumHint"].Replace("5", $"{Config.BoatFixIridiumBarsRequired}");
				});
			}
		}

		private void SetupConfig()
		{
			var api = Helper.ModRegistry.GetApi<IGenericModConfigMenuApi>("spacechase0.GenericModConfigMenu");
			if (api is null)
				return;
			GMCMI18nHelper helper = new(api, ModManifest, Helper.Translation);

			if (IsConfigRegistered)
				api.Unregister(ModManifest);

			api.Register(
				ModManifest,
				reset: () => Config = new ModConfig(),
				save: () =>
				{
					while (Config.UnlockConditions.Count != 0 && Config.UnlockConditions[Config.UnlockConditions.Count - 1].Date.Year < 1)
						Config.UnlockConditions.RemoveAt(Config.UnlockConditions.Count - 1);
					if (NewUnlockCondition.Date.Year >= 1)
					{
						Config.UnlockConditions.Add(NewUnlockCondition);
						NewUnlockCondition = new(WorldDateExt.New(-1, Season.Spring, 1), 0);
					}

					Helper.WriteConfig(Config);
					Helper.GameContent.InvalidateCache("Strings/Locations");
					SetupConfig();
				}
			);

			helper.AddNumberOption(
				keyPrefix: "config.boatTicketPrice",
				property: () => Config.BoatTicketPrice
			);

			helper.AddBoolOption(
				keyPrefix: "config.allowIslandFarmBeforeCC",
				property: () => Config.AllowIslandFarmBeforeCC
			);

			helper.AddSectionTitle("config.boatFix.section");

			helper.AddNumberOption(
				keyPrefix: "config.boatFix.hardwoodRequired",
				property: () => Config.BoatFixHardwoodRequired,
				min: 0
			);

			helper.AddNumberOption(
				keyPrefix: "config.boatFix.iridiumBarsRequired",
				property: () => Config.BoatFixIridiumBarsRequired,
				min: 0
			);

			helper.AddNumberOption(
				keyPrefix: "config.boatFix.batteryPacksRequired",
				property: () => Config.BoatFixBatteryPacksRequired,
				min: 0
			);

			void RegisterUnlockConditionSection(int? index)
			{
				helper.AddSectionTitle("config.unlockConditions.section", new { Number = index is null ? Config.UnlockConditions.Count + 1 : index.Value + 1 });
				helper.AddTextOption(
					keyPrefix: "config.unlockConditions.date",
					getValue: () =>
					{
						var date = (index is null ? NewUnlockCondition : Config.UnlockConditions[index.Value]).Date;
						if (date.Year >= 1)
							return date.ToParsable();
						else
							return "";
					},
					setValue: value =>
					{
						var parsed = WorldDateExt.ParseDate(value) ?? WorldDateExt.New(-1, Season.Spring, 1);
						if (index is null)
							NewUnlockCondition = new(parsed, NewUnlockCondition.HeartsWithWilly);
						else
							Config.UnlockConditions[index.Value] = new(parsed, Config.UnlockConditions[index.Value].HeartsWithWilly);
					}
				);
				helper.AddNumberOption(
					keyPrefix: "config.unlockConditions.heartsWithWilly",
					getValue: () => (index is null ? NewUnlockCondition : Config.UnlockConditions[index.Value]).HeartsWithWilly,
					setValue: value =>
					{
						if (index is null)
							NewUnlockCondition = new(NewUnlockCondition.Date, value);
						else
							Config.UnlockConditions[index.Value] = new(Config.UnlockConditions[index.Value].Date, value);
					},
					min: 0
				);
			}

			for (int i = 0; i < Config.UnlockConditions.Count; i++)
				RegisterUnlockConditionSection(i);
			RegisterUnlockConditionSection(null);

			IsConfigRegistered = true;
		}

		private bool ShouldGingerIslandBeUnlocked()
		{
			if (!Game1.MasterPlayer.mailReceived.Contains("spring_2_1")) // Willy introduction mail
				return false;
			foreach (var condition in Config.UnlockConditions)
			{
				if (Game1.Date.TotalDays < condition.Date.TotalDays)
					continue;
				foreach (var player in Game1.getAllFarmers())
					if (player.getFriendshipHeartLevelForNPC("Willy") < condition.HeartsWithWilly)
						goto outerContinue;
				return true;
				outerContinue:;
			}
			return ShouldGingerIslandBeUnlockedInVanilla();
		}

		private static bool ShouldGingerIslandBeUnlockedInVanilla()
			=> Game1.MasterPlayer.eventsSeen.Contains("191393") || Game1.MasterPlayer.eventsSeen.Contains("502261") || Game1.MasterPlayer.hasCompletedCommunityCenter();

		private static IEnumerable<CodeInstruction> BoatTunnel_checkAction_Transpiler(IEnumerable<CodeInstruction> instructions)
		{
			try
			{
				return new ILBlockMatcher(instructions)
					.Do(matcher =>
					{
						return matcher
							.Find(
								"(O)787",
								ILMatches.LdcI4(5).WithAutoAnchor(out var countAnchor),
								AccessTools.Method(typeof(Inventory), nameof(Inventory.ContainsId), new Type[] { typeof(string), typeof(int) })
							)
							.Anchors[countAnchor]
							.Replace(new CodeInstruction(OpCodes.Ldc_I4, Instance.Config.BoatFixBatteryPacksRequired));
					})
					.Do(matcher =>
					{
						return matcher
							.Find(
								"(O)709",
								ILMatches.LdcI4(200).WithAutoAnchor(out var countAnchor),
								AccessTools.Method(typeof(Inventory), nameof(Inventory.ContainsId), new Type[] { typeof(string), typeof(int) })
							)
							.Anchors[countAnchor]
							.Replace(new CodeInstruction(OpCodes.Ldc_I4, Instance.Config.BoatFixHardwoodRequired));
					})
					.Do(matcher =>
					{
						return matcher
							.Find(
								"(O)337",
								ILMatches.LdcI4(5).WithAutoAnchor(out var countAnchor),
								AccessTools.Method(typeof(Inventory), nameof(Inventory.ContainsId), new Type[] { typeof(string), typeof(int) })
							)
							.Anchors[countAnchor]
							.Replace(new CodeInstruction(OpCodes.Ldc_I4, Instance.Config.BoatFixIridiumBarsRequired));
					})
					.AllInstructions;
			}
			catch (Exception ex)
			{
				Instance.Monitor.Log($"Could not patch methods - {Instance.ModManifest.Name} probably won't work.\nReason: {ex}", LogLevel.Error);
				return instructions;
			}
		}

		private static IEnumerable<CodeInstruction> BoatTunnel_answerDialogue_Transpiler(IEnumerable<CodeInstruction> instructions)
		{
			try
			{
				return new ILBlockMatcher(instructions)
					.Do(matcher =>
					{
						return matcher
							.Find(
								AccessTools.PropertyGetter(typeof(Game1), nameof(Game1.player)),
								ILMatches.Ldfld(AccessTools.Field(typeof(Farmer), nameof(Farmer.items))),
								"(O)787",
								ILMatches.LdcI4(5).WithAutoAnchor(out var countAnchor),
								AccessTools.Method(typeof(Inventory), nameof(Inventory.ReduceId), new Type[] { typeof(string), typeof(int) })
							)
							.Anchors[countAnchor]
							.Replace(new CodeInstruction(OpCodes.Ldc_I4, Instance.Config.BoatFixBatteryPacksRequired));
					})
					.Do(matcher =>
					{
						return matcher
							.Find(
								AccessTools.PropertyGetter(typeof(Game1), nameof(Game1.player)),
								ILMatches.Ldfld(AccessTools.Field(typeof(Farmer), nameof(Farmer.items))),
								"(O)709",
								ILMatches.LdcI4(200).WithAutoAnchor(out var countAnchor),
								AccessTools.Method(typeof(Inventory), nameof(Inventory.ReduceId), new Type[] { typeof(string), typeof(int) })
							)
							.Anchors[countAnchor]
							.Replace(new CodeInstruction(OpCodes.Ldc_I4, Instance.Config.BoatFixHardwoodRequired));
					})
					.Do(matcher =>
					{
						return matcher
							.Find(
								AccessTools.PropertyGetter(typeof(Game1), nameof(Game1.player)),
								ILMatches.Ldfld(AccessTools.Field(typeof(Farmer), nameof(Farmer.items))),
								"(O)337",
								ILMatches.LdcI4(5).WithAutoAnchor(out var countAnchor),
								AccessTools.Method(typeof(Inventory), nameof(Inventory.ReduceId), new Type[] { typeof(string), typeof(int) })
							)
							.Anchors[countAnchor]
							.Replace(new CodeInstruction(OpCodes.Ldc_I4, Instance.Config.BoatFixIridiumBarsRequired));
					})
					.AllInstructions;
			}
			catch (Exception ex)
			{
				Instance.Monitor.Log($"Could not patch methods - Early Ginger Island probably won't work.\nReason: {ex}", LogLevel.Error);
				return instructions;
			}
		}

		private static void BoatTunnel_TicketPrice_Postfix(ref int __result)
		{
			__result = Instance.Config.BoatTicketPrice;
		}

		private static void ParrotUpgradePerch_IsAvailable_Postfix(ParrotUpgradePerch __instance, ref bool __result)
		{
			if (__instance.upgradeName.Value == "Turtle" && !Instance.Config.AllowIslandFarmBeforeCC && !ShouldGingerIslandBeUnlockedInVanilla())
				__result = false;
		}

		private static IEnumerable<CodeInstruction> ParrotUpgradePerch_IsAvailable_Transpiler(IEnumerable<CodeInstruction> instructions)
		{
			try
			{
				return new ILBlockMatcher(instructions)
					.Find(
						ILMatches.Ldarg(0),
						ILMatches.Ldfld(AccessTools.Field(typeof(ParrotUpgradePerch), nameof(ParrotUpgradePerch.requiredMail))),
						new ILMatch(i => i.opcode == OpCodes.Callvirt && ((MethodInfo)i.operand).Name == "get_Value"),
						44,
						ILMatches.AnyLdcI4,
						new ILMatch(i => i.opcode == OpCodes.Callvirt && ((MethodInfo)i.operand).Name == "Split"),
						ILMatches.AnyStloc
					)
					.EndPointer
					.CreateLdlocInstruction(out var requiredMailsLdlocInstruction)
					.CreateStlocInstruction(out var requiredMailsStlocInstruction)
					.Advance()
					.Insert(
						requiredMailsLdlocInstruction,
						new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(EarlyGingerIsland), nameof(ParrotUpgradePerch_IsAvailable_Transpiler_ModifyRequiredMails))),
						requiredMailsStlocInstruction
					)
					.AllInstructions;
			}
			catch (Exception ex)
			{
				Instance.Monitor.Log($"Could not patch methods - Early Ginger Island probably won't work.\nReason: {ex}", LogLevel.Error);
				return instructions;
			}
		}

		public static string[] ParrotUpgradePerch_IsAvailable_Transpiler_ModifyRequiredMails(string[] requiredMails)
		{
			if (!Instance.Config.AllowIslandFarmBeforeCC)
			{
				for (int i = 0; i < requiredMails.Length; i++)
					if (requiredMails[i] is "Island_Turtle" or "Island_W_Obelisk" or "Island_UpgradeHouse_Mailbox" or "Island_UpgradeHouse" or "Island_UpgradeParrotPlatform")
						requiredMails[i] = "Island_FirstParrot";
			}
			return requiredMails;
		}
	}
}