// VehicleManager
using System;
using System.Collections;
using System.Collections.Generic;
using ColossalFramework;
using ColossalFramework.Globalization;
using ColossalFramework.IO;
using ColossalFramework.Math;
using UnityEngine;

public class VehicleManager : SimulationManagerBase<VehicleManager, VehicleProperties>, ISimulationManager, IRenderableManager, IAudibleManager {
    public class VehicleTypeComparer : IComparer<ushort> {
        public static VehicleTypeComparer comparer = new VehicleTypeComparer();

        int IComparer<ushort>.Compare(ushort prefabIndex1, ushort prefabIndex2) {
            VehicleInfo prefab = PrefabCollection<VehicleInfo>.GetPrefab(prefabIndex1);
            VehicleInfo prefab2 = PrefabCollection<VehicleInfo>.GetPrefab(prefabIndex2);
            return prefab.m_vehicleType - prefab2.m_vehicleType;
        }
    }

    public class Data : IDataContainer {
        public void Serialize(DataSerializer s) {
            Singleton<LoadingManager>.instance.m_loadingProfilerSimulation.BeginSerialize(s, "VehicleManager");
            VehicleManager instance = Singleton<VehicleManager>.instance;
            Vehicle[] buffer = instance.m_vehicles.m_buffer;
            VehicleParked[] buffer2 = instance.m_parkedVehicles.m_buffer;
            int num = buffer.Length;
            int num2 = buffer2.Length;
            EncodedArray.UInt uInt = EncodedArray.UInt.BeginWrite(s);
            for (int i = 1; i < num; i++) {
                uInt.Write((uint)buffer[i].m_flags);
            }
            for (int j = 1; j < num; j++) {
                uInt.Write((uint)buffer[j].m_flags2);
            }
            uInt.EndWrite();
            EncodedArray.UShort uShort = EncodedArray.UShort.BeginWrite(s);
            for (int k = 1; k < num2; k++) {
                uShort.Write(buffer2[k].m_flags);
            }
            uShort.EndWrite();
            try {
                PrefabCollection<VehicleInfo>.BeginSerialize(s);
                for (int l = 1; l < num; l++) {
                    if (buffer[l].m_flags != 0) {
                        PrefabCollection<VehicleInfo>.Serialize(buffer[l].m_infoIndex);
                    }
                }
                for (int m = 1; m < num2; m++) {
                    if (buffer2[m].m_flags != 0) {
                        PrefabCollection<VehicleInfo>.Serialize(buffer2[m].m_infoIndex);
                    }
                }
            }
            finally {
                PrefabCollection<VehicleInfo>.EndSerialize(s);
            }
            EncodedArray.Byte @byte = EncodedArray.Byte.BeginWrite(s);
            for (int n = 1; n < num; n++) {
                if (buffer[n].m_flags != 0) {
                    @byte.Write(buffer[n].m_gateIndex);
                }
            }
            @byte.EndWrite();
            EncodedArray.UShort uShort2 = EncodedArray.UShort.BeginWrite(s);
            for (int num3 = 1; num3 < num; num3++) {
                if (buffer[num3].m_flags != 0) {
                    uShort2.Write(buffer[num3].m_waterSource);
                }
            }
            uShort2.EndWrite();
            for (int num4 = 1; num4 < num; num4++) {
                if (buffer[num4].m_flags != 0) {
                    Vehicle.Frame lastFrameData = buffer[num4].GetLastFrameData();
                    s.WriteVector3(lastFrameData.m_velocity);
                    s.WriteVector3(lastFrameData.m_position);
                    s.WriteQuaternion(lastFrameData.m_rotation);
                    s.WriteFloat(lastFrameData.m_angleVelocity);
                    s.WriteVector4(buffer[num4].m_targetPos0);
                    s.WriteVector4(buffer[num4].m_targetPos1);
                    s.WriteVector4(buffer[num4].m_targetPos2);
                    s.WriteVector4(buffer[num4].m_targetPos3);
                    s.WriteUInt16(buffer[num4].m_sourceBuilding);
                    s.WriteUInt16(buffer[num4].m_targetBuilding);
                    s.WriteUInt16(buffer[num4].m_transportLine);
                    s.WriteUInt16(buffer[num4].m_transferSize);
                    s.WriteUInt8(buffer[num4].m_transferType);
                    s.WriteUInt8(buffer[num4].m_waitCounter);
                    s.WriteUInt8(buffer[num4].m_blockCounter);
                    s.WriteUInt24(buffer[num4].m_citizenUnits);
                    s.WriteUInt24(buffer[num4].m_path);
                    s.WriteUInt8(buffer[num4].m_pathPositionIndex);
                    s.WriteUInt8(buffer[num4].m_lastPathOffset);
                    s.WriteUInt16(buffer[num4].m_trailingVehicle);
                    s.WriteUInt16(buffer[num4].m_cargoParent);
                    s.WriteUInt16(buffer[num4].m_custom);
                }
            }
            for (int num5 = 1; num5 < num2; num5++) {
                if (buffer2[num5].m_flags != 0) {
                    s.WriteVector3(buffer2[num5].m_position);
                    s.WriteQuaternion(buffer2[num5].m_rotation);
                    s.WriteUInt24(buffer2[num5].m_ownerCitizen);
                }
            }
            s.WriteUInt32(instance.m_totalTrafficFlow);
            s.WriteUInt32(instance.m_maxTrafficFlow);
            s.WriteUInt8(instance.m_lastTrafficFlow);
            Singleton<LoadingManager>.instance.m_loadingProfilerSimulation.EndSerialize(s, "VehicleManager");
        }

        public void Deserialize(DataSerializer s) {
            Singleton<LoadingManager>.instance.m_loadingProfilerSimulation.BeginDeserialize(s, "VehicleManager");
            VehicleManager instance = Singleton<VehicleManager>.instance;
            Vehicle[] buffer = instance.m_vehicles.m_buffer;
            VehicleParked[] buffer2 = instance.m_parkedVehicles.m_buffer;
            ushort[] vehicleGrid = instance.m_vehicleGrid;
            ushort[] vehicleGrid2 = instance.m_vehicleGrid2;
            ushort[] parkedGrid = instance.m_parkedGrid;
            int num = buffer.Length;
            int num2 = buffer2.Length;
            int num3 = vehicleGrid.Length;
            int num4 = vehicleGrid2.Length;
            int num5 = parkedGrid.Length;
            instance.m_vehicles.ClearUnused();
            instance.m_parkedVehicles.ClearUnused();
            for (int i = 0; i < num3; i++) {
                vehicleGrid[i] = 0;
            }
            for (int j = 0; j < num4; j++) {
                vehicleGrid2[j] = 0;
            }
            for (int k = 0; k < num5; k++) {
                parkedGrid[k] = 0;
            }
            for (int l = 0; l < instance.m_updatedParked.Length; l++) {
                instance.m_updatedParked[l] = 0uL;
            }
            instance.m_parkedUpdated = false;
            EncodedArray.UInt uInt = EncodedArray.UInt.BeginRead(s);
            for (int m = 1; m < num; m++) {
                buffer[m].m_flags = (Vehicle.Flags)uInt.Read();
            }
            if (s.version >= 274) {
                for (int n = 1; n < num; n++) {
                    buffer[n].m_flags2 = (Vehicle.Flags2)uInt.Read();
                }
            } else {
                for (int num6 = 1; num6 < num; num6++) {
                    buffer[num6].m_flags2 = (Vehicle.Flags2)0;
                }
            }
            uInt.EndRead();
            if (s.version >= 205) {
                EncodedArray.UShort uShort = EncodedArray.UShort.BeginRead(s);
                for (int num7 = 1; num7 < num2; num7++) {
                    buffer2[num7].m_flags = uShort.Read();
                }
                uShort.EndRead();
            } else if (s.version >= 115) {
                EncodedArray.UShort uShort2 = EncodedArray.UShort.BeginRead(s);
                for (int num8 = 1; num8 < 16384; num8++) {
                    buffer2[num8].m_flags = uShort2.Read();
                }
                for (int num9 = 16384; num9 < num2; num9++) {
                    buffer2[num9].m_flags = 0;
                }
                uShort2.EndRead();
            } else {
                for (int num10 = 1; num10 < num2; num10++) {
                    buffer2[num10].m_flags = 0;
                }
            }
            if (s.version >= 30) {
                PrefabCollection<VehicleInfo>.BeginDeserialize(s);
                for (int num11 = 1; num11 < num; num11++) {
                    if (buffer[num11].m_flags != 0) {
                        buffer[num11].m_infoIndex = (ushort)PrefabCollection<VehicleInfo>.Deserialize(important: true);
                    }
                }
                if (s.version >= 115) {
                    for (int num12 = 1; num12 < num2; num12++) {
                        if (buffer2[num12].m_flags != 0) {
                            buffer2[num12].m_infoIndex = (ushort)PrefabCollection<VehicleInfo>.Deserialize(important: true);
                        }
                    }
                }
                PrefabCollection<VehicleInfo>.EndDeserialize(s);
            }
            if (s.version >= 182) {
                EncodedArray.Byte @byte = EncodedArray.Byte.BeginRead(s);
                for (int num13 = 1; num13 < num; num13++) {
                    if (buffer[num13].m_flags != 0) {
                        buffer[num13].m_gateIndex = @byte.Read();
                    } else {
                        buffer[num13].m_gateIndex = 0;
                    }
                }
                @byte.EndRead();
            } else {
                for (int num14 = 1; num14 < num; num14++) {
                    buffer[num14].m_gateIndex = 0;
                }
            }
            if (s.version >= 273) {
                EncodedArray.UShort uShort3 = EncodedArray.UShort.BeginRead(s);
                for (int num15 = 1; num15 < num; num15++) {
                    if (buffer[num15].m_flags != 0) {
                        buffer[num15].m_waterSource = uShort3.Read();
                    } else {
                        buffer[num15].m_waterSource = 0;
                    }
                }
                uShort3.EndRead();
            } else {
                for (int num16 = 1; num16 < num; num16++) {
                    buffer[num16].m_waterSource = 0;
                }
            }
            for (int num17 = 1; num17 < num; num17++) {
                buffer[num17].m_nextGridVehicle = 0;
                buffer[num17].m_nextGuestVehicle = 0;
                buffer[num17].m_nextOwnVehicle = 0;
                buffer[num17].m_nextLineVehicle = 0;
                buffer[num17].m_leadingVehicle = 0;
                buffer[num17].m_firstCargo = 0;
                buffer[num17].m_nextCargo = 0;
                buffer[num17].m_lastFrame = 0;
                buffer[num17].m_touristCount = 0;
                if (buffer[num17].m_flags != 0) {
                    buffer[num17].m_frame0 = new Vehicle.Frame(Vector3.zero, Quaternion.identity);
                    if (s.version >= 47) {
                        buffer[num17].m_frame0.m_velocity = s.ReadVector3();
                    }
                    buffer[num17].m_frame0.m_position = s.ReadVector3();
                    if (s.version >= 78) {
                        buffer[num17].m_frame0.m_rotation = s.ReadQuaternion();
                    }
                    if (s.version >= 129) {
                        buffer[num17].m_frame0.m_angleVelocity = s.ReadFloat();
                    }
                    buffer[num17].m_frame0.m_underground = (buffer[num17].m_flags & Vehicle.Flags.Underground) != 0;
                    buffer[num17].m_frame0.m_transition = (buffer[num17].m_flags & Vehicle.Flags.Transition) != 0;
                    buffer[num17].m_frame0.m_insideBuilding = (buffer[num17].m_flags & Vehicle.Flags.InsideBuilding) != 0;
                    buffer[num17].m_frame1 = buffer[num17].m_frame0;
                    buffer[num17].m_frame2 = buffer[num17].m_frame0;
                    buffer[num17].m_frame3 = buffer[num17].m_frame0;
                    if (s.version >= 68) {
                        buffer[num17].m_targetPos0 = s.ReadVector4();
                    } else if (s.version >= 47) {
                        buffer[num17].m_targetPos0 = s.ReadVector3();
                        buffer[num17].m_targetPos0.w = 2f;
                    } else {
                        buffer[num17].m_targetPos0 = buffer[num17].m_frame0.m_position;
                        buffer[num17].m_targetPos0.w = 2f;
                    }
                    if (s.version >= 90) {
                        buffer[num17].m_targetPos1 = s.ReadVector4();
                        buffer[num17].m_targetPos2 = s.ReadVector4();
                        buffer[num17].m_targetPos3 = s.ReadVector4();
                    } else {
                        buffer[num17].m_targetPos1 = buffer[num17].m_targetPos0;
                        buffer[num17].m_targetPos2 = buffer[num17].m_targetPos0;
                        buffer[num17].m_targetPos3 = buffer[num17].m_targetPos0;
                    }
                    buffer[num17].m_sourceBuilding = (ushort)s.ReadUInt16();
                    buffer[num17].m_targetBuilding = (ushort)s.ReadUInt16();
                    if (s.version >= 52) {
                        buffer[num17].m_transportLine = (ushort)s.ReadUInt16();
                    } else {
                        buffer[num17].m_transportLine = 0;
                    }
                    buffer[num17].m_transferSize = (ushort)s.ReadUInt16();
                    buffer[num17].m_transferType = (byte)s.ReadUInt8();
                    buffer[num17].m_waitCounter = (byte)s.ReadUInt8();
                    if (s.version >= 99) {
                        buffer[num17].m_blockCounter = (byte)s.ReadUInt8();
                    } else {
                        buffer[num17].m_blockCounter = 0;
                    }
                    if (s.version >= 32) {
                        buffer[num17].m_citizenUnits = s.ReadUInt24();
                    } else {
                        buffer[num17].m_citizenUnits = 0u;
                    }
                    if (s.version >= 47) {
                        buffer[num17].m_path = s.ReadUInt24();
                        buffer[num17].m_pathPositionIndex = (byte)s.ReadUInt8();
                        buffer[num17].m_lastPathOffset = (byte)s.ReadUInt8();
                    } else {
                        buffer[num17].m_path = 0u;
                        buffer[num17].m_pathPositionIndex = 0;
                        buffer[num17].m_lastPathOffset = 0;
                    }
                    if (s.version >= 58) {
                        buffer[num17].m_trailingVehicle = (ushort)s.ReadUInt16();
                    } else {
                        buffer[num17].m_trailingVehicle = 0;
                    }
                    if (s.version >= 104) {
                        buffer[num17].m_cargoParent = (ushort)s.ReadUInt16();
                    } else {
                        buffer[num17].m_cargoParent = 0;
                    }
                    if (s.version >= 113012) {
                        buffer[num17].m_custom = (ushort)s.ReadUInt16();
                    } else {
                        buffer[num17].m_custom = 0;
                    }
                } else {
                    buffer[num17].m_frame0 = new Vehicle.Frame(Vector3.zero, Quaternion.identity);
                    buffer[num17].m_frame1 = new Vehicle.Frame(Vector3.zero, Quaternion.identity);
                    buffer[num17].m_frame2 = new Vehicle.Frame(Vector3.zero, Quaternion.identity);
                    buffer[num17].m_frame3 = new Vehicle.Frame(Vector3.zero, Quaternion.identity);
                    buffer[num17].m_targetPos0 = Vector4.zero;
                    buffer[num17].m_targetPos1 = Vector4.zero;
                    buffer[num17].m_targetPos2 = Vector4.zero;
                    buffer[num17].m_targetPos3 = Vector4.zero;
                    buffer[num17].m_sourceBuilding = 0;
                    buffer[num17].m_targetBuilding = 0;
                    buffer[num17].m_transportLine = 0;
                    buffer[num17].m_transferSize = 0;
                    buffer[num17].m_transferType = 0;
                    buffer[num17].m_waitCounter = 0;
                    buffer[num17].m_blockCounter = 0;
                    buffer[num17].m_citizenUnits = 0u;
                    buffer[num17].m_path = 0u;
                    buffer[num17].m_pathPositionIndex = 0;
                    buffer[num17].m_lastPathOffset = 0;
                    buffer[num17].m_trailingVehicle = 0;
                    buffer[num17].m_cargoParent = 0;
                    buffer[num17].m_custom = 0;
                    instance.m_vehicles.ReleaseItem((ushort)num17);
                }
            }
            if (s.version >= 115) {
                for (int num18 = 1; num18 < num2; num18++) {
                    buffer2[num18].m_nextGridParked = 0;
                    buffer2[num18].m_travelDistance = 0f;
                    if (buffer2[num18].m_flags != 0) {
                        buffer2[num18].m_position = s.ReadVector3();
                        buffer2[num18].m_rotation = s.ReadQuaternion();
                        buffer2[num18].m_ownerCitizen = s.ReadUInt24();
                        if ((buffer2[num18].m_flags & 4u) != 0) {
                            instance.m_updatedParked[num18 >> 6] |= (ulong)(1L << num18);
                            instance.m_parkedUpdated = true;
                        }
                    } else {
                        buffer2[num18].m_position = Vector3.zero;
                        buffer2[num18].m_rotation = Quaternion.identity;
                        buffer2[num18].m_ownerCitizen = 0u;
                        instance.m_parkedVehicles.ReleaseItem((ushort)num18);
                    }
                }
            } else {
                for (int num19 = 1; num19 < num2; num19++) {
                    buffer2[num19].m_nextGridParked = 0;
                    buffer2[num19].m_travelDistance = 0f;
                    buffer2[num19].m_position = Vector3.zero;
                    buffer2[num19].m_rotation = Quaternion.identity;
                    buffer2[num19].m_ownerCitizen = 0u;
                    instance.m_parkedVehicles.ReleaseItem((ushort)num19);
                }
            }
            if (s.version >= 315) {
                instance.m_totalTrafficFlow = s.ReadUInt32();
                instance.m_maxTrafficFlow = s.ReadUInt32();
                instance.m_lastTrafficFlow = s.ReadUInt8();
            } else {
                instance.m_totalTrafficFlow = 0u;
                instance.m_maxTrafficFlow = 0u;
                instance.m_lastTrafficFlow = 0u;
            }
            Singleton<LoadingManager>.instance.m_loadingProfilerSimulation.EndDeserialize(s, "VehicleManager");
        }

