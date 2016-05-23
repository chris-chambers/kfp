using System;

using UnityEngine;

namespace Kfp
{
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
    }
}
