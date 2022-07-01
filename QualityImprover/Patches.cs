using HarmonyLib;
using System.Collections.Generic;
using System;
using UnityEngine;
using CodeStage.AntiCheat.ObscuredTypes;
namespace QualityImprover
{
    internal class Patches
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
    }
}
