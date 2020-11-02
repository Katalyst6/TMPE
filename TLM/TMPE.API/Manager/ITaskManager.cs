using System;

namespace TrafficManager.API.Manager {
    public interface ITaskManager {
        void Queue(Action action);
    }
}
