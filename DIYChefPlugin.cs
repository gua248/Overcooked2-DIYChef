using BepInEx;
using HarmonyLib;
using System.Collections;

namespace OC2DIYChef
{
    [BepInPlugin("dev.gua.overcooked.diychef", "Overcooked2 DIYChef Plugin", "1.1")]
    [BepInProcess("Overcooked2.exe")]
    public class DIYChefPlugin : BaseUnityPlugin
    {
        static DIYChefPlugin pluginInstance;
        static Harmony patcher;

        public void Awake()
        {
            pluginInstance = this;
            patcher = new Harmony("dev.gua.overcooked.diychef");
            patcher.PatchAll(typeof(Patch));
            patcher.PatchAll(typeof(Patch.FrontendChefCustomisationPatch));
            OnlinePatch.Patch(patcher);
            Patch.PatchInternal(patcher);
            foreach (var patched in patcher.GetPatchedMethods())
                Log("Patched: " + patched.FullDescription());
        }

        void Update()
        {
            DIYChefCustomisation.Update();
        }

        void OnGUI()
        {
            DIYChefCustomisation.OnGUI();
        }

        public static void Log(string msg) { pluginInstance.Logger.LogInfo(msg); }
    }
}
