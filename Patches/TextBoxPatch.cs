﻿using System;
using System.Linq;
using HarmonyLib;
using TMPro;
using UnityEngine;

namespace EHR.Patches
{
    [HarmonyPatch(typeof(TextBoxTMP), nameof(TextBoxTMP.SetText))]
    internal static class TextBoxTMPSetTextPatch
    {
        private static TextMeshPro PlaceHolderText;
        private static TextMeshPro CommandInfoText;

        public static bool IsInvalidCommand;

        // The only characters to treat specially are \r, \n and \b, allow all other characters to be written
        public static bool Prefix(TextBoxTMP __instance, [HarmonyArgument(0)] string input, [HarmonyArgument(1)] string inputCompo = "")
        {
            try
            {
                var flag = false;
                var ch = ' ';
                __instance.AdjustCaretPosition(input.Length - __instance.text.Length);
                __instance.tempTxt.Clear();

                foreach (char c in input)
                {
                    char upperInvariant = c;

                    if (ch == ' ' && upperInvariant == ' ')
                        __instance.AdjustCaretPosition(-1);
                    else
                    {
                        switch (upperInvariant)
                        {
                            case '\r':
                            case '\n':
                                flag = true;
                                break;
                            case '\b':
                                __instance.tempTxt.Length = Math.Max(__instance.tempTxt.Length - 1, 0);
                                __instance.AdjustCaretPosition(-2);
                                break;
                        }

                        if (__instance.ForceUppercase) upperInvariant = char.ToUpperInvariant(upperInvariant);

                        if (upperInvariant is not '\r' and not '\n' and not '\b')
                        {
                            __instance.tempTxt.Append(upperInvariant);
                            ch = upperInvariant;
                        }
                    }
                }

                if (!__instance.tempTxt.ToString().Equals(DestroyableSingleton<TranslationController>.Instance.GetString(StringNames.EnterName), StringComparison.OrdinalIgnoreCase) && __instance.characterLimit > 0)
                {
                    int length = __instance.tempTxt.Length;
                    __instance.tempTxt.Length = Math.Min(__instance.tempTxt.Length, __instance.characterLimit);
                    __instance.AdjustCaretPosition(-(length - __instance.tempTxt.Length));
                }

                input = __instance.tempTxt.ToString();

                if (!input.Equals(__instance.text) || !inputCompo.Equals(__instance.compoText))
                {
                    __instance.text = input;
                    __instance.compoText = inputCompo;
                    string str = __instance.text;
                    string compoText = __instance.compoText;

                    if (__instance.Hidden)
                    {
                        str = "";
                        for (var index = 0; index < __instance.text.Length; ++index) str += "*";
                    }

                    __instance.outputText.text = str + compoText;
                    __instance.outputText.ForceMeshUpdate(true, true);
                    if (__instance.keyboard != null) __instance.keyboard.text = __instance.text;

                    __instance.OnChange.Invoke();
                }

                if (flag) __instance.OnEnter.Invoke();

                __instance.SetPipePosition();
            }
            catch (Exception e)
            {
                Utils.ThrowException(e);
            }

            return false;
        }

