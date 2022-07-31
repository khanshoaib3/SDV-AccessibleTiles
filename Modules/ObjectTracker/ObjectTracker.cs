﻿using Microsoft.Xna.Framework;
using StardewModdingAPI.Events;
using StardewValley;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Timers;

namespace AccessibleTiles.Modules.ObjectTracker {
    internal class ObjectTracker {

        private Boolean sortByProxy = true;

        private readonly ModEntry Mod;
        private readonly ModConfig ModConfig;

        private TrackedObjects TrackedObjects;

        public string SelectedCategory;
        public string SelectedObject;

        //stop player from moving too fast
        int msBetweenCheckingPathfindingController = 1000;
        Timer timer = new Timer();

        public ObjectTracker(ModEntry mod, ModConfig config) {
            this.Mod = mod;
            this.ModConfig = config;

            //set is_moving after x time to allow the next grid movement
            timer.Interval = msBetweenCheckingPathfindingController;
            timer.Elapsed += Timer_Elapsed;
        }

        private void Timer_Elapsed(object sender, ElapsedEventArgs e) {
            this.Mod.Output("Check Pathfinder.", true);
            if (Game1.player.controller != null && (Game1.activeClickableMenu == null || Game1.IsMultiplayer)) {
                if (Game1.player.controller.timerSinceLastCheckPoint > 350) {
                    Game1.player.controller.endBehaviorFunction(Game1.player, Game1.currentLocation);
                    GetLocationObjects(reset_focus: false);
                    this.Mod.Output("Pathfinding forcibly stopped. Took too long to reach checkpoint.", true);
                }
            }
        }

        public void HandleKeys(object sender, ButtonsChangedEventArgs e) {

            if (ModConfig.OTCycleUpCategory.JustPressed()) {
                CycleCategory(back: true);
            } else if (ModConfig.OTCycleDownCategory.JustPressed()) {
                CycleCategory();
            } else if (ModConfig.OTCycleUpObject.JustPressed()) {
                CycleObjects(back: true);
            } else if (ModConfig.OTCycleDownObject.JustPressed()) {
                CycleObjects();
            } else if (ModConfig.OTReadSelectedObject.JustPressed()) {
                ReadCurrentlySelectedObject();
            } else if(ModConfig.OTSwitchSortingMode.JustPressed()) {
                this.sortByProxy = !this.sortByProxy;

                this.Mod.Output("Sort By Proxy: " + (sortByProxy ? "Enabled" : "Disabled"), true);
                GetLocationObjects(reset_focus: false);

            }

            if (ModConfig.OTMoveToSelectedObject.JustPressed()) {
                MoveToCurrentlySelectedObject();
            } else if (ModConfig.OTReadSelectedObjectTileLocation.JustPressed()) {
                GetLocationObjects(reset_focus: false);
                ReadCurrentlySelectedObject(readTileOnly: true);
            }

        }

        private void MoveToCurrentlySelectedObject() {

            this.Mod.Output($"Attempt pathfinding.", true);

            SortedList<string, Dictionary<string, SpecialObject>> objects = TrackedObjects.GetObjects();

            if (objects.ContainsKey(SelectedCategory) && objects[SelectedCategory].ContainsKey(SelectedObject)) {
                ReadCurrentlySelectedObject();
            }

            Farmer player = Game1.player;
            SpecialObject sObject = GetCurrentlySelectedObject();

            Vector2 playerTile = player.getTileLocation();
            Vector2 sObjectTile = sObject.TileLocation;

            Vector2? closestTile = null;
            if (sObject.PathfindingOverride != null) {
                closestTile = Utility.GetClosestTilePath((Vector2)sObject.PathfindingOverride);
            } else {
                closestTile = Utility.GetClosestTilePath(sObjectTile);
            }

            if (closestTile != null) {

                this.Mod.Output($"Moving to {closestTile.Value.X},{closestTile.Value.Y}.", true);

                timer.Start();
                player.controller = new PathFindController(player, Game1.currentLocation, closestTile.Value.ToPoint(), -1, (Character farmer, GameLocation location) => {
                    this.StopPathfinding(Utility.GetDirection(playerTile, sObjectTile));
                });

            } else {

                this.Mod.Output("Could not find path to object.", true);

            }

        }

        private void StopPathfinding(string faceDirection) {

            Farmer player = Game1.player;

            if (faceDirection == "North") {
                player.faceDirection(0);
            }
            if (faceDirection == "East") {
                player.faceDirection(1);
            }
            if (faceDirection == "South") {
                player.faceDirection(2);
            }
            if (faceDirection == "West") {
                player.faceDirection(3);
            }

            ReadCurrentlySelectedObject();
            Utility.FixCharacterMovement();
            player.controller = null;
            timer.Stop();

        }

