namespace TrafficManager.API.Manager {
    using TrafficManager.API.Traffic.Data;
    using TrafficManager.API.Traffic.Enums;
    using UnityEngine;

    public interface IParkingSpaceManager {

        /// <summary>
        /// Finds a free parking space in the vicinity of the given target position <paramref name="endPos"/>
        /// for the given citizen instance <paramref name="extDriverInstance"/>.
        /// </summary>
        /// <param name="endPos">target position</param>
        /// <param name="vehicleInfo">vehicle type that is being used</param>
        /// <param name="extDriverInstance">cititzen instance that is driving the car</param>
        /// <param name="homeId">Home building of the citizen (may be 0 for tourists/homeless cims)</param>
        /// <param name="goingHome">Specifies if the citizen is going home</param>
        /// <param name="vehicleId">Vehicle that is being used (used for logging)</param>
        /// <param name="allowTourists">If true, method fails if given citizen is a tourist (TODO remove this parameter)</param>
        /// <param name="parkPos">parking position (output)</param>
        /// <param name="endPathPos">sidewalk path position near parking space (output). only valid if <paramref name="calculateEndPos"/> yields false.</param>
        /// <param name="calculateEndPos">if false, a parking space path position could be calculated (TODO negate & rename parameter)</param>
        /// <returns>true if a parking space could be found, false otherwise</returns>
        bool FindParkingSpaceForCitizen(Vector3 endPos,
                                        VehicleInfo vehicleInfo,
                                        ref ExtCitizenInstance extDriverInstance,
                                        ushort homeId,
                                        bool goingHome,
                                        ushort vehicleId,
                                        bool allowTourists,
                                        out Vector3 parkPos,
                                        ref PathUnit.Position endPathPos,
                                        out bool calculateEndPos);

        /// <summary>
        /// Tries to find a parking space in the broaded vicinity of the given position <paramref name="targetPos"/>.
        /// </summary>
        /// <param name="targetPos">Target position that is used as a center point for the search procedure</param>
        /// <param name="searchDir">Search direction</param>
        /// <param name="vehicleInfo">Vehicle that shall be parked (used for gathering vehicle geometry information)</param>
        /// <param name="homeId">Home building id of the citizen (citizens are not allowed to park their car on foreign residential premises)</param>
        /// <param name="vehicleId">Vehicle that shall be parked</param>
        /// <param name="maxDist">maximum allowed distance between target position and parking space location</param>
        /// <param name="parkingSpaceLocation">identified parking space location type (only valid if method returns true)</param>
        /// <param name="parkingSpaceLocationId">identified parking space location identifier (only valid if method returns true)</param>
        /// <param name="parkPos">identified parking space position (only valid if method returns true)</param>
        /// <param name="parkRot">identified parking space rotation (only valid if method returns true)</param>
        /// <param name="parkOffset">identified parking space offset (only valid if method returns true)</param>
        /// <returns>true if a parking space could be found, false otherwise</returns>
        bool FindParkingSpaceInVicinity(Vector3 targetPos,
                                        Vector3 searchDir,
                                        VehicleInfo vehicleInfo,
                                        ushort homeId,
                                        ushort vehicleId,
                                        float maxDist,
                                        out ExtParkingSpaceLocation parkingSpaceLocation,
                                        out ushort parkingSpaceLocationId,
                                        out Vector3 parkPos,
                                        out Quaternion parkRot,
                                        out float parkOffset);

        /// <summary>
        /// Tries to find a parking space for a moving vehicle at a given segment. The search
        /// is restricted to the given segment.
        /// </summary>
        /// <param name="vehicleInfo">vehicle that shall be parked (used for gathering vehicle geometry information)</param>
        /// <param name="ignoreParked">if true, already parked vehicles are ignored</param>
        /// <param name="segmentId">segment to search on</param>
        /// <param name="refPos">current vehicle position</param>
        /// <param name="parkPos">identified parking space position (only valid if method returns true)</param>
        /// <param name="parkRot">identified parking space rotation (only valid if method returns true)</param>
        /// <param name="parkOffset">identified parking space offset (only valid if method returns true)</param>
        /// <param name="laneId">identified parking space lane id (only valid if method returns true)</param>
        /// <param name="laneIndex">identified parking space lane index (only valid if method returns true)</param>
        /// <returns>true if a parking space could be found, false otherwise</returns>
        bool FindParkingSpaceRoadSideForVehiclePos(VehicleInfo vehicleInfo,
                                                   ushort ignoreParked,
                                                   ushort segmentId,
                                                   Vector3 refPos,
                                                   out Vector3 parkPos,
                                                   out Quaternion parkRot,
                                                   out float parkOffset,
                                                   out uint laneId,
                                                   out int laneIndex);