        public void AfterDeserialize(DataSerializer s) {
            Singleton<LoadingManager>.instance.m_loadingProfilerSimulation.BeginAfterDeserialize(s, "VehicleManager");
            CitizenManager instance = Singleton<CitizenManager>.instance;
            VehicleManager instance2 = Singleton<VehicleManager>.instance;
            Singleton<LoadingManager>.instance.WaitUntilEssentialScenesLoaded();
            instance2.m_vehiclesRefreshed = false;
            PrefabCollection<VehicleInfo>.BindPrefabs();
            instance2.RefreshTransferVehicles();
            Vehicle[] buffer = instance2.m_vehicles.m_buffer;
            VehicleParked[] buffer2 = instance2.m_parkedVehicles.m_buffer;
            int num = buffer.Length;
            int num2 = buffer2.Length;
            VehicleInfo vehicleInfo = null;
            VehicleInfo vehicleInfo2 = null;
            if (s.version < 303) {
                int num3 = PrefabCollection<VehicleInfo>.PrefabCount();
                for (int i = 0; i < num3; i++) {
                    VehicleInfo prefab = PrefabCollection<VehicleInfo>.GetPrefab((uint)i);
                    if ((object)prefab != null && prefab.m_prefabDataIndex != -1) {
                        string a = PrefabCollection<VehicleInfo>.PrefabName((uint)prefab.m_prefabDataIndex);
                        if (a == "Evacuation Bus") {
                            vehicleInfo = prefab;
                        } else if (a == "Sedan") {
                            vehicleInfo2 = prefab;
                        }
                    }
                }
            }
            for (int j = 1; j < num; j++) {
                if (buffer[j].m_flags == (Vehicle.Flags)0) {
                    continue;
                }
                ushort trailingVehicle = buffer[j].m_trailingVehicle;
                if (trailingVehicle != 0) {
                    if (buffer[trailingVehicle].m_flags != 0) {
                        buffer[trailingVehicle].m_leadingVehicle = (ushort)j;
                    } else {
                        buffer[j].m_trailingVehicle = 0;
                    }
                }
                ushort cargoParent = buffer[j].m_cargoParent;
                if (cargoParent != 0) {
                    if (buffer[cargoParent].m_flags != 0) {
                        buffer[j].m_nextCargo = buffer[cargoParent].m_firstCargo;
                        buffer[cargoParent].m_firstCargo = (ushort)j;
                    } else {
                        buffer[j].m_cargoParent = 0;
                    }
                }
            }
            for (int k = 1; k < num; k++) {
                if (buffer[k].m_flags == (Vehicle.Flags)0) {
                    continue;
                }
                if (buffer[k].m_path != 0) {
                    Singleton<PathManager>.instance.m_pathUnits.m_buffer[buffer[k].m_path].m_referenceCount++;
                }
                VehicleInfo vehicleInfo3 = buffer[k].Info;
                if ((object)vehicleInfo3 != null) {
                    if ((object)vehicleInfo3 == vehicleInfo && (object)vehicleInfo2 != null) {
                        vehicleInfo3 = vehicleInfo2;
                    }
                    buffer[k].m_infoIndex = (ushort)vehicleInfo3.m_prefabDataIndex;
                    vehicleInfo3.m_vehicleAI.LoadVehicle((ushort)k, ref buffer[k]);
                    if ((buffer[k].m_flags & Vehicle.Flags.Spawned) != 0) {
                        instance2.AddToGrid((ushort)k, ref buffer[k], vehicleInfo3.m_isLargeVehicle);
                    }
                }
                int touristCount = 0;
                uint num4 = buffer[k].m_citizenUnits;
                int num5 = 0;
                while (num4 != 0) {
                    instance.m_units.m_buffer[num4].SetVehicleAfterLoading((ushort)k, ref touristCount);
                    num4 = instance.m_units.m_buffer[num4].m_nextUnit;
                    if (++num5 > 524288) {
                        CODebugBase<LogChannel>.Error(LogChannel.Core, "Invalid list detected!\n" + Environment.StackTrace);
                        break;
                    }
                }
                buffer[k].m_touristCount = (ushort)touristCount;
            }
            for (int l = 1; l < num2; l++) {
                if (buffer2[l].m_flags == 0) {
                    continue;
                }
                VehicleInfo vehicleInfo4 = buffer2[l].Info;
                if ((object)vehicleInfo4 != null) {
                    if ((object)vehicleInfo4 == vehicleInfo && (object)vehicleInfo2 != null) {
                        vehicleInfo4 = vehicleInfo2;
                    }
                    buffer2[l].m_infoIndex = (ushort)vehicleInfo4.m_prefabDataIndex;
                    instance2.AddToGrid((ushort)l, ref buffer2[l]);
                }
                uint ownerCitizen = buffer2[l].m_ownerCitizen;
                if (ownerCitizen != 0) {
                    instance.m_citizens.m_buffer[ownerCitizen].m_parkedVehicle = (ushort)l;
                }
            }
            instance2.m_vehicleCount = (int)(instance2.m_vehicles.ItemCount() - 1);
            instance2.m_parkedCount = (int)(instance2.m_parkedVehicles.ItemCount() - 1);
            Singleton<LoadingManager>.instance.m_loadingProfilerSimulation.EndAfterDeserialize(s, "VehicleManager");
        }
    }

    public const float VEHICLEGRID_CELL_SIZE = 32f;

    public const int VEHICLEGRID_RESOLUTION = 540;

    public const float VEHICLEGRID_CELL_SIZE2 = 320f;

    public const int VEHICLEGRID_RESOLUTION2 = 54;

    public const int MAX_VEHICLE_COUNT = 16384;

    public const int MAX_PARKED_COUNT = 32768;

