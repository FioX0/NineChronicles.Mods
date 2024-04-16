using System;
using System.Linq;
using BepInEx.Logging;
using Cysharp.Threading.Tasks;
using Nekoyume.Game;
using Nekoyume.Model.EnumType;
using Nekoyume.Model.Item;
using Nekoyume.Model.State;
using Nekoyume.State;
using Nekoyume.TableData;
using NineChronicles.Mods.Athena.Extensions;
using NineChronicles.Mods.Athena.Managers;
using NineChronicles.Modules.BlockSimulation.ActionSimulators;
using NineChronicles.Modules.BlockSimulation.Extensions;
using UnityEngine;

namespace NineChronicles.Mods.Athena.GUIs
{
    public class AdventureGUI : IGUI
    {
        private bool _isCalculating;
        private int selectedStageId = 0;
        private int simulationStep = 0;
        private (WorldSheet WorldSheet, StageSheet StageSheet, int clearedStageId)? StateData { get; set; } = null;
        private readonly Rect _simulateLayoutRect;

        private ModInventoryManager _modInventoryManager;

        private int _wave0ClearCount = 0;
        private int _wave1ClearCount = 0;
        private int _wave2ClearCount = 0;
        private int _wave3ClearCount = 0;
        private int playCount = 100;

        public AdventureGUI(ModInventoryManager modInventoryManager)
        {
            _modInventoryManager = modInventoryManager;

            InitStateData();

            _simulateLayoutRect = new Rect(
                GUIToolbox.ScreenWidthReference - 350,
                GUIToolbox.ScreenHeightReference - 220,
                200,
                400);
        }
        public void OnGUI()
        {
            if (StateData is not { } stateData)
            {
                return;
            }

            GUI.matrix = GUIToolbox.GetGUIMatrix();

            if (selectedStageId == 0)
            {
                selectedStageId = stateData.clearedStageId + 1;
            }

            using (var areaScope = new GUILayout.AreaScope(_simulateLayoutRect))
            {
                using (var horizontalScope = new GUILayout.HorizontalScope())
                {
                    using (var verticalScope = new GUILayout.VerticalScope())
                    {
                        GUILayout.Label("Choose stage to simulate");
                        ControllablePicker(
                            stateData.StageSheet.Keys.Select(x => x.ToString()).ToArray(),
                            (_, index) => selectedStageId = index + 1,
                            selectedStageId - 1);

                        GUILayout.Label("Play Count");
                        DrawPlayCountController();

                        GUI.enabled = !_isCalculating;
                        if (GUILayout.Button("Simulate"))
                        {
                            SimulateLocal();
                            AthenaPlugin.Log($"[StageGUI] Simulate button clicked {selectedStageId})");
                        }

                        // NOTE: Use for testing with remote state.
                        //if (GUILayout.Button("Simulate(Remote)"))
                        //{
                        //    SimulateRemote();
                        //    AthenaPlugin.Log($"[StageGUI] Simulate button clicked {selectedStageId})");
                        //}

                        GUI.enabled = true;

                        DrawSimulationResultTextArea();
                    }
                }
            }
        }

        private void InitStateData()
        {
            var tableSheets = TableSheets.Instance;
            var stid = States.Instance.CurrentAvatarState.worldInformation.TryGetLastClearedStageId(out int stageId)
                ? stageId
                : 0;
            StateData = (tableSheets.WorldSheet, tableSheets.StageSheet, stid);
        }

        private GUIContent CreateSlotText(Equipment equipment)
        {
            var slotText = $"Grade {equipment.Grade}" +
                $"\n{equipment.ElementalType}" +
                $"\n{equipment.GetName()}\n" +
                $"+{equipment.level}";
            return new GUIContent(slotText);
        }

        private void DrawSimulationResultTextArea()
        {
            if (_isCalculating)
            {
                GUILayout.Label($"Simulating... {simulationStep}/{playCount}");
            }
            else
            {
                using (var horizontalScope = new GUILayout.HorizontalScope())
                {
                    GUILayout.Label($"Fail: {_wave0ClearCount}");
                    GUILayout.Label($"★: {_wave1ClearCount}");
                    GUILayout.Label($"★★: {_wave2ClearCount}");
                    GUILayout.Label($"★★★: {_wave3ClearCount}");
                }

                using (var horizontalScope = new GUILayout.HorizontalScope())
                {
                    var totalStars = _wave1ClearCount + _wave2ClearCount * 2 + _wave3ClearCount * 3;
                    GUILayout.Label($"Total Stars: {totalStars}");
                }

                using (var horizontalScope = new GUILayout.HorizontalScope())
                {
                    var winRate = (float)_wave3ClearCount / playCount;
                    GUILayout.Label($"Win Rate: {winRate:P2}");
                }
            }
        }
        private void DrawPlayCountController()
        {
            using (var verticalScope = new GUILayout.HorizontalScope())
            {
                GUI.enabled = !_isCalculating;
                if (GUILayout.Button("+", GUILayout.Width(30), GUILayout.Height(20)))
                {
                    playCount += 100;
                }

                GUI.enabled = true;
                GUILayout.Label(playCount + "", GUILayout.Width(30));

                GUI.enabled = !_isCalculating;
                if (GUILayout.Button("-", GUILayout.Width(30), GUILayout.Height(20)))
                {
                    if (playCount >= 0)
                    {
                        playCount -= 100;
                    }
                }

                GUI.enabled = true;
            }
        }

