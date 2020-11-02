namespace TrafficManager.Manager.Impl {
    using System.Collections.Generic;
    using System.Threading;
    using TrafficManager.API.Manager;

    public class ParkingManager
        : IParkingManager {

        private readonly object _parkedVehicleCheckupLock;
        private readonly HashSet<ushort> _parkedVehicleCheckupSet;
        private readonly Queue<ushort> _parkedVehicleCheckupQueue;
        private readonly Thread _parkedVehicleCheckupThread;

        private static long _queuedCheckups = 0;
        public static long QueuedCheckups => _queuedCheckups;

        public static readonly ParkingManager Instance
            = new ParkingManager();

        public ParkingManager() {
            _parkedVehicleCheckupLock = new object();
            _parkedVehicleCheckupSet = new HashSet<ushort>();
            _parkedVehicleCheckupQueue = new Queue<ushort>();
            _parkedVehicleCheckupThread = new Thread(ParkedVehicleCheckups);
            _parkedVehicleCheckupThread.Name = "ParkedVehicleCheckups";
            _parkedVehicleCheckupThread.Priority = ThreadPriority.Lowest;
            _parkedVehicleCheckupThread.Start();
        }

        public void QueueParkedVehicleCheckups(IEnumerable<ushort> parkingIds) {
            lock (_parkedVehicleCheckupLock) {
                foreach (var id in parkingIds) {
                    if (!_parkedVehicleCheckupSet.Contains(id)) {
                        _parkedVehicleCheckupQueue.Enqueue(id);
                        _parkedVehicleCheckupSet.Add(id);
                        Interlocked.Increment(ref _queuedCheckups);
                    }
                }
            }
        }

        public void ParkedVehicleCheckups() {
            while (true) {
                ushort? parkedVehicleId = null;

                lock (_parkedVehicleCheckupLock) {
                    if (_parkedVehicleCheckupQueue.Count > 0) {
                        parkedVehicleId = _parkedVehicleCheckupQueue.Dequeue();
                        _parkedVehicleCheckupSet.Remove(parkedVehicleId.Value);
                        Interlocked.Decrement(ref _queuedCheckups);
                    }
                }

                if (parkedVehicleId == null) {
                    Thread.Sleep(500);
                    continue;
                }

                var vehicleManager = VehicleManager.instance;
                var parkedVehicle = vehicleManager.m_parkedVehicles.m_buffer[parkedVehicleId.Value];
                var info = parkedVehicle.Info;
                info.m_vehicleAI.UpdateParkedVehicle(parkedVehicleId.Value, ref vehicleManager.m_parkedVehicles.m_buffer[parkedVehicleId.Value]);
            }
        }
    }
}
