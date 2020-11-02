namespace TrafficManager.UI.MainMenu {
    using TrafficManager.Custom.PathFinding;
    using TrafficManager.Manager.Impl;
    using UnityEngine;

    public class StatsLabel : U.Label.ULabel {

        public override void Start() {
            base.Start();
            this.text = string.Empty;
            this.textColor = Color.green;
        }

#if QUEUEDSTATS
        public override void Update() {
            var pathfinds = CustomPathManager.TotalQueuedPathFinds;
            var parkingCheckups = ParkingManager.QueuedCheckups;

            if (pathfinds < 1000 && parkingCheckups < 1000) {
                textColor = Color.Lerp(Color.green, Color.yellow, pathfinds / 1000f);
            } else if (pathfinds < 2500 && parkingCheckups < 2500) {
                textColor = Color.Lerp(
                    Color.yellow,
                    Color.red,
                    (pathfinds - 1000f) / 1500f);
            } else {
                textColor = Color.red;
            }

            text = $"{pathfinds} pathfinds; {parkingCheckups} parking checkups";
        }
#endif
    }
}