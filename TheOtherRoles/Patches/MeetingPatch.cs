using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using AmongUs.GameOptions;
using Assets.CoreScripts;
using BepInEx.Unity.IL2CPP.Utils.Collections;
using HarmonyLib;
using Hazel;
using TheOtherRoles.Modules;
using TheOtherRoles.Objects;
using TheOtherRoles.Utilities;
using TMPro;
using UnityEngine;
using static TheOtherRoles.TheOtherRoles;
using static TheOtherRoles.TORMapOptions;

namespace TheOtherRoles.Patches
{
    [HarmonyPatch]
    class MeetingHudPatch
    {
        static bool[] selections;
        static SpriteRenderer[] renderers;
        private static NetworkedPlayerInfo target = null;
        private const float scale = 0.65f;
        private static TMPro.TextMeshPro meetingExtraButtonText;
        private static PassiveButton[] swapperButtonList;
        private static TMPro.TextMeshPro meetingExtraButtonLabel;
        private static PlayerVoteArea swapped1 = null;
        private static PlayerVoteArea swapped2 = null;

        [HarmonyPatch(typeof(MeetingHud), nameof(MeetingHud.CheckForEndVoting))]
        class MeetingCalculateVotesPatch
        {
            private static Dictionary<byte, int> CalculateVotes(MeetingHud __instance)
            {
                Dictionary<byte, int> dictionary = new Dictionary<byte, int>();
                for (int i = 0; i < __instance.playerStates.Length; i++)
                {
                    PlayerVoteArea playerVoteArea = __instance.playerStates[i];
                    if (playerVoteArea.VotedFor != 252 && playerVoteArea.VotedFor != 255 && playerVoteArea.VotedFor != 254)
                    {
                        PlayerControl player = Helpers.playerById((byte)playerVoteArea.TargetPlayerId);
                        if (player == null || player.Data == null || player.Data.IsDead || player.Data.Disconnected) continue;

                        int currentVotes;
                        int additionalVotes = (Mayor.mayor != null && Mayor.mayor.PlayerId == playerVoteArea.TargetPlayerId && Mayor.voteTwice) ? 2 : 1; // Mayor vote
                        if (dictionary.TryGetValue(playerVoteArea.VotedFor, out currentVotes))
                            dictionary[playerVoteArea.VotedFor] = currentVotes + additionalVotes;
                        else
                            dictionary[playerVoteArea.VotedFor] = additionalVotes;
                    }
                }
                // Swapper swap votes
                if (Swapper.swapper != null && !Swapper.swapper.Data.IsDead)
                {
                    swapped1 = null;
                    swapped2 = null;
                    foreach (PlayerVoteArea playerVoteArea in __instance.playerStates)
                    {
                        if (playerVoteArea.TargetPlayerId == Swapper.playerId1) swapped1 = playerVoteArea;
                        if (playerVoteArea.TargetPlayerId == Swapper.playerId2) swapped2 = playerVoteArea;
                    }

                    if (swapped1 != null && swapped2 != null)
                    {
                        if (!dictionary.ContainsKey(swapped1.TargetPlayerId)) dictionary[swapped1.TargetPlayerId] = 0;
                        if (!dictionary.ContainsKey(swapped2.TargetPlayerId)) dictionary[swapped2.TargetPlayerId] = 0;
                        int tmp = dictionary[swapped1.TargetPlayerId];
                        dictionary[swapped1.TargetPlayerId] = dictionary[swapped2.TargetPlayerId];
                        dictionary[swapped2.TargetPlayerId] = tmp;
                    }
                }

                return dictionary;
            }


            static bool Prefix(MeetingHud __instance)
            {
                if (__instance.playerStates.All((PlayerVoteArea ps) => ps.AmDead || ps.DidVote))
                {
                    // If skipping is disabled, replace skipps/no-votes with self vote
                    if (target == null && blockSkippingInEmergencyMeetings && noVoteIsSelfVote)
                    {
                        foreach (PlayerVoteArea playerVoteArea in __instance.playerStates)
                        {
                            if (playerVoteArea.VotedFor == byte.MaxValue - 1) playerVoteArea.VotedFor = playerVoteArea.TargetPlayerId; // TargetPlayerId
                        }
                    }

                    if (YasunaJr.yasunaJr != null && !YasunaJr.yasunaJr.Data.IsDead && YasunaJr.specialVoteTargetPlayerId != byte.MaxValue)
                    {
                        byte takeAwayTheVoteTargetPlayerId = YasunaJr.specialVoteTargetPlayerId;
                        for (int i = 0; i < __instance.playerStates.Length; i++)
                        {
                            PlayerVoteArea playerVoteArea = __instance.playerStates[i];
                            if (playerVoteArea.TargetPlayerId == YasunaJr.specialVoteTargetPlayerId)
                            {
                                byte selfVotedFor = byte.MaxValue;
                                for (int j = 0; j < __instance.playerStates.Length; j++)
                                {
                                    if (__instance.playerStates[j].TargetPlayerId == YasunaJr.yasunaJr.PlayerId)
                                    {
                                        selfVotedFor = __instance.playerStates[j].VotedFor;
                                        break;
                                    }
                                }
                                playerVoteArea.VotedFor = selfVotedFor;
                            }
                        }
                    }

                    Dictionary<byte, int> self = CalculateVotes(__instance);
                    bool tie;
                    KeyValuePair<byte, int> max = self.MaxPair(out tie);
                    NetworkedPlayerInfo exiled = GameData.Instance.AllPlayers.ToArray().FirstOrDefault(v => !tie && v.PlayerId == max.Key && !v.IsDead);

                    // TieBreaker 
                    List<NetworkedPlayerInfo> potentialExiled = new List<NetworkedPlayerInfo>();
                    bool skipIsTie = false;
                    if (self.Count > 0)
                    {
                        Tiebreaker.isTiebreak = false;
                        int maxVoteValue = self.Values.Max();
                        PlayerVoteArea tb = null;
                        if (Tiebreaker.tiebreaker != null)
                            tb = __instance.playerStates.ToArray().FirstOrDefault(x => x.TargetPlayerId == Tiebreaker.tiebreaker.PlayerId);
                        bool isTiebreakerSkip = tb == null || tb.VotedFor == 253;
                        if (tb != null && tb.AmDead) isTiebreakerSkip = true;

                        foreach (KeyValuePair<byte, int> pair in self)
                        {
                            if (pair.Value != maxVoteValue || isTiebreakerSkip) continue;
                            if (pair.Key != 253)
                                potentialExiled.Add(GameData.Instance.AllPlayers.ToArray().FirstOrDefault(x => x.PlayerId == pair.Key));
                            else
                                skipIsTie = true;
                        }
                    }

                    byte forceTargetPlayerId = Yasuna.yasuna != null && !Yasuna.yasuna.Data.IsDead && Yasuna.specialVoteTargetPlayerId != byte.MaxValue ? Yasuna.specialVoteTargetPlayerId : byte.MaxValue;
                    if (forceTargetPlayerId != byte.MaxValue)
                        tie = false;
                    MeetingHud.VoterState[] array = new MeetingHud.VoterState[__instance.playerStates.Length];
                    for (int i = 0; i < __instance.playerStates.Length; i++)
                    {
                        PlayerVoteArea playerVoteArea = __instance.playerStates[i];
                        if (forceTargetPlayerId != byte.MaxValue)
                            playerVoteArea.VotedFor = forceTargetPlayerId;

                        array[i] = new MeetingHud.VoterState
                        {
                            VoterId = playerVoteArea.TargetPlayerId,
                            VotedForId = playerVoteArea.VotedFor
                        };

                        if (Tiebreaker.tiebreaker == null || playerVoteArea.TargetPlayerId != Tiebreaker.tiebreaker.PlayerId) continue;

                        byte tiebreakerVote = playerVoteArea.VotedFor;
                        if (swapped1 != null && swapped2 != null)
                        {
                            if (tiebreakerVote == swapped1.TargetPlayerId) tiebreakerVote = swapped2.TargetPlayerId;
                            else if (tiebreakerVote == swapped2.TargetPlayerId) tiebreakerVote = swapped1.TargetPlayerId;
                        }

                        if (potentialExiled.FindAll(x => x != null && x.PlayerId == tiebreakerVote).Count > 0 && (potentialExiled.Count > 1 || skipIsTie))
                        {
                            exiled = potentialExiled.ToArray().FirstOrDefault(v => v.PlayerId == tiebreakerVote);
                            tie = false;

                            MessageWriter writer = AmongUsClient.Instance.StartRpcImmediately(PlayerControl.LocalPlayer.NetId, (byte)CustomRPC.SetTiebreak, Hazel.SendOption.Reliable, -1);
                            AmongUsClient.Instance.FinishRpcImmediately(writer);
                            RPCProcedure.setTiebreak();
                        }
                    }

                    if (forceTargetPlayerId != byte.MaxValue)
                        exiled = GameData.Instance.AllPlayers.ToArray().FirstOrDefault(v => v.PlayerId == forceTargetPlayerId && !v.IsDead);

                    // RPCVotingComplete
                    __instance.RpcVotingComplete(array, exiled, tie);
                }
                return false;
            }
        }