        private void ControllablePicker(string[] list, Action<string[], int> onChanged, int index = 0)
        {
            void Btn(string text, int change)
            {
                GUI.enabled = !_isCalculating;
                if (GUILayout.Button(text))
                {
                    if (index + change > 0 && index + change < list.Length)
                    {
                        onChanged(list, index + change);
                    }
                }

                GUI.enabled = true;
            }

            using (var horizontalScope = new GUILayout.HorizontalScope())
            {
                Btn("<<", -5);
                Btn("<", -1);
                GUILayout.Label(list[index]);
                Btn(">", 1);
                Btn(">>", 5);
            }
        }

        private async void SimulateLocal()
        {
            _isCalculating = true;
            _wave0ClearCount = -1;
            _wave1ClearCount = -1;
            _wave2ClearCount = -1;
            _wave3ClearCount = -1;
            simulationStep = 0;

            var states = States.Instance;
            var (_, equippedCostumes) = states.GetEquippedItems(BattleType.Adventure);
            var equippedRuneStates = states.GetEquippedRuneStates(BattleType.Adventure);
            var clearWaveInfo = await UniTask.Run(() => HackAndSlashSimulator.Simulate(
                TableSheets.Instance,
                states.CurrentAvatarState,
                equipments: _modInventoryManager.GetEquippedEquipments(),
                costumes: equippedCostumes,
                consumables: null,
                runeStates: equippedRuneStates,
                collectionState: states.CollectionState,
                gameConfigState: states.GameConfigState,
                worldId: selectedStageId / 50,
                selectedStageId,
                playCount,
                stageBuffId: null,
                onProgress: step => simulationStep = step,
                onLog: AthenaPlugin.Log));
            _wave0ClearCount = clearWaveInfo.TryGetValue(0, out var w0) ? w0 : 0;
            _wave1ClearCount = clearWaveInfo.TryGetValue(1, out var w1) ? w1 : 0;
            _wave2ClearCount = clearWaveInfo.TryGetValue(2, out var w2) ? w2 : 0;
            _wave3ClearCount = clearWaveInfo.TryGetValue(3, out var w3) ? w3 : 0;

            AthenaPlugin.Log($"[StageGUI] Simulate {playCount}: w0 ({_wave0ClearCount}) w1({_wave1ClearCount}) w2({_wave2ClearCount}) w3({_wave3ClearCount})");
            _isCalculating = false;
        }

        private async void SimulateRemote()
        {
            _isCalculating = true;
            _wave0ClearCount = -1;
            _wave1ClearCount = -1;
            _wave2ClearCount = -1;
            _wave3ClearCount = -1;
            simulationStep = 0;

            var states = States.Instance;
            var avatarAddress = states.CurrentAvatarState.address;
            var agent = Game.instance.Agent;
            var avatarDict = await agent.GetAvatarStatesAsync(new[] { avatarAddress });
            var avatarState = avatarDict[avatarAddress];
            var inventory = avatarState.inventory;
            var (equippedEquipments, equippedCostumes) = await agent.GetEquippedItemsAsync(
                avatarAddress,
                BattleType.Adventure,
                inventory: inventory);
            var equippedRuneStates = await agent.GetEquippedRuneStatesAsync(
                TableSheets.Instance.RuneListSheet,
                avatarAddress,
                BattleType.Adventure);
            var collectionState = await agent.GetCollectionStateAsync(avatarAddress);
            var clearWaveInfo = await UniTask.Run(() => HackAndSlashSimulator.Simulate(
                TableSheets.Instance,
                avatarState,
                equipments: equippedEquipments,
                costumes: equippedCostumes,
                consumables: null,
                runeStates: equippedRuneStates.ToList(),
                collectionState: collectionState,
                gameConfigState: states.GameConfigState,
                worldId: selectedStageId / 50,
                selectedStageId,
                playCount,
                stageBuffId: null,
                onProgress: step => simulationStep = step,
                onLog: AthenaPlugin.Log));
            _wave0ClearCount = clearWaveInfo.TryGetValue(0, out var w0) ? w0 : 0;
            _wave1ClearCount = clearWaveInfo.TryGetValue(1, out var w1) ? w1 : 0;
            _wave2ClearCount = clearWaveInfo.TryGetValue(2, out var w2) ? w2 : 0;
            _wave3ClearCount = clearWaveInfo.TryGetValue(3, out var w3) ? w3 : 0;

            AthenaPlugin.Log($"[StageGUI] Simulate {playCount}: w0 ({_wave0ClearCount}) w1({_wave1ClearCount}) w2({_wave2ClearCount}) w3({_wave3ClearCount})");
            _isCalculating = false;
        }
    }
}