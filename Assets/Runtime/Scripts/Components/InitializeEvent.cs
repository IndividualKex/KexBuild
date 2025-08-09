using Unity.Entities;
using UnityEngine;

namespace KexBuild {
    public class InitializeEvent : IComponentData {
        public LayerMask GroundLayerMask;
    }
}