    public int m_vehicleCount;

    public int m_parkedCount;

    public int m_infoCount;

    [NonSerialized]
    public Array16<Vehicle> m_vehicles;

    [NonSerialized]
    public Array16<VehicleParked> m_parkedVehicles;

    [NonSerialized]
    public ulong[] m_updatedParked;

    [NonSerialized]
    public bool m_parkedUpdated;

    [NonSerialized]
    public ushort[] m_vehicleGrid;

    [NonSerialized]
    public ushort[] m_vehicleGrid2;

    [NonSerialized]
    public ushort[] m_parkedGrid;

    [NonSerialized]
    public MaterialPropertyBlock m_materialBlock;

    [NonSerialized]
    public int ID_TyreMatrix;

    [NonSerialized]
    public int ID_TyrePosition;

    [NonSerialized]
    public int ID_LightState;

    [NonSerialized]
    public int ID_Color;

    [NonSerialized]
    public int ID_MainTex;

    [NonSerialized]
    public int ID_XYSMap;

    [NonSerialized]
    public int ID_ACIMap;

    [NonSerialized]
    public int ID_AtlasRect;

    [NonSerialized]
    public int ID_VehicleTransform;

    [NonSerialized]
    public int ID_VehicleLightState;

    [NonSerialized]
    public int ID_VehicleColor;

    [NonSerialized]
    public int ID_TyreLocation;

    [NonSerialized]
    public AudioGroup m_audioGroup;

    [NonSerialized]
    public int m_undergroundLayer;

    [NonSerialized]
    public uint m_totalTrafficFlow;

    [NonSerialized]
    public uint m_maxTrafficFlow;

    [NonSerialized]
    public uint m_lastTrafficFlow;

    public Texture2D m_lodRgbAtlas;

    public Texture2D m_lodXysAtlas;

    public Texture2D m_lodAciAtlas;

    private FastList<ushort>[] m_transferVehicles;

    private bool m_vehiclesRefreshed;

    private ulong[] m_renderBuffer;

    private ulong[] m_renderBuffer2;

    private static int GetTransferIndex(ItemClass.Service service, ItemClass.SubService subService, ItemClass.Level level) {
        int num = ((subService == ItemClass.SubService.None) ? ((int)(service - 1)) : ((int)(25 + subService - 1)));
        return (int)(num * 5 + level);
    }

    protected override void Awake() {
        base.Awake();
        m_vehicles = new Array16<Vehicle>(16384u);
        m_parkedVehicles = new Array16<VehicleParked>(32768u);
        m_updatedParked = new ulong[512];
        m_renderBuffer = new ulong[256];
        m_renderBuffer2 = new ulong[512];
        m_vehicleGrid = new ushort[291600];
        m_vehicleGrid2 = new ushort[2916];
        m_parkedGrid = new ushort[291600];
        m_transferVehicles = new FastList<ushort>[305];
        m_materialBlock = new MaterialPropertyBlock();
        ID_TyreMatrix = Shader.PropertyToID("_TyreMatrix");
        ID_TyrePosition = Shader.PropertyToID("_TyrePosition");
        ID_LightState = Shader.PropertyToID("_LightState");
        ID_Color = Shader.PropertyToID("_Color");
        ID_MainTex = Shader.PropertyToID("_MainTex");
        ID_XYSMap = Shader.PropertyToID("_XYSMap");
        ID_ACIMap = Shader.PropertyToID("_ACIMap");
        ID_AtlasRect = Shader.PropertyToID("_AtlasRect");
        ID_VehicleTransform = Shader.PropertyToID("_VehicleTransform");
        ID_VehicleLightState = Shader.PropertyToID("_VehicleLightState");
        ID_VehicleColor = Shader.PropertyToID("_VehicleColor");
        ID_TyreLocation = Shader.PropertyToID("_TyreLocation");
        m_audioGroup = new AudioGroup(5, new SavedFloat(Settings.effectAudioVolume, Settings.gameSettingsFile, DefaultSettings.effectAudioVolume, autoUpdate: true));
        m_undergroundLayer = LayerMask.NameToLayer("MetroTunnels");
        m_vehicles.CreateItem(out var item);
        m_parkedVehicles.CreateItem(out item);
    }

    private void OnDestroy() {
        if (m_lodRgbAtlas != null) {
            UnityEngine.Object.Destroy(m_lodRgbAtlas);
            m_lodRgbAtlas = null;
        }
        if (m_lodXysAtlas != null) {
            UnityEngine.Object.Destroy(m_lodXysAtlas);
            m_lodXysAtlas = null;
        }
        if (m_lodAciAtlas != null) {
            UnityEngine.Object.Destroy(m_lodAciAtlas);
            m_lodAciAtlas = null;
        }
    }

    public override void InitializeProperties(VehicleProperties properties) {
        base.InitializeProperties(properties);
    }

    public override void DestroyProperties(VehicleProperties properties) {
        if (m_properties == properties) {
            if (m_audioGroup != null) {
                m_audioGroup.Reset();
            }
            if (m_lodRgbAtlas != null) {
                UnityEngine.Object.Destroy(m_lodRgbAtlas);
                m_lodRgbAtlas = null;
            }
            if (m_lodXysAtlas != null) {
                UnityEngine.Object.Destroy(m_lodXysAtlas);
                m_lodXysAtlas = null;
            }
            if (m_lodAciAtlas != null) {
                UnityEngine.Object.Destroy(m_lodAciAtlas);
                m_lodAciAtlas = null;
            }
        }
        base.DestroyProperties(properties);
    }

    public override void CheckReferences() {
        base.CheckReferences();
        Singleton<LoadingManager>.instance.QueueLoadingAction(CheckReferencesImpl());
    }

    private IEnumerator CheckReferencesImpl() {
        Singleton<LoadingManager>.instance.m_loadingProfilerMain.BeginLoading("VehicleManager.CheckReferences");
        int vehicleCount = PrefabCollection<VehicleInfo>.LoadedCount();
        for (int i = 0; i < vehicleCount; i++) {
            VehicleInfo loaded = PrefabCollection<VehicleInfo>.GetLoaded((uint)i);
            if ((object)loaded != null) {
                try {
                    loaded.CheckReferences();
                }
                catch (PrefabException ex) {
                    CODebugBase<LogChannel>.Error(LogChannel.Core, ex.m_prefabInfo.gameObject.name + ": " + ex.Message + "\n" + ex.StackTrace, ex.m_prefabInfo.gameObject);
                    LoadingManager instance = Singleton<LoadingManager>.instance;
                    string brokenAssets = instance.m_brokenAssets;
                    instance.m_brokenAssets = brokenAssets + "\n" + ex.m_prefabInfo.gameObject.name + ": " + ex.Message;
                }
            }
        }
        Singleton<LoadingManager>.instance.m_loadingProfilerMain.EndLoading();
        yield return 0;
    }

    public override void InitRenderData() {
        base.InitRenderData();
        Singleton<LoadingManager>.instance.QueueLoadingAction(InitRenderDataImpl());
    }