        public static void Postfix(TextBoxTMP __instance)
        {
            try
            {
                if (!Main.EnableCommandHelper.Value)
                {
                    Destroy();
                    return;
                }

                string input = __instance.outputText.text.Trim().Replace("\b", "");

                if (!input.StartsWith('/') || input.Length < 2)
                {
                    Destroy();
                    IsInvalidCommand = false;
                    return;
                }

                Command command = null;
                double highestMatchRate = 0;
                string inputCheck = input.Split(' ')[0];
                var exactMatch = false;
                bool english = TranslationController.Instance.currentLanguage.languageID == SupportedLangs.English;

                foreach (Command cmd in ChatCommands.AllCommands)
                {
                    foreach (string form in cmd.CommandForms)
                    {
                        if (english && !form.All(char.IsAscii)) continue;

                        string check = "/" + form;
                        if (check.Length < inputCheck.Length) continue;

                        if (check == inputCheck)
                        {
                            highestMatchRate = 1;
                            command = cmd;
                            exactMatch = true;
                            break;
                        }

                        var matchNum = 0;

                        for (var i = 0; i < inputCheck.Length; i++)
                        {
                            if (i >= check.Length) break;

                            if (inputCheck[i].Equals(check[i]))
                                matchNum++;
                            else
                                break;
                        }

                        double matchRate = (double)matchNum / inputCheck.Length;

                        if (matchRate > highestMatchRate)
                        {
                            highestMatchRate = matchRate;
                            command = cmd;
                        }
                    }

                    if (exactMatch) break;
                }

                if (command == null || highestMatchRate < 1)
                {
                    Destroy();
                    IsInvalidCommand = true;
                    __instance.compoText.Color(Color.red);
                    __instance.outputText.color = Color.red;
                    return;
                }

                IsInvalidCommand = false;

                if (PlaceHolderText == null)
                {
                    PlaceHolderText = Object.Instantiate(__instance.outputText, __instance.outputText.transform.parent);
                    PlaceHolderText.name = "PlaceHolderText";
                    PlaceHolderText.color = new(0.7f, 0.7f, 0.7f, 0.7f);
                    PlaceHolderText.transform.localPosition = __instance.outputText.transform.localPosition;
                }

                if (CommandInfoText == null)
                {
                    HudManager hud = DestroyableSingleton<HudManager>.Instance;
                    CommandInfoText = Object.Instantiate(hud.KillButton.cooldownTimerText, hud.transform, true);
                    CommandInfoText.name = "CommandInfoText";
                    CommandInfoText.alignment = TextAlignmentOptions.Left;
                    CommandInfoText.verticalAlignment = VerticalAlignmentOptions.Top;
                    CommandInfoText.transform.localPosition = new(-3.2f, -2.35f, 0f);
                    CommandInfoText.overflowMode = TextOverflowModes.Overflow;
                    CommandInfoText.enableWordWrapping = false;
                    CommandInfoText.color = Color.white;
                    CommandInfoText.fontSize = CommandInfoText.fontSizeMax = CommandInfoText.fontSizeMin = 1.8f;
                    CommandInfoText.sortingOrder = 1000;
                    CommandInfoText.transform.SetAsLastSibling();
                }

                string inputForm = input.TrimStart('/');
                string text = "/" + (exactMatch ? inputForm : command.CommandForms.Where(x => x.All(char.IsAscii) && x.StartsWith(inputForm)).MaxBy(x => x.Length));
                var info = $"<b>{command.Description}</b>";

                if (exactMatch && command.Arguments.Length > 0)
                {
                    bool poll = command.CommandForms.Contains("poll");
                    bool say = command.CommandForms.Contains("say");
                    int spaces = poll ? input.SkipWhile(x => x != '?').Count(x => x == ' ') + 1 : input.Count(x => x == ' ');
                    if (say) spaces = Math.Min(spaces, 1);

                    var preText = $"{text} {command.Arguments}";
                    if (!poll) text += " " + command.Arguments.Split(' ').Skip(spaces).Join(delimiter: " ");

                    string[] args = preText.Split(' ')[1..];

                    for (var i = 0; i < args.Length; i++)
                    {
                        if (command.ArgsDescriptions.Length <= i) break;

                        int skip = poll ? input.TakeWhile(x => x != '?').Count(x => x == ' ') - 1 : 0;
                        string arg = poll ? i == 0 ? args[..++skip].Join(delimiter: " ") : args[spaces - 1 < i ? skip + i + spaces : skip + i] : args[spaces > i ? i : i + spaces];
                        bool current = spaces - 1 == i, invalid = IsInvalidArg(), valid = IsValidArg();

                        info += "\n" + (invalid, current, valid) switch
                        {
                            (true, true, false) => "<#ffa500>\u27a1    ",
                            (true, false, false) => "<#ff0000>        ",
                            (false, true, true) => "<#00ffa5>\u27a1 \u2713 ",
                            (false, false, true) => "<#00ffa5>\u2713</color> <#00ffff>     ",
                            (false, true, false) => "<#ffff44>\u27a1    ",
                            _ => "        "
                        };

                        info += $"   - <b>{arg}</b>{GetExtraArgInfo()}: {command.ArgsDescriptions[i]}";
                        if (current || invalid || valid) info += "</color>";

                        continue;

                        bool IsInvalidArg()
                        {
                            return arg != command.Arguments.Split(' ')[i] && command.Arguments.Split(' ')[i] switch
                            {
                                "{id}" or "{id1}" or "{id2}" => !byte.TryParse(arg, out byte id) || Main.AllPlayerControls.All(x => x.PlayerId != id),
                                "{number}" or "{level}" or "{duration}" or "{number1}" or "{number2}" => !int.TryParse(arg, out int num) || num < 0,
                                "{team}" => arg is not "crew" and not "imp",
                                "{role}" => !ChatCommands.GetRoleByName(arg, out _),
                                "{addon}" => !ChatCommands.GetRoleByName(arg, out CustomRoles role) || !role.IsAdditionRole(),
                                "{letter}" => arg.Length != 1 || !char.IsLetter(arg[0]),
                                "{chance}" => !int.TryParse(arg, out int chance) || chance < 0 || chance > 100 || chance % 5 != 0,
                                _ => false
                            };
                        }

                        bool IsValidArg()
                        {
                            return command.Arguments.Split(' ')[i].Replace('[', '{').Replace(']', '}') switch
                            {
                                "{id}" or "{id1}" or "{id2}" => byte.TryParse(arg, out byte id) && Main.AllPlayerControls.Any(x => x.PlayerId == id),
                                "{team}" => arg is "crew" or "imp",
                                "{role}" => ChatCommands.GetRoleByName(arg, out _),
                                "{addon}" => ChatCommands.GetRoleByName(arg, out CustomRoles role) && role.IsAdditionRole(),
                                "{chance}" => int.TryParse(arg, out int chance) && chance is >= 0 and <= 100 && chance % 5 == 0,
                                _ => false
                            };
                        }

                        string GetExtraArgInfo()
                        {
                            return !IsValidArg()
                                ? string.Empty
                                : command.Arguments.Split(' ')[i] switch
                                {
                                    "{id}" or "{id1}" or "{id2}" => $" ({byte.Parse(arg).ColoredPlayerName()})",
                                    "{role}" or "{addon}" when ChatCommands.GetRoleByName(arg, out CustomRoles role) => $" ({role.ToColoredString()})",
                                    _ => string.Empty
                                };
                        }
                    }
                }

                PlaceHolderText.text = text;
                CommandInfoText.text = info;

                PlaceHolderText.enabled = true;
                CommandInfoText.enabled = true;
            }
            catch
            {
                Destroy();
            }

            return;

            void Destroy()
            {
                if (PlaceHolderText != null) PlaceHolderText.enabled = false;

                if (CommandInfoText != null) CommandInfoText.enabled = false;
            }
        }

