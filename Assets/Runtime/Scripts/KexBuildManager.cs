using Unity.Entities;
using Unity.Collections;
using UnityEngine;

namespace KexBuild {
    public class KexBuildManager : MonoBehaviour {
        public LayerMask GroundLayerMask;
        public float GridSize = 0.5f;

        private void Start() {
            var entityManager = World.DefaultGameObjectInjectionWorld.EntityManager;
            using var ecb = new EntityCommandBuffer(Allocator.Temp);
            var entity = ecb.CreateEntity();
            ecb.AddComponent(entity, new InitializeEvent {
                GroundLayerMask = GroundLayerMask,
                GridSize = GridSize,
            });
            ecb.Playback(entityManager);
        }
    }
}
