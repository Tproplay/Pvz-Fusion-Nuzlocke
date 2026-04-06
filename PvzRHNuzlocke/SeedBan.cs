using System;
using System.Collections.Generic;
using System.IO;
using HarmonyLib;
using Il2Cpp;
using MelonLoader;
using MelonLoader.Utils;
using Newtonsoft.Json;
using UnityEngine;

namespace PvZ_Fusion_Nuzlocke
{
    public static class NuzlockeCore
    {
        public static HashSet<int> BannedIDs = new HashSet<int>();
        public static List<int> SafeIDs = new List<int> {
            (int)PlantType.LilyPad,
            (int)PlantType.Pot
            };

        // Map of how Fusions are made. 
        // Key: Fusion ID, Value: A list of parent pairs (int array [parent1, parent2])
        public static Dictionary<int, List<int[]>> FusionRecipeBook = new Dictionary<int, List<int[]>>();

        private static string BannedPlantsPath => Path.Combine(MelonEnvironment.UserDataDirectory, "NuzlockeBannedPlants.json");
        private static string RecipePath => Path.Combine(MelonEnvironment.UserDataDirectory, "NuzlockeRecipes.json");

        #region Persistence (Save/Load)

        public static void SaveAllData()
        {
            try
            {
                File.WriteAllText(BannedPlantsPath, JsonConvert.SerializeObject(BannedIDs, Formatting.Indented));
                File.WriteAllText(RecipePath, JsonConvert.SerializeObject(FusionRecipeBook, Formatting.Indented));
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"[Nuzlocke] Failed to save data: {ex.Message}");
            }
        }

        public static void LoadAllData()
        {
            if (File.Exists(BannedPlantsPath))
                BannedIDs = JsonConvert.DeserializeObject<HashSet<int>>(File.ReadAllText(BannedPlantsPath)) ?? new HashSet<int>();

            if (File.Exists(RecipePath))
            {
                FusionRecipeBook = JsonConvert.DeserializeObject<Dictionary<int, List<int[]>>>(File.ReadAllText(RecipePath)) ?? new Dictionary<int, List<int[]>>();
            }
        }

        #endregion

        #region Banning Logic

        /// <summary>
        /// Registers a fusion recipe if it hasn't been seen before.
        /// Handles cases where one plant can be made in multiple ways.
        /// </summary>
        public static void RegisterFusion(int FusionId, int parent1, int parent2)
        {
            if (FusionId < 0 || parent1 < 0 || parent2 < 0) return;

            if (!FusionRecipeBook.ContainsKey(FusionId))
                FusionRecipeBook[FusionId] = new List<int[]>();

            // Check if this specific recipe (combination of parents) already exists
            bool recipeExists = FusionRecipeBook[FusionId].Exists(r =>
                (r[0] == parent1 && r[1] == parent2) || (r[0] == parent2 && r[1] == parent1));

            if (!recipeExists)
            {
                FusionRecipeBook[FusionId].Add(new int[] { parent1, parent2 });
                SaveAllData();
            }
        }

        /// <summary>
        /// Recursively bans a plant and all ancestors found in the FusionRecipeBook.
        /// </summary>
        public static void BanPlantID(int plantId)
        {
            if (   plantId < 0
                || plantId == (int)PlantType.Nothing
                || BannedIDs.Contains(plantId)
                || SafeIDs.Contains(plantId)
                ) return;

            BannedIDs.Add(plantId);

            // 2. Look up every known way this plant was made and ban those parents too
            if (FusionRecipeBook.TryGetValue(plantId, out List<int[]> recipes))
            {
                foreach (var parents in recipes)
                {
                    BanPlantID(parents[0]); // Recurse into Parent 1
                    BanPlantID(parents[1]); // Recurse into Parent 2
                }
            }
            WipeAllRelatedPlantsFromBoard(plantId);
        }


        private static void WipeAllRelatedPlantsFromBoard(int bannedId)
        {
            var allPlantsOnLawn = UnityEngine.Object.FindObjectsOfType<Il2Cpp.Plant>();

            foreach (var p in allPlantsOnLawn)
            {
                if (p == null || p.Pointer == IntPtr.Zero) continue;

                int currentOnBoardId = (int)p.thePlantType;

                if (currentOnBoardId == bannedId || IsPlantDerivedFrom(currentOnBoardId, bannedId))
                {
                    // Kill it without triggering the Nuzlocke 'Die' patch again
                    p.Die(Il2Cpp.Plant.DieReason.BySelf);
                }
            }
        }

