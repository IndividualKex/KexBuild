using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;

namespace KexBuild {
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    public partial class SnapPointRenderSystem : SystemBase {
        private const int MAX_SNAP_POINTS = 2048;
        private const float MAX_RENDER_DISTANCE = 50f;
        private const float UNIFORM_SIZE = 0.25f;

        private GraphicsBuffer _matricesBuffer;
        private GraphicsBuffer _visualizationBuffer;
        private GraphicsBuffer _argsBuffer;

        private NativeList<float4x4> _matrices;
        private NativeList<float4> _visualizationData;

        private Mesh _quadMesh;

        protected override void OnCreate() {
            _matricesBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, MAX_SNAP_POINTS, sizeof(float) * 16);
            _visualizationBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, MAX_SNAP_POINTS, sizeof(float) * 4);
            _argsBuffer = new GraphicsBuffer(GraphicsBuffer.Target.IndirectArguments, 5, sizeof(uint));

            _matrices = new NativeList<float4x4>(MAX_SNAP_POINTS, Allocator.Persistent);
            _visualizationData = new NativeList<float4>(MAX_SNAP_POINTS, Allocator.Persistent);

            CreateQuadMesh();

            RequireForUpdate<GlobalSettings>();
            RequireForUpdate<SnapSettings>();
        }

        protected override void OnDestroy() {
            _matricesBuffer?.Dispose();
            _visualizationBuffer?.Dispose();
            _argsBuffer?.Dispose();

            if (_matrices.IsCreated) _matrices.Dispose();
            if (_visualizationData.IsCreated) _visualizationData.Dispose();

            if (_quadMesh != null) Object.DestroyImmediate(_quadMesh);
        }

        protected override void OnUpdate() {
            var settings = SystemAPI.ManagedAPI.GetSingleton<GlobalSettings>();
            if (settings.SnapPointMaterial == null || _quadMesh == null) return;

            _matrices.Clear();
            _visualizationData.Clear();

            var snapPointSettings = SystemAPI.GetSingleton<SnapSettings>();

            if (snapPointSettings.Mode == SnapMode.None) return;

            var buildableQuery = SystemAPI.QueryBuilder().WithAll<Buildable>().Build();
            var placedQuery = SystemAPI.QueryBuilder().WithAll<PlacedBuildable>().Build();

            int buildableCount = buildableQuery.CalculateEntityCount();
            int placedCount = placedQuery.CalculateEntityCount();

            if (buildableCount == 0) return;

            var buildables = new NativeArray<SnapPointGatherJob.BuildableData>(buildableCount, Allocator.TempJob);
            var placedBuildables = new NativeArray<SnapPointGatherJob.PlacedBuildableData>(placedCount, Allocator.TempJob);

            int buildableIdx = 0;
            foreach (var buildable in SystemAPI.Query<Buildable>()) {
                buildables[buildableIdx++] = new SnapPointGatherJob.BuildableData {
                    definition = buildable.Definition,
                    position = buildable.TargetPosition,
                    yaw = buildable.TargetYaw
                };
            }

            int placedIdx = 0;
            foreach (var placed in SystemAPI.Query<PlacedBuildable>()) {
                placedBuildables[placedIdx++] = new SnapPointGatherJob.PlacedBuildableData {
                    definition = placed.Definition,
                    position = placed.Position,
                    yaw = placed.Yaw
                };
            }

            new SnapPointGatherJob {
                Buildables = buildables,
                PlacedBuildables = placedBuildables,
                SnapLookup = SystemAPI.GetBufferLookup<SnapPosition>(true),
                SnapMode = snapPointSettings.Mode,
                GridSize = snapPointSettings.GridSize,
                Matrices = _matrices,
                VisualizationData = _visualizationData,
            }.Run();

            buildables.Dispose();
            placedBuildables.Dispose();

            if (_matrices.Length > 0) {
                RenderSnapPoints(settings.SnapPointMaterial);
            }
        }

        private void RenderSnapPoints(Material material) {
            if (_matrices.Length == 0) return;

            int count = math.min(_matrices.Length, MAX_SNAP_POINTS);

            _matricesBuffer.SetData(_matrices.AsArray(), 0, 0, count);
            _visualizationBuffer.SetData(_visualizationData.AsArray(), 0, 0, count);

            var matProps = new MaterialPropertyBlock();
            matProps.SetBuffer("_Matrices", _matricesBuffer);
            matProps.SetBuffer("_VisualizationData", _visualizationBuffer);

            var bounds = new Bounds(Vector3.zero, 2 * MAX_RENDER_DISTANCE * Vector3.one);
            var renderParams = new RenderParams(material) {
                worldBounds = bounds,
                matProps = matProps,
                shadowCastingMode = ShadowCastingMode.Off,
                receiveShadows = false
            };

            var args = new uint[5] { _quadMesh.GetIndexCount(0), (uint)count, 0, 0, 0 };
            _argsBuffer.SetData(args);

            Graphics.RenderMeshIndirect(renderParams, _quadMesh, _argsBuffer);
        }

        private void CreateQuadMesh() {
            _quadMesh = new Mesh {
                name = "SnapPointQuad"
            };

            var vertices = new Vector3[] {
                new(-0.5f, -0.5f, 0),
                new(0.5f, -0.5f, 0),
                new(0.5f, 0.5f, 0),
                new(-0.5f, 0.5f, 0)
            };

            var uvs = new Vector2[] {
                new(0, 0),
                new(1, 0),
                new(1, 1),
                new(0, 1)
            };

            var indices = new int[] { 0, 1, 2, 0, 2, 3 };

            _quadMesh.vertices = vertices;
            _quadMesh.uv = uvs;
            _quadMesh.SetIndices(indices, MeshTopology.Triangles, 0);
            _quadMesh.bounds = new Bounds(Vector3.zero, Vector3.one * 100f);
        }

        [BurstCompile]
        private struct SnapPointGatherJob : IJob {
            [ReadOnly] public NativeArray<BuildableData> Buildables;
            [ReadOnly] public NativeArray<PlacedBuildableData> PlacedBuildables;
            [ReadOnly] public BufferLookup<SnapPosition> SnapLookup;
            public SnapMode SnapMode;
            public float GridSize;
            public NativeList<float4x4> Matrices;
            public NativeList<float4> VisualizationData;

            public struct BuildableData {
                public Entity definition;
                public float3 position;
                public float yaw;
            }

            public struct PlacedBuildableData {
                public Entity definition;
                public float3 position;
                public float yaw;
            }

            private const float ALIGNMENT_THRESHOLD = 0.01f;
            private const float NEARBY_DISTANCE = 5.0f;
            private const float UNIFORM_SIZE = 0.25f;
            private const int MAX_SNAP_POINTS = 2048;

            public void Execute() {
                var alignedPoints = new NativeHashSet<float3>(256, Allocator.Temp);
                var buildableSnapPoints = new NativeList<float3>(256, Allocator.Temp);
                var buildablePositions = new NativeList<float3>(32, Allocator.Temp);

                for (int idx = 0; idx < Buildables.Length; idx++) {
                    var buildable = Buildables[idx];
                    if (buildable.definition == Entity.Null || !SnapLookup.HasBuffer(buildable.definition)) continue;

                    buildablePositions.Add(buildable.position);
                    var buf = SnapLookup[buildable.definition];
                    quaternion yaw = quaternion.RotateY(math.radians(buildable.yaw));

                    for (int i = 0; i < buf.Length; i++) {
                        var snapPoint = buf[i];
                        int3 cell = snapPoint.Value;
                        byte priority = snapPoint.Priority;

                        if (SnapMode == SnapMode.Simple && priority != 1) continue;

                        float3 local = new(cell.x * GridSize, cell.y * GridSize, cell.z * GridSize);
                        float3 world = buildable.position + math.rotate(yaw, local);
                        buildableSnapPoints.Add(world);
                    }
                }

                for (int pidx = 0; pidx < PlacedBuildables.Length; pidx++) {
                    var placed = PlacedBuildables[pidx];
                    if (placed.definition == Entity.Null || !SnapLookup.HasBuffer(placed.definition)) continue;

                    bool isNearBuildable = false;
                    for (int b = 0; b < buildablePositions.Length; b++) {
                        if (math.distance(placed.position, buildablePositions[b]) < NEARBY_DISTANCE) {
                            isNearBuildable = true;
                            break;
                        }
                    }

                    if (!isNearBuildable) continue;

                    var buf = SnapLookup[placed.definition];
                    quaternion yaw = quaternion.RotateY(math.radians(placed.yaw));

                    for (int i = 0; i < buf.Length; i++) {
                        var snapPoint = buf[i];
                        int3 cell = snapPoint.Value;
                        byte priority = snapPoint.Priority;

                        if (SnapMode == SnapMode.Simple && priority != 1) continue;

                        float3 local = new(cell.x * GridSize, cell.y * GridSize, cell.z * GridSize);
                        float3 placedWorld = placed.position + math.rotate(yaw, local);

                        for (int j = 0; j < buildableSnapPoints.Length; j++) {
                            if (math.distance(placedWorld, buildableSnapPoints[j]) < ALIGNMENT_THRESHOLD) {
                                alignedPoints.Add(placedWorld);
                                alignedPoints.Add(buildableSnapPoints[j]);
                            }
                        }
                    }
                }

                for (int idx = 0; idx < Buildables.Length; idx++) {
                    var buildable = Buildables[idx];
                    if (buildable.definition == Entity.Null || !SnapLookup.HasBuffer(buildable.definition)) continue;

                    var buf = SnapLookup[buildable.definition];
                    quaternion yaw = quaternion.RotateY(math.radians(buildable.yaw));

                    for (int i = 0; i < buf.Length; i++) {
                        var snapPoint = buf[i];
                        int3 cell = snapPoint.Value;
                        byte priority = snapPoint.Priority;

                        if (SnapMode == SnapMode.Simple && priority != 1) continue;

                        float3 local = new(cell.x * GridSize, cell.y * GridSize, cell.z * GridSize);
                        float3 world = buildable.position + math.rotate(yaw, local);

                        bool isAligned = alignedPoints.Contains(world);
                        bool isPrimary = priority == 1;

                        AddSnapPoint(world, isAligned, isPrimary);
                    }
                }

                for (int pidx = 0; pidx < PlacedBuildables.Length; pidx++) {
                    var placed = PlacedBuildables[pidx];
                    if (placed.definition == Entity.Null || !SnapLookup.HasBuffer(placed.definition)) continue;

                    bool isNearBuildable = false;
                    for (int b = 0; b < buildablePositions.Length; b++) {
                        if (math.distance(placed.position, buildablePositions[b]) < NEARBY_DISTANCE) {
                            isNearBuildable = true;
                            break;
                        }
                    }

                    if (!isNearBuildable) continue;

                    var buf = SnapLookup[placed.definition];
                    quaternion yaw = quaternion.RotateY(math.radians(placed.yaw));

                    for (int i = 0; i < buf.Length; i++) {
                        var snapPoint = buf[i];
                        int3 cell = snapPoint.Value;
                        byte priority = snapPoint.Priority;

                        if (SnapMode == SnapMode.Simple && priority != 1) continue;

                        float3 local = new(cell.x * GridSize, cell.y * GridSize, cell.z * GridSize);
                        float3 world = placed.position + math.rotate(yaw, local);

                        bool isAligned = alignedPoints.Contains(world);
                        bool isPrimary = priority == 1;

                        AddSnapPoint(world, isAligned, isPrimary);
                    }
                }

                alignedPoints.Dispose();
                buildableSnapPoints.Dispose();
                buildablePositions.Dispose();
            }

            private void AddSnapPoint(float3 position, bool isAligned, bool isPrimary) {
                if (Matrices.Length >= MAX_SNAP_POINTS) return;

                var matrix = float4x4.TRS(position, quaternion.identity, new float3(UNIFORM_SIZE, UNIFORM_SIZE, 1));

                var data = new float4(
                    isAligned ? 1.0f : 0.0f,
                    isPrimary ? 1.0f : 0.0f,
                    0.0f,
                    0.0f
                );

                Matrices.Add(matrix);
                VisualizationData.Add(data);
            }
        }
    }
}
