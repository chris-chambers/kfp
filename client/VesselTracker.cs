using System;
using System.Collections.Generic;

using UnityEngine;

namespace Kfp
{
    class VesselTracker
    {
        private readonly IConnection _conn;
        private readonly Dictionary<Guid, VesselStatus> _vessels;

        public VesselTracker(IConnection conn) {
            if (conn == null) {
                throw new ArgumentNullException("conn");
            }

            _conn = conn;
            _vessels = new Dictionary<Guid, VesselStatus>();
        }

        public void Update()
        {
            if (FlightGlobals.fetch == null || FlightGlobals.Vessels == null) {
                return;
            }

            var toRemove = new HashSet<Guid>(_vessels.Keys);
            foreach (var vessel in FlightGlobals.Vessels) {
                toRemove.Remove(vessel.id);
                VesselStatus stat = VesselStatusExtensions.FromVessel(vessel);
                VesselStatus prevStat;
                Diff<VesselStatus> diff;
                if (!_vessels.TryGetValue(vessel.id, out prevStat)) {
                    _conn.SendDebug(
                        "> new vessel: {0} ({1})",
                        stat.Name, stat.Id);
                    diff = Diff.Create(null, stat);
                    // Debug.LogFormat("Name: {0}", diff.Item.Name);
                } else {
                    diff = Diff.Create(prevStat, stat);
                }

                diff.Apply(ref prevStat);
                _vessels[vessel.id] = prevStat;

                if (diff.Changed != 0) {
                    // Debug.LogFormat("kfp: acc: {0} {1}",
                    //                 prevInfo.acceleration,
                    //                 info.acceleration);
                    _conn.SendVesselUpdate(vessel.id, diff);
                }
            }

            foreach (var id in toRemove) {
                var info = _vessels[id];
                _vessels.Remove(id);
                _conn.SendDebug(
                    "< removed vessel: {0} ({1})",
                    info.Name, info.Id);
            }
        }
    }

    static class VesselStatusExtensions
    {
        internal static VesselStatus FromVessel(Vessel v)
        {
            return new VesselStatus {
                Id = v.id,
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