        [HarmonyPatch(typeof(MeetingHud), nameof(MeetingHud.BloopAVoteIcon))]
        class MeetingHudBloopAVoteIconPatch
        {
            public static bool Prefix(MeetingHud __instance, NetworkedPlayerInfo voterPlayer, int index, Transform parent)
            {
                var spriteRenderer = UnityEngine.Object.Instantiate<SpriteRenderer>(__instance.PlayerVotePrefab);
                var showVoteColors = !GameManager.Instance.LogicOptions.GetAnonymousVotes() ||
                                      (PlayerControl.LocalPlayer.Data.IsDead && TORMapOptions.ghostsSeeVotes) ||
                                      (Mayor.mayor != null && Mayor.mayor == PlayerControl.LocalPlayer && Mayor.canSeeVoteColors && TasksHandler.taskInfo(PlayerControl.LocalPlayer.Data).Item1 >= Mayor.tasksNeededToSeeVoteColors);
                if (showVoteColors)
                {
                    PlayerMaterial.SetColors(voterPlayer.DefaultOutfit.ColorId, spriteRenderer);
                }
                else
                {
                    PlayerMaterial.SetColors(Palette.DisabledGrey, spriteRenderer);
                }
                var transform = spriteRenderer.transform;
                transform.SetParent(parent);
                transform.localScale = Vector3.zero;
                var component = parent.GetComponent<PlayerVoteArea>();
                if (component != null)
                {
                    spriteRenderer.material.SetInt(PlayerMaterial.MaskLayer, component.MaskLayer);
                }
                __instance.StartCoroutine(Effects.Bloop(index * 0.3f, transform));
                parent.GetComponent<VoteSpreader>().AddVote(spriteRenderer);
                return false;
            }
        }

        [HarmonyPatch(typeof(MeetingHud), nameof(MeetingHud.PopulateResults))]
        class MeetingHudPopulateVotesPatch
        {
            private static bool Prefix(MeetingHud __instance, Il2CppStructArray<MeetingHud.VoterState> states)
            {
                // Swapper swap

                PlayerVoteArea swapped1 = null;
                PlayerVoteArea swapped2 = null;
                foreach (PlayerVoteArea playerVoteArea in __instance.playerStates)
                {
                    if (playerVoteArea.TargetPlayerId == Swapper.playerId1) swapped1 = playerVoteArea;
                    if (playerVoteArea.TargetPlayerId == Swapper.playerId2) swapped2 = playerVoteArea;
                }
                bool doSwap = swapped1 != null && swapped2 != null && Swapper.swapper != null && !Swapper.swapper.Data.IsDead;
                if (doSwap)
                {
                    __instance.StartCoroutine(Effects.Slide3D(swapped1.transform, swapped1.transform.localPosition, swapped2.transform.localPosition, 1.5f));
                    __instance.StartCoroutine(Effects.Slide3D(swapped2.transform, swapped2.transform.localPosition, swapped1.transform.localPosition, 1.5f));
                }


                __instance.TitleText.text = FastDestroyableSingleton<TranslationController>.Instance.GetString(StringNames.MeetingVotingResults, new Il2CppReferenceArray<Il2CppSystem.Object>(0));
                int num = 0;
                for (int i = 0; i < __instance.playerStates.Length; i++)
                {
                    PlayerVoteArea playerVoteArea = __instance.playerStates[i];
                    byte targetPlayerId = playerVoteArea.TargetPlayerId;
                    // Swapper change playerVoteArea that gets the votes
                    if (doSwap && playerVoteArea.TargetPlayerId == swapped1.TargetPlayerId) playerVoteArea = swapped2;
                    else if (doSwap && playerVoteArea.TargetPlayerId == swapped2.TargetPlayerId) playerVoteArea = swapped1;

                    playerVoteArea.ClearForResults();
                    int num2 = 0;
                    bool mayorFirstVoteDisplayed = false;
                    for (int j = 0; j < states.Length; j++)
                    {
                        MeetingHud.VoterState voterState = states[j];
                        NetworkedPlayerInfo playerById = GameData.Instance.GetPlayerById(voterState.VoterId);
                        if (playerById == null)
                        {
                            Debug.LogError(string.Format("Couldn't find player info for voter: {0}", voterState.VoterId));
                        }
                        else if (i == 0 && voterState.SkippedVote && !playerById.IsDead)
                        {
                            __instance.BloopAVoteIcon(playerById, num, __instance.SkippedVoting.transform);
                            num++;
                        }
                        else if (voterState.VotedForId == targetPlayerId && !playerById.IsDead)
                        {
                            __instance.BloopAVoteIcon(playerById, num2, playerVoteArea.transform);
                            num2++;
                        }

                        // Major vote, redo this iteration to place a second vote
                        if (Mayor.mayor != null && voterState.VoterId == (sbyte)Mayor.mayor.PlayerId && !mayorFirstVoteDisplayed && Mayor.voteTwice)
                        {
                            mayorFirstVoteDisplayed = true;
                            j--;
                        }
                    }
                }
                return false;
            }
        }

        [HarmonyPatch(typeof(MeetingHud), nameof(MeetingHud.VotingComplete))]
        class MeetingHudVotingCompletedPatch
        {
            static void Postfix(MeetingHud __instance, [HarmonyArgument(0)] byte[] states, [HarmonyArgument(1)] NetworkedPlayerInfo exiled, [HarmonyArgument(2)] bool tie)
            {
                if (Yasuna.isYasuna(PlayerControl.LocalPlayer.PlayerId) && Yasuna.specialVoteTargetPlayerId == byte.MaxValue)
                {
                    for (int i = 0; i < __instance.playerStates.Length; i++)
                    {
                        PlayerVoteArea voteArea = __instance.playerStates[i];
                        Transform t = voteArea.transform.FindChild("SpecialVoteButton");
                        if (t != null)
                            t.gameObject.SetActive(false);
                    }
                }
                if (YasunaJr.isYasunaJr(PlayerControl.LocalPlayer.PlayerId) && YasunaJr.specialVoteTargetPlayerId == byte.MaxValue)
                {
                    for (int i = 0; i < __instance.playerStates.Length; i++)
                    {
                        PlayerVoteArea voteArea = __instance.playerStates[i];
                        Transform t = voteArea.transform.FindChild("SpecialVoteButton2");
                        if (t != null)
                            t.gameObject.SetActive(false);
                    }
                }

                // Reset swapper values
                Swapper.playerId1 = Byte.MaxValue;
                Swapper.playerId2 = Byte.MaxValue;

                // Lovers, Lawyer & Pursuer save next to be exiled, because RPC of ending game comes before RPC of exiled
                Lovers.notAckedExiledIsLover = false;
                Pursuer.notAckedExiled = false;
                if (exiled != null)
                {
                    Lovers.notAckedExiledIsLover = ((Lovers.lover1 != null && Lovers.lover1.PlayerId == exiled.PlayerId) || (Lovers.lover2 != null && Lovers.lover2.PlayerId == exiled.PlayerId));
                    Pursuer.notAckedExiled = (Pursuer.pursuer != null && Pursuer.pursuer.PlayerId == exiled.PlayerId) || (Lawyer.lawyer != null && Lawyer.target != null && Lawyer.target.PlayerId == exiled.PlayerId && Lawyer.target != Jester.jester && !Lawyer.isProsecutor);
                }

                // Mini
                if (!Mini.isGrowingUpInMeeting) Mini.timeOfGrowthStart = Mini.timeOfGrowthStart.Add(DateTime.UtcNow.Subtract(Mini.timeOfMeetingStart)).AddSeconds(10);
            }
        }


        static void swapperOnClick(int i, MeetingHud __instance)
        {
            if (__instance.state == MeetingHud.VoteStates.Results || Swapper.charges <= 0) return;
            if (__instance.playerStates[i].AmDead) return;

            int selectedCount = selections.Where(b => b).Count();
            SpriteRenderer renderer = renderers[i];

            if (selectedCount == 0)
            {
                renderer.color = Color.yellow;
                selections[i] = true;
            }
            else if (selectedCount == 1)
            {
                if (selections[i])
                {
                    renderer.color = Color.red;
                    selections[i] = false;
                }
                else
                {
                    selections[i] = true;
                    renderer.color = Color.yellow;
                    meetingExtraButtonLabel.text = Helpers.cs(Color.yellow, ModTranslation.GetString("Game-Swapper", 2));
                }
            }
            else if (selectedCount == 2)
            {
                if (selections[i])
                {
                    renderer.color = Color.red;
                    selections[i] = false;
                    meetingExtraButtonLabel.text = Helpers.cs(Color.red, ModTranslation.GetString("Game-Swapper", 2));
                }
            }
        }

