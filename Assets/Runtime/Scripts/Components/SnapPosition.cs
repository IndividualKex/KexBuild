using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace KexBuild {
    public struct SnapPosition : IBufferElementData {
        public int3 Value;
        public byte Priority;

        public static implicit operator int3(SnapPosition position) => position.Value;
        public static implicit operator SnapPosition(int3 value) => new() { Value = value, Priority = 0 };

        public static implicit operator Vector3Int(SnapPosition position) =>
            new(position.Value.x, position.Value.y, position.Value.z);

        public static implicit operator SnapPosition(Vector3Int value) =>
            new() { Value = new int3(value.x, value.y, value.z), Priority = 0 };
    }
}