        public static void OnTabPress(ChatController __instance)
        {
            if (PlaceHolderText == null || PlaceHolderText.text == "") return;

            __instance.freeChatField.textArea.SetText(PlaceHolderText.text);
            __instance.freeChatField.textArea.compoText = "";
        }

        public static void Update()
        {
            try
            {
                bool open = HudManager.Instance?.Chat?.IsOpenOrOpening ?? false;
                PlaceHolderText?.gameObject.SetActive(open);
                CommandInfoText?.gameObject.SetActive(open);
            }
            catch { }
        }
    }

/*
//Thanks https://github.com/NuclearPowered/Reactor/blob/master/Reactor/Patches/Fixes/CursorPosPatch.cs

/// <summary>
/// "Fixes" an issue where empty TextBoxes have wrong cursor positions.
/// </summary>
[HarmonyPatch(typeof(TextMeshProExtensions), nameof(TextMeshProExtensions.CursorPos))]
internal static class CursorPosPatch
{
    public static bool Prefix(TextMeshPro self, ref Vector2 __result)
    {
        if (self.textInfo == null || self.textInfo.lineCount == 0 || self.textInfo.lineInfo[0].characterCount <= 0)
        {
            __result = self.GetTextInfo(" ").lineInfo[0].lineExtents.max;
            return false;
        }

        return true;
    }
}*/

/* Originally by KARPED1EM. Reference: https://github.com/KARPED1EM/TownOfNext/blob/TONX/TONX/Patches/TextBoxPatch.cs */
    [HarmonyPatch(typeof(TextBoxTMP))]
    public class TextBoxPatch
    {
        [HarmonyPatch(nameof(TextBoxTMP.SetText))]
        [HarmonyPrefix]
        public static void ModifyCharacterLimit(TextBoxTMP __instance)
        {
            __instance.characterLimit = 1200;
        }
    }
}