        static void swapperConfirm(MeetingHud __instance)
        {
            __instance.playerStates[0].Cancel();  // This will stop the underlying buttons of the template from showing up
            if (__instance.state == MeetingHud.VoteStates.Results) return;
            if (selections.Where(b => b).Count() != 2) return;
            if (Swapper.charges <= 0 || Swapper.playerId1 != Byte.MaxValue) return;

            PlayerVoteArea firstPlayer = null;
            PlayerVoteArea secondPlayer = null;
            for (int A = 0; A < selections.Length; A++)
            {
                if (selections[A])
                {
                    if (firstPlayer == null)
                    {
                        firstPlayer = __instance.playerStates[A];
                    }
                    else
                    {
                        secondPlayer = __instance.playerStates[A];
                    }
                    renderers[A].color = Color.green;
                }
                else if (renderers[A] != null)
                {
                    renderers[A].color = Color.gray;
                }
                if (swapperButtonList[A] != null) swapperButtonList[A].OnClick.RemoveAllListeners();  // Swap buttons can't be clicked / changed anymore
            }
            if (firstPlayer != null && secondPlayer != null)
            {
                MessageWriter writer = AmongUsClient.Instance.StartRpcImmediately(PlayerControl.LocalPlayer.NetId, (byte)CustomRPC.SwapperSwap, Hazel.SendOption.Reliable, -1);
                writer.Write((byte)firstPlayer.TargetPlayerId);
                writer.Write((byte)secondPlayer.TargetPlayerId);
                AmongUsClient.Instance.FinishRpcImmediately(writer);

                RPCProcedure.swapperSwap((byte)firstPlayer.TargetPlayerId, (byte)secondPlayer.TargetPlayerId);
                meetingExtraButtonLabel.text = Helpers.cs(Color.green, ModTranslation.GetString("Game-Swapper", 3));
                Swapper.charges--;
                meetingExtraButtonText.text = string.Format(ModTranslation.GetString("Game-Swapper", 4), Swapper.charges);
            }
        }

        public static void swapperCheckAndReturnSwap(MeetingHud __instance, byte dyingPlayerId)
        {
            // someone was guessed or dced in the meeting, check if this affects the swapper.
            if (Swapper.swapper == null || __instance.state == MeetingHud.VoteStates.Results) return;

            // reset swap.
            bool reset = false;
            if (dyingPlayerId == Swapper.playerId1 || dyingPlayerId == Swapper.playerId2)
            {
                reset = true;
                Swapper.playerId1 = Swapper.playerId2 = byte.MaxValue;
            }


            // Only for the swapper: Reset all the buttons and charges value to their original state.
            if (PlayerControl.LocalPlayer != Swapper.swapper) return;


            // check if dying player was a selected player (but not confirmed yet)
            for (int i = 0; i < __instance.playerStates.Count; i++)
            {
                reset = reset || selections[i] && __instance.playerStates[i].TargetPlayerId == dyingPlayerId;
                if (reset) break;
            }

            if (!reset) return;


            for (int i = 0; i < selections.Length; i++)
            {
                selections[i] = false;
                PlayerVoteArea playerVoteArea = __instance.playerStates[i];
                if (playerVoteArea.AmDead || (playerVoteArea.TargetPlayerId == Swapper.swapper.PlayerId && Swapper.canOnlySwapOthers)) continue;
                renderers[i].color = Color.red;
                Swapper.charges++;
                int copyI = i;
                swapperButtonList[i].OnClick.RemoveAllListeners();
                swapperButtonList[i].OnClick.AddListener((System.Action)(() => swapperOnClick(copyI, __instance)));
            }
            meetingExtraButtonText.text = string.Format(ModTranslation.GetString("Game-Swapper", 4), Swapper.charges);
            meetingExtraButtonLabel.text = Helpers.cs(Color.red, ModTranslation.GetString("Game-Swapper", 2));

        }

        static void mayorToggleVoteTwice(MeetingHud __instance)
        {
            __instance.playerStates[0].Cancel();  // This will stop the underlying buttons of the template from showing up
            if (__instance.state == MeetingHud.VoteStates.Results || Mayor.mayor.Data.IsDead) return;
            if (Mayor.mayorChooseSingleVote == 1)
            { // Only accept changes until the mayor voted
                var mayorPVA = __instance.playerStates.FirstOrDefault(x => x.TargetPlayerId == Mayor.mayor.PlayerId);
                if (mayorPVA != null && mayorPVA.DidVote)
                {
                    SoundEffectsManager.play("fail");
                    return;
                }
            }

            Mayor.voteTwice = !Mayor.voteTwice;

            MessageWriter writer = AmongUsClient.Instance.StartRpcImmediately(PlayerControl.LocalPlayer.NetId, (byte)CustomRPC.MayorSetVoteTwice, Hazel.SendOption.Reliable, -1);
            writer.Write(Mayor.voteTwice);
            AmongUsClient.Instance.FinishRpcImmediately(writer);

            meetingExtraButtonLabel.text = Helpers.cs(Mayor.color, $"{ModTranslation.GetString("Game-Mayor", 1)} " + (Mayor.voteTwice ? Helpers.cs(Color.green, ModTranslation.GetString("Opt-General", 70)) : Helpers.cs(Color.red, ModTranslation.GetString("Opt-General", 69))));
        }

