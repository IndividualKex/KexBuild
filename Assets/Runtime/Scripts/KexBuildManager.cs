using Unity.Entities;
using Unity.Collections;
using UnityEngine;

namespace KexBuild {
    public class KexBuildManager : MonoBehaviour {
        public LayerMask GroundLayerMask;

        private void Start() {
            var entityManager = World.DefaultGameObjectInjectionWorld.EntityManager;
            using var ecb = new EntityCommandBuffer(Allocator.Temp);
            var entity = ecb.CreateEntity();
            ecb.AddComponent(entity, new InitializeEvent {
                GroundLayerMask = GroundLayerMask,
            });
            ecb.Playback(entityManager);
        }
    }
}
