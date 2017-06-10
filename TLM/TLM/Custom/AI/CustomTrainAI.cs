﻿#define USEPATHWAITCOUNTERx

using ColossalFramework;
using ColossalFramework.Math;
using System;
using System.Collections.Generic;
using System.Text;
using TrafficManager.Custom.PathFinding;
using TrafficManager.State;
using TrafficManager.Geometry;
using UnityEngine;
using TrafficManager.Traffic;
using TrafficManager.Manager;
using CSUtil.Commons;

namespace TrafficManager.Custom.AI {
	public class CustomTrainAI : TrainAI { // TODO inherit from VehicleAI (in order to keep the correct references to `base`)
		public void TrafficManagerSimulationStep(ushort vehicleId, ref Vehicle vehicleData, Vector3 physicsLodRefPos) {
			if ((vehicleData.m_flags & Vehicle.Flags.WaitingPath) != 0) {
				byte pathFindFlags = Singleton<PathManager>.instance.m_pathUnits.m_buffer[(int)((UIntPtr)vehicleData.m_path)].m_pathFindFlags;

#if USEPATHWAITCOUNTER
				if ((pathFindFlags & (PathUnit.FLAG_READY | PathUnit.FLAG_FAILED)) != 0) {
					VehicleState state = VehicleStateManager.Instance._GetVehicleState(vehicleId);
					state.PathWaitCounter = 0; // NON-STOCK CODE
				}
#endif

				if ((pathFindFlags & PathUnit.FLAG_READY) != 0) {
					try {
						this.PathFindReady(vehicleId, ref vehicleData);
						// NON-STOCK CODE START
						if ((Options.prioritySignsEnabled || Options.timedLightsEnabled) &&
							(vehicleData.m_flags & Vehicle.Flags.Spawned) != (Vehicle.Flags)0) {
#if DEBUG
							if (GlobalConfig.Instance.DebugSwitches[9])
								Log._Debug($"CustomTrainAI.CustomSimulationStep({vehicleId}): calling OnSpawnVehicle after PathFindReady");
#endif
							VehicleStateManager.Instance.OnSpawnVehicle(vehicleId, ref vehicleData);
						}
						// NON-STOCK CODE END
					} catch (Exception e) {
						Log.Warning($"TrainAI.PathFindReady({vehicleId}) for vehicle {vehicleData.Info?.m_class?.name} threw an exception: {e.ToString()}");
						vehicleData.m_flags &= ~Vehicle.Flags.WaitingPath;
						Singleton<PathManager>.instance.ReleasePath(vehicleData.m_path);
						vehicleData.m_path = 0u;
						vehicleData.Unspawn(vehicleId);
						return;
					}
				} else if ((pathFindFlags & PathUnit.FLAG_FAILED) != 0 || vehicleData.m_path == 0) {
					vehicleData.m_flags &= ~Vehicle.Flags.WaitingPath;
					Singleton<PathManager>.instance.ReleasePath(vehicleData.m_path);
					vehicleData.m_path = 0u;
					vehicleData.Unspawn(vehicleId);
					return;
				}
#if USEPATHWAITCOUNTER
				else {
					VehicleState state = VehicleStateManager.Instance._GetVehicleState(vehicleId);
					state.PathWaitCounter = (ushort)Math.Min(ushort.MaxValue, (int)state.PathWaitCounter+1); // NON-STOCK CODE
				}
#endif
			} else {
				if ((vehicleData.m_flags & Vehicle.Flags.WaitingSpace) != 0) {
					this.TrySpawn(vehicleId, ref vehicleData);
					// NON-STOCK CODE START
					if ((Options.prioritySignsEnabled || Options.timedLightsEnabled) &&
						(vehicleData.m_flags & Vehicle.Flags.Spawned) != (Vehicle.Flags)0) {
#if DEBUG
						if (GlobalConfig.Instance.DebugSwitches[9])
							Log._Debug($"CustomTrainAI.CustomSimulationStep({vehicleId}): calling OnSpawnVehicle after TrySpawn");
#endif
						VehicleStateManager.Instance.OnSpawnVehicle(vehicleId, ref vehicleData);
					}
					// NON-STOCK CODE END
				}
			}

			bool reversed = (vehicleData.m_flags & Vehicle.Flags.Reversed) != 0;
			ushort frontVehicleId;
			if (reversed) {
				frontVehicleId = vehicleData.GetLastVehicle(vehicleId);
			} else {
				frontVehicleId = vehicleId;
			}

			VehicleManager instance = Singleton<VehicleManager>.instance;
			VehicleInfo info = instance.m_vehicles.m_buffer[(int)frontVehicleId].Info;
			info.m_vehicleAI.SimulationStep(frontVehicleId, ref instance.m_vehicles.m_buffer[(int)frontVehicleId], vehicleId, ref vehicleData, 0);
			if ((vehicleData.m_flags & (Vehicle.Flags.Created | Vehicle.Flags.Deleted)) != Vehicle.Flags.Created) {
				return;
			}
			bool flag2 = (vehicleData.m_flags & Vehicle.Flags.Reversed) != 0;
			if (flag2 != reversed) {
				reversed = flag2;
				if (reversed) {
					frontVehicleId = vehicleData.GetLastVehicle(vehicleId);
				} else {
					frontVehicleId = vehicleId;
				}
				info = instance.m_vehicles.m_buffer[(int)frontVehicleId].Info;
				info.m_vehicleAI.SimulationStep(frontVehicleId, ref instance.m_vehicles.m_buffer[(int)frontVehicleId], vehicleId, ref vehicleData, 0);
				if ((vehicleData.m_flags & (Vehicle.Flags.Created | Vehicle.Flags.Deleted)) != Vehicle.Flags.Created) {
					return;
				}
				flag2 = ((vehicleData.m_flags & Vehicle.Flags.Reversed) != 0);
				if (flag2 != reversed) {
					Singleton<VehicleManager>.instance.ReleaseVehicle(vehicleId);
					return;
				}
			}
			if (reversed) {
				frontVehicleId = instance.m_vehicles.m_buffer[(int)frontVehicleId].m_leadingVehicle;
				int num2 = 0;
				while (frontVehicleId != 0) {
					info = instance.m_vehicles.m_buffer[(int)frontVehicleId].Info;
					info.m_vehicleAI.SimulationStep(frontVehicleId, ref instance.m_vehicles.m_buffer[(int)frontVehicleId], vehicleId, ref vehicleData, 0);
					if ((vehicleData.m_flags & (Vehicle.Flags.Created | Vehicle.Flags.Deleted)) != Vehicle.Flags.Created) {
						return;
					}
					frontVehicleId = instance.m_vehicles.m_buffer[(int)frontVehicleId].m_leadingVehicle;
					if (++num2 > 16384) {
						CODebugBase<LogChannel>.Error(LogChannel.Core, "Invalid list detected!\n" + Environment.StackTrace);
						break;
					}
				}
			} else {
				frontVehicleId = instance.m_vehicles.m_buffer[(int)frontVehicleId].m_trailingVehicle;
				int num3 = 0;
				while (frontVehicleId != 0) {
					info = instance.m_vehicles.m_buffer[(int)frontVehicleId].Info;
					info.m_vehicleAI.SimulationStep(frontVehicleId, ref instance.m_vehicles.m_buffer[(int)frontVehicleId], vehicleId, ref vehicleData, 0);
					if ((vehicleData.m_flags & (Vehicle.Flags.Created | Vehicle.Flags.Deleted)) != Vehicle.Flags.Created) {
						return;
					}
					frontVehicleId = instance.m_vehicles.m_buffer[(int)frontVehicleId].m_trailingVehicle;
					if (++num3 > 16384) {
						CODebugBase<LogChannel>.Error(LogChannel.Core, "Invalid list detected!\n" + Environment.StackTrace);
						break;
					}
				}
			}
			if ((vehicleData.m_flags & (Vehicle.Flags.Spawned | Vehicle.Flags.WaitingPath | Vehicle.Flags.WaitingSpace | Vehicle.Flags.WaitingCargo)) == 0 || (vehicleData.m_blockCounter == 255 && Options.enableDespawning)) {
				Singleton<VehicleManager>.instance.ReleaseVehicle(vehicleId);
			}
		}

