﻿using static EHR.Options;

namespace EHR.Roles.AddOns.Common
{
    internal class Antidote : IAddon
    {
        public AddonTypes Type => AddonTypes.Mixed;

        public void SetupCustomOption()
        {
            const int id = 648500;
            SetupAdtRoleOptions(id, CustomRoles.Antidote, canSetNum: true, teamSpawnOptions: true);
            AntidoteCDOpt = FloatOptionItem.Create(id + 6, "AntidoteCDOpt", new(0f, 180f, 1f), 5f, TabGroup.Addons)
                .SetParent(CustomRoleSpawnChances[CustomRoles.Antidote])
                .SetValueFormat(OptionFormat.Seconds);
            AntidoteCDReset = BooleanOptionItem.Create(id + 7, "AntidoteCDReset", true, TabGroup.Addons)
                .SetParent(CustomRoleSpawnChances[CustomRoles.Antidote]);
        }
    }
}