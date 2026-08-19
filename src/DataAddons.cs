using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Reflection;
using BepInEx.Logging;
using LitJson;
using Ostranauts.Core;

namespace DataAddonMerge
{
	//Directory Type Mapping
	public class DTM
	{
		public string Name { get; set; }
		public Type TargetType { get; set; }
		public Func<IDictionary> GetTargetDictionary { get; set; }
		public Dictionary<string, object> Entries { get; } = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);

		public DTM(string name, Type targetType, Func<IDictionary> getTargetDictionary)
		{
			Name = name;
			TargetType = targetType;
			GetTargetDictionary = getTargetDictionary;
		}
	}
	internal static class DataAddons
	{
		private static Dictionary<string, DTM> DataStructures = new(StringComparer.OrdinalIgnoreCase){
			{ "condowners", new DTM("CondOwners", typeof(JsonCondOwner), () => DataHandler.dictCOs) },
			{ "cooverlays", new DTM("COOverlays", typeof(JsonCOOverlay), () => DataHandler.dictCOOverlays) },
			{ "slot_effects", new DTM("SlotEffects", typeof(JsonSlotEffects), () => DataHandler.dictSlotEffects) },
			{ "items", new DTM("Items", typeof(Item), () => DataHandler.dictItemDefs) },
			{ "loot", new DTM("Loot", typeof(Loot), () => DataHandler.dictLoot) },
			{ "conditions", new DTM("Conditions", typeof(JsonCond), () => DataHandler.dictConds) },
			{ "cond_rules", new DTM("ConditionRules", typeof(CondRule), () => DataHandler.dictCondRules) },
			{ "cond_trigs", new DTM("ConditionTriggers", typeof(CondTrigger), () => DataHandler.dictCTs) },
			{ "interactions", new DTM("Interactions", typeof(JsonInteraction), () => DataHandler.dictInteractions) },
			{"personspecs", new DTM("PersonSpecs", typeof(JsonPersonSpec), () => DataHandler.dictPersonSpecs) },
			{"pledges", new DTM("Pledges", typeof(JsonPledge), () => DataHandler.dictPledges) },
			// { "plots", new DTM("Plots", typeof(JsonPlot), () => DataHandler.dictPlots) },
			{ "plot_manager", new DTM("PlotManager", typeof(JsonPlotManagerSettings), () => DataHandler.dictPlotManager) },
		};

		internal static void Install()
		{
			// DataHandler.LoadComplete = (Action)Delegate.Combine(DataHandler.LoadComplete, new Action(DataAddons.InjectKioskStock));
			DataHandler.LoadComplete = (Action)Delegate.Combine(DataHandler.LoadComplete, new Action(InjectAddons));
			if (DataHandler.bLoaded)
				InjectAddons();
		}

		private static void InjectAddons()
		{
			Plugin.Log.LogInfo("InjectAddons(): Injecting addon objects...");
			if (DataHandler.dictCOs == null)
			{
				Plugin.Log.LogWarning("COs dict missing after data load!");
				return;
			}

			Dictionary<string, JsonModInfo> mods = DataHandler.dictModInfos;

			foreach (KeyValuePair<string, JsonModInfo> mod in mods)
			{
				JsonModInfo modInfo = mod.Value;
				if (modInfo == null || modInfo.GetIsDisabled())
				{
					continue;
				}

				string strFolderPath = modInfo.GetDirectory();
				if (!string.IsNullOrEmpty(strFolderPath))
				{
					char sep = Path.DirectorySeparatorChar;
					string addonsPath = strFolderPath + sep + "data" + sep + "addons";
					if (Directory.Exists(addonsPath))
					{
						Plugin.Log.LogInfo($"Loading from '{addonsPath}'");
						foreach (KeyValuePair<string, DTM> dataStructure in DataStructures)
						{
							try
							{
								LoadAddonFiles(addonsPath, dataStructure.Key, dataStructure.Value);
							}
							catch (Exception ex)
							{
								string str = "Addon loading failed in '" + dataStructure.Key + "': ";
								Plugin.Log.LogError(str + ((ex != null) ? ex.ToString() : null));
							}
						}
					}
				}
			}

			foreach (KeyValuePair<string, DTM> dataStructure in DataStructures)
			{
				IDictionary targetDictionary = dataStructure.Value.GetTargetDictionary();
				foreach (KeyValuePair<string, object> entry in dataStructure.Value.Entries)
				{
					Plugin.LogDebug($"Processing {dataStructure.Value.Name} addon '{entry.Key}'...");
					if (targetDictionary.Contains(entry.Key) && targetDictionary[entry.Key] != null)
					{
						MergeArrayFields(targetDictionary[entry.Key], entry.Value, dataStructure.Value.TargetType);
					}
					else
					{
						targetDictionary[entry.Key] = entry.Value;
					}
				}

				if (dataStructure.Value.Entries.Count > 0)
				{
					Plugin.Log.LogInfo($"Injected {dataStructure.Value.Entries.Count} {dataStructure.Value.Name} addon(s).");
					dataStructure.Value.Entries.Clear();
				}
			}
			Plugin.Log.LogInfo("Addon injection complete.");
		}

		private static void LoadAddonFiles(string addonsPath, string directoryName, DTM mapping)
		{
			string directoryPath = Path.Combine(addonsPath, directoryName);
			if (!Directory.Exists(directoryPath))
			{
				return;
			}

			PropertyInfo nameProperty = mapping.TargetType.GetProperty("strName");
			if (nameProperty == null)
			{
				Plugin.Log.LogWarning($"Loading {mapping.Name} addon: Type '{mapping.TargetType.Name}' has no strName property; skipping '{directoryName}'.");
				return;
			}

			foreach (string file in Directory.GetFiles(directoryPath, "*.json", SearchOption.AllDirectories))
			{
				try
				{
					Plugin.Log.LogInfo($"Loading {mapping.Name} addon file '{file}'...");
					MethodInfo parseMethod = typeof(JsonMapper).GetMethods()
						.First(method => method.Name == "ToObject" && method.IsGenericMethodDefinition && method.GetParameters().Length == 1 && method.GetParameters()[0].ParameterType == typeof(string));
					Array entries = parseMethod.MakeGenericMethod(mapping.TargetType.MakeArrayType())
						.Invoke(null, new object[] { File.ReadAllText(file) }) as Array ?? Array.Empty<object>();

					foreach (object entry in entries)
					{
						if (entry == null)
						{
							continue;
						}

						object nameValue = nameProperty.GetValue(entry, null);
						string key = nameValue != null ? nameValue.ToString() : string.Empty;
						if (string.IsNullOrEmpty(key))
						{
							continue;
						}

						if (mapping.Entries.TryGetValue(key, out object existingEntry) && existingEntry != null)
						{
							MergeArrayFields(existingEntry, entry, mapping.TargetType);
						}
						else
						{
							mapping.Entries[key] = entry;
						}
					}
				}
				catch (Exception ex)
				{
					Plugin.Log.LogWarning($"Loading {mapping.Name} file '{file}': {ex.Message}");
				}
			}
		}

		private static void MergeArrayFields(object target, object addon, Type type)
		{
			BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
			PropertyInfo[] properties = type.GetProperties(flags);
			foreach (PropertyInfo property in properties)
			{
				if (!property.CanRead || !property.CanWrite || property.PropertyType != typeof(string[]))
				{
					continue;
				}

				var addonValue = property.GetValue(addon, null) as string[];
				if (addonValue == null || addonValue.Length == 0)
				{
					continue;
				}

				string[]? targetValue = property.GetValue(target, null) as string[];
				string[] mergedValue = MergeArrays(targetValue ?? Array.Empty<string>(), addonValue);
				Plugin.LogDebug($"MergeArrayFields(): Merging property '{property.Name}' with {targetValue?.Length ?? 0} existing + {addonValue.Length} addon entries.");
				property.SetValue(target, mergedValue, null);
			}
		}

		private static string[] MergeArrays(string[] array1, string[] array2)
		{
			if (array1 == null || array1.Length == 0)
			{
				return array2;
			}
			if (array2 == null || array2.Length == 0)
			{
				return array1;
			}

			string[] combined = new string[array1.Length + array2.Length];
			Array.Copy(array1, 0, combined, 0, array1.Length);
			Array.Copy(array2, 0, combined, array1.Length, array2.Length);
			return combined;
		}
	}
}
