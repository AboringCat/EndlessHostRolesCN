using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using EHR.AddOns.GhostRoles;
using EHR.Crewmate;
using EHR.Modules;
using EHR.Neutral;
using HarmonyLib;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using TMPro;
using UnityEngine;
using static EHR.Translator;


namespace EHR
{
    [HarmonyPatch(typeof(AmongUsClient), nameof(AmongUsClient.OnGameEnd))]
    internal static class EndGamePatch
    {
        public static Dictionary<byte, string> SummaryText = [];
        public static string KillLog = string.Empty;

        public static void Postfix()
        {
            GameStates.InGame = false;

            Logger.Info("-----------Game over-----------", "Phase");

            ChatCommands.DraftResult = [];
            ChatCommands.DraftRoles = [];
            Main.SetRoles = [];
            Main.SetAddOns = [];
            SummaryText = [];
            Main.LastAddOns = [];

            Main.ChangedRole = false;

            foreach ((byte id, PlayerState state) in Main.PlayerStates)
            {
                if (Doppelganger.PlayerIdList.Count > 0 && Doppelganger.DoppelVictim.ContainsKey(id))
                {
                    PlayerControl dpc = Utils.GetPlayerById(id);

                    if (dpc != null)
                    {
                        dpc.RpcSetName(Doppelganger.DoppelVictim[id]);
                        Main.AllPlayerNames[id] = Doppelganger.DoppelVictim[id];
                    }
                }

                SummaryText[id] = Utils.SummaryTexts(id, false);
                if (state.SubRoles.Count == 0) continue;

                Main.LastAddOns[id] = $"<size=70%>{id.ColoredPlayerName()}: {state.SubRoles.Join(x => x.ToColoredString())}</size>";
            }

            if (Options.DumpLogAfterGameEnd.GetBool()) Utils.DumpLog(false);

            StringBuilder sb = new(GetString("KillLog") + ":");

            foreach ((byte key, PlayerState value) in Main.PlayerStates.OrderBy(x => x.Value.RealKiller.TimeStamp.Ticks))
            {
                DateTime date = value.RealKiller.TimeStamp;
                if (date == DateTime.MinValue) continue;

                byte killerId = value.GetRealKiller();
                bool gmIsFM = Options.CurrentGameMode is CustomGameMode.FFA or CustomGameMode.MoveAndStop;
                bool gmIsFMHH = gmIsFM || Options.CurrentGameMode is CustomGameMode.HotPotato or CustomGameMode.HideAndSeek or CustomGameMode.Speedrun or CustomGameMode.CaptureTheFlag or CustomGameMode.NaturalDisasters or CustomGameMode.RoomRush;
                sb.Append($"\n{date:T} {Main.AllPlayerNames[key]} ({(gmIsFMHH ? string.Empty : Utils.GetDisplayRoleName(key, true))}{(gmIsFM ? string.Empty : Utils.GetSubRolesText(key, summary: true))}) [{Utils.GetVitalText(key)}]");
                if (killerId != byte.MaxValue && killerId != key) sb.Append($"\n\t⇐ {Main.AllPlayerNames[killerId]} ({(gmIsFMHH ? string.Empty : Utils.GetDisplayRoleName(killerId, true))}{(gmIsFM ? string.Empty : Utils.GetSubRolesText(killerId, summary: true))})");
            }

            KillLog = sb.ToString();
            if (!KillLog.Contains('\n')) KillLog = string.Empty;

            Main.NormalOptions.KillCooldown = Options.DefaultKillCooldown;

            EndGameResult.CachedWinners = new();

            HashSet<PlayerControl> winner = Main.AllPlayerControls.Where(pc => CustomWinnerHolder.WinnerIds.Contains(pc.PlayerId)).ToHashSet();

            foreach (CustomRoles team in CustomWinnerHolder.WinnerRoles) winner.UnionWith(Main.AllPlayerControls.Where(p => p.Is(team) && !winner.Contains(p)));

            Main.WinnerNameList = [];
            Main.WinnerList = [];

            foreach (PlayerControl pc in winner)
            {
                if (CustomWinnerHolder.WinnerTeam is not CustomWinner.Draw && pc.Is(CustomRoles.GM)) continue;

                EndGameResult.CachedWinners.Add(new(pc.Data));
                Main.WinnerList.Add(pc.PlayerId);
                Main.WinnerNameList.Add(pc.GetRealName());
            }

            Arsonist.IsDoused = [];
            Revolutionist.IsDraw = [];
            Farseer.IsRevealed = [];

            Main.VisibleTasksCount = false;

            CustomNetObject.Reset();
            Main.LoversPlayers.Clear();
            Bloodmoon.OnMeetingStart();
            AFKDetector.ExemptedPlayers.Clear();

            foreach (PlayerState state in Main.PlayerStates.Values) state.Role.Init();

            if (AmongUsClient.Instance.AmHost)
            {
                Main.RealOptionsData.Restore(GameOptionsManager.Instance.CurrentGameOptions);
                GameOptionsSender.AllSenders.Clear();
                GameOptionsSender.AllSenders.Add(new NormalGameOptionsSender());

                switch (Options.CurrentGameMode)
                {
                    case CustomGameMode.MoveAndStop:
                        Main.AllPlayerControls.Do(x => MoveAndStopManager.HasPlayed.Add(x.FriendCode));
                        break;
                    case CustomGameMode.RoomRush:
                        Main.AllPlayerControls.Do(x => RoomRush.HasPlayedFriendCodes.Add(x.FriendCode));
                        break;
                }
            }
        }
    }

