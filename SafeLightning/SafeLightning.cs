using HarmonyLib;
using Microsoft.Xna.Framework;
using Shockah.CommonModCode;
using Shockah.CommonModCode.GMCM;
using Shockah.CommonModCode.IL;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using StardewValley.TerrainFeatures;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;

namespace Shockah.SafeLightning
{
	public class SafeLightning : Mod
	{
		private enum StrikeTargetType
		{
			LightningRod,
			Tile,
			FruitTree
		}

		private static SafeLightning Instance = null!;
		internal ModConfig Config { get; private set; } = null!;

		private StrikeTargetType? LastTargetType;
		private bool DidPreventLast;

		public override void Entry(IModHelper helper)
		{
			Instance = this;
			Config = helper.ReadConfig<ModConfig>();

			helper.Events.GameLoop.GameLaunched += OnGameLaunched;

			var harmony = new Harmony(ModManifest.UniqueID);
			harmony.TryPatch(
				monitor: Monitor,
				original: () => AccessTools.Method(typeof(Utility), nameof(Utility.performLightningUpdate)),
				prefix: new HarmonyMethod(typeof(SafeLightning), nameof(Utility_performLightningUpdate_Prefix)),
				transpiler: new HarmonyMethod(typeof(SafeLightning), nameof(Utility_performLightningUpdate_Transpiler))
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

			helper.AddBoolOption("config.safeTiles", () => Config.SafeTiles);
			helper.AddBoolOption("config.safeFruitTrees", () => Config.SafeFruitTrees);
			helper.AddEnumOption("config.bigLightningBehavior", () => Config.BigLightningBehavior);
		}

		private static void Utility_performLightningUpdate_Prefix()
		{
			Instance.LastTargetType = null;
			Instance.DidPreventLast = false;
		}

		private static IEnumerable<CodeInstruction> Utility_performLightningUpdate_Transpiler(IEnumerable<CodeInstruction> instructions, ILGenerator il, MethodBase original)
		{
			try
			{
				var methodLocals = original.GetMethodBody()!.LocalVariables;

				return new ILBlockMatcher(instructions)
					// find the place to jump to whenever we want to stop an actual strike from happening
					.Find(
						ILBlockMatcher.FindOccurence.Last, ILBlockMatcher.FindBounds.AllInstructions,
						ILMatches.Newobj(AccessTools.DeclaredConstructor(typeof(Farm.LightningStrikeEvent))),
						ILMatches.Stloc<Farm.LightningStrikeEvent>(methodLocals),
						ILMatches.Ldloc<Farm.LightningStrikeEvent>(methodLocals),
						ILMatches.LdcI4(1),
						ILMatches.Stfld(AccessTools.Field(typeof(Farm.LightningStrikeEvent), nameof(Farm.LightningStrikeEvent.smallFlash)))
					)
					.StartPointer
					.CreateLabel(il, out var smallLightningStrikeLabel)

					.AllInstructionsBlock

					// modify the small lightning strike (which will also be called by strikes we stopped)
					.Find(
						ILBlockMatcher.FindOccurence.Last, ILBlockMatcher.FindBounds.AllInstructions,
						ILMatches.Ldloc<Farm>(methodLocals),
						ILMatches.Ldfld("lightningStrikeEvent"),
						ILMatches.Ldloc<Farm.LightningStrikeEvent>(methodLocals),
						ILMatches.Call("Fire")
					)
					.EndPointer
					.Insert(
						new CodeInstruction(OpCodes.Dup),
						new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(SafeLightning), nameof(SafeLightning.Utility_performLightningUpdate_Transpiler_ModifyStrikeEvent)))
					)

					// stopping lightning rod strikes
					.Find(
						ILBlockMatcher.FindOccurence.First, ILBlockMatcher.FindBounds.AllInstructions,
						ILMatches.Ldloc<Farm>(methodLocals),
						ILMatches.Ldfld("objects"),
						ILMatches.Ldloc<Vector2>(methodLocals).WithAutoAnchor(out var tilePositionLocalAnchor),
						ILMatches.Call("get_Item"),
						ILMatches.Ldfld("heldObject"),
						ILMatches.Call("get_Value"),
						ILMatches.Brtrue
					)
					.Anchors[tilePositionLocalAnchor]
					.CreateLdlocInstruction(out var tilePositionLoadInstruction)
					.Anchors[ILBlockMatcher.LastFindEndPointerAnchor]
					.Advance()
					.Insert(
						tilePositionLoadInstruction,
						new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(SafeLightning), nameof(SafeLightning.Utility_performLightningUpdate_Transpiler_ShouldStrikeLightningRod))),
						new CodeInstruction(OpCodes.Brfalse, smallLightningStrikeLabel)
					)

