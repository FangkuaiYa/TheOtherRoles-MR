﻿
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using HarmonyLib;
using Hazel;
using TheOtherRoles.CustomGameModes;
using TheOtherRoles.Utilities;
using UnityEngine;
using static TheOtherRoles.TheOtherRoles;

namespace TheOtherRoles.Patches
{
    enum CustomGameOverReason
    {
        LoversWin = 10,
        TeamJackalWin = 11,
        MiniLose = 12,
        JesterWin = 13,
        ArsonistWin = 14,
        VultureWin = 15,
        ProsecutorWin = 16,
        KataomoiWin = 80,
        TaskMasterWin = 100,
        ForceEnd = 101,

        // Task Vs Mode
        TaskVsModeEnd = 130,
        // Happy Birthay Mode
        HappyBirthdayModeEnd = 131,

        Unused = byte.MaxValue,
    }

    enum WinCondition
    {
        Default,
        LoversTeamWin,
        LoversSoloWin,
        JesterWin,
        JackalWin,
        MiniLose,
        ArsonistWin,
        VultureWin,
        AdditionalLawyerBonusWin,
        AdditionalAlivePursuerWin,
        ProsecutorWin,
        TaskMasterTeamWin,
        KataomoiWin,
        ForceEnd,

        // Task Vs Mode
        TaskVsModeEnd,
        // Happy Birthday Mode
        HappyBirthdayModeEnd,
    }

    static class AdditionalTempData
    {
        // Should be implemented using a proper GameOverReason in the future
        public static WinCondition winCondition = WinCondition.Default;
        public static List<WinCondition> additionalWinConditions = new List<WinCondition>();
        public static List<PlayerRoleInfo> playerRoles = new List<PlayerRoleInfo>();
        public static List<TaskVsModeInfo> taskVsModeInfos = new List<TaskVsModeInfo>();
        public static float timer = 0;

        public static void clear()
        {
            playerRoles.Clear();
            taskVsModeInfos.Clear();
            additionalWinConditions.Clear();
            winCondition = WinCondition.Default;
            timer = 0;
        }

        internal class PlayerRoleInfo
        {
            public string PlayerName { get; set; }
            public List<RoleInfo> Roles { get; set; }
            public string RoleNames { get; set; }
            public string RoleString { get; set; }
            public int TasksCompleted { get; set; }
            public int TasksTotal { get; set; }
            public int ExTasksCompleted { get; set; }
            public int ExTasksTotal { get; set; }
            public string ExtraInfo { get; set; }
            public bool IsGuesser { get; set; }
            public bool IsImpostor { get; set; }
            public int? Kills { get; set; }
            public bool IsAlive { get; set; }
        }

        internal class TaskVsModeInfo
        {
            public string PlayerName { get; set; }
            public ulong Time { get; set; } = ulong.MaxValue;
            public bool IsNewRecord { get; set; } = false;
        }
    }

    [HarmonyPatch(typeof(AmongUsClient), nameof(AmongUsClient.OnGameEnd))]
    public static class OnGameEndPatch
    {
        public static GameOverReason gameOverReason = GameOverReason.HumansByTask;
        public static void Prefix(AmongUsClient __instance, [HarmonyArgument(0)] ref EndGameResult endGameResult)
        {
            gameOverReason = endGameResult.GameOverReason;

            if ((int)endGameResult.GameOverReason >= 10 && (int)endGameResult.GameOverReason < 100)
            {
                endGameResult.GameOverReason = GameOverReason.ImpostorByKill;
            }
            else if ((int)endGameResult.GameOverReason >= 100)
            {
                switch ((CustomGameOverReason)endGameResult.GameOverReason)
                {
                    case CustomGameOverReason.ForceEnd:
                    case CustomGameOverReason.TaskMasterWin:
                    case CustomGameOverReason.TaskVsModeEnd:
                    case CustomGameOverReason.HappyBirthdayModeEnd:
                        endGameResult.GameOverReason = GameOverReason.HumansByTask;
                        break;
                }
            }

            // Reset zoomed out ghosts
            Helpers.toggleZoom(reset: true);
        }