    private IEnumerator InitRenderDataImpl() {
        Singleton<LoadingManager>.instance.m_loadingProfilerMain.BeginLoading("VehicleManager.InitRenderData");
        FastList<VehicleInfoBase> infos = new FastList<VehicleInfoBase>();
        FastList<Texture2D> rgbTextures = new FastList<Texture2D>();
        FastList<Texture2D> xysTextures = new FastList<Texture2D>();
        FastList<Texture2D> aciTextures = new FastList<Texture2D>();
        int vehicleCount = PrefabCollection<VehicleInfo>.LoadedCount();
        infos.EnsureCapacity(vehicleCount * 2);
        rgbTextures.EnsureCapacity(vehicleCount * 2);
        xysTextures.EnsureCapacity(vehicleCount * 2);
        aciTextures.EnsureCapacity(vehicleCount * 2);
        for (int i = 0; i < vehicleCount; i++) {
            VehicleInfo loaded = PrefabCollection<VehicleInfo>.GetLoaded((uint)i);
            if ((object)loaded == null) {
                continue;
            }
            if (!loaded.m_hasLodData) {
                try {
                    loaded.m_hasLodData = true;
                    if (loaded.m_lodMesh == null || loaded.m_lodMaterial == null) {
                        loaded.InitMeshData(new Rect(0f, 0f, 1f, 1f), null, null, null);
                    } else {
                        Texture2D texture2D = null;
                        if (loaded.m_lodMaterial.HasProperty(ID_MainTex)) {
                            texture2D = loaded.m_lodMaterial.GetTexture(ID_MainTex) as Texture2D;
                        }
                        Texture2D texture2D2 = null;
                        if (loaded.m_lodMaterial.HasProperty(ID_XYSMap)) {
                            texture2D2 = loaded.m_lodMaterial.GetTexture(ID_XYSMap) as Texture2D;
                        }
                        Texture2D texture2D3 = null;
                        if (loaded.m_lodMaterial.HasProperty(ID_ACIMap)) {
                            texture2D3 = loaded.m_lodMaterial.GetTexture(ID_ACIMap) as Texture2D;
                        }
                        if (texture2D == null && texture2D2 == null && texture2D3 == null && loaded.m_material.mainTexture == null) {
                            loaded.InitMeshData(new Rect(0f, 0f, 1f, 1f), null, null, null);
                        } else {
                            if (texture2D == null) {
                                throw new PrefabException(loaded, "LOD diffuse null");
                            }
                            if (texture2D2 == null) {
                                throw new PrefabException(loaded, "LOD xys null");
                            }
                            if (texture2D3 == null) {
                                throw new PrefabException(loaded, "LOD aci null");
                            }
                            if (texture2D2.width != texture2D.width || texture2D2.height != texture2D.height) {
                                throw new PrefabException(loaded, "LOD xys size doesnt match diffuse size");
                            }
                            if (texture2D3.width != texture2D.width || texture2D3.height != texture2D.height) {
                                throw new PrefabException(loaded, "LOD aci size doesnt match diffuse size");
                            }
                            try {
                                texture2D.GetPixel(0, 0);
                            }
                            catch (UnityException) {
                                throw new PrefabException(loaded, "LOD diffuse not readable");
                            }
                            try {
                                texture2D2.GetPixel(0, 0);
                            }
                            catch (UnityException) {
                                throw new PrefabException(loaded, "LOD xys not readable");
                            }
                            try {
                                texture2D3.GetPixel(0, 0);
                            }
                            catch (UnityException) {
                                throw new PrefabException(loaded, "LOD aci not readable");
                            }
                            infos.Add(loaded);
                            rgbTextures.Add(texture2D);
                            xysTextures.Add(texture2D2);
                            aciTextures.Add(texture2D3);
                        }
                    }
                }
                catch (PrefabException ex4) {
                    CODebugBase<LogChannel>.Error(LogChannel.Core, ex4.m_prefabInfo.gameObject.name + ": " + ex4.Message + "\n" + ex4.StackTrace, ex4.m_prefabInfo.gameObject);
                    LoadingManager instance = Singleton<LoadingManager>.instance;
                    string brokenAssets = instance.m_brokenAssets;
                    instance.m_brokenAssets = brokenAssets + "\n" + ex4.m_prefabInfo.gameObject.name + ": " + ex4.Message;
                }
            }
            if (loaded.m_subMeshes == null) {
                continue;
            }
            for (int j = 0; j < loaded.m_subMeshes.Length; j++) {
                try {
                    VehicleInfoBase subInfo = loaded.m_subMeshes[j].m_subInfo;
                    if (!(subInfo != null) || subInfo.m_hasLodData) {
                        continue;
                    }
                    subInfo.m_hasLodData = true;
                    if (subInfo.m_lodMesh == null || subInfo.m_lodMaterial == null) {
                        subInfo.InitMeshData(new Rect(0f, 0f, 1f, 1f), null, null, null);
                        continue;
                    }
                    Texture2D texture2D4 = null;
                    if (subInfo.m_lodMaterial.HasProperty(ID_MainTex)) {
                        texture2D4 = subInfo.m_lodMaterial.GetTexture(ID_MainTex) as Texture2D;
                    }
                    Texture2D texture2D5 = null;
                    if (subInfo.m_lodMaterial.HasProperty(ID_XYSMap)) {
                        texture2D5 = subInfo.m_lodMaterial.GetTexture(ID_XYSMap) as Texture2D;
                    }
                    Texture2D texture2D6 = null;
                    if (subInfo.m_lodMaterial.HasProperty(ID_ACIMap)) {
                        texture2D6 = subInfo.m_lodMaterial.GetTexture(ID_ACIMap) as Texture2D;
                    }
                    if (texture2D4 == null && texture2D5 == null && texture2D6 == null && subInfo.m_material.mainTexture == null) {
                        subInfo.InitMeshData(new Rect(0f, 0f, 1f, 1f), null, null, null);
                        continue;
                    }
                    if (texture2D4 == null) {
                        throw new PrefabException(subInfo, "LOD diffuse null");
                    }
                    if (texture2D5 == null) {
                        throw new PrefabException(subInfo, "LOD xys null");
                    }
                    if (texture2D6 == null) {
                        throw new PrefabException(subInfo, "LOD aci null");
                    }
                    if (texture2D5.width != texture2D4.width || texture2D5.height != texture2D4.height) {
                        throw new PrefabException(subInfo, "LOD xys size not match diffuse size");
                    }
                    if (texture2D6.width != texture2D4.width || texture2D6.height != texture2D4.height) {
                        throw new PrefabException(subInfo, "LOD aci size not match diffuse size");
                    }
                    try {
                        texture2D4.GetPixel(0, 0);
                    }
                    catch (UnityException) {
                        throw new PrefabException(subInfo, "LOD diffuse not readable");
                    }
                    try {
                        texture2D5.GetPixel(0, 0);
                    }
                    catch (UnityException) {
                        throw new PrefabException(subInfo, "LOD xys not readable");
                    }
                    try {
                        texture2D6.GetPixel(0, 0);
                    }
                    catch (UnityException) {
                        throw new PrefabException(subInfo, "LOD aci not readable");
                    }
                    infos.Add(subInfo);
                    rgbTextures.Add(texture2D4);
                    xysTextures.Add(texture2D5);
                    aciTextures.Add(texture2D6);
                }
                catch (PrefabException ex8) {
                    CODebugBase<LogChannel>.Error(LogChannel.Core, ex8.m_prefabInfo.gameObject.name + ": " + ex8.Message + "\n" + ex8.StackTrace, ex8.m_prefabInfo.gameObject);
                    LoadingManager instance2 = Singleton<LoadingManager>.instance;
                    string brokenAssets = instance2.m_brokenAssets;
                    instance2.m_brokenAssets = brokenAssets + "\n" + ex8.m_prefabInfo.gameObject.name + ": " + ex8.Message;
                }
            }
        }
        if (m_lodRgbAtlas == null) {
            m_lodRgbAtlas = new Texture2D(1024, 1024, TextureFormat.DXT1, mipmap: true, linear: false);
            m_lodRgbAtlas.filterMode = FilterMode.Trilinear;
            m_lodRgbAtlas.anisoLevel = 4;
        }
        if (m_lodXysAtlas == null) {
            m_lodXysAtlas = new Texture2D(1024, 1024, TextureFormat.DXT1, mipmap: true, linear: true);
            m_lodXysAtlas.filterMode = FilterMode.Trilinear;
            m_lodXysAtlas.anisoLevel = 4;
        }
        if (m_lodAciAtlas == null) {
            m_lodAciAtlas = new Texture2D(1024, 1024, TextureFormat.DXT1, mipmap: true, linear: true);
            m_lodAciAtlas.filterMode = FilterMode.Trilinear;
            m_lodAciAtlas.anisoLevel = 4;
        }
        Singleton<LoadingManager>.instance.m_loadingProfilerMain.PauseLoading();
        yield return 0;
        Singleton<LoadingManager>.instance.m_loadingProfilerMain.ContinueLoading();
        Rect[] rect = m_lodRgbAtlas.PackTextures(rgbTextures.ToArray(), 0, 4096, makeNoLongerReadable: false);
        Singleton<LoadingManager>.instance.m_loadingProfilerMain.PauseLoading();
        yield return 0;
        Singleton<LoadingManager>.instance.m_loadingProfilerMain.ContinueLoading();
        m_lodXysAtlas.PackTextures(xysTextures.ToArray(), 0, 4096, makeNoLongerReadable: false);
        Singleton<LoadingManager>.instance.m_loadingProfilerMain.PauseLoading();
        yield return 0;
        Singleton<LoadingManager>.instance.m_loadingProfilerMain.ContinueLoading();
        m_lodAciAtlas.PackTextures(aciTextures.ToArray(), 0, 4096, makeNoLongerReadable: false);
        Singleton<LoadingManager>.instance.m_loadingProfilerMain.PauseLoading();
        yield return 0;
        Singleton<LoadingManager>.instance.m_loadingProfilerMain.ContinueLoading();
        for (int k = 0; k < infos.m_size; k++) {
            try {
                infos.m_buffer[k].InitMeshData(rect[k], m_lodRgbAtlas, m_lodXysAtlas, m_lodAciAtlas);
            }
            catch (PrefabException ex9) {
                CODebugBase<LogChannel>.Error(LogChannel.Core, ex9.m_prefabInfo.gameObject.name + ": " + ex9.Message + "\n" + ex9.StackTrace, ex9.m_prefabInfo.gameObject);
                LoadingManager instance3 = Singleton<LoadingManager>.instance;
                string brokenAssets = instance3.m_brokenAssets;
                instance3.m_brokenAssets = brokenAssets + "\n" + ex9.m_prefabInfo.gameObject.name + ": " + ex9.Message;
            }
        }
        Singleton<LoadingManager>.instance.m_loadingProfilerMain.EndLoading();
        yield return 0;
    }

    protected override void EndRenderingImpl(RenderManager.CameraInfo cameraInfo) {
        float levelOfDetailFactor = RenderManager.LevelOfDetailFactor;
        float near = cameraInfo.m_near;
        float d = Mathf.Min(levelOfDetailFactor * 5000f, Mathf.Min(levelOfDetailFactor * 2000f + cameraInfo.m_height * 0.6f, cameraInfo.m_far));
        Vector3 lhs = cameraInfo.m_position + cameraInfo.m_directionA * near;
        Vector3 rhs = cameraInfo.m_position + cameraInfo.m_directionB * near;
        Vector3 lhs2 = cameraInfo.m_position + cameraInfo.m_directionC * near;
        Vector3 rhs2 = cameraInfo.m_position + cameraInfo.m_directionD * near;
        Vector3 lhs3 = cameraInfo.m_position + cameraInfo.m_directionA * d;
        Vector3 rhs3 = cameraInfo.m_position + cameraInfo.m_directionB * d;
        Vector3 lhs4 = cameraInfo.m_position + cameraInfo.m_directionC * d;
        Vector3 rhs4 = cameraInfo.m_position + cameraInfo.m_directionD * d;
        Vector3 vector = Vector3.Min(Vector3.Min(Vector3.Min(lhs, rhs), Vector3.Min(lhs2, rhs2)), Vector3.Min(Vector3.Min(lhs3, rhs3), Vector3.Min(lhs4, rhs4)));
        Vector3 vector2 = Vector3.Max(Vector3.Max(Vector3.Max(lhs, rhs), Vector3.Max(lhs2, rhs2)), Vector3.Max(Vector3.Max(lhs3, rhs3), Vector3.Max(lhs4, rhs4)));
        int num = Mathf.Max((int)((vector.x - 10f) / 32f + 270f), 0);
        int num2 = Mathf.Max((int)((vector.z - 10f) / 32f + 270f), 0);
        int num3 = Mathf.Min((int)((vector2.x + 10f) / 32f + 270f), 539);
        int num4 = Mathf.Min((int)((vector2.z + 10f) / 32f + 270f), 539);
        for (int i = num2; i <= num4; i++) {
            for (int j = num; j <= num3; j++) {
                ushort num5 = m_vehicleGrid[i * 540 + j];
                if (num5 != 0) {
                    m_renderBuffer[num5 >> 6] |= (ulong)(1L << (int)num5);
                }
            }
        }
        float near2 = cameraInfo.m_near;
        float d2 = Mathf.Min(2000f, cameraInfo.m_far);
        Vector3 lhs5 = cameraInfo.m_position + cameraInfo.m_directionA * near2;
        Vector3 rhs5 = cameraInfo.m_position + cameraInfo.m_directionB * near2;
        Vector3 lhs6 = cameraInfo.m_position + cameraInfo.m_directionC * near2;
        Vector3 rhs6 = cameraInfo.m_position + cameraInfo.m_directionD * near2;
        Vector3 lhs7 = cameraInfo.m_position + cameraInfo.m_directionA * d2;
        Vector3 rhs7 = cameraInfo.m_position + cameraInfo.m_directionB * d2;
        Vector3 lhs8 = cameraInfo.m_position + cameraInfo.m_directionC * d2;
        Vector3 rhs8 = cameraInfo.m_position + cameraInfo.m_directionD * d2;
        Vector3 vector3 = Vector3.Min(Vector3.Min(Vector3.Min(lhs5, rhs5), Vector3.Min(lhs6, rhs6)), Vector3.Min(Vector3.Min(lhs7, rhs7), Vector3.Min(lhs8, rhs8)));
        Vector3 vector4 = Vector3.Max(Vector3.Max(Vector3.Max(lhs5, rhs5), Vector3.Max(lhs6, rhs6)), Vector3.Max(Vector3.Max(lhs7, rhs7), Vector3.Max(lhs8, rhs8)));
        int num6 = Mathf.Max((int)((vector3.x - 10f) / 32f + 270f), 0);
        int num7 = Mathf.Max((int)((vector3.z - 10f) / 32f + 270f), 0);
        int num8 = Mathf.Min((int)((vector4.x + 10f) / 32f + 270f), 539);
        int num9 = Mathf.Min((int)((vector4.z + 10f) / 32f + 270f), 539);
        for (int k = num7; k <= num9; k++) {
            for (int l = num6; l <= num8; l++) {
                ushort num10 = m_parkedGrid[k * 540 + l];
                if (num10 != 0) {
                    m_renderBuffer2[num10 >> 6] |= (ulong)(1L << (int)num10);
                }
            }
        }
        float near3 = cameraInfo.m_near;
        float num11 = Mathf.Min(10000f, cameraInfo.m_far);
        Vector3 lhs9 = cameraInfo.m_position + cameraInfo.m_directionA * near3;
        Vector3 rhs9 = cameraInfo.m_position + cameraInfo.m_directionB * near3;
        Vector3 lhs10 = cameraInfo.m_position + cameraInfo.m_directionC * near3;
        Vector3 rhs10 = cameraInfo.m_position + cameraInfo.m_directionD * near3;
        Vector3 lhs11 = cameraInfo.m_position + cameraInfo.m_directionA * num11;
        Vector3 rhs11 = cameraInfo.m_position + cameraInfo.m_directionB * num11;
        Vector3 lhs12 = cameraInfo.m_position + cameraInfo.m_directionC * num11;
        Vector3 rhs12 = cameraInfo.m_position + cameraInfo.m_directionD * num11;
        Vector3 vector5 = Vector3.Min(Vector3.Min(Vector3.Min(lhs9, rhs9), Vector3.Min(lhs10, rhs10)), Vector3.Min(Vector3.Min(lhs11, rhs11), Vector3.Min(lhs12, rhs12)));
        Vector3 vector6 = Vector3.Max(Vector3.Max(Vector3.Max(lhs9, rhs9), Vector3.Max(lhs10, rhs10)), Vector3.Max(Vector3.Max(lhs11, rhs11), Vector3.Max(lhs12, rhs12)));
        if (cameraInfo.m_shadowOffset.x < 0f) {
            vector6.x = Mathf.Min(cameraInfo.m_position.x + num11, vector6.x - cameraInfo.m_shadowOffset.x);
        } else {
            vector5.x = Mathf.Max(cameraInfo.m_position.x - num11, vector5.x - cameraInfo.m_shadowOffset.x);
        }
        if (cameraInfo.m_shadowOffset.z < 0f) {
            vector6.z = Mathf.Min(cameraInfo.m_position.z + num11, vector6.z - cameraInfo.m_shadowOffset.z);
        } else {
            vector5.z = Mathf.Max(cameraInfo.m_position.z - num11, vector5.z - cameraInfo.m_shadowOffset.z);
        }
        int num12 = Mathf.Max((int)((vector5.x - 50f) / 320f + 27f), 0);
        int num13 = Mathf.Max((int)((vector5.z - 50f) / 320f + 27f), 0);
        int num14 = Mathf.Min((int)((vector6.x + 50f) / 320f + 27f), 53);
        int num15 = Mathf.Min((int)((vector6.z + 50f) / 320f + 27f), 53);
        for (int m = num13; m <= num15; m++) {
            for (int n = num12; n <= num14; n++) {
                ushort num16 = m_vehicleGrid2[m * 54 + n];
                if (num16 != 0) {
                    m_renderBuffer[num16 >> 6] |= (ulong)(1L << (int)num16);
                }
            }
        }
        int num17 = m_renderBuffer.Length;
        for (int num18 = 0; num18 < num17; num18++) {
            ulong num19 = m_renderBuffer[num18];
            if (num19 == 0) {
                continue;
            }
            for (int num20 = 0; num20 < 64; num20++) {
                ulong num21 = (ulong)(1L << num20);
                if ((num19 & num21) == 0) {
                    continue;
                }
                ushort num22 = (ushort)((num18 << 6) | num20);
                if (!m_vehicles.m_buffer[num22].RenderInstance(cameraInfo, num22)) {
                    num19 &= ~num21;
                }
                ushort nextGridVehicle = m_vehicles.m_buffer[num22].m_nextGridVehicle;
                int num23 = 0;
                while (nextGridVehicle != 0) {
                    int num24 = nextGridVehicle >> 6;
                    num21 = (ulong)(1L << (int)nextGridVehicle);
                    if (num24 == num18) {
                        if ((num19 & num21) != 0) {
                            break;
                        }
                        num19 |= num21;
                    } else {
                        ulong num25 = m_renderBuffer[num24];
                        if ((num25 & num21) != 0) {
                            break;
                        }
                        m_renderBuffer[num24] = num25 | num21;
                    }
                    if (nextGridVehicle > num22) {
                        break;
                    }
                    nextGridVehicle = m_vehicles.m_buffer[nextGridVehicle].m_nextGridVehicle;
                    if (++num23 > 16384) {
                        CODebugBase<LogChannel>.Error(LogChannel.Core, "Invalid list detected!\n" + Environment.StackTrace);
                        break;
                    }
                }
            }
            m_renderBuffer[num18] = num19;
        }
        int num26 = m_renderBuffer2.Length;
        for (int num27 = 0; num27 < num26; num27++) {
            ulong num28 = m_renderBuffer2[num27];
            if (num28 == 0) {
                continue;
            }
            for (int num29 = 0; num29 < 64; num29++) {
                ulong num30 = (ulong)(1L << num29);
                if ((num28 & num30) == 0) {
                    continue;
                }
                ushort num31 = (ushort)((num27 << 6) | num29);
                if (!m_parkedVehicles.m_buffer[num31].RenderInstance(cameraInfo, num31)) {
                    num28 &= ~num30;
                }
                ushort nextGridParked = m_parkedVehicles.m_buffer[num31].m_nextGridParked;
                int num32 = 0;
                while (nextGridParked != 0) {
                    int num33 = nextGridParked >> 6;
                    num30 = (ulong)(1L << (int)nextGridParked);
                    if (num33 == num27) {
                        if ((num28 & num30) != 0) {
                            break;
                        }
                        num28 |= num30;
                    } else {
                        ulong num34 = m_renderBuffer2[num33];
                        if ((num34 & num30) != 0) {
                            break;
                        }
                        m_renderBuffer2[num33] = num34 | num30;
                    }
                    if (nextGridParked > num31) {
                        break;
                    }
                    nextGridParked = m_parkedVehicles.m_buffer[nextGridParked].m_nextGridParked;
                    if (++num32 > 32768) {
                        CODebugBase<LogChannel>.Error(LogChannel.Core, "Invalid list detected!\n" + Environment.StackTrace);
                        break;
                    }
                }
            }
            m_renderBuffer2[num27] = num28;
        }
        int num35 = PrefabCollection<VehicleInfo>.PrefabCount();
        for (int num36 = 0; num36 < num35; num36++) {
            VehicleInfo prefab = PrefabCollection<VehicleInfo>.GetPrefab((uint)num36);
            if ((object)prefab == null) {
                continue;
            }
            if (prefab.m_lodCount != 0) {
                Vehicle.RenderLod(cameraInfo, prefab);
            }
            if (prefab.m_undergroundLodCount != 0) {
                Vehicle.RenderUndergroundLod(cameraInfo, prefab);
            }
            if (prefab.m_subMeshes == null) {
                continue;
            }
            for (int num37 = 0; num37 < prefab.m_subMeshes.Length; num37++) {
                VehicleInfoBase subInfo = prefab.m_subMeshes[num37].m_subInfo;
                if (subInfo != null) {
                    if (subInfo.m_lodCount != 0) {
                        Vehicle.RenderLod(cameraInfo, subInfo);
                    }
                    if (subInfo.m_undergroundLodCount != 0) {
                        Vehicle.RenderUndergroundLod(cameraInfo, subInfo);
                    }
                }
            }
        }
    }

