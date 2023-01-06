﻿using HarmonyLib;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Shockah.CommonModCode;
using Shockah.CommonModCode.GMCM;
using Shockah.CommonModCode.SMAPI;
using Shockah.CommonModCode.Stardew;
using Shockah.CommonModCode.UI;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewModdingAPI.Utilities;
using StardewValley;
using StardewValley.Locations;
using StardewValley.Objects;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Linq.Expressions;
using SObject = StardewValley.Object;

namespace Shockah.MachineStatus
{
	public class MachineStatus : Mod
	{
		private enum MachineType
		{
			Generator,
			Processor
		}

		private static readonly ItemRenderer ItemRenderer = new();
		private static readonly Vector2 DigitSize = new(5, 7);
		private static readonly Vector2 SingleMachineSize = new(64, 64);
		private static readonly Vector2 RealSingleMachineSize = new(32, 64);

		private static readonly (string titleKey, (string machineId, string machineName, MachineType type)[] machineNames)[] KnownMachineNames = new[]
		{
			("config.machine.category.artisan", new (string machineId, string machineName, MachineType type)[]
			{
				("(BC)10", "Bee House", MachineType.Generator),
				("(BC)163", "Cask", MachineType.Processor),
				("(BC)16", "Cheese Press", MachineType.Processor),
				("(BC)12", "Keg", MachineType.Processor),
				("(BC)17", "Loom", MachineType.Processor),
				("(BC)24", "Mayonnaise Machine", MachineType.Processor),
				("(BC)19", "Oil Maker", MachineType.Processor),
				("(BC)15", "Preserves Jar", MachineType.Processor)
			}),
			("config.machine.category.refining", new (string machineId, string machineName, MachineType type)[]
			{
				("(BC)90", "Bone Mill", MachineType.Processor),
				("(BC)114", "Charcoal Kiln", MachineType.Processor),
				("(BC)21", "Crystalarium", MachineType.Processor), // it is more of a generator, but it does take an input (once)
				("(BC)13", "Furnace", MachineType.Processor),
				("(BC)182", "Geode Crusher", MachineType.Processor),
				("(BC)264", "Heavy Tapper", MachineType.Generator),
				("(BC)9", "Lightning Rod", MachineType.Processor), // does not take items as input, but it is a kind of a processor
				("(BC)20", "Recycling Machine", MachineType.Processor),
				("(BC)25", "Seed Maker", MachineType.Processor),
				("(BC)158", "Slime Egg-Press", MachineType.Processor),
				("(BC)231", "Solar Panel", MachineType.Generator),
				("(BC)105", "Tapper", MachineType.Generator),
				("(BC)211", "Wood Chipper", MachineType.Processor),
				("(BC)154", "Worm Bin", MachineType.Generator)
			}),
			("config.machine.category.misc", new (string machineId, string machineName, MachineType type)[]
			{
				("(BC)246", "Coffee Maker", MachineType.Generator),
				("(O)710", "Crab Pot", MachineType.Processor),
				("(BC)265", "Deconstructor", MachineType.Processor),
				("(BC)101", "Incubator", MachineType.Processor),
				("(BC)128", "Mushroom Box", MachineType.Generator),
				("(BC)254", "Ostrich Incubator", MachineType.Processor),
				("(BC)156", "Slime Incubator", MachineType.Processor),
				("(BC)117", "Soda Machine", MachineType.Generator),
				("(BC)127", "Statue of Endless Fortune", MachineType.Generator),
				("(BC)160", "Statue of Perfection", MachineType.Generator),
				("(BC)280", "Statue of True Perfection", MachineType.Generator)
			}),
		};

		internal static MachineStatus Instance { get; set; } = null!;
		internal ModConfig Config { get; private set; } = null!;
		private bool IsConfigRegistered { get; set; } = false;

		private readonly IList<WeakReference<SObject>> TrackedMachines = new List<WeakReference<SObject>>();
		private readonly IList<SObject> IgnoredMachinesForUpdates = new List<SObject>();
		private readonly ISet<(GameLocation location, SObject machine)> QueuedMachineUpdates = new HashSet<(GameLocation location, SObject machine)>();

		private readonly PerScreen<IList<(LocationDescriptor location, SObject machine, MachineState state)>> PerScreenHostMachines = new(() => new List<(LocationDescriptor location, SObject machine, MachineState state)>());
		private readonly PerScreen<IList<(LocationDescriptor location, SObject machine, MachineState state)>> PerScreenClientMachines = new(() => new List<(LocationDescriptor location, SObject machine, MachineState state)>());
		private readonly PerScreen<IList<(LocationDescriptor location, SObject machine, MachineState state)>> PerScreenVisibleMachines = new(() => new List<(LocationDescriptor location, SObject machine, MachineState state)>());
		private readonly PerScreen<IList<(LocationDescriptor location, SObject machine, MachineState state)>> PerScreenSortedMachines = new(() => new List<(LocationDescriptor location, SObject machine, MachineState state)>());
		private readonly PerScreen<IList<(SObject machine, IList<SObject> heldItems)>> PerScreenGroupedMachines = new(() => new List<(SObject machine, IList<SObject> heldItems)>());
		private readonly PerScreen<IList<(IntPoint position, (SObject machine, IList<SObject> heldItems) machine)>> PerScreenFlowMachines = new(() => new List<(IntPoint position, (SObject machine, IList<SObject> heldItems) machine)>());
		private readonly PerScreen<bool> PerScreenAreVisibleMachinesDirty = new(() => true);
		private readonly PerScreen<bool> PerScreenAreSortedMachinesDirty = new(() => true);
		private readonly PerScreen<bool> PerScreenAreGroupedMachinesDirty = new(() => true);
		private readonly PerScreen<bool> PerScreenAreFlowMachinesDirty = new(() => true);
		private readonly PerScreen<(GameLocation, Vector2)?> PerScreenLastPlayerTileLocation = new();
		private readonly PerScreen<MachineRenderingOptions.Visibility> PerScreenVisibility = new(() => MachineRenderingOptions.Visibility.Normal);
		private readonly PerScreen<float> PerScreenVisibilityAlpha = new(() => 1f);
		private readonly PerScreen<bool> PerScreenIsHoveredOver = new(() => false);