    [HarmonyPatch(typeof(EndGameManager), nameof(EndGameManager.SetEverythingUp))]
    internal static class SetEverythingUpPatch
    {
        public static string LastWinsText = string.Empty;
        public static string LastWinsReason = string.Empty;

        public static void Postfix(EndGameManager __instance)
        {
            //#######################################
            //      ==Victory faction display==
            //#######################################

            try
            {
                // ---------- Code from TOR (The Other Roles)! ----------
                // https://github.com/TheOtherRolesAU/TheOtherRoles/blob/main/TheOtherRoles/Patches/EndGamePatch.cs

                if (Options.CurrentGameMode is not CustomGameMode.Standard) goto End;

                int num = Mathf.CeilToInt(7.5f);

                Il2CppArrayBase<PoolablePlayer> pbs = __instance?.transform.GetComponentsInChildren<PoolablePlayer>();

                if (pbs != null)
                    foreach (PoolablePlayer pb in pbs)
                        pb.ToggleName(false);

                List<CachedPlayerData> list = EndGameResult.CachedWinners.ToArray().ToList();

                for (var i = 0; i < list.Count; i++)
                {
                    CachedPlayerData data = list[i];
                    int num2 = i % 2 == 0 ? -1 : 1;
                    int num3 = (i + 1) / 2;
                    float num4 = num3 / (float)num;
                    float num5 = Mathf.Lerp(1f, 0.75f, num4);
                    float num6 = i == 0 ? -8 : -1;
                    PoolablePlayer poolablePlayer = Object.Instantiate(__instance?.PlayerPrefab, __instance?.transform);
                    poolablePlayer.transform.localPosition = new Vector3(1f * num2 * num3 * num5, FloatRange.SpreadToEdges(-1.125f, 0f, num3, num), num6 + (num3 * 0.01f)) * 0.9f;
                    float num7 = Mathf.Lerp(1f, 0.65f, num4) * 0.9f;
                    Vector3 vector = new(num7, num7, 1f);
                    poolablePlayer.transform.localScale = vector;
                    poolablePlayer.UpdateFromPlayerOutfit(data.Outfit, PlayerMaterial.MaskType.ComplexUI, data.IsDead, true);

                    if (data.IsDead)
                    {
                        poolablePlayer.cosmetics.currentBodySprite.BodySprite.sprite = poolablePlayer.cosmetics.currentBodySprite.GhostSprite;
                        poolablePlayer.SetDeadFlipX(i % 2 == 0);
                    }
                    else
                        poolablePlayer.SetFlipX(i % 2 == 0);

                    bool lowered = i is 1 or 2 or 5 or 6 or 9 or 10 or 13 or 14;

                    poolablePlayer.cosmetics.nameText.color = Color.white;
                    poolablePlayer.cosmetics.nameText.transform.localScale = new(1f / vector.x, 1f / vector.y, 1f / vector.z);
                    poolablePlayer.cosmetics.nameText.text = data.PlayerName;

                    Vector3 defaultPos = poolablePlayer.cosmetics.nameText.transform.localPosition;

                    for (var j = 0; j < Main.WinnerList.Count; j++)
                    {
                        byte id = Main.WinnerList[j];
                        if (Main.WinnerNameList[j].RemoveHtmlTags() != data.PlayerName.RemoveHtmlTags()) continue;

                        CustomRoles role = Main.PlayerStates[id].MainRole;

                        string color = Main.RoleColors[role];
                        string rolename = Utils.GetRoleName(role);

                        poolablePlayer.cosmetics.nameText.text += $"\n<color={color}>{rolename}</color>";
                        poolablePlayer.cosmetics.nameText.transform.localPosition = new(defaultPos.x, !lowered ? defaultPos.y - 0.6f : defaultPos.y - 1.4f, -15f);
                    }
                }
            }
            catch (Exception e)
            {
                Logger.Error(e.ToString(), "OutroPatch.SetEverythingUpPatch.Postfix");
            }

            End:

            __instance.WinText.alignment = TextAlignmentOptions.Center;
            GameObject WinnerTextObject = Object.Instantiate(__instance.WinText.gameObject);
            WinnerTextObject.transform.position = new(__instance.WinText.transform.position.x, __instance.WinText.transform.position.y - 0.5f, __instance.WinText.transform.position.z);
            WinnerTextObject.transform.localScale = new(0.6f, 0.6f, 0.6f);
            var WinnerText = WinnerTextObject.GetComponent<TextMeshPro>();
            WinnerText.fontSizeMin = 3f;
            WinnerText.text = string.Empty;

            var CustomWinnerText = string.Empty;
            var AdditionalWinnerText = string.Empty;
            string CustomWinnerColor = Utils.GetRoleColorCode(CustomRoles.Crewmate);

            switch (Options.CurrentGameMode)
            {
                case CustomGameMode.SoloKombat:
                {
                    byte winnerId = CustomWinnerHolder.WinnerIds.FirstOrDefault();
                    __instance.WinText.text = Main.AllPlayerNames[winnerId] + GetString("Win");
                    __instance.WinText.fontSize -= 5f;
                    __instance.WinText.color = Main.PlayerColors[winnerId];
                    __instance.BackgroundBar.material.color = new Color32(245, 82, 82, 255);
                    WinnerText.text = $"<color=#f55252>{GetString("ModeSoloKombat")}</color>";
                    WinnerText.color = Color.red;
                    goto EndOfText;
                }
                case CustomGameMode.FFA:
                {
                    byte winnerId = CustomWinnerHolder.WinnerIds.FirstOrDefault();
                    __instance.BackgroundBar.material.color = new Color32(0, 255, 255, 255);
                    WinnerText.text = FFAManager.FFATeamMode.GetBool() ? string.Empty : Main.AllPlayerNames[winnerId] + GetString("Win");
                    WinnerText.color = Main.PlayerColors[winnerId];
                    goto EndOfText;
                }
                case CustomGameMode.MoveAndStop:
                {
                    byte winnerId = CustomWinnerHolder.WinnerIds.FirstOrDefault();
                    __instance.BackgroundBar.material.color = new Color32(0, 255, 165, 255);
                    WinnerText.text = Main.AllPlayerNames[winnerId] + GetString("Win");
                    WinnerText.color = Main.PlayerColors[winnerId];
                    goto EndOfText;
                }
                case CustomGameMode.HotPotato:
                {
                    byte winnerId = CustomWinnerHolder.WinnerIds.FirstOrDefault();
                    __instance.BackgroundBar.material.color = new Color32(232, 205, 70, 255);
                    WinnerText.text = Main.AllPlayerNames[winnerId] + GetString("Win");
                    WinnerText.color = Main.PlayerColors[winnerId];
                    goto EndOfText;
                }
                case CustomGameMode.Speedrun:
                {
                    byte winnerId = CustomWinnerHolder.WinnerIds.FirstOrDefault();
                    __instance.BackgroundBar.material.color = Utils.GetRoleColor(CustomRoles.Speedrunner);
                    WinnerText.text = Main.AllPlayerNames[winnerId] + GetString("Win");
                    WinnerText.color = Main.PlayerColors[winnerId];
                    goto EndOfText;
                }
                case CustomGameMode.CaptureTheFlag:
                {
                    (Color Color, string Team) winnerData = CTFManager.WinnerData;
                    __instance.BackgroundBar.material.color = winnerData.Color;
                    WinnerText.text = winnerData.Team;
                    WinnerText.color = winnerData.Color;
                    goto EndOfText;
                }
                case CustomGameMode.NaturalDisasters:
                {
                    byte winnerId = CustomWinnerHolder.WinnerIds.FirstOrDefault();
                    __instance.BackgroundBar.material.color = new Color32(3, 252, 74, 255);
                    WinnerText.text = Main.AllPlayerNames[winnerId] + GetString("Win");
                    WinnerText.color = Main.PlayerColors[winnerId];
                    goto EndOfText;
                }
                case CustomGameMode.RoomRush:
                {
                    byte winnerId = CustomWinnerHolder.WinnerIds.FirstOrDefault();
                    __instance.BackgroundBar.material.color = new Color32(255, 171, 27, 255);
                    WinnerText.text = Main.AllPlayerNames[winnerId] + GetString("Win");
                    WinnerText.color = Main.PlayerColors[winnerId];
                    goto EndOfText;
                }
            }

            if (CustomWinnerHolder.WinnerTeam == CustomWinner.CustomTeam)
            {
                CustomTeamManager.CustomTeam team = CustomTeamManager.WinnerTeam;
                CustomWinnerText = string.Format(GetString("CustomWinnerText"), team.TeamName);
                CustomWinnerColor = team.RoleRevealScreenBackgroundColor == "*" ? Main.NeutralColor : team.RoleRevealScreenBackgroundColor;
                __instance.BackgroundBar.material.color = ColorUtility.TryParseHtmlString(team.RoleRevealScreenBackgroundColor, out Color color) ? color : Utils.GetRoleColor(CustomRoles.Sprayer);
                AdditionalWinnerText = $"\n{team.TeamMembers.Where(r => Main.PlayerStates.Values.Any(x => x.MainRole == r)).Join(x => x.ToColoredString())}{GetString("Win")}";
                goto Skip;
            }

            var winnerRole = (CustomRoles)CustomWinnerHolder.WinnerTeam;

            if (winnerRole >= 0)
            {
                CustomWinnerText = GetWinnerRoleName(winnerRole);
                CustomWinnerColor = Utils.GetRoleColorCode(winnerRole);
                __instance.BackgroundBar.material.color = Utils.GetRoleColor(winnerRole);
            }

            if (AmongUsClient.Instance.AmHost && Main.PlayerStates[0].MainRole == CustomRoles.GM)
            {
                __instance.WinText.text = GetString("GameOver");
                __instance.WinText.color = Utils.GetRoleColor(CustomRoles.GM);
                __instance.BackgroundBar.material.color = Utils.GetRoleColor(winnerRole);
            }

            switch (CustomWinnerHolder.WinnerTeam)
            {
                case CustomWinner.Crewmate:
                    CustomWinnerColor = Utils.GetRoleColorCode(CustomRoles.Crewmate);
                    __instance.BackgroundBar.material.color = Utils.GetRoleColor(CustomRoles.Crewmate);
                    break;
                case CustomWinner.Impostor:
                    CustomWinnerColor = Utils.GetRoleColorCode(CustomRoles.Impostor);
                    __instance.BackgroundBar.material.color = Utils.GetRoleColor(CustomRoles.Impostor);
                    break;
                case CustomWinner.Egoist:
                    CustomWinnerColor = Utils.GetRoleColorCode(CustomRoles.Egoist);
                    __instance.BackgroundBar.material.color = Utils.GetRoleColor(CustomRoles.Egoist);
                    break;
                case CustomWinner.Terrorist:
                    __instance.BackgroundBar.material.color = Utils.GetRoleColor(CustomRoles.Terrorist);
                    break;
                case CustomWinner.Lovers:
                    __instance.BackgroundBar.material.color = Utils.GetRoleColor(CustomRoles.Lovers);
                    break;
                case CustomWinner.Specter:
                    __instance.BackgroundBar.material.color = Utils.GetRoleColor(CustomRoles.Specter);
                    break;
                case CustomWinner.Draw:
                    __instance.WinText.text = GetString("ForceEnd");
                    __instance.WinText.color = Color.white;
                    __instance.BackgroundBar.material.color = Color.gray;
                    WinnerText.text = GetString("ForceEndText");
                    WinnerText.color = Color.gray;
                    break;
                case CustomWinner.Neutrals:
                    __instance.WinText.text = GetString("DefeatText");
                    __instance.WinText.color = Utils.GetRoleColor(CustomRoles.Impostor);
                    __instance.BackgroundBar.material.color = Utils.GetRoleColor(CustomRoles.Executioner);
                    WinnerText.text = GetString("NeutralsLeftText");
                    WinnerText.color = Utils.GetRoleColor(CustomRoles.Executioner);
                    break;
                case CustomWinner.None:
                    __instance.WinText.text = string.Empty;
                    __instance.WinText.color = Color.black;
                    __instance.BackgroundBar.material.color = Color.gray;
                    WinnerText.text = GetString("EveryoneDied");
                    WinnerText.color = Color.gray;
                    break;
                case CustomWinner.Error:
                    __instance.WinText.text = GetString("ErrorEndText");
                    __instance.WinText.color = Color.red;
                    __instance.BackgroundBar.material.color = Color.red;
                    WinnerText.text = GetString("ErrorEndTextDescription");
                    WinnerText.color = Color.white;
                    break;
            }

            foreach (AdditionalWinners additionalWinners in CustomWinnerHolder.AdditionalWinnerTeams)
            {
                var addWinnerRole = (CustomRoles)additionalWinners;
                Logger.Warn(additionalWinners.ToString(), "AdditionalWinner");
                if (addWinnerRole == CustomRoles.Sidekick) continue;

                AdditionalWinnerText += "\n" + Utils.ColorString(Utils.GetRoleColor(addWinnerRole), GetAdditionalWinnerRoleName(addWinnerRole));
            }

            Skip:

            if (CustomWinnerHolder.WinnerTeam is not CustomWinner.Draw and not CustomWinner.None and not CustomWinner.Error) WinnerText.text = AdditionalWinnerText == string.Empty ? $"<size=100%><color={CustomWinnerColor}>{CustomWinnerText}</color></size>" : $"<size=100%><color={CustomWinnerColor}>{CustomWinnerText}</color></size><size=50%>{AdditionalWinnerText}</size>";

            EndOfText:

            LastWinsText = WinnerText.text /*.RemoveHtmlTags()*/;

            ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

            //########################################
            //     ==The final result indicates==
            //########################################

            Vector3 Pos = Camera.main.ViewportToWorldPoint(new(0f, 1f, Camera.main.nearClipPlane));
            GameObject RoleSummaryObject = Object.Instantiate(__instance.WinText.gameObject);
            RoleSummaryObject.transform.position = new(__instance.Navigation.ExitButton.transform.position.x + 0.1f, Pos.y - 0.1f, -15f);
            RoleSummaryObject.transform.localScale = new(1f, 1f, 1f);

            StringBuilder sb = new($"<font=\"DIN_Pro_Bold_700 SDF\">{GetString("RoleSummaryText")}\n<b>");
            List<byte> cloneRoles = [.. Main.PlayerStates.Keys];

            foreach (byte id in Main.WinnerList)
            {
                if (EndGamePatch.SummaryText[id].Contains("<INVALID:NotAssigned>")) continue;

                sb.Append('\n').Append(EndGamePatch.SummaryText[id]);
                cloneRoles.Remove(id);
            }

            sb.Append("</b>\n");

            switch (Options.CurrentGameMode)
            {
                case CustomGameMode.SoloKombat:
                {
                    List<(int, byte)> list = [];
                    list.AddRange(cloneRoles.Select(id => (SoloKombatManager.GetRankOfScore(id), id)));

                    list.Sort();
                    foreach ((int, byte) id in list.Where(x => EndGamePatch.SummaryText.ContainsKey(x.Item2))) sb.Append('\n').Append(EndGamePatch.SummaryText[id.Item2]);

                    break;
                }
                case CustomGameMode.FFA:
                {
                    List<(int, byte)> list = [];
                    list.AddRange(cloneRoles.Select(id => (FFAManager.GetRankFromScore(id), id)));

                    list.Sort();
                    foreach ((int, byte) id in list.Where(x => EndGamePatch.SummaryText.ContainsKey(x.Item2))) sb.Append('\n').Append(EndGamePatch.SummaryText[id.Item2]);

                    break;
                }
                case CustomGameMode.MoveAndStop:
                {
                    List<(int, byte)> list = [];
                    list.AddRange(cloneRoles.Select(id => (MoveAndStopManager.GetRankOfScore(id), id)));

                    list.Sort();
                    foreach ((int, byte) id in list.Where(x => EndGamePatch.SummaryText.ContainsKey(x.Item2))) sb.Append('\n').Append(EndGamePatch.SummaryText[id.Item2]);

                    break;
                }
                case CustomGameMode.HotPotato:
                {
                    IOrderedEnumerable<byte> list = cloneRoles.OrderByDescending(HotPotatoManager.GetSurvivalTime);
                    foreach (byte id in list.Where(EndGamePatch.SummaryText.ContainsKey)) sb.Append('\n').Append(EndGamePatch.SummaryText[id]);

                    break;
                }
                case CustomGameMode.Speedrun:
                {
                    IOrderedEnumerable<byte> list = cloneRoles.OrderByDescending(id => Main.PlayerStates[id].TaskState.CompletedTasksCount);
                    foreach (byte id in list.Where(EndGamePatch.SummaryText.ContainsKey)) sb.Append('\n').Append(EndGamePatch.SummaryText[id]);

                    break;
                }
                case CustomGameMode.CaptureTheFlag:
                {
                    IOrderedEnumerable<byte> list = cloneRoles.OrderByDescending(CTFManager.GetFlagTime);
                    foreach (byte id in list.Where(EndGamePatch.SummaryText.ContainsKey)) sb.Append('\n').Append(EndGamePatch.SummaryText[id]);

                    break;
                }
                case CustomGameMode.NaturalDisasters:
                {
                    IOrderedEnumerable<byte> list = cloneRoles.OrderByDescending(NaturalDisasters.SurvivalTime);
                    foreach (byte id in list.Where(EndGamePatch.SummaryText.ContainsKey)) sb.Append('\n').Append(EndGamePatch.SummaryText[id]);

                    break;
                }
                case CustomGameMode.RoomRush:
                {
                    IOrderedEnumerable<byte> list = cloneRoles.OrderByDescending(RoomRush.GetSurvivalTime);
                    foreach (byte id in list.Where(EndGamePatch.SummaryText.ContainsKey)) sb.Append('\n').Append(EndGamePatch.SummaryText[id]);

                    break;
                }
                default:
                {
                    foreach (byte id in cloneRoles)
                    {
                        if (EndGamePatch.SummaryText[id].Contains("<INVALID:NotAssigned>")) continue;

                        sb.Append('\n').Append(EndGamePatch.SummaryText[id]);
                    }

                    break;
                }
            }

            sb.Append("</font>");

            var RoleSummary = RoleSummaryObject.GetComponent<TextMeshPro>();
            RoleSummary.alignment = TextAlignmentOptions.TopLeft;
            RoleSummary.color = Color.white;
            RoleSummary.outlineWidth *= 1.2f;
            RoleSummary.fontSizeMin = RoleSummary.fontSizeMax = RoleSummary.fontSize = 1.25f;

            var RoleSummaryRectTransform = RoleSummary.GetComponent<RectTransform>();
            RoleSummaryRectTransform.anchoredPosition = new(Pos.x + 3.5f, Pos.y - 0.1f);
            RoleSummary.text = sb.ToString();

            return;

            static string GetAdditionalWinnerRoleName(CustomRoles role)
            {
                string name = GetString($"AdditionalWinnerRoleText.{role}");
                if (name == string.Empty || name.StartsWith("*") || name.StartsWith("<INVALID")) name = string.Format(GetString("AdditionalWinnerRoleText.Default"), GetString($"{role}"));

                return name;
            }

            static string GetWinnerRoleName(CustomRoles role)
            {
                string name = GetString($"WinnerRoleText.{role}");
                if (name == string.Empty || name.StartsWith("*") || name.StartsWith("<INVALID")) name = string.Format(GetString("WinnerRoleText.Default"), GetString($"{role}"));

                return name;
            }
        }
    }
}