StructuredBuffer<float4x4> _Matrices;
StructuredBuffer<float4> _VisualizationData;

void TransformBillboard_float(
    float instanceID,
    float3 pos,
    float3 normal,
    out float3 Position,
    out float3 Normal,
    out float4 VisualizationData
) {
    uint id = (uint)instanceID;

    float4x4 mat = _Matrices[id];
    
    float3 center = float3(mat[0][3], mat[1][3], mat[2][3]);
    float2 scale = float2(length(mat[0].xyz), length(mat[1].xyz));
    
    float3 forward = normalize(_WorldSpaceCameraPos - center);
    float3 up = float3(0, 1, 0);
    float3 right = normalize(cross(up, forward));
    up = cross(forward, right);
    
    float3 worldPos = center + (pos.x * right * scale.x + pos.y * up * scale.y);
    
    #if defined(DOTS_INSTANCING_ON)
        float4x4 worldToObject = transpose(UNITY_MATRIX_M);
    #else
        float4x4 worldToObject = unity_WorldToObject;
    #endif
    
    float4 objectPos = mul(worldToObject, float4(worldPos, 1.0));
    Position = objectPos.xyz;
    
    float3 objectNormal = mul((float3x3)worldToObject, forward);
    Normal = normalize(objectNormal);
    
    VisualizationData = _VisualizationData[id];
}
