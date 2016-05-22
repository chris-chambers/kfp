using System;
using System.Collections.Generic;
using System.Net;

using UnityEngine;

namespace Kfp
{
    [KSPAddon(KSPAddon.Startup.Instantly, true)]
    public class KfpClient : MonoBehaviour
    {
        private Connection _conn;
        private VesselTracker _vesselTracker;

        private void Start() {
            DontDestroyOnLoad(this);
            Debug.LogFormat("kfp: Start");

            var serverEndPoint = new IPEndPoint(IPAddress.Parse("127.0.0.1"), 6754);
            _conn = new Connection(serverEndPoint);
            _vesselTracker = new VesselTracker(_conn);

            // GameEvents.onCrewBoardVessel.Add(OnCrewBoard);
            // GameEvents.OnScienceChanged.Add(OnScienceChanged);
        }

        private void Update() {
            _vesselTracker.Update();
        }

        private void OnDestroy() {
            Debug.LogFormat("kfp: OnDestroy");

            _conn.Dispose();
            _conn = null;

            _vesselTracker = null;

            // GameEvents.onCrewBoardVessel.Remove(OnCrewBoard);
            // GameEvents.OnScienceChanged.Remove(OnScienceChanged);

            Destroy(gameObject);
        }

        private void OnScienceChanged(float amount, TransactionReasons reasons) {
            Debug.LogFormat("kfp: Science Changed: {0} {1}", amount, reasons);
        }

        private void OnCrewBoard(GameEvents.FromToAction<Part, Part> partAction) {
            Debug.LogFormat("kfp: EVA Boarding, from: {0}, name: {1}",
                            partAction.from.vessel.id,
                            partAction.from.vessel.vesselName);
            Debug.LogFormat("kfp: EVA Boarding, to: {0}, name: {1}",
                            partAction.to.vessel.id,
                            partAction.to.vessel.vesselName);
        }
    }
}
