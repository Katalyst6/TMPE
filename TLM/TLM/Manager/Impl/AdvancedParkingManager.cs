namespace TrafficManager.Manager.Impl {
    using ColossalFramework;
    using ColossalFramework.Globalization;
    using ColossalFramework.Math;
    using CSUtil.Commons;
    using JetBrains.Annotations;
    using TrafficManager.API.Manager;
    using TrafficManager.API.Traffic.Data;
    using TrafficManager.API.Traffic.Enums;
    using TrafficManager.Custom.PathFinding;
    using TrafficManager.State;
    using TrafficManager.State.ConfigData;
    using TrafficManager.UI;
    using UnityEngine;

    public class AdvancedParkingManager
        : AbstractFeatureManager,
          IAdvancedParkingManager
    {
        public static readonly AdvancedParkingManager Instance
            = new AdvancedParkingManager();

        protected override void OnDisableFeatureInternal() {
            for (var citizenInstanceId = 0;
                 citizenInstanceId < ExtCitizenInstanceManager.Instance.ExtInstances.Length;
                 ++citizenInstanceId) {
                ExtPathMode pathMode = ExtCitizenInstanceManager
                               .Instance.ExtInstances[citizenInstanceId].pathMode;
                switch (pathMode) {
                    case ExtPathMode.RequiresWalkingPathToParkedCar:
                    case ExtPathMode.CalculatingWalkingPathToParkedCar:
                    case ExtPathMode.WalkingToParkedCar:
                    case ExtPathMode.ApproachingParkedCar: {
                        // citizen requires a path to their parked car: release instance to prevent
                        // it from floating
                        Services.CitizenService.ReleaseCitizenInstance((ushort)citizenInstanceId);
                        break;
                    }

                    case ExtPathMode.RequiresCarPath:
                    case ExtPathMode.RequiresMixedCarPathToTarget:
                    case ExtPathMode.CalculatingCarPathToKnownParkPos:
                    case ExtPathMode.CalculatingCarPathToTarget:
                    case ExtPathMode.DrivingToKnownParkPos:
                    case ExtPathMode.DrivingToTarget: {
                        if (Services.CitizenService.CheckCitizenInstanceFlags(
                            (ushort)citizenInstanceId,
                            CitizenInstance.Flags.Character)) {
                            // citizen instance requires a car but is walking: release instance to
                            // prevent it from floating
                            Services.CitizenService.ReleaseCitizenInstance(
                                (ushort)citizenInstanceId);
                        }

                        break;
                    }
                }
            }

            ExtCitizenManager.Instance.Reset();
            ExtCitizenInstanceManager.Instance.Reset();
        }

        protected override void OnEnableFeatureInternal() {
        }

        public bool EnterParkedCar(ushort instanceId,
                                   ref CitizenInstance instanceData,
                                   ushort parkedVehicleId,
                                   out ushort vehicleId) {
#if DEBUG
            bool citizenDebug =
                (DebugSettings.CitizenInstanceId == 0
                 || DebugSettings.CitizenInstanceId == instanceId)
                && (DebugSettings.CitizenId == 0
                    || DebugSettings.CitizenId == instanceData.m_citizen)
                && (DebugSettings.SourceBuildingId == 0
                    || DebugSettings.SourceBuildingId == instanceData.m_sourceBuilding)
                && (DebugSettings.TargetBuildingId == 0
                    || DebugSettings.TargetBuildingId == instanceData.m_targetBuilding);

            bool logParkingAi = DebugSwitch.BasicParkingAILog.Get() && citizenDebug;
            bool extendedLogParkingAi = DebugSwitch.ExtendedParkingAILog.Get() && citizenDebug;

            Log._TraceIf(
                logParkingAi,
                () => $"CustomHumanAI.EnterParkedCar({instanceId}, ..., {parkedVehicleId}) called.");
#else
            const bool logParkingAi = false;
            const bool extendedLogParkingAi = false;
#endif
            VehicleManager vehManager = Singleton<VehicleManager>.instance;
            NetManager netManager = Singleton<NetManager>.instance;
            CitizenManager citManager = Singleton<CitizenManager>.instance;

            Vector3 parkedVehPos = vehManager.m_parkedVehicles.m_buffer[parkedVehicleId].m_position;
            Quaternion parkedVehRot =
                vehManager.m_parkedVehicles.m_buffer[parkedVehicleId].m_rotation;
            VehicleInfo vehicleInfo = vehManager.m_parkedVehicles.m_buffer[parkedVehicleId].Info;

            if (!CustomPathManager._instance.m_pathUnits.m_buffer[instanceData.m_path]
                                  .GetPosition(0, out PathUnit.Position vehLanePathPos)) {
                Log._TraceIf(
                    logParkingAi,
                    () => $"CustomHumanAI.EnterParkedCar({instanceId}): Could not get first car " +
                          $"path position of citizen instance {instanceId}!");
                vehicleId = 0;
                return false;
            }

            uint vehLaneId = PathManager.GetLaneID(vehLanePathPos);
            Log._TraceIf(
                extendedLogParkingAi,
                () => $"CustomHumanAI.EnterParkedCar({instanceId}): Determined vehicle " +
                      $"position for citizen instance {instanceId}: seg. {vehLanePathPos.m_segment}, " +
                      $"lane {vehLanePathPos.m_lane}, off {vehLanePathPos.m_offset} (lane id {vehLaneId})");

            netManager.m_lanes.m_buffer[vehLaneId].GetClosestPosition(
                parkedVehPos,
                out Vector3 vehLanePos,
                out float vehLaneOff);

            var vehLaneOffset = (byte)Mathf.Clamp(Mathf.RoundToInt(vehLaneOff * 255f), 0, 255);

            // movement vector from parked vehicle position to road position
            // Vector3 forwardVector =
            //    parkedVehPos + Vector3.ClampMagnitude(vehLanePos - parkedVehPos, 5f);

            if (vehManager.CreateVehicle(
                out vehicleId,
                ref Singleton<SimulationManager>.instance.m_randomizer,
                vehicleInfo,
                parkedVehPos,
                TransferManager.TransferReason.None,
                false,
                false)) {
                // update frame data
                Vehicle.Frame frame = vehManager.m_vehicles.m_buffer[vehicleId].m_frame0;
                frame.m_rotation = parkedVehRot;

                vehManager.m_vehicles.m_buffer[vehicleId].m_frame0 = frame;
                vehManager.m_vehicles.m_buffer[vehicleId].m_frame1 = frame;
                vehManager.m_vehicles.m_buffer[vehicleId].m_frame2 = frame;
                vehManager.m_vehicles.m_buffer[vehicleId].m_frame3 = frame;
                vehicleInfo.m_vehicleAI.FrameDataUpdated(
                    vehicleId,
                    ref vehManager.m_vehicles.m_buffer[vehicleId],
                    ref frame);

                // update vehicle target position
                vehManager.m_vehicles.m_buffer[vehicleId].m_targetPos0 = new Vector4(
                    vehLanePos.x,
                    vehLanePos.y,
                    vehLanePos.z,
                    2f);

                // update other fields
                vehManager.m_vehicles.m_buffer[vehicleId].m_flags =
                    vehManager.m_vehicles.m_buffer[vehicleId].m_flags | Vehicle.Flags.Stopped;

                vehManager.m_vehicles.m_buffer[vehicleId].m_path = instanceData.m_path;
                vehManager.m_vehicles.m_buffer[vehicleId].m_pathPositionIndex = 0;
                vehManager.m_vehicles.m_buffer[vehicleId].m_lastPathOffset = vehLaneOffset;
                vehManager.m_vehicles.m_buffer[vehicleId].m_transferSize =
                    (ushort)(instanceData.m_citizen & 65535u);

                if (!vehicleInfo.m_vehicleAI.TrySpawn(
                        vehicleId,
                        ref vehManager.m_vehicles.m_buffer[vehicleId])) {
                    Log._TraceIf(
                        logParkingAi,
                        () => $"CustomHumanAI.EnterParkedCar({instanceId}): Could not " +
                              $"spawn a {vehicleInfo.m_vehicleType} for citizen instance {instanceId}!");
                    return false;
                }

                // change instances
                InstanceID parkedVehInstance = InstanceID.Empty;
                parkedVehInstance.ParkedVehicle = parkedVehicleId;
                InstanceID vehInstance = InstanceID.Empty;
                vehInstance.Vehicle = vehicleId;
                Singleton<InstanceManager>.instance.ChangeInstance(parkedVehInstance, vehInstance);

                // set vehicle id for citizen instance
                instanceData.m_path = 0u;
                citManager.m_citizens.m_buffer[instanceData.m_citizen]
                          .SetParkedVehicle(instanceData.m_citizen, 0);
                citManager.m_citizens.m_buffer[instanceData.m_citizen]
                          .SetVehicle(instanceData.m_citizen, vehicleId, 0u);

                // update citizen instance flags
                instanceData.m_flags &= ~CitizenInstance.Flags.WaitingPath;
                instanceData.m_flags &= ~CitizenInstance.Flags.EnteringVehicle;
                instanceData.m_flags &= ~CitizenInstance.Flags.TryingSpawnVehicle;
                instanceData.m_flags &= ~CitizenInstance.Flags.BoredOfWaiting;
                instanceData.m_waitCounter = 0;

                // unspawn citizen instance
                instanceData.Unspawn(instanceId);

                if (extendedLogParkingAi) {
                    Log._Trace(
                        $"CustomHumanAI.EnterParkedCar({instanceId}): Citizen instance " +
                        $"{instanceId} is now entering vehicle {vehicleId}. Set vehicle " +
                        $"target position to {vehLanePos} (segment={vehLanePathPos.m_segment}, " +
                        $"lane={vehLanePathPos.m_lane}, offset={vehLanePathPos.m_offset})");
                }

                return true;
            }

            // failed to find a road position
            Log._TraceIf(
                logParkingAi,
                () => $"CustomHumanAI.EnterParkedCar({instanceId}): Could not " +
                      $"find a road position for citizen instance {instanceId} near " +
                      $"parked vehicle {parkedVehicleId}!");
            return false;
        }

        public ExtSoftPathState UpdateCitizenPathState(ushort citizenInstanceId,
                                                       ref CitizenInstance citizenInstance,
                                                       ref ExtCitizenInstance extInstance,
                                                       ref ExtCitizen extCitizen,
                                                       ref Citizen citizen,
                                                       ExtPathState mainPathState) {
#if DEBUG
            bool citizenDebug =
                (DebugSettings.CitizenInstanceId == 0
                 || DebugSettings.CitizenInstanceId == citizenInstanceId)
                && (DebugSettings.CitizenId == 0
                    || DebugSettings.CitizenId == citizenInstance.m_citizen)
                && (DebugSettings.SourceBuildingId == 0
                    || DebugSettings.SourceBuildingId == citizenInstance.m_sourceBuilding)
                && (DebugSettings.TargetBuildingId == 0
                    || DebugSettings.TargetBuildingId == citizenInstance.m_targetBuilding);

            bool logParkingAi = DebugSwitch.BasicParkingAILog.Get() && citizenDebug;
            bool extendedLogParkingAi = DebugSwitch.ExtendedParkingAILog.Get() && citizenDebug;
#else
            const bool logParkingAi = false;
            const bool extendedLogParkingAi = false;
#endif
            Log._TraceIf(
                extendedLogParkingAi,
                () => $"AdvancedParkingManager.UpdateCitizenPathState({citizenInstanceId}, ..., " +
                $"{mainPathState}) called.");

            if (mainPathState == ExtPathState.Calculating) {
                // main path is still calculating, do not check return path
                Log._TraceIf(
                    extendedLogParkingAi,
                    () => $"AdvancedParkingManager.UpdateCitizenPathState({citizenInstanceId}, ..., " +
                    $"{mainPathState}): still calculating main path. returning CALCULATING.");

                return ExtCitizenInstance.ConvertPathStateToSoftPathState(mainPathState);
            }

            IExtCitizenInstanceManager extCitInstMan = Constants.ManagerFactory.ExtCitizenInstanceManager;

            // if (!Constants.ManagerFactory.ExtCitizenInstanceManager.IsValid(citizenInstanceId)) {
            // // no citizen
            //#if DEBUG
            // if (debug)
            //  Log._Debug($"AdvancedParkingManager.UpdateCitizenPathState({citizenInstanceId}, ..., {mainPathState}): no citizen found!");
            //#endif
            //  return ExtCitizenInstance.ConvertPathStateToSoftPathState(mainPathState);
            // }

            if (mainPathState == ExtPathState.None || mainPathState == ExtPathState.Failed) {
                // main path failed or non-existing
                Log._TraceIf(
                    logParkingAi,
                    () => $"AdvancedParkingManager.UpdateCitizenPathState({citizenInstanceId}, ..., " +
                    $"{mainPathState}): mainPathSate is {mainPathState}.");

                if (mainPathState == ExtPathState.Failed) {
                    Log._TraceIf(
                        logParkingAi,
                        () => $"AdvancedParkingManager.UpdateCitizenPathState({citizenInstanceId}, " +
                        $"..., {mainPathState}): Checking if path-finding may be repeated.");

                    return OnCitizenPathFindFailure(
                        citizenInstanceId,
                        ref citizenInstance,
                        ref extInstance,
                        ref extCitizen);
                }

                Log._TraceIf(
                    logParkingAi,
                    () => $"AdvancedParkingManager.UpdateCitizenPathState({citizenInstanceId}, ..., " +
                    $"{mainPathState}): Resetting instance and returning FAILED.");

                extCitInstMan.Reset(ref extInstance);
                return ExtSoftPathState.FailedHard;
            }

            // main path state is READY

            // main path calculation succeeded: update return path state and check its state if necessary
            extCitInstMan.UpdateReturnPathState(ref extInstance);

            var success = true;
            switch (extInstance.returnPathState) {
                case ExtPathState.None:
                default: {
                    // no return path calculated: ignore
                    Log._TraceIf(
                        logParkingAi,
                        () => $"AdvancedParkingManager.UpdateCitizenPathState({citizenInstanceId}, ..., " +
                        $"{mainPathState}): return path state is None. Ignoring and " +
                        "returning main path state.");
                    break;
                }

                case ExtPathState.Calculating: // OK
                {
                    Log._TraceIf(
                        extendedLogParkingAi,
                        () => $"AdvancedParkingManager.UpdateCitizenPathState({citizenInstanceId}, ..., " +
                        $"{mainPathState}): return path state is still calculating.");
                    return ExtSoftPathState.Calculating;
                }

                case ExtPathState.Failed: // OK
                {
                    // no walking path from parking position to target found. flag main path as 'failed'.
                    Log._TraceIf(
                        logParkingAi,
                        () => $"AdvancedParkingManager.UpdateCitizenPathState({citizenInstanceId}, ..., " +
                        $"{mainPathState}): Return path FAILED.");

                    success = false;
                    break;
                }

                case ExtPathState.Ready: {
                    // handle valid return path
                    Log._TraceIf(
                        extendedLogParkingAi,
                        () => $"AdvancedParkingManager.UpdateCitizenPathState({citizenInstanceId}, ..., " +
                        $"{mainPathState}): Path is READY.");
                    break;
                }
            }

            extCitInstMan.ReleaseReturnPath(ref extInstance);

            return success
                       ? OnCitizenPathFindSuccess(
                           citizenInstanceId,
                           ref citizenInstance,
                           ref extInstance,
                           ref extCitizen,
                           ref citizen)
                       : OnCitizenPathFindFailure(
                           citizenInstanceId,
                           ref citizenInstance,
                           ref extInstance,
                           ref extCitizen);
        }

        public ExtSoftPathState UpdateCarPathState(ushort vehicleId,
                                                   ref Vehicle vehicleData,
                                                   ref CitizenInstance driverInstance,
                                                   ref ExtCitizenInstance driverExtInstance,
                                                   ExtPathState mainPathState) {
            IExtCitizenInstanceManager extCitInstMan = Constants.ManagerFactory.ExtCitizenInstanceManager;
#if DEBUG
            bool citizenDebug
                = (DebugSettings.VehicleId == 0
                   || DebugSettings.VehicleId == vehicleId)
                  && (DebugSettings.CitizenInstanceId == 0
                      || DebugSettings.CitizenInstanceId == driverExtInstance.instanceId)
                  && (DebugSettings.CitizenId == 0
                      || DebugSettings.CitizenId == driverInstance.m_citizen)
                  && (DebugSettings.SourceBuildingId == 0
                      || DebugSettings.SourceBuildingId == driverInstance.m_sourceBuilding)
                  && (DebugSettings.TargetBuildingId == 0
                      || DebugSettings.TargetBuildingId == driverInstance.m_targetBuilding);

            bool logParkingAi = DebugSwitch.BasicParkingAILog.Get() && citizenDebug;
            bool extendedLogParkingAi = DebugSwitch.ExtendedParkingAILog.Get() && citizenDebug;
#else
            const bool logParkingAi = false;
            const bool extendedLogParkingAi = false;
#endif

            Log._TraceIf(
                extendedLogParkingAi,
                () => $"AdvancedParkingManager.UpdateCarPathState({vehicleId}, ..., " +
                      $"{mainPathState}) called.");

            if (mainPathState == ExtPathState.Calculating) {
                // main path is still calculating, do not check return path
                Log._TraceIf(
                    extendedLogParkingAi,
                    () => $"AdvancedParkingManager.UpdateCarPathState({vehicleId}, ..., " +
                          $"{mainPathState}): still calculating main path. returning CALCULATING.");

                return ExtCitizenInstance.ConvertPathStateToSoftPathState(mainPathState);
            }

            // if (!driverExtInstance.IsValid()) {
            // // no driver
            // #if DEBUG
            //    if (debug)
            //        Log._Debug($"AdvancedParkingManager.UpdateCarPathState({vehicleId}, ..., {mainPathState}): no driver found!");
            // #endif
            //    return mainPathState;
            // }

            // ExtCitizenInstance driverExtInstance = ExtCitizenInstanceManager.Instance.GetExtInstance(
            // CustomPassengerCarAI.GetDriverInstance(vehicleId, ref vehicleData));
            if (!extCitInstMan.IsValid(driverExtInstance.instanceId)) {
                // no driver
                Log._TraceIf(
                    logParkingAi,
                    () => $"AdvancedParkingManager.UpdateCarPathState({vehicleId}, ..., " +
                    $"{mainPathState}): no driver found!");

                return ExtCitizenInstance.ConvertPathStateToSoftPathState(mainPathState);
            }

            if (Constants.ManagerFactory.ExtVehicleManager.ExtVehicles[vehicleId].vehicleType !=
                ExtVehicleType.PassengerCar) {
                // non-passenger cars are not handled
                Log._TraceIf(
                    logParkingAi,
                    () => $"AdvancedParkingManager.UpdateCarPathState({vehicleId}, ..., " +
                    $"{mainPathState}): not a passenger car!");

                extCitInstMan.Reset(ref driverExtInstance);
                return ExtCitizenInstance.ConvertPathStateToSoftPathState(mainPathState);
            }

            if (mainPathState == ExtPathState.None || mainPathState == ExtPathState.Failed) {
                // main path failed or non-existing: reset return path
                Log._TraceIf(
                    logParkingAi,
                    () => $"AdvancedParkingManager.UpdateCarPathState({vehicleId}, ..., " +
                          $"{mainPathState}): mainPathSate is {mainPathState}.");

                if (mainPathState == ExtPathState.Failed) {
                    Log._TraceIf(
                        logParkingAi,
                        () => $"AdvancedParkingManager.UpdateCarPathState({vehicleId}, ..., " +
                        $"{mainPathState}): Checking if path-finding may be repeated.");

                    extCitInstMan.ReleaseReturnPath(ref driverExtInstance);
                    return OnCarPathFindFailure(vehicleId,
                                                ref vehicleData,
                                                ref driverInstance,
                                                ref driverExtInstance);
                }

                Log._TraceIf(
                    logParkingAi,
                    () => $"AdvancedParkingManager.UpdateCarPathState({vehicleId}, ..., " +
                          $"{mainPathState}): Resetting instance and returning FAILED.");

                extCitInstMan.Reset(ref driverExtInstance);
                return ExtSoftPathState.FailedHard;
            }

            // main path state is READY

            // main path calculation succeeded: update return path state and check its state
            extCitInstMan.UpdateReturnPathState(ref driverExtInstance);

            switch (driverExtInstance.returnPathState) {
                case ExtPathState.None:
                default: {
                    // no return path calculated: ignore
                    Log._TraceIf(
                        logParkingAi,
                        () => $"AdvancedParkingManager.UpdateCarPathState({vehicleId}, ..., " +
                              $"{mainPathState}): return path state is None. " +
                              "Setting pathMode=DrivingToTarget and returning main path state.");

                    driverExtInstance.pathMode = ExtPathMode.DrivingToTarget;
                    return ExtCitizenInstance.ConvertPathStateToSoftPathState(mainPathState);
                }

                case ExtPathState.Calculating: {
                    // return path not read yet: wait for it
                    Log._TraceIf(
                        extendedLogParkingAi,
                        () => $"AdvancedParkingManager.UpdateCarPathState({vehicleId}, ..., " +
                              $"{mainPathState}): return path state is still calculating.");

                    return ExtSoftPathState.Calculating;
                }

                case ExtPathState.Failed: {
                    // no walking path from parking position to target found. flag main path as 'failed'.
                    if (logParkingAi) {
                        Log._Trace(
                            $"AdvancedParkingManager.UpdateCarPathState({vehicleId}, ..., " +
                            $"{mainPathState}): Return path {driverExtInstance.returnPathId} " +
                            "FAILED. Forcing path-finding to fail.");
                    }

                    extCitInstMan.Reset(ref driverExtInstance);
                    return ExtSoftPathState.FailedHard;
                }

                case ExtPathState.Ready: {
                    // handle valid return path
                    extCitInstMan.ReleaseReturnPath(ref driverExtInstance);
                    if (extendedLogParkingAi) {
                        Log._Trace(
                            $"AdvancedParkingManager.UpdateCarPathState({vehicleId}, ..., " +
                            $"{mainPathState}): Path is ready for vehicle {vehicleId}, " +
                            $"citizen instance {driverExtInstance.instanceId}! " +
                            $"CurrentPathMode={driverExtInstance.pathMode}");
                    }

                    byte laneTypes = CustomPathManager
                                     ._instance.m_pathUnits.m_buffer[vehicleData.m_path]
                                     .m_laneTypes;
                    bool usesPublicTransport =
                        (laneTypes & (byte)(NetInfo.LaneType.PublicTransport)) != 0;

                    if (usesPublicTransport &&
                        (driverExtInstance.pathMode == ExtPathMode.CalculatingCarPathToKnownParkPos
                         || driverExtInstance.pathMode == ExtPathMode.CalculatingCarPathToAltParkPos))
                    {
                        driverExtInstance.pathMode = ExtPathMode.CalculatingCarPathToTarget;
                        driverExtInstance.parkingSpaceLocation = ExtParkingSpaceLocation.None;
                        driverExtInstance.parkingSpaceLocationId = 0;
                    }

                    switch (driverExtInstance.pathMode) {
                        case ExtPathMode.CalculatingCarPathToAltParkPos: {
                            driverExtInstance.pathMode = ExtPathMode.DrivingToAltParkPos;
                            driverExtInstance.parkingPathStartPosition = null;
                            if (logParkingAi) {
                                Log._Trace(
                                    $"AdvancedParkingManager.UpdateCarPathState({vehicleId}, ..., " +
                                    $"{mainPathState}): Path to an alternative parking position is " +
                                    $"READY for vehicle {vehicleId}! CurrentPathMode={driverExtInstance.pathMode}");
                            }

                            break;
                        }

                        case ExtPathMode.CalculatingCarPathToTarget: {
                            driverExtInstance.pathMode = ExtPathMode.DrivingToTarget;
                            if (logParkingAi) {
                                Log._Trace(
                                    $"AdvancedParkingManager.UpdateCarPathState({vehicleId}, ..., " +
                                    $"{mainPathState}): Car path is READY for vehicle {vehicleId}! " +
                                    $"CurrentPathMode={driverExtInstance.pathMode}");
                            }

                            break;
                        }

                        case ExtPathMode.CalculatingCarPathToKnownParkPos: {
                            driverExtInstance.pathMode = ExtPathMode.DrivingToKnownParkPos;
                            if (logParkingAi) {
                                Log._Trace(
                                    $"AdvancedParkingManager.UpdateCarPathState({vehicleId}, ..., " +
                                    $"{mainPathState}): Car path to known parking position is READY " +
                                    $"for vehicle {vehicleId}! CurrentPathMode={driverExtInstance.pathMode}");
                            }

                            break;
                        }
                    }

                    return ExtSoftPathState.Ready;
                }
            }
        }

        public ParkedCarApproachState CitizenApproachingParkedCarSimulationStep(
            ushort instanceId,
            ref CitizenInstance instanceData,
            ref ExtCitizenInstance extInstance,
            Vector3 physicsLodRefPos,
            ref VehicleParked parkedCar)
        {
#if DEBUG
            bool citizenDebug =
                    (DebugSettings.CitizenInstanceId == 0
                     || DebugSettings.CitizenInstanceId == instanceId)
                    && (DebugSettings.CitizenId == 0
                        || DebugSettings.CitizenId == instanceData.m_citizen)
                    && (DebugSettings.SourceBuildingId == 0
                        || DebugSettings.SourceBuildingId == instanceData.m_sourceBuilding)
                    && (DebugSettings.TargetBuildingId == 0
                        || DebugSettings.TargetBuildingId == instanceData.m_targetBuilding);

            bool logParkingAi = DebugSwitch.BasicParkingAILog.Get() && citizenDebug;
            bool extendedLogParkingAi = DebugSwitch.ExtendedParkingAILog.Get() && citizenDebug;
#else
            bool logParkingAi = false;
            bool extendedLogParkingAi = false;
#endif

            if ((instanceData.m_flags & CitizenInstance.Flags.WaitingPath) != CitizenInstance.Flags.None) {
                Log._TraceIf(
                    extendedLogParkingAi,
                    () => $"AdvancedParkingManager.CheckCitizenReachedParkedCar({instanceId}): " +
                        $"citizen instance {instanceId} is waiting for path-finding to complete.");

                return ParkedCarApproachState.None;
            }

            // ExtCitizenInstance extInstance = ExtCitizenInstanceManager.Instance.GetExtInstance(instanceId);
            if (extInstance.pathMode != ExtPathMode.ApproachingParkedCar &&
                extInstance.pathMode != ExtPathMode.WalkingToParkedCar) {
                if (extendedLogParkingAi) {
                    Log._Trace(
                        "AdvancedParkingManager.CitizenApproachingParkedCarSimulationStep" +
                        $"({instanceId}): citizen instance {instanceId} is not reaching " +
                        $"a parked car ({extInstance.pathMode})");
                }

                return ParkedCarApproachState.None;
            }

            if ((instanceData.m_flags & CitizenInstance.Flags.Character) == CitizenInstance.Flags.None) {
                return ParkedCarApproachState.None;
            }

            Vector3 lastFramePos = instanceData.GetLastFramePosition();
            Vector3 doorPosition = parkedCar.GetClosestDoorPosition(
                parkedCar.m_position,
                VehicleInfo.DoorType.Enter);

            if (extInstance.pathMode == ExtPathMode.WalkingToParkedCar) {
                // check if path is complete
                if (instanceData.m_pathPositionIndex != 255 &&
                    (instanceData.m_path == 0
                     || !CustomPathManager._instance.m_pathUnits.m_buffer[instanceData.m_path]
                                          .GetPosition(instanceData.m_pathPositionIndex >> 1,
                                                       out _)))
                {
                    extInstance.pathMode = ExtPathMode.ApproachingParkedCar;
                    extInstance.lastDistanceToParkedCar =
                        (instanceData.GetLastFramePosition() - doorPosition).sqrMagnitude;

                    if (logParkingAi) {
                        Log._Trace(
                            "AdvancedParkingManager.CitizenApproachingParkedCarSimulationStep" +
                            $"({instanceId}): citizen instance {instanceId} was walking to " +
                            "parked car and reached final path position. " +
                            $"Switched PathMode to {extInstance.pathMode}.");
                    }
                }
            }

            if (extInstance.pathMode != ExtPathMode.ApproachingParkedCar) {
                return ParkedCarApproachState.None;
            }

            Vector3 doorTargetDir = doorPosition - lastFramePos;
            Vector3 doorWalkVector = doorPosition;
            float doorTargetDirMagnitude = doorTargetDir.magnitude;
            if (doorTargetDirMagnitude > 1f) {
                float speed = Mathf.Max(doorTargetDirMagnitude - 5f, doorTargetDirMagnitude * 0.5f);
                doorWalkVector = lastFramePos + (doorTargetDir * (speed / doorTargetDirMagnitude));
            }

            instanceData.m_targetPos = new Vector4(doorWalkVector.x, doorWalkVector.y, doorWalkVector.z, 0.5f);
            instanceData.m_targetDir = VectorUtils.XZ(doorTargetDir);

            CitizenApproachingParkedCarSimulationStep(instanceId, ref instanceData, physicsLodRefPos);

            float doorSqrDist = (instanceData.GetLastFramePosition() - doorPosition).sqrMagnitude;

            if (doorSqrDist > GlobalConfig.Instance.Parking.MaxParkedCarInstanceSwitchSqrDistance) {
                // citizen is still too far away from the parked car
                ExtPathMode oldPathMode = extInstance.pathMode;
                if (doorSqrDist > extInstance.lastDistanceToParkedCar + 1024f) {

                    // distance has increased dramatically since the last time
                    if (logParkingAi) {
                        Log._Trace(
                            "AdvancedParkingManager.CitizenApproachingParkedCarSimulationStep" +
                            $"({instanceId}): Citizen instance {instanceId} is currently " +
                            "reaching their parked car but distance increased! " +
                            $"dist={doorSqrDist}, LastDistanceToParkedCar" +
                            $"={extInstance.lastDistanceToParkedCar}.");
                    }

#if DEBUG
                    if (DebugSwitch.ParkingAIDistanceIssue.Get()) {
                        Log._Trace(
                            "AdvancedParkingManager.CitizenApproachingParkedCarSimulationStep" +
                            $"({instanceId}): FORCED PAUSE. Distance increased! " +
                            $"Citizen instance {instanceId}. dist={doorSqrDist}");
                        Singleton<SimulationManager>.instance.SimulationPaused = true;
                    }
#endif

                    CitizenInstance.Frame frameData = instanceData.GetLastFrameData();
                    frameData.m_position = doorPosition;
                    instanceData.SetLastFrameData(frameData);

                    extInstance.pathMode = ExtPathMode.RequiresCarPath;

                    return ParkedCarApproachState.Approached;
                }

                if (doorSqrDist < extInstance.lastDistanceToParkedCar) {
                    extInstance.lastDistanceToParkedCar = doorSqrDist;
                }

                if (extendedLogParkingAi) {
                    Log._Trace(
                        "AdvancedParkingManager.CitizenApproachingParkedCarSimulationStep" +
                        $"({instanceId}): Citizen instance {instanceId} is currently " +
                        $"reaching their parked car (dist={doorSqrDist}, " +
                        $"LastDistanceToParkedCar={extInstance.lastDistanceToParkedCar}). " +
                        $"CurrentDepartureMode={extInstance.pathMode}");
                }

                return ParkedCarApproachState.Approaching;
            }

            extInstance.pathMode = ExtPathMode.RequiresCarPath;
            if (logParkingAi) {
                Log._Trace(
                    "AdvancedParkingManager.CitizenApproachingParkedCarSimulationStep" +
                    $"({instanceId}): Citizen instance {instanceId} reached parking position " +
                    $"(dist={doorSqrDist}). Calculating remaining path now. " +
                    $"CurrentDepartureMode={extInstance.pathMode}");
            }

            return ParkedCarApproachState.Approached;
        }

        protected void CitizenApproachingParkedCarSimulationStep(ushort instanceId,
                                                                 ref CitizenInstance instanceData,
                                                                 Vector3 physicsLodRefPos) {
            if ((instanceData.m_flags & CitizenInstance.Flags.Character) == CitizenInstance.Flags.None) {
                return;
            }

            CitizenInstance.Frame lastFrameData = instanceData.GetLastFrameData();
            int oldGridX = Mathf.Clamp(
                (int)((lastFrameData.m_position.x / CitizenManager.CITIZENGRID_CELL_SIZE) +
                      (CitizenManager.CITIZENGRID_RESOLUTION / 2f)),
                0,
                CitizenManager.CITIZENGRID_RESOLUTION - 1);
            int oldGridY = Mathf.Clamp(
                (int)((lastFrameData.m_position.z / CitizenManager.CITIZENGRID_CELL_SIZE) +
                      (CitizenManager.CITIZENGRID_RESOLUTION / 2f)),
                0,
                CitizenManager.CITIZENGRID_RESOLUTION - 1);
            bool lodPhysics = Vector3.SqrMagnitude(physicsLodRefPos - lastFrameData.m_position) >= 62500f;

            CitizenApproachingParkedCarSimulationStep(instanceId, ref instanceData, ref lastFrameData, lodPhysics);

            int newGridX = Mathf.Clamp(
                (int)((lastFrameData.m_position.x / CitizenManager.CITIZENGRID_CELL_SIZE) +
                      (CitizenManager.CITIZENGRID_RESOLUTION / 2f)),
                0,
                CitizenManager.CITIZENGRID_RESOLUTION - 1);
            int newGridY = Mathf.Clamp(
                (int)((lastFrameData.m_position.z / CitizenManager.CITIZENGRID_CELL_SIZE) +
                      (CitizenManager.CITIZENGRID_RESOLUTION / 2f)),
                0,
                CitizenManager.CITIZENGRID_RESOLUTION - 1);

            if ((newGridX != oldGridX || newGridY != oldGridY) &&
                (instanceData.m_flags & CitizenInstance.Flags.Character) !=
                CitizenInstance.Flags.None) {
                Singleton<CitizenManager>.instance.RemoveFromGrid(
                    instanceId,
                    ref instanceData,
                    oldGridX,
                    oldGridY);
                Singleton<CitizenManager>.instance.AddToGrid(
                    instanceId,
                    ref instanceData,
                    newGridX,
                    newGridY);
            }

            if (instanceData.m_flags != CitizenInstance.Flags.None) {
                instanceData.SetFrameData(
                    Singleton<SimulationManager>.instance.m_currentFrameIndex,
                    lastFrameData);
            }
        }

        [UsedImplicitly]
        protected void CitizenApproachingParkedCarSimulationStep(ushort instanceId,
                                                                 ref CitizenInstance instanceData,
                                                                 ref CitizenInstance.Frame frameData,
                                                                 bool lodPhysics) {
            frameData.m_position += frameData.m_velocity * 0.5f;

            Vector3 targetDiff = (Vector3)instanceData.m_targetPos - frameData.m_position;
            Vector3 targetVelDiff = targetDiff - frameData.m_velocity;
            float targetVelDiffMag = targetVelDiff.magnitude;

            targetVelDiff *= 2f / Mathf.Max(targetVelDiffMag, 2f);
            frameData.m_velocity += targetVelDiff;
            frameData.m_velocity -= Mathf.Max(
                                         0f,
                                         Vector3.Dot(
                                             (frameData.m_position + frameData.m_velocity) -
                                             (Vector3)instanceData.m_targetPos,
                                             frameData.m_velocity)) /
                                     Mathf.Max(0.01f, frameData.m_velocity.sqrMagnitude) *
                                     frameData.m_velocity;
            if (frameData.m_velocity.sqrMagnitude > 0.01f) {
                frameData.m_rotation = Quaternion.LookRotation(frameData.m_velocity);
            }
        }

        public bool CitizenApproachingTargetSimulationStep(ushort instanceId,
                                                           ref CitizenInstance instanceData,
                                                           ref ExtCitizenInstance extInstance) {
            IExtCitizenInstanceManager extCitInstMan = Constants.ManagerFactory.ExtCitizenInstanceManager;
#if DEBUG
            bool citizenDebug =
                (DebugSettings.CitizenInstanceId == 0
                 || DebugSettings.CitizenInstanceId == instanceId)
                && (DebugSettings.CitizenId == 0
                    || DebugSettings.CitizenId == instanceData.m_citizen)
                && (DebugSettings.SourceBuildingId == 0
                    || DebugSettings.SourceBuildingId == instanceData.m_sourceBuilding)
                && (DebugSettings.TargetBuildingId == 0
                    || DebugSettings.TargetBuildingId == instanceData.m_targetBuilding);

            bool logParkingAi = DebugSwitch.BasicParkingAILog.Get() && citizenDebug;
            bool extendedLogParkingAi = DebugSwitch.ExtendedParkingAILog.Get() && citizenDebug;
#else
            const bool logParkingAi = false;
            const bool extendedLogParkingAi = false;
#endif
            if ((instanceData.m_flags & CitizenInstance.Flags.WaitingPath) !=
                CitizenInstance.Flags.None) {
                Log._TraceIf(
                    extendedLogParkingAi,
                    () => $"AdvancedParkingManager.CitizenApproachingTargetSimulationStep({instanceId}): " +
                    $"citizen instance {instanceId} is waiting for path-finding to complete.");
                return false;
            }

            // ExtCitizenInstance extInstance = ExtCitizenInstanceManager.Instance.GetExtInstance(instanceId);
            if (extInstance.pathMode != ExtPathMode.WalkingToTarget &&
                extInstance.pathMode != ExtPathMode.TaxiToTarget) {
                if (extendedLogParkingAi) {
                    Log._Trace(
                        $"AdvancedParkingManager.CitizenApproachingTargetSimulationStep({instanceId}): " +
                        $"citizen instance {instanceId} is not reaching target ({extInstance.pathMode})");
                }

                return false;
            }

            if ((instanceData.m_flags & CitizenInstance.Flags.Character) ==
                CitizenInstance.Flags.None) {
                return false;
            }

            // check if path is complete
            if (instanceData.m_pathPositionIndex != 255
                && (instanceData.m_path == 0
                    || !CustomPathManager._instance.m_pathUnits.m_buffer[instanceData.m_path]
                                         .GetPosition(
                                             instanceData.m_pathPositionIndex >> 1,
                                             out _))) {
                extCitInstMan.Reset(ref extInstance);
                if (logParkingAi) {
                    Log._Trace(
                        $"AdvancedParkingManager.CitizenApproachingTargetSimulationStep({instanceId}): " +
                        $"Citizen instance {instanceId} reached target. " +
                        $"CurrentDepartureMode={extInstance.pathMode}");
                }

                return true;
            }

            return false;
        }

        /// <summary>
        /// Handles a path-finding success for activated Parking AI.
        /// </summary>
        /// <param name="instanceId">Citizen instance id</param>
        /// <param name="instanceData">Citizen instance data</param>
        /// <param name="extInstance">Extended citizen instance data</param>
        /// <param name="extCitizen">Extended citizen data</param>
        /// <param name="citizenData">Citizen data</param>
        /// <returns>soft path state</returns>
        protected ExtSoftPathState OnCitizenPathFindSuccess(ushort instanceId,
                                                            ref CitizenInstance instanceData,
                                                            ref ExtCitizenInstance extInstance,
                                                            ref ExtCitizen extCitizen,
                                                            ref Citizen citizenData) {
            IExtCitizenInstanceManager extCitInstMan = Constants.ManagerFactory.ExtCitizenInstanceManager;
            IExtBuildingManager extBuildingMan = Constants.ManagerFactory.ExtBuildingManager;
#if DEBUG
            bool citizenDebug =
                (DebugSettings.CitizenInstanceId == 0
                 || DebugSettings.CitizenInstanceId == instanceId)
                && (DebugSettings.CitizenId == 0
                    || DebugSettings.CitizenId == instanceData.m_citizen)
                && (DebugSettings.SourceBuildingId == 0
                    || DebugSettings.SourceBuildingId == instanceData.m_sourceBuilding)
                && (DebugSettings.TargetBuildingId == 0
                    || DebugSettings.TargetBuildingId == instanceData.m_targetBuilding);

            bool logParkingAi = DebugSwitch.BasicParkingAILog.Get() && citizenDebug;
            bool extendedLogParkingAi = DebugSwitch.ExtendedParkingAILog.Get() && citizenDebug;
#else
            bool logParkingAi = false;
            bool extendedLogParkingAi = false;
#endif

            if (logParkingAi) {
                Log._Trace(
                    $"AdvancedParkingManager.OnCitizenPathFindSuccess({instanceId}): " +
                    $"Path-finding succeeded for citizen instance {instanceId}. " +
                    $"Path: {instanceData.m_path} vehicle={citizenData.m_vehicle}");
            }

            if (citizenData.m_vehicle == 0) {
                // citizen does not already have a vehicle assigned
                if (extInstance.pathMode == ExtPathMode.TaxiToTarget) {
                    Log._TraceIf(
                        extendedLogParkingAi,
                        () => $"AdvancedParkingManager.OnCitizenPathFindSuccess({instanceId}): " +
                          "Citizen uses a taxi. Decreasing public transport demand and " +
                          "returning READY.");

                    // cim uses taxi
                    if (instanceData.m_sourceBuilding != 0) {
                        extBuildingMan.RemovePublicTransportDemand(
                            ref extBuildingMan.ExtBuildings[instanceData.m_sourceBuilding],
                            GlobalConfig.Instance.Parking.PublicTransportDemandUsageDecrement,
                            true);
                    }

                    if (instanceData.m_targetBuilding != 0) {
                        extBuildingMan.RemovePublicTransportDemand(
                            ref extBuildingMan.ExtBuildings[instanceData.m_targetBuilding],
                            GlobalConfig.Instance.Parking.PublicTransportDemandUsageDecrement,
                            false);
                    }

                    extCitizen.transportMode |= ExtTransportMode.PublicTransport;
                    return ExtSoftPathState.Ready;
                }

                ushort parkedVehicleId = citizenData.m_parkedVehicle;
                var sqrDistToParkedVehicle = 0f;
                if (parkedVehicleId != 0) {
                    // calculate distance to parked vehicle
                    VehicleManager vehicleManager = Singleton<VehicleManager>.instance;
                    VehicleParked parkedVehicle = vehicleManager.m_parkedVehicles.m_buffer[parkedVehicleId];
                    Vector3 doorPosition = parkedVehicle.GetClosestDoorPosition(
                        parkedVehicle.m_position,
                        VehicleInfo.DoorType.Enter);
                    sqrDistToParkedVehicle = (instanceData.GetLastFramePosition() - doorPosition)
                        .sqrMagnitude;
                }

                byte laneTypes = CustomPathManager
                                ._instance.m_pathUnits.m_buffer[instanceData.m_path].m_laneTypes;
                uint vehicleTypes = CustomPathManager
                                   ._instance.m_pathUnits.m_buffer[instanceData.m_path]
                                   .m_vehicleTypes;
                bool usesPublicTransport =
                    (laneTypes & (byte)NetInfo.LaneType.PublicTransport) != 0;
                bool usesCar = (laneTypes & (byte)(NetInfo.LaneType.Vehicle
                                                  | NetInfo.LaneType.TransportVehicle)) != 0
                              && (vehicleTypes & (ushort)VehicleInfo.VehicleType.Car) != 0;

                if (usesPublicTransport && usesCar &&
                    (extInstance.pathMode == ExtPathMode.CalculatingCarPathToKnownParkPos ||
                     extInstance.pathMode == ExtPathMode.CalculatingCarPathToAltParkPos)) {
                     // when using public transport together with a car (assuming a
                     // "source -> walk -> drive -> walk -> use public transport -> walk -> target"
                     // path) discard parking space information since the cim has to park near the
                     // public transport stop (instead of parking in the vicinity of the target building).
                     // TODO we could check if the path looks like "source -> walk -> use public transport -> walk -> drive -> [walk ->] target" (in this case parking space information would still be valid)
                    Log._TraceIf(
                        extendedLogParkingAi,
                        () => $"AdvancedParkingManager.OnCitizenPathFindSuccess({instanceId}): " +
                        "Citizen uses their car together with public transport. " +
                        "Discarding parking space information and setting path mode to " +
                        "CalculatingCarPathToTarget.");

                    extInstance.pathMode = ExtPathMode.CalculatingCarPathToTarget;
                    extInstance.parkingSpaceLocation = ExtParkingSpaceLocation.None;
                    extInstance.parkingSpaceLocationId = 0;
                }

                switch (extInstance.pathMode) {
                    case ExtPathMode.None: // citizen starts at source building
                    default: {
                        return OnCitizenPathFindSuccess_Default(
                            instanceId,
                            instanceData,
                            ref extInstance,
                            ref extCitizen,
                            logParkingAi,
                            usesCar,
                            parkedVehicleId,
                            extBuildingMan,
                            usesPublicTransport);
                    }

                    // citizen has not yet entered their car (but is close to do so) and tries to
                    // reach the target directly
                    case ExtPathMode.CalculatingCarPathToTarget:

                    // citizen has not yet entered their (but is close to do so) car and tries to
                    // reach a parking space in the vicinity of the target
                    case ExtPathMode.CalculatingCarPathToKnownParkPos:

                    // citizen has not yet entered their car (but is close to do so) and tries to
                    // reach an alternative parking space in the vicinity of the target
                    case ExtPathMode.CalculatingCarPathToAltParkPos:
                    {
                        return OnCitizenPathFindSuccess_CarPath(
                            instanceId,
                            ref instanceData,
                            ref extInstance,
                            ref extCitizen,
                            usesCar,
                            logParkingAi,
                            parkedVehicleId,
                            extCitInstMan,
                            sqrDistToParkedVehicle,
                            extendedLogParkingAi,
                            usesPublicTransport,
                            extBuildingMan);
                    }

                    case ExtPathMode.CalculatingWalkingPathToParkedCar: {
                        return OnCitizenPathFindSuccess_ToParkedCar(
                            instanceId,
                            instanceData,
                            ref extInstance,
                            parkedVehicleId,
                            logParkingAi,
                            extCitInstMan);
                    }

                    case ExtPathMode.CalculatingWalkingPathToTarget: {
                        return OnCitizenPathFindSuccess_ToTarget(
                            instanceId,
                            instanceData,
                            ref extInstance,
                            logParkingAi);
                    }
                }
            }

            // citizen has a vehicle assigned
            Log._DebugOnlyWarningIf(
                logParkingAi,
                () => $"AdvancedParkingManager.OnCitizenPathFindSuccess({instanceId}): " +
                "Citizen has a vehicle assigned but this method does not handle this " +
                "situation. Forcing path-find to fail.");

            extCitInstMan.Reset(ref extInstance);
            return ExtSoftPathState.FailedHard;
        }

        private static ExtSoftPathState OnCitizenPathFindSuccess_ToTarget(
            ushort instanceId,
            CitizenInstance instanceData,
            ref ExtCitizenInstance extInstance,
            bool logParkingAi) {
            // final walking path to target has been calculated
            extInstance.pathMode = ExtPathMode.WalkingToTarget;

            if (logParkingAi) {
                Log._Trace(
                    $"AdvancedParkingManager.OnCitizenPathFindSuccess({instanceId}): " +
                    $"Citizen instance {instanceId} is now travelling by foot to their final " +
                    $"target. CurrentDepartureMode={extInstance.pathMode}, " +
                    $"targetPos={instanceData.m_targetPos} " +
                    $"lastFramePos={instanceData.GetLastFramePosition()}");
            }

            return ExtSoftPathState.Ready;
        }

        private static ExtSoftPathState OnCitizenPathFindSuccess_ToParkedCar(
            ushort instanceId,
            CitizenInstance instanceData,
            ref ExtCitizenInstance extInstance,
            ushort parkedVehicleId,
            bool logParkingAi,
            IExtCitizenInstanceManager extCitInstMan) {

            // path to parked vehicle has been calculated...
            if (parkedVehicleId == 0) {
                // ... but the parked vehicle has vanished
                Log._TraceIf(
                    logParkingAi,
                    () => $"AdvancedParkingManager.OnCitizenPathFindSuccess({instanceId}): " +
                    $"Citizen instance {instanceId} shall walk to their parked vehicle but it " +
                    "disappeared. Retrying path-find for walking.");

                extCitInstMan.Reset(ref extInstance);
                extInstance.pathMode = ExtPathMode.RequiresWalkingPathToTarget;
                return ExtSoftPathState.FailedSoft;
            }

            extInstance.pathMode = ExtPathMode.WalkingToParkedCar;
            if (logParkingAi) {
                Log._Trace(
                    $"AdvancedParkingManager.OnCitizenPathFindSuccess({instanceId}): " +
                    $"Citizen instance {instanceId} is now on their way to its parked vehicle. " +
                    $"CurrentDepartureMode={extInstance.pathMode}, " +
                    $"targetPos={instanceData.m_targetPos} " +
                    $"lastFramePos={instanceData.GetLastFramePosition()}");
            }

            return ExtSoftPathState.Ready;
        }

        private ExtSoftPathState
            OnCitizenPathFindSuccess_CarPath(ushort instanceId,
                                             ref CitizenInstance instanceData,
                                             ref ExtCitizenInstance extInstance,
                                             ref ExtCitizen extCitizen,
                                             bool usesCar,
                                             bool logParkingAi,
                                             ushort parkedVehicleId,
                                             IExtCitizenInstanceManager extCitInstMan,
                                             float sqrDistToParkedVehicle,
                                             bool extendedLogParkingAi,
                                             bool usesPublicTransport,
                                             IExtBuildingManager extBuildingMan)
        {
            if (usesCar) {
                // parked car should be reached now
                Log._TraceIf(
                    logParkingAi,
                    () => $"AdvancedParkingManager.OnCitizenPathFindSuccess({instanceId}): " +
                    $"Path for citizen instance {instanceId} contains passenger car section and " +
                    "citizen should stand in front of their car.");

                if (extInstance.atOutsideConnection) {
                    switch (extInstance.pathMode) {
                        // car path calculated starting at road outside connection: success
                        case ExtPathMode.CalculatingCarPathToAltParkPos: {
                            extInstance.pathMode = ExtPathMode.DrivingToAltParkPos;
                            extInstance.parkingPathStartPosition = null;
                            if (logParkingAi) {
                                Log._Trace(
                                    $"AdvancedParkingManager.OnCitizenPathFindSuccess({instanceId}): " +
                                    "Path to an alternative parking position is READY! " +
                                    $"CurrentPathMode={extInstance.pathMode}");
                            }

                            break;
                        }

                        case ExtPathMode.CalculatingCarPathToTarget: {
                            extInstance.pathMode = ExtPathMode.DrivingToTarget;
                            if (logParkingAi) {
                                Log._Trace(
                                    $"AdvancedParkingManager.OnCitizenPathFindSuccess({instanceId}): " +
                                    $"Car path is READY! CurrentPathMode={extInstance.pathMode}");
                            }

                            break;
                        }

                        case ExtPathMode.CalculatingCarPathToKnownParkPos: {
                            extInstance.pathMode = ExtPathMode.DrivingToKnownParkPos;
                            if (logParkingAi) {
                                Log._Trace(
                                    $"AdvancedParkingManager.OnCitizenPathFindSuccess({instanceId}): " +
                                    "Car path to known parking position is READY! " +
                                    $"CurrentPathMode={extInstance.pathMode}");
                            }

                            break;
                        }
                    }

                    extInstance.atOutsideConnection = false; // citizen leaves outside connection
                    return ExtSoftPathState.Ready;
                }

                if (parkedVehicleId == 0) {
                    // error! could not find/spawn parked car
                    Log._TraceIf(
                        logParkingAi,
                        () => $"AdvancedParkingManager.OnCitizenPathFindSuccess({instanceId}): " +
                        $"Citizen instance {instanceId} still does not have a parked vehicle! " +
                        "Retrying: Cim should walk to target");

                    extCitInstMan.Reset(ref extInstance);
                    extInstance.pathMode = ExtPathMode.RequiresWalkingPathToTarget;
                    return ExtSoftPathState.FailedSoft;
                }

                if (sqrDistToParkedVehicle >
                    4f * GlobalConfig.Instance.Parking.MaxParkedCarInstanceSwitchSqrDistance) {
                    // error! parked car is too far away
                    Log._TraceIf(
                        logParkingAi,
                        () => $"AdvancedParkingManager.OnCitizenPathFindSuccess({instanceId}): " +
                        $"Citizen instance {instanceId} cannot enter parked vehicle because it is " +
                        $"too far away (sqrDistToParkedVehicle={sqrDistToParkedVehicle})! " +
                        "Retrying: Cim should walk to parked car");
                    extInstance.pathMode = ExtPathMode.RequiresWalkingPathToParkedCar;
                    return ExtSoftPathState.FailedSoft;
                }

                // path using passenger car has been calculated
                if (EnterParkedCar(
                    instanceId,
                    ref instanceData,
                    parkedVehicleId,
                    out ushort vehicleId))
                {
                    extInstance.pathMode =
                        extInstance.pathMode == ExtPathMode.CalculatingCarPathToTarget
                            ? ExtPathMode.DrivingToTarget
                            : ExtPathMode.DrivingToKnownParkPos;

                    extCitizen.transportMode |= ExtTransportMode.Car;

                    if (extendedLogParkingAi) {
                        Log._Trace(
                            $"AdvancedParkingManager.OnCitizenPathFindSuccess({instanceId}): " +
                            $"Citizen instance {instanceId} has entered their car and is now " +
                            $"travelling by car (vehicleId={vehicleId}). " +
                            $"CurrentDepartureMode={extInstance.pathMode}, " +
                            $"targetPos={instanceData.m_targetPos} " +
                            $"lastFramePos={instanceData.GetLastFramePosition()}");
                    }

                    return ExtSoftPathState.Ignore;
                }

                // error! parked car could not be entered (reached vehicle limit?): try to walk to target
                if (logParkingAi) {
                    Log._Trace(
                        $"AdvancedParkingManager.OnCitizenPathFindSuccess({instanceId}): " +
                        $"Entering parked vehicle {parkedVehicleId} failed for citizen " +
                        $"instance {instanceId}. Trying to walk to target. " +
                        $"CurrentDepartureMode={extInstance.pathMode}");
                }

                extCitInstMan.Reset(ref extInstance);
                extInstance.pathMode = ExtPathMode.RequiresWalkingPathToTarget;
                return ExtSoftPathState.FailedSoft;
            }

            // citizen does not need a car for the calculated path...
            switch (extInstance.pathMode) {
                case ExtPathMode.CalculatingCarPathToTarget: {
                    // ... and the path can be reused for walking
                    Log._TraceIf(
                        logParkingAi,
                        () => $"AdvancedParkingManager.OnCitizenPathFindSuccess({instanceId}): " +
                        "A direct car path was queried that does not contain a car section. " +
                        "Switching path mode to walking.");

                    extCitInstMan.Reset(ref extInstance);

                    if (usesPublicTransport) {
                        // decrease public tranport demand
                        if (instanceData.m_sourceBuilding != 0) {
                            extBuildingMan.RemovePublicTransportDemand(
                                ref extBuildingMan.ExtBuildings[instanceData.m_sourceBuilding],
                                GlobalConfig.Instance.Parking.PublicTransportDemandUsageDecrement,
                                true);
                        }

                        if (instanceData.m_targetBuilding != 0) {
                            extBuildingMan.RemovePublicTransportDemand(
                                ref extBuildingMan.ExtBuildings[instanceData.m_targetBuilding],
                                GlobalConfig.Instance.Parking.PublicTransportDemandUsageDecrement,
                                false);
                        }

                        extCitizen.transportMode |= ExtTransportMode.PublicTransport;
                    }

                    extInstance.pathMode = ExtPathMode.WalkingToTarget;
                    return ExtSoftPathState.Ready;
                }

                case ExtPathMode.CalculatingCarPathToKnownParkPos:
                case ExtPathMode.CalculatingCarPathToAltParkPos:
                default: {
                    // ... and a path to a parking spot was calculated: dismiss path and
                    // restart path-finding for walking
                Log._TraceIf(
                    logParkingAi,
                    () => $"AdvancedParkingManager.OnCitizenPathFindSuccess({instanceId}): " +
                    "A parking space car path was queried but it turned out that no car is " +
                    "needed. Retrying path-finding for walking.");

                    extCitInstMan.Reset(ref extInstance);
                    extInstance.pathMode = ExtPathMode.RequiresWalkingPathToTarget;
                    return ExtSoftPathState.FailedSoft;
                }
            }
        }

        private ExtSoftPathState
            OnCitizenPathFindSuccess_Default(ushort instanceId,
                                             CitizenInstance instanceData,
                                             ref ExtCitizenInstance extInstance,
                                             ref ExtCitizen extCitizen,
                                             bool logParkingAi,
                                             bool usesCar,
                                             ushort parkedVehicleId,
                                             IExtBuildingManager extBuildingMan,
                                             bool usesPublicTransport)
        {
            if (extInstance.pathMode != ExtPathMode.None) {
                if (logParkingAi) {
                    Log._DebugOnlyWarning(
                        $"AdvancedParkingManager.OnCitizenPathFindSuccess({instanceId}): " +
                        $"Unexpected path mode {extInstance.pathMode}! {extInstance}");
                }
            }

            ParkingConfig parkingAiConf = GlobalConfig.Instance.Parking;
            if (usesCar) {
                Log._TraceIf(
                    logParkingAi,
                    () => $"AdvancedParkingManager.OnCitizenPathFindSuccess({instanceId}): " +
                    $"Path for citizen instance {instanceId} contains passenger car " +
                    "section. Ensuring that citizen is allowed to use their car.");

                ushort sourceBuildingId = instanceData.m_sourceBuilding;
                ushort homeId = Singleton<CitizenManager>
                             .instance.m_citizens.m_buffer[instanceData.m_citizen]
                             .m_homeBuilding;

                if (parkedVehicleId == 0) {
                    if (logParkingAi) {
                        Log._Trace(
                            $"AdvancedParkingManager.OnCitizenPathFindSuccess({instanceId}): " +
                            $"Citizen {instanceData.m_citizen} (citizen instance {instanceId}), " +
                            $"source building {sourceBuildingId} does not have a parked " +
                            $"vehicle! CurrentPathMode={extInstance.pathMode}");
                    }

                    // try to spawn parked vehicle in the vicinity of the starting point.
                    VehicleInfo vehicleInfo = null;
                    if (instanceData.Info.m_agePhase > Citizen.AgePhase.Child) {
                        // get a random car info (due to the fact we are using a
                        // different randomizer, car assignment differs from the
                        // selection in ResidentAI.GetVehicleInfo/TouristAI.GetVehicleInfo
                        // method, but this should not matter since we are reusing
                        // parked vehicle infos there)
                        vehicleInfo =
                            Singleton<VehicleManager>.instance.GetRandomVehicleInfo(
                                ref Singleton<SimulationManager>.instance.m_randomizer,
                                ItemClass.Service.Residential,
                                ItemClass.SubService.ResidentialLow,
                                ItemClass.Level.Level1);
                    }

                    if (vehicleInfo != null) {
                        if (logParkingAi) {
                            Log._Trace(
                                $"AdvancedParkingManager.OnCitizenPathFindSuccess({instanceId}): " +
                                $"Citizen {instanceData.m_citizen} (citizen instance {instanceId}), " +
                                $"source building {sourceBuildingId} is using their own passenger car. " +
                                $"CurrentPathMode={extInstance.pathMode}");
                        }

                        // determine current position vector
                        Vector3 currentPos;
                        ushort currentBuildingId = Singleton<CitizenManager>
                                                .instance.m_citizens.m_buffer[instanceData.m_citizen]
                                                .GetBuildingByLocation();
                        Building[] buildingsBuffer = Singleton<BuildingManager>.instance.m_buildings.m_buffer;
                        if (currentBuildingId != 0) {
                            currentPos = buildingsBuffer[currentBuildingId].m_position;
                            if (logParkingAi) {
                                Log._Trace(
                                    $"AdvancedParkingManager.OnCitizenPathFindSuccess({instanceId}): " +
                                    $"Taking current position from current building {currentBuildingId} " +
                                    $"for citizen {instanceData.m_citizen} (citizen instance {instanceId}): " +
                                    $"{currentPos} CurrentPathMode={extInstance.pathMode}");
                            }
                        } else {
                            currentBuildingId = sourceBuildingId;
                            currentPos = instanceData.GetLastFramePosition();
                            if (logParkingAi) {
                                Log._Trace(
                                    $"AdvancedParkingManager.OnCitizenPathFindSuccess({instanceId}): " +
                                    "Taking current position from last frame position for citizen " +
                                    $"{instanceData.m_citizen} (citizen instance {instanceId}): " +
                                    $"{currentPos}. Home {homeId} pos: " +
                                    $"{buildingsBuffer[homeId].m_position} " +
                                    $"CurrentPathMode={extInstance.pathMode}");
                            }
                        }

                        // spawn a passenger car near the current position
                        if (AdvancedParkingManager.Instance.TrySpawnParkedPassengerCar(
                            instanceData.m_citizen,
                            homeId,
                            currentPos,
                            vehicleInfo,
                            out Vector3 parkPos,
                            out ParkingError parkReason)) {
                            parkedVehicleId = Singleton<CitizenManager>
                                              .instance.m_citizens.m_buffer[instanceData.m_citizen]
                                              .m_parkedVehicle;
                            Log._TraceIf(
                                logParkingAi,
                                () => $"AdvancedParkingManager.OnCitizenPathFindSuccess({instanceId}): " +
                                $"Parked vehicle for citizen {instanceData.m_citizen} " +
                                $"(instance {instanceId}) is {parkedVehicleId} now (parkPos={parkPos}).");

                            if (currentBuildingId != 0) {
                                extBuildingMan.ModifyParkingSpaceDemand(
                                    ref extBuildingMan.ExtBuildings[currentBuildingId],
                                    parkPos,
                                    parkingAiConf.MinSpawnedCarParkingSpaceDemandDelta,
                                    parkingAiConf.MaxSpawnedCarParkingSpaceDemandDelta);
                            }
                        } else {
                            Log._TraceIf(
                                logParkingAi,
                                () => $"AdvancedParkingManager.OnCitizenPathFindSuccess({instanceId}): " +
                                $">> Failed to spawn parked vehicle for citizen {instanceData.m_citizen} " +
                                $"(citizen instance {instanceId}). reason={parkReason}. homePos: " +
                                $"{buildingsBuffer[homeId].m_position}");

                            if (parkReason == ParkingError.NoSpaceFound &&
                                currentBuildingId != 0) {
                                extBuildingMan.AddParkingSpaceDemand(
                                    ref extBuildingMan.ExtBuildings[currentBuildingId],
                                    parkingAiConf.FailedSpawnParkingSpaceDemandIncrement);
                            }
                        }
                    } else {
                        Log._TraceIf(
                            logParkingAi,
                            () => $"AdvancedParkingManager.OnCitizenPathFindSuccess({instanceId}): " +
                            $"Citizen {instanceData.m_citizen} (citizen instance {instanceId}), " +
                            $"source building {sourceBuildingId}, home {homeId} does not own a vehicle.");
                    }
                }

                if (parkedVehicleId != 0) {
                    // citizen has to reach their parked vehicle first
                    Log._TraceIf(
                        logParkingAi,
                        () => $"AdvancedParkingManager.OnCitizenPathFindSuccess({instanceId}): " +
                        $"Calculating path to reach parked vehicle {parkedVehicleId} for citizen " +
                        $"instance {instanceId}. targetPos={instanceData.m_targetPos} " +
                        $"lastFramePos={instanceData.GetLastFramePosition()}");
                    extInstance.pathMode = ExtPathMode.RequiresWalkingPathToParkedCar;
                    return ExtSoftPathState.FailedSoft;
                }

                // error! could not find/spawn parked car
                Log._TraceIf(
                    logParkingAi,
                    () => $"AdvancedParkingManager.OnCitizenPathFindSuccess({instanceId}): " +
                    $"Citizen instance {instanceId} still does not have a parked vehicle! " +
                    "Retrying: Cim should walk to target");

                extInstance.pathMode = ExtPathMode.RequiresWalkingPathToTarget;
                return ExtSoftPathState.FailedSoft;
            }

            // path does not contain a car section: path can be reused for walking
            Log._TraceIf(
                logParkingAi,
                () => $"AdvancedParkingManager.OnCitizenPathFindSuccess({instanceId}): A direct car " +
                "path OR initial path was queried that does not contain a car section. " +
                "Switching path mode to walking.");

            if (usesPublicTransport) {
                // decrease public tranport demand
                if (instanceData.m_sourceBuilding != 0) {
                    extBuildingMan.RemovePublicTransportDemand(
                        ref extBuildingMan.ExtBuildings[instanceData.m_sourceBuilding],
                        parkingAiConf.PublicTransportDemandUsageDecrement,
                        true);
                }

                if (instanceData.m_targetBuilding != 0) {
                    extBuildingMan.RemovePublicTransportDemand(
                        ref extBuildingMan.ExtBuildings[instanceData.m_targetBuilding],
                        parkingAiConf.PublicTransportDemandUsageDecrement,
                        false);
                }

                extCitizen.transportMode |= ExtTransportMode.PublicTransport;
            }

            extInstance.pathMode = ExtPathMode.WalkingToTarget;
            return ExtSoftPathState.Ready;
        }

        /// <summary>
        /// Handles a path-finding failure for citizen instances and activated Parking AI.
        /// </summary>
        /// <param name="instanceId">Citizen instance id</param>
        /// <param name="instanceData">Citizen instance data</param>
        /// <param name="extInstance">extended citizen instance information</param>
        /// <param name="extCitizen">extended citizen information</param>
        /// <returns>if true path-finding may be repeated (path mode has been updated), false otherwise</returns>
        protected ExtSoftPathState OnCitizenPathFindFailure(ushort instanceId,
                                                            ref CitizenInstance instanceData,
                                                            ref ExtCitizenInstance extInstance,
                                                            ref ExtCitizen extCitizen) {
            IExtCitizenInstanceManager extCitInstMan = Constants.ManagerFactory.ExtCitizenInstanceManager;
            IExtBuildingManager extBuildingMan = Constants.ManagerFactory.ExtBuildingManager;

#if DEBUG
            bool citizenDebug
                = (DebugSettings.CitizenInstanceId == 0
                   || DebugSettings.CitizenInstanceId == instanceId)
                  && (DebugSettings.CitizenId == 0
                      || DebugSettings.CitizenId == instanceData.m_citizen)
                  && (DebugSettings.SourceBuildingId == 0
                      || DebugSettings.SourceBuildingId == instanceData.m_sourceBuilding)
                  && (DebugSettings.TargetBuildingId == 0
                      || DebugSettings.TargetBuildingId == instanceData.m_targetBuilding);

            bool logParkingAi = DebugSwitch.BasicParkingAILog.Get() && citizenDebug;
            bool extendedLogParkingAi = DebugSwitch.ExtendedParkingAILog.Get() && citizenDebug;
#else
            const bool logParkingAi = false;
            const bool extendedLogParkingAi = false;
#endif

            if (logParkingAi) {
                Log._Trace(
                    $"AdvancedParkingManager.OnCitizenPathFindFailure({instanceId}): Path-finding " +
                    $"failed for citizen instance {extInstance.instanceId}. " +
                    $"CurrentPathMode={extInstance.pathMode}");
            }

            // update public transport demands
            if (extInstance.pathMode == ExtPathMode.None ||
                extInstance.pathMode == ExtPathMode.CalculatingWalkingPathToTarget ||
                extInstance.pathMode == ExtPathMode.CalculatingWalkingPathToParkedCar ||
                extInstance.pathMode == ExtPathMode.TaxiToTarget) {
                // could not reach target building by walking/driving/public transport: increase
                // public transport demand
                if ((instanceData.m_flags & CitizenInstance.Flags.CannotUseTransport) ==
                    CitizenInstance.Flags.None) {
                    if (extendedLogParkingAi) {
                        Log._Trace(
                            $"AdvancedParkingManager.OnCitizenPathFindFailure({instanceId}): " +
                            "Increasing public transport demand of target building " +
                            $"{instanceData.m_targetBuilding} and source building " +
                            $"{instanceData.m_sourceBuilding}");
                    }

                    if (instanceData.m_targetBuilding != 0) {
                        extBuildingMan.AddPublicTransportDemand(
                            ref extBuildingMan.ExtBuildings[instanceData.m_targetBuilding],
                            GlobalConfig.Instance.Parking.PublicTransportDemandIncrement,
                            false);
                    }

                    if (instanceData.m_sourceBuilding != 0) {
                        extBuildingMan.AddPublicTransportDemand(
                            ref extBuildingMan.ExtBuildings[instanceData.m_sourceBuilding],
                            GlobalConfig.Instance.Parking.PublicTransportDemandIncrement,
                            true);
                    }
                }
            }

            // relocate parked car if abandoned
            if (extInstance.pathMode == ExtPathMode.CalculatingWalkingPathToParkedCar) {
                // parked car is unreachable
                Citizen[] citizensBuffer = Singleton<CitizenManager>.instance.m_citizens.m_buffer;
                ushort parkedVehicleId = citizensBuffer[instanceData.m_citizen].m_parkedVehicle;

                if (parkedVehicleId != 0) {
                    // parked car is present
                    ushort homeId = 0;
                    Services.CitizenService.ProcessCitizen(
                        extCitizen.citizenId,
                        (uint citId, ref Citizen cit) => {
                            homeId = cit.m_homeBuilding;
                            return true;
                        });

                    // calculate distance between citizen and parked car
                    var movedCar = false;
                    Vector3 citizenPos = instanceData.GetLastFramePosition();
                    var parkedToCitizen = 0f;
                    Vector3 oldParkedVehiclePos = default;

                    Services.VehicleService.ProcessParkedVehicle(
                        parkedVehicleId,
                        (ushort parkedVehId, ref VehicleParked parkedVehicle) => {
                            oldParkedVehiclePos = parkedVehicle.m_position;
                            parkedToCitizen = (parkedVehicle.m_position - citizenPos).magnitude;
                            if (parkedToCitizen > GlobalConfig.Instance.Parking.MaxParkedCarDistanceToHome) {
                                // parked car is far away from current location
                                // -> relocate parked car and try again
                                movedCar = TryMoveParkedVehicle(
                                    parkedVehicleId,
                                    ref parkedVehicle,
                                    citizenPos,
                                    GlobalConfig.Instance.Parking.MaxParkedCarDistanceToHome,
                                    homeId);
                            }

                            return true;
                        });

                    if (movedCar) {
                        // successfully moved the parked car to a closer location
                        // -> retry path-finding
                        extInstance.pathMode = ExtPathMode.RequiresWalkingPathToParkedCar;
                        Vector3 parkedPos = Singleton<VehicleManager>.instance.m_parkedVehicles
                                                                 .m_buffer[parkedVehicleId]
                                                                 .m_position;
                        if (extendedLogParkingAi) {
                            Log._Trace(
                                $"AdvancedParkingManager.OnCitizenPathFindFailure({instanceId}): " +
                                $"Relocated parked car {parkedVehicleId} to a closer location (old pos/distance: " +
                                $"{oldParkedVehiclePos}/{parkedToCitizen}, new pos/distance: " +
                                $"{parkedPos}/{(parkedPos - citizenPos).magnitude}) " +
                                $"for citizen @ {citizenPos}. Retrying path-finding. " +
                                $"CurrentPathMode={extInstance.pathMode}");
                        }

                        return ExtSoftPathState.FailedSoft;
                    }

                    // could not move car
                    // -> despawn parked car, walk to target or use public transport
                    if (extendedLogParkingAi) {
                        Log._Trace(
                            $"AdvancedParkingManager.OnCitizenPathFindFailure({instanceId}): " +
                            $"Releasing unreachable parked vehicle {parkedVehicleId} for citizen " +
                            $"instance {extInstance.instanceId}. CurrentPathMode={extInstance.pathMode}");
                    }

                    Singleton<VehicleManager>.instance.ReleaseParkedVehicle(parkedVehicleId);
                }
            }

            // check if path-finding may be repeated
            var ret = ExtSoftPathState.FailedHard;
            switch (extInstance.pathMode) {
                case ExtPathMode.CalculatingCarPathToTarget:
                case ExtPathMode.CalculatingCarPathToKnownParkPos:
                case ExtPathMode.CalculatingWalkingPathToParkedCar: {
                    // try to walk to target
                    Log._TraceIf(
                        logParkingAi,
                        () => $"AdvancedParkingManager.OnCitizenPathFindFailure({instanceId}): " +
                        "Path failed but it may be retried to walk to the target.");
                    extInstance.pathMode = ExtPathMode.RequiresWalkingPathToTarget;
                    ret = ExtSoftPathState.FailedSoft;
                    break;
                }

                default: {
                    Log._TraceIf(
                        logParkingAi,
                        () => $"AdvancedParkingManager.OnCitizenPathFindFailure({instanceId}): " +
                        "Path failed and walking to target is not an option. Resetting ext. instance.");
                    extCitInstMan.Reset(ref extInstance);
                    break;
                }
            }

            if (logParkingAi) {
                Log._Trace(
                    $"AdvancedParkingManager.OnCitizenPathFindFailure({instanceId}): " +
                    $"Setting CurrentPathMode for citizen instance {extInstance.instanceId} " +
                    $"to {extInstance.pathMode}, ret={ret}");
            }

            // reset current transport mode for hard failures
            if (ret == ExtSoftPathState.FailedHard) {
                extCitizen.transportMode = ExtTransportMode.None;
            }

            return ret;
        }

        /// <summary>
        /// Handles a path-finding failure for citizen instances and activated Parking AI.
        /// </summary>
        /// <param name="vehicleId">Vehicle id</param>
        /// <param name="vehicleData">Vehicle data</param>
        /// <param name="driverInstanceData">Driver citizen instance data</param>
        /// <param name="driverExtInstance">extended citizen instance information of driver</param>
        /// <returns>if true path-finding may be repeated (path mode has been updated), false otherwise</returns>
        [UsedImplicitly]
        protected ExtSoftPathState OnCarPathFindFailure(ushort vehicleId,
                                                        ref Vehicle vehicleData,
                                                        ref CitizenInstance driverInstanceData,
                                                        ref ExtCitizenInstance driverExtInstance) {
            IExtCitizenInstanceManager extCitizenInstanceManager = Constants.ManagerFactory.ExtCitizenInstanceManager;
#if DEBUG
            bool citizenDebug
                = (DebugSettings.VehicleId == 0
                   || DebugSettings.VehicleId == vehicleId)
                  && (DebugSettings.CitizenInstanceId == 0
                      || DebugSettings.CitizenInstanceId == driverExtInstance.instanceId)
                  && (DebugSettings.CitizenId == 0
                      || DebugSettings.CitizenId == driverInstanceData.m_citizen)
                  && (DebugSettings.SourceBuildingId == 0
                      || DebugSettings.SourceBuildingId == driverInstanceData.m_sourceBuilding)
                  && (DebugSettings.TargetBuildingId == 0
                      || DebugSettings.TargetBuildingId == driverInstanceData.m_targetBuilding);

            bool logParkingAi = DebugSwitch.BasicParkingAILog.Get() && citizenDebug;
            bool extendedLogParkingAi = DebugSwitch.ExtendedParkingAILog.Get() && citizenDebug;
#else
            const bool logParkingAi = false;
            const bool extendedLogParkingAi = false;
#endif

            if (logParkingAi) {
                Log._Trace(
                    $"AdvancedParkingManager.OnCarPathFindFailure({vehicleId}): Path-finding failed " +
                    $"for driver citizen instance {driverExtInstance.instanceId}. " +
                    $"CurrentPathMode={driverExtInstance.pathMode}");
            }

            // update parking demands
            switch (driverExtInstance.pathMode) {
                case ExtPathMode.None:
                case ExtPathMode.CalculatingCarPathToAltParkPos:
                case ExtPathMode.CalculatingCarPathToKnownParkPos: {
                    // could not reach target building by driving: increase parking space demand
                    if (extendedLogParkingAi) {
                        Log._Trace(
                            $"AdvancedParkingManager.OnCarPathFindFailure({vehicleId}): " +
                            "Increasing parking space demand of target building " +
                            $"{driverInstanceData.m_targetBuilding}");
                    }

                    if (driverInstanceData.m_targetBuilding != 0) {
                        IExtBuildingManager extBuildingManager = Constants.ManagerFactory.ExtBuildingManager;
                        extBuildingManager.AddParkingSpaceDemand(
                            ref extBuildingManager.ExtBuildings[driverInstanceData.m_targetBuilding],
                            GlobalConfig.Instance.Parking.FailedParkingSpaceDemandIncrement);
                    }

                    break;
                }
            }

            // check if path-finding may be repeated
            var ret = ExtSoftPathState.FailedHard;
            switch (driverExtInstance.pathMode) {
                case ExtPathMode.CalculatingCarPathToAltParkPos:
                case ExtPathMode.CalculatingCarPathToKnownParkPos: {
                    // try to drive directly to the target if public transport is allowed
                    if ((driverInstanceData.m_flags & CitizenInstance.Flags.CannotUseTransport) ==
                        CitizenInstance.Flags.None) {
                        Log._TraceIf(
                            logParkingAi,
                            () => $"AdvancedParkingManager.OnCarPathFindFailure({vehicleId}): " +
                            "Path failed but it may be retried to drive directly to the target " +
                            "/ using public transport.");

                        driverExtInstance.pathMode = ExtPathMode.RequiresMixedCarPathToTarget;
                        ret = ExtSoftPathState.FailedSoft;
                    }

                    break;
                }

                default: {
                    Log._TraceIf(
                        logParkingAi,
                        () => $"AdvancedParkingManager.OnCarPathFindFailure({vehicleId}): Path failed " +
                        "and a direct target is not an option. Resetting driver ext. instance.");
                    extCitizenInstanceManager.Reset(ref driverExtInstance);
                    break;
                }
            }

            if (logParkingAi) {
                Log._Trace(
                    $"AdvancedParkingManager.OnCarPathFindFailure({vehicleId}): Setting " +
                    $"CurrentPathMode for driver citizen instance {driverExtInstance.instanceId} " +
                    $"to {driverExtInstance.pathMode}, ret={ret}");
            }

            return ret;
        }

        public bool TryMoveParkedVehicle(ushort parkedVehicleId,
                                         ref VehicleParked parkedVehicle,
                                         Vector3 refPos,
                                         float maxDistance,
                                         ushort homeId) {
            bool found;
            Vector3 parkPos;
            Quaternion parkRot;

            found = Constants.ManagerFactory.ParkingSpaceManager.FindInVicinity(
                    refPos,
                    Vector3.zero,
                    parkedVehicle.Info,
                    homeId,
                    0,
                    maxDistance,
                    out _,
                    out _,
                    out parkPos,
                    out parkRot,
                    out _);

            if (found) {
                Singleton<VehicleManager>.instance.RemoveFromGrid(parkedVehicleId, ref parkedVehicle);
                parkedVehicle.m_position = parkPos;
                parkedVehicle.m_rotation = parkRot;
                Singleton<VehicleManager>.instance.AddToGrid(parkedVehicleId, ref parkedVehicle);
            }

            return found;
        }

        public bool TrySpawnParkedPassengerCar(uint citizenId,
                                               ushort homeId,
                                               Vector3 refPos,
                                               VehicleInfo vehicleInfo,
                                               out Vector3 parkPos,
                                               out ParkingError reason) {
#if DEBUG
            bool citizenDebug = DebugSettings.CitizenId == 0 || DebugSettings.CitizenId == citizenId;
            // var logParkingAi = DebugSwitch.BasicParkingAILog.Get() && citizenDebug;
            bool extendedLogParkingAi = DebugSwitch.ExtendedParkingAILog.Get() && citizenDebug;
#else
            // const bool logParkingAi = false;
            const bool extendedLogParkingAi = false;
#endif
            Log._TraceIf(
                extendedLogParkingAi && homeId != 0,
                () => $"Trying to spawn parked passenger car for citizen {citizenId}, " +
                $"home {homeId} @ {refPos}");

            bool roadParkSuccess = TrySpawnParkedPassengerCarRoadSide(
                citizenId,
                refPos,
                vehicleInfo,
                out Vector3 roadParkPos,
                out ParkingError roadParkReason);

            bool buildingParkSuccess = TrySpawnParkedPassengerCarBuilding(
                citizenId,
                homeId,
                refPos,
                vehicleInfo,
                out Vector3 buildingParkPos,
                out ParkingError buildingParkReason);

            if ((!roadParkSuccess && !buildingParkSuccess)
                || (roadParkSuccess && !buildingParkSuccess)) {
                parkPos = roadParkPos;
                reason = roadParkReason;
                return roadParkSuccess;
            }

            if (!roadParkSuccess) {
                parkPos = buildingParkPos;
                reason = buildingParkReason;
                return true;
            }

            if ((roadParkPos - refPos).sqrMagnitude < (buildingParkPos - refPos).sqrMagnitude) {
                parkPos = roadParkPos;
                reason = roadParkReason;
                return true;
            }

            parkPos = buildingParkPos;
            reason = buildingParkReason;
            return true;
        }

        public bool TrySpawnParkedPassengerCarRoadSide(uint citizenId,
                                                       Vector3 refPos,
                                                       VehicleInfo vehicleInfo,
                                                       out Vector3 parkPos,
                                                       out ParkingError reason) {
#if DEBUG
            bool citizenDebug = DebugSettings.CitizenId == 0 || DebugSettings.CitizenId == citizenId;
            bool logParkingAi = DebugSwitch.BasicParkingAILog.Get() && citizenDebug;

            // bool extendedLogParkingAi = DebugSwitch.ExtendedParkingAILog.Get() && citizenDebug;
#else
            const bool logParkingAi = false;
            // const bool extendedLogParkingAi = false;
#endif

            Log._TraceIf(
                logParkingAi,
                () => $"Trying to spawn parked passenger car at road side for citizen {citizenId} @ {refPos}");

            parkPos = Vector3.zero;

            if (Constants.ManagerFactory.ParkingSpaceManager.FindRoadSide(
                0,
                refPos,
                vehicleInfo.m_generatedInfo.m_size.x,
                vehicleInfo.m_generatedInfo.m_size.z,
                GlobalConfig.Instance.Parking.MaxParkedCarDistanceToBuilding,
                out parkPos,
                out Quaternion parkRot,
                out _))
            {
                // position found, spawn a parked vehicle
                if (Singleton<VehicleManager>.instance.CreateParkedVehicle(
                    out ushort parkedVehicleId,
                    ref Singleton<SimulationManager>
                        .instance.m_randomizer,
                    vehicleInfo,
                    parkPos,
                    parkRot,
                    citizenId))
                {
                    Singleton<CitizenManager>.instance.m_citizens.m_buffer[citizenId]
                        .SetParkedVehicle(citizenId, parkedVehicleId);

                    Singleton<VehicleManager>.instance.m_parkedVehicles.m_buffer[parkedVehicleId].m_flags
                        &= (ushort)(VehicleParked.Flags.All & ~VehicleParked.Flags.Parking);

                    if (logParkingAi) {
                        Log._Trace(
                            "[SUCCESS] Spawned parked passenger car at road side for citizen " +
                            $"{citizenId}: {parkedVehicleId} @ {parkPos}");
                    }

                    reason = ParkingError.None;
                    return true;
                }

                reason = ParkingError.LimitHit;
            } else {
                reason = ParkingError.NoSpaceFound;
            }

            Log._TraceIf(
                logParkingAi,
                () => $"[FAIL] Failed to spawn parked passenger car at road side for citizen {citizenId}");
            return false;
        }

        public bool TrySpawnParkedPassengerCarBuilding(uint citizenId,
                                                       ushort homeId,
                                                       Vector3 refPos,
                                                       VehicleInfo vehicleInfo,
                                                       out Vector3 parkPos,
                                                       out ParkingError reason) {
#if DEBUG
            bool citizenDebug = DebugSettings.CitizenId == 0 || DebugSettings.CitizenId == citizenId;
            bool logParkingAi = DebugSwitch.BasicParkingAILog.Get() && citizenDebug;
            bool extendedLogParkingAi = DebugSwitch.ExtendedParkingAILog.Get() && citizenDebug;
#else
            const bool logParkingAi = false;
            const bool extendedLogParkingAi = false;
#endif

            Log._TraceIf(
                extendedLogParkingAi && homeId != 0,
                () => "Trying to spawn parked passenger car next to building for citizen " +
                $"{citizenId} @ {refPos}");

            parkPos = Vector3.zero;

            if (Constants.ManagerFactory.ParkingSpaceManager.FindInBuilding(
                vehicleInfo,
                homeId,
                0,
                0,
                refPos,
                GlobalConfig.Instance.Parking.MaxParkedCarDistanceToBuilding,
                GlobalConfig.Instance.Parking.MaxParkedCarDistanceToBuilding,
                out parkPos,
                out Quaternion parkRot,
                out _))
            {
                // position found, spawn a parked vehicle
                if (Singleton<VehicleManager>.instance.CreateParkedVehicle(
                    out ushort parkedVehicleId,
                    ref Singleton<SimulationManager>.instance.m_randomizer,
                    vehicleInfo,
                    parkPos,
                    parkRot,
                    citizenId))
                {
                    Singleton<CitizenManager>.instance.m_citizens.m_buffer[citizenId]
                        .SetParkedVehicle(citizenId, parkedVehicleId);

                    Singleton<VehicleManager>.instance.m_parkedVehicles.m_buffer[parkedVehicleId].m_flags
                        &= (ushort)(VehicleParked.Flags.All & ~VehicleParked.Flags.Parking);

                    if (extendedLogParkingAi && homeId != 0) {
                        Log._Trace(
                            "[SUCCESS] Spawned parked passenger car next to building for citizen " +
                            $"{citizenId}: {parkedVehicleId} @ {parkPos}");
                    }

                    reason = ParkingError.None;
                    return true;
                }

                reason = ParkingError.LimitHit;
            } else {
                reason = ParkingError.NoSpaceFound;
            }

            Log._TraceIf(
                logParkingAi && homeId != 0,
                () => "[FAIL] Failed to spawn parked passenger car next to building " +
                $"for citizen {citizenId}");
            return false;
        }

        public bool GetBuildingInfoViewColor(ushort buildingId,
                                             ref Building buildingData,
                                             ref ExtBuilding extBuilding,
                                             InfoManager.InfoMode infoMode,
                                             out Color? color) {
            color = null;
            InfoProperties.ModeProperties[] modeProperties
                = Singleton<InfoManager>.instance.m_properties.m_modeProperties;

            switch (infoMode) {
                case InfoManager.InfoMode.Traffic: {
                    // parking space demand info view
                    color = Color.Lerp(
                        modeProperties[(int)infoMode].m_targetColor,
                        modeProperties[(int)infoMode].m_negativeColor,
                        Mathf.Clamp01(extBuilding.parkingSpaceDemand * 0.01f));
                    return true;
                }

                case InfoManager.InfoMode.Transport when !(buildingData.Info.m_buildingAI is DepotAI): {
                    // public transport demand info view
                    // TODO should not depend on UI class "TrafficManagerTool"
                    color = Color.Lerp(
                        modeProperties[(int)InfoManager.InfoMode.Traffic].m_targetColor,
                        modeProperties[(int)InfoManager.InfoMode.Traffic].m_negativeColor,
                        Mathf.Clamp01(
                            (TrafficManagerTool.CurrentTransportDemandViewMode ==
                             TransportDemandViewMode.Outgoing
                                 ? extBuilding.outgoingPublicTransportDemand
                                 : extBuilding.incomingPublicTransportDemand) * 0.01f));
                    return true;
                }

                default:
                    return false;
            }
        }

        public string EnrichLocalizedCitizenStatus(string ret,
                                                   ref ExtCitizenInstance extInstance,
                                                   ref ExtCitizen extCitizen) {
            switch (extInstance.pathMode) {
                case ExtPathMode.ApproachingParkedCar:
                case ExtPathMode.RequiresCarPath:
                case ExtPathMode.RequiresMixedCarPathToTarget: {
                    ret = Translation.AICitizen.Get("Label:Entering vehicle") + ", " + ret;
                    break;
                }

                case ExtPathMode.RequiresWalkingPathToParkedCar:
                case ExtPathMode.CalculatingWalkingPathToParkedCar:
                case ExtPathMode.WalkingToParkedCar: {
                    ret = Translation.AICitizen.Get("Label:Walking to car") + ", " + ret;
                    break;
                }

                case ExtPathMode.CalculatingWalkingPathToTarget:
                case ExtPathMode.TaxiToTarget:
                case ExtPathMode.WalkingToTarget: {
                    if ((extCitizen.transportMode & ExtTransportMode.PublicTransport) != ExtTransportMode.None) {
                        ret = Translation.AICitizen.Get("Label:Using public transport")
                              + ", " + ret;
                    } else {
                        ret = Translation.AICitizen.Get("Label:Walking") + ", " + ret;
                    }

                    break;
                }

                case ExtPathMode.CalculatingCarPathToTarget:
                case ExtPathMode.CalculatingCarPathToKnownParkPos: {
                    ret = Translation.AICitizen.Get("Label:Thinking of a good parking spot")
                          + ", " + ret;
                    break;
                }
            }

            return ret;
        }

        public string EnrichLocalizedCarStatus(string ret, ref ExtCitizenInstance driverExtInstance) {
            switch (driverExtInstance.pathMode) {
                case ExtPathMode.DrivingToAltParkPos: {
                    if (driverExtInstance.failedParkingAttempts <= 1) {
                        ret = Translation.AICar.Get("Label:Driving to a parking spot")
                              + ", " + ret;
                    } else {
                        ret = Translation.AICar.Get("Label:Driving to another parking spot")
                              + " (#" + driverExtInstance.failedParkingAttempts + "), " + ret;
                    }

                    break;
                }

                case ExtPathMode.CalculatingCarPathToKnownParkPos:
                case ExtPathMode.DrivingToKnownParkPos: {
                    ret = Translation.AICar.Get("Label:Driving to a parking spot") + ", " + ret;
                    break;
                }

                case ExtPathMode.ParkingFailed:
                case ExtPathMode.CalculatingCarPathToAltParkPos: {
                    ret = Translation.AICar.Get("Label:Looking for a parking spot") + ", " + ret;
                    break;
                }

                case ExtPathMode.RequiresWalkingPathToTarget: {
                    ret = Locale.Get("VEHICLE_STATUS_PARKING") + ", " + ret;
                    break;
                }
            }

            return ret;
        }
    }
}
