﻿using Hazel;
using System.Collections.Generic;
using TOHE.Roles.Neutral;
using static TOHE.Translator;

namespace TOHE.Roles.Impostor
{
    public static class Consort
    {
        private static readonly int Id = 642400;
        private static List<byte> playerIdList = [];

        private static OptionItem CD;
        private static OptionItem UseLimit;

        public static void SetupCustomOption()
        {
            Options.SetupRoleOptions(Id, TabGroup.ImpostorRoles, CustomRoles.Consort);
            CD = FloatOptionItem.Create(Id + 10, "EscortCD", new(2.5f, 60f, 2.5f), 30f, TabGroup.ImpostorRoles, false)
                .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Consort])
                .SetValueFormat(OptionFormat.Seconds);
            UseLimit = IntegerOptionItem.Create(Id + 11, "AbilityUseLimit", new(1, 20, 1), 3, TabGroup.ImpostorRoles, false)
                .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Consort])
                .SetValueFormat(OptionFormat.Times);
        }

        public static void Init()
        {
            playerIdList = [];
        }
        public static void Add(byte playerId)
        {
            playerIdList.Add(playerId);
            playerId.SetAbilityUseLimit(UseLimit.GetInt());
        }
        public static bool IsEnable => playerIdList.Count > 0;
        public static bool OnCheckMurder(PlayerControl killer, PlayerControl target)
        {
            if (!IsEnable || killer == null || target == null) return false;
            if (killer.GetAbilityUseLimit() <= 0 || !killer.Is(CustomRoles.Consort)) return true;

            return killer.CheckDoubleTrigger(target, () =>
            {
                killer.RpcRemoveAbilityUse();
                Glitch.hackedIdList.TryAdd(target.PlayerId, Utils.TimeStamp);
                killer.Notify(GetString("EscortTargetHacked"));
                killer.SetKillCooldown(CD.GetFloat());
            });
        }
        public static string GetProgressText(byte id) => $"<color=#777777>-</color> <color=#ffffff>{id.GetAbilityUseLimit()}</color>";
    }
}