					// stopping fruit tree strikes
					.Find(
						ILBlockMatcher.FindOccurence.First, ILBlockMatcher.FindBounds.AllInstructions,
						ILMatches.Isinst<FruitTree>(),
						ILMatches.Stloc<FruitTree>(methodLocals),
						ILMatches.Ldloc<FruitTree>(methodLocals).WithAutoAnchor(out var fruitTreeLocalAnchor),
						ILMatches.Brfalse.WithAutoAnchor(out var notFruitTreeBranchAnchor)
					)
					.Anchors[notFruitTreeBranchAnchor]
					.ExtractBranchTarget(out var notFruitTreeBranchLabel)
					.Anchors[fruitTreeLocalAnchor]
					.CreateLdlocInstruction(out var fruitTreeLoadInstruction)
					.Anchors[ILBlockMatcher.LastFindEndPointerAnchor]
					.Advance()
					.CreateLabel(il, out var fruitTreeStrikeProceedLabel)
					.Insert(
						fruitTreeLoadInstruction,
						new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(SafeLightning), nameof(SafeLightning.Utility_performLightningUpdate_Transpiler_ShouldStrikeFruitTree))),
						new CodeInstruction(OpCodes.Brfalse, fruitTreeStrikeProceedLabel),
						new CodeInstruction(OpCodes.Leave, smallLightningStrikeLabel)
					)

					// stopping tile (terrain feature) strikes
					.JumpToLabel(notFruitTreeBranchLabel)
					.CreateLdlocInstruction(out var kvpLoadInstruction)
					.CreateLabel(il, out var tileStrikeProceedLabel)
					.Insert(
						kvpLoadInstruction,
						new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(SafeLightning), nameof(SafeLightning.Utility_performLightningUpdate_Transpiler_ShouldStrikeTile))),
						new CodeInstruction(OpCodes.Brfalse, tileStrikeProceedLabel),
						new CodeInstruction(OpCodes.Leave, smallLightningStrikeLabel)
					)

					.AllInstructions;
			}
			catch (Exception ex)
			{
				Instance.Monitor.Log($"Could not patch methods - {Instance.ModManifest.Name} probably won't work.\nReason: {ex}", LogLevel.Error);
				return instructions;
			}
		}

		public static bool Utility_performLightningUpdate_Transpiler_ShouldStrikeLightningRod(Vector2 rodPosition)
		{
			Instance.LastTargetType = StrikeTargetType.LightningRod;
			Instance.DidPreventLast = false;
			return true;
		}

		public static bool Utility_performLightningUpdate_Transpiler_ShouldStrikeFruitTree(FruitTree tree)
		{
			Instance.LastTargetType = StrikeTargetType.FruitTree;
			bool shouldPrevent = Instance.Config.SafeFruitTrees;
			Instance.DidPreventLast = shouldPrevent;
			return !shouldPrevent;
		}

		public static bool Utility_performLightningUpdate_Transpiler_ShouldStrikeTile(KeyValuePair<Vector2, TerrainFeature> kvp)
		{
			Instance.LastTargetType = StrikeTargetType.Tile;
			bool shouldPrevent = Instance.Config.SafeTiles;
			Instance.DidPreventLast = shouldPrevent;
			return !shouldPrevent;
		}

		public static void Utility_performLightningUpdate_Transpiler_ModifyStrikeEvent(Farm.LightningStrikeEvent @event)
		{
			switch (Instance.Config.BigLightningBehavior)
			{
				case BigLightningBehavior.Never:
					@event.smallFlash = true;
					@event.bigFlash = false;
					break;
				case BigLightningBehavior.WhenSupposedToStrike:
					break;
				case BigLightningBehavior.WhenActuallyStrikes:
					if (@event.bigFlash && (Instance.DidPreventLast || Instance.LastTargetType is null))
					{
						@event.smallFlash = true;
						@event.bigFlash = false;
					}
					break;
				case BigLightningBehavior.Always:
					@event.smallFlash = false;
					@event.bigFlash = true;
					break;
			}
		}
	}
}
