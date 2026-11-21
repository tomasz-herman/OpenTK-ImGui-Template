#version 450 core

uniform vec4 viewport;
uniform mat4 invViewProj;
uniform sampler2D sampler;
uniform sampler2D uShadowMap;
uniform mat4 uLightSpaceMatrix;
uniform int uShadowsEnabled;
uniform vec3 uSunDir;

out vec4 FragColor;

float ComputeShadow(vec3 worldPos, vec3 L)
{
    if (uShadowsEnabled == 0 || L.y <= 0.0)
        return 0.0;

    vec4 lightSpacePos = uLightSpaceMatrix * vec4(worldPos, 1.0);
    vec3 projCoords = lightSpacePos.xyz / max(lightSpacePos.w, 1e-6);
    projCoords = projCoords * 0.5 + 0.5;
    if (projCoords.z > 1.0 || projCoords.x < 0.0 || projCoords.x > 1.0 || projCoords.y < 0.0 || projCoords.y > 1.0)
        return 0.0;

    float bias = max(0.002 * (1.0 - max(L.y, 0.0)), 0.0004);
    vec2 texelSize = 1.0 / vec2(textureSize(uShadowMap, 0));

    float shadow = 0.0;
    for (int x = -1; x <= 1; ++x)
    {
        for (int y = -1; y <= 1; ++y)
        {
            float pcfDepth = texture(uShadowMap, projCoords.xy + vec2(x, y) * texelSize).r;
            shadow += (projCoords.z - bias > pcfDepth) ? 1.0 : 0.0;
        }
    }
    return shadow / 9.0;
}

void main()
{
    vec4 ndc;
    ndc.xy = ((2.0 * gl_FragCoord.xy) - (2.0 * viewport.xy)) / (viewport.zw) - 1.0;
    ndc.z = (2.0 * gl_FragCoord.z - gl_DepthRange.near - gl_DepthRange.far) / (gl_DepthRange.far - gl_DepthRange.near);
    ndc.w = 1.0;

    vec4 clip = invViewProj * ndc;
    vec3 world = (clip / clip.w).xyz;
    vec2 uv = fract(world.xz);

    vec4 base = texture(sampler, uv);
    vec3 L = normalize(uSunDir);
    float shadow = ComputeShadow(world, L);
    float sunFactor = clamp(L.y * 1.4, 0.15, 1.0);
    vec3 lit = base.rgb * sunFactor * (1.0 - shadow * 0.65);

    FragColor = vec4(lit, base.a);
}