        public static void Postfix(AmongUsClient __instance, [HarmonyArgument(0)] ref EndGameResult endGameResult)
        {
            AdditionalTempData.clear();

            // Task Vs Mode
            if (TaskRacer.isValid())
            {
                foreach (var taskRacer in TaskRacer.taskRacers)
                {
                    AdditionalTempData.taskVsModeInfos.Add(new AdditionalTempData.TaskVsModeInfo()
                    {
                        PlayerName = taskRacer.player.Data.PlayerName,
                        Time = taskRacer.taskCompleteTime,
                        IsNewRecord = taskRacer.isSelf() && taskRacer.taskCompleteTime < TaskRacer.recordTime,
                    });
                }
            }
            else
            {
                foreach (var playerControl in PlayerControl.AllPlayerControls)
                {
                    int allTasks = GameOptionsManager.Instance.currentNormalGameOptions.NumCommonTasks + GameOptionsManager.Instance.currentNormalGameOptions.NumLongTasks + GameOptionsManager.Instance.currentNormalGameOptions.NumShortTasks;
                    var roles = RoleInfo.getRoleInfoForPlayer(playerControl);
                    var (tasksCompleted, tasksTotal) = TasksHandler.taskInfo(playerControl.Data, true, true);
                    bool isGuesser = HandleGuesser.isGuesserGm && HandleGuesser.isGuesser(playerControl.PlayerId);
                    int? killCount = GameHistory.deadPlayers.FindAll(x => x.killerIfExisting != null && x.killerIfExisting.PlayerId == playerControl.PlayerId).Count;
                    if (killCount == 0 && !(new List<RoleInfo>() { RoleInfo.sheriff, RoleInfo.jackal, RoleInfo.sidekick, RoleInfo.thief }.Contains(RoleInfo.getRoleInfoForPlayer(playerControl, false).FirstOrDefault()) || playerControl.Data.Role.IsImpostor))
                    {
                        killCount = null;
                    }
                    bool isTaskMaster = TaskMaster.isTaskMaster(playerControl.PlayerId);
                    bool isTaskMasterExTasks = isTaskMaster && TaskMaster.isTaskComplete;
                    string extraInfo = "";
                    if (Kataomoi.kataomoi != null && Kataomoi.target == playerControl)
                        extraInfo = Helpers.cs(Kataomoi.color, "♥");
                    string roleString = RoleInfo.GetRolesString(playerControl, true, true, false);
                    AdditionalTempData.playerRoles.Add(new AdditionalTempData.PlayerRoleInfo()
                    {
                        PlayerName = playerControl.Data.PlayerName,
                        Roles = roles,
                        RoleNames = roleString,
                        TasksTotal = isTaskMasterExTasks ? allTasks : tasksTotal,
                        TasksCompleted = isTaskMasterExTasks ? allTasks : tasksCompleted,
                        ExTasksTotal = isTaskMasterExTasks ? TaskMaster.allExTasks : isTaskMaster ? TaskMasterTaskHelper.GetTaskMasterTasks() : 0,
                        ExTasksCompleted = isTaskMasterExTasks ? TaskMaster.clearExTasks : 0,
                        ExtraInfo = extraInfo,
                        IsGuesser = isGuesser,
                        IsImpostor = playerControl.Data.Role.IsImpostor,
                        Kills = killCount,
                        IsAlive = !playerControl.Data.IsDead
                    });
                }
            }

            // Remove Jester, Arsonist, Vulture, Amnesiac, Jackal, former Jackals and Sidekick, Kataomoi from winners (if they win, they'll be readded)
            List<PlayerControl> notWinners = new List<PlayerControl>();
            if (Jester.jester != null) notWinners.Add(Jester.jester);
            if (Sidekick.sidekick != null) notWinners.Add(Sidekick.sidekick);
            if (Jackal.jackal != null) notWinners.Add(Jackal.jackal);
            if (Arsonist.arsonist != null) notWinners.Add(Arsonist.arsonist);
            if (Vulture.vulture != null) notWinners.Add(Vulture.vulture);
            if (Amnesiac.amnesiac != null) notWinners.Add(Amnesiac.amnesiac);
            if (Madmate.madmate != null) notWinners.Add(Madmate.madmate);
            if (Lawyer.lawyer != null) notWinners.Add(Lawyer.lawyer);
            if (Pursuer.pursuer != null) notWinners.Add(Pursuer.pursuer);
            if (Thief.thief != null) notWinners.Add(Thief.thief);
            if (Kataomoi.kataomoi != null) notWinners.Add(Kataomoi.kataomoi);
            if (MadmateKiller.madmateKiller != null) notWinners.Add(MadmateKiller.madmateKiller);

            notWinners.AddRange(Jackal.formerJackals);

            List<CachedPlayerData> winnersToRemove = new List<CachedPlayerData>();
            foreach (CachedPlayerData winner in EndGameResult.CachedWinners.GetFastEnumerator())
            {
                if (notWinners.Any(x => x.Data.PlayerName == winner.PlayerName)) winnersToRemove.Add(winner);
            }
            foreach (var winner in winnersToRemove) EndGameResult.CachedWinners.Remove(winner);

            bool jesterWin = Jester.jester != null && gameOverReason == (GameOverReason)CustomGameOverReason.JesterWin;
            bool arsonistWin = Arsonist.arsonist != null && gameOverReason == (GameOverReason)CustomGameOverReason.ArsonistWin;
            bool miniLose = Mini.mini != null && gameOverReason == (GameOverReason)CustomGameOverReason.MiniLose;
            bool loversWin = Lovers.existingAndAlive() && (gameOverReason == (GameOverReason)CustomGameOverReason.LoversWin || (GameManager.Instance.DidHumansWin(gameOverReason) && !Lovers.existingWithKiller())); // Either they win if they are among the last 3 players, or they win if they are both Crewmates and both alive and the Crew wins (Team Imp/Jackal Lovers can only win solo wins)
            bool teamJackalWin = gameOverReason == (GameOverReason)CustomGameOverReason.TeamJackalWin && ((Jackal.jackal != null && !Jackal.jackal.Data.IsDead) || (Sidekick.sidekick != null && !Sidekick.sidekick.Data.IsDead));
            bool vultureWin = Vulture.vulture != null && gameOverReason == (GameOverReason)CustomGameOverReason.VultureWin;
            bool prosecutorWin = Lawyer.lawyer != null && gameOverReason == (GameOverReason)CustomGameOverReason.ProsecutorWin;
            bool taskMasterTeamWin = TaskMaster.taskMaster != null && gameOverReason == (GameOverReason)CustomGameOverReason.TaskMasterWin;
            bool kataomoiWin = Kataomoi.kataomoi != null && gameOverReason == (GameOverReason)CustomGameOverReason.KataomoiWin;
            bool taskVsModeEnd = TaskRacer.isValid() && gameOverReason == (GameOverReason)CustomGameOverReason.TaskVsModeEnd;
            bool forceEnd = gameOverReason == (GameOverReason)CustomGameOverReason.ForceEnd;
            bool happyBirthdayModeEnd = CustomOptionHolder.enabledHappyBirthdayMode.getBool() && gameOverReason == (GameOverReason)CustomGameOverReason.HappyBirthdayModeEnd;
            if (happyBirthdayModeEnd)
            {
                var p = Helpers.playerById((byte)CustomOptionHolder.happyBirthdayMode_Target.getFloat());
                if (p == null)
                    happyBirthdayModeEnd = false;
            }

            bool isPursurerLose = jesterWin || arsonistWin || miniLose || vultureWin || teamJackalWin;

            // Happy Birthday Mode
            if (happyBirthdayModeEnd)
            {
                var p = Helpers.playerById((byte)CustomOptionHolder.happyBirthdayMode_Target.getFloat());
                EndGameResult.CachedWinners = new Il2CppSystem.Collections.Generic.List<CachedPlayerData>();
                CachedPlayerData wpd = new CachedPlayerData(p.Data);
                EndGameResult.CachedWinners.Add(wpd);
                AdditionalTempData.winCondition = WinCondition.HappyBirthdayModeEnd;
            }
            // Mini lose
            else if (miniLose)
            {
                EndGameResult.CachedWinners = new Il2CppSystem.Collections.Generic.List<CachedPlayerData>();
                CachedPlayerData wpd = new CachedPlayerData(Mini.mini.Data);
                wpd.IsYou = false; // If "no one is the Mini", it will display the Mini, but also show defeat to everyone
                EndGameResult.CachedWinners.Add(wpd);
                AdditionalTempData.winCondition = WinCondition.MiniLose;
            }

            // Jester win
            else if (jesterWin)
            {
                EndGameResult.CachedWinners = new Il2CppSystem.Collections.Generic.List<CachedPlayerData>();
                CachedPlayerData wpd = new CachedPlayerData(Jester.jester.Data);
                EndGameResult.CachedWinners.Add(wpd);
                AdditionalTempData.winCondition = WinCondition.JesterWin;
            }

            // Arsonist win
            else if (arsonistWin)
            {
                EndGameResult.CachedWinners = new Il2CppSystem.Collections.Generic.List<CachedPlayerData>();
                CachedPlayerData wpd = new CachedPlayerData(Arsonist.arsonist.Data);
                EndGameResult.CachedWinners.Add(wpd);
                AdditionalTempData.winCondition = WinCondition.ArsonistWin;
            }

            // Kataomoi win
            else if (kataomoiWin)
            {
                EndGameResult.CachedWinners = new Il2CppSystem.Collections.Generic.List<CachedPlayerData>();
                CachedPlayerData wpd = new CachedPlayerData(Kataomoi.kataomoi.Data);
                EndGameResult.CachedWinners.Add(wpd);
                AdditionalTempData.winCondition = WinCondition.KataomoiWin;
            }

            // Vulture win
            else if (vultureWin)
            {
                EndGameResult.CachedWinners = new Il2CppSystem.Collections.Generic.List<CachedPlayerData>();
                CachedPlayerData wpd = new CachedPlayerData(Vulture.vulture.Data);
                EndGameResult.CachedWinners.Add(wpd);
                AdditionalTempData.winCondition = WinCondition.VultureWin;
            }

            // Prosecutor win
            else if (prosecutorWin)
            {
                EndGameResult.CachedWinners = new Il2CppSystem.Collections.Generic.List<CachedPlayerData>();
                CachedPlayerData wpd = new CachedPlayerData(Lawyer.lawyer.Data);
                EndGameResult.CachedWinners.Add(wpd);
                AdditionalTempData.winCondition = WinCondition.ProsecutorWin;
            }

            // Lovers win conditions
            else if (loversWin)
            {
                // Double win for lovers, crewmates also win
                if (!Lovers.existingWithKiller())
                {
                    AdditionalTempData.winCondition = WinCondition.LoversTeamWin;
                    EndGameResult.CachedWinners = new Il2CppSystem.Collections.Generic.List<CachedPlayerData>();
                    foreach (PlayerControl p in PlayerControl.AllPlayerControls)
                    {
                        if (p == null) continue;
                        if (p == Lovers.lover1 || p == Lovers.lover2)
                            EndGameResult.CachedWinners.Add(new CachedPlayerData(p.Data));
                        else if (p == Pursuer.pursuer && !Pursuer.pursuer.Data.IsDead)
                            EndGameResult.CachedWinners.Add(new CachedPlayerData(p.Data));
                        else if (p != Jester.jester && p != Jackal.jackal && p != Sidekick.sidekick && p != Arsonist.arsonist && p != Vulture.vulture && p != Kataomoi.kataomoi && !Jackal.formerJackals.Contains(p) && !p.Data.Role.IsImpostor)
                            EndGameResult.CachedWinners.Add(new CachedPlayerData(p.Data));
                    }
                }
                // Lovers solo win
                else
                {
                    AdditionalTempData.winCondition = WinCondition.LoversSoloWin;
                    EndGameResult.CachedWinners = new Il2CppSystem.Collections.Generic.List<CachedPlayerData>();
                    EndGameResult.CachedWinners.Add(new CachedPlayerData(Lovers.lover1.Data));
                    EndGameResult.CachedWinners.Add(new CachedPlayerData(Lovers.lover2.Data));
                }
            }

            // Jackal win condition (should be implemented using a proper GameOverReason in the future)
            else if (teamJackalWin)
            {
                // Jackal wins if nobody except jackal is alive
                AdditionalTempData.winCondition = WinCondition.JackalWin;
                EndGameResult.CachedWinners = new Il2CppSystem.Collections.Generic.List<CachedPlayerData>();
                CachedPlayerData wpd = new CachedPlayerData(Jackal.jackal.Data);
                wpd.IsImpostor = false;
                EndGameResult.CachedWinners.Add(wpd);
                // If there is a sidekick. The sidekick also wins
                if (Sidekick.sidekick != null)
                {
                    CachedPlayerData wpdSidekick = new CachedPlayerData(Sidekick.sidekick.Data);
                    wpdSidekick.IsImpostor = false;
                    EndGameResult.CachedWinners.Add(wpdSidekick);
                }
                foreach (var player in Jackal.formerJackals)
                {
                    CachedPlayerData wpdFormerJackal = new CachedPlayerData(player.Data);
                    wpdFormerJackal.IsImpostor = false;
                    EndGameResult.CachedWinners.Add(wpdFormerJackal);
                }
            }
            // TaskMaster team win
            else if (taskMasterTeamWin)
            {
                AdditionalTempData.winCondition = WinCondition.TaskMasterTeamWin;
                bool addCrewmateLovers = !Lovers.existingWithKiller();
                EndGameResult.CachedWinners = new Il2CppSystem.Collections.Generic.List<CachedPlayerData>();
                foreach (PlayerControl p in PlayerControl.AllPlayerControls)
                {
                    if (p == null) continue;
                    if (addCrewmateLovers && (p == Lovers.lover1 || p == Lovers.lover2))
                        EndGameResult.CachedWinners.Add(new CachedPlayerData(p.Data));
                    else if (p == Pursuer.pursuer && !Pursuer.pursuer.Data.IsDead)
                        EndGameResult.CachedWinners.Add(new CachedPlayerData(p.Data));
                    else if (p != Madmate.madmate && p != Jester.jester && p != Jackal.jackal && p != Sidekick.sidekick && p != Arsonist.arsonist && p != Vulture.vulture && p != Kataomoi.kataomoi && !Jackal.formerJackals.Contains(p) && !p.Data.Role.IsImpostor)
                        EndGameResult.CachedWinners.Add(new CachedPlayerData(p.Data));
                }
            }
            // Task Vs Mode
            else if (taskVsModeEnd)
            {
                EndGameResult.CachedWinners = new Il2CppSystem.Collections.Generic.List<CachedPlayerData>();
                AdditionalTempData.winCondition = WinCondition.TaskVsModeEnd;
                for (int i = 0; i < TaskRacer.taskRacers.Count; ++i)
                {
                    if (TaskRacer.taskRacers[i].player == null) continue;
                    EndGameResult.CachedWinners.Add(new CachedPlayerData(TaskRacer.taskRacers[i].player.Data));
                    if (EndGameResult.CachedWinners.Count >= 3) break;
                }
            }
            else if (forceEnd)
            {
                EndGameResult.CachedWinners = new Il2CppSystem.Collections.Generic.List<CachedPlayerData>();
                AdditionalTempData.winCondition = WinCondition.ForceEnd;
            }
            else
            {
                bool isImpostor = false;
                foreach (CachedPlayerData winner in EndGameResult.CachedWinners.GetFastEnumerator())
                {
                    if (winner.IsImpostor)
                    {
                        isImpostor = true;
                        break;
                    }
                }

                if (isImpostor)
                {
                    // Madmate wins if team impostors wins
                    if (Madmate.madmate != null)
                    {
                        CachedPlayerData wpd = new CachedPlayerData(Madmate.madmate.Data);
                        EndGameResult.CachedWinners.Add(wpd);
                    }

                    // MadmateKiller wins if team impostors wins
                    if (MadmateKiller.madmateKiller != null)
                    {
                        CachedPlayerData wpd = new CachedPlayerData(MadmateKiller.madmateKiller.Data);
                        EndGameResult.CachedWinners.Add(wpd);
                    }
                }
            }

            if (!happyBirthdayModeEnd)
            {
                // Possible Additional winner: Lawyer
                if (Lawyer.lawyer != null && Lawyer.target != null && (!Lawyer.target.Data.IsDead || Lawyer.target == Jester.jester) && !Pursuer.notAckedExiled && !Lawyer.isProsecutor)
                {
                    CachedPlayerData winningClient = null;
                    foreach (CachedPlayerData winner in EndGameResult.CachedWinners.GetFastEnumerator())
                    {
                        if (winner.PlayerName == Lawyer.target.Data.PlayerName)
                            winningClient = winner;
                    }
                    if (winningClient != null)
                    { // The Lawyer wins if the client is winning (and alive, but if he wasn't the Lawyer shouldn't exist anymore)
                        if (!EndGameResult.CachedWinners.ToArray().Any(x => x.PlayerName == Lawyer.lawyer.Data.PlayerName))
                            EndGameResult.CachedWinners.Add(new CachedPlayerData(Lawyer.lawyer.Data));
                        AdditionalTempData.additionalWinConditions.Add(WinCondition.AdditionalLawyerBonusWin); // The Lawyer wins together with the client
                    }
                }

                // Possible Additional winner: Pursuer
                if (Pursuer.pursuer != null && !Pursuer.pursuer.Data.IsDead && !Pursuer.notAckedExiled && !isPursurerLose && !EndGameResult.CachedWinners.ToArray().Any(x => x.IsImpostor))
                {
                    if (!EndGameResult.CachedWinners.ToArray().Any(x => x.PlayerName == Pursuer.pursuer.Data.PlayerName))
                        EndGameResult.CachedWinners.Add(new CachedPlayerData(Pursuer.pursuer.Data));
                    AdditionalTempData.additionalWinConditions.Add(WinCondition.AdditionalAlivePursuerWin);
                }
            }

            AdditionalTempData.timer = ((float)(DateTime.UtcNow - (HideNSeek.isHideNSeekGM ? HideNSeek.startTime : PropHunt.startTime)).TotalMilliseconds) / 1000;

            // Reset Settings
            if (TORMapOptions.gameMode == CustomGamemodes.HideNSeek) ShipStatusPatch.resetVanillaSettings();
            RPCProcedure.resetVariables();
            EventUtility.gameEndsUpdate();

            if (DebugManager.EnableDebugMode())
            {
                for (int i = 0; i < DebugManager.bots.Count; ++i)
                    PlayerControl.AllPlayerControls.Remove(DebugManager.bots[i]);
                DebugManager.bots.Clear();
            }
        }

