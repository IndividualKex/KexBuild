using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace KexBuild.UI {
    public class BuildableDefinitionAuthoring : MonoBehaviour {
        public string Name;
        public Vector3 Size;
        public Vector3 Center;
        public SnapPointData[] SnapPoints;

        private class Baker : Baker<BuildableDefinitionAuthoring> {
            public override void Bake(BuildableDefinitionAuthoring authoring) {
                var entity = GetEntity(TransformUsageFlags.Dynamic);

                FixedString64Bytes name = default;
                if (!string.IsNullOrEmpty(authoring.Name)) {
                    name = new FixedString64Bytes(authoring.Name);
                }

                AddComponent(entity, new BuildableDefinition {
                    Name = name,
                    Size = authoring.Size,
                    Center = authoring.Center
                });

                AddBuffer<SnapPosition>(entity);
                if (authoring.SnapPoints != null) {
                    for (int i = 0; i < authoring.SnapPoints.Length; i++) {
                        var snapPoint = authoring.SnapPoints[i];
                        AppendToBuffer<SnapPosition>(entity, new SnapPosition {
                            Value = new int3(snapPoint.Position.x, snapPoint.Position.y, snapPoint.Position.z),
                            Priority = (byte)(snapPoint.IsPrimary ? 1 : 0)
                        });
                    }
                }
            }
        }

        [System.Serializable]
        public struct SnapPointData {
            public Vector3Int Position;
            public bool IsPrimary;
        }
    }
}