    protected override void PlayAudioImpl(AudioManager.ListenerInfo listenerInfo) {
        if (!(m_properties != null)) {
            return;
        }
        LoadingManager instance = Singleton<LoadingManager>.instance;
        SimulationManager instance2 = Singleton<SimulationManager>.instance;
        AudioManager instance3 = Singleton<AudioManager>.instance;
        if (!instance.m_currentlyLoading) {
            int num = Mathf.Max((int)((listenerInfo.m_position.x - 150f) / 32f + 270f), 0);
            int num2 = Mathf.Max((int)((listenerInfo.m_position.z - 150f) / 32f + 270f), 0);
            int num3 = Mathf.Min((int)((listenerInfo.m_position.x + 150f) / 32f + 270f), 539);
            int num4 = Mathf.Min((int)((listenerInfo.m_position.z + 150f) / 32f + 270f), 539);
            for (int i = num2; i <= num4; i++) {
                for (int j = num; j <= num3; j++) {
                    int num5 = i * 540 + j;
                    ushort num6 = m_vehicleGrid[num5];
                    int num7 = 0;
                    while (num6 != 0) {
                        m_vehicles.m_buffer[num6].PlayAudio(listenerInfo, num6);
                        num6 = m_vehicles.m_buffer[num6].m_nextGridVehicle;
                        if (++num7 >= 16384) {
                            CODebugBase<LogChannel>.Error(LogChannel.Core, "Invalid list detected!\n" + Environment.StackTrace);
                            break;
                        }
                    }
                }
            }
        }
        if (!instance.m_currentlyLoading) {
            int num8 = Mathf.Max((int)((listenerInfo.m_position.x - 600f) / 320f + 27f), 0);
            int num9 = Mathf.Max((int)((listenerInfo.m_position.z - 600f) / 320f + 27f), 0);
            int num10 = Mathf.Min((int)((listenerInfo.m_position.x + 600f) / 320f + 27f), 53);
            int num11 = Mathf.Min((int)((listenerInfo.m_position.z + 600f) / 320f + 27f), 53);
            for (int k = num9; k <= num11; k++) {
                for (int l = num8; l <= num10; l++) {
                    int num12 = k * 54 + l;
                    ushort num13 = m_vehicleGrid2[num12];
                    int num14 = 0;
                    while (num13 != 0) {
                        m_vehicles.m_buffer[num13].PlayAudio(listenerInfo, num13);
                        num13 = m_vehicles.m_buffer[num13].m_nextGridVehicle;
                        if (++num14 >= 16384) {
                            CODebugBase<LogChannel>.Error(LogChannel.Core, "Invalid list detected!\n" + Environment.StackTrace);
                            break;
                        }
                    }
                }
            }
        }
        float masterVolume = ((!instance.m_currentlyLoading && !instance2.SimulationPaused && !instance3.MuteAll) ? instance3.MasterVolume : 0f);
        m_audioGroup.UpdatePlayers(listenerInfo, masterVolume);
    }

    public bool CreateVehicle(out ushort vehicle, ref Randomizer r, VehicleInfo info, Vector3 position, TransferManager.TransferReason type, bool transferToSource, bool transferToTarget) {
        if (m_vehicles.CreateItem(out var item, ref r)) {
            vehicle = item;
            Vehicle.Frame frame = new Vehicle.Frame(position, Quaternion.identity);
            m_vehicles.m_buffer[vehicle].m_flags = Vehicle.Flags.Created;
            m_vehicles.m_buffer[vehicle].m_flags2 = (Vehicle.Flags2)0;
            if (transferToSource) {
                m_vehicles.m_buffer[vehicle].m_flags |= Vehicle.Flags.TransferToSource;
            }
            if (transferToTarget) {
                m_vehicles.m_buffer[vehicle].m_flags |= Vehicle.Flags.TransferToTarget;
            }
            m_vehicles.m_buffer[vehicle].Info = info;
            m_vehicles.m_buffer[vehicle].m_frame0 = frame;
            m_vehicles.m_buffer[vehicle].m_frame1 = frame;
            m_vehicles.m_buffer[vehicle].m_frame2 = frame;
            m_vehicles.m_buffer[vehicle].m_frame3 = frame;
            m_vehicles.m_buffer[vehicle].m_targetPos0 = Vector4.zero;
            m_vehicles.m_buffer[vehicle].m_targetPos1 = Vector4.zero;
            m_vehicles.m_buffer[vehicle].m_targetPos2 = Vector4.zero;
            m_vehicles.m_buffer[vehicle].m_targetPos3 = Vector4.zero;
            m_vehicles.m_buffer[vehicle].m_sourceBuilding = 0;
            m_vehicles.m_buffer[vehicle].m_targetBuilding = 0;
            m_vehicles.m_buffer[vehicle].m_transferType = (byte)type;
            m_vehicles.m_buffer[vehicle].m_transferSize = 0;
            m_vehicles.m_buffer[vehicle].m_waitCounter = 0;
            m_vehicles.m_buffer[vehicle].m_blockCounter = 0;
            m_vehicles.m_buffer[vehicle].m_nextGridVehicle = 0;
            m_vehicles.m_buffer[vehicle].m_nextOwnVehicle = 0;
            m_vehicles.m_buffer[vehicle].m_nextGuestVehicle = 0;
            m_vehicles.m_buffer[vehicle].m_nextLineVehicle = 0;
            m_vehicles.m_buffer[vehicle].m_transportLine = 0;
            m_vehicles.m_buffer[vehicle].m_leadingVehicle = 0;
            m_vehicles.m_buffer[vehicle].m_trailingVehicle = 0;
            m_vehicles.m_buffer[vehicle].m_cargoParent = 0;
            m_vehicles.m_buffer[vehicle].m_firstCargo = 0;
            m_vehicles.m_buffer[vehicle].m_nextCargo = 0;
            m_vehicles.m_buffer[vehicle].m_citizenUnits = 0u;
            m_vehicles.m_buffer[vehicle].m_path = 0u;
            m_vehicles.m_buffer[vehicle].m_lastFrame = 0;
            m_vehicles.m_buffer[vehicle].m_pathPositionIndex = 0;
            m_vehicles.m_buffer[vehicle].m_lastPathOffset = 0;
            m_vehicles.m_buffer[vehicle].m_gateIndex = 0;
            m_vehicles.m_buffer[vehicle].m_waterSource = 0;
            m_vehicles.m_buffer[vehicle].m_touristCount = 0;
            m_vehicles.m_buffer[vehicle].m_custom = 0;
            info.m_vehicleAI.CreateVehicle(vehicle, ref m_vehicles.m_buffer[vehicle]);
            info.m_vehicleAI.FrameDataUpdated(vehicle, ref m_vehicles.m_buffer[vehicle], ref m_vehicles.m_buffer[vehicle].m_frame0);
            m_vehicleCount = (int)(m_vehicles.ItemCount() - 1);
            return true;
        }
        vehicle = 0;
        return false;
    }