		public void TmCalculateSegmentPositionPathFinder(ushort vehicleID, ref Vehicle vehicleData, PathUnit.Position position, uint laneID, byte offset, out Vector3 pos, out Vector3 dir, out float maxSpeed) {
			NetManager instance = Singleton<NetManager>.instance;
			instance.m_lanes.m_buffer[(int)((UIntPtr)laneID)].CalculatePositionAndDirection((float)offset * 0.003921569f, out pos, out dir);
			NetInfo info = instance.m_segments.m_buffer[(int)position.m_segment].Info;
			if (info.m_lanes != null && info.m_lanes.Length > (int)position.m_lane) {
				var laneSpeedLimit = Options.customSpeedLimitsEnabled ? SpeedLimitManager.Instance.GetLockFreeGameSpeedLimit(position.m_segment, position.m_lane, laneID, info.m_lanes[position.m_lane]) : info.m_lanes[position.m_lane].m_speedLimit;
				maxSpeed = this.CalculateTargetSpeed(vehicleID, ref vehicleData, laneSpeedLimit, instance.m_lanes.m_buffer[(int)((UIntPtr)laneID)].m_curve);
			} else {
				maxSpeed = this.CalculateTargetSpeed(vehicleID, ref vehicleData, 1f, 0f);
			}
		}

