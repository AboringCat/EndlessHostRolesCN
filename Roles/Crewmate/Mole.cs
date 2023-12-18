﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static TOHE.Options;
using static TOHE.Translator;
using static TOHE.Utils;

namespace TOHE.Roles.Crewmate
{
    internal class Mole
    {
        private static int Id => 64400;
        public static void SetupCustomOption() => SetupRoleOptions(Id, TabGroup.CrewmateRoles, CustomRoles.Mole);
        public static void OnCoEnterVent(PlayerPhysics physics)
        {
            var pc = physics.myPlayer;
            if (pc == null || !pc.Is(CustomRoles.Mole)) return;
            _ = new LateTask(() =>
            {
                var vents = UnityEngine.Object.FindObjectsOfType<Vent>();
                var vent = vents[IRandom.Instance.Next(0, vents.Count)];
                physics.RpcBootFromVent(vent.Id);
            }, 0.5f, "Mole BootFromVent");
        }
    }
}