        public static GameObject guesserUI;
        public static PassiveButton guesserUIExitButton;
        public static byte guesserCurrentTarget;
        static void guesserOnClick(int buttonTarget, MeetingHud __instance)
        {
            if (guesserUI != null || !(__instance.state == MeetingHud.VoteStates.Voted || __instance.state == MeetingHud.VoteStates.NotVoted)) return;
            __instance.playerStates.ToList().ForEach(x => x.gameObject.SetActive(false));

            Transform PhoneUI = UnityEngine.Object.FindObjectsOfType<Transform>().FirstOrDefault(x => x.name == "PhoneUI");
            Transform container = UnityEngine.Object.Instantiate(PhoneUI, __instance.transform);
            container.transform.localPosition = new Vector3(0, 0, -5f);
            guesserUI = container.gameObject;

            int i = 0;
            var buttonTemplate = __instance.playerStates[0].transform.FindChild("votePlayerBase");
            var maskTemplate = __instance.playerStates[0].transform.FindChild("MaskArea");
            var smallButtonTemplate = __instance.playerStates[0].Buttons.transform.Find("CancelButton");
            var textTemplate = __instance.playerStates[0].NameText;

            guesserCurrentTarget = __instance.playerStates[buttonTarget].TargetPlayerId;

            Transform exitButtonParent = (new GameObject()).transform;
            exitButtonParent.SetParent(container);
            Transform exitButton = UnityEngine.Object.Instantiate(buttonTemplate.transform, exitButtonParent);
            Transform exitButtonMask = UnityEngine.Object.Instantiate(maskTemplate, exitButtonParent);
            exitButton.gameObject.GetComponent<SpriteRenderer>().sprite = smallButtonTemplate.GetComponent<SpriteRenderer>().sprite;
            exitButtonParent.transform.localPosition = new Vector3(2.725f, 2.1f, -5);
            exitButtonParent.transform.localScale = new Vector3(0.217f, 0.9f, 1);
            guesserUIExitButton = exitButton.GetComponent<PassiveButton>();
            guesserUIExitButton.OnClick.RemoveAllListeners();
            guesserUIExitButton.OnClick.AddListener((System.Action)(() =>
            {
                __instance.playerStates.ToList().ForEach(x =>
                {
                    x.gameObject.SetActive(true);
                    if (PlayerControl.LocalPlayer.Data.IsDead && x.transform.FindChild("ShootButton") != null) UnityEngine.Object.Destroy(x.transform.FindChild("ShootButton").gameObject);
                });
                UnityEngine.Object.Destroy(container.gameObject);
            }));

            List<Transform> buttons = new List<Transform>();
            Transform selectedButton = null;

            foreach (RoleInfo roleInfo in RoleInfo.allRoleInfos)
            {
                RoleId guesserRole = (Guesser.niceGuesser != null && PlayerControl.LocalPlayer.PlayerId == Guesser.niceGuesser.PlayerId) ? RoleId.NiceGuesser : RoleId.EvilGuesser;
                if (roleInfo.isModifier || roleInfo.roleId == guesserRole || (!HandleGuesser.evilGuesserCanGuessSpy && guesserRole == RoleId.EvilGuesser && roleInfo.roleId == RoleId.Spy && !HandleGuesser.isGuesserGm)) continue; // Not guessable roles & modifier
                if (HandleGuesser.isGuesserGm && (roleInfo.roleId == RoleId.NiceGuesser || roleInfo.roleId == RoleId.EvilGuesser)) continue; // remove Guesser for guesser game mode
                if (HandleGuesser.isGuesserGm && PlayerControl.LocalPlayer.Data.Role.IsImpostor && !HandleGuesser.evilGuesserCanGuessSpy && roleInfo.roleId == RoleId.Spy) continue;
                // remove all roles that cannot spawn due to the settings from the ui.
                RoleManagerSelectRolesPatch.RoleAssignmentData roleData = RoleManagerSelectRolesPatch.getRoleAssignmentData();
                if (roleData.neutralSettings.ContainsKey((byte)roleInfo.roleId) && roleData.neutralSettings[(byte)roleInfo.roleId] == 0) continue;
                else if (roleData.impSettings.ContainsKey((byte)roleInfo.roleId) && roleData.impSettings[(byte)roleInfo.roleId] == 0) continue;
                else if (roleData.crewSettings.ContainsKey((byte)roleInfo.roleId) && roleData.crewSettings[(byte)roleInfo.roleId] == 0) continue;
                else if (new List<RoleId>() { RoleId.Janitor, RoleId.Godfather, RoleId.Mafioso }.Contains(roleInfo.roleId) && (CustomOptionHolder.mafiaSpawnRate.getSelection() == 0 || GameOptionsManager.Instance.currentGameOptions.NumImpostors < 3)) continue;
                else if (roleInfo.roleId == RoleId.Sidekick && (!CustomOptionHolder.jackalCanCreateSidekick.getBool() || CustomOptionHolder.jackalSpawnRate.getSelection() == 0)) continue;
                if (roleInfo.roleId == RoleId.Deputy && (CustomOptionHolder.deputySpawnRate.getSelection() == 0 || CustomOptionHolder.sheriffSpawnRate.getSelection() == 0)) continue;
                if (roleInfo.roleId == RoleId.Pursuer && CustomOptionHolder.lawyerSpawnRate.getSelection() == 0) continue;
                if (roleInfo.roleId == RoleId.Spy && roleData.impostors.Count <= 1) continue;
                if (roleInfo.roleId == RoleId.Prosecutor && (CustomOptionHolder.lawyerIsProsecutorChance.getSelection() == 0 || CustomOptionHolder.lawyerSpawnRate.getSelection() == 0)) continue;
                if (roleInfo.roleId == RoleId.Lawyer && (CustomOptionHolder.lawyerIsProsecutorChance.getSelection() == 10 || CustomOptionHolder.lawyerSpawnRate.getSelection() == 0)) continue;
                if (Snitch.snitch != null && HandleGuesser.guesserCantGuessSnitch)
                {
                    var (playerCompleted, playerTotal) = TasksHandler.taskInfo(Snitch.snitch.Data);
                    int numberOfLeftTasks = playerTotal - playerCompleted;
                    if (numberOfLeftTasks <= 0 && roleInfo.roleId == RoleId.Snitch) continue;
                }

                Transform buttonParent = (new GameObject()).transform;
                buttonParent.SetParent(container);
                Transform button = UnityEngine.Object.Instantiate(buttonTemplate, buttonParent);
                Transform buttonMask = UnityEngine.Object.Instantiate(maskTemplate, buttonParent);
                TMPro.TextMeshPro label = UnityEngine.Object.Instantiate(textTemplate, button);
                button.GetComponent<SpriteRenderer>().sprite = ShipStatus.Instance.CosmeticsCache.GetNameplate("nameplate_NoPlate").Image;
                buttons.Add(button);
                int row = i / 5, col = i % 5;
                buttonParent.localPosition = new Vector3(-3.47f + 1.75f * col, 1.5f - 0.45f * row, -5);
                buttonParent.localScale = new Vector3(0.55f, 0.55f, 1f);
                label.text = Helpers.cs(roleInfo.color, roleInfo.name);
                label.alignment = TMPro.TextAlignmentOptions.Center;
                label.transform.localPosition = new Vector3(0, 0, label.transform.localPosition.z);
                label.transform.localScale *= 1.7f;
                int copiedIndex = i;

                button.GetComponent<PassiveButton>().OnClick.RemoveAllListeners();
                if (!PlayerControl.LocalPlayer.Data.IsDead && !Helpers.playerById((byte)__instance.playerStates[buttonTarget].TargetPlayerId).Data.IsDead) button.GetComponent<PassiveButton>().OnClick.AddListener((System.Action)(() =>
                {
                    if (selectedButton != button)
                    {
                        selectedButton = button;
                        buttons.ForEach(x => x.GetComponent<SpriteRenderer>().color = x == selectedButton ? Color.red : Color.white);
                    }
                    else
                    {
                        PlayerControl focusedTarget = Helpers.playerById((byte)__instance.playerStates[buttonTarget].TargetPlayerId);
                        if (!(__instance.state == MeetingHud.VoteStates.Voted || __instance.state == MeetingHud.VoteStates.NotVoted) || focusedTarget == null || HandleGuesser.remainingShots(PlayerControl.LocalPlayer.PlayerId) <= 0) return;

                        if (!HandleGuesser.killsThroughShield && focusedTarget == Medic.shielded)
                        { // Depending on the options, shooting the shielded player will not allow the guess, notifiy everyone about the kill attempt and close the window
                            __instance.playerStates.ToList().ForEach(x => x.gameObject.SetActive(true));
                            UnityEngine.Object.Destroy(container.gameObject);

                            MessageWriter murderAttemptWriter = AmongUsClient.Instance.StartRpcImmediately(PlayerControl.LocalPlayer.NetId, (byte)CustomRPC.ShieldedMurderAttempt, Hazel.SendOption.Reliable, -1);
                            AmongUsClient.Instance.FinishRpcImmediately(murderAttemptWriter);
                            RPCProcedure.shieldedMurderAttempt();
                            SoundEffectsManager.play("fail");
                            return;
                        }

                        var mainRoleInfo = RoleInfo.getRoleInfoForPlayer(focusedTarget, false).FirstOrDefault();
                        if (mainRoleInfo == null) return;

                        PlayerControl dyingTarget = (mainRoleInfo == roleInfo) ? focusedTarget : PlayerControl.LocalPlayer;

                        // Reset the GUI
                        __instance.playerStates.ToList().ForEach(x => x.gameObject.SetActive(true));
                        UnityEngine.Object.Destroy(container.gameObject);
                        if (HandleGuesser.hasMultipleShotsPerMeeting && HandleGuesser.remainingShots(PlayerControl.LocalPlayer.PlayerId) > 1 && dyingTarget != PlayerControl.LocalPlayer)
                            __instance.playerStates.ToList().ForEach(x => { if (x.TargetPlayerId == dyingTarget.PlayerId && x.transform.FindChild("ShootButton") != null) UnityEngine.Object.Destroy(x.transform.FindChild("ShootButton").gameObject); });
                        else
                            __instance.playerStates.ToList().ForEach(x => { if (x.transform.FindChild("ShootButton") != null) UnityEngine.Object.Destroy(x.transform.FindChild("ShootButton").gameObject); });

                        // Shoot player and send chat info if activated
                        MessageWriter writer = AmongUsClient.Instance.StartRpcImmediately(PlayerControl.LocalPlayer.NetId, (byte)CustomRPC.GuesserShoot, Hazel.SendOption.Reliable, -1);
                        writer.Write(PlayerControl.LocalPlayer.PlayerId);
                        writer.Write(dyingTarget.PlayerId);
                        writer.Write(focusedTarget.PlayerId);
                        writer.Write((byte)roleInfo.roleId);
                        AmongUsClient.Instance.FinishRpcImmediately(writer);
                        RPCProcedure.guesserShoot(PlayerControl.LocalPlayer.PlayerId, dyingTarget.PlayerId, focusedTarget.PlayerId, (byte)roleInfo.roleId);
                    }
                }));

                i++;
            }
            container.transform.localScale *= 0.75f;
        }

