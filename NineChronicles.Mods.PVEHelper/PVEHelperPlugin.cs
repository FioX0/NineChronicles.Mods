﻿using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using Nekoyume.State;
using Nekoyume.UI;
using NineChronicles.Mods.PVEHelper.GUIs;
using NineChronicles.Mods.PVEHelper.Manager;
using NineChronicles.Mods.PVEHelper.Models;
using NineChronicles.Mods.PVEHelper.Patches;
using UniRx;
using UnityEngine;
using UnityEngine.EventSystems;

namespace NineChronicles.Mods.PVEHelper
{
    [BepInPlugin(PluginGUID, PluginName, PluginVersion)]
    public class PVEHelperPlugin : BaseUnityPlugin
    {
        private const string PluginGUID = "org.ninechronicles.mods.pvehelper";
        private const string PluginName = "PVE Helper";
        private const string PluginVersion = "0.1.0";

        internal static PVEHelperPlugin Instance { get; private set; }

        private Harmony _harmony;

        private ModInventoryManager modInventoryManager = new ModInventoryManager("../../mod_inventory.csv");

        private List<IDisposable> _disposables;

        private EventSystem _eventSystem;

        private InventoryGUI _inventoryGUI;

        public static void Log(LogLevel logLevel, object data)
        {
            Instance?.Logger.Log(logLevel, data);
        }

        public static void Log(object data) => Log(LogLevel.Info, data);
        private EnhancementGUI _enhancementGUI;
        private EquipGUI _equipGUI;
        private IGUI _overlayGUI;
        private StageSimulateGUI _stageSimulateGUI;

        private void Awake()
        {
            if (Instance is not null)
            {
                throw new InvalidOperationException("PVEHelperPlugin must be only one instance.");
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);

            _harmony = new Harmony(PluginGUID);
            _harmony.PatchAll(typeof(PVEHelperPlugin));
            _harmony.PatchAll(typeof(BattlePreparationWidgetPatch));

            _eventSystem = FindObjectOfType<EventSystem>();

            _disposables = new List<IDisposable>
            {
                Widget.OnEnableStaticObservable.Subscribe(OnWidgetEnable),
                Widget.OnDisableStaticObservable.Subscribe(OnWidgetDisable)
            };

            Logger.LogInfo("Loaded");
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.Space))
            {
                _inventoryGUI = new InventoryGUI(
                    positionX: 600,
                    positionY: 100,
                    slotCountPerPage: 15,
                    slotCountPerRow: 5);
                _inventoryGUI.Clear();

                var inventory = States.Instance.CurrentAvatarState?.inventory;
                if (inventory is not null)
                {
                    foreach (var inventoryItem in inventory.Items)
                    {
                        _inventoryGUI.AddItem(inventoryItem.item, inventoryItem.count);
                    }
                }
                _enhancementGUI = new EnhancementGUI(modInventoryManager, _inventoryGUI);

                DisableEventSystem();
            }

            if (Input.GetKeyDown(KeyCode.C))
            {
                _inventoryGUI = new InventoryGUI(
                    positionX: 600,
                    positionY: 100,
                    slotCountPerPage: 15,
                    slotCountPerRow: 5);
                _inventoryGUI.Clear();

                var inventory = States.Instance.CurrentAvatarState?.inventory;
                if (inventory is not null)
                {
                    foreach (var inventoryItem in inventory.Items)
                    {
                        _inventoryGUI.AddItem(inventoryItem.item, inventoryItem.count);
                    }
                }
                _equipGUI = new EquipGUI(modInventoryManager, _inventoryGUI);
                DisableEventSystem();
            }

            if (Input.GetKeyDown(KeyCode.X))
            {
                _stageSimulateGUI = new StageSimulateGUI(1);
                _overlayGUI = new OverlayGUI(() => _stageSimulateGUI.Show());

                DisableEventSystem();
            }

            if (Input.GetKeyDown(KeyCode.Escape))
            {
                _enhancementGUI = null;
                _inventoryGUI = null;
                _overlayGUI = null;
                _stageSimulateGUI = null;
                _equipGUI = null;
                EnableEventSystem();
            }
        }

        private void DisableEventSystem()
        {
            if (_eventSystem != null)
            {
                _eventSystem.enabled = false;
            }
        }

        private void EnableEventSystem()
        {
            if (_eventSystem == null)
            {
                _eventSystem = FindObjectOfType<EventSystem>();
            }

            if (_eventSystem != null)
            {
                _eventSystem.enabled = true;
            }
        }

        private void OnGUI()
        {
            _inventoryGUI?.OnGUI();
            _enhancementGUI?.OnGUI();
            _overlayGUI?.OnGUI();
            _stageSimulateGUI?.OnGUI();
            _equipGUI?.OnGUI();
        }

        private void OnDestroy()
        {
            if (Instance != this)
            {
                return;
            }

            Instance = null;

            _harmony.UnpatchSelf();
            _harmony = null;

            foreach (var disposable in _disposables)
            {
                disposable.Dispose();
            }

            modInventoryManager.SaveItemsToCsv();

            Logger.LogInfo("Unloaded");
        }

        private void OnWidgetEnable(Widget widget)
        {
            switch (widget)
            {
                case Menu:
                    break;
                case BattlePreparation:
                    // do nothing: show BattlePreparationWidgetPatch_OnShow((int, int))
                    break;
            }
        }

        private void OnWidgetDisable(Widget widget)
        {
            switch (widget)
            {
                case BattlePreparation:
                    _enhancementGUI = null;
                    break;
            }
        }
    }
}