        /// <summary>
        /// Tries to find a road-side parking space in the vicinity of the given position <paramref name="refPos"/>.
        /// </summary>
        /// <param name="ignoreParked">if true, already parked vehicles are ignored</param>
        /// <param name="refPos">Target position that is used as a center point for the search procedure</param>
        /// <param name="width">vehicle width</param>
        /// <param name="length">vehicle length</param>
        /// <param name="maxDistance">Maximum allowed distance between the target position and the parking space</param>
        /// <param name="parkPos">identified parking space position (only valid if method returns true)</param>
        /// <param name="parkRot">identified parking space rotation (only valid if method returns true)</param>
        /// <param name="parkOffset">identified parking space offset (only valid if method returns true)</param>
        /// <returns>true if a parking space could be found, false otherwise</returns>
        bool FindParkingSpaceRoadSide(ushort ignoreParked,
                                      Vector3 refPos,
                                      float width,
                                      float length,
                                      float maxDistance,
                                      out Vector3 parkPos,
                                      out Quaternion parkRot,
                                      out float parkOffset);

        /// <summary>
        /// Tries to find a parking space at a building in the vicinity of the given position
        /// <paramref name="targetPos"/>.
        /// </summary>
        /// <param name="vehicleInfo">vehicle that shall be parked (used for gathering vehicle
        ///     geometry information)</param>
        /// <param name="homeID">Home building id of the citizen (citizens are not allowed to park
        ///     their car on foreign residential premises)</param>
        /// <param name="ignoreParked">if true, already parked vehicles are ignored</param>
        /// <param name="segmentId">if != 0, the building is forced to be "accessible" from this
        ///     segment (where accessible means "close enough")</param>
        /// <param name="refPos">Target position that is used as a center point for the search
        ///     procedure</param>
        /// <param name="maxBuildingDistance">Maximum allowed distance between the target position
        ///     and the parking building</param>
        /// <param name="maxParkingSpaceDistance">Maximum allowed distance between the target
        ///     position and the parking space</param>
        /// <param name="parkPos">identified parking space position (only valid if method returns
        ///     true)</param>
        /// <param name="parkRot">identified parking space rotation (only valid if method returns
        ///     true)</param>
        /// <param name="parkOffset">identified parking space offset (only valid if method returns
        ///     true and a segment id was given)</param>
        /// <returns>true if a parking space could be found, false otherwise</returns>
        bool FindParkingSpaceBuilding(VehicleInfo vehicleInfo,
                                      ushort homeID,
                                      ushort ignoreParked,
                                      ushort segmentId,
                                      Vector3 refPos,
                                      float maxBuildingDistance,
                                      float maxParkingSpaceDistance,
                                      out Vector3 parkPos,
                                      out Quaternion parkRot,
                                      out float parkOffset);

        /// <summary>
        /// Tries to find a parking space prop that belongs to the given building <paramref name="buildingID"/>.
        /// </summary>
        /// <param name="vehicleInfo">vehicle that shall be parked (used for gathering vehicle geometry information)</param>
        /// <param name="homeID">Home building id of the citizen (citizens are not allowed to park their car on foreign residential premises)</param>
        /// <param name="ignoreParked">if true, already parked vehicles are ignored</param>
        /// <param name="buildingID">Building that is queried</param>
        /// <param name="building">Building data</param>
        /// <param name="segmentId">if != 0, the building is forced to be "accessible" from this segment (where accessible means "close enough")</param>
        /// <param name="refPos">Target position that is used as a center point for the search procedure</param>
        /// <param name="maxDistance">Maximum allowed distance between the target position and the parking space</param>
        /// <param name="randomize">If true, search is randomized such that not always only the closest parking space is selected.</param>
        /// <param name="parkPos">identified parking space position (only valid if method returns true)</param>
        /// <param name="parkRot">identified parking space rotation (only valid if method returns true)</param>
        /// <param name="parkOffset">identified parking space offset (only valid if method returns true and a segment id was given)</param>
        /// <returns>true if a parking space could be found, false otherwise</returns>
        bool FindParkingSpacePropAtBuilding(VehicleInfo vehicleInfo,
                                            ushort homeID,
                                            ushort ignoreParked,
                                            ushort buildingID,
                                            ref Building building,
                                            ushort segmentId,
                                            Vector3 refPos,
                                            ref float maxDistance,
                                            bool randomize,
                                            out Vector3 parkPos,
                                            out Quaternion parkRot,
                                            out float parkOffset);
    }
}