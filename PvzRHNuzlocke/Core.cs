using MelonLoader;
using UnityEngine;
using HarmonyLib;
using Il2Cpp;

[assembly: MelonInfo(typeof(PvzRHNuzlocke.Core), "Nuzlocke", "3.5", "Tproplay")]
[assembly: MelonGame("LanPiaoPiao", "PlantsVsZombiesRH")]


namespace PvzRHNuzlocke
{
    public class Core : MelonMod
    {
        public override void OnInitializeMelon()
        {
            PvZ_Fusion_Nuzlocke.NuzlockeCore.LoadAllData();
        }

    }
}
