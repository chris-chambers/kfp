using System;
using System.Collections.Generic;

using UnityEngine;

namespace Kfp
{
    class VesselTracker
    {
        private readonly Connection _conn;
        private readonly Dictionary<Guid, VesselStatus> _vessels;

        public VesselTracker(Connection conn) {
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
                VesselStatus stat = VesselStatus.FromVessel(vessel);
                VesselStatus prevStat;
                MagicDiff<VesselStatus> diff;
                if (!_vessels.TryGetValue(vessel.id, out prevStat)) {
                    _conn.SendDebug(
                        "> new vessel: {0} ({1})",
                        stat.Name, stat.Id);
                    diff = MagicDiff.Create(null, stat);
                    // Debug.LogFormat("Name: {0}", diff.Item.Name);
                } else {
                    diff = MagicDiff.Create(prevStat, stat);
                }

                diff.Apply(ref prevStat);
                _vessels[vessel.id] = prevStat;

                if (diff.Changed.Data != 0) {
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

    // TODO: Move to Common
    public struct VesselStatus
    {
        public Guid Id;

        [Magic(0)] public string Name;
        // [Magic(1)]
        public double planetTime;
        [Magic(2)] public string bodyName;
        [Magic(3)] public Quaternion rotation;
        [Magic(4)] public Vector3 angularVelocity;

        // public FlightCtrlState flightState;
        // public bool[] actiongroupControls;

        // public bool isSurfaceUpdate;

        //Orbital parameters
        // TODO: orbit
        public double[] orbit;

        //Surface parameters
        //Position = lat,long,alt,ground height.
        // TODO: position
        [Magic(5)] public Vector3d position;
        [Magic(6)] public Vector3d velocity;
        [Magic(7)] public Vector3d acceleration;
        [Magic(8)] public Vector3 terrainNormal;

        public static VesselStatus FromVessel(Vessel v)
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