        static void yasunaOnClick(int buttonTarget, MeetingHud __instance)
        {
            if (Yasuna.yasuna != null && (Yasuna.yasuna.Data.IsDead || Yasuna.specialVoteTargetPlayerId != byte.MaxValue)) return;
            if (!(__instance.state == MeetingHud.VoteStates.Voted || __instance.state == MeetingHud.VoteStates.NotVoted || __instance.state == MeetingHud.VoteStates.Results)) return;
            if (__instance.playerStates[buttonTarget].AmDead) return;

            byte targetId = __instance.playerStates[buttonTarget].TargetPlayerId;
            RPCProcedure.yasunaSpecialVote(PlayerControl.LocalPlayer.PlayerId, targetId);
            MessageWriter writer = AmongUsClient.Instance.StartRpcImmediately(PlayerControl.LocalPlayer.NetId, (byte)CustomRPC.YasunaSpecialVote, Hazel.SendOption.Reliable, -1);
            writer.Write(PlayerControl.LocalPlayer.PlayerId);
            writer.Write(targetId);
            AmongUsClient.Instance.FinishRpcImmediately(writer);

            __instance.SkipVoteButton.gameObject.SetActive(false);
            for (int i = 0; i < __instance.playerStates.Length; i++)
            {
                PlayerVoteArea voteArea = __instance.playerStates[i];
                voteArea.ClearButtons();
                Transform t = voteArea.transform.FindChild("SpecialVoteButton");
                if (t != null && voteArea.TargetPlayerId != targetId)
                    t.gameObject.SetActive(false);
            }
            if (AmongUsClient.Instance.AmHost)
            {
                PlayerControl target = Helpers.playerById(targetId);
                if (target != null)
                    MeetingHud.Instance.CmdCastVote(PlayerControl.LocalPlayer.PlayerId, target.PlayerId);
            }
        }

        static void yasunaJrOnClick(int buttonTarget, MeetingHud __instance)
        {
            if (YasunaJr.yasunaJr != null && YasunaJr.yasunaJr.Data.IsDead) return;
            if (!(__instance.state == MeetingHud.VoteStates.Voted || __instance.state == MeetingHud.VoteStates.NotVoted)) return;
            if (__instance.playerStates[buttonTarget].AmDead) return;
            SoundManager.Instance.PlaySound(AccountManager.Instance.accountTab.resendEmailButton.GetComponent<PassiveButton>().ClickSound, false, 1f, null).volume = 0.8f;
            for (int i = 0; i < __instance.playerStates.Length; i++)
            {
                PlayerVoteArea voteArea = __instance.playerStates[i];
                Transform t = voteArea.transform.FindChild("SpecialVoteButton2");
                if (t != null)
                {
                    var s = t.gameObject.GetComponent<SpriteRenderer>();
                    if (s != null)
                        s.color = new Color(s.color.r, s.color.g, s.color.b, i == buttonTarget ? 1.0f : 0.5f);
                }
            }

            byte targetId = __instance.playerStates[buttonTarget].TargetPlayerId;
            RPCProcedure.yasunaJrSpecialVote(PlayerControl.LocalPlayer.PlayerId, targetId);
            MessageWriter writer = AmongUsClient.Instance.StartRpcImmediately(PlayerControl.LocalPlayer.NetId, (byte)CustomRPC.YasunaJrSpecialVote, Hazel.SendOption.Reliable, -1);
            writer.Write(PlayerControl.LocalPlayer.PlayerId);
            writer.Write(targetId);
            AmongUsClient.Instance.FinishRpcImmediately(writer);

            /*
            __instance.SkipVoteButton.gameObject.SetActive(false);
            for (int i = 0; i < __instance.playerStates.Length; i++)
            {
                PlayerVoteArea voteArea = __instance.playerStates[i];
                voteArea.ClearButtons();
                Transform t = voteArea.transform.FindChild("SpecialVoteButton2");
                if (t != null && voteArea.TargetPlayerId != targetId)
                    t.gameObject.SetActive(false);
            }
            */

            /*
            if (AmongUsClient.Instance.AmHost)
            {
                PlayerControl target = Helpers.playerById(targetId);
                if (target != null)
                    MeetingHud.Instance.CmdCastVote(PlayerControl.LocalPlayer.PlayerId, target.PlayerId);
            }
            */
        }

        [HarmonyPatch(typeof(PlayerVoteArea), nameof(PlayerVoteArea.Select))]
        class PlayerVoteAreaSelectPatch
        {
            static bool Prefix(MeetingHud __instance)
            {
                if (PlayerControl.LocalPlayer != null)
                {
                    if (HandleGuesser.isGuesser(PlayerControl.LocalPlayer.PlayerId) && guesserUI != null)
                        return false;
                    if (Yasuna.isYasuna(PlayerControl.LocalPlayer.PlayerId) && Yasuna.specialVoteTargetPlayerId != byte.MaxValue)
                        return false;
                }

                return true;
            }
        }

