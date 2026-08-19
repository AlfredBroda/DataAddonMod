using BepInEx;
using BepInEx.Logging;
using BepInEx.Configuration;
using HarmonyLib;
using System;
using System.Reflection;

namespace DataAddonMod;

[BepInPlugin(LCMPluginInfo.PLUGIN_GUID, LCMPluginInfo.PLUGIN_NAME, LCMPluginInfo.PLUGIN_VERSION)]
public class Plugin : BaseUnityPlugin
{
  internal static ManualLogSource Log = null!;

  internal static ConfigEntry<bool> configDebugGeneral = null!;
  internal static ConfigEntry<bool> configDebugLayers = null!;
  internal static ConfigEntry<float> configRandomDisplace = null!;

  private void Awake()
  {
    Log = Logger;

    configDebugGeneral = Config.Bind("General",
                                  "DebugMode",
                                  false,
                                  "Toggles logging more debug information to the log file.");

    // Log our awake here so we can see it in LogOutput.txt file
    Log.LogInfo($"Plugin {LCMPluginInfo.PLUGIN_NAME} version {LCMPluginInfo.PLUGIN_VERSION} is loaded!");
    try
    {
      DataAddons.Install();
      
      Log.LogInfo("Successfully installed addon injectors");
    }
    catch (Exception ex)
    {
      Log.LogError($"Error while initializing:\n{ex}\n");
    }
  }

  public static void LogDebug(string text)
  {
    if (configDebugGeneral.Value)
      Log.LogDebug(text);
  }
}
