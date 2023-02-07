using HarmonyLib;
using System.Collections.Generic;
using System;
using UnityEngine;
using CodeStage.AntiCheat.ObscuredTypes;
using UnityEngine.UI;
using System.Reflection.Emit;
using static PulsarModLoader.Patches.HarmonyHelpers;
using OculusSampleFramework;
using System.Diagnostics;
using static OVRLipSync;

namespace QualityImprover
{
    public class Patches
    {
        [HarmonyPatch(typeof(PLSpaceTarget), "TakeDamage_Location")]
        class MainTurretSpaceObjectFix
        {
            static void Postfix(PLSpaceTarget __instance, float damage)
            {
                __instance.photonView.RPC("NetTakeDamage", PhotonTargets.Others, new object[]
                    {
                            damage
                    });
            }
        }
		[HarmonyPatch(typeof(PLShipInfoBase), "UpdateVirusSendQueue")]
		class BetterVirusTargeting
		{
			static bool Prefix(PLShipInfoBase __instance)
			{
				if (PLServer.Instance != null)
				{
					List<int> list = new List<int>();
					foreach (int num in __instance.VirusSendQueue.ReverseDictionary.Values)
					{
						PLVirus plvirus = __instance.VirusSendQueue.Forward[num];
						if (plvirus != null && PLServer.Instance.GetEstimatedServerMs() - plvirus.InitialTime > plvirus.TimeLimitMs)
						{
							list.Add(num);
						}
					}
					foreach (int t in list)
					{
						__instance.VirusSendQueue.Remove(t);
					}
					list.Clear();
					if (PLServer.Instance != null)
					{
						int num2 = 3000;
						num2 = UnityEngine.Mathf.RoundToInt((float)num2 / (1f + __instance.MyStats.CyberAttackRating));
						bool flag = !PLGlobal.WithinTimeLimit(PLServer.Instance.GetEstimatedServerMs(), __instance.LastVirusSendQueueTime, num2);
						if (__instance.VirusSendQueue.ForwardDictionary.Values.Count > 0 && PLEncounterManager.Instance.GetCPEI() != null)
						{
							foreach (PLShipInfoBase plshipInfoBase in PLEncounterManager.Instance.AllShips.Values)
							{
								if (plshipInfoBase != null && plshipInfoBase != __instance && plshipInfoBase.MySensorObjectShip != null && plshipInfoBase.MySensorObjectShip.IsDetectedBy(__instance) && !plshipInfoBase.IsInfected && (__instance.ShouldBeHostileToShip(plshipInfoBase, false, true, true) || __instance.TargetShip == plshipInfoBase))
								{
									foreach (int num3 in __instance.VirusSendQueue.ForwardDictionary.Keys)
									{
										PLVirus plvirus2 = __instance.VirusSendQueue.Forward[num3];
										if (!plvirus2.InfectionCompletedOnShips.Contains(plshipInfoBase.ShipID) && (flag || !plvirus2.InitialSendDone || (plshipInfoBase.MyStats != null && plshipInfoBase.MyStats.CyberDefenseRating == 0f)) && PhotonNetwork.isMasterClient)
										{
											if (plshipInfoBase.VirusAttemptSuccessful(plvirus2))
											{
												if (plvirus2.NetID == -1)
												{
													plvirus2.NetID = PLShipInfoBase.ComponentIDCounter++;
												}
												PLServer.Instance.photonView.RPC("VirusSuccess", PhotonTargets.All, new object[]
												{
												__instance.ShipID,
												plshipInfoBase.ShipID,
												num3,
												plvirus2.NetID
												});
											}
											else
											{
												if (!plvirus2.AttemptCounterMap.ContainsKey(plshipInfoBase.ShipID))
												{
													plvirus2.AttemptCounterMap.Add(plshipInfoBase.ShipID, 1);
												}
												else
												{
													plvirus2.AttemptCounterMap[plshipInfoBase.ShipID] = plvirus2.AttemptCounterMap[plshipInfoBase.ShipID] + 1;
												}
												PLServer.Instance.photonView.RPC("VirusBroadcastAttempt", PhotonTargets.Others, new object[]
												{
											__instance.ShipID,
											plshipInfoBase.ShipID,
											__instance.VirusSendQueue.Reverse[plvirus2],
											plvirus2.AttemptCounterMap[plshipInfoBase.ShipID],
											__instance.LastVirusSendQueueTime
												});
											}
										}
									}
								}
							}
							foreach (PLVirus plvirus3 in __instance.VirusSendQueue.ForwardDictionary.Values)
							{
								if (plvirus3 != null)
								{
									plvirus3.InitialSendDone = true;
								}
							}
							if (flag)
							{
								__instance.LastVirusSendQueueTime = PLServer.Instance.GetEstimatedServerMs();
							}
						}
					}
				}
				return false;
			}
		}
		[HarmonyPatch(typeof(PLShipStats), "AddShipComponent")]
		class VirusBugFix 
		{
			static bool Prefix(PLShipStats __instance, PLShipComponent inComponent, ESlotType visualSlot) 
			{
				if(visualSlot == ESlotType.E_COMP_VIRUS) 
				{
					PLSlot slot = __instance.GetSlot(visualSlot);
					if (slot.Contains(inComponent)) return false;
				}
				return true;
			}
		}
        [HarmonyPatch(typeof(PLLiarsDiceGame), "AI_TakeTurns")]
        class LiarsDiceAI
        {
            static bool Prefix(PLLiarsDiceGame __instance, ref ObscuredInt ___LastNewPlayerTimeMs, ref float ___LastAIUpdateTime, ref ObscuredBool ___CallBluffOverTime_InProgress, ref ObscuredBool ___Game_AllowTurnsToAdvance, ref ObscuredInt ___CurrentTurn_PlayerID, ref int ___PrevTurn_PlayerID, ref float ___GameIsActive_Time, ref List<ObscuredInt> ___CurrentlyPlaying_PlayerIDs, ref ObscuredByte ___CurrentTurn_LastDieFace, ref ObscuredByte ___CurrentTurn_LastDieCount)
            {
                if (PhotonNetwork.isMasterClient && !__instance.IsCurrentlyChallenging() && !__instance.IsCurrentlyRolling() && PLServer.Instance.GetEstimatedServerMs() - ___LastNewPlayerTimeMs > 3000 && Time.time - ___LastAIUpdateTime > 1f && !___CallBluffOverTime_InProgress && ___Game_AllowTurnsToAdvance)
                {
                    ___LastAIUpdateTime = Time.time;
                    PLPlayer CurrentPlayer = PLServer.Instance.GetPlayerFromPlayerID(___CurrentTurn_PlayerID);
                    if (CurrentPlayer != null && (CurrentPlayer.IsBot || (___GameIsActive_Time > 20f && PLServer.Instance.GetEstimatedServerMs() - ___LastNewPlayerTimeMs > 60000)))
                    {
                        Dictionary<int, int> MyDices = new Dictionary<int, int>();
                        foreach (Byte value in CurrentPlayer.LocalGame_MyDice) //Gets my Current Hand
                        {
                            int num = (int)value;
                            if (!MyDices.ContainsKey(num))
                            {
                                MyDices.Add(num, 1);
                            }
                            else
                            {
                                MyDices[num]++;
                            }
                        }
                        int Players = 0;
                        foreach (int playerID in ___CurrentlyPlaying_PlayerIDs)
                        {
                            if (PLServer.Instance.GetPlayerFromPlayerID(playerID).LocalGame_MyDice.Count > 0) //Should help count only players currently playing, not the ones waiting for next turn
                            {
                                Players++;
                            }
                        }
                        Byte CurrentFace = ___CurrentTurn_LastDieFace;
                        Byte CurrentBid = ___CurrentTurn_LastDieCount;
                        int BetFace = 0;
                        int BetFace2 = 0;
                        int BetValue = 1;
                        int BetValue2 = 1;
                        double ChanceOfTruth = 0;
                        if (CurrentPlayer.LiarsDice_DieCountOfFace(CurrentFace) >= CurrentBid || CurrentBid == 0) //If I have more dices of that type than the bid I know its true, also if there is no bid
                        {
                            ChanceOfTruth = 100;
                        }
                        else
                        {
                            for (int i = CurrentBid; i <= Players * 5; i++) //This calculates the chance of bid being true
                            {
                                ChanceOfTruth += (Factorial(Players * 5) / (Factorial(i) * Factorial(Players * 5 - i))) * Math.Pow(1f / 6f, i) * Math.Pow(5f / 6f, Players * 5 - i);
                            }
                            ChanceOfTruth *= 100; //Transforms the decimal 0,22 in to 22% for example
                        }
                        foreach (Byte Face in MyDices.Keys) //Gets the highest value Dice in My Hand
                        {
                            if (MyDices.GetValueSafe(Face) > BetValue)
                            {
                                BetFace2 = BetFace;
                                BetValue2 = BetValue;
                                BetFace = Face;
                            }
                            else if (MyDices.GetValueSafe(Face) > BetValue2)//Gets sencond highest value Dice in My Hand 
                            {
                                BetFace2 = Face;
                                BetValue2 = MyDices.GetValueSafe(Face);
                            }
                        }
                        if (UnityEngine.Random.Range(0, 2) == 1) //Should Help AI get not as predicable (because it would always bet dice with biggest number, so now it has a chance of playing the second biggest dice) 
                        {
                            BetFace = BetFace2;
                        }
                        BetValue = (int)UnityEngine.Random.Range(CurrentBid + 1, CurrentBid + (float)Math.Ceiling(Players * 5 * 0.1)); //Gets a random number for the next challenge value between 1 and 10% of the dices
                        if (UnityEngine.Random.Range(0f, 100f) > ChanceOfTruth + 3 && ChanceOfTruth != 100) //Challanges if my random number is bigger than the chance of failure (Plus a little ballance to encorage a little rasing)
                        {
                            __instance.CallBluff();
                        }
                        else
                        {
                            __instance.Raise((Byte)BetFace, (Byte)BetValue);
                        }
                    }
                }
                return false;
            }
            public static double Factorial(double num)
            {
                if (num == 0)
                {
                    return 1;
                }
                else if (num < 0)
                {
                    throw new Exception("Can't Factorial negative numbers!");
                }
                for (int i = (int)num - 1; i > 0; i--)
                {
                    num *= i;
                }
                return num;
            }
        }
        [HarmonyPatch(typeof(PLShipInfo), "RevealCrewRepClicked")]
        class RevealComms 
        {
            static bool Prefix(PLShipInfo __instance, PhotonMessageInfo pmi) 
            {
                PLServer.Instance.IsCrewRepRevealed = true;
                for(int i = 0; i <= 5; i++) 
                {
                    if (PLServer.Instance.RepLevels[i] * 0.05f > UnityEngine.Random.value)
                    {
                        foreach (PLShipInfoBase plshipInfoBase in PLEncounterManager.Instance.AllShips.Values)
                        {
                            if (plshipInfoBase != null && plshipInfoBase != __instance && plshipInfoBase.FactionID == i && plshipInfoBase.HostileShips.Contains(__instance.ShipID))
                            {
                                if(plshipInfoBase.TargetShip == __instance) 
                                {
                                    plshipInfoBase.TargetShip = null;
                                    plshipInfoBase.TargetShip_ForAI = null;
                                    plshipInfoBase.TargetSpaceTarget = null;
                                }
                                plshipInfoBase.HostileShips.Remove(__instance.ShipID);
                                __instance.HostileShips.Remove(plshipInfoBase.ShipID);
                            }
                        }
                    }
                }
                if (PLServer.Instance != null && PhotonNetwork.isMasterClient && __instance.GetIsPlayerShip())
                {
                    PLPlayer playerForPhotonPlayer = PLServer.GetPlayerForPhotonPlayer(pmi.sender);
                    if (playerForPhotonPlayer != null && playerForPhotonPlayer.TeamID == 0 && !playerForPhotonPlayer.IsBot)
                    {
                        PLPlayer cachedFriendlyPlayerOfClass = PLServer.Instance.GetCachedFriendlyPlayerOfClass(0);
                        if (cachedFriendlyPlayerOfClass != null && playerForPhotonPlayer != cachedFriendlyPlayerOfClass)
                        {
                            PLServer.Instance.photonView.RPC("AddNotificationLocalize", cachedFriendlyPlayerOfClass.GetPhotonPlayer(), new object[]
                            {
                        "[PL] has revealed the crew's reputation",
                        playerForPhotonPlayer.GetPlayerID(),
                        PLServer.Instance.GetEstimatedServerMs() + 6000,
                        true
                            });
                        }
                    }
                }
                return false;
            }
        }
        [HarmonyPatch(typeof(PLShipInfoBase), "ShouldBeHostileToShip")]
        class HostilityCode 
        {
            static bool Prefix(PLShipInfo __instance,ref bool __result, PLShipInfoBase inShip) 
            {
                if (inShip != null && !__instance.HostileShips.Contains(inShip.ShipID)) 
                {
                    if (inShip.GetIsPlayerShip() && PLServer.Instance.IsCrewRepRevealed && __instance.FactionID != 6 && __instance.FactionID != -1 && PLServer.Instance.RepLevels[__instance.FactionID] > 0 && __instance.ShipTypeID != EShipType.E_BEACON && !__instance.HasModifier(EShipModifierType.CORRUPTED))
                    {
                        __result = false;
                        return false;
                    }
                    else if (__instance.GetIsPlayerShip() && inShip.ShipTypeID != EShipType.E_BEACON && inShip.FactionID != 6 && inShip.FactionID != -1 && PLServer.Instance.RepLevels[inShip.FactionID] > 0 && inShip.HasModifier(EShipModifierType.CORRUPTED)) 
                    {
                        __result = false;
                        return false;
                    }
                }
                return true;
            }
        }
        [HarmonyPatch(typeof(PLShipInfo),"Update")]
        class ReflectWDPTFix 
        {
            static void Prefix(PLShipInfo __instance, ref List<GameObject> ___SysInstUIRoots, ref bool ___reflection_AppliedToShipWorldUI, ref Image ___DialogueChoiceBG, ref Image ___DialogueTextBG) 
            {
                if ((__instance.ShipTypeID == EShipType.E_WDCRUISER || __instance.ShipTypeID == EShipType.E_DESTROYER) && PLServer.Instance != null && ___DialogueChoiceBG != null && ___DialogueTextBG != null && ___SysInstUIRoots != null && ___SysInstUIRoots.Count > 0 && ___reflection_AppliedToShipWorldUI != PLServer.Instance.IsReflection_FlipIsActiveLocal)
                {
                    FlipLocalScale(___DialogueChoiceBG.transform);
                    FlipLocalScale(___DialogueTextBG.transform);
                    foreach (GameObject sysPower in ___SysInstUIRoots)
                    {
                        if (sysPower != null)
                        {
                            FlipLocalScale(sysPower.transform);
                        }
                    }
                }
                else if (PLServer.Instance != null && PLServer.Instance.GameHasStarted && __instance.ShipTypeID == EShipType.E_POLYTECH_SHIP && ___reflection_AppliedToShipWorldUI != PLServer.Instance.IsReflection_FlipIsActiveLocal)
                {
                    List<PLEngineerCoolantScreen> engineerCoolantScreens = new List<PLEngineerCoolantScreen>();
                    List<PLEngineerReactorScreen> engineerReactorScreens = new List<PLEngineerReactorScreen>();
                    List<PLEngineerAuxReactorScreen> engineeAuxScreens = new List<PLEngineerAuxReactorScreen>();
                    List<PLUIPilotingScreen> pilotingScreens = new List<PLUIPilotingScreen>();
                    foreach (PLUIScreen screen in __instance.MyScreenBase.AllScreens)
                    {
                        if (screen is PLEngineerCoolantScreen)
                        {
                            engineerCoolantScreens.Add(screen as PLEngineerCoolantScreen);
                        }
                        else if (screen is PLEngineerReactorScreen)
                        {
                            engineerReactorScreens.Add(screen as PLEngineerReactorScreen);
                        }
                        else if (screen is PLEngineerAuxReactorScreen)
                        {
                            engineeAuxScreens.Add(screen as PLEngineerAuxReactorScreen);
                        }
                        else if(screen is PLUIPilotingScreen) 
                        {
                            pilotingScreens.Add(screen as PLUIPilotingScreen);
                        }
                    }
                    foreach (PLEngineerCoolantScreen screen in engineerCoolantScreens)
                    {
                        Vector3 temp = screen.FuelPanel.transform.position;
                        screen.FuelPanel.transform.position = screen.DistressPanel.transform.position;
                        screen.DistressPanel.transform.position = temp;
                        temp = screen.CoolantPumpBtns[0].transform.position;
                        screen.CoolantPumpBtns[0].transform.position = screen.CoolantPumpBtns[2].transform.position;
                        screen.CoolantPumpBtns[2].transform.position = temp;
                        if (!___reflection_AppliedToShipWorldUI)
                        {
                            for (int i = 0; i < 3; i++)
                            {
                                screen.CoolantPumpBtns[i].transform.localPosition += new Vector3(30f, 0f, 0f);
                            }
                            screen.CoolantBarOutline.transform.localPosition += new Vector3(15f, 0f, 0f);
                            foreach (UILabel label in UnityEngine.Object.FindObjectsOfType<UILabel>())
                            {
                                if (label != null && label.text == PLLocalize.Localize("Coolant Reserves", false))
                                {
                                    label.transform.localPosition += new Vector3(15f, 0f, 0f);
                                }
                            }
                        }
                        else
                        {
                            for (int i = 0; i < 3; i++)
                            {
                                screen.CoolantPumpBtns[i].transform.localPosition -= new Vector3(30f, 0f, 0f);
                            }
                            screen.CoolantBarOutline.transform.localPosition -= new Vector3(15f, 0f, 0f);
                            foreach (UILabel label in UnityEngine.Object.FindObjectsOfType<UILabel>())
                            {
                                if (label != null && label.text == PLLocalize.Localize("Coolant Reserves", false))
                                {
                                    label.transform.localPosition -= new Vector3(15f, 0f, 0f);
                                }
                            }
                        }
                    }
                    foreach (PLEngineerReactorScreen screen in engineerReactorScreens)
                    {
                        if (!___reflection_AppliedToShipWorldUI)
                        {
                            screen.OCBtn.transform.localPosition = new Vector3(5.5f, -20f, 0f);
                            screen.OCBtnIcon.transform.localPosition = new Vector3(55f, -40f, 0f);
                            screen.StabilityBarLabel.transform.localPosition += new Vector3(70f, 0f, 0f);
                            screen.StabilityBarValueLabel.transform.localPosition += new Vector3(70f, 0f, 0f);
                            screen.TempBarOuter.transform.localPosition += new Vector3(70f, 0f, 0f);
                            screen.TempBarValueLabel.transform.localPosition += new Vector3(70f, 0f, 0f);
                            screen.TempBarValueLabel_Max.transform.localPosition += new Vector3(70f, 0f, 0f);
                            foreach (UILabel label in UnityEngine.Object.FindObjectsOfType<UILabel>())
                            {
                                if (label != null && label.text == PLLocalize.Localize("Engineering", false))
                                {
                                    label.transform.localPosition += new Vector3(490f, 0f, 0f);
                                }
                                else if (label != null && label.text == PLLocalize.Localize("Weapons", false))
                                {
                                    label.transform.localPosition += new Vector3(530f, 0f, 0f);
                                }
                            }
                            screen.m_EngineerLabels[0].transform.localPosition -= new Vector3(160f, 0f, 0f);
                            screen.m_EngineerLabels[3].transform.localPosition -= new Vector3(160f, 0f, 0f);
                            PLUIEditableReactorEditBar engine = screen.m_EditableBar[0];
                            engine.BarBG.transform.localPosition -= new Vector3(145f, 0f, 0f);
                            engine.BarInnerBG.transform.localPosition -= new Vector3(145f, 0f, 0f);
                            PLUIEditableReactorEditBar weapons = screen.m_EditableBar[3];
                            weapons.BarBG.transform.localPosition -= new Vector3(145f, 0f, 0f);
                            weapons.BarInnerBG.transform.localPosition -= new Vector3(145f, 0f, 0f);
                        }
                        else
                        {
                            screen.OCBtn.transform.localPosition = new Vector3(422.5f, -20f, 0f);
                            screen.OCBtnIcon.transform.localPosition = new Vector3(435f, -40f, 0f);
                            screen.StabilityBarLabel.transform.localPosition += new Vector3(-70f, 0f, 0f);
                            screen.StabilityBarValueLabel.transform.localPosition += new Vector3(-70f, 0f, 0f);
                            screen.TempBarOuter.transform.localPosition += new Vector3(-70f, 0f, 0f);
                            screen.TempBarValueLabel.transform.localPosition += new Vector3(-70f, 0f, 0f);
                            screen.TempBarValueLabel_Max.transform.localPosition += new Vector3(-70f, 0f, 0f);
                            foreach (UILabel label in UnityEngine.Object.FindObjectsOfType<UILabel>())
                            {
                                if (label != null && label.text == PLLocalize.Localize("Engineering", false))
                                {
                                    label.transform.localPosition -= new Vector3(490f, 0f, 0f);
                                }
                                else if (label != null && label.text == PLLocalize.Localize("Weapons", false))
                                {
                                    label.transform.localPosition -= new Vector3(530f, 0f, 0f);
                                }
                            }
                            screen.m_EngineerLabels[0].transform.localPosition += new Vector3(160f, 0f, 0f);
                            screen.m_EngineerLabels[3].transform.localPosition += new Vector3(160f, 0f, 0f);
                            PLUIEditableReactorEditBar engine = screen.m_EditableBar[0];
                            engine.BarBG.transform.localPosition += new Vector3(145f, 0f, 0f);
                            engine.BarInnerBG.transform.localPosition += new Vector3(145f, 0f, 0f);
                            PLUIEditableReactorEditBar weapons = screen.m_EditableBar[3];
                            weapons.BarBG.transform.localPosition += new Vector3(145f, 0f, 0f);
                            weapons.BarInnerBG.transform.localPosition += new Vector3(145f, 0f, 0f);
                        }
                        FlipLocalScale(screen.OCBtnIcon.transform);
                    }
                    foreach (PLEngineerAuxReactorScreen screen in engineeAuxScreens)
                    {
                        if (!___reflection_AppliedToShipWorldUI)
                        {
                            foreach (UILabel label in screen.AllAuxSystemPowerLabel_On)
                            {
                                label.transform.localPosition -= new Vector3(330, 1, 1);
                            }
                            foreach (UILabel label in screen.AllAuxSystemPowerLabel_Off)
                            {
                                label.transform.localPosition -= new Vector3(330, 1, 1);
                            }
                            foreach (UILabel label in screen.AllAuxSystemPowerLabel)
                            {
                                label.transform.localPosition += new Vector3(170, 1, 1);
                            }
                            foreach (UILabel label in screen.AllAuxSystemNameLabel)
                            {
                                label.transform.localPosition += new Vector3(160, 1, 1);
                            }
                        }
                        else
                        {
                            foreach (UILabel label in screen.AllAuxSystemPowerLabel_On)
                            {
                                label.transform.localPosition += new Vector3(330, 1, 1);
                            }
                            foreach (UILabel label in screen.AllAuxSystemPowerLabel_Off)
                            {
                                label.transform.localPosition += new Vector3(330, 1, 1);
                            }
                            foreach (UILabel label in screen.AllAuxSystemPowerLabel)
                            {
                                label.transform.localPosition -= new Vector3(170, 1, 1);
                            }
                            foreach (UILabel label in screen.AllAuxSystemNameLabel)
                            {
                                label.transform.localPosition -= new Vector3(160, 1, 1);
                            }
                            foreach (UILabel label in UnityEngine.Object.FindObjectsOfType<UILabel>())
                            {
                                if (label != null && label.text == PLLocalize.Localize("Engineering", false))
                                {
                                    label.transform.localPosition -= new Vector3(345f, 0f, 0f);
                                }
                                else if(label != null && label.text == PLLocalize.Localize("Weapons", false)) 
                                {
                                    label.transform.localPosition -= new Vector3(385f, 0f, 0f);
                                }
                            }
                        }
                    }
                    foreach (PLUIPilotingScreen screen in pilotingScreens) 
                    {
                        if (!___reflection_AppliedToShipWorldUI) 
                        {
                            screen.BinaryThrust.transform.localPosition += new Vector3(253f, 0f, 0f);
                            screen.PreciseThrust.transform.localPosition += new Vector3(253f, 0f, 0f);
                            screen.Sectors_Enabled.transform.localPosition += new Vector3(253f, 0f, 0f);
                            screen.Sectors_Disabled.transform.localPosition += new Vector3(253f, 0f, 0f);
                            foreach (UILabel label in UnityEngine.Object.FindObjectsOfType<UILabel>())
                            {
                                if (label != null && label.text == PLLocalize.Localize("Thrust Controls", false))
                                {
                                    label.transform.localPosition += new Vector3(248f, 0f, 0f);
                                }
                                else if (label != null && label.text == PLLocalize.Localize("Show Sectors", false))
                                {
                                    label.transform.localPosition += new Vector3(263f, 0f, 0f);
                                }
                            }
                        }
                        else 
                        {
                            screen.BinaryThrust.transform.localPosition -= new Vector3(253f, 0f, 0f);
                            screen.PreciseThrust.transform.localPosition -= new Vector3(253f, 0f, 0f);
                            screen.Sectors_Enabled.transform.localPosition -= new Vector3(253f, 0f, 0f);
                            screen.Sectors_Disabled.transform.localPosition -= new Vector3(253f, 0f, 0f);
                            foreach (UILabel label in UnityEngine.Object.FindObjectsOfType<UILabel>())
                            {
                                if (label != null && label.text == PLLocalize.Localize("Thrust Controls", false))
                                {
                                    label.transform.localPosition -= new Vector3(248f, 0f, 0f);
                                }
                                else if (label != null && label.text == PLLocalize.Localize("Show Sectors", false))
                                {
                                    label.transform.localPosition -= new Vector3(263f, 0f, 0f);
                                }
                            }
                        }
                    }
                }
            }
            private static void FlipLocalScale(Transform targetTransform)
            {
                Vector3 localScale = targetTransform.localScale;
                localScale.Scale(new Vector3(-1, 1, 1));
                targetTransform.localScale = localScale;
            }
        }
        [HarmonyPatch(typeof(PLServer), "SpawnPlayerShip")]
        class NoAOGStart 
        {
            static void Postfix(PLServer __instance) 
            {
                if (PLEncounterManager.Instance.PlayerShip.ShipTypeID == EShipType.E_CIVILIAN_STARTING_SHIP || PLEncounterManager.Instance.PlayerShip.ShipTypeID == EShipType.OLDWARS_HUMAN)
                {
                    PLEncounterManager.Instance.PlayerShip.FactionID = -1;
                }
            }
        }
        [HarmonyPatch(typeof(PLServer), "OnGameOver")]
        class FixTimerReset 
        {
            private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
            {
                List<CodeInstruction> targetSequence = new List<CodeInstruction>
                {
                new CodeInstruction(OpCodes.Ldarg_0),
                new CodeInstruction(OpCodes.Ldc_R4,0f),
                new CodeInstruction(OpCodes.Call,AccessTools.Method(typeof(ObscuredFloat),"op_Implicit",new Type[]{typeof(float)})),
                new CodeInstruction(OpCodes.Stfld, AccessTools.Field(typeof(PLServer),"Playtime")),
                };
                List<CodeInstruction> patchSequence = new List<CodeInstruction>
                {
                };
                return PatchBySequence(instructions, targetSequence, patchSequence, PatchMode.REPLACE, CheckMode.NONNULL, false);
            }
        }
        [HarmonyPatch(typeof(PLNetworkManager), "GameOver")]
        class FixJumpAndEnemiReset 
        {
            private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
            {
                List<CodeInstruction> targetSequence = new List<CodeInstruction>
                {
                new CodeInstruction(OpCodes.Ldarg_0),
                new CodeInstruction(OpCodes.Ldc_I4_0),
                new CodeInstruction(OpCodes.Call,AccessTools.Method(typeof(ObscuredInt),"op_Implicit",new Type[]{typeof(int)})),
                new CodeInstruction(OpCodes.Stfld, AccessTools.Field(typeof(PLNetworkManager),"NumJumps")),
                };
                List<CodeInstruction> patchSequence = new List<CodeInstruction>
                {
                };
                IEnumerable<CodeInstruction> moded = PatchBySequence(instructions, targetSequence, patchSequence, PatchMode.REPLACE, CheckMode.NONNULL, false);
                targetSequence = new List<CodeInstruction>
                {
                new CodeInstruction(OpCodes.Ldarg_0),
                new CodeInstruction(OpCodes.Ldc_I4_0),
                new CodeInstruction(OpCodes.Call,AccessTools.Method(typeof(ObscuredInt),"op_Implicit",new Type[]{typeof(int)})),
                new CodeInstruction(OpCodes.Stfld, AccessTools.Field(typeof(PLNetworkManager),"NumEnemiesDestroyed")),
                };
                return PatchBySequence(moded, targetSequence, patchSequence, PatchMode.REPLACE, CheckMode.NONNULL, false);
            }
        }
        [HarmonyPatch(typeof(PLSylvassiCypher),"Update")]
        class SyncedChyper 
        {
            static void Prefix(PLSylvassiCypher __instance) 
            {
                if (PLServer.Instance != null && !PhotonNetwork.isMasterClient)
                {
                    int sad = 0;
                    PLRand rand = new PLRand(PLServer.Instance.GalaxySeed + PLServer.Instance.GetCurrentHubID());
                    sad = rand.Next(0, __instance.DesignMaterials.Length);
                    for(int i = 0; i < __instance.RingOffsets.Length; i++) 
                    {
                        if (i <= __instance.SetupFor_PlayerCount)
                        {
                            sad = rand.Next(1, 8);
                        }
                    }
                    __instance.CenterCore.transform.localEulerAngles = new Vector3(__instance.CenterCore.transform.localEulerAngles.x, __instance.CenterCore.transform.localEulerAngles.y, rand.NextFloat() * 360f);
                }
            }
        }
        [HarmonyPatch(typeof(PLPawn),"Update")]
        class HumanOxygenDamage 
        {
            static void Postfix(PLPawn __instance) 
            {
                if (__instance.MyPlayer != null && __instance.MyPlayer.OnPlanet && __instance.PawnType == EPawnType.E_CREWMAN && __instance.MyPlayer.RaceID == 0 && PLNetworkManager.Instance.LocalPlayer == __instance.MyPlayer && !__instance.ExosuitIsActive)
                {
                    float num8 = 3f * (1f + (float)__instance.MyPlayer.Talents[1] * 0.2f);
                    foreach (PawnStatusEffect pawnStatusEffect4 in __instance.MyStatusEffects)
                    {
                        if (pawnStatusEffect4 != null && pawnStatusEffect4.Type == EPawnStatusEffectType.OXYGEN)
                        {
                            num8 += 4f * pawnStatusEffect4.Strength;
                        }
                    }
                    bool flag2 = false;
                    if (__instance.CurrentShip != null)
                    {
                        if (__instance.CurrentShip.MyStats.OxygenLevel <= 0f)
                        {
                            flag2 = true;
                        }
                    }
                    else
                    {
                        AtmoSettings atmoSettings2 = __instance.GetAtmoSettings(false);
                        if (atmoSettings2 != null && !atmoSettings2.Oxygen)
                        {
                            flag2 = true;
                        }
                    }
                    if (flag2 && Time.time - __instance.OxygenTakeDamageTime > num8)
                    {
                        __instance.OxygenTakeDamageTime = Time.time;
                        __instance.photonView.RPC("TakeOxygenDamage", PhotonTargets.All, new object[]
                        {
                    15f
                        });
                    }
                }
            }
        }
        [HarmonyPatch(typeof(PLLiarsDiceGame), "MovePlayerToSafeSpot")]
        class LiarsDiceFix 
        {
            static void Postfix() 
            {
                Physics.SyncTransforms();
            }
        }
        [HarmonyPatch(typeof(PLRacingLevelShipInfo), "SetupShipStats")]
        class FixRacingRep 
        {
            static void Postfix(PLRacingLevelShipInfo __instance) 
            {
                __instance.NoRepLossOnKilled = true;
            }
        }
        [HarmonyPatch(typeof(PLGalaxy), "GetFactionColorForID")]
        class FixNoFactionPlayerLog 
        {
            static bool Prefix(PLGalaxy __instance, int inID, ref Color __result) 
            {
                if (inID >= __instance.FactionColors.Length)
                {
                    UnityEngine.Debug.Log("invalid id: GetFactionColorForID " + inID.ToString());
                    __result = Color.white;
                    return false;
                }
                if (inID == -1)
                {
                    __result = Color.white;
                    return false;
                }
                if (inID == 4)
                {
                    __result = Color.Lerp(Color.red, Color.red * 0.8f, Time.time % 1f);
                    return false;
                }
                __result = __instance.FactionColors[inID];
                return false;
            }
        }
        [HarmonyPatch(typeof(PLPawn),"Update")]
        class FixOxygenOnExosuit 
        {
            static void Postfix(PLPawn __instance) 
            {
                if (__instance.MyPlayer != null && !__instance.MyPlayer.OnPlanet && __instance.CurrentShip != null && __instance.MyPlayer.RaceID == 0 && __instance.GetExosuitIsActive())
                {
                    __instance.CurrentShip.MyStats.OxygenLevel += Time.deltaTime * 0.002f;
                }
            }
        }
        [HarmonyPatch(typeof(PLShipInfoBase), "Update")]
        class FixReactorMeltdownAndCoolDown 
        {
            static void Prefix(PLShipInfoBase __instance,out PLReactor __state) 
            {
                __state = __instance.MyReactor;
            }

            static void Postfix(PLShipInfoBase __instance, PLReactor __state) 
            {
                if(__state != __instance.MyReactor && __instance.MyReactor != null && __instance.MyStats != null) 
                {
                    foreach(PLEnergySphere sphere in UnityEngine.Object.FindObjectsOfType<PLEnergySphere>())
                    {
                        if(sphere.MyOwner == __instance && !sphere.DisableSelfDetonation) 
                        {
                            sphere.Detonate();
                        }
                    }
                    __instance.LastReactorMeltdownBeginTime = int.MinValue;
                    __instance.ReactorOverheatTime = -99999f;
                    __instance.MyStats.ReactorTempCurrent = 0;
                }
            }
        }
    }
}
