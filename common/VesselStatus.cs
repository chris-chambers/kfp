using System;

using UnityEngine;

namespace Kfp
{
    public struct VesselStatus
    {
        [Diff(0)] public string Name;
        // [Magic(1)]
        public double planetTime;
        [Diff(2)] public string bodyName;
        [Diff(3)] public Quaternion rotation;
        [Diff(4)] public Vector3 angularVelocity;

        // public FlightCtrlState flightState;
        // public bool[] actiongroupControls;

        // public bool isSurfaceUpdate;

        //Orbital parameters
        // TODO: orbit
        public double[] orbit;

        //Surface parameters
        //Position = lat,long,alt,ground height.
        // TODO: position
        [Diff(5)] public Vector3d position;
        [Diff(6)] public Vector3d velocity;
        [Diff(7)] public Vector3d acceleration;
        [Diff(8)] public Vector3 terrainNormal;
    }
}