        private void ReadCurrentlySelectedObject(bool readTileOnly = false) {

            SortedList<string, Dictionary<string, SpecialObject>> objects = TrackedObjects.GetObjects();

            if (!(objects.ContainsKey(SelectedCategory) && objects[SelectedCategory].ContainsKey(SelectedObject))) {
                this.Mod.Output($"No Object Selected", true);
                return;
            }

            Farmer player = Game1.player;
            SpecialObject sObject = GetCurrentlySelectedObject();

            Vector2 playerTile = player.getTileLocation();
            Vector2 sObjectTile = sObject.TileLocation;

            string direction = Utility.GetDirection(playerTile, sObject.TileLocation);
            string distance = Utility.GetDistance(playerTile, sObject.TileLocation).ToString();

            this.Mod.Output(ReplacePlaceholders(readTileOnly ? this.ModConfig.OTReadSelectedObjectTileText : this.ModConfig.OTReadSelectedObjectText, playerTile, sObjectTile, direction, distance), true);

        }

        private string ReplacePlaceholders(string s, Vector2 playerTile, Vector2 sObjectTile, string direction, string distance) {
            StringBuilder sb = new StringBuilder(s);

            sb.Replace("{object}", SelectedObject);

            sb.Replace("{objectX}", $"{sObjectTile.X}");
            sb.Replace("{objectY}", $"{sObjectTile.Y}");
            sb.Replace("{playerX}", $"{playerTile.X}");
            sb.Replace("{playerY}", $"{playerTile.Y}");
            sb.Replace("{direction}", $"{direction}");
            sb.Replace("{distance}", $"{distance} tiles");

            return sb.ToString().ToLower();
        }

        private void CycleCategory(bool back = false) {

            SortedList<string, Dictionary<string, SpecialObject>> objects = TrackedObjects.GetObjects();

            string[] object_keys = objects.Keys.ToArray();

            if (!object_keys.Contains(SelectedCategory)) {
                this.Mod.Output("No Categories Found", true);
            }

            string suffix_text = Utility.DoCycle(ref SelectedCategory, object_keys, back);
            this.SetFocusedObjectToFirstInCategory();

            if (suffix_text.Length > 0) {
                suffix_text = ", " + suffix_text;
            }

            this.Mod.Output($"{SelectedCategory}, Object: {SelectedObject}" + suffix_text, true);

        }

        private void CycleObjects(bool back = false) {

            SortedList<string, Dictionary<string, SpecialObject>> objects = TrackedObjects.GetObjects();

            string[] categories = objects.Keys.ToArray();

            if (!categories.Contains(SelectedCategory)) {
                this.Mod.Output("No Categories Found", true);
            }

            string[] object_keys = objects[SelectedCategory].Keys.ToArray();

            string suffix_text = Utility.DoCycle(ref SelectedObject, object_keys, back);

            if(suffix_text.Length > 0) {
                suffix_text = ", " + suffix_text;
            }

            this.Mod.Output($"{SelectedObject}, Category: {SelectedCategory}" + suffix_text, true);

        }

        private SpecialObject? GetCurrentlySelectedObject() {
            return TrackedObjects.GetObjects()[SelectedCategory][SelectedObject];
        }
        private void SetFocusedObjectToFirstInCategory() {

            var objects = TrackedObjects.GetObjects();

            if(objects.ContainsKey(SelectedCategory)) {
                Dictionary<string, SpecialObject> cat_objects = objects[SelectedCategory];
                SelectedObject = cat_objects.Keys.ToArray()[0];
            }

        }

        private void SetDefaultCategoryAndFocusedObject() {

            var objects = TrackedObjects.GetObjects();

            if(TrackedObjects.GetObjects().Count() < 1) {
                this.Mod.Output("No objects found.");
            } else {

                SelectedCategory = objects.Keys[0];
                
                Dictionary<string, SpecialObject> cat_objects = objects[SelectedCategory];
                SelectedObject = cat_objects.Keys.ToArray()[0];

                this.Mod.Output($"Category: {SelectedCategory} | Object: {SelectedObject}");

            }
        }

        internal void GetLocationObjects(bool reset_focus = true) {
            TrackedObjects tracked_objects = new TrackedObjects(this.Mod);
            tracked_objects.FindObjectsInArea(!this.sortByProxy);
            this.TrackedObjects = tracked_objects;

            if(!reset_focus) {
                if(!tracked_objects.GetObjects().ContainsKey(SelectedCategory) || !tracked_objects.GetObjects()[SelectedCategory].ContainsKey(SelectedObject)) {
                    reset_focus = true;
                }
            }

            if(reset_focus) {
                this.SetDefaultCategoryAndFocusedObject();
            }

        }
    }
}