        [HarmonyPatch(typeof(EndGameManager), nameof(EndGameManager.SetEverythingUp))]
        public class EndGameManagerSetUpPatch
        {
            public static void Postfix(EndGameManager __instance)
            {
                // Delete and readd PoolablePlayers always showing the name and role of the player
                foreach (PoolablePlayer pb in __instance.transform.GetComponentsInChildren<PoolablePlayer>())
                {
                    UnityEngine.Object.Destroy(pb.gameObject);
                }
                int num = Mathf.CeilToInt(7.5f);

                bool isForceEnd = AdditionalTempData.winCondition == WinCondition.ForceEnd;

                // Task Vs Mode
                bool isTaskVsMode = AdditionalTempData.winCondition == WinCondition.TaskVsModeEnd;
                // Happy Birthday Mode
                bool isHappyBirthdayMode = AdditionalTempData.winCondition == WinCondition.HappyBirthdayModeEnd;
                List<CachedPlayerData> list = isTaskVsMode ? EndGameResult.CachedWinners.ToArray().ToList() : EndGameResult.CachedWinners.ToArray().ToList().OrderBy(delegate (CachedPlayerData b)
                {
                    if (!b.IsYou)
                    {
                        return 0;
                    }
                    return -1;
                }).ToList<CachedPlayerData>();
                for (int i = 0; i < list.Count; i++)
                {
                    CachedPlayerData playerControlData2 = list[i];
                    int num2 = (i % 2 == 0) ? -1 : 1;
                    int num3 = (i + 1) / 2;
                    float offsetX = isTaskVsMode ? 2f : 1f;
                    float offsetX2 = 0;
                    if (isHappyBirthdayMode) offsetX2 = 0.75f;
                    float num4 = (float)num3 / (float)num;
                    float num5 = Mathf.Lerp(1f, 0.75f, num4);
                    float num6 = (float)((i == 0) ? -8 : -1);
                    PoolablePlayer poolablePlayer = UnityEngine.Object.Instantiate<PoolablePlayer>(__instance.PlayerPrefab, __instance.transform);
                    poolablePlayer.transform.localPosition = new Vector3(offsetX2 + offsetX * (float)num2 * (float)num3 * num5, FloatRange.SpreadToEdges(-1.125f, 0f, num3, num), num6 + (float)num3 * 0.01f) * 0.9f;
                    float num7 = Mathf.Lerp(1f, 0.65f, num4) * 0.9f;
                    Vector3 vector = new Vector3(num7, num7, 1f);
                    poolablePlayer.transform.localScale = vector;
                    if (isHappyBirthdayMode)
                        playerControlData2.IsDead = false;
                    poolablePlayer.UpdateFromPlayerOutfit(playerControlData2.Outfit, PlayerMaterial.MaskType.ComplexUI, playerControlData2.IsDead, true);
                    if (playerControlData2.IsDead)
                    {
                        poolablePlayer.SetBodyAsGhost();
                        poolablePlayer.SetDeadFlipX(i % 2 == 0);
                    }
                    else
                    {
                        poolablePlayer.SetFlipX(i % 2 == 0);
                    }
                    poolablePlayer.UpdateFromPlayerOutfit(playerControlData2.Outfit, PlayerMaterial.MaskType.None, playerControlData2.IsDead, true);

                    if (!isHappyBirthdayMode)
                    {
                        poolablePlayer.cosmetics.nameText.color = isTaskVsMode ? TaskRacer.getRankTextColor(i + 1) : Color.white;
                        poolablePlayer.cosmetics.nameText.transform.localScale = new Vector3(1f / vector.x, 1f / vector.y, 1f / vector.z);
                        poolablePlayer.cosmetics.nameText.transform.localPosition = new Vector3(poolablePlayer.cosmetics.nameText.transform.localPosition.x, poolablePlayer.cosmetics.nameText.transform.localPosition.y, -15f);
                        poolablePlayer.cosmetics.nameText.text = playerControlData2.PlayerName;
                        if (isTaskVsMode)
                        {
                            poolablePlayer.cosmetics.nameText.text += $"\n{TaskRacer.getRankText(i + 1, true)}";
                        }

                        foreach (var data in AdditionalTempData.playerRoles)
                        {
                            if (data.PlayerName != playerControlData2.PlayerName) continue;
                            var roles =
                            poolablePlayer.cosmetics.nameText.text += $"\n{string.Join("\n", data.Roles.Select(x => Helpers.cs(x.color, x.name)))}";
                        }
                    }
                    else
                    {
                        poolablePlayer.cosmetics.nameText.text = "";
                        {
                            var cake = new Objects.BirthdayCake(poolablePlayer.transform, (Objects.BirthdayCake.CakeType)CustomOptionHolder.happyBirthdayMode_CakeType.getFloat(), new Vector3(-1.4f, 0f, 0f), Vector3.one * 1.75f);
                            cake.SetColorId(playerControlData2.ColorId);
                        }
                        {
                            var cakeEmoObj = new GameObject("cake_emo");
                            cakeEmoObj.transform.SetParent(poolablePlayer.transform);
                            cakeEmoObj.transform.localPosition = new Vector3(0.6f, 1.2f, -2f);
                            cakeEmoObj.transform.localScale = Vector3.one;

                            var spriteRenderer = cakeEmoObj.AddComponent<SpriteRenderer>();
                            spriteRenderer.sprite = Helpers.loadSpriteFromResources("BirthdayCake_Emo.png", 200f);
                        }
                    }
                }

                if (isTaskVsMode)
                {
                    __instance.WinText.text = ModTranslation.GetString("EndGame", 1);
                }
                else if (isHappyBirthdayMode)
                {
                    int birthMonth = (int)CustomOptionHolder.happyBirthdayMode_TargetBirthMonth.getFloat();
                    int birthDay = (int)CustomOptionHolder.happyBirthdayMode_TargetBirthDay.getFloat();
                    if (birthMonth > 0 && birthDay > 0)
                        __instance.WinText.text = string.Format("{0}/{1}", birthMonth, birthDay);
                    else
                        __instance.WinText.text = "";
                }
                else if (isForceEnd)
                {
                    __instance.WinText.text = ModTranslation.GetString("EndGame", 2);
                    __instance.WinText.color = Palette.DisabledGrey;
                }

                // Additional code
                GameObject bonusText = UnityEngine.Object.Instantiate(__instance.WinText.gameObject);
                bonusText.transform.position = new Vector3(__instance.WinText.transform.position.x, __instance.WinText.transform.position.y - 0.5f, __instance.WinText.transform.position.z);
                bonusText.transform.localScale = new Vector3(0.7f, 0.7f, 1f);
                TMPro.TMP_Text textRenderer = bonusText.GetComponent<TMPro.TMP_Text>();
                textRenderer.text = "";

                if (AdditionalTempData.winCondition == WinCondition.JesterWin)
                {
                    textRenderer.text = ModTranslation.GetString("EndGame", 3);
                    textRenderer.color = Jester.color;
                }
                else if (AdditionalTempData.winCondition == WinCondition.ArsonistWin)
                {
                    textRenderer.text = ModTranslation.GetString("EndGame", 4);
                    textRenderer.color = Arsonist.color;
                }
                else if (AdditionalTempData.winCondition == WinCondition.KataomoiWin)
                {
                    textRenderer.text = ModTranslation.GetString("EndGame", 5);
                    foreach (var data in AdditionalTempData.playerRoles)
                    {
                        if (data.ExtraInfo.Contains("♥"))
                        {
                            textRenderer.text += ModTranslation.GetString("EndGame", 6);
                            break;
                        }
                    }
                    textRenderer.color = Kataomoi.color;
                    __instance.BackgroundBar.material.SetColor("_Color", Kataomoi.color);
                }
                else if (AdditionalTempData.winCondition == WinCondition.VultureWin)
                {
                    textRenderer.text = ModTranslation.GetString("EndGame", 7);
                    textRenderer.color = Vulture.color;
                }
                else if (AdditionalTempData.winCondition == WinCondition.ProsecutorWin)
                {
                    textRenderer.text = ModTranslation.GetString("EndGame", 8);
                    textRenderer.color = Lawyer.color;
                }
                else if (AdditionalTempData.winCondition == WinCondition.LoversTeamWin)
                {
                    textRenderer.text = ModTranslation.GetString("EndGame", 9);
                    textRenderer.color = Lovers.color;
                    __instance.BackgroundBar.material.SetColor("_Color", Lovers.color);
                }
                else if (AdditionalTempData.winCondition == WinCondition.LoversSoloWin)
                {
                    textRenderer.text = ModTranslation.GetString("EndGame", 10);
                    textRenderer.color = Lovers.color;
                    __instance.BackgroundBar.material.SetColor("_Color", Lovers.color);
                }
                else if (AdditionalTempData.winCondition == WinCondition.JackalWin)
                {
                    textRenderer.text = ModTranslation.GetString("EndGame", 11);
                    textRenderer.color = Jackal.color;
                }
                else if (AdditionalTempData.winCondition == WinCondition.MiniLose)
                {
                    textRenderer.text = ModTranslation.GetString("EndGame", 12);
                    textRenderer.color = Mini.color;
                }
                else if (AdditionalTempData.winCondition == WinCondition.TaskMasterTeamWin)
                {
                    textRenderer.text = ModTranslation.GetString("EndGame", 13);
                    textRenderer.color = TaskMaster.color;
                }
                else if (AdditionalTempData.winCondition == WinCondition.TaskVsModeEnd)
                {
                    textRenderer.text = ModTranslation.GetString("EndGame", 14);
                    textRenderer.color = TaskMaster.color;
                    __instance.BackgroundBar.material.SetColor("_Color", TaskRacer.color);
                }
                else if (AdditionalTempData.winCondition == WinCondition.ForceEnd)
                {
                    __instance.BackgroundBar.material.SetColor("_Color", Palette.DisabledGrey);
                }
                else if (AdditionalTempData.winCondition == WinCondition.HappyBirthdayModeEnd)
                {
                    textRenderer.text = string.Format(ModTranslation.GetString("EndGame", 15), list[0].PlayerName);
                    textRenderer.color = Color.green;
                    __instance.BackgroundBar.material.SetColor("_Color", Color.green);
                }
                else if (AdditionalTempData.winCondition == WinCondition.Default)
                {
                    switch (OnGameEndPatch.gameOverReason)
                    {
                        case GameOverReason.ImpostorDisconnect:
                            textRenderer.text = ModTranslation.GetString("EndGame", 23);
                            textRenderer.color = Color.red;
                            break;
                        case GameOverReason.ImpostorByKill:
                            textRenderer.text = ModTranslation.GetString("EndGame", 24);
                            textRenderer.color = Color.red;
                            break;
                        case GameOverReason.ImpostorBySabotage:
                            textRenderer.text = ModTranslation.GetString("EndGame", 25);
                            textRenderer.color = Color.red;
                            break;
                        case GameOverReason.ImpostorByVote:
                            textRenderer.text = ModTranslation.GetString("EndGame", 26);
                            textRenderer.color = Color.red;
                            break;
                        case GameOverReason.HumansByTask:
                            textRenderer.text = ModTranslation.GetString("EndGame", 27);
                            textRenderer.color = Color.white;
                            break;
                        case GameOverReason.HumansDisconnect:
                            textRenderer.text = ModTranslation.GetString("EndGame", 28);
                            textRenderer.color = Color.white;
                            break;
                        case GameOverReason.HumansByVote:
                            textRenderer.text = ModTranslation.GetString("EndGame", 29);
                            textRenderer.color = Color.white;
                            break;
                    }
                }

                foreach (WinCondition cond in AdditionalTempData.additionalWinConditions)
                {
                    if (cond == WinCondition.AdditionalLawyerBonusWin)
                    {
                        textRenderer.text += $"\n{Helpers.cs(Lawyer.color, ModTranslation.GetString("EndGame", 16))}";
                    }
                    else if (cond == WinCondition.AdditionalAlivePursuerWin)
                    {
                        textRenderer.text += $"\n{Helpers.cs(Pursuer.color, ModTranslation.GetString("EndGame", 17))}";
                    }
                }

                if (TORMapOptions.showRoleSummary || CustomOptionHolder.enabledTaskVsMode.getBool() || HideNSeek.isHideNSeekGM || PropHunt.isPropHuntGM)
                {
                    var position = Camera.main.ViewportToWorldPoint(new Vector3(0f, 1f, Camera.main.nearClipPlane));
                    GameObject roleSummary = UnityEngine.Object.Instantiate(__instance.WinText.gameObject);
                    roleSummary.transform.position = new Vector3(__instance.Navigation.ExitButton.transform.position.x + 0.1f, position.y - 0.1f, -214f);
                    roleSummary.transform.localScale = new Vector3(1f, 1f, 1f);

                    var roleSummaryText = new StringBuilder();
                    if (HideNSeek.isHideNSeekGM || PropHunt.isPropHuntGM)
                    {
                        int minutes = (int)AdditionalTempData.timer / 60;
                        int seconds = (int)AdditionalTempData.timer % 60;
                        roleSummaryText.AppendLine($"<color=#FAD934FF>Time: {minutes:00}:{seconds:00}</color> \n");
                    }

                    // Task Vs Mode
                    if (CustomOptionHolder.enabledTaskVsMode.getBool())
                    {
                        if (CustomOptionHolder.taskVsMode_EnabledBurgerMakeMode.getBool())
                            roleSummaryText.AppendLine(string.Format(ModTranslation.GetString("EndGame", 18), CustomOptionHolder.taskVsMode_BurgerMakeMode_BurgerLayers.getInt(), CustomOptionHolder.taskVsMode_BurgerMakeMode_MakeBurgerNums.getInt()));
                        else
                            roleSummaryText.AppendLine(ModTranslation.GetString("EndGame", 19));

                        int rank = 1;
                        foreach (var data in AdditionalTempData.taskVsModeInfos)
                        {
                            string rankText = TaskRacer.getRankText(rank);
                            Color color = TaskRacer.getRankTextColor(rank);
                            if (data.Time != ulong.MaxValue)
                                roleSummaryText.AppendLine(Helpers.cs(color, $"{rankText}.{data.PlayerName} - {string.Format("{0:D2}:{1:D2}:{2:D3} <color=yellow>{3}</color>", data.Time / 60000, (data.Time / 1000) % 60, data.Time % 1000, data.IsNewRecord ? "*NEW RECORD" : "")}"));
                            else
                                roleSummaryText.AppendLine(Helpers.cs(color, $"{rankText}.{data.PlayerName} - --:--:---"));
                            ++rank;
                        }
                    }
                    else
                    {
                        roleSummaryText.AppendLine(ModTranslation.GetString("EndGame", 20));
                        foreach (var data in AdditionalTempData.playerRoles)
                        {
                            string roles = data.RoleNames;
                            var taskInfo = data.TasksTotal > 0 ? $" - <color=#FAD934FF>({data.TasksCompleted}/{data.TasksTotal})</color>" : "";
                            var taskInfo2 = data.ExTasksTotal > 0 ? $"Ex <color=#E1564BFF>({data.ExTasksCompleted}/{data.ExTasksTotal})</color>" : "";
                            if (data.Kills != null) taskInfo += $" - <color=#FF0000FF>{string.Format(ModTranslation.GetString("EndGame", 22), data.Kills)}</color>";
                            roleSummaryText.AppendLine($"{Helpers.cs(data.IsAlive ? Color.white : new Color(.7f, .7f, .7f), data.PlayerName)} - {roles}{taskInfo} {taskInfo2}");
                        }
                    }

                    TMPro.TMP_Text roleSummaryTextMesh = roleSummary.GetComponent<TMPro.TMP_Text>();
                    roleSummaryTextMesh.alignment = TMPro.TextAlignmentOptions.TopLeft;
                    roleSummaryTextMesh.color = Color.white;
                    roleSummaryTextMesh.outlineWidth *= 1.2f;
                    roleSummaryTextMesh.fontSizeMin = 1.25f;
                    roleSummaryTextMesh.fontSizeMax = 1.25f;
                    roleSummaryTextMesh.fontSize = 1.25f;

                    var roleSummaryTextMeshRectTransform = roleSummaryTextMesh.GetComponent<RectTransform>();
                    roleSummaryTextMeshRectTransform.anchoredPosition = new Vector2(position.x + 3.5f, position.y - 0.1f);
                    roleSummaryTextMesh.text = roleSummaryText.ToString();
                    Helpers.previousEndGameSummary = $"<size=110%>{roleSummaryText.ToString()}</size>";
                }
                AdditionalTempData.clear();

                if (RPCProcedure.uncheckedEndGameReason != (byte)CustomGameOverReason.Unused)
                {
                    MessageWriter writer = AmongUsClient.Instance.StartRpcImmediately(PlayerControl.LocalPlayer.NetId,
                        (byte)CustomRPC.UncheckedEndGame_Response, Hazel.SendOption.Reliable, -1);
                    writer.Write(PlayerControl.LocalPlayer.PlayerId);
                    AmongUsClient.Instance.FinishRpcImmediately(writer);
                    RPCProcedure.uncheckedEndGameResponse(PlayerControl.LocalPlayer.PlayerId);
                    if (!AmongUsClient.Instance.AmHost)
                        RPCProcedure.uncheckedEndGameReason = (byte)CustomGameOverReason.Unused;
                }
            }
        }