    public void ReleaseVehicle(ushort vehicle) {
        ReleaseVehicleImplementation(vehicle, ref m_vehicles.m_buffer[vehicle]);
    }

    private void ReleaseVehicleImplementation(ushort vehicle, ref Vehicle data) {
        if (data.m_flags == (Vehicle.Flags)0) {
            return;
        }
        InstanceID id = default(InstanceID);
        id.Vehicle = vehicle;
        Singleton<InstanceManager>.instance.ReleaseInstance(id);
        data.m_flags |= Vehicle.Flags.Deleted;
        data.Unspawn(vehicle);
        data.Info?.m_vehicleAI.ReleaseVehicle(vehicle, ref data);
        if (data.m_leadingVehicle != 0) {
            if (m_vehicles.m_buffer[data.m_leadingVehicle].m_trailingVehicle == vehicle) {
                m_vehicles.m_buffer[data.m_leadingVehicle].m_trailingVehicle = 0;
            }
            data.m_leadingVehicle = 0;
        }
        if (data.m_trailingVehicle != 0) {
            if (m_vehicles.m_buffer[data.m_trailingVehicle].m_leadingVehicle == vehicle) {
                m_vehicles.m_buffer[data.m_trailingVehicle].m_leadingVehicle = 0;
            }
            data.m_trailingVehicle = 0;
        }
        ReleaseWaterSource(vehicle, ref data);
        if (data.m_cargoParent != 0) {
            ushort num = 0;
            ushort num2 = m_vehicles.m_buffer[data.m_cargoParent].m_firstCargo;
            int num3 = 0;
            while (num2 != 0) {
                if (num2 == vehicle) {
                    if (num == 0) {
                        m_vehicles.m_buffer[data.m_cargoParent].m_firstCargo = data.m_nextCargo;
                    } else {
                        m_vehicles.m_buffer[num].m_nextCargo = data.m_nextCargo;
                    }
                    break;
                }
                num = num2;
                num2 = m_vehicles.m_buffer[num2].m_nextCargo;
                if (++num3 > 16384) {
                    CODebugBase<LogChannel>.Error(LogChannel.Core, "Invalid list detected!\n" + Environment.StackTrace);
                    break;
                }
            }
            data.m_cargoParent = 0;
            data.m_nextCargo = 0;
        }
        if (data.m_firstCargo != 0) {
            ushort num4 = data.m_firstCargo;
            int num5 = 0;
            while (num4 != 0) {
                ushort nextCargo = m_vehicles.m_buffer[num4].m_nextCargo;
                m_vehicles.m_buffer[num4].m_cargoParent = 0;
                m_vehicles.m_buffer[num4].m_nextCargo = 0;
                num4 = nextCargo;
                if (++num5 > 16384) {
                    CODebugBase<LogChannel>.Error(LogChannel.Core, "Invalid list detected!\n" + Environment.StackTrace);
                    break;
                }
            }
            data.m_firstCargo = 0;
        }
        if (data.m_path != 0) {
            Singleton<PathManager>.instance.ReleasePath(data.m_path);
            data.m_path = 0u;
        }
        if (data.m_citizenUnits != 0) {
            Singleton<CitizenManager>.instance.ReleaseUnits(data.m_citizenUnits);
            data.m_citizenUnits = 0u;
        }
        data.m_flags = (Vehicle.Flags)0;
        m_vehicles.ReleaseItem(vehicle);
        m_vehicleCount = (int)(m_vehicles.ItemCount() - 1);
    }

    private void ReleaseWaterSource(ushort vehicle, ref Vehicle data) {
        if (data.m_waterSource != 0) {
            Singleton<TerrainManager>.instance.WaterSimulation.ReleaseWaterSource(data.m_waterSource);
            data.m_waterSource = 0;
        }
    }

    public void AddToGrid(ushort vehicle, ref Vehicle data, bool large) {
        Vector3 lastFramePosition = data.GetLastFramePosition();
        if (large) {
            int gridX = Mathf.Clamp((int)(lastFramePosition.x / 320f + 27f), 0, 53);
            int gridZ = Mathf.Clamp((int)(lastFramePosition.z / 320f + 27f), 0, 53);
            AddToGrid(vehicle, ref data, large, gridX, gridZ);
        } else {
            int gridX2 = Mathf.Clamp((int)(lastFramePosition.x / 32f + 270f), 0, 539);
            int gridZ2 = Mathf.Clamp((int)(lastFramePosition.z / 32f + 270f), 0, 539);
            AddToGrid(vehicle, ref data, large, gridX2, gridZ2);
        }
    }

    public void AddToGrid(ushort vehicle, ref Vehicle data, bool large, int gridX, int gridZ) {
        if (large) {
            int num = gridZ * 54 + gridX;
            data.m_nextGridVehicle = m_vehicleGrid2[num];
            m_vehicleGrid2[num] = vehicle;
        } else {
            int num2 = gridZ * 540 + gridX;
            data.m_nextGridVehicle = m_vehicleGrid[num2];
            m_vehicleGrid[num2] = vehicle;
        }
    }

    public void RemoveFromGrid(ushort vehicle, ref Vehicle data, bool large) {
        Vector3 lastFramePosition = data.GetLastFramePosition();
        if (large) {
            int gridX = Mathf.Clamp((int)(lastFramePosition.x / 320f + 27f), 0, 53);
            int gridZ = Mathf.Clamp((int)(lastFramePosition.z / 320f + 27f), 0, 53);
            RemoveFromGrid(vehicle, ref data, large, gridX, gridZ);
        } else {
            int gridX2 = Mathf.Clamp((int)(lastFramePosition.x / 32f + 270f), 0, 539);
            int gridZ2 = Mathf.Clamp((int)(lastFramePosition.z / 32f + 270f), 0, 539);
            RemoveFromGrid(vehicle, ref data, large, gridX2, gridZ2);
        }
    }

    public void RemoveFromGrid(ushort vehicle, ref Vehicle data, bool large, int gridX, int gridZ) {
        if (large) {
            int num = gridZ * 54 + gridX;
            ushort num2 = 0;
            ushort num3 = m_vehicleGrid2[num];
            int num4 = 0;
            while (num3 != 0) {
                if (num3 == vehicle) {
                    if (num2 == 0) {
                        m_vehicleGrid2[num] = data.m_nextGridVehicle;
                    } else {
                        m_vehicles.m_buffer[num2].m_nextGridVehicle = data.m_nextGridVehicle;
                    }
                    break;
                }
                num2 = num3;
                num3 = m_vehicles.m_buffer[num3].m_nextGridVehicle;
                if (++num4 > 16384) {
                    CODebugBase<LogChannel>.Error(LogChannel.Core, "Invalid list detected!\n" + Environment.StackTrace);
                    break;
                }
            }
            data.m_nextGridVehicle = 0;
            return;
        }
        int num5 = gridZ * 540 + gridX;
        ushort num6 = 0;
        ushort num7 = m_vehicleGrid[num5];
        int num8 = 0;
        while (num7 != 0) {
            if (num7 == vehicle) {
                if (num6 == 0) {
                    m_vehicleGrid[num5] = data.m_nextGridVehicle;
                } else {
                    m_vehicles.m_buffer[num6].m_nextGridVehicle = data.m_nextGridVehicle;
                }
                break;
            }
            num6 = num7;
            num7 = m_vehicles.m_buffer[num7].m_nextGridVehicle;
            if (++num8 > 16384) {
                CODebugBase<LogChannel>.Error(LogChannel.Core, "Invalid list detected!\n" + Environment.StackTrace);
                break;
            }
        }
        data.m_nextGridVehicle = 0;
    }

    public bool CreateParkedVehicle(out ushort parked, ref Randomizer r, VehicleInfo info, Vector3 position, Quaternion rotation, uint ownerCitizen) {
        if (m_parkedVehicles.CreateItem(out var item, ref r)) {
            parked = item;
            m_parkedVehicles.m_buffer[parked].m_flags = 9;
            m_parkedVehicles.m_buffer[parked].Info = info;
            m_parkedVehicles.m_buffer[parked].m_position = position;
            m_parkedVehicles.m_buffer[parked].m_rotation = rotation;
            m_parkedVehicles.m_buffer[parked].m_ownerCitizen = ownerCitizen;
            m_parkedVehicles.m_buffer[parked].m_travelDistance = 0f;
            m_parkedVehicles.m_buffer[parked].m_nextGridParked = 0;
            AddToGrid(parked, ref m_parkedVehicles.m_buffer[parked]);
            m_parkedCount = (int)(m_parkedVehicles.ItemCount() - 1);
            return true;
        }
        parked = 0;
        return false;
    }

    public void ReleaseParkedVehicle(ushort parked) {
        ReleaseParkedVehicleImplementation(parked, ref m_parkedVehicles.m_buffer[parked]);
    }

    private void ReleaseParkedVehicleImplementation(ushort parked, ref VehicleParked data) {
        if (data.m_flags != 0) {
            InstanceID id = default(InstanceID);
            id.ParkedVehicle = parked;
            Singleton<InstanceManager>.instance.ReleaseInstance(id);
            data.m_flags |= 2;
            RemoveFromGrid(parked, ref data);
            if (data.m_ownerCitizen != 0) {
                Singleton<CitizenManager>.instance.m_citizens.m_buffer[data.m_ownerCitizen].m_parkedVehicle = 0;
                data.m_ownerCitizen = 0u;
            }
            data.m_flags = 0;
            m_parkedVehicles.ReleaseItem(parked);
            m_parkedCount = (int)(m_parkedVehicles.ItemCount() - 1);
        }
    }

    public void AddToGrid(ushort parked, ref VehicleParked data) {
        int gridX = Mathf.Clamp((int)(data.m_position.x / 32f + 270f), 0, 539);
        int gridZ = Mathf.Clamp((int)(data.m_position.z / 32f + 270f), 0, 539);
        AddToGrid(parked, ref data, gridX, gridZ);
    }

    public void AddToGrid(ushort parked, ref VehicleParked data, int gridX, int gridZ) {
        int num = gridZ * 540 + gridX;
        data.m_nextGridParked = m_parkedGrid[num];
        m_parkedGrid[num] = parked;
    }

    public void RemoveFromGrid(ushort parked, ref VehicleParked data) {
        int gridX = Mathf.Clamp((int)(data.m_position.x / 32f + 270f), 0, 539);
        int gridZ = Mathf.Clamp((int)(data.m_position.z / 32f + 270f), 0, 539);
        RemoveFromGrid(parked, ref data, gridX, gridZ);
    }

    public void RemoveFromGrid(ushort parked, ref VehicleParked data, int gridX, int gridZ) {
        int num = gridZ * 540 + gridX;
        ushort num2 = 0;
        ushort num3 = m_parkedGrid[num];
        int num4 = 0;
        while (num3 != 0) {
            if (num3 == parked) {
                if (num2 == 0) {
                    m_parkedGrid[num] = data.m_nextGridParked;
                } else {
                    m_parkedVehicles.m_buffer[num2].m_nextGridParked = data.m_nextGridParked;
                }
                break;
            }
            num2 = num3;
            num3 = m_parkedVehicles.m_buffer[num3].m_nextGridParked;
            if (++num4 > 32768) {
                CODebugBase<LogChannel>.Error(LogChannel.Core, "Invalid list detected!\n" + Environment.StackTrace);
                break;
            }
        }
        data.m_nextGridParked = 0;
    }

