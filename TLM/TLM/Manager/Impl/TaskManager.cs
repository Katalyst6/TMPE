using System;
using System.Collections.Generic;
using System.Threading;
using TrafficManager.API.Manager;

namespace TrafficManager.Manager.Impl {
    public class TaskManager : ITaskManager {
        public static readonly ITaskManager Instance = new TaskManager();

        private readonly IDictionary<Guid, Thread> _activeTasks = new Dictionary<Guid, Thread>();

        public void Queue(Action action) {

            var taskId = Guid.NewGuid();

            var thread = new Thread(() => {
                action();
                _activeTasks.Remove(taskId);
            }) {
                Name = "Task",
                Priority = ThreadPriority.Lowest,
            };

            _activeTasks[taskId] = thread;
            thread.Start();
        }
    }
}
