using System;
using System.Collections.Generic;

using UnityEngine;

namespace Kfp
{
    class VesselTracker
    {
        private readonly IConnection _conn;
        private readonly Dictionary<Guid, History<VesselStatus>> _vessels;
        private readonly HashSet<Guid> _toRemove;

        public VesselTracker(IConnection conn) {
            if (conn == null) {
                throw new ArgumentNullException("conn");
            }

            _conn = conn;
            _vessels = new Dictionary<Guid, History<VesselStatus>>();
            _toRemove = new HashSet<Guid>();
        }

        public void Update() {
            if (FlightGlobals.fetch == null || FlightGlobals.Vessels == null) {
                return;
            }

            try {
                UpdateCore();
            } finally {
                _toRemove.Clear();
            }
        }

        private void UpdateCore() {
            foreach (var vessel in FlightGlobals.Vessels) {
                _toRemove.Remove(vessel.id);

                VesselStatus stat = VesselStatusExtensions.FromVessel(vessel);
                History<VesselStatus> history;
                if (!_vessels.TryGetValue(vessel.id, out history)) {
                    _conn.SendDebug(
                        "> new vessel: {0} ({1})",
                        stat.Name, vessel.id);

                    // TODO: Make history size configurable.
                    history = new History<VesselStatus>(10);
                    _vessels.Add(vessel.id, history);
                    history.Add(stat);
                } else {
                    var diff = Diff.Create(history.Current, stat);
                    if (diff.Changed != 0) {
                        stat = history.Current;
                        diff.Apply(ref stat);
                        history.Add(stat);
                        Debug.LogFormat(
                            "history size: {0} - {1}",
                            history.Count, vessel.id);
                    }
                }

                // FIXME: Figure out how/when to send an update again.

                // if (diff.Changed != 0) {
                //     // Debug.LogFormat("kfp: acc: {0} {1}",
                //     //                 prevInfo.acceleration,
                //     //                 info.acceleration);
                //     _conn.SendVesselUpdate(vessel.id, diff);
                // }
            }

            foreach (var id in _toRemove) {
                _vessels.Remove(id);
                _conn.SendDebug("< removed vessel: {0}", id);
            }
        }
    }

    static class VesselStatusExtensions
    {
        internal static VesselStatus FromVessel(Vessel v)
        {
            return new VesselStatus {
                Name = v.name,
                planetTime = Planetarium.GetUniversalTime(),
                bodyName = v.mainBody.name,
                rotation = v.srfRelRotation,
                angularVelocity = v.angularVelocity,
                velocity = v.srf_velocity,
                acceleration = v.acceleration,
                terrainNormal = v.terrainNormal,
            };
        }
    }
}
