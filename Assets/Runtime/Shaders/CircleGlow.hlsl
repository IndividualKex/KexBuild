void CircleGlow_float(
    float4 visualizationData,
    float2 uv,
    out float3 Color,
    out float Alpha
) {
    float2 center = float2(0.5, 0.5);
    float dist = distance(uv, center);
    
    bool isAligned = visualizationData.x > 0.5;
    
    float3 unalignedColor = float3(1.0, 1.0, 0.0);
    
    float targetRadius = 0.25;
    float ringThickness = 0.05;
    
    float ringDistance = abs(dist - targetRadius);
    float ringMask = 1.0 - smoothstep(0.0, ringThickness * 0.5, ringDistance);
    
    float ringIntensity = ringMask * 1.2;
    float ringAlpha = ringMask * 0.4;
    
    Color = unalignedColor * ringIntensity;
    Alpha = ringAlpha;
    
    if (isAligned) {
        float3 alignedColor = float3(0.2, 0.5, 1.0);
        
        float innerRadius = 0.08;
        float outerRadius = 0.35;
        
        float innerCircle = 1.0 - smoothstep(0.0, innerRadius, dist);
        float outerGlow = 1.0 - smoothstep(innerRadius, outerRadius, dist);
        
        float innerIntensity = innerCircle * 6.0;
        float outerIntensity = outerGlow * 0.8;
        
        float innerAlpha = innerCircle;
        float outerAlpha = outerGlow * 0.25;
        
        float3 glowColor = alignedColor * (innerIntensity + outerIntensity);
        float glowAlpha = saturate(innerAlpha + outerAlpha);
        
        Color += glowColor;
        Alpha = saturate(Alpha + glowAlpha);
    }
}
