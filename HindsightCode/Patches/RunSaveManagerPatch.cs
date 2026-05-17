using System.Text.Json;
using HarmonyLib;
using Hindsight.HindsightCode.Config;
using MegaCrit.Sts2.Core.Saves;
using MegaCrit.Sts2.Core.Saves.Managers;

namespace Hindsight.HindsightCode.Patches;

[HarmonyPatch(typeof(RunSaveManager))]
public class RunSaveManagerPatch
{
    [HarmonyPrefix]
    [HarmonyPatch(nameof(RunSaveManager.SaveRun), typeof(SerializableRun), typeof(bool))]
    static async void PrefixSaveRun(RunSaveManager __instance, SerializableRun save, bool isMultiplayer)
    {
        if (!isMultiplayer && save.PreFinishedRoom == null)
        {
            var savePath = RunSaveManager.GetRunSavePath(__instance._profileIdProvider.CurrentProfileId, $"hindsight/{save.StartTime}/floor_{save.FloorReached + 1}.save");
            MemoryStream stream = new MemoryStream();
            try
            {
                if (!__instance._forceSynchronous)
                {
                    await JsonSerializer.SerializeAsync(stream, save, JsonSerializationUtility.GetTypeInfo<SerializableRun>());
                    stream.Seek(0L, SeekOrigin.Begin);
                    await __instance._saveStore.WriteFileAsync(savePath, stream.ToArray());
                }
                else
                {
                    JsonSerializer.Serialize(stream, save, JsonSerializationUtility.GetTypeInfo<SerializableRun>());
                    stream.Seek(0L, SeekOrigin.Begin);
                    __instance._saveStore.WriteFile(savePath, stream.ToArray());
                }
                MainFile.Logger.Info($"Run resumer file saved at {savePath}");
            }
            finally
            {
                stream?.Dispose();
            }
        }
    }
    
    
    [HarmonyPrefix]
    [HarmonyPatch(nameof(RunSaveManager.DeleteCurrentRun))]
    static void PrefixDeleteCurrentRun(RunSaveManager __instance)
    {
        var parent = RunSaveManager.GetRunSavePath(__instance._profileIdProvider.CurrentProfileId, "hindsight");
        var directories = __instance._saveStore.GetDirectoriesInDirectory(parent);
        var directoriesOrdered = directories.OrderByDescending(s =>
        {
            try
            {
                return long.Parse(s);
            }
            catch (FormatException ex)
            {
                return long.MinValue;
            }
            catch (OverflowException ex)
            {
                return long.MinValue;
            }
        }).ToArray();
        MainFile.Logger.Info($"Hindsight data folders in {parent}, most recent first: {string.Join(", ", directoriesOrdered)}");
        var i = 0;
        foreach (var directory in directoriesOrdered)
        {
            if (i >= HindsightModConfig.RunsSavedToHindsight)
            {
                var toDelete = $"{parent}/{directory}";
                MainFile.Logger.Info($"Deleting old hindsight data folder {toDelete}");
                __instance._saveStore.DeleteDirectory(toDelete);
            }

            ++i;
        }
    }
}