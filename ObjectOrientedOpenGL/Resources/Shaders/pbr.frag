#version 450 core

in vec3 vWorldPos;
in vec3 vNormal;
in vec4 vTangent;
in vec2 vTexCoord;

out vec4 FragColor;

// Material properties
uniform vec4  uBaseColorFactor;
uniform float uMetallicFactor;
uniform float uRoughnessFactor;
uniform vec3  uEmissiveFactor;
uniform float uAlphaCutoff;
uniform int   uAlphaMode; // 0=Opaque, 1=Mask, 2=Blend
uniform int   uHasNormalMap;

uniform sampler2D uBaseColorMap;
uniform sampler2D uMetallicRoughnessMap;
uniform sampler2D uNormalMap;
uniform sampler2D uOcclusionMap;
uniform sampler2D uEmissiveMap;

// Camera & Lighting
uniform vec3 uCameraPos;
uniform vec3 uSunDir;
uniform vec3 uSunColor;
uniform float uSunIntensity;
uniform vec3 uAmbientSky;
uniform vec3 uAmbientGround;

const int MAX_POINT_LIGHTS = 4;
uniform int uPointLightCount;
uniform vec3 uPointLightPos[MAX_POINT_LIGHTS];
uniform vec3 uPointLightColor[MAX_POINT_LIGHTS];
uniform float uPointLightIntensity[MAX_POINT_LIGHTS];
uniform float uPointLightRange[MAX_POINT_LIGHTS];

const float PI = 3.14159265359;

float DistributionGGX(vec3 N, vec3 H, float roughness)
{
    float a = roughness * roughness;
    float a2 = a * a;
    float NdotH = max(dot(N, H), 0.0);
    float NdotH2 = NdotH * NdotH;
    float denom = (NdotH2 * (a2 - 1.0) + 1.0);
    return a2 / max(PI * denom * denom, 1e-6);
}

float GeometrySchlickGGX(float NdotV, float roughness)
{
    float r = roughness + 1.0;
    float k = (r * r) / 8.0;
    return NdotV / max(NdotV * (1.0 - k) + k, 1e-6);
}

float GeometrySmith(vec3 N, vec3 V, vec3 L, float roughness)
{
    return GeometrySchlickGGX(max(dot(N, V), 0.0), roughness) *
           GeometrySchlickGGX(max(dot(N, L), 0.0), roughness);
}

vec3 FresnelSchlick(float cosTheta, vec3 F0)
{
    return F0 + (1.0 - F0) * pow(clamp(1.0 - cosTheta, 0.0, 1.0), 5.0);
}

vec3 EvaluateLight(vec3 N, vec3 V, vec3 L, vec3 radiance, vec3 albedo, float metallic, float roughness, vec3 F0)
{
    vec3 H = normalize(V + L);
    float NDF = DistributionGGX(N, H, roughness);
    float G = GeometrySmith(N, V, L, roughness);
    vec3 F = FresnelSchlick(max(dot(H, V), 0.0), F0);

    vec3 specular = (NDF * G * F) / max(4.0 * max(dot(N, V), 0.0) * max(dot(N, L), 0.0), 1e-4);
    vec3 kD = (vec3(1.0) - F) * (1.0 - metallic);
    float NdotL = max(dot(N, L), 0.0);
    return (kD * albedo / PI + specular) * radiance * NdotL;
}

vec3 ACESFilm(vec3 x)
{
    float a = 2.51;
    float b = 0.03;
    float c = 2.43;
    float d = 0.59;
    float e = 0.14;
    return clamp((x * (a * x + b)) / (x * (c * x + d) + e), 0.0, 1.0);
}

void main()
{
    vec4 baseSample = texture(uBaseColorMap, vTexCoord);
    vec4 baseColor = vec4(pow(baseSample.rgb, vec3(2.2)), baseSample.a) * uBaseColorFactor;

    if (uAlphaMode == 1 && baseColor.a < uAlphaCutoff)
        discard;

    vec4 mrSample = texture(uMetallicRoughnessMap, vTexCoord);
    float metallic = clamp(mrSample.b * uMetallicFactor, 0.0, 1.0);
    float roughness = clamp(mrSample.g * uRoughnessFactor, 0.04, 1.0);
    float ao = texture(uOcclusionMap, vTexCoord).r;
    vec3 emissive = pow(texture(uEmissiveMap, vTexCoord).rgb, vec3(2.2)) * uEmissiveFactor;

    vec3 N = normalize(vNormal);
    if (!gl_FrontFacing) N = -N;
    if (uHasNormalMap != 0 && length(vTangent.xyz) > 0.001)
    {
        vec3 T = normalize(vTangent.xyz - dot(vTangent.xyz, N) * N);
        vec3 B = cross(N, T) * (vTangent.w < 0.0 ? -1.0 : 1.0);
        vec3 mapN = texture(uNormalMap, vTexCoord).xyz * 2.0 - 1.0;
        N = normalize(mat3(T, B, N) * mapN);
    }

    vec3 V = normalize(uCameraPos - vWorldPos);
    vec3 F0 = mix(vec3(0.04), baseColor.rgb, metallic);

    vec3 Lo = vec3(0.0);

    // Directional sun light
    if (uSunIntensity > 0.0)
    {
        vec3 L = normalize(uSunDir);
        Lo += EvaluateLight(N, V, L, uSunColor * uSunIntensity, baseColor.rgb, metallic, roughness, F0);
    }

    // Point lights
    for (int i = 0; i < uPointLightCount && i < MAX_POINT_LIGHTS; i++)
    {
        vec3 toLight = uPointLightPos[i] - vWorldPos;
        float dist = length(toLight);
        float range = max(uPointLightRange[i], 0.01);
        float atten = clamp(1.0 - (dist / range) * (dist / range), 0.0, 1.0);
        atten = (atten * atten) / max(dist * dist + 0.1, 0.1);
        vec3 L = toLight / max(dist, 1e-4);
        Lo += EvaluateLight(N, V, L, uPointLightColor[i] * uPointLightIntensity[i] * atten, baseColor.rgb, metallic, roughness, F0);
    }

    // Hemisphere ambient
    vec3 ambientColor = mix(uAmbientGround, uAmbientSky, N.y * 0.5 + 0.5);
    vec3 ambient = ambientColor * baseColor.rgb * ao * (1.0 - metallic * 0.6);

    vec3 color = ambient + Lo + emissive;
    color = ACESFilm(color);
    color = pow(color, vec3(1.0 / 2.2));

    FragColor = vec4(color, uAlphaMode == 2 ? baseColor.a : 1.0);
}