    public bool RayCast(Segment3 ray, Vehicle.Flags ignoreFlags, VehicleParked.Flags ignoreFlags2, out Vector3 hit, out ushort vehicleIndex, out ushort parkedIndex) {
        hit = ray.b;
        vehicleIndex = 0;
        parkedIndex = 0;
        Bounds bounds = new Bounds(new Vector3(0f, 512f, 0f), new Vector3(17280f, 1152f, 17280f));
        Segment3 ray2 = ray;
        if (ray2.Clip(bounds)) {
            Vector3 b = ray2.b - ray2.a;
            Vector3 normalized = b.normalized;
            Vector3 vector = ray2.a - normalized * 72f;
            Vector3 vector2 = ray2.a + Vector3.ClampMagnitude(ray2.b - ray2.a, 2000f) + normalized * 72f;
            int num = (int)(vector.x / 32f + 270f);
            int num2 = (int)(vector.z / 32f + 270f);
            int num3 = (int)(vector2.x / 32f + 270f);
            int num4 = (int)(vector2.z / 32f + 270f);
            float num5 = Mathf.Abs(b.x);
            float num6 = Mathf.Abs(b.z);
            int num7;
            int num8;
            if (num5 >= num6) {
                num7 = ((b.x > 0f) ? 1 : (-1));
                num8 = 0;
                if (num5 != 0f) {
                    b *= 32f / num5;
                }
            } else {
                num7 = 0;
                num8 = ((b.z > 0f) ? 1 : (-1));
                if (num6 != 0f) {
                    b *= 32f / num6;
                }
            }
            Vector3 vector3 = vector;
            Vector3 vector4 = vector;
            do {
                Vector3 vector5 = vector4 + b;
                int num9;
                int num10;
                int num11;
                int num12;
                if (num7 != 0) {
                    num9 = Mathf.Max(num, 0);
                    num10 = Mathf.Min(num, 539);
                    num11 = Mathf.Max((int)((Mathf.Min(vector3.z, vector5.z) - 72f) / 32f + 270f), 0);
                    num12 = Mathf.Min((int)((Mathf.Max(vector3.z, vector5.z) + 72f) / 32f + 270f), 539);
                } else {
                    num11 = Mathf.Max(num2, 0);
                    num12 = Mathf.Min(num2, 539);
                    num9 = Mathf.Max((int)((Mathf.Min(vector3.x, vector5.x) - 72f) / 32f + 270f), 0);
                    num10 = Mathf.Min((int)((Mathf.Max(vector3.x, vector5.x) + 72f) / 32f + 270f), 539);
                }
                for (int i = num11; i <= num12; i++) {
                    for (int j = num9; j <= num10; j++) {
                        ushort num13 = m_vehicleGrid[i * 540 + j];
                        int num14 = 0;
                        while (num13 != 0) {
                            if (m_vehicles.m_buffer[num13].RayCast(num13, ray2, ignoreFlags, out var t)) {
                                Vector3 vector6 = ray2.Position(t);
                                if (Vector3.SqrMagnitude(vector6 - ray.a) < Vector3.SqrMagnitude(hit - ray.a)) {
                                    hit = vector6;
                                    vehicleIndex = num13;
                                    parkedIndex = 0;
                                }
                            }
                            num13 = m_vehicles.m_buffer[num13].m_nextGridVehicle;
                            if (++num14 > 16384) {
                                CODebugBase<LogChannel>.Error(LogChannel.Core, "Invalid list detected!\n" + Environment.StackTrace);
                                break;
                            }
                        }
                        ushort num15 = m_parkedGrid[i * 540 + j];
                        int num16 = 0;
                        while (num15 != 0) {
                            if (m_parkedVehicles.m_buffer[num15].RayCast(num15, ray2, ignoreFlags2, out var t2)) {
                                Vector3 vector7 = ray2.Position(t2);
                                if (Vector3.SqrMagnitude(vector7 - ray.a) < Vector3.SqrMagnitude(hit - ray.a)) {
                                    hit = vector7;
                                    vehicleIndex = 0;
                                    parkedIndex = num15;
                                }
                            }
                            num15 = m_parkedVehicles.m_buffer[num15].m_nextGridParked;
                            if (++num16 > 32768) {
                                CODebugBase<LogChannel>.Error(LogChannel.Core, "Invalid list detected!\n" + Environment.StackTrace);
                                break;
                            }
                        }
                    }
                }
                vector3 = vector4;
                vector4 = vector5;
                num += num7;
                num2 += num8;
            }
            while ((num <= num3 || num7 <= 0) && (num >= num3 || num7 >= 0) && (num2 <= num4 || num8 <= 0) && (num2 >= num4 || num8 >= 0));
        }
        bounds = new Bounds(new Vector3(0f, 1512f, 0f), new Vector3(17280f, 3024f, 17280f));
        ray2 = ray;
        if (ray2.Clip(bounds)) {
            Vector3 b2 = ray2.b - ray2.a;
            Vector3 normalized2 = b2.normalized;
            Vector3 vector8 = ray2.a - normalized2 * 112f;
            Vector3 vector9 = ray2.b + normalized2 * 112f;
            int num17 = (int)(vector8.x / 320f + 27f);
            int num18 = (int)(vector8.z / 320f + 27f);
            int num19 = (int)(vector9.x / 320f + 27f);
            int num20 = (int)(vector9.z / 320f + 27f);
            float num21 = Mathf.Abs(b2.x);
            float num22 = Mathf.Abs(b2.z);
            int num23;
            int num24;
            if (num21 >= num22) {
                num23 = ((b2.x > 0f) ? 1 : (-1));
                num24 = 0;
                if (num21 != 0f) {
                    b2 *= 320f / num21;
                }
            } else {
                num23 = 0;
                num24 = ((b2.z > 0f) ? 1 : (-1));
                if (num22 != 0f) {
                    b2 *= 320f / num22;
                }
            }
            Vector3 vector10 = vector8;
            Vector3 vector11 = vector8;
            do {
                Vector3 vector12 = vector11 + b2;
                int num25;
                int num26;
                int num27;
                int num28;
                if (num23 != 0) {
                    num25 = Mathf.Max(num17, 0);
                    num26 = Mathf.Min(num17, 53);
                    num27 = Mathf.Max((int)((Mathf.Min(vector10.z, vector12.z) - 112f) / 320f + 27f), 0);
                    num28 = Mathf.Min((int)((Mathf.Max(vector10.z, vector12.z) + 112f) / 320f + 27f), 53);
                } else {
                    num27 = Mathf.Max(num18, 0);
                    num28 = Mathf.Min(num18, 53);
                    num25 = Mathf.Max((int)((Mathf.Min(vector10.x, vector12.x) - 112f) / 320f + 27f), 0);
                    num26 = Mathf.Min((int)((Mathf.Max(vector10.x, vector12.x) + 112f) / 320f + 27f), 53);
                }
                for (int k = num27; k <= num28; k++) {
                    for (int l = num25; l <= num26; l++) {
                        ushort num29 = m_vehicleGrid2[k * 54 + l];
                        int num30 = 0;
                        while (num29 != 0) {
                            if (m_vehicles.m_buffer[num29].RayCast(num29, ray2, ignoreFlags, out var t3)) {
                                Vector3 vector13 = ray2.Position(t3);
                                if (Vector3.SqrMagnitude(vector13 - ray.a) < Vector3.SqrMagnitude(hit - ray.a)) {
                                    hit = vector13;
                                    vehicleIndex = num29;
                                    parkedIndex = 0;
                                }
                            }
                            num29 = m_vehicles.m_buffer[num29].m_nextGridVehicle;
                            if (++num30 > 16384) {
                                CODebugBase<LogChannel>.Error(LogChannel.Core, "Invalid list detected!\n" + Environment.StackTrace);
                                break;
                            }
                        }
                    }
                }
                vector10 = vector11;
                vector11 = vector12;
                num17 += num23;
                num18 += num24;
            }
            while ((num17 <= num19 || num23 <= 0) && (num17 >= num19 || num23 >= 0) && (num18 <= num20 || num24 <= 0) && (num18 >= num20 || num24 >= 0));
        }
        if (vehicleIndex != 0 || parkedIndex != 0) {
            return true;
        }
        hit = Vector3.zero;
        vehicleIndex = 0;
        parkedIndex = 0;
        return false;
    }

    public void UpdateParkedVehicles(float minX, float minZ, float maxX, float maxZ) {
        int num = Mathf.Max((int)((minX - 10f) / 32f + 270f), 0);
        int num2 = Mathf.Max((int)((minZ - 10f) / 32f + 270f), 0);
        int num3 = Mathf.Min((int)((maxX + 10f) / 32f + 270f), 539);
        int num4 = Mathf.Min((int)((maxZ + 10f) / 32f + 270f), 539);
        for (int i = num2; i <= num4; i++) {
            for (int j = num; j <= num3; j++) {
                ushort num5 = m_parkedGrid[i * 540 + j];
                int num6 = 0;
                while (num5 != 0) {
                    if ((m_parkedVehicles.m_buffer[num5].m_flags & 4) == 0) {
                        m_parkedVehicles.m_buffer[num5].m_flags |= 4;
                        m_updatedParked[num5 >> 6] |= (ulong)(1L << (int)num5);
                        m_parkedUpdated = true;
                    }
                    num5 = m_parkedVehicles.m_buffer[num5].m_nextGridParked;
                    if (++num6 > 32768) {
                        CODebugBase<LogChannel>.Error(LogChannel.Core, "Invalid list detected!\n" + Environment.StackTrace);
                        break;
                    }
                }
            }
        }
    }

    protected override void SimulationStepImpl(int subStep) {
        if (m_parkedUpdated) {
            int num = m_updatedParked.Length;
            for (int i = 0; i < num; i++) {
                ulong num2 = m_updatedParked[i];
                if (num2 == 0) {
                    continue;
                }
                m_updatedParked[i] = 0uL;
                for (int j = 0; j < 64; j++) {
                    if ((num2 & (ulong)(1L << j)) != 0) {
                        ushort num3 = (ushort)((i << 6) | j);
                        VehicleInfo info = m_parkedVehicles.m_buffer[num3].Info;
                        m_parkedVehicles.m_buffer[num3].m_flags &= 65531;
                        info.m_vehicleAI.UpdateParkedVehicle(num3, ref m_parkedVehicles.m_buffer[num3]);
                    }
                }
            }
            m_parkedUpdated = false;
        }
        if (subStep == 0) {
            return;
        }
        SimulationManager instance = Singleton<SimulationManager>.instance;
        Vector3 physicsLodRefPos = instance.m_simulationView.m_position + instance.m_simulationView.m_direction * 1000f;
        for (int k = 0; k < 16384; k++) {
            Vehicle.Flags flags = m_vehicles.m_buffer[k].m_flags;
            if ((flags & Vehicle.Flags.Created) != 0 && m_vehicles.m_buffer[k].m_leadingVehicle == 0) {
                VehicleInfo info2 = m_vehicles.m_buffer[k].Info;
                info2.m_vehicleAI.ExtraSimulationStep((ushort)k, ref m_vehicles.m_buffer[k]);
            }
        }
        int num4 = (int)(instance.m_currentFrameIndex & 0xF);
        int num5 = num4 * 1024;
        int num6 = (num4 + 1) * 1024 - 1;
        for (int l = num5; l <= num6; l++) {
            Vehicle.Flags flags2 = m_vehicles.m_buffer[l].m_flags;
            if ((flags2 & Vehicle.Flags.Created) != 0 && m_vehicles.m_buffer[l].m_leadingVehicle == 0) {
                VehicleInfo info3 = m_vehicles.m_buffer[l].Info;
                info3.m_vehicleAI.SimulationStep((ushort)l, ref m_vehicles.m_buffer[l], physicsLodRefPos);
            }
        }
        if ((instance.m_currentFrameIndex & 0xFF) == 0) {
            uint num7 = m_maxTrafficFlow / 100u;
            if (num7 == 0) {
                num7 = 1u;
            }
            uint num8 = m_totalTrafficFlow / num7;
            if (num8 > 100) {
                num8 = 100u;
            }
            m_lastTrafficFlow = num8;
            m_totalTrafficFlow = 0u;
            m_maxTrafficFlow = 0u;
            StatisticsManager instance2 = Singleton<StatisticsManager>.instance;
            StatisticBase statisticBase = instance2.Acquire<StatisticInt32>(StatisticType.TrafficFlow);
            statisticBase.Set((int)num8);
        }
    }

