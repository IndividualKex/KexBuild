using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine.InputSystem;

namespace KexBuild.UI {
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    public partial class BuildableSystem : SystemBase {
        private FixedString64Bytes[] _names;
        private Entity _current;
        private int _currentIndex = -1;

        protected override void OnCreate() {
            _names = new FixedString64Bytes[] {
                new("Floor"),
                new("Wall")
            };

            RequireForUpdate<PrefabsSingleton>();
            RequireForUpdate<SnapPointSettings>();
        }

        protected override void OnUpdate() {
            var definitionQuery = SystemAPI.QueryBuilder()
                .WithAll<BuildableDefinition>()
                .Build();
            int definitionCount = definitionQuery.CalculateEntityCount();
            using var map = new NativeHashMap<FixedString64Bytes, Entity>(definitionCount, Allocator.Temp);
            foreach (var (definition, entity) in SystemAPI.Query<BuildableDefinition>().WithEntityAccess()) {
                map.Add(definition.Name, entity);
            }

            var prefabs = SystemAPI.GetSingleton<PrefabsSingleton>();

            if (_current != Entity.Null &&
                !SystemAPI.HasComponent<Buildable>(_current)) {
                _current = Entity.Null;
            }

            if (Keyboard.current.yKey.wasPressedThisFrame) {
                ref var snapPointSettings = ref SystemAPI.GetSingletonRW<SnapPointSettings>().ValueRW;
                snapPointSettings.Mode = snapPointSettings.Mode switch {
                    SnapMode.Simple => SnapMode.Advanced,
                    SnapMode.Advanced => SnapMode.None,
                    SnapMode.None => SnapMode.Simple,
                    _ => SnapMode.Simple
                };
            }

            if (Keyboard.current.digit1Key.wasPressedThisFrame) {
                bool isCurrent = _currentIndex == 0;
                Clear();
                if (!isCurrent) {
                    _current = EntityManager.Instantiate(prefabs.Floor);
                    EntityManager.SetComponentData(_current, new Buildable {
                        Definition = map[_names[0]],
                        ResolvedTargetPosition = float3.zero,
                        ResolvedTargetRotation = quaternion.identity,
                        VerticalOffset = 0,
                        DepthOffset = 0,
                    });
                    ref var transform = ref SystemAPI.GetComponentRW<LocalTransform>(_current).ValueRW;
                    transform.Position = new float3(0, -999f, 0);
                    _currentIndex = 0;
                }
            }

            if (Keyboard.current.digit2Key.wasPressedThisFrame) {
                bool isCurrent = _currentIndex == 1;
                Clear();
                if (!isCurrent) {
                    _current = EntityManager.Instantiate(prefabs.Wall);
                    EntityManager.SetComponentData(_current, new Buildable {
                        Definition = map[_names[1]],
                        ResolvedTargetPosition = float3.zero,
                        ResolvedTargetRotation = quaternion.identity,
                        VerticalOffset = 0,
                        DepthOffset = 0,
                    });
                    ref var transform = ref SystemAPI.GetComponentRW<LocalTransform>(_current).ValueRW;
                    transform.Position = new float3(0, -999f, 0);
                    _currentIndex = 1;
                }
            }

            var scrollDelta = Mouse.current.scroll.ReadValue().y;
            if (scrollDelta != 0f) {
                bool ctrlHeld = Keyboard.current.leftCtrlKey.isPressed || Keyboard.current.rightCtrlKey.isPressed;
                bool altHeld = Keyboard.current.leftAltKey.isPressed || Keyboard.current.rightAltKey.isPressed;

                foreach (var buildableRW in SystemAPI.Query<RefRW<Buildable>>()) {
                    ref var buildable = ref buildableRW.ValueRW;

                    if (ctrlHeld) {
                        buildable.VerticalOffset += scrollDelta > 0 ? 1 : -1;
                        buildable.VerticalOffset = math.clamp(buildable.VerticalOffset, -10, 10);
                    }
                    else if (altHeld) {
                        buildable.DepthOffset += scrollDelta > 0 ? 1 : -1;
                        buildable.DepthOffset = math.clamp(buildable.DepthOffset, -10, 10);
                    }
                    else {
                        buildable.TargetYaw += scrollDelta > 0 ? 15f : -15f;

                        while (buildable.TargetYaw < 0f) {
                            buildable.TargetYaw += 360f;
                        }
                        while (buildable.TargetYaw >= 360f) {
                            buildable.TargetYaw -= 360f;
                        }
                    }
                }
            }

            if (Mouse.current.leftButton.wasPressedThisFrame &&
                _current != Entity.Null) {
                var requestEntity = EntityManager.CreateEntity();
                EntityManager.AddComponentData(requestEntity, new PlacementRequest {
                    BuildableEntity = _current
                });
                _current = Entity.Null;
                _currentIndex = -1;
            }
            else if (Mouse.current.rightButton.wasPressedThisFrame) {
                Clear();
            }
        }

        private void Clear() {
            if (_current != Entity.Null) {
                EntityManager.DestroyEntity(_current);
                _current = Entity.Null;
                _currentIndex = -1;
            }
        }
    }
}