        [HarmonyPatch(typeof(LogicGameFlowNormal), nameof(LogicGameFlowNormal.CheckEndCriteria))]
        public class CheckEndCriteriaPatch
        {
            public static bool Prefix(ShipStatus __instance)
            {
                if (!GameData.Instance) return false;
                if (DestroyableSingleton<TutorialManager>.InstanceExists) // InstanceExists | Don't check Custom Criteria when in Tutorial
                    return true;

                // Task Vs Mode
                if (CustomOptionHolder.enabledTaskVsMode.getBool())
                {
                    if (CheckAndEndGameForTaskVsModeEnd(__instance)) return false;
                    return false;
                }

                var statistics = new PlayerStatistics(__instance);
                if (CheckAndEndGameForTaskMasterWin(__instance)) return false;
                if (CheckAndEndGameForMiniLose(__instance)) return false;
                if (CheckAndEndGameForJesterWin(__instance)) return false;
                if (CheckAndEndGameForKataomoiWin(__instance)) return false;
                if (CheckAndEndGameForArsonistWin(__instance)) return false;
                if (CheckAndEndGameForVultureWin(__instance)) return false;
                if (CheckAndEndGameForSabotageWin(__instance)) return false;
                if (CheckAndEndGameForTaskWin(__instance)) return false;
                if (CheckAndEndGameForProsecutorWin(__instance)) return false;
                if (CheckAndEndGameForLoverWin(__instance, statistics)) return false;
                if (CheckAndEndGameForJackalWin(__instance, statistics)) return false;
                if (CheckAndEndGameForImpostorWin(__instance, statistics)) return false;
                if (CheckAndEndGameForCrewmateWin(__instance, statistics)) return false;
                return false;
            }