		private IList<(LocationDescriptor location, SObject machine, MachineState state)> HostMachines => PerScreenHostMachines.Value;
		private IList<(LocationDescriptor location, SObject machine, MachineState state)> ClientMachines => PerScreenClientMachines.Value;
		private IList<(LocationDescriptor location, SObject machine, MachineState state)> VisibleMachines => PerScreenVisibleMachines.Value;
		private IList<(LocationDescriptor location, SObject machine, MachineState state)> SortedMachines => PerScreenSortedMachines.Value;
		private IList<(SObject machine, IList<SObject> heldItems)> GroupedMachines => PerScreenGroupedMachines.Value;
		private IList<(IntPoint position, (SObject machine, IList<SObject> heldItems) machine)> FlowMachines => PerScreenFlowMachines.Value;
		private bool AreVisibleMachinesDirty { get => PerScreenAreVisibleMachinesDirty.Value; set => PerScreenAreVisibleMachinesDirty.Value = value; }
		private bool AreSortedMachinesDirty { get => PerScreenAreSortedMachinesDirty.Value; set => PerScreenAreSortedMachinesDirty.Value = value; }
		private bool AreGroupedMachinesDirty { get => PerScreenAreGroupedMachinesDirty.Value; set => PerScreenAreGroupedMachinesDirty.Value = value; }
		private bool AreFlowMachinesDirty { get => PerScreenAreFlowMachinesDirty.Value; set => PerScreenAreFlowMachinesDirty.Value = value; }
		private (GameLocation, Vector2)? LastPlayerTileLocation { get => PerScreenLastPlayerTileLocation.Value; set => PerScreenLastPlayerTileLocation.Value = value; }
		private MachineRenderingOptions.Visibility Visibility { get => PerScreenVisibility.Value; set => PerScreenVisibility.Value = value; }
		private float VisibilityAlpha { get => PerScreenVisibilityAlpha.Value; set => PerScreenVisibilityAlpha.Value = value; }
		private bool IsHoveredOver { get => PerScreenIsHoveredOver.Value; set => PerScreenIsHoveredOver.Value = value; }

		public override void Entry(IModHelper helper)
		{
			Instance = this;
			Config = helper.ReadConfig<ModConfig>();

			helper.Events.GameLoop.GameLaunched += OnGameLaunched;
			helper.Events.GameLoop.SaveLoaded += OnSaveLoaded;
			helper.Events.GameLoop.UpdateTicked += OnUpdateTicked;
			helper.Events.World.ObjectListChanged += OnObjectListChanged;
			helper.Events.Display.RenderedHud += OnRenderedHud;
			helper.Events.Multiplayer.PeerConnected += OnPeerConnected;
			helper.Events.Multiplayer.ModMessageReceived += OnModMessageReceived;
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
					var newSortingOptions = Config.Sorting.Where(o => o != MachineRenderingOptions.Sorting.None).ToList();
					Config.Sorting.Clear();
					foreach (var sortingOption in newSortingOptions)
						Config.Sorting.Add(sortingOption);
					Helper.WriteConfig(Config);
					ForceRefreshDisplayedMachines();
					SetupConfig();
				}
			);

			string BuiltInMachineSyntax(string machineName)
				=> $"*|{machineName}";

			void SetupExceptionsPage(string typeKey, MachineState state, IList<string> exceptions)
			{
				helper.AddPage(typeKey, typeKey);

				helper.AddTextOption(
					keyPrefix: "config.exceptions.manual",
					getValue: () => string.Join(", ", exceptions.Where(ex => !KnownMachineNames.Any(section => section.machineNames.Any(machine => BuiltInMachineSyntax(machine.machineName) == ex)))),
					setValue: value =>
					{
						var existingVanillaValues = exceptions
							.Where(ex => KnownMachineNames.Any(section => section.machineNames.Any(machine => BuiltInMachineSyntax(machine.machineName) == ex)))
							.ToList();
						var customInputValues = value
							.Split(',')
							.Select(s => s.Trim())
							.Where(s => s.Length > 0)
							.Where(s => !KnownMachineNames.Any(section => section.machineNames.Any(machine => BuiltInMachineSyntax(machine.machineName) == s)))
							.ToList();

						exceptions.Clear();
						foreach (var existingVanillaValue in existingVanillaValues)
							exceptions.Add(existingVanillaValue);
						foreach (var customInputValue in customInputValues)
							exceptions.Add(customInputValue);
					}
				);

				foreach (var (titleKey, machines) in KnownMachineNames)
				{
					helper.AddMultiSelectTextOption(
						titleKey,
						getValue: v => exceptions.Contains(BuiltInMachineSyntax(v.machineName)),
						addValue: v => exceptions.Add(BuiltInMachineSyntax(v.machineName)),
						removeValue: v => exceptions.Remove(BuiltInMachineSyntax(v.machineName)),
						columns: _ => 2,
						allowedValues: machines.Where(m => !(m.type == MachineType.Generator && state == MachineState.Waiting)).ToArray(),
						formatAllowedValue: v =>
						{
							var localizedMachineName = v.machineName;
							if (v.machineId.StartsWith("(B)"))
							{
								if (Game1.bigCraftablesInformation.TryGetValue(v.machineId, out string? info))
									localizedMachineName = info.Split('/')[8];
							}
							else
							{
								if (Game1.objectInformation.TryGetValue(v.machineId, out string? info))
									localizedMachineName = info.Split('/')[4];
							}
							return localizedMachineName;
						}
					);
				}
			}

