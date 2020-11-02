using System.Collections.Generic;

namespace TrafficManager.API.Manager {
    public interface IParkingManager {
        void QueueParkedVehicleCheckups(IEnumerable<ushort> parkingIds);
    }
}