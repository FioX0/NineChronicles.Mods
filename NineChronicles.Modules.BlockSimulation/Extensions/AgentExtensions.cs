﻿using System.Collections.Generic;
using System.Linq;
using Bencodex.Types;
using Cysharp.Threading.Tasks;
using Libplanet.Action.State;
using Libplanet.Crypto;
using Nekoyume;
using Nekoyume.Blockchain;
using Nekoyume.Model.EnumType;
using Nekoyume.Model.State;
using Nekoyume.TableData;

namespace NineChronicles.Modules.BlockSimulation.Extensions
{
    public static class AgentExtensions
    {
        public static async UniTask<IEnumerable<RuneState>> GetRuneStatesAsync(
            this IAgent agent,
            RuneListSheet runeListSheet,
            Address avatarAddress)
        {
            var runeIds = runeListSheet.OrderedList.Select(x => x.Id).ToList();
            var runeAddresses = runeIds.Select(id => RuneState.DeriveAddress(avatarAddress, id)).ToList();
            var stateBulk = await agent.GetStateBulkAsync(ReservedAddresses.LegacyAccount, runeAddresses);
            return stateBulk.Values
                .OfType<List>()
                .Select(serialized => new RuneState(serialized));
        }

        public static async UniTask<RuneSlotState> GetRuneSlotStateAsync(
            this IAgent agent,
            Address avatarAddress,
            BattleType battleType)
        {
            var runeSlotAddress = RuneSlotState.DeriveAddress(avatarAddress, battleType);
            var state = await agent.GetStateAsync(ReservedAddresses.LegacyAccount, runeSlotAddress);
            return state is List list
                ? new RuneSlotState(list)
                : new RuneSlotState(battleType);
        }

        public static async UniTask<IEnumerable<RuneState>> GetEquippedRuneStatesAsync(
            this IAgent agent,
            RuneListSheet runeListSheet,
            Address avatarAddress,
            BattleType battleType)
        {
            var runeSlotState = await agent.GetRuneSlotStateAsync(avatarAddress, battleType);
            var equippedRuneIds = runeSlotState.GetEquippedRuneSlotInfos().Select(e => e.RuneId);
            var runeStates = await agent.GetRuneStatesAsync(runeListSheet, avatarAddress);
            return runeStates.Where(e => equippedRuneIds.Contains(e.RuneId));
        }

        public static async UniTask<CollectionState> GetCollectionStateAsync(
            this IAgent agent,
            Address avatarAddress)
        {
            var state = await agent.GetStateAsync(Addresses.Collection, avatarAddress);
            return state is List list
                ? new CollectionState(list)
                : new CollectionState();
        }
    }
}