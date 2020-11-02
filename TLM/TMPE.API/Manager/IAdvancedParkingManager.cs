namespace TrafficManager.API.Manager {
    using TrafficManager.API.Traffic.Data;
    using TrafficManager.API.Traffic.Enums;
    using UnityEngine;

    public interface IAdvancedParkingManager : IFeatureManager {
        /// <summary>
        /// Determines the color the given building should be colorized with given the current info view mode.
        /// While the traffic view is active buildings with high parking space demand are colored red and
        /// buildings with low demand are colored green.
        /// </summary>
        /// <param name="buildingId">building id</param>
        /// <param name="buildingData">building data</param>
        /// <param name="infoMode">current info view mode</param>
        /// <param name="color">output color</param>
        /// <returns>true if a custom color should be displayed, false otherwise</returns>
        bool GetBuildingInfoViewColor(ushort buildingId,
                                      ref Building buildingData,
                                      ref ExtBuilding extBuilding,
                                      InfoManager.InfoMode infoMode,
                                      out Color? color);

        /// <summary>
        /// Adds Parking AI related information to the given citizen status text.
        /// </summary>
        /// <param name="ret">status text to enrich</param>
        /// <param name="extInstance">extended citizen instance data</param>
        /// <param name="extCitizen">extended citizen data</param>
        /// <returns></returns>
        string EnrichLocalizedCitizenStatus(string ret,
                                            ref ExtCitizenInstance extInstance,
                                            ref ExtCitizen extCitizen);

        /// <summary>
        /// Adds Parking AI related information to the given passenger car status text.
        /// </summary>
        /// <param name="ret">status text to enrich</param>
        /// <param name="driverExtInstance">extended citizen instance data</param>
        /// <returns></returns>
        string EnrichLocalizedCarStatus(string ret, ref ExtCitizenInstance driverExtInstance);

        /// <summary>
        /// Makes the given citizen instance enter their parked car.
        /// </summary>
        /// <param name="instanceID">Citizen instance id</param>
        /// <param name="instanceData">Citizen instance data</param>
        /// <param name="parkedVehicleId">Parked vehicle id</param>
        /// <param name="vehicleId">Vehicle id</param>
        /// <returns>true if entering the car succeeded, false otherwise</returns>
        bool EnterParkedCar(ushort instanceID,
                            ref CitizenInstance instanceData,
                            ushort parkedVehicleId,
                            out ushort vehicleId);

        /// <summary>
        /// Merges the current calculation states of the citizen's main path and return path (while walking).
        /// If a definite calculation state can be determined path-find failure/success is handled appropriately.
        /// The returned (soft) path state indicates if further handling must be undertaken by the game.
        /// </summary>
        /// <param name="citizenInstanceId">citizen instance that shall be processed</param>
        /// <param name="citizenInstance">citizen instance data</param>
        /// <param name="extInstance">extended citizen instance data</param>
        /// <param name="extCitizen">extended citizen data</param>
        /// <param name="citizen">citizen data</param>
        /// <param name="mainPathState">current state of the citizen instance's main path</param>
        /// <returns>
        ///		Indication of how (external) game logic should treat this situation:
        ///		<code>Calculating</code>: Paths are still being calculated. Game must await completion.
        ///		<code>Ready</code>: All paths are ready and path-find success must be handled.
        ///		<code>FailedHard</code>: At least one path calculation failed and the failure must be handled.
        ///		<code>FailedSoft</code>: Path-finding must be repeated.
        ///		<code>Ignore</code>: Default citizen behavior must be skipped.
        ///	</returns>
        ExtSoftPathState UpdateCitizenPathState(ushort citizenInstanceId,
                                                ref CitizenInstance citizenInstance,
                                                ref ExtCitizenInstance extInstance,
                                                ref ExtCitizen extCitizen,
                                                ref Citizen citizen,
                                                ExtPathState mainPathState);

        /// <summary>
        /// Merges the current calculation states of the citizen's main path and return path (while driving a passenger car).
        /// If a definite calculation state can be determined path-find failure/success is handled appropriately.
        /// The returned (soft) path state indicates if further handling must be undertaken by the game.
        /// </summary>
        /// <param name="vehicleId">vehicle that shall be processed</param>
        /// <param name="vehicleData">vehicle data</param>
        /// <param name="driverInstance">driver citizen instance</param>
        /// <param name="driverExtInstance">extended citizen instance data of the driving citizen</param>
        /// <param name="mainPathState">current state of the citizen instance's main path</param>
        /// <returns>
        ///		Indication of how (external) game logic should treat this situation:
        ///		<code>Calculating</code>: Paths are still being calculated. Game must await completion.
        ///		<code>Ready</code>: All paths are ready and path-find success must be handled.
        ///		<code>FailedHard</code>: At least one path calculation failed and the failure must be handled.
        ///		<code>FailedSoft</code>: Path-finding must be repeated.
        ///		<code>Ignore</code>: Default citizen behavior must be skipped.
        /// </returns>
        ExtSoftPathState UpdateCarPathState(ushort vehicleId,
                                            ref Vehicle vehicleData,
                                            ref CitizenInstance driverInstance,
                                            ref ExtCitizenInstance driverExtInstance,
                                            ExtPathState mainPathState);

        /// <summary>
        /// Processes a citizen that is approaching their private car.
        /// Internal state information is updated appropriately. The returned approach
        /// state indicates if the approach is finished.
        /// </summary>
        /// <param name="instanceId">citizen instance that shall be processed</param>
        /// <param name="instanceData">citizen instance data</param>
        /// <param name="extInstance">extended citizen instance data</param>
        /// <param name="physicsLodRefPos">simulation accuracy</param>
        /// <param name="parkedCar">parked car data</param>
        /// <returns>
        ///		Approach state indication:
        ///		<code>Approaching</code>: The citizen is currently approaching the parked car.
        ///		<code>Approached</code>: The citizen has approached the car and is ready to enter it.
        ///		<code>Failure</code>: The approach procedure failed (currently not returned).
        /// </returns>
        ParkedCarApproachState CitizenApproachingParkedCarSimulationStep(
            ushort instanceId,
            ref CitizenInstance instanceData,
            ref ExtCitizenInstance extInstance,
            Vector3 physicsLodRefPos,
            ref VehicleParked parkedCar);

        /// <summary>
        /// Processes a citizen that is approaching their target building.
        /// Internal state information is updated appropriately. The returned flag
        /// indicates if the approach is finished.
        /// </summary>
        /// <param name="instanceId">citizen instance that shall be processed</param>
        /// <param name="instanceData">citizen instance data</param>
        /// <param name="extInstance">extended citizen instance</param>
        /// <returns>true if the citizen arrived at the target, false otherwise</returns>
        bool CitizenApproachingTargetSimulationStep(ushort instanceId,
                                                    ref CitizenInstance instanceData,
                                                    ref ExtCitizenInstance extInstance);

        /// <summary>
        /// Tries to relocate the given parked car (<paramref name="parkedVehicleId"/>, <paramref name="parkedVehicle"/>)
        /// within the vicinity of the given reference position <paramref name="refPos"/>.
        /// </summary>
        /// <param name="parkedVehicleId">parked vehicle id</param>
        /// <param name="parkedVehicle">parked vehicle data</param>
        /// <param name="refPos">reference position</param>
        /// <param name="maxDistance">maximum allowed distance between reference position and parking space location</param>
        /// <param name="homeId">Home building id of the citizen (For residential buildings, parked cars may only spawn at the home building)</param>
        /// <returns><code>true</code> if the parked vehicle was relocated, <code>false</code> otherwise</returns>
        bool TryMoveParkedVehicle(ushort parkedVehicleId,
                                  ref VehicleParked parkedVehicle,
                                  Vector3 refPos,
                                  float maxDistance,
                                  ushort homeId);

        /// <summary>
        /// Tries to spawn a parked passenger car for the given citizen <paramref name="citizenId"/>
        /// in the vicinity of the given position <paramref name="refPos"/>.
        /// </summary>
        /// <param name="citizenId">Citizen that requires a parked car</param>
        /// <param name="homeId">Home building id of the citizen (For residential buildings, parked cars may only spawn at the home building)</param>
        /// <param name="refPos">Reference position</param>
        /// <param name="vehicleInfo">Vehicle type to spawn</param>
        /// <param name="parkPos">Parked vehicle position (output)</param>
        /// <param name="reason">Indicates the reason why no car could be spawned when the method returns false</param>
        /// <returns>true if a passenger car could be spawned, false otherwise</returns>
        bool TrySpawnParkedPassengerCar(uint citizenId,
                                        ushort homeId,
                                        Vector3 refPos,
                                        VehicleInfo vehicleInfo,
                                        out Vector3 parkPos,
                                        out ParkingError reason);

        /// <summary>
        /// Tries to spawn a parked passenger car for the given citizen <paramref name="citizenId"/>
        /// at a road segment in the vicinity of the given position <paramref name="refPos"/>.
        /// </summary>
        /// <param name="citizenId">Citizen that requires a parked car</param>
        /// <param name="refPos">Reference position</param>
        /// <param name="vehicleInfo">Vehicle type to spawn</param>
        /// <param name="parkPos">Parked vehicle position (output)</param>
        /// <param name="reason">Indicates the reason why no car could be spawned when the method returns false</param>
        /// <returns>true if a passenger car could be spawned, false otherwise</returns>
        bool TrySpawnParkedPassengerCarRoadSide(uint citizenId,
                                                Vector3 refPos,
                                                VehicleInfo vehicleInfo,
                                                out Vector3 parkPos,
                                                out ParkingError reason);

        /// <summary>
        /// Tries to spawn a parked passenger car for the given citizen <paramref name="citizenId"/>
        /// at a building in the vicinity of the given position <paramref name="refPos"/>.
        /// </summary>
        /// <param name="citizenId">Citizen that requires a parked car</param>
        /// <param name="homeId">Home building id of the citizen (For residential buildings, parked cars may only spawn at the home building)</param>
        /// <param name="refPos">Reference position</param>
        /// <param name="vehicleInfo">Vehicle type to spawn</param>
        /// <param name="parkPos">Parked vehicle position (output)</param>
        /// <param name="reason">Indicates the reason why no car could be spawned when the method returns false</param>
        /// <returns>true if a passenger car could be spawned, false otherwise</returns>
        bool TrySpawnParkedPassengerCarBuilding(uint citizenId,
                                                ushort homeId,
                                                Vector3 refPos,
                                                VehicleInfo vehicleInfo,
                                                out Vector3 parkPos,
                                                out ParkingError reason);
    }
}