        static void populateButtonsPostfix(MeetingHud __instance)
        {
            // Add Swapper Buttons
            bool addSwapperButtons = Swapper.swapper != null && PlayerControl.LocalPlayer == Swapper.swapper && !Swapper.swapper.Data.IsDead;
            bool addMayorButton = Mayor.mayor != null && PlayerControl.LocalPlayer == Mayor.mayor && !Mayor.mayor.Data.IsDead && Mayor.mayorChooseSingleVote > 0;
            if (addSwapperButtons)
            {
                selections = new bool[__instance.playerStates.Length];
                renderers = new SpriteRenderer[__instance.playerStates.Length];
                swapperButtonList = new PassiveButton[__instance.playerStates.Length];

                for (int i = 0; i < __instance.playerStates.Length; i++)
                {
                    PlayerVoteArea playerVoteArea = __instance.playerStates[i];
                    if (playerVoteArea.AmDead || (playerVoteArea.TargetPlayerId == Swapper.swapper.PlayerId && Swapper.canOnlySwapOthers)) continue;

                    GameObject template = playerVoteArea.Buttons.transform.Find("CancelButton").gameObject;
                    GameObject checkbox = UnityEngine.Object.Instantiate(template);
                    checkbox.transform.SetParent(playerVoteArea.transform);
                    checkbox.transform.position = template.transform.position;
                    checkbox.transform.localPosition = new Vector3(-0.95f, 0.03f, -1.3f);
                    if (HandleGuesser.isGuesserGm && HandleGuesser.isGuesser(PlayerControl.LocalPlayer.PlayerId)) checkbox.transform.localPosition = new Vector3(-0.5f, 0.03f, -1.3f);
                    SpriteRenderer renderer = checkbox.GetComponent<SpriteRenderer>();
                    renderer.sprite = Swapper.getCheckSprite();
                    renderer.color = Color.red;

                    if (Swapper.charges <= 0) renderer.color = Color.gray;

                    PassiveButton button = checkbox.GetComponent<PassiveButton>();
                    swapperButtonList[i] = button;
                    button.OnClick.RemoveAllListeners();
                    int copiedIndex = i;
                    button.OnClick.AddListener((System.Action)(() => swapperOnClick(copiedIndex, __instance)));

                    selections[i] = false;
                    renderers[i] = renderer;
                }
            }
            // Add meeting extra button, i.e. Swapper Confirm Button or Mayor Toggle Double Vote Button. Swapper Button uses ExtraButtonText on the Left of the Button. (Future meeting buttons can easily be added here)
            if (addSwapperButtons || addMayorButton)
            {
                Transform meetingUI = UnityEngine.Object.FindObjectsOfType<Transform>().FirstOrDefault(x => x.name == "PhoneUI");

                var buttonTemplate = __instance.playerStates[0].transform.FindChild("votePlayerBase");
                var maskTemplate = __instance.playerStates[0].transform.FindChild("MaskArea");
                var textTemplate = __instance.playerStates[0].NameText;
                Transform meetingExtraButtonParent = (new GameObject()).transform;
                meetingExtraButtonParent.SetParent(meetingUI);
                Transform meetingExtraButton = UnityEngine.Object.Instantiate(buttonTemplate, meetingExtraButtonParent);

                Transform infoTransform = __instance.playerStates[0].NameText.transform.parent.FindChild("Info");
                TMPro.TextMeshPro meetingInfo = infoTransform != null ? infoTransform.GetComponent<TMPro.TextMeshPro>() : null;
                meetingExtraButtonText = UnityEngine.Object.Instantiate(__instance.playerStates[0].NameText, meetingExtraButtonParent);
                meetingExtraButtonText.text = addSwapperButtons ? string.Format(ModTranslation.GetString("Game-Swapper", 4), Swapper.charges) : "";
                meetingExtraButtonText.enableWordWrapping = false;
                meetingExtraButtonText.transform.localScale = Vector3.one * 1.7f;
                meetingExtraButtonText.transform.localPosition = new Vector3(-2.5f, 0f, 0f);

                Transform meetingExtraButtonMask = UnityEngine.Object.Instantiate(maskTemplate, meetingExtraButtonParent);
                meetingExtraButtonLabel = UnityEngine.Object.Instantiate(textTemplate, meetingExtraButton);
                meetingExtraButton.GetComponent<SpriteRenderer>().sprite = ShipStatus.Instance.CosmeticsCache.GetNameplate("nameplate_NoPlate").Image;
                meetingExtraButtonParent.localPosition = new Vector3(0, -2.225f, -5);
                meetingExtraButtonParent.localScale = new Vector3(0.55f, 0.55f, 1f);
                meetingExtraButtonLabel.alignment = TMPro.TextAlignmentOptions.Center;
                meetingExtraButtonLabel.transform.localPosition = new Vector3(0, 0, meetingExtraButtonLabel.transform.localPosition.z);
                if (addSwapperButtons)
                {
                    meetingExtraButtonLabel.transform.localScale *= 1.7f;
                    meetingExtraButtonLabel.text = Helpers.cs(Color.red, ModTranslation.GetString("Game-Swapper", 2));
                }
                else if (addMayorButton)
                {
                    meetingExtraButtonLabel.transform.localScale = new Vector3(meetingExtraButtonLabel.transform.localScale.x * 1.5f, meetingExtraButtonLabel.transform.localScale.x * 1.7f, meetingExtraButtonLabel.transform.localScale.x * 1.7f);
                    meetingExtraButtonLabel.text = Helpers.cs(Mayor.color, $"{ModTranslation.GetString("Game-Mayor", 1)} " + (Mayor.voteTwice ? Helpers.cs(Color.green, "On ") : Helpers.cs(Color.red, "Off")));
                }
                PassiveButton passiveButton = meetingExtraButton.GetComponent<PassiveButton>();
                passiveButton.OnClick.RemoveAllListeners();
                if (!PlayerControl.LocalPlayer.Data.IsDead)
                {
                    if (addSwapperButtons)
                        passiveButton.OnClick.AddListener((Action)(() => swapperConfirm(__instance)));
                    else if (addMayorButton)
                        passiveButton.OnClick.AddListener((Action)(() => mayorToggleVoteTwice(__instance)));
                }
                meetingExtraButton.parent.gameObject.SetActive(false);
                __instance.StartCoroutine(Effects.Lerp(7.27f, new Action<float>((p) =>
                { // Button appears delayed, so that its visible in the voting screen only!
                    if (p == 1f)
                    {
                        meetingExtraButton.parent.gameObject.SetActive(true);
                    }
                })));
            }

            bool isGuesser = HandleGuesser.isGuesser(PlayerControl.LocalPlayer.PlayerId);

            // Add overlay for spelled players
            if (Witch.witch != null && Witch.futureSpelled != null)
            {
                foreach (PlayerVoteArea pva in __instance.playerStates)
                {
                    if (Witch.futureSpelled.Any(x => x.PlayerId == pva.TargetPlayerId))
                    {
                        SpriteRenderer rend = (new GameObject()).AddComponent<SpriteRenderer>();
                        rend.transform.SetParent(pva.transform);
                        rend.gameObject.layer = pva.Megaphone.gameObject.layer;
                        rend.transform.localPosition = new Vector3(-0.5f, -0.03f, -1f);
                        if (PlayerControl.LocalPlayer == Swapper.swapper && isGuesser) rend.transform.localPosition = new Vector3(-0.725f, -0.15f, -1f);
                        rend.sprite = Witch.getSpelledOverlaySprite();
                    }
                }
            }

            // Add Guesser Buttons
            int remainingShots = HandleGuesser.remainingShots(PlayerControl.LocalPlayer.PlayerId);
            var (playerCompleted, playerTotal) = TasksHandler.taskInfo(PlayerControl.LocalPlayer.Data);

            if (isGuesser && !PlayerControl.LocalPlayer.Data.IsDead && remainingShots > 0)
            {
                for (int i = 0; i < __instance.playerStates.Length; i++)
                {
                    PlayerVoteArea playerVoteArea = __instance.playerStates[i];
                    if (playerVoteArea.AmDead || playerVoteArea.TargetPlayerId == PlayerControl.LocalPlayer.PlayerId) continue;
                    if (PlayerControl.LocalPlayer != null && PlayerControl.LocalPlayer == Eraser.eraser && Eraser.alreadyErased.Contains(playerVoteArea.TargetPlayerId)) continue;
                    if (PlayerControl.LocalPlayer != null && !Helpers.isEvil(PlayerControl.LocalPlayer) && playerCompleted < HandleGuesser.tasksToUnlock) continue;

                    GameObject template = playerVoteArea.Buttons.transform.Find("CancelButton").gameObject;
                    GameObject targetBox = UnityEngine.Object.Instantiate(template, playerVoteArea.transform);
                    targetBox.name = "ShootButton";
                    targetBox.transform.localPosition = new Vector3(-0.95f, 0.03f, -1.3f);
                    SpriteRenderer renderer = targetBox.GetComponent<SpriteRenderer>();
                    renderer.sprite = HandleGuesser.getTargetSprite();
                    PassiveButton button = targetBox.GetComponent<PassiveButton>();
                    button.OnClick.RemoveAllListeners();
                    int copiedIndex = i;
                    button.OnClick.AddListener((System.Action)(() => guesserOnClick(copiedIndex, __instance)));
                }
            }

            // Add Yasuna Special Buttons
            if (Yasuna.isYasuna(PlayerControl.LocalPlayer.PlayerId) && !Yasuna.yasuna.Data.IsDead && Yasuna.remainingSpecialVotes() > 0)
            {
                for (int i = 0; i < __instance.playerStates.Length; i++)
                {
                    PlayerVoteArea playerVoteArea = __instance.playerStates[i];
                    if (playerVoteArea.AmDead || playerVoteArea.TargetPlayerId == PlayerControl.LocalPlayer.PlayerId) continue;

                    GameObject template = playerVoteArea.Buttons.transform.Find("CancelButton").gameObject;
                    GameObject targetBox = UnityEngine.Object.Instantiate(template, playerVoteArea.transform);
                    targetBox.name = "SpecialVoteButton";
                    targetBox.transform.localPosition = new Vector3(-0.95f, 0.03f, -2.5f);
                    SpriteRenderer renderer = targetBox.GetComponent<SpriteRenderer>();
                    renderer.sprite = Yasuna.getTargetSprite(PlayerControl.LocalPlayer.Data.Role.IsImpostor);
                    PassiveButton button = targetBox.GetComponent<PassiveButton>();
                    button.OnClick.RemoveAllListeners();
                    int copiedIndex = i;
                    button.OnClick.AddListener((UnityEngine.Events.UnityAction)(() => yasunaOnClick(copiedIndex, __instance)));

                    TMPro.TextMeshPro targetBoxRemainText = UnityEngine.Object.Instantiate(__instance.playerStates[0].NameText, targetBox.transform);
                    targetBoxRemainText.text = Yasuna.remainingSpecialVotes().ToString();
                    targetBoxRemainText.color = PlayerControl.LocalPlayer.Data.Role.IsImpostor ? Palette.ImpostorRed : Yasuna.color;
                    targetBoxRemainText.alignment = TMPro.TextAlignmentOptions.Center;
                    targetBoxRemainText.transform.localPosition = new Vector3(0.2f, -0.3f, targetBoxRemainText.transform.localPosition.z);
                    targetBoxRemainText.transform.localScale *= 1.7f;
                }
            }

            // Add Yasuna Jr. Special Buttons
            if (YasunaJr.isYasunaJr(PlayerControl.LocalPlayer.PlayerId) && !YasunaJr.yasunaJr.Data.IsDead && YasunaJr.remainingSpecialVotes() > 0)
            {
                for (int i = 0; i < __instance.playerStates.Length; i++)
                {
                    PlayerVoteArea playerVoteArea = __instance.playerStates[i];
                    if (playerVoteArea.AmDead || playerVoteArea.TargetPlayerId == PlayerControl.LocalPlayer.PlayerId) continue;

                    GameObject template = playerVoteArea.Buttons.transform.Find("CancelButton").gameObject;
                    GameObject targetBox = UnityEngine.Object.Instantiate(template, playerVoteArea.transform);
                    targetBox.name = "SpecialVoteButton2";
                    targetBox.transform.localPosition = new Vector3(-0.95f, 0.03f, -2.5f);
                    SpriteRenderer renderer = targetBox.GetComponent<SpriteRenderer>();
                    renderer.sprite = YasunaJr.getTargetSprite(PlayerControl.LocalPlayer.Data.Role.IsImpostor);
                    renderer.color = new Color(renderer.color.r, renderer.color.g, renderer.color.b, 0.5f);
                    PassiveButton button = targetBox.GetComponent<PassiveButton>();
                    button.OnClick.RemoveAllListeners();
                    int copiedIndex = i;
                    button.OnClick.AddListener((UnityEngine.Events.UnityAction)(() => yasunaJrOnClick(copiedIndex, __instance)));

                    TMPro.TextMeshPro targetBoxRemainText = UnityEngine.Object.Instantiate(__instance.playerStates[0].NameText, targetBox.transform);
                    targetBoxRemainText.text = YasunaJr.remainingSpecialVotes().ToString();
                    targetBoxRemainText.color = YasunaJr.color;
                    targetBoxRemainText.alignment = TMPro.TextAlignmentOptions.Center;
                    targetBoxRemainText.transform.localPosition = new Vector3(0.2f, -0.3f, targetBoxRemainText.transform.localPosition.z);
                    targetBoxRemainText.transform.localScale *= 1.7f;
                }
            }
        }