            public static void UncheckedEndGame(GameOverReason reason, bool never_used)
            {
                MessageWriter writer = AmongUsClient.Instance.StartRpcImmediately(PlayerControl.LocalPlayer.NetId,
                    (byte)CustomRPC.UncheckedEndGame, Hazel.SendOption.Reliable, -1);
                writer.Write((byte)reason);
                AmongUsClient.Instance.FinishRpcImmediately(writer);
                RPCProcedure.uncheckedEndGame((byte)reason);
            }

            // Task Vs Mode
            private static bool CheckAndEndGameForTaskVsModeEnd(ShipStatus __instance)
            {
                if (TaskRacer.triggerTaskVsModeEnd)
                {
                    //__instance.enabled = false;
                    //GameManager.Instance.RpcEndGame((GameOverReason)CustomGameOverReason.TaskVsModeEnd, false);
                    UncheckedEndGame((GameOverReason)CustomGameOverReason.TaskVsModeEnd, false);
                    return true;
                }
                return false;
            }

            private static bool CheckAndEndGameForTaskMasterWin(ShipStatus __instance)
            {
                if (TaskMaster.triggerTaskMasterWin)
                {
                    //__instance.enabled = false;
                    GameManager.Instance.RpcEndGame((GameOverReason)CheckCustomGameOverReason(CustomGameOverReason.TaskMasterWin), false);
                    return true;
                }
                return false;
            }

