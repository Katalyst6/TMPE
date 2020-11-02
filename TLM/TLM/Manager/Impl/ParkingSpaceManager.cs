namespace TrafficManager.Manager.Impl {
    using ColossalFramework.Globalization;
    using ColossalFramework.Math;
    using ColossalFramework;
    using CSUtil.Commons;
    using JetBrains.Annotations;
    using System;
    using TrafficManager.API.Manager;
    using TrafficManager.API.Traffic.Data;
    using TrafficManager.API.Traffic.Enums;
    using TrafficManager.Custom.AI;
    using TrafficManager.Custom.PathFinding;
    using TrafficManager.State.ConfigData;
    using TrafficManager.State;
    using TrafficManager.UI;
    using TrafficManager.Util;
    using UnityEngine;

    public class ParkingSpaceManager
        : AbstractCustomManager,
        IParkingSpaceManager {

        private readonly Spiral _spiral;

        public static readonly ParkingSpaceManager Instance
            = new ParkingSpaceManager(SingletonLite<Spiral>.instance);

        public ParkingSpaceManager(Spiral spiral) {
            _spiral = spiral ?? throw new ArgumentNullException(nameof(spiral));
        }

        /// <inheritdoc />
        public bool FindParkingSpaceForCitizen(Vector3 endPos,
                                               VehicleInfo vehicleInfo,
                                               ref ExtCitizenInstance extDriverInstance,
                                               ushort homeId,
                                               bool goingHome,
                                               ushort vehicleId,
                                               bool allowTourists,
                                               out Vector3 parkPos,
                                               ref PathUnit.Position endPathPos,
                                               out bool calculateEndPos) {
            IExtCitizenInstanceManager extCitInstMan = Constants.ManagerFactory.ExtCitizenInstanceManager;

#if DEBUG
            CitizenInstance[] citizensBuffer = Singleton<CitizenManager> .instance.m_instances.m_buffer;
            ushort ctzTargetBuilding = citizensBuffer[extDriverInstance.instanceId] .m_targetBuilding;
            ushort ctzSourceBuilding = citizensBuffer[extDriverInstance.instanceId] .m_sourceBuilding;

            bool citizenDebug
                    = (DebugSettings.VehicleId == 0
                       || DebugSettings.VehicleId == vehicleId)
                      && (DebugSettings.CitizenInstanceId == 0
                          || DebugSettings.CitizenInstanceId == extDriverInstance.instanceId)
                      && (DebugSettings.CitizenId == 0
                          || DebugSettings.CitizenId == extCitInstMan.GetCitizenId(extDriverInstance.instanceId))
                      && (DebugSettings.SourceBuildingId == 0
                          || DebugSettings.SourceBuildingId == ctzSourceBuilding)
                      && (DebugSettings.TargetBuildingId == 0
                          || DebugSettings.TargetBuildingId == ctzTargetBuilding);

            bool logParkingAi = DebugSwitch.BasicParkingAILog.Get() && citizenDebug;
            bool extendedLogParkingAi = DebugSwitch.ExtendedParkingAILog.Get() && citizenDebug;
#else
            const bool logParkingAi = false;
            const bool extendedLogParkingAi = false;
#endif

            calculateEndPos = true;
            parkPos = default;

            if (!allowTourists) {
                // TODO remove this from this method
                uint citizenId = extCitInstMan.GetCitizenId(extDriverInstance.instanceId);

                if (citizenId == 0 ||
                    (Singleton<CitizenManager>.instance.m_citizens.m_buffer[citizenId].m_flags &
                     Citizen.Flags.Tourist) != Citizen.Flags.None) {
                    return false;
                }
            }

            if (extendedLogParkingAi) {
                Log._Trace(
                    $"Citizen instance {extDriverInstance.instanceId} " +
                    $"(CurrentPathMode={extDriverInstance.pathMode}) can still use their passenger " +
                    "car and is either not a tourist or wants to find an alternative parking spot. " +
                    "Finding a parking space before starting path-finding.");
            }

            // find a free parking space
            bool success = FindParkingSpaceInVicinity(
                endPos,
                Vector3.zero,
                vehicleInfo,
                homeId,
                vehicleId,
                goingHome
                    ? GlobalConfig.Instance.Parking.MaxParkedCarDistanceToHome
                    : GlobalConfig.Instance.Parking.MaxParkedCarDistanceToBuilding,
                out ExtParkingSpaceLocation knownParkingSpaceLocation,
                out ushort knownParkingSpaceLocationId,
                out parkPos,
                out _,
                out float parkOffset);

            extDriverInstance.parkingSpaceLocation = knownParkingSpaceLocation;
            extDriverInstance.parkingSpaceLocationId = knownParkingSpaceLocationId;

            if (!success) {
                return false;
            }

            if (extendedLogParkingAi) {
                Log._Trace(
                    $"Found a parking spot for citizen instance {extDriverInstance.instanceId} " +
                    $"(CurrentPathMode={extDriverInstance.pathMode}) before starting car path: " +
                    $"{knownParkingSpaceLocation} @ {knownParkingSpaceLocationId}");
            }

            switch (knownParkingSpaceLocation) {
                case ExtParkingSpaceLocation.RoadSide: {
                    // found segment with parking space
                    if (logParkingAi) {
                        Log._Trace(
                            $"Found segment {knownParkingSpaceLocationId} for road-side parking " +
                            $"position for citizen instance {extDriverInstance.instanceId}!");
                    }

                    // determine nearest sidewalk position for parking position at segment
                    if (Singleton<NetManager>.instance.m_segments.m_buffer[knownParkingSpaceLocationId]
                        .GetClosestLanePosition(
                            parkPos,
                            NetInfo.LaneType.Pedestrian,
                            VehicleInfo.VehicleType.None,
                            out _,
                            out uint laneId,
                            out int laneIndex,
                            out _)) {
                        endPathPos.m_segment = knownParkingSpaceLocationId;
                        endPathPos.m_lane = (byte)laneIndex;
                        endPathPos.m_offset = (byte)(parkOffset * 255f);
                        calculateEndPos = false;

                        // extDriverInstance.CurrentPathMode = successMode;
                        // ExtCitizenInstance.PathMode.CalculatingKnownCarPath;
                        if (logParkingAi) {
                            Log._Trace(
                                "Found an parking spot sidewalk position for citizen instance " +
                                $"{extDriverInstance.instanceId} @ segment {knownParkingSpaceLocationId}, " +
                                $"laneId {laneId}, laneIndex {laneIndex}, offset={endPathPos.m_offset}! " +
                                $"CurrentPathMode={extDriverInstance.pathMode}");
                        }

                        return true;
                    }

                    if (logParkingAi) {
                        Log._Trace(
                            "Could not find an alternative parking spot sidewalk position for " +
                            $"citizen instance {extDriverInstance.instanceId}! " +
                            $"CurrentPathMode={extDriverInstance.pathMode}");
                    }

                    return false;
                }

                case ExtParkingSpaceLocation.Building: {
                    // found a building with parking space
                    if (Constants.ManagerFactory.ExtPathManager.FindPathPositionWithSpiralLoop(
                        parkPos,
                        endPos,
                        ItemClass.Service.Road,
                        NetInfo.LaneType.Pedestrian,
                        VehicleInfo.VehicleType.None,
                        NetInfo.LaneType.Vehicle | NetInfo.LaneType.TransportVehicle,
                        VehicleInfo.VehicleType.Car,
                        false,
                        false,
                        GlobalConfig.Instance.Parking.MaxBuildingToPedestrianLaneDistance,
                        out endPathPos)) {
                        calculateEndPos = false;
                    }

                    if (logParkingAi) {
                        Log._Trace(
                            $"Navigating citizen instance {extDriverInstance.instanceId} to parking " +
                            $"building {knownParkingSpaceLocationId}! segment={endPathPos.m_segment}, " +
                            $"laneIndex={endPathPos.m_lane}, offset={endPathPos.m_offset}. " +
                            $"CurrentPathMode={extDriverInstance.pathMode} " +
                            $"calculateEndPos={calculateEndPos}");
                    }

                    return true;
                }

                default:
                    return false;
            }
        }

        /// <inheritdoc />
        public bool FindParkingSpaceInVicinity(Vector3 targetPos,
                                               Vector3 searchDir,
                                               VehicleInfo vehicleInfo,
                                               ushort homeId,
                                               ushort vehicleId,
                                               float maxDist,
                                               out ExtParkingSpaceLocation parkingSpaceLocation,
                                               out ushort parkingSpaceLocationId,
                                               out Vector3 parkPos,
                                               out Quaternion parkRot,
                                               out float parkOffset) {
#if DEBUG
            bool vehDebug = DebugSettings.VehicleId == 0 || DebugSettings.VehicleId == vehicleId;
            bool logParkingAi = DebugSwitch.VehicleParkingAILog.Get() && vehDebug;
#else
            const bool logParkingAi = false;
#endif

            // TODO check isElectric
            Vector3 refPos = targetPos + (searchDir * 16f);

            // TODO depending on simulation accuracy, disable searching for both road-side and building parking spaces
            ushort parkingSpaceSegmentId = FindParkingSpaceAtRoadSide(
                0,
                refPos,
                vehicleInfo.m_generatedInfo.m_size.x,
                vehicleInfo.m_generatedInfo.m_size.z,
                maxDist,
                true,
                out Vector3 roadParkPos,
                out Quaternion roadParkRot,
                out float roadParkOffset);

            ushort parkingBuildingId = FindParkingSpaceBuilding(
                vehicleInfo,
                homeId,
                0,
                0,
                refPos,
                maxDist,
                maxDist,
                true,
                out Vector3 buildingParkPos,
                out Quaternion buildingParkRot,
                out float buildingParkOffset);

            if (parkingSpaceSegmentId != 0) {
                if (parkingBuildingId != 0) {
                    Randomizer rng = Services.SimulationService.Randomizer;

                    // choose nearest parking position, after a bit of randomization
                    if ((roadParkPos - targetPos).magnitude < (buildingParkPos - targetPos).magnitude
                        && rng.Int32(GlobalConfig.Instance.Parking.VicinityParkingSpaceSelectionRand) != 0) {
                        // road parking space is closer

                        Log._TraceIf(
                            logParkingAi,
                            () => "Found an (alternative) road-side parking position for " +
                            $"vehicle {vehicleId} @ segment {parkingSpaceSegmentId} after comparing " +
                            $"distance with a bulding parking position @ {parkingBuildingId}!");

                        parkPos = roadParkPos;
                        parkRot = roadParkRot;
                        parkOffset = roadParkOffset;
                        parkingSpaceLocation = ExtParkingSpaceLocation.RoadSide;
                        parkingSpaceLocationId = parkingSpaceSegmentId;
                        return true;
                    }

                    // choose building parking space
                    Log._TraceIf(
                        logParkingAi,
                        () => $"Found an alternative building parking position for vehicle {vehicleId} " +
                        $"at building {parkingBuildingId} after comparing distance with a road-side " +
                        $"parking position @ {parkingSpaceSegmentId}!");

                    parkPos = buildingParkPos;
                    parkRot = buildingParkRot;
                    parkOffset = buildingParkOffset;
                    parkingSpaceLocation = ExtParkingSpaceLocation.Building;
                    parkingSpaceLocationId = parkingBuildingId;
                    return true;
                }

                // road-side but no building parking space found
                Log._TraceIf(
                    logParkingAi,
                    () => "Found an alternative road-side parking position for vehicle " +
                    $"{vehicleId} @ segment {parkingSpaceSegmentId}!");

                parkPos = roadParkPos;
                parkRot = roadParkRot;
                parkOffset = roadParkOffset;
                parkingSpaceLocation = ExtParkingSpaceLocation.RoadSide;
                parkingSpaceLocationId = parkingSpaceSegmentId;
                return true;
            }

            if (parkingBuildingId != 0) {
                // building but no road-side parking space found
                Log._TraceIf(
                    logParkingAi,
                    () => $"Found an alternative building parking position for vehicle {vehicleId} " +
                    $"at building {parkingBuildingId}!");

                parkPos = buildingParkPos;
                parkRot = buildingParkRot;
                parkOffset = buildingParkOffset;
                parkingSpaceLocation = ExtParkingSpaceLocation.Building;
                parkingSpaceLocationId = parkingBuildingId;
                return true;
            }

            // driverExtInstance.CurrentPathMode = ExtCitizenInstance.PathMode.AltParkFailed;
            parkingSpaceLocation = ExtParkingSpaceLocation.None;
            parkingSpaceLocationId = 0;
            parkPos = default;
            parkRot = default;
            parkOffset = -1f;
            Log._TraceIf(
                logParkingAi,
                () => $"Could not find a road-side or building parking position for vehicle {vehicleId}!");
            return false;
        }

        /// <inheritdoc />
        protected ushort FindParkingSpaceAtRoadSide(ushort ignoreParked,
                                                    Vector3 refPos,
                                                    float width,
                                                    float length,
                                                    float maxDistance,
                                                    bool randomize,
                                                    out Vector3 parkPos,
                                                    out Quaternion parkRot,
                                                    out float parkOffset) {
#if DEBUG
            bool logParkingAi = DebugSwitch.VehicleParkingAILog.Get();
#else
            const bool logParkingAi = false;
#endif

            parkPos = Vector3.zero;
            parkRot = Quaternion.identity;
            parkOffset = 0f;

            var centerI = (int)((refPos.z / BuildingManager.BUILDINGGRID_CELL_SIZE) +
                                (BuildingManager.BUILDINGGRID_RESOLUTION / 2f));
            var centerJ = (int)((refPos.x / BuildingManager.BUILDINGGRID_CELL_SIZE) +
                                (BuildingManager.BUILDINGGRID_RESOLUTION / 2f));

            int radius = Math.Max(1, (int)(maxDistance / (BuildingManager.BUILDINGGRID_CELL_SIZE / 2f)) + 1);

            NetManager netManager = Singleton<NetManager>.instance;
            Randomizer rng = Singleton<SimulationManager>.instance.m_randomizer;

            ushort foundSegmentId = 0;
            Vector3 myParkPos = parkPos;
            Quaternion myParkRot = parkRot;
            float myParkOffset = parkOffset;

            // Local function used for spiral loop below
            bool LoopHandler(int i, int j) {
                if (i < 0 || i >= BuildingManager.BUILDINGGRID_RESOLUTION || j < 0 ||
                    j >= BuildingManager.BUILDINGGRID_RESOLUTION) {
                    return true;
                }

                ushort segmentId =
                    netManager.m_segmentGrid[(i * BuildingManager.BUILDINGGRID_RESOLUTION) + j];
                var iterations = 0;

                while (segmentId != 0) {
                    NetInfo segmentInfo = netManager.m_segments.m_buffer[segmentId].Info;
                    Vector3 segCenter = netManager.m_segments.m_buffer[segmentId].m_bounds.center;

                    // randomize target position to allow for opposite road-side parking
                    ParkingConfig parkingAiConf = GlobalConfig.Instance.Parking;
                    segCenter.x +=
                        Singleton<SimulationManager>.instance.m_randomizer.Int32(
                            parkingAiConf.ParkingSpacePositionRand) -
                        (parkingAiConf.ParkingSpacePositionRand / 2u);

                    segCenter.z +=
                        Singleton<SimulationManager>.instance.m_randomizer.Int32(
                            parkingAiConf.ParkingSpacePositionRand) -
                        (parkingAiConf.ParkingSpacePositionRand / 2u);

                    if (netManager.m_segments.m_buffer[segmentId].GetClosestLanePosition(
                        segCenter,
                        NetInfo.LaneType.Parking,
                        VehicleInfo.VehicleType.Car,
                        out Vector3 innerParkPos,
                        out uint laneId,
                        out int laneIndex,
                        out _))
                    {
                        NetInfo.Lane laneInfo = segmentInfo.m_lanes[laneIndex];
                        if (!Options.parkingRestrictionsEnabled ||
                            ParkingRestrictionsManager.Instance.IsParkingAllowed(
                                segmentId,
                                laneInfo.m_finalDirection))
                        {
                            if (!Options.vehicleRestrictionsEnabled ||
                                (VehicleRestrictionsManager.Instance.GetAllowedVehicleTypes(
                                     segmentId,
                                     segmentInfo,
                                     (uint)laneIndex,
                                     laneInfo,
                                     VehicleRestrictionsMode.Configured)
                                 & ExtVehicleType.PassengerCar) != ExtVehicleType.None)
                            {
                                if (CustomPassengerCarAI.FindParkingSpaceRoadSide(
                                    ignoreParked,
                                    segmentId,
                                    innerParkPos,
                                    width,
                                    length,
                                    out innerParkPos,
                                    out Quaternion innerParkRot,
                                    out float innerParkOffset))
                                {
                                    Log._TraceIf(
                                        logParkingAi,
                                        () => "FindParkingSpaceRoadSide: Found a parking space for " +
                                        $"refPos {refPos}, segment center {segCenter} " +
                                        $"@ {innerParkPos}, laneId {laneId}, laneIndex {laneIndex}!");

                                    foundSegmentId = segmentId;
                                    myParkPos = innerParkPos;
                                    myParkRot = innerParkRot;
                                    myParkOffset = innerParkOffset;
                                    if (!randomize || rng.Int32(parkingAiConf
                                                .VicinityParkingSpaceSelectionRand) != 0) {
                                        return false;
                                    }
                                } // if find parking roadside
                            } // if allowed vehicle types
                        } // if parking allowed
                    } // if closest lane position

                    segmentId = netManager.m_segments.m_buffer[segmentId].m_nextGridSegment;

                    if (++iterations >= NetManager.MAX_SEGMENT_COUNT) {
                        CODebugBase<LogChannel>.Error(
                            LogChannel.Core,
                            $"Invalid list detected!\n{Environment.StackTrace}");
                        break;
                    }
                } // while segmentid

                return true;
            }

            var coords = _spiral.GetCoords(radius);
            for (int i = 0; i < radius * radius; i++) {
                if (!LoopHandler((int)(centerI + coords[i].x), (int)(centerJ + coords[i].y))) {
                    break;
                }
            }

            if (foundSegmentId == 0) {
                Log._TraceIf(
                    logParkingAi,
                    () => $"FindParkingSpaceRoadSide: Could not find a parking space for refPos {refPos}!");
                return 0;
            }

            parkPos = myParkPos;
            parkRot = myParkRot;
            parkOffset = myParkOffset;

            return foundSegmentId;
        }

        /// <inheritdoc />
        protected ushort FindParkingSpaceBuilding(VehicleInfo vehicleInfo,
                                                  ushort homeID,
                                                  ushort ignoreParked,
                                                  ushort segmentId,
                                                  Vector3 refPos,
                                                  float maxBuildingDistance,
                                                  float maxParkingSpaceDistance,
                                                  bool randomize,
                                                  out Vector3 parkPos,
                                                  out Quaternion parkRot,
                                                  out float parkOffset) {
#if DEBUG
            bool logParkingAi = DebugSwitch.VehicleParkingAILog.Get();
#else
            const bool logParkingAi = false;
#endif

            parkPos = Vector3.zero;
            parkRot = Quaternion.identity;
            parkOffset = -1f;

            var centerI = (int)((refPos.z / BuildingManager.BUILDINGGRID_CELL_SIZE) +
                                (BuildingManager.BUILDINGGRID_RESOLUTION / 2f));
            var centerJ = (int)((refPos.x / BuildingManager.BUILDINGGRID_CELL_SIZE) +
                                 BuildingManager.BUILDINGGRID_RESOLUTION / 2f);
            int radius = Math.Max(
                1,
                (int)(maxBuildingDistance / (BuildingManager.BUILDINGGRID_CELL_SIZE / 2f)) + 1);

            Randomizer rng = Singleton<SimulationManager>.instance.m_randomizer;

            ushort foundBuildingId = 0;
            Vector3 myParkPos = parkPos;
            Quaternion myParkRot = parkRot;
            float myParkOffset = parkOffset;

            // Local function used below in SpiralLoop
            bool LoopHandler(int i, int j) {
                if (i < 0 || i >= BuildingManager.BUILDINGGRID_RESOLUTION || j < 0 ||
                    j >= BuildingManager.BUILDINGGRID_RESOLUTION) {
                    return true;
                }

                ushort buildingId = Singleton<BuildingManager>.instance.m_buildingGrid[
                    (i * BuildingManager.BUILDINGGRID_RESOLUTION) + j];
                var numIterations = 0;
                Building[] buildingsBuffer = Singleton<BuildingManager>.instance.m_buildings.m_buffer;
                ParkingConfig parkingAiConf = GlobalConfig.Instance.Parking;

                while (buildingId != 0) {
                    if (FindParkingSpacePropAtBuilding(
                        vehicleInfo,
                        homeID,
                        ignoreParked,
                        buildingId,
                        ref buildingsBuffer[buildingId],
                        segmentId,
                        refPos,
                        ref maxParkingSpaceDistance,
                        randomize,
                        out Vector3 innerParkPos,
                        out Quaternion innerParkRot,
                        out float innerParkOffset))
                    {
                        foundBuildingId = buildingId;
                        myParkPos = innerParkPos;
                        myParkRot = innerParkRot;
                        myParkOffset = innerParkOffset;

                        if (!randomize
                            || rng.Int32(parkingAiConf.VicinityParkingSpaceSelectionRand) != 0)
                        {
                            return false;
                        }
                    } // if find parking prop at building

                    buildingId = buildingsBuffer[buildingId].m_nextGridBuilding;
                    if (++numIterations >= 49152) {
                        CODebugBase<LogChannel>.Error(
                            LogChannel.Core,
                            $"Invalid list detected!\n{Environment.StackTrace}");
                        break;
                    }
                } // while building id

                return true;
            }

            var coords = _spiral.GetCoords(radius);
            for (int i = 0; i < radius * radius; i++) {
                if (!LoopHandler((int)(centerI + coords[i].x), (int)(centerJ + coords[i].y))) {
                    break;
                }
            }

            if (foundBuildingId == 0) {
                Log._TraceIf(
                    logParkingAi && homeID != 0,
                    () => $"FindParkingSpaceBuilding: Could not find a parking space for homeID {homeID}!");
                return 0;
            }

            parkPos = myParkPos;
            parkRot = myParkRot;
            parkOffset = myParkOffset;

            return foundBuildingId;
        }

        /// <inheritdoc />
        public bool FindParkingSpacePropAtBuilding(VehicleInfo vehicleInfo,
                                                   ushort homeId,
                                                   ushort ignoreParked,
                                                   ushort buildingId,
                                                   ref Building building,
                                                   ushort segmentId,
                                                   Vector3 refPos,
                                                   ref float maxDistance,
                                                   bool randomize,
                                                   out Vector3 parkPos,
                                                   out Quaternion parkRot,
                                                   out float parkOffset) {
#if DEBUG
            bool logParkingAi = DebugSwitch.VehicleParkingAILog.Get();
#else
            const bool logParkingAi = false;
#endif
            // int buildingWidth = building.Width;
            int buildingLength = building.Length;

            // NON-STOCK CODE START
            parkOffset = -1f; // only set if segmentId != 0
            parkPos = default;
            parkRot = default;

            if ((building.m_flags & Building.Flags.Created) == Building.Flags.None) {
                Log._TraceIf(
                    logParkingAi,
                    () => $"Refusing to find parking space at building {buildingId}! Building is not created.");
                return false;
            }

            if ((building.m_problems & Notification.Problem.TurnedOff) != Notification.Problem.None) {
                Log._TraceIf(
                    logParkingAi,
                    () => $"Refusing to find parking space at building {buildingId}! Building is not active.");
                return false;
            }

            if ((building.m_flags & Building.Flags.Collapsed) != Building.Flags.None) {
                Log._TraceIf(
                    logParkingAi,
                    () => $"Refusing to find parking space at building {buildingId}! Building is collapsed.");
                return false;
            }

            Randomizer rng = Singleton<SimulationManager>.instance.m_randomizer; // NON-STOCK CODE

            bool isElectric = vehicleInfo.m_class.m_subService != ItemClass.SubService.ResidentialLow;
            BuildingInfo buildingInfo = building.Info;
            Matrix4x4 transformMatrix = default;
            var transformMatrixCalculated = false;
            var result = false;

            if (buildingInfo.m_class.m_service == ItemClass.Service.Residential &&
                buildingId != homeId && rng.Int32((uint)Options.getRecklessDriverModulo()) != 0) {
                // NON-STOCK CODE
                return false;
            }

            var propMinDistance = 9999f; // NON-STOCK CODE

            if (buildingInfo.m_props != null &&
                (buildingInfo.m_hasParkingSpaces & VehicleInfo.VehicleType.Car) !=
                VehicleInfo.VehicleType.None)
            {
                foreach (BuildingInfo.Prop prop in buildingInfo.m_props) {
                    var randomizer = new Randomizer(buildingId << 6 | prop.m_index);
                    if (randomizer.Int32(100u) >= prop.m_probability ||
                        buildingLength < prop.m_requiredLength) {
                        continue;
                    }

                    PropInfo propInfo = prop.m_finalProp;
                    if (propInfo == null) {
                        continue;
                    }

                    propInfo = propInfo.GetVariation(ref randomizer);
                    if (propInfo.m_parkingSpaces == null || propInfo.m_parkingSpaces.Length == 0) {
                        continue;
                    }

                    if (!transformMatrixCalculated) {
                        transformMatrixCalculated = true;
                        Vector3 pos = Building.CalculateMeshPosition(
                            buildingInfo,
                            building.m_position,
                            building.m_angle,
                            building.Length);
                        Quaternion q = Quaternion.AngleAxis(
                            building.m_angle * Mathf.Rad2Deg,
                            Vector3.down);
                        transformMatrix.SetTRS(pos, q, Vector3.one);
                    }

                    Vector3 position = transformMatrix.MultiplyPoint(prop.m_position);
                    if (CustomPassengerCarAI.FindParkingSpaceProp(
                        isElectric,
                        ignoreParked,
                        propInfo,
                        position,
                        building.m_angle + prop.m_radAngle,
                        prop.m_fixedHeight,
                        refPos,
                        vehicleInfo.m_generatedInfo.m_size.x,
                        vehicleInfo.m_generatedInfo.m_size.z,
                        ref propMinDistance,
                        ref parkPos,
                        ref parkRot))
                    {
                        // NON-STOCK CODE
                        result = true;
                        if (randomize
                            && propMinDistance <= maxDistance
                            && rng.Int32(GlobalConfig.Instance.Parking.VicinityParkingSpaceSelectionRand) == 0)
                        {
                            break;
                        }
                    }
                }
            }

            if (result && propMinDistance <= maxDistance) {
                maxDistance = propMinDistance; // NON-STOCK CODE
                if (logParkingAi) {
                    Log._Trace(
                        $"Found parking space prop in range ({maxDistance}) at building {buildingId}.");
                }

                if (segmentId == 0) {
                    return true;
                }

                // check if building is accessible from the given segment
                Log._TraceIf(
                    logParkingAi,
                    () => $"Calculating unspawn position of building {buildingId} for segment {segmentId}.");

                building.Info.m_buildingAI.CalculateUnspawnPosition(
                    buildingId,
                    ref building,
                    ref Singleton<SimulationManager>.instance.m_randomizer,
                    vehicleInfo,
                    out Vector3 unspawnPos,
                    out _);

                // calculate segment offset
                if (Singleton<NetManager>.instance.m_segments.m_buffer[segmentId].GetClosestLanePosition(
                        unspawnPos,
                        NetInfo.LaneType.Pedestrian,
                        VehicleInfo.VehicleType.None,
                        out Vector3 lanePos,
                        out uint laneId,
                        out int laneIndex,
                        out float laneOffset))
                {
                    Log._TraceIf(
                        logParkingAi,
                        () => "Succeeded in finding unspawn position lane offset for building " +
                        $"{buildingId}, segment {segmentId}, unspawnPos={unspawnPos}! " +
                        $"lanePos={lanePos}, dist={(lanePos - unspawnPos).magnitude}, " +
                        $"laneId={laneId}, laneIndex={laneIndex}, laneOffset={laneOffset}");

                        // if (dist > 16f) {
                        //    if (debug)
                        //        Log._Debug(
                        //            $"Distance between unspawn position and lane position is too big! {dist}
                        //             unspawnPos={unspawnPos} lanePos={lanePos}");
                        //    return false;
                        // }

                    parkOffset = laneOffset;
                } else {
                    Log._TraceIf(
                        logParkingAi,
                        () => $"Could not find unspawn position lane offset for building {buildingId}, " +
                        $"segment {segmentId}, unspawnPos={unspawnPos}!");
                }

                return true;
            }

            if (result && logParkingAi) {
                Log._Trace(
                    $"Could not find parking space prop in range ({maxDistance}) " +
                    $"at building {buildingId}.");
            }

            return false;
        }

        /// <inheritdoc />
        public bool FindParkingSpaceRoadSideForVehiclePos(VehicleInfo vehicleInfo,
                                                          ushort ignoreParked,
                                                          ushort segmentId,
                                                          Vector3 refPos,
                                                          out Vector3 parkPos,
                                                          out Quaternion parkRot,
                                                          out float parkOffset,
                                                          out uint laneId,
                                                          out int laneIndex) {
#if DEBUG
            bool logParkingAi = DebugSwitch.VehicleParkingAILog.Get();
#else
            const bool logParkingAi = false;
#endif
            float width = vehicleInfo.m_generatedInfo.m_size.x;
            float length = vehicleInfo.m_generatedInfo.m_size.z;

            NetManager netManager = Singleton<NetManager>.instance;
            if ((netManager.m_segments.m_buffer[segmentId].m_flags & NetSegment.Flags.Created)
                != NetSegment.Flags.None)
            {
                if (netManager.m_segments.m_buffer[segmentId].GetClosestLanePosition(
                    refPos,
                    NetInfo.LaneType.Parking,
                    VehicleInfo.VehicleType.Car,
                    out parkPos,
                    out laneId,
                    out laneIndex,
                    out parkOffset))
                {
                    if (!Options.parkingRestrictionsEnabled ||
                        ParkingRestrictionsManager.Instance.IsParkingAllowed(
                            segmentId,
                            netManager.m_segments.m_buffer[segmentId].Info
                                      .m_lanes[laneIndex].m_finalDirection))
                    {
                        if (CustomPassengerCarAI.FindParkingSpaceRoadSide(
                            ignoreParked,
                            segmentId,
                            parkPos,
                            width,
                            length,
                            out parkPos,
                            out parkRot,
                            out parkOffset)) {
                            if (logParkingAi) {
                                Log._Trace(
                                    "FindParkingSpaceRoadSideForVehiclePos: Found a parking space " +
                                    $"for refPos {refPos} @ {parkPos}, laneId {laneId}, " +
                                    $"laneIndex {laneIndex}!");
                            }

                            return true;
                        }
                    }
                }
            }

            parkPos = default;
            parkRot = default;
            laneId = 0;
            laneIndex = -1;
            parkOffset = -1f;
            return false;
        }

        /// <inheritdoc />
        public bool FindParkingSpaceRoadSide(ushort ignoreParked,
                                             Vector3 refPos,
                                             float width,
                                             float length,
                                             float maxDistance,
                                             out Vector3 parkPos,
                                             out Quaternion parkRot,
                                             out float parkOffset) {
            return FindParkingSpaceAtRoadSide(
                       ignoreParked,
                       refPos,
                       width,
                       length,
                       maxDistance,
                       false,
                       out parkPos,
                       out parkRot,
                       out parkOffset) != 0;
        }

        /// <inheritdoc />
        public bool FindParkingSpaceBuilding(VehicleInfo vehicleInfo,
                                             ushort homeId,
                                             ushort ignoreParked,
                                             ushort segmentId,
                                             Vector3 refPos,
                                             float maxBuildingDistance,
                                             float maxParkingSpaceDistance,
                                             out Vector3 parkPos,
                                             out Quaternion parkRot,
                                             out float parkOffset) {
            return FindParkingSpaceBuilding(
                       vehicleInfo,
                       homeId,
                       ignoreParked,
                       segmentId,
                       refPos,
                       maxBuildingDistance,
                       maxParkingSpaceDistance,
                       false,
                       out parkPos,
                       out parkRot,
                       out parkOffset) != 0;
        }
    }
}