			void SetupStateConfig(string optionKey, string pageKey, Expression<Func<bool>> boolProperty)
			{
				helper.AddBoolOption(optionKey, boolProperty);
				helper.AddPageLink(pageKey, "config.exceptions");
			}

			helper.AddBoolOption("config.misc.ignoreUnknownItems", () => Config.IgnoreUnknownItems);

			helper.AddSectionTitle("config.layout.section");
			helper.AddEnumOption("config.anchor.screen", valuePrefix: "config.anchor", property: () => Config.ScreenAnchorSide);
			helper.AddEnumOption("config.anchor.panel", valuePrefix: "config.anchor", property: () => Config.PanelAnchorSide);
			helper.AddNumberOption("config.anchor.inset", () => Config.AnchorInset);
			helper.AddNumberOption("config.anchor.x", () => Config.AnchorOffsetX);
			helper.AddNumberOption("config.anchor.y", () => Config.AnchorOffsetY);
			helper.AddEnumOption("config.layout.flowDirection", valuePrefix: "config.flowDirection", property: () => Config.FlowDirection);
			helper.AddNumberOption("config.layout.scale", () => Config.Scale, min: 0f, max: 12f, interval: 0.05f);
			helper.AddNumberOption("config.layout.xSpacing", () => Config.XSpacing, min: -16f, max: 64f, interval: 0.5f);
			helper.AddNumberOption("config.layout.ySpacing", () => Config.YSpacing, min: -16f, max: 64f, interval: 0.5f);
			helper.AddNumberOption("config.layout.maxColumns", () => Config.MaxColumns, min: 0, max: 20);

			helper.AddSectionTitle("config.bubble.section");
			helper.AddBoolOption("config.bubble.showItem", () => Config.ShowItemBubble);
			helper.AddNumberOption("config.bubble.itemCycleTime", () => Config.BubbleItemCycleTime, min: 0.2f, max: 5f, interval: 0.1f);
			helper.AddEnumOption("config.bubble.sway", () => Config.BubbleSway);

			helper.AddSectionTitle("config.appearance.section");
			helper.AddEnumOption("config.appearance.splitScreenScreens", valuePrefix: "config.splitScreenScreens", property: () => Config.SplitScreenScreens);
			helper.AddKeybindList("config.appearance.visibilityKeybind", () => Config.VisibilityKeybind);
			helper.AddNumberOption("config.appearance.alpha.focused", () => Config.FocusedAlpha, min: 0f, max: 1f, interval: 0.05f);
			helper.AddNumberOption("config.appearance.alpha.normal", () => Config.NormalAlpha, min: 0f, max: 1f, interval: 0.05f);
			helper.AddBoolOption("config.appearance.busyDancing", () => Config.BusyDancing);

			helper.AddSectionTitle("config.groupingSorting.section");
			helper.AddEnumOption("config.groupingSorting.grouping", () => Config.Grouping);
			for (int i = 0; i < Math.Max(Config.Sorting.Count + 1, 3); i++)
			{
				int loopI = i;
				helper.AddEnumOption(
					"config.groupingSorting.sorting",
					valuePrefix: "config.sorting",
					tokens: new { Ordinal = loopI + 1 },
					getValue: () => loopI < Config.Sorting.Count ? Config.Sorting[loopI] : MachineRenderingOptions.Sorting.None,
					setValue: value =>
					{
						while (loopI >= Config.Sorting.Count)
							Config.Sorting.Add(MachineRenderingOptions.Sorting.None);
						Config.Sorting[loopI] = value;
						while (Config.Sorting.Count > 0 && Config.Sorting.Last() == MachineRenderingOptions.Sorting.None)
							Config.Sorting.RemoveAt(Config.Sorting.Count - 1);
					}
				);
			}

			helper.AddSectionTitle("config.show.section");
			SetupStateConfig("config.show.ready", "config.show.ready.exceptions", () => Config.ShowReady);
			SetupStateConfig("config.show.waiting", "config.show.waiting.exceptions", () => Config.ShowWaiting);
			SetupStateConfig("config.show.busy", "config.show.busy.exceptions", () => Config.ShowBusy);
			SetupExceptionsPage("config.show.ready.exceptions", MachineState.Ready, Config.ShowReadyExceptions);
			SetupExceptionsPage("config.show.waiting.exceptions", MachineState.Waiting, Config.ShowWaitingExceptions);
			SetupExceptionsPage("config.show.busy.exceptions", MachineState.Busy, Config.ShowBusyExceptions);