            private static bool CheckAndEndGameForMiniLose(ShipStatus __instance)
            {
                if (Mini.triggerMiniLose)
                {
                    //__instance.enabled = false;
                    GameManager.Instance.RpcEndGame((GameOverReason)CheckCustomGameOverReason(CustomGameOverReason.MiniLose), false);
                    return true;
                }
                return false;
            }

            private static bool CheckAndEndGameForJesterWin(ShipStatus __instance)
            {
                if (Jester.triggerJesterWin)
                {
                    //__instance.enabled = false;
                    GameManager.Instance.RpcEndGame((GameOverReason)CheckCustomGameOverReason(CustomGameOverReason.JesterWin), false);
                    return true;
                }
                return false;
            }

            private static bool CheckAndEndGameForKataomoiWin(ShipStatus __instance)
            {
                if (Kataomoi.triggerKataomoiWin)
                {
                    //__instance.enabled = false;
                    GameManager.Instance.RpcEndGame((GameOverReason)CheckCustomGameOverReason(CustomGameOverReason.KataomoiWin), false);
                    return true;
                }
                return false;
            }

            private static bool CheckAndEndGameForArsonistWin(ShipStatus __instance)
            {
                if (Arsonist.triggerArsonistWin)
                {
                    //__instance.enabled = false;
                    GameManager.Instance.RpcEndGame((GameOverReason)CheckCustomGameOverReason(CustomGameOverReason.ArsonistWin), false);
                    return true;
                }
                return false;
            }