        /// <summary>
        /// Checks if a Fusion ID eventually traces back to a specific ancestor ID
        /// </summary>
        public static bool IsPlantDerivedFrom(int FusionId, int ancestorId)
        {
            if (FusionId == ancestorId) return true;

            if (FusionRecipeBook.TryGetValue(FusionId, out List<int[]> recipes))
            {
                foreach (var parents in recipes)
                {
                    if (parents[0] == ancestorId || parents[1] == ancestorId ||
                        IsPlantDerivedFrom(parents[0], ancestorId) ||
                        IsPlantDerivedFrom(parents[1], ancestorId))
                    {
                        return true;
                    }
                }
            }
            return false;
        }

        #endregion
    }

    #region Harmony Patches

    [HarmonyPatch(typeof(Plant), nameof(Plant.Start))]
    public static class PlantStartPatch
    {
        public static void Prefix(Plant __instance)
        {
            if (__instance == null || __instance.Pointer == IntPtr.Zero) return;

            int FusionId = (int)__instance.thePlantType;
            int p1 = (int)__instance.firstParent;
            int p2 = (int)__instance.secondParent;

            if (p1 >= 0 && p2 >= 0)
            {
                NuzlockeCore.RegisterFusion(FusionId, p1, p2);
            }
        }
    }

    [HarmonyPatch(typeof(Plant), nameof(Plant.Die))]
    public static class PlantDeathPatch
    {
        public static void Postfix(Plant __instance, Plant.DieReason reason)
        {
            if (__instance == null || __instance.Pointer == IntPtr.Zero) return;

            // Only trigger extinction on actual gameplay losses
            if (reason == Plant.DieReason.Default ||     // Eaten/Generic death
                reason == Plant.DieReason.ByShovel ||    // Shovel Ban feature
                reason == Plant.DieReason.Crash ||       // Crushed by Gargantuar/Vehicle
                reason == Plant.DieReason.CrashInWater ||// Sunk
                reason == Plant.DieReason.BySteal)       // Stolen by Bungee
            {
                int deadId = (int)__instance.thePlantType;

                NuzlockeCore.BanPlantID(deadId);
                NuzlockeCore.SaveAllData();
            }
        }
    }
    
    [HarmonyPatch(typeof(SeedLibrary), nameof(SeedLibrary.CreateCard))]
    public static class SeedLibraryLockoutPatch
    {
        /// <summary>
        /// Prevents Banned plants from being generated in the Seed Selection menu.
        /// </summary>
        public static bool Prefix(PlantType thePlantType, ref CardUI __result)
        {
            if (NuzlockeCore.BannedIDs.Contains((int)thePlantType))
            {
                __result = null;
                return false;
            }
            return true;
        }
    }

    // 1. Block the interaction during seed selection so players can't even click on the card to select it.
    [HarmonyPatch(typeof(CardUI), nameof(CardUI.OnMouseDown))]
    public static class CardUIClickPatch
    {
        public static bool Prefix(CardUI __instance)
        {
            if (__instance == null || __instance.Pointer == System.IntPtr.Zero) return true;

            int plantId = (int)__instance.thePlantType;

            if (NuzlockeCore.BannedIDs.Contains(plantId))
            {
                return false;
            }

            return true;
        }
    }

    // 2. Adding a visual lockout to the card so players can easily see which plants are extinct when browsing the seed selection menu.
    [HarmonyPatch(typeof(CardUI), nameof(CardUI.Update))]
    public static class CardUIVisualLockout
    {
        // Define a dark grey color for extinct plants
        private static readonly Color BannedColor = new Color(0.2f, 0.2f, 0.2f, 0.7f);

        public static void Postfix(CardUI __instance)
        {
            // Safety Check
            if (__instance == null || __instance.Pointer == IntPtr.Zero) return;

            int plantId = (int)__instance.thePlantType;

            // Check if this plant is in our Nuzlocke extinction list
            if (NuzlockeCore.BannedIDs.Contains(plantId))
            {
                // 1. Force the internal game flag for disabled cards
                __instance.disabled = true;

                // 2. Desaturate the Card visuals
                var renderers = __instance.GetComponentsInChildren<SpriteRenderer>();
                foreach (var sr in renderers)
                {
                    sr.color = BannedColor;
                }

                // 3. Update the text
                if (__instance.text != null)
                {
                    __instance.text.color = Color.red;
                    __instance.text.text = "X";       
                }
            }
        }
    }

    #endregion
}