			IsConfigRegistered = true;
		}

		private void OnGameLaunched(object? sender, GameLaunchedEventArgs e)
		{
			SetupConfig();
			Visibility = MachineRenderingOptions.Visibility.Normal;
			VisibilityAlpha = Config.NormalAlpha;
			IsHoveredOver = false;

			var harmony = new Harmony(ModManifest.UniqueID);
			try
			{
				harmony.PatchVirtual(
					original: AccessTools.Method(typeof(SObject), nameof(SObject.performObjectDropInAction)),
					monitor: Monitor,
					prefix: new HarmonyMethod(typeof(MachineStatus), nameof(SObject_performObjectDropInAction_Prefix)),
					postfix: new HarmonyMethod(typeof(MachineStatus), nameof(SObject_performObjectDropInAction_Postfix))
				);
				harmony.PatchVirtual(
					original: AccessTools.Method(typeof(SObject), nameof(SObject.checkForAction)),
					monitor: Monitor,
					prefix: new HarmonyMethod(typeof(MachineStatus), nameof(SObject_checkForAction_Prefix)),
					postfix: new HarmonyMethod(typeof(MachineStatus), nameof(SObject_checkForAction_Postfix))
				);
			}
			catch (Exception ex)
			{
				Monitor.Log($"Could not patch methods - Machine Status probably won't work.\nReason: {ex}", LogLevel.Error);
			}
		}

		private void OnSaveLoaded(object? sender, SaveLoadedEventArgs e)
		{
			QueuedMachineUpdates.Clear();
			IgnoredMachinesForUpdates.Clear();
			LastPlayerTileLocation = null;

			HostMachines.Clear();
			ForceRefreshDisplayedMachines();
		}

		private void OnUpdateTicked(object? sender, UpdateTickedEventArgs e)
		{
			if (!Context.IsWorldReady)
				return;

			if (GameExt.GetMultiplayerMode() != MultiplayerMode.Client)
			{
				foreach (var (location, machine) in QueuedMachineUpdates)
					UpdateMachine(location, machine);
				QueuedMachineUpdates.Clear();
			}

			if (Context.IsPlayerFree && Config.VisibilityKeybind.JustPressed())
			{
				Visibility = Visibility switch
				{
					MachineRenderingOptions.Visibility.Hidden => Config.FocusedAlpha == Config.NormalAlpha ? MachineRenderingOptions.Visibility.Focused : MachineRenderingOptions.Visibility.Normal,
					MachineRenderingOptions.Visibility.Normal => MachineRenderingOptions.Visibility.Focused,
					MachineRenderingOptions.Visibility.Focused => MachineRenderingOptions.Visibility.Hidden,
					_ => throw new ArgumentException($"{nameof(MachineRenderingOptions.Visibility)} has an invalid value."),
				};
			}

			var targetAlpha = Visibility switch
			{
				MachineRenderingOptions.Visibility.Hidden => 0f,
				MachineRenderingOptions.Visibility.Normal => IsHoveredOver ? Config.FocusedAlpha : Config.NormalAlpha,
				MachineRenderingOptions.Visibility.Focused => Config.FocusedAlpha,
				_ => throw new ArgumentException($"{nameof(MachineRenderingOptions.Visibility)} has an invalid value."),
			};

			VisibilityAlpha += (targetAlpha - VisibilityAlpha) * 0.15f;
			if (VisibilityAlpha <= 0.01f)
				VisibilityAlpha = 0f;
			else if (VisibilityAlpha >= 0.99f)
				VisibilityAlpha = 1f;

			var player = Game1.player;
			var newPlayerLocation = (player.currentLocation, player.getTileLocation());
			if (Config.Sorting.Any(s => s is MachineRenderingOptions.Sorting.ByDistanceAscending or MachineRenderingOptions.Sorting.ByDistanceDescending))
				if (LastPlayerTileLocation is null || LastPlayerTileLocation.Value != newPlayerLocation)
					SortMachines(player);
			LastPlayerTileLocation = newPlayerLocation;
		}

		private void OnObjectListChanged(object? sender, ObjectListChangedEventArgs e)
		{
			if (GameExt.GetMultiplayerMode() == MultiplayerMode.Client)
				return;

			foreach (var @object in e.Removed)
				RemoveMachine(e.Location, @object.Value);
			foreach (var @object in e.Added)
				StartTrackingMachine(e.Location, @object.Value);
		}

		private void OnRenderedHud(object? sender, RenderedHudEventArgs e)
		{
			if (!Context.IsWorldReady || Game1.eventUp)
				return;
			if (!Config.SplitScreenScreens.MatchesCurrentScreen())
				return;
			if (HostMachines.Count == 0 && ClientMachines.Count == 0)
				return;
			if (VisibilityAlpha <= 0f)
				return;

			UpdateFlowMachinesIfNeeded(Game1.player);
			if (FlowMachines.Count == 0)
				return;
			var minX = FlowMachines.Min(e => e.position.X);
			var minY = FlowMachines.Min(e => e.position.Y);
			var maxX = FlowMachines.Max(e => e.position.X);
			var maxY = FlowMachines.Max(e => e.position.Y);
			var width = maxX - minX + 1;
			var height = maxY - minY + 1;

			var viewportBounds = Game1.graphics.GraphicsDevice.Viewport.Bounds;
			var screenSize = new Vector2(viewportBounds.Size.X, viewportBounds.Size.Y);
			var panelSize = (SingleMachineSize * new Vector2(width, height) + Config.Spacing * new Vector2(width - 1, height - 1)) * Config.Scale;
			var panelLocation = Config.Anchor.GetAnchoredPoint(Vector2.Zero, screenSize, panelSize);

			var mouseLocation = Game1.getMousePosition();
			IsHoveredOver =
				mouseLocation.X >= panelLocation.X &&
				mouseLocation.Y >= panelLocation.Y &&
				mouseLocation.X < panelLocation.X + panelSize.X &&
				mouseLocation.Y < panelLocation.Y + panelSize.Y;

			foreach (var ((x, y), (machine, heldItems)) in FlowMachines)
			{
				float GetBubbleSwayOffset()
				{
					return Config.BubbleSway switch
					{
						MachineRenderingOptions.BubbleSway.Static => 0f,
						MachineRenderingOptions.BubbleSway.Together => 2f * (float)Math.Round(Math.Sin(Game1.currentGameTime.TotalGameTime.TotalMilliseconds / 250), 2),
						MachineRenderingOptions.BubbleSway.Wave => 2f * (float)Math.Round(Math.Sin(Game1.currentGameTime.TotalGameTime.TotalMilliseconds / 250 + x + y), 2),
						_ => throw new ArgumentException($"{nameof(MachineRenderingOptions.BubbleSway)} has an invalid value."),
					};
				}

				var machineUnscaledOffset = new Vector2(x - minX, y - minY) * SingleMachineSize + new Vector2(x - minX, y - minY) * Config.Spacing;
				var machineLocation = panelLocation + machineUnscaledOffset * Config.Scale;
				var machineState = GetMachineState(machine);

				Vector2 scaleFactor = Vector2.One;
				if (Config.BusyDancing)
				{
					scaleFactor = (SingleMachineSize + machine.getScale() * new Vector2(3f, 1f)) / SingleMachineSize;
					scaleFactor = new Vector2(scaleFactor.X, 1f / scaleFactor.Y);
				}
				ItemRenderer.DrawItem(
					e.SpriteBatch, machine,
					machineLocation + new Vector2((SingleMachineSize.X - RealSingleMachineSize.X) / 2, 0) * Config.Scale, RealSingleMachineSize * Config.Scale,
					Color.White * VisibilityAlpha,
					scale: machineState == MachineState.Busy ? scaleFactor : Vector2.One
				);

				float timeVariableOffset = GetBubbleSwayOffset();

				void DrawEmote(int emoteX, int emoteY)
				{
					var xEmoteRectangle = new Rectangle(emoteX * 16, emoteY * 16, 16, 16);
					float emoteScale = 2.5f;

					e.SpriteBatch.Draw(
						Game1.emoteSpriteSheet,
						machineLocation + new Vector2(SingleMachineSize.X * 0.5f - xEmoteRectangle.Width * emoteScale * 0.5f, timeVariableOffset - xEmoteRectangle.Height * emoteScale * 0.5f) * Config.Scale,
						xEmoteRectangle,
						Color.White * 0.75f * VisibilityAlpha,
						0f, Vector2.Zero, emoteScale * Config.Scale, SpriteEffects.None, 0.91f
					);
				}

				if (machineState == MachineState.Ready)
				{
					if (Config.ShowItemBubble)
					{
						if (heldItems.Count == 0)
						{
							DrawEmote(43, 0);
						}
						else
						{
							var bubbleRectangle = new Rectangle(141, 465, 20, 24);
							float bubbleScale = 2f;

							e.SpriteBatch.Draw(
								Game1.mouseCursors,
								machineLocation + new Vector2(SingleMachineSize.X * 0.5f - bubbleRectangle.Width * bubbleScale * 0.5f, timeVariableOffset - bubbleRectangle.Height * bubbleScale * 0.5f) * Config.Scale,
								bubbleRectangle,
								Color.White * 0.75f * VisibilityAlpha,
								0f, Vector2.Zero, bubbleScale * Config.Scale, SpriteEffects.None, 0.91f
							);

							int heldItemVariableIndex = (int)(Game1.currentGameTime.TotalGameTime.TotalMilliseconds / (1000.0 * Config.BubbleItemCycleTime)) % heldItems.Count;
							ItemRenderer.DrawItem(
								e.SpriteBatch, heldItems[heldItemVariableIndex],
								machineLocation + new Vector2(SingleMachineSize.X * 0.5f, timeVariableOffset - 4) * Config.Scale,
								new Vector2(bubbleRectangle.Size.X, bubbleRectangle.Size.Y) * bubbleScale * 0.8f * Config.Scale,
								Color.White * VisibilityAlpha,
								rectAnchorSide: UIAnchorSide.Center
							);
						}
					}
					else
					{
						DrawEmote(3, 0);
					}
				}
				else if (machineState == MachineState.Waiting)
				{
					DrawEmote(0, 4);
				}

				if (machine.Stack > 1)
				{
					Utility.drawTinyDigits(
						machine.Stack,
						e.SpriteBatch,
						machineLocation + SingleMachineSize * Config.Scale - DigitSize * new Vector2(((int)Math.Log10(machine.Stack) + 1), 1.2f) * 3 * Config.Scale,
						3f * Config.Scale,
						1f,
						Color.White * VisibilityAlpha
					);
				}
			}
		}

		private void OnPeerConnected(object? sender, PeerConnectedEventArgs e)
		{
			if (GameExt.GetMultiplayerMode() != MultiplayerMode.Server)
				return;
			if (e.Peer.GetMod(ModManifest.UniqueID) is null)
				return;
			foreach (var (location, machine, state) in HostMachines)
				Helper.Multiplayer.SendMessage(
					NetMessage.MachineUpsert.Create(location, machine, state),
					new[] { ModManifest.UniqueID },
					new[] { e.Peer.PlayerID }
				);
		}

		private void OnModMessageReceived(object? sender, ModMessageReceivedEventArgs e)
		{
			if (e.FromModID != ModManifest.UniqueID)
				return;

			if (e.Type == typeof(NetMessage.MachineUpsert).FullName)
			{
				var message = e.ReadAs<NetMessage.MachineUpsert>();
				var existingEntry = ClientMachines.FirstOrNull(e => message.Location == e.location && message.MatchesMachine(e.machine));
				if (existingEntry is not null)
				{
					static bool IsHeldObjectEqual(SObject? @object, NetMessage.Entity.SObject? message)
					{
						if (@object is null && message is null)
							return true;
						if (@object is null != message is null)
							return false;
						return message!.Value.Matches(@object!);
					}

					if (existingEntry.Value.state == message.State && IsHeldObjectEqual(existingEntry.Value.machine.GetAnyHeldObject(), message.HeldObject))
						return;
					ClientMachines.Remove(existingEntry.Value);
					AreVisibleMachinesDirty = true;
				}

				var recreatedMachine = message.RetrieveMachine();
				ClientMachines.Add((message.Location, recreatedMachine, message.State));
				AreVisibleMachinesDirty = true;
			}
			else if (e.Type == typeof(NetMessage.MachineRemove).FullName)
			{
				var message = e.ReadAs<NetMessage.MachineRemove>();
				var existingEntry = ClientMachines.FirstOrNull(e => message.Location == e.location && message.MatchesMachine(e.machine));
				if (existingEntry is not null)
				{
					ClientMachines.Remove(existingEntry.Value);
					AreVisibleMachinesDirty = true;
				}
			}
			else
			{
				Monitor.Log($"Received unknown message of type {e.Type}.", LogLevel.Warn);
			}
		}

		private void ForceRefreshDisplayedMachines()
		{
			PerScreenVisibleMachines.ResetAllScreens();
			PerScreenSortedMachines.ResetAllScreens();
			PerScreenGroupedMachines.ResetAllScreens();
			PerScreenFlowMachines.ResetAllScreens();

			PerScreenAreVisibleMachinesDirty.ResetAllScreens();
			PerScreenAreSortedMachinesDirty.ResetAllScreens();
			PerScreenAreGroupedMachinesDirty.ResetAllScreens();
			PerScreenAreFlowMachinesDirty.ResetAllScreens();

			if (!Context.IsWorldReady || Game1.currentLocation is null || GameExt.GetMultiplayerMode() == MultiplayerMode.Client)
				return;
			foreach (var location in GameExt.GetAllLocations())
				foreach (var @object in location.Objects.Values)
					StartTrackingMachine(location, @object);
		}

		private void UpdateFlowMachinesIfNeeded(Farmer player)
		{
			GroupMachinesIfNeeded(player);
			if (AreFlowMachinesDirty)
				UpdateFlowMachines();
		}

		private void UpdateFlowMachines()
		{
			IList<((int column, int row) position, (SObject machine, IList<SObject> heldItems) machine)> machineCoords
				= new List<((int column, int row) position, (SObject machine, IList<SObject> heldItems) machine)>();
			int column = 0;
			int row = 0;

			foreach (var entry in GroupedMachines)
			{
				if (entry.machine.ParentSheetIndex < 0 && Config.IgnoreUnknownItems)
					continue;

				machineCoords.Add((position: (column++, row), machine: entry));
				if (column == Config.MaxColumns)
				{
					column = 0;
					row++;
				}
			}

			var machineFlowCoords = machineCoords
				.Select(e => (position: Config.FlowDirection.GetXYPositionFromZeroOrigin(e.position), machine: e.machine))
				.Select(e => (position: new IntPoint(e.position.x, e.position.y), machine: e.machine))
				.OrderBy(e => e.position.Y)
				.ThenByDescending(e => e.position.X);
			FlowMachines.Clear();
			foreach (var entry in machineFlowCoords)
				FlowMachines.Add(entry);
			AreFlowMachinesDirty = false;
		}

		private void GroupMachinesIfNeeded(Farmer player)
		{
			SortMachinesIfNeeded(player);
			if (AreGroupedMachinesDirty)
				GroupMachines();
		}

		private void GroupMachines()
		{
			void AddHeldItem(IList<SObject> heldItems, SObject newHeldItem)
			{
				if (newHeldItem.ParentSheetIndex < 0 && Config.IgnoreUnknownItems)
					return;
				foreach (var heldItem in heldItems)
				{
					if (heldItem.Name == newHeldItem.name)
						return;
				}
				heldItems.Add(newHeldItem);
			}

			IList<SObject> CopyHeldItems(SObject machine)
			{
				var list = new List<SObject>();
				if (machine.TryGetAnyHeldObject(out var heldObject))
					AddHeldItem(list, heldObject);
				return list;
			}

			IList<(SObject machine, IList<SObject> heldItems)> results = new List<(SObject machine, IList<SObject> heldItems)>();
			foreach (var (location, machine, state) in SortedMachines)
			{
				switch (Config.Grouping)
				{
					case MachineRenderingOptions.Grouping.None:
						results.Add((CopyMachine(machine), new List<SObject>()));
						break;
					case MachineRenderingOptions.Grouping.ByMachine:
						foreach (var (result, resultHeldItems) in results)
						{
							if (
								machine.Name == result.Name && machine.readyForHarvest.Value == result.readyForHarvest.Value &&
								machine.GetAnyHeldObject()?.bigCraftable.Value == resultHeldItems.FirstOrDefault()?.bigCraftable.Value
							)
							{
								result.Stack++;
								if (machine.TryGetAnyHeldObject(out var heldObject))
									AddHeldItem(resultHeldItems, GetOne(heldObject));
								goto machineLoopContinue;
							}
						}
						results.Add((CopyMachine(machine), CopyHeldItems(machine)));
						break;
					case MachineRenderingOptions.Grouping.ByMachineAndItem:
						foreach (var (result, resultHeldItems) in results)
						{
							if (
								machine.Name == result.Name && machine.readyForHarvest.Value == result.readyForHarvest.Value &&
								machine.GetAnyHeldObject()?.bigCraftable.Value == resultHeldItems.FirstOrDefault()?.bigCraftable.Value &&
								machine.GetAnyHeldObject()?.Name == resultHeldItems.FirstOrDefault()?.Name
							)
							{
								result.Stack++;
								if (machine.TryGetAnyHeldObject(out var heldObject))
									AddHeldItem(resultHeldItems, GetOne(heldObject));
								goto machineLoopContinue;
							}
						}
						results.Add((CopyMachine(machine), CopyHeldItems(machine)));
						break;
					default:
						throw new ArgumentException($"{nameof(MachineRenderingOptions.Grouping)} has an invalid value.");
				}
				machineLoopContinue:;
			}

			var final = results.ToList();
			if (!final.SequenceEqual(GroupedMachines))
			{
				GroupedMachines.Clear();
				foreach (var entry in final)
					GroupedMachines.Add(entry);
				AreFlowMachinesDirty = true;
			}
			AreGroupedMachinesDirty = false;
		}

		private void SortMachinesIfNeeded(Farmer player)
		{
			UpdateVisibleMachinesIfNeeded();
			if (AreSortedMachinesDirty)
				SortMachines(player);
		}

		private void SortMachines(Farmer player)
		{
			IEnumerable<(LocationDescriptor location, SObject machine, MachineState state)> results = VisibleMachines;

			void SortResults<T>(bool ascending, Func<(LocationDescriptor location, SObject machine, MachineState state), T> keySelector) where T : IComparable<T>
			{
				results = results is IOrderedEnumerable<(LocationDescriptor location, SObject machine, MachineState state)> ordered
					? (ascending ? ordered.ThenBy(keySelector) : ordered.ThenByDescending(keySelector))
					: (ascending ? results.OrderBy(keySelector) : results.OrderByDescending(keySelector));
			}

			foreach (var sorting in Config.Sorting)
			{
				switch (sorting)
				{
					case MachineRenderingOptions.Sorting.None:
						break;
					case MachineRenderingOptions.Sorting.ByMachineAZ:
					case MachineRenderingOptions.Sorting.ByMachineZA:
						SortResults(
							sorting == MachineRenderingOptions.Sorting.ByMachineAZ,
							e => e.machine.DisplayName
						);
						break;
					case MachineRenderingOptions.Sorting.ReadyFirst:
						SortResults(false, e => e.state == MachineState.Ready);
						break;
					case MachineRenderingOptions.Sorting.WaitingFirst:
						SortResults(false, e => e.state == MachineState.Waiting);
						break;
					case MachineRenderingOptions.Sorting.BusyFirst:
						SortResults(false, e => e.state == MachineState.Busy);
						break;
					case MachineRenderingOptions.Sorting.ByDistanceAscending:
					case MachineRenderingOptions.Sorting.ByDistanceDescending:
						SortResults(
							sorting == MachineRenderingOptions.Sorting.ByDistanceAscending,
							e => e.location == LocationDescriptor.Create(player.currentLocation) ? (player.getTileLocation() - e.machine.TileLocation).Length() : float.PositiveInfinity
						);
						break;
					case MachineRenderingOptions.Sorting.ByItemAZ:
					case MachineRenderingOptions.Sorting.ByItemZA:
						SortResults(
							sorting == MachineRenderingOptions.Sorting.ByItemAZ,
							e => e.machine.GetAnyHeldObject()?.DisplayName ?? ""
						);
						break;
					default:
						throw new ArgumentException($"{nameof(MachineRenderingOptions.Sorting)} has an invalid value.");
				}
			}

			var final = results.ToList();
			if (!final.SequenceEqual(SortedMachines))
			{
				SortedMachines.Clear();
				foreach (var entry in final)
					SortedMachines.Add(entry);
				AreGroupedMachinesDirty = true;
			}
			AreSortedMachinesDirty = false;
		}

		private void UpdateVisibleMachinesIfNeeded()
		{
			if (AreVisibleMachinesDirty)
				UpdateVisibleMachines();
		}

		private void UpdateVisibleMachines()
		{
			var final = (GameExt.GetMultiplayerMode() == MultiplayerMode.Client ? ClientMachines : HostMachines)
				.Where(e => ShouldShowMachine(e.machine, e.state))
				.ToList();
			if (!final.SequenceEqual(VisibleMachines))
			{
				VisibleMachines.Clear();
				foreach (var entry in final)
					VisibleMachines.Add(entry);
				AreSortedMachinesDirty = true;
			}
			AreVisibleMachinesDirty = false;
		}

		private static bool MachineMatches(SObject machine, IEnumerable<IWildcardPattern> patterns)
			=> patterns.Any(p => p.Matches(machine.Name) || p.Matches(machine.DisplayName));

		[return: NotNullIfNotNull("object")]
		private static SObject? GetOne(SObject? @object)
		{
			if (@object is CrabPot crabPot)
				return new CrabPot(crabPot.TileLocation);
			else
				return @object?.getOne() as SObject;
		}

		private static SObject CopyMachine(SObject machine)
		{
			var newMachine = GetOne(machine);
			newMachine.TileLocation = machine.TileLocation;
			newMachine.readyForHarvest.Value = machine.readyForHarvest.Value;
			newMachine.showNextIndex.Value = machine.showNextIndex.Value;
			newMachine.heldObject.Value = GetOne(machine.heldObject?.Value);
			if (newMachine is CrabPot newCrabPot && machine is CrabPot crabPot)
				newCrabPot.bait.Value = GetOne(crabPot.bait?.Value);
			newMachine.MinutesUntilReady = machine.MinutesUntilReady;
			return newMachine;
		}

		private static bool IsLocationAccessible(GameLocation location)
		{
			if (location is Cellar cellar)
			{
				var farmHouse = cellar.GetFarmHouse();
				if (farmHouse is null)
					return false;
				return farmHouse.owner.HouseUpgradeLevel >= 3;
			}
			else if (location is FarmCave)
			{
				return GameExt.GetAllLocations().Where(l => l is FarmCave).First() == location;
			}
			return true;
		}

		private static MachineState GetMachineState(SObject machine)
		{
			if (machine is CrabPot crabPot)
				if (crabPot.bait.Value is not null && crabPot.heldObject.Value is null)
					return MachineState.Busy;
			if (machine is WoodChipper woodChipper)
				if (woodChipper.depositedItem.Value is not null && woodChipper.heldObject.Value is null)
					return MachineState.Busy;
			return GetMachineState(machine.readyForHarvest.Value, machine.MinutesUntilReady, machine.GetAnyHeldObject());
		}

		private bool ShouldShowMachine(SObject machine, MachineState state)
			=> state switch
			{
				MachineState.Ready => Config.ShowReady != MachineMatches(machine, Config.ShowReadyExceptionPatterns),
				MachineState.Waiting => Config.ShowWaiting != MachineMatches(machine, Config.ShowWaitingExceptionPatterns),
				MachineState.Busy => Config.ShowBusy != MachineMatches(machine, Config.ShowBusyExceptionPatterns),
				_ => throw new InvalidOperationException(),
			};

		private static MachineState GetMachineState(bool readyForHarvest, int minutesUntilReady, SObject? heldObject)
		{
			if (readyForHarvest || (heldObject is not null && minutesUntilReady <= 0))
				return MachineState.Ready;
			else if (minutesUntilReady > 0)
				return MachineState.Busy;
			else
				return MachineState.Waiting;
		}

		private static bool IsMachine(SObject @object)
		{
			if (@object is CrabPot || @object is WoodChipper)
				return true;
			if (@object.IsSprinkler())
				return false;
			if (!@object.bigCraftable.Value && @object.Category != SObject.BigCraftableCategory)
				return false;
			if (@object.ParentSheetIndex <= 0)
				return false;
			if (@object.heldObject.Value is Chest || @object.heldObject.Value?.Name == "Chest")
				return false;
			return true;
		}

		private bool UpsertMachine(GameLocation location, SObject machine)
		{
			var newState = GetMachineState(machine);
			var locationDescriptor = LocationDescriptor.Create(location);
			var existingEntry = HostMachines.FirstOrNull(e => e.location == locationDescriptor && e.machine.Name == machine.Name && e.machine.TileLocation == machine.TileLocation);
			if (existingEntry is null)
			{
				Monitor.Log($"Added {newState} machine {{Name: {machine.Name}, DisplayName: {machine.DisplayName}, Type: {machine.GetType().GetBestName()}}} in location {locationDescriptor}", LogLevel.Trace);
			}
			else
			{
				if (existingEntry.Value.state == newState)
					return false;
				HostMachines.Remove(existingEntry.Value);
			}
			HostMachines.Add((locationDescriptor, machine, newState));
			AreVisibleMachinesDirty = true;
			Helper.Multiplayer.SendMessageInMultiplayer(
				() => NetMessage.MachineUpsert.Create(locationDescriptor, machine, newState),
				new[] { ModManifest.UniqueID }
			);
			return true;
		}

		private bool RemoveMachine(GameLocation location, SObject machine)
		{
			var locationDescriptor = LocationDescriptor.Create(location);
			var existingEntry = HostMachines.FirstOrNull(e => e.location == locationDescriptor && e.machine.Name == machine.Name && e.machine.TileLocation == machine.TileLocation);
			if (existingEntry is not null)
			{
				HostMachines.Remove(existingEntry.Value);
				Helper.Multiplayer.SendMessageInMultiplayer(
					() => NetMessage.MachineRemove.Create(location, machine),
					new[] { ModManifest.UniqueID }
				);
				Monitor.Log($"Removed {existingEntry.Value.state} machine {{Name: {machine.Name}, DisplayName: {machine.DisplayName}, Type: {machine.GetType().GetBestName()}}} in location {locationDescriptor}", LogLevel.Trace);
				AreVisibleMachinesDirty = true;
			}
			return existingEntry is not null;
		}

		internal bool UpdateMachine(GameLocation location, SObject machine)
		{
			if (!IsMachine(machine))
				return false;
			if (!IsLocationAccessible(location))
				return RemoveMachine(location, machine);
			return UpsertMachine(location, machine);
		}

		[SuppressMessage("SMAPI.CommonErrors", "AvoidNetField:Avoid Netcode types when possible", Justification = "Registering for events")]
		internal void StartTrackingMachine(GameLocation location, SObject machine)
		{
			if (GameExt.GetMultiplayerMode() == MultiplayerMode.Client)
				return;
			if (!IsMachine(machine))
				return;

			UpdateMachine(location, machine);
			foreach (var refToRemove in TrackedMachines.Where(r => !r.TryGetTarget(out _)).ToList())
				TrackedMachines.Remove(refToRemove);
			if (TrackedMachines.Any(r => r.TryGetTarget(out var trackedMachine) && machine == trackedMachine))
				return;

			machine.readyForHarvest.fieldChangeVisibleEvent += (_, oldValue, newValue) =>
			{
				if (IgnoredMachinesForUpdates.Contains(machine))
					return;
				var oldState = GetMachineState(oldValue, machine.MinutesUntilReady, machine.GetAnyHeldObject());
				var newState = GetMachineState(newValue, machine.MinutesUntilReady, machine.GetAnyHeldObject());
				if (newState != oldState)
					QueuedMachineUpdates.Add((location, machine));
			};
			machine.minutesUntilReady.fieldChangeVisibleEvent += (_, oldValue, newValue) =>
			{
				if (IgnoredMachinesForUpdates.Contains(machine))
					return;
				var oldState = GetMachineState(machine.readyForHarvest.Value, oldValue, machine.GetAnyHeldObject());
				var newState = GetMachineState(machine.readyForHarvest.Value, newValue, machine.GetAnyHeldObject());
				if (newState != oldState)
					QueuedMachineUpdates.Add((location, machine));
			};
			machine.heldObject.fieldChangeVisibleEvent += (_, oldValue, newValue) =>
			{
				if (IgnoredMachinesForUpdates.Contains(machine))
					return;
				var oldState = GetMachineState(machine.readyForHarvest.Value, machine.MinutesUntilReady, oldValue);
				var newState = GetMachineState(machine.readyForHarvest.Value, machine.MinutesUntilReady, newValue);
				if (newState != oldState)
					QueuedMachineUpdates.Add((location, machine));
			};
			if (machine is CrabPot crabPot)
			{
				crabPot.bait.fieldChangeVisibleEvent += (_, oldValue, newValue) =>
				{
					if (IgnoredMachinesForUpdates.Contains(machine))
						return;
					QueuedMachineUpdates.Add((location, machine));
				};
			}

			TrackedMachines.Add(new(machine));
		}

		private static void SObject_performObjectDropInAction_Prefix(SObject __instance, bool __1 /* probe */)
		{
			if (__1)
				Instance.IgnoredMachinesForUpdates.Add(__instance);
		}

		private static void SObject_performObjectDropInAction_Postfix(SObject __instance, bool __1 /* probe */)
		{
			if (__1)
				Instance.IgnoredMachinesForUpdates.Remove(__instance);
		}

		private static void SObject_checkForAction_Prefix(SObject __instance, bool __1 /* justCheckingForActivity */)
		{
			if (__1)
				Instance.IgnoredMachinesForUpdates.Add(__instance);
		}

		private static void SObject_checkForAction_Postfix(SObject __instance, bool __1 /* justCheckingForActivity */)
		{
			if (__1)
				Instance.IgnoredMachinesForUpdates.Remove(__instance);
		}
	}
}