using System.Diagnostics;
using HarmonyLib;
using Hindsight.HindsightCode.Config;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Multiplayer;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Multiplayer;
using MegaCrit.Sts2.Core.Multiplayer.Game.PeerInput;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.Audio;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using MegaCrit.Sts2.Core.Nodes.Multiplayer;
using MegaCrit.Sts2.Core.Nodes.Screens.RunHistoryScreen;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Saves;
using MegaCrit.Sts2.Core.Saves.Managers;

namespace Hindsight.HindsightCode.Patches;

[HarmonyPatch(typeof(NMapPointHistoryEntry))]
public class MapPointHistoryPatch
{
    [HarmonyPrefix]
    [HarmonyPatch(nameof(NMapPointHistoryEntry._Ready))]
    static void PrefixReady(NMapPointHistoryEntry __instance)
    {
        __instance.Released += async (button) =>
        {
            var savePath = RunSaveManager.GetRunSavePath(
                SaveManager.Instance._runSaveManager._profileIdProvider.CurrentProfileId,
                $"hindsight/{__instance._runHistory.StartTime}/floor_{__instance.FloorNum}.save");
            var readSaveResult = SaveManager.Instance._migrationManager.LoadSave<SerializableRun>(savePath);
            if (readSaveResult is { Success: true, SaveData: not null })
            {
                MainFile.Logger.Info("Loaded save at " + savePath);
                var modalToCreate = NGenericPopup.Create();
                if (modalToCreate == null || NModalContainer.Instance == null)
                {
                    return;
                }

                NModalContainer.Instance.Add(modalToCreate);
                var result = await modalToCreate.WaitForConfirmation(
                    new LocString("settings_ui", "HINDSIGHT-RESUME_RUNNING.body"),
                    new LocString("settings_ui", "HINDSIGHT-RESUME_RUNNING.header"),
                    new LocString("main_menu_ui", "GENERIC_POPUP.cancel"),
                    new LocString("main_menu_ui", "GENERIC_POPUP.confirm")
                );
                
                if (!result) return;
                
                MainFile.Logger.Info("Hindsighting run");
                NAudioManager.Instance?.StopMusic();
                var serializableRun = readSaveResult.SaveData;
                var runState = RunState.FromSerializable(serializableRun);
                RunManager.Instance.State = RunManager.Instance.State == null ? runState : throw new InvalidOperationException("State is already set.");
                var netService = new NetSingleplayerGameService();
                RunManager.Instance.InitializeShared(netService, new PeerInputSynchronizer(netService), HindsightModConfig.SaveHindsightedRuns,
                    serializableRun.DailyTime, serializableRun.StartTime, serializableRun.RunTime, serializableRun.WinTime, serializableRun.NumReloads);
                IEnumerable<RunLobbyPlayer> players = [new RunLobbyPlayer() {
                    id = netService.NetId,
                    isModded = netService.LocalVersion.IsModded()
                }];
                RunManager.Instance.InitializeRunLobby(netService, runState, players);
                RunManager.Instance.InitializeSavedRun(serializableRun);
                SfxCmd.Play(runState.Players[0].Character.CharacterTransitionSfx);
                Debug.Assert(NGame.Instance != null, "NGame.Instance != null");
                await NGame.Instance.Transition.FadeOut(transitionPath: runState.Players[0].Character.CharacterSelectTransitionPath);
                NGame.Instance.ReactionContainer.InitializeNetworking(new NetSingleplayerGameService());
                await NGame.Instance.LoadRun(runState, serializableRun.PreFinishedRoom);
                await NGame.Instance.Transition.FadeIn();
            }
        };
    }
}