            private static bool CheckAndEndGameForVultureWin(ShipStatus __instance)
            {
                if (Vulture.triggerVultureWin)
                {
                    //__instance.enabled = false;
                    GameManager.Instance.RpcEndGame((GameOverReason)CheckCustomGameOverReason(CustomGameOverReason.VultureWin), false);
                    return true;
                }
                return false;
            }

            private static bool CheckAndEndGameForSabotageWin(ShipStatus __instance)
            {
                if (MapUtilities.Systems == null) return false;
                var systemType = MapUtilities.Systems.ContainsKey(SystemTypes.LifeSupp) ? MapUtilities.Systems[SystemTypes.LifeSupp] : null;
                if (systemType != null)
                {
                    LifeSuppSystemType lifeSuppSystemType = systemType.TryCast<LifeSuppSystemType>();
                    if (lifeSuppSystemType != null && lifeSuppSystemType.Countdown < 0f)
                    {
                        EndGameForSabotage(__instance);
                        lifeSuppSystemType.Countdown = 10000f;
                        return true;
                    }
                }
                var systemType2 = MapUtilities.Systems.ContainsKey(SystemTypes.Reactor) ? MapUtilities.Systems[SystemTypes.Reactor] : null;
                if (systemType2 == null)
                {
                    systemType2 = MapUtilities.Systems.ContainsKey(SystemTypes.Laboratory) ? MapUtilities.Systems[SystemTypes.Laboratory] : null;
                }
                if (systemType2 != null)
                {
                    ICriticalSabotage criticalSystem = systemType2.TryCast<ICriticalSabotage>();
                    if (criticalSystem != null && criticalSystem.Countdown < 0f)
                    {
                        EndGameForSabotage(__instance);
                        criticalSystem.ClearSabotage();
                        return true;
                    }
                }
                return false;
            }

            private static bool CheckAndEndGameForTaskWin(ShipStatus __instance)
            {
                if (HideNSeek.isHideNSeekGM && !HideNSeek.taskWinPossible || PropHunt.isPropHuntGM) return false;
                if (GameData.Instance.TotalTasks > 0 && GameData.Instance.TotalTasks <= GameData.Instance.CompletedTasks)
                {
                    //__instance.enabled = false;
                    GameManager.Instance.RpcEndGame(GameOverReason.HumansByTask, false);
                    return true;
                }
                return false;
            }

            private static bool CheckAndEndGameForProsecutorWin(ShipStatus __instance)
            {
                if (Lawyer.triggerProsecutorWin)
                {
                    //__instance.enabled = false;
                    GameManager.Instance.RpcEndGame((GameOverReason)CustomGameOverReason.ProsecutorWin, false);
                    return true;
                }
                return false;
            }

            private static bool CheckAndEndGameForLoverWin(ShipStatus __instance, PlayerStatistics statistics)
            {
                if (statistics.TeamLoversAlive == 2 && statistics.TotalAlive <= 3)
                {
                    //__instance.enabled = false;
                    GameManager.Instance.RpcEndGame((GameOverReason)CheckCustomGameOverReason(CustomGameOverReason.LoversWin), false);
                    return true;
                }
                return false;
            }

            private static bool CheckAndEndGameForJackalWin(ShipStatus __instance, PlayerStatistics statistics)
            {
                if (statistics.TeamJackalAlive >= statistics.TotalAlive - statistics.TeamJackalAlive && statistics.TeamImpostorsAlive == 0 && !(statistics.TeamJackalHasAliveLover && statistics.TeamLoversAlive == 2))
                {
                    //__instance.enabled = false;
                    GameManager.Instance.RpcEndGame((GameOverReason)CheckCustomGameOverReason(CustomGameOverReason.TeamJackalWin), false);
                    return true;
                }
                return false;
            }

            private static bool CheckAndEndGameForImpostorWin(ShipStatus __instance, PlayerStatistics statistics)
            {
                if (HideNSeek.isHideNSeekGM || PropHunt.isPropHuntGM)
                    if ((0 != statistics.TotalAlive - statistics.TeamImpostorsAlive)) return false;

                if (statistics.TeamImpostorsAlive >= statistics.TotalAlive - statistics.TeamImpostorsAlive && statistics.TeamJackalAlive == 0 && !(statistics.TeamImpostorHasAliveLover && statistics.TeamLoversAlive == 2))
                {
                    //__instance.enabled = false;
                    GameOverReason endReason;
                    switch (GameData.LastDeathReason)
                    {
                        case DeathReason.Exile:
                            endReason = GameOverReason.ImpostorByVote;
                            break;
                        case DeathReason.Kill:
                            endReason = GameOverReason.ImpostorByKill;
                            break;
                        default:
                            endReason = GameOverReason.ImpostorByVote;
                            break;
                    }
                    GameManager.Instance.RpcEndGame(endReason, false);
                    return true;
                }
                return false;
            }

            private static bool CheckAndEndGameForCrewmateWin(ShipStatus __instance, PlayerStatistics statistics)
            {
                if (HideNSeek.isHideNSeekGM && HideNSeek.timer <= 0 && !HideNSeek.isWaitingTimer)
                {
                    //__instance.enabled = false;
                    GameManager.Instance.RpcEndGame(GameOverReason.HumansByVote, false);
                    return true;
                }
                if (PropHunt.isPropHuntGM && PropHunt.timer <= 0 && PropHunt.timerRunning)
                {
                    GameManager.Instance.RpcEndGame(GameOverReason.HumansByVote, false);
                    return true;
                }
                if (statistics.TeamImpostorsAlive == 0 && statistics.TeamJackalAlive == 0)
                {
                    //__instance.enabled = false;
                    GameManager.Instance.RpcEndGame(CheckGameOverReason(GameOverReason.HumansByVote), false);
                    return true;
                }
                return false;
            }

            private static void EndGameForSabotage(ShipStatus __instance)
            {
                //__instance.enabled = false;
                GameManager.Instance.RpcEndGame(CheckGameOverReason(GameOverReason.ImpostorBySabotage), false);
                return;
            }

            private static void UncheckedEndGame(CustomGameOverReason reason)
            {
                UncheckedEndGame((GameOverReason)CheckCustomGameOverReason(reason), false);
            }

            private static CustomGameOverReason CheckCustomGameOverReason(CustomGameOverReason reason)
            {
                // Happy Birthday Mode
                if (CustomOptionHolder.enabledHappyBirthdayMode.getBool())
                {
                    var p = Helpers.playerById((byte)CustomOptionHolder.happyBirthdayMode_Target.getFloat());
                    if (p != null)
                        return CustomGameOverReason.HappyBirthdayModeEnd;
                }
                return reason;
            }

            private static GameOverReason CheckGameOverReason(GameOverReason reason)
            {
                // Happy Birthday Mode
                if (CustomOptionHolder.enabledHappyBirthdayMode.getBool())
                {
                    var p = Helpers.playerById((byte)CustomOptionHolder.happyBirthdayMode_Target.getFloat());
                    if (p != null)
                        return (GameOverReason)CustomGameOverReason.HappyBirthdayModeEnd;
                }
                return reason;
            }
        }

        internal class PlayerStatistics
        {
            public int TeamImpostorsAlive { get; set; }
            public int TeamJackalAlive { get; set; }
            public int TeamLoversAlive { get; set; }
            public int TotalAlive { get; set; }
            public bool TeamImpostorHasAliveLover { get; set; }
            public bool TeamJackalHasAliveLover { get; set; }

            public PlayerStatistics(ShipStatus __instance)
            {
                GetPlayerCounts();
            }

            private static bool isLover(NetworkedPlayerInfo p)
            {
                return (Lovers.lover1 != null && Lovers.lover1.PlayerId == p.PlayerId) || (Lovers.lover2 != null && Lovers.lover2.PlayerId == p.PlayerId);
            }

            private void GetPlayerCounts()
            {
                int numJackalAlive = 0;
                int numImpostorsAlive = 0;
                int numLoversAlive = 0;
                int numTotalAlive = 0;
                bool impLover = false;
                bool jackalLover = false;

                foreach (var playerInfo in GameData.Instance.AllPlayers.GetFastEnumerator())
                {
                    if (!playerInfo.Disconnected)
                    {
                        if (!playerInfo.IsDead)
                        {
                            numTotalAlive++;

                            bool lover = isLover(playerInfo);
                            if (lover) numLoversAlive++;

                            if (playerInfo.Role.IsImpostor || (MadmateKiller.madmateKiller != null && MadmateKiller.madmateKiller.PlayerId == playerInfo.PlayerId && KillerCreator.killerCreator != null && KillerCreator.killerCreator.isDead()))
                            {
                                numImpostorsAlive++;
                                if (lover) impLover = true;
                            }
                            if (Jackal.jackal != null && Jackal.jackal.PlayerId == playerInfo.PlayerId)
                            {
                                numJackalAlive++;
                                if (lover) jackalLover = true;
                            }
                            if (Sidekick.sidekick != null && Sidekick.sidekick.PlayerId == playerInfo.PlayerId)
                            {
                                numJackalAlive++;
                                if (lover) jackalLover = true;
                            }
                        }
                    }
                }

                TeamJackalAlive = numJackalAlive;
                TeamImpostorsAlive = numImpostorsAlive;
                TeamLoversAlive = numLoversAlive;
                TotalAlive = numTotalAlive;
                TeamImpostorHasAliveLover = impLover;
                TeamJackalHasAliveLover = jackalLover;
            }
        }
    }
}