    public VehicleInfo GetRandomVehicleInfo(ref Randomizer r, ItemClass.Service service, ItemClass.SubService subService, ItemClass.Level level) {
        if (!m_vehiclesRefreshed) {
            CODebugBase<LogChannel>.Error(LogChannel.Core, "Random vehicles not refreshed yet!\n" + Environment.StackTrace);
            return null;
        }
        int transferIndex = GetTransferIndex(service, subService, level);
        FastList<ushort> fastList = m_transferVehicles[transferIndex];
        if (fastList == null) {
            return null;
        }
        if (fastList.m_size == 0) {
            return null;
        }
        transferIndex = r.Int32((uint)fastList.m_size);
        return PrefabCollection<VehicleInfo>.GetPrefab(fastList.m_buffer[transferIndex]);
    }

    public VehicleInfo GetRandomVehicleInfo(ref Randomizer r, ItemClass.Service service, ItemClass.SubService subService, ItemClass.Level level, VehicleInfo.VehicleType type) {
        if (!m_vehiclesRefreshed) {
            CODebugBase<LogChannel>.Error(LogChannel.Core, "Random vehicles not refreshed yet!\n" + Environment.StackTrace);
            return null;
        }
        int transferIndex = GetTransferIndex(service, subService, level);
        FastList<ushort> fastList = m_transferVehicles[transferIndex];
        if (fastList == null) {
            return null;
        }
        int num = FindFirst(fastList.m_buffer, fastList.m_size, type);
        int num2 = FindFirst(fastList.m_buffer, fastList.m_size, type + 1);
        if (num2 - num <= 0) {
            return null;
        }
        transferIndex = r.Int32(num, num2 - 1);
        return PrefabCollection<VehicleInfo>.GetPrefab(fastList.m_buffer[transferIndex]);
    }

    private int FindFirst(ushort[] vehicles, int size, VehicleInfo.VehicleType type) {
        int num = 0;
        int num2 = size;
        while (num2 > num) {
            int num3 = num2 + num >> 1;
            VehicleInfo.VehicleType vehicleType = PrefabCollection<VehicleInfo>.GetPrefab(vehicles[num3]).m_vehicleType;
            if (vehicleType < type) {
                num = num3 + 1;
            } else {
                num2 = num3;
            }
        }
        return num2;
    }

    public void RefreshTransferVehicles() {
        if (m_vehiclesRefreshed) {
            return;
        }
        int num = m_transferVehicles.Length;
        for (int i = 0; i < num; i++) {
            m_transferVehicles[i] = null;
        }
        int num2 = PrefabCollection<VehicleInfo>.PrefabCount();
        for (int j = 0; j < num2; j++) {
            VehicleInfo prefab = PrefabCollection<VehicleInfo>.GetPrefab((uint)j);
            if ((object)prefab != null && prefab.m_class.m_service != 0 && prefab.m_placementStyle == ItemClass.Placement.Automatic) {
                int transferIndex = GetTransferIndex(prefab.m_class.m_service, prefab.m_class.m_subService, prefab.m_class.m_level);
                if (m_transferVehicles[transferIndex] == null) {
                    m_transferVehicles[transferIndex] = new FastList<ushort>();
                }
                m_transferVehicles[transferIndex].Add((ushort)j);
            }
        }
        for (int k = 0; k < num; k++) {
            if (m_transferVehicles[k] != null) {
                Array.Sort(m_transferVehicles[k].m_buffer, 0, m_transferVehicles[k].m_size, VehicleTypeComparer.comparer);
            }
        }
        int num3 = 61;
        for (int l = 0; l < num3; l++) {
            for (int m = 1; m < 5; m++) {
                int num4 = l;
                num4 = num4 * 5 + m;
                FastList<ushort> fastList = m_transferVehicles[num4];
                FastList<ushort> fastList2 = m_transferVehicles[num4 - 1];
                if (fastList == null && fastList2 != null) {
                    m_transferVehicles[num4] = fastList2;
                }
            }
        }
        m_vehiclesRefreshed = true;
    }

    public IEnumerator<bool> SetVehicleName(ushort vehicleID, string name) {
        bool result = false;
        Vehicle.Flags flags = m_vehicles.m_buffer[vehicleID].m_flags;
        if (vehicleID != 0 && flags != 0) {
            VehicleInfo info = m_vehicles.m_buffer[vehicleID].Info;
            if ((object)info != null) {
                result = info.m_vehicleAI.SetVehicleName(vehicleID, ref m_vehicles.m_buffer[vehicleID], name);
            }
            if (!result) {
                if (!name.IsNullOrWhiteSpace() && name != GenerateVehicleName(vehicleID)) {
                    m_vehicles.m_buffer[vehicleID].m_flags = flags | Vehicle.Flags.CustomName;
                    InstanceID id = default(InstanceID);
                    id.Vehicle = vehicleID;
                    Singleton<InstanceManager>.instance.SetName(id, name);
                    Singleton<GuideManager>.instance.m_renameNotUsed.Disable();
                } else if ((flags & Vehicle.Flags.CustomName) != 0) {
                    m_vehicles.m_buffer[vehicleID].m_flags = flags & ~Vehicle.Flags.CustomName;
                    InstanceID id2 = default(InstanceID);
                    id2.Vehicle = vehicleID;
                    Singleton<InstanceManager>.instance.SetName(id2, null);
                }
                result = true;
            }
        }
        yield return result;
    }

    public IEnumerator<bool> SetParkedVehicleName(ushort parkedID, string name) {
        bool result = false;
        VehicleParked.Flags flags = (VehicleParked.Flags)m_parkedVehicles.m_buffer[parkedID].m_flags;
        if (parkedID != 0 && flags != 0) {
            if (!name.IsNullOrWhiteSpace() && name != GenerateParkedVehicleName(parkedID)) {
                m_parkedVehicles.m_buffer[parkedID].m_flags = (ushort)(flags | VehicleParked.Flags.CustomName);
                InstanceID id = default(InstanceID);
                id.ParkedVehicle = parkedID;
                Singleton<InstanceManager>.instance.SetName(id, name);
                Singleton<GuideManager>.instance.m_renameNotUsed.Disable();
            } else if ((flags & VehicleParked.Flags.CustomName) != 0) {
                m_parkedVehicles.m_buffer[parkedID].m_flags = (ushort)((uint)flags & 0xFFFFFFEFu);
                InstanceID id2 = default(InstanceID);
                id2.ParkedVehicle = parkedID;
                Singleton<InstanceManager>.instance.SetName(id2, null);
            }
            result = true;
        }
        yield return result;
    }

    public override void UpdateData(SimulationManager.UpdateMode mode) {
        Singleton<LoadingManager>.instance.m_loadingProfilerSimulation.BeginLoading("VehicleManager.UpdateData");
        base.UpdateData(mode);
        for (int i = 1; i < 16384; i++) {
            if (m_vehicles.m_buffer[i].m_flags != 0) {
                VehicleInfo info = m_vehicles.m_buffer[i].Info;
                if ((object)info == null) {
                    ReleaseVehicle((ushort)i);
                }
            }
        }
        for (int j = 1; j < 32768; j++) {
            if (m_parkedVehicles.m_buffer[j].m_flags != 0) {
                VehicleInfo info2 = m_parkedVehicles.m_buffer[j].Info;
                if ((object)info2 == null) {
                    ReleaseParkedVehicle((ushort)j);
                }
            }
        }
        m_infoCount = PrefabCollection<VehicleInfo>.PrefabCount();
        Singleton<LoadingManager>.instance.m_loadingProfilerSimulation.EndLoading();
    }

    public override void GetData(FastList<IDataContainer> data) {
        base.GetData(data);
        data.Add(new Data());
    }

    public string GetVehicleName(ushort vehicleID) {
        if (m_vehicles.m_buffer[vehicleID].m_flags != 0) {
            string text = null;
            VehicleInfo info = m_vehicles.m_buffer[vehicleID].Info;
            if ((object)info != null) {
                text = info.m_vehicleAI.GetVehicleName(vehicleID, ref m_vehicles.m_buffer[vehicleID]);
            }
            if (text == null) {
                if ((m_vehicles.m_buffer[vehicleID].m_flags & Vehicle.Flags.CustomName) != 0) {
                    InstanceID id = default(InstanceID);
                    id.Vehicle = vehicleID;
                    text = Singleton<InstanceManager>.instance.GetName(id);
                }
                if (text == null) {
                    text = GenerateVehicleName(vehicleID);
                }
            }
            return text;
        }
        return null;
    }

    public string GetParkedVehicleName(ushort parkedID) {
        if (m_parkedVehicles.m_buffer[parkedID].m_flags != 0) {
            string text = null;
            if ((m_parkedVehicles.m_buffer[parkedID].m_flags & 0x10u) != 0) {
                InstanceID id = default(InstanceID);
                id.ParkedVehicle = parkedID;
                text = Singleton<InstanceManager>.instance.GetName(id);
            }
            if (text == null) {
                text = GenerateParkedVehicleName(parkedID);
            }
            return text;
        }
        return null;
    }

    public string GetDefaultVehicleName(ushort vehicleID) {
        return GenerateVehicleName(vehicleID);
    }

    public string GetDefaultParkedVehicleName(ushort parkedID) {
        return GenerateParkedVehicleName(parkedID);
    }

    private string GenerateVehicleName(ushort vehicleID) {
        VehicleInfo info = m_vehicles.m_buffer[vehicleID].Info;
        if ((object)info != null) {
            string key = PrefabCollection<VehicleInfo>.PrefabName((uint)info.m_prefabDataIndex);
            return ColossalFramework.Globalization.Locale.Get("VEHICLE_TITLE", key);
        }
        return "Invalid";
    }

    private string GenerateParkedVehicleName(ushort parkedID) {
        VehicleInfo info = m_parkedVehicles.m_buffer[parkedID].Info;
        if ((object)info != null) {
            string key = PrefabCollection<VehicleInfo>.PrefabName((uint)info.m_prefabDataIndex);
            return ColossalFramework.Globalization.Locale.Get("VEHICLE_TITLE", key);
        }
        return "Invalid";
    }

    string ISimulationManager.GetName() {
        return GetName();
    }

    ThreadProfiler ISimulationManager.GetSimulationProfiler() {
        return GetSimulationProfiler();
    }

    void ISimulationManager.SimulationStep(int subStep) {
        SimulationStep(subStep);
    }

    string IRenderableManager.GetName() {
        return GetName();
    }

    DrawCallData IRenderableManager.GetDrawCallData() {
        return GetDrawCallData();
    }

    void IRenderableManager.BeginRendering(RenderManager.CameraInfo cameraInfo) {
        BeginRendering(cameraInfo);
    }

    void IRenderableManager.EndRendering(RenderManager.CameraInfo cameraInfo) {
        EndRendering(cameraInfo);
    }

    void IRenderableManager.BeginOverlay(RenderManager.CameraInfo cameraInfo) {
        BeginOverlay(cameraInfo);
    }

    void IRenderableManager.EndOverlay(RenderManager.CameraInfo cameraInfo) {
        EndOverlay(cameraInfo);
    }

    void IRenderableManager.UndergroundOverlay(RenderManager.CameraInfo cameraInfo) {
        UndergroundOverlay(cameraInfo);
    }

    void IAudibleManager.PlayAudio(AudioManager.ListenerInfo listenerInfo) {
        PlayAudio(listenerInfo);
    }
}