		public bool CustomStartPathFind(ushort vehicleID, ref Vehicle vehicleData, Vector3 startPos, Vector3 endPos, bool startBothWays, bool endBothWays) {
#if DEBUG
			//Log._Debug($"CustomTrainAI.CustomStartPathFind called for vehicle {vehicleID}");
#endif

			/// NON-STOCK CODE START ///
			ExtVehicleType vehicleType = VehicleStateManager.Instance.VehicleStates[VehicleStateManager.Instance.GetFrontVehicleId(vehicleID, ref vehicleData)].vehicleType;
			if (vehicleType == ExtVehicleType.None) {
#if DEBUG
				Log.Warning($"CustomTrainAI.CustomStartPathFind: Vehicle {vehicleID} does not have a valid vehicle type!");
#endif
				vehicleType = ExtVehicleType.RailVehicle;
			} else if (vehicleType == ExtVehicleType.CargoTrain) {
				vehicleType = ExtVehicleType.CargoVehicle;
			}
			/// NON-STOCK CODE END ///

			VehicleInfo info = this.m_info;
			if ((vehicleData.m_flags & Vehicle.Flags.Spawned) == 0 && Vector3.Distance(startPos, endPos) < 100f) {
				startPos = endPos;
			}
			bool allowUnderground;
			bool allowUnderground2;
			if (info.m_vehicleType == VehicleInfo.VehicleType.Metro) {
				allowUnderground = true;
				allowUnderground2 = true;
			} else {
				allowUnderground = ((vehicleData.m_flags & (Vehicle.Flags.Underground | Vehicle.Flags.Transition)) != 0);
				allowUnderground2 = false;
			}
			PathUnit.Position startPosA;
			PathUnit.Position startPosB;
			float startSqrDistA;
			float startSqrDistB;
			PathUnit.Position endPosA;
			PathUnit.Position endPosB;
			float endSqrDistA;
			float endSqrDistB;
			if (CustomPathManager.FindPathPosition(startPos, this.m_transportInfo.m_netService, this.m_transportInfo.m_secondaryNetService, NetInfo.LaneType.Vehicle, info.m_vehicleType, VehicleInfo.VehicleType.None, allowUnderground, false, 32f, out startPosA, out startPosB, out startSqrDistA, out startSqrDistB) &&
				CustomPathManager.FindPathPosition(endPos, this.m_transportInfo.m_netService, this.m_transportInfo.m_secondaryNetService, NetInfo.LaneType.Vehicle, info.m_vehicleType, VehicleInfo.VehicleType.None, allowUnderground2, false, 32f, out endPosA, out endPosB, out endSqrDistA, out endSqrDistB)) {
				if (!startBothWays || startSqrDistB > startSqrDistA * 1.2f) {
					startPosB = default(PathUnit.Position);
				}
				if (!endBothWays || endSqrDistB > endSqrDistA * 1.2f) {
					endPosB = default(PathUnit.Position);
				}
				uint path;
				if (CustomPathManager._instance.CreatePath((ExtVehicleType)vehicleType, vehicleID, ExtCitizenInstance.ExtPathType.None, out path, ref Singleton<SimulationManager>.instance.m_randomizer, Singleton<SimulationManager>.instance.m_currentBuildIndex, startPosA, startPosB, endPosA, endPosB, NetInfo.LaneType.Vehicle, info.m_vehicleType, 20000f, false, false, true, false)) {
#if USEPATHWAITCOUNTER
					VehicleState state = VehicleStateManager.Instance._GetVehicleState(vehicleID);
					state.PathWaitCounter = 0;
#endif

					if (vehicleData.m_path != 0u) {
						Singleton<PathManager>.instance.ReleasePath(vehicleData.m_path);
					}
					vehicleData.m_path = path;
					vehicleData.m_flags |= Vehicle.Flags.WaitingPath;
					return true;
				}
			}
			return false;
		}

