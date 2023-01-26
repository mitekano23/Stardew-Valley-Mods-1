﻿using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using StardewValley.Characters;
using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using System.Reflection.Emit;

namespace MPInfo {
    internal class ModEntry : Mod {
        internal static Config Config = null!;

        private int lastMaxHealth;
        private int lastHealth;

        public override void Entry(IModHelper helper) {
            PlayerInfoBox.Crown = helper.ModContent.Load<Texture2D>("Assets/Crown.png");

            helper.Events.GameLoop.GameLaunched += this.OnGameLaunched;
            Config = helper.ReadConfig<Config>();

            helper.Events.Multiplayer.PeerConnected += OnPlayerJoin;
            helper.Events.Multiplayer.PeerDisconnected += OnPlayerLeave;
            helper.Events.Multiplayer.ModMessageReceived += OnMultiplayerDataReceived;

            helper.Events.GameLoop.SaveLoaded += OnSaveLoaded;
            helper.Events.GameLoop.UpdateTicked += OnUpdateTicked;

            Patches.Apply(this.ModManifest.UniqueID);
        }

        private static void ResetDisplays(int offsetIndex = 0) {
            var displays = Game1.onScreenMenus.Where(x => x is PlayerInfoBox).OfType<PlayerInfoBox>().ToArray();
            var reportedHealthList = new List<int>(displays.Select(x => x.Who.health));
            for (int i = 0; i < displays.Length; i++)
                Game1.onScreenMenus.Remove(displays[i]);

            int playerIndex = 0;
            foreach (var player in Game1.getOnlineFarmers()) {
                int index = offsetIndex;
                PlayerInfoBox display = new(32, Game1.uiViewport.Height - 32 - 96, player, Config);
                foreach (var pib in Game1.onScreenMenus.Where(x => x is PlayerInfoBox).OfType<PlayerInfoBox>()) {
                    pib.yPositionOnScreen -= (112 * index);
                    index++;
                }
                Game1.onScreenMenus.Add(display);
                playerIndex++;

            }
        }

        private void OnGameLaunched(object? sender, GameLaunchedEventArgs e) {
            var configMenu = this.Helper.ModRegistry.GetApi<IGenericModConfigMenuApi>("spacechase0.GenericModConfigMenu");
            if (configMenu is null)
                return;

            configMenu.Register(
                mod: this.ModManifest,
                reset: () => Config = new Config(),
                save: () => this.Helper.WriteConfig(Config)
            );

            configMenu.AddBoolOption(
                mod: this.ModManifest,
                name: () => "Enabled",
                tooltip: () => "",
                getValue: () => Config.Enabled,
                setValue: value => Config.Enabled = value
            );
            configMenu.AddBoolOption(
                mod: this.ModManifest,
                name: () => "Show Self",
                tooltip: () => "",
                getValue: () => Config.ShowSelf,
                setValue: value => Config.ShowSelf = value
            );
            configMenu.AddBoolOption(
                mod: this.ModManifest,
                name: () => "Show Host Crown",
                tooltip: () => "",
                getValue: () => Config.ShowHostCrown,
                setValue: value => Config.ShowHostCrown = value
            );
            configMenu.AddBoolOption(
                mod: this.ModManifest,
                name: () => "Hide Health and Stamina Bars",
                tooltip: () => "",
                getValue: () => Config.HideHealthBars,
                setValue: value => Config.HideHealthBars = value
            );
        }

        private void OnSaveLoaded(object? sender, SaveLoadedEventArgs e) {
            lastHealth = Game1.player.health;
            lastMaxHealth = Game1.player.maxHealth;
            ResetDisplays();
        }

        private void OnUpdateTicked(object? sender, UpdateTickedEventArgs e) {
            if (!Context.IsWorldReady)
                return;

            if (Game1.player.health != lastHealth)
                Helper.Multiplayer.SendMessage(Game1.player.health, "MPInfo.Health", new[] { Helper.ModRegistry.ModID });
            if (Game1.player.maxHealth != lastMaxHealth)
                Helper.Multiplayer.SendMessage(Game1.player.maxHealth, "MPInfo.MaxHealth", new[] { Helper.ModRegistry.ModID });
        }

        private void OnPlayerJoin(object? sender, PeerConnectedEventArgs e) => ResetDisplays();

        private void OnPlayerLeave(object? sender, PeerDisconnectedEventArgs e) => ResetDisplays();

        private void OnMultiplayerDataReceived(object? sender, ModMessageReceivedEventArgs e) {
            if (e.FromModID == Helper.ModRegistry.ModID) {
                if (e.Type == "MPInfo.Health") {
                    var display = (PlayerInfoBox?)Game1.onScreenMenus.FirstOrDefault(x => x is PlayerInfoBox pib && pib.Who.UniqueMultiplayerID == e.FromPlayerID);
                    if (display is not null)
                        display.Who.health = e.ReadAs<int>();
                } else if (e.Type == "MPInfo.MaxHealth") {
                    var display = (PlayerInfoBox?)Game1.onScreenMenus.FirstOrDefault(x => x is PlayerInfoBox pib && pib.Who.UniqueMultiplayerID == e.FromPlayerID);
                    if (display is not null)
                        display.Who.maxHealth = e.ReadAs<int>();
                }
            }
        }
    }
}