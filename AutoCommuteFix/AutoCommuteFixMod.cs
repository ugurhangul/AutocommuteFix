using System;
using System.IO;
using System.Linq;
using Accord.Math.Optimization;
using Il2Cpp;
using Il2CppAIPathfinding;
using MelonLoader;
using MelonLoader.Utils;
using UnityEngine;

namespace AutoCommuteFix
{
    public class AutoCommuteMod : MelonMod
    {
        private readonly KeyCode _triggerKey = KeyCode.K;
        private GameManager _gameManager;

        public override void OnUpdate()
        {
            if (!Input.GetKey(KeyCode.LeftControl) && !Input.GetKey(KeyCode.RightControl) || !Input.GetKeyDown(_triggerKey))
                return;
            _gameManager = GameObject.Find("GameManager").GetComponent<GameManager>();
            LoggerInstance.Msg("Base dir: " + MelonEnvironment.MelonBaseDirectory);
            LoggerInstance.Msg("Game dir: " + MelonEnvironment.GameRootDirectory);
            LoggerInstance.Msg("User data: " + MelonEnvironment.UserDataDirectory);
            LoggerInstance.Msg("User libs: " + MelonEnvironment.UserLibsDirectory);
            if (_gameManager != null && _gameManager.aiPathfinder != null)
            {
                var villagers = _gameManager.resourceManager.villagers;
                var gameObjectList1 = new System.Collections.Generic.List<GameObject>();
                var gameObjectList2 = new System.Collections.Generic.List<GameObject>();
                var iplaceOfWorkList = new System.Collections.Generic.List<IPlaceOfWork>();
                var villagerList = new System.Collections.Generic.List<Villager>();
                var num = 0;
                foreach (var villager in villagers)
                {
                    if (villager.occupation.occupation != VillagerOccupation.Occupation.Child)
                    {
                        if (villager.occupation.occupation == VillagerOccupation.Occupation.Farmer)
                            ++num;
                        villagerList.Add(villager);
                        if (villager.residenceGO == null)
                        {
                            LoggerInstance.Msg("NULL home residence, exiting calculation process");
                            return;
                        }
                        gameObjectList1.Add(villager.residenceGO);
                        gameObjectList2.Add(villager.placeOfWork?.Cast<IRegistersForWork>().gameObject);
                        iplaceOfWorkList.Add(villager.placeOfWork);
                    }
                }
                LoggerInstance.Msg($"WORKING VILLAGERS: {villagerList.Count}");
                var costMatrix = new double[villagerList.Count][];
                LoggerInstance.Msg($"About to create matrix at {DateTime.Now}");
                for (var index1 = 0; index1 < villagerList.Count; ++index1)
                {
                    costMatrix[index1] = new double[gameObjectList2.Count];
                    if (gameObjectList1[index1] == null)
                    {
                        LoggerInstance.Msg("NULL home residence, exiting calculation process");
                        return;
                    }

                    var gridNodeForObject1 = _gameManager.aiPathfinder.GetPathCheckGridNodeForObject(gameObjectList1[index1], AIGridGraph.FloodFillType.WallsBlock, true, out _);
                    for (var index2 = 0; index2 < gameObjectList2.Count; ++index2)
                    {
                        if (gameObjectList2[index2] == null)
                        {
                            costMatrix[index1][index2] = 1000000.0;
                        }
                        else
                        {
                            var gridNodeForObject2 = _gameManager.aiPathfinder.GetPathCheckGridNodeForObject(gameObjectList2[index2], AIGridGraph.FloodFillType.WallsBlock, true, out _);
                            costMatrix[index1][index2] = _gameManager.aiPathfinder.GetDistance(gridNodeForObject1, gridNodeForObject2);
                        }
                    }
                }
                LoggerInstance.Msg($"Finished Matrix at {DateTime.Now}. Starting munkres");
                var munkres = new Munkres(costMatrix);
                munkres.Minimize();
                var solution = munkres.Solution;
                LoggerInstance.Msg($"Finished munkres at {DateTime.Now}. Min cost: {(object)munkres.Value}");
                foreach (var villager in villagerList)
                {
                    villager.FireWorker(villager.placeOfWork);
                    villager.SetOccupation(VillagerOccupation.Occupation.Laborer);
                }
                for (var index = 0; index < villagerList.Count; ++index)
                {
                    var villager = villagerList[index];
                    var iplaceOfWork = iplaceOfWorkList[(int)solution[index]];
                    if (iplaceOfWork == null)
                    {
                        if (num > 0)
                        {
                            villager.SetOccupation(VillagerOccupation.Occupation.Farmer);
                            --num;
                        }
                    }
                    else
                    {
                        LoggerInstance.Msg("Matched building: " + gameObjectList2[(int)solution[index]].name);
                        var workAt = iplaceOfWork.RequestToWorkAt(villager.Cast<IWorker>());
                        LoggerInstance.Msg(workAt);
                        if (workAt)
                            villager.SetOccupation(iplaceOfWork.employmentOccupation);
                    }
                }
            }
        }

  
    }
}