		public void CustomCheckNextLane(ushort vehicleId, ref Vehicle vehicleData, ref float maxSpeed, PathUnit.Position position, uint laneID, byte offset, PathUnit.Position prevPos, uint prevLaneID, byte prevOffset, Bezier3 bezier) {
			NetManager netManager = Singleton<NetManager>.instance;
			Vehicle.Frame lastFrameData = vehicleData.GetLastFrameData();
			float sqrVelocity = lastFrameData.m_velocity.sqrMagnitude;

			// NON-STOCK CODE START
			ushort frontVehicleId = VehicleStateManager.Instance.GetFrontVehicleId(vehicleId, ref vehicleData);
#if DEBUG
			if (GlobalConfig.Instance.DebugSwitches[9])
				Log._Debug($"CustomTrainAI.CustomCheckNextLane({vehicleId}): processing front vehicle {frontVehicleId} @ seg. {position.m_segment}, lane {position.m_lane}, off {position.m_offset}");
#endif
			if (vehicleId == frontVehicleId) {
				if (Options.prioritySignsEnabled || Options.timedLightsEnabled) {
					// update vehicle position for timed traffic lights and priority signs
					int pathPosIndex = vehicleData.m_pathPositionIndex >> 1;
					PathUnit.Position curPathPos = Singleton<PathManager>.instance.m_pathUnits.m_buffer[vehicleData.m_path].GetPosition(pathPosIndex);
					PathUnit.Position nextPathPos;
					Singleton<PathManager>.instance.m_pathUnits.m_buffer[vehicleData.m_path].GetNextPosition(pathPosIndex, out nextPathPos);
					VehicleStateManager.Instance.VehicleStates[frontVehicleId].UpdatePosition(ref vehicleData, sqrVelocity, ref curPathPos, ref nextPathPos);
				}
			} else {
#if DEBUG
				if (GlobalConfig.Instance.DebugSwitches[9])
					Log._Debug($"CustomTrainAI.CustomCheckNextLane({vehicleId}): processing non-front vehicle! frontVehicleId={frontVehicleId}");
#endif
			}
			// NON-STOCK CODE END

			Vector3 lastPos = lastFrameData.m_position;
			Vector3 lastPos2 = lastFrameData.m_position;
			Vector3 b = lastFrameData.m_rotation * new Vector3(0f, 0f, this.m_info.m_generatedInfo.m_wheelBase * 0.5f);
			lastPos += b;
			lastPos2 -= b;
			float num = 0.5f * sqrVelocity / this.m_info.m_braking;
			float a3 = Vector3.Distance(lastPos, bezier.a);
			float b2 = Vector3.Distance(lastPos2, bezier.a);
			if (Mathf.Min(a3, b2) >= num - 5f) {
				if (!netManager.m_lanes.m_buffer[(int)((UIntPtr)laneID)].CheckSpace(1000f, vehicleId)) {
					maxSpeed = 0f;
					return;
				}
				Vector3 vector = bezier.Position(0.5f);
				Segment3 segment;
				if (Vector3.SqrMagnitude(vehicleData.m_segment.a - vector) < Vector3.SqrMagnitude(bezier.a - vector)) {
					segment = new Segment3(vehicleData.m_segment.a, vector);
				} else {
					segment = new Segment3(bezier.a, vector);
				}
				if (segment.LengthSqr() >= 3f) {
					segment.a += (segment.b - segment.a).normalized * 2.5f;
					if (CustomTrainAI.CheckOverlap(vehicleId, ref vehicleData, segment, vehicleId)) {
						maxSpeed = 0f;
						return;
					}
				}
				segment = new Segment3(vector, bezier.d);
				if (segment.LengthSqr() >= 1f && CustomTrainAI.CheckOverlap(vehicleId, ref vehicleData, segment, vehicleId)) {
					maxSpeed = 0f;
					return;
				}
				//if (this.m_info.m_vehicleType != VehicleInfo.VehicleType.Monorail) { // NON-STOCK CODE
				ushort targetNodeId;
				if (offset < position.m_offset) {
					targetNodeId = netManager.m_segments.m_buffer[(int)position.m_segment].m_startNode;
				} else {
					targetNodeId = netManager.m_segments.m_buffer[(int)position.m_segment].m_endNode;
				}
				ushort prevTargetNodeId;
				if (prevOffset == 0) {
					prevTargetNodeId = netManager.m_segments.m_buffer[(int)prevPos.m_segment].m_startNode;
				} else {
					prevTargetNodeId = netManager.m_segments.m_buffer[(int)prevPos.m_segment].m_endNode;
				}
				if (targetNodeId == prevTargetNodeId) {
					float oldMaxSpeed = maxSpeed;
#if DEBUG
					bool debug = targetNodeId == 30132;
					if (debug)
						Log._Debug($"Train {vehicleId} wants to change segment. seg. {prevPos.m_segment} -> node {targetNodeId} -> seg. {position.m_segment}");
#else
					bool debug = false;
#endif

					if (vehicleId == frontVehicleId && !Options.isStockLaneChangerUsed()) {
						// Advanced AI traffic measurement
						VehicleStateManager.Instance.LogTraffic(frontVehicleId, ref vehicleData, position.m_segment, position.m_lane, true);
					}

					bool mayChange = VehicleBehaviorManager.Instance.MayChangeSegment(frontVehicleId, ref VehicleStateManager.Instance.VehicleStates[frontVehicleId], ref Singleton<VehicleManager>.instance.m_vehicles.m_buffer[frontVehicleId], ref lastFrameData, false, ref prevPos, ref netManager.m_segments.m_buffer[prevPos.m_segment], prevTargetNodeId, prevLaneID, ref position, targetNodeId, ref netManager.m_nodes.m_buffer[targetNodeId], laneID, out maxSpeed, debug);
					if (!mayChange) {
						return;
					}
					maxSpeed = oldMaxSpeed;
				}
				//} // NON-STOCK CODE
			}
		}

		protected static bool CheckOverlap(ushort vehicleID, ref Vehicle vehicleData, Segment3 segment, ushort ignoreVehicle) {
			Log.Error("CustomTrainAI.CheckOverlap (1) called.");
			return false;
		}

		protected static ushort CheckOverlap(ushort vehicleID, ref Vehicle vehicleData, Segment3 segment, ushort ignoreVehicle, ushort otherID, ref Vehicle otherData, ref bool overlap, Vector3 min, Vector3 max) {
			Log.Error("CustomTrainAI.CheckOverlap (2) called.");
			return 0;
		}

		private static void InitializePath(ushort vehicleID, ref Vehicle vehicleData) {
			Log.Error("CustomTrainAI.InitializePath called");
		}
	}
}