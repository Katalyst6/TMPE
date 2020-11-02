namespace TrafficManager.Custom.PathFinding {
    using ColossalFramework;
    using CSUtil.Commons;
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using TrafficManager.RedirectionFramework.Attributes;
    using UnityEngine;

    [TargetType(typeof(VehicleManager))]
    public class CustomVehicleManager {

        [RedirectMethod]
        public void UpdateParkedVehicles(float minX, float minZ, float maxX, float maxZ) {
            Log._Debug($"UpdateParkedVehicles From ({minX}, {minZ}) To From ({maxX}, {maxZ})");

            var radius = 10f;

            var gridMinX = Mathf.Max((int)((minX - radius) / 32f + 270f), 0);
            var gridMinZ = Mathf.Max((int)((minZ - radius) / 32f + 270f), 0);
            var gridMaxX = Mathf.Min((int)((maxX + radius) / 32f + 270f), 539);
            var gridMaxZ = Mathf.Min((int)((maxZ + radius) / 32f + 270f), 539);

            var parkingIds = GetParkingIds(gridMinX, gridMinZ, gridMaxX, gridMaxZ).ToArray();
            Log._Debug($"{parkingIds.Count()} parking ids found");

            if (!parkingIds.Any()) {
                return;
            }

            Constants
                .ManagerFactory
                .ParkingManager
                .QueueParkedVehicleCheckups(parkingIds);
        }

        private ushort GetParkingId(int gridX, int gridZ) {
            return VehicleManager.instance.m_parkedGrid[gridZ * VehicleManager.VEHICLEGRID_RESOLUTION + gridX];
        }

        private IEnumerable<ushort> GetParkingIds(int gridMinX, int gridMinZ, int gridMaxX, int gridMaxZ) {
            Log._Debug($"Getting parking ids From ({gridMinX}, {gridMinZ}) To From ({gridMaxX}, {gridMaxZ})");

            for (var z = gridMinZ; z <= gridMaxZ; z++) {
                for (var x = gridMinX; x <= gridMaxX; x++) {
                    var parkingId = GetParkingId(x, z);
                    var num6 = 0;
                    while (parkingId != 0) {
                        yield return parkingId;

                        var vehicleParked = VehicleManager.instance.m_parkedVehicles.m_buffer[parkingId];

                        parkingId = vehicleParked.m_nextGridParked;

                        if (++num6 > 32768) {
                            CODebugBase<LogChannel>.Error(LogChannel.Core, "Invalid list detected!\n" + Environment.StackTrace);
                            break;
                        }
                    }
                }
            }
        }

        [RedirectMethod]
        public void SimulationStepImpl(int subStep) {

            // Removed: logic too heavy to be executed on the Simulation step
            //if (m_parkedUpdated) {
            //    int num = m_updatedParked.Length;
            //    for (int i = 0; i < num; i++) {
            //        ulong num2 = m_updatedParked[i];
            //        if (num2 == 0) {
            //            continue;
            //        }
            //        m_updatedParked[i] = 0uL;
            //        for (int j = 0; j < 64; j++) {
            //            if ((num2 & (ulong)(1L << j)) != 0) {
            //                ushort num3 = (ushort)((i << 6) | j);
            //                VehicleInfo info = m_parkedVehicles.m_buffer[num3].Info;
            //                m_parkedVehicles.m_buffer[num3].m_flags &= 65531;
            //                info.m_vehicleAI.UpdateParkedVehicle(num3, ref m_parkedVehicles.m_buffer[num3]);
            //            }
            //        }
            //    }
            //    m_parkedUpdated = false;
            //}

            var original = VehicleManager.instance;

            if (subStep == 0) {
                return;
            }

            SimulationManager instance = Singleton<SimulationManager>.instance;
            Vector3 physicsLodRefPos = instance.m_simulationView.m_position + instance.m_simulationView.m_direction * 1000f;
            for (int k = 0; k < 16384; k++) {
                Vehicle.Flags flags = original.m_vehicles.m_buffer[k].m_flags;
                if ((flags & Vehicle.Flags.Created) != 0 && original.m_vehicles.m_buffer[k].m_leadingVehicle == 0) {
                    VehicleInfo info2 = original.m_vehicles.m_buffer[k].Info;
                    info2.m_vehicleAI.ExtraSimulationStep((ushort)k, ref original.m_vehicles.m_buffer[k]);
                }
            }
            int num4 = (int)(instance.m_currentFrameIndex & 0xF);
            int num5 = num4 * 1024;
            int num6 = (num4 + 1) * 1024 - 1;
            for (int l = num5; l <= num6; l++) {
                Vehicle.Flags flags2 = original.m_vehicles.m_buffer[l].m_flags;
                if ((flags2 & Vehicle.Flags.Created) != 0 && original.m_vehicles.m_buffer[l].m_leadingVehicle == 0) {
                    VehicleInfo info3 = original.m_vehicles.m_buffer[l].Info;
                    info3.m_vehicleAI.SimulationStep((ushort)l, ref original.m_vehicles.m_buffer[l], physicsLodRefPos);
                }
            }
            if ((instance.m_currentFrameIndex & 0xFF) == 0) {
                uint num7 = original.m_maxTrafficFlow / 100u;
                if (num7 == 0) {
                    num7 = 1u;
                }
                uint num8 = original.m_totalTrafficFlow / num7;
                if (num8 > 100) {
                    num8 = 100u;
                }
                original.m_lastTrafficFlow = num8;
                original.m_totalTrafficFlow = 0u;
                original.m_maxTrafficFlow = 0u;
                StatisticsManager instance2 = Singleton<StatisticsManager>.instance;
                StatisticBase statisticBase = instance2.Acquire<StatisticInt32>(StatisticType.TrafficFlow);
                statisticBase.Set((int)num8);
            }
        }
    }
}