        [HarmonyPatch(typeof(MeetingHud), nameof(MeetingHud.ServerStart))]
        class MeetingServerStartPatch
        {
            static void Postfix(MeetingHud __instance)
            {
                //populateButtonsPostfix(__instance);
            }
        }

        [HarmonyPatch(typeof(MeetingHud), nameof(MeetingHud.Deserialize))]
        class MeetingDeserializePatch
        {
            static void Postfix(MeetingHud __instance, [HarmonyArgument(0)] MessageReader reader, [HarmonyArgument(1)] bool initialState)
            {
                // Add swapper buttons
                //if (initialState) {
                //	populateButtonsPostfix(__instance);
                //}
            }
        }

        [HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.StartMeeting))]
        class StartMeetingPatch
        {
            public static bool Prefix(PlayerControl __instance, [HarmonyArgument(0)] NetworkedPlayerInfo meetingTarget)
            {
                {
                    RoomTracker roomTracker = FastDestroyableSingleton<HudManager>.Instance?.roomTracker;
                    byte roomId = Byte.MinValue;
                    if (roomTracker != null && roomTracker.LastRoom != null)
                    {
                        roomId = (byte)roomTracker.LastRoom?.RoomId;
                    }
                    if (Snitch.snitch != null && roomTracker != null)
                    {
                        MessageWriter roomWriter = AmongUsClient.Instance.StartRpcImmediately(PlayerControl.LocalPlayer.NetId, (byte)CustomRPC.ShareRoom, Hazel.SendOption.Reliable, -1);
                        roomWriter.Write(PlayerControl.LocalPlayer.PlayerId);
                        roomWriter.Write(roomId);
                        AmongUsClient.Instance.FinishRpcImmediately(roomWriter);
                    }
                    // Reset Bait list
                    Bait.active = new Dictionary<DeadPlayer, float>();
                    // Save AntiTeleport position, if the player is able to move (i.e. not on a ladder or a gap thingy)
                    if (PlayerControl.LocalPlayer.MyPhysics.enabled && (PlayerControl.LocalPlayer.moveable || PlayerControl.LocalPlayer.inVent
                        || HudManagerStartPatch.hackerVitalsButton.isEffectActive || HudManagerStartPatch.hackerAdminTableButton.isEffectActive || HudManagerStartPatch.securityGuardCamButton.isEffectActive
                    || Portal.isTeleporting && Portal.teleportedPlayers.Last().playerId == PlayerControl.LocalPlayer.PlayerId))
                    {
                        if (!PlayerControl.LocalPlayer.inMovingPlat)
                            AntiTeleport.position = PlayerControl.LocalPlayer.transform.position;
                    }

                    // Medium meeting start time
                    Medium.meetingStartTime = DateTime.UtcNow;
                    // Mini
                    Mini.timeOfMeetingStart = DateTime.UtcNow;
                    Mini.ageOnMeetingStart = Mathf.FloorToInt(Mini.growingProgress() * 18);
                    // Reset vampire bitten
                    Vampire.bitten = null;
                    // Count meetings
                    if (meetingTarget == null) meetingsCount++;
                    // Save the meeting target
                    target = meetingTarget;

                    // Add Portal info into Portalmaker Chat:
                    if (Portalmaker.portalmaker != null && (PlayerControl.LocalPlayer == Portalmaker.portalmaker || Helpers.shouldShowGhostInfo()) && !Portalmaker.portalmaker.Data.IsDead)
                    {
                        if (Portal.teleportedPlayers.Count > 0)
                        {
                            string msg = ModTranslation.GetString("Game-Portalmaker", 3);
                            foreach (var entry in Portal.teleportedPlayers)
                            {
                                float timeBeforeMeeting = ((float)(DateTime.UtcNow - entry.time).TotalMilliseconds) / 1000;
                                msg += Portalmaker.logShowsTime ? string.Format(ModTranslation.GetString("Game-Portalmaker", 1), (int)timeBeforeMeeting) : "";
                                msg = msg + string.Format(ModTranslation.GetString("Game-Portalmaker", 2), entry.name);
                            }
                            FastDestroyableSingleton<HudManager>.Instance.Chat.AddChat(Portalmaker.portalmaker, $"{msg}");
                        }
                    }

                    // Add trapped Info into Trapper chat
                    if (Trapper.trapper != null && (PlayerControl.LocalPlayer == Trapper.trapper || Helpers.shouldShowGhostInfo()) && !Trapper.trapper.Data.IsDead)
                    {
                        if (Trap.traps.Any(x => x.revealed))
                            FastDestroyableSingleton<HudManager>.Instance.Chat.AddChat(Trapper.trapper, ModTranslation.GetString("Game-Trapper", 4));
                        foreach (Trap trap in Trap.traps)
                        {
                            if (!trap.revealed) continue;
                            string message = string.Format(ModTranslation.GetString("Game-Trapper", 1), trap.instanceId);
                            trap.trappedPlayer = trap.trappedPlayer.OrderBy(x => rnd.Next()).ToList();
                            foreach (byte playerId in trap.trappedPlayer)
                            {
                                PlayerControl p = Helpers.playerById(playerId);
                                if (Trapper.infoType == 0) message += RoleInfo.GetRolesString(p, false, false, true) + "\n";
                                else if (Trapper.infoType == 1)
                                {
                                    if (Helpers.isNeutral(p) || p.Data.Role.IsImpostor) message += ModTranslation.GetString("Game-Trapper", 2);
                                    else message += ModTranslation.GetString("Game-Trapper", 3);
                                }
                                else message += p.Data.PlayerName + "\n";
                            }
                            FastDestroyableSingleton<HudManager>.Instance.Chat.AddChat(PlayerControl.LocalPlayer, $"{message}");
                        }
                    }

                    Trapper.playersOnMap = new ();

                    // Remove revealed traps
                    Trap.clearRevealedTraps();

                    // Reset zoomed out ghosts
                    Helpers.toggleZoom(reset: true);

                    // Stop all playing sounds
                    SoundEffectsManager.stopAll();

                    Bomber.clearBomb();

                    // Close In-Game Settings Display if open
                    HudManagerUpdate.CloseSettings();
                }

                {
                    bool flag = target == null;
                    DestroyableSingleton<UnityTelemetry>.Instance.WriteMeetingStarted(flag);
                    StartMeeting(__instance, target);
                    if (__instance.AmOwner)
                    {
                        if (flag)
                        {
                            __instance.RemainingEmergencies--;
                            StatsManager.Instance.IncrementStat(StringNames.StatsEmergenciesCalled);
                            return false;
                        }
                        StatsManager.Instance.IncrementStat(StringNames.StatsBodiesReported);
                    }
                }

                return false;
            }

            static void StartMeeting(PlayerControl reporter, NetworkedPlayerInfo target)
            {
                MapUtilities.CachedShipStatus.StartCoroutine(CoStartMeeting(reporter, target).WrapToIl2Cpp());
            }

            static IEnumerator CoStartMeeting(PlayerControl reporter, NetworkedPlayerInfo target)
            {
                {
                    while (!MeetingHud.Instance)
                    {
                        yield return null;
                    }
                    MeetingRoomManager.Instance.RemoveSelf();
                    foreach (var p in PlayerControl.AllPlayerControls)
                    {
                        if (p != null)
                            p.ResetForMeeting();
                    }
                    if (MapBehaviour.Instance)
                    {
                        MapBehaviour.Instance.Close();
                    }
                    if (Minigame.Instance)
                    {
                        Minigame.Instance.ForceClose();
                    }
                    MapUtilities.CachedShipStatus.OnMeetingCalled();
                    KillAnimation.SetMovement(reporter, true);
                }

                DestroyableSingleton<HudManager>._instance.StartCoroutine(CoStartMeeting2(reporter, target).WrapToIl2Cpp());
                yield break;
            }

            static IEnumerator CoStartMeeting2(PlayerControl reporter, NetworkedPlayerInfo target)
            {
                SpriteRenderer blackscreen = null;
                {
                    MeetingHud.Instance.state = MeetingHud.VoteStates.Animating;
                    HudManager hudManager = DestroyableSingleton<HudManager>.Instance;
                    blackscreen = UnityEngine.Object.Instantiate(hudManager.FullScreen, hudManager.transform);
                    var greyscreen = UnityEngine.Object.Instantiate(hudManager.FullScreen, hudManager.transform);
                    blackscreen.color = Palette.Black;
                    blackscreen.transform.position = Vector3.zero;
                    blackscreen.transform.localPosition = new Vector3(0f, 0f, -910f);
                    blackscreen.transform.localScale = new Vector3(10f, 10f, 1f);
                    blackscreen.gameObject.SetActive(true);
                    blackscreen.enabled = true;
                    greyscreen.color = Palette.Black;
                    greyscreen.transform.position = Vector3.zero;
                    greyscreen.transform.localPosition = new Vector3(0f, 0f, -920f);
                    greyscreen.transform.localScale = new Vector3(10f, 10f, 1f);
                    greyscreen.gameObject.SetActive(true);
                    greyscreen.enabled = true;
                    TMPro.TMP_Text text;
                    RoomTracker roomTracker = FastDestroyableSingleton<HudManager>.Instance?.roomTracker;
                    var textObj = UnityEngine.Object.Instantiate(roomTracker.gameObject);
                    UnityEngine.Object.DestroyImmediate(textObj.GetComponent<RoomTracker>());
                    textObj.transform.SetParent(FastDestroyableSingleton<HudManager>.Instance.transform);
                    textObj.transform.localPosition = new Vector3(0, 0, -930f);
                    textObj.transform.localScale = Vector3.one * 5f;
                    text = textObj.GetComponent<TMPro.TMP_Text>();
                    yield return Effects.Lerp(delay, new Action<float>((p) =>
                    { // Delayed action
                        greyscreen.color = new Color(1.0f, 1.0f, 1.0f, 0.5f - p / 2);
                        string message = (delay - (p * delay)).ToString("0.00");
                        if (message == "0") return;
                        string prefix = "<color=#FFFF00FF>";
                        text.text = ModTranslation.GetString("Game-General", 13) + prefix + message + "</color>";
                        if (text != null) text.color = Color.white;
                    }));

                    text.enabled = false;
                    blackscreen.transform.SetParent(MeetingHud.Instance.transform);
                    blackscreen.transform.position = Vector3.zero;
                    blackscreen.transform.localPosition = new Vector3(0f, 0f, 9f);
                    blackscreen.transform.localScale = new Vector3(200f, 100f, 1f);
                    blackscreen.transform.SetAsFirstSibling();
                    greyscreen.transform.SetParent(MeetingHud.Instance.transform);
                    greyscreen.transform.position = Vector3.zero;
                    greyscreen.transform.localPosition = new Vector3(0f, 0f, 9f);
                    greyscreen.transform.localScale = new Vector3(200f, 100f, 1f);
                    greyscreen.transform.SetAsFirstSibling();
                    // yield return new WaitForSeconds(2f);

                    //UnityEngine.Object.Destroy(textObj);
                    //UnityEngine.Object.Destroy(blackscreen);
                    //UnityEngine.Object.Destroy(greyscreen);

                    //populateButtons(MeetingHud.Instance, reporter.Data.PlayerId);
                    populateButtonsPostfix(MeetingHud.Instance);
                }

                {
                    DeadBody[] array = UnityEngine.Object.FindObjectsOfType<DeadBody>();
                    NetworkedPlayerInfo[] deadBodies = (from b in array
                                                        select GameData.Instance.GetPlayerById(b.ParentId)).ToArray<NetworkedPlayerInfo>();
                    for (int j = 0; j < array.Length; j++)
                    {
                        if (array[j] != null && array[j].gameObject != null)
                        {
                            UnityEngine.Object.Destroy(array[j].gameObject);
                        }
                        else
                        {
                            Debug.LogError("Encountered a null Dead Body while destroying.");
                        }
                    }
                    ShapeshifterEvidence[] array2 = UnityEngine.Object.FindObjectsOfType<ShapeshifterEvidence>();
                    for (int k = 0; k < array2.Length; k++)
                    {
                        if (array2[k] != null && array2[k].gameObject != null)
                        {
                            UnityEngine.Object.Destroy(array2[k].gameObject);
                        }
                        else
                        {
                            Debug.LogError("Encountered a null Evidence while destroying.");
                        }
                    }
                    MeetingHud.Instance.StartCoroutine(MeetingHud.Instance.CoIntro(reporter.Data, target, deadBodies));
                }

                if (blackscreen != null)
                {
                    yield return Effects.Lerp(30.0f, new Action<float>((p) =>
                    {
                        if (blackscreen != null)
                            blackscreen.color = new Color(0.0f, 0.0f, 0.0f, 1.0f - p);
                    }));
                }
            }

            static float delay { get { return CustomOptionHolder.delayBeforeMeeting.getFloat(); } }
        }

        [HarmonyPatch(typeof(MeetingHud), nameof(MeetingHud.Update))]
        class MeetingHudUpdatePatch
        {
            static void Postfix(MeetingHud __instance)
            {
                // Deactivate skip Button if skipping on emergency meetings is disabled
                if (target == null && blockSkippingInEmergencyMeetings)
                    __instance.SkipVoteButton.gameObject.SetActive(false);

                if (__instance.state >= MeetingHud.VoteStates.Discussion)
                {
                    // Remove first kill shield
                    TORMapOptions.firstKillPlayer = null;
                }
            }
        }
        [HarmonyPrefix]
        [HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.StartMeeting))]
        public static void MeetingHudIntroPrefix()
        {
            EventUtility.meetingStartsUpdate();
        }

        public static void populateButtons(MeetingHud __instance, byte reporter)
        {
            var playerControlesToBeIgnored = new List<PlayerControl>() { };
            playerControlesToBeIgnored.RemoveAll(x => x == null);
            var playerIdsToBeIgnored = playerControlesToBeIgnored.Select(x => x.PlayerId);
            // Generate PlayerVoteAreas
            __instance.playerStates = new PlayerVoteArea[GameData.Instance.PlayerCount - playerIdsToBeIgnored.Count()];
            int playerStatesCounter = 0;
            for (int i = 0; i < __instance.playerStates.Length + playerIdsToBeIgnored.Count(); i++)
            {
                if (playerIdsToBeIgnored.Contains(GameData.Instance.AllPlayers[i].PlayerId))
                {
                    continue;
                }
                NetworkedPlayerInfo playerInfo = GameData.Instance.AllPlayers[i];
                PlayerVoteArea playerVoteArea = __instance.playerStates[playerStatesCounter] = __instance.CreateButton(playerInfo);
                playerVoteArea.Parent = __instance;
                playerVoteArea.SetTargetPlayerId(playerInfo.PlayerId);
                playerVoteArea.SetDead(reporter == playerInfo.PlayerId, playerInfo.Disconnected || playerInfo.IsDead, playerInfo.Role.Role == RoleTypes.GuardianAngel);
                playerVoteArea.UpdateOverlay();
                playerStatesCounter++;
            }
            foreach (PlayerVoteArea playerVoteArea2 in __instance.playerStates)
            {
                ControllerManager.Instance.AddSelectableUiElement(playerVoteArea2.PlayerButton, false);
            }
            __instance.SortButtons();
        }
        [HarmonyPatch]
        public class ShowHost
        {
            private static TextMeshPro Text = null;
            [HarmonyPatch(typeof(MeetingHud), nameof(MeetingHud.Start))]
            [HarmonyPostfix]
            public static void Setup(MeetingHud __instance)
            {
                if (AmongUsClient.Instance.NetworkMode != NetworkModes.OnlineGame) return;
                __instance.ProceedButton.gameObject.transform.localPosition = new(-2.5f, 2.2f, 0);
                __instance.ProceedButton.gameObject.GetComponent<SpriteRenderer>().enabled = false;
                __instance.ProceedButton.GetComponent<PassiveButton>().enabled = false;
                __instance.HostIcon.gameObject.SetActive(true);
                __instance.ProceedButton.gameObject.SetActive(true);
            }
            [HarmonyPatch(typeof(MeetingHud), nameof(MeetingHud.Update))]
            [HarmonyPostfix]
            public static void Postfix(MeetingHud __instance)
            {
                var host = GameData.Instance.GetHost();
                if (host != null)
                {
                    PlayerMaterial.SetColors(host.DefaultOutfit.ColorId, __instance.HostIcon);
                    if (Text == null) Text = __instance.ProceedButton.gameObject.GetComponentInChildren<TextMeshPro>();
                    Text.text = $"{ModTranslation.GetString("Credentials", 8)}: {host.PlayerName}";
                }
            }
        }
    }
}
