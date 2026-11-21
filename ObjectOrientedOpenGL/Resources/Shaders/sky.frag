#version 430

uniform vec3 p_A, p_B, p_C, p_D, p_E, p_Z;

vec3 perez(float cos_theta, float gamma, float cos_gamma, vec3 A, vec3 B, vec3 C, vec3 D, vec3 E)
{
    return (1.0 + A * exp(B / (cos_theta + 0.01))) * (1.0 + C * exp(D * gamma) + E * cos_gamma * cos_gamma);
}

vec3 preetham_sky_rgb(vec3 v, vec3 sun_dir)
{
    float cos_theta = clamp(v.y, 0.0, 1.0);
    float cos_gamma = clamp(dot(v, sun_dir), -1.0, 1.0);
    float gamma = acos(cos_gamma);

    vec3 R_xyY = p_Z * perez(cos_theta, gamma, cos_gamma, p_A, p_B, p_C, p_D, p_E);
    vec3 R_XYZ = vec3(R_xyY.x, R_xyY.y, 1.0 - R_xyY.x - R_xyY.y) * R_xyY.z / max(R_xyY.y, 1e-4);

    float r = dot(vec3( 3.240479, -1.537150, -0.498535), R_XYZ);
    float g = dot(vec3(-0.969256,  1.875992,  0.041556), R_XYZ);
    float b = dot(vec3( 0.055648, -0.204043,  1.057311), R_XYZ);

    return max(vec3(r, g, b), vec3(0.0));
}

out vec4 PS_OUT_Color;
in vec3 PS_IN_TexCoord;
uniform vec3 SunDir;

void main()
{
    vec3 dir = normalize(PS_IN_TexCoord);
    vec3 sun = normalize(SunDir);

    // Evaluate Preetham with clamped elevation so it stays stable through twilight
    vec3 clampedSun = normalize(vec3(sun.x, max(sun.y, 0.02), sun.z));
    vec3 daySky = preetham_sky_rgb(dir, clampedSun);

    // Night sky gradient (dark indigo zenith -> navy horizon)
    float up = clamp(dir.y, 0.0, 1.0);
    vec3 nightSky = mix(vec3(0.025, 0.035, 0.065), vec3(0.008, 0.012, 0.028), pow(up, 0.55));

    // Warm twilight glow along horizon when sun is near 0 elevation
    float twilight = smoothstep(-0.18, 0.0, sun.y) * (1.0 - smoothstep(0.0, 0.22, sun.y));
    float towardSun = pow(max(dot(normalize(vec3(dir.x, 0.0, dir.z)), normalize(vec3(sun.x, 0.0, sun.z))), 0.0), 4.0);
    float horizonBand = exp(-abs(dir.y) * 6.0);
    vec3 twilightGlow = vec3(0.75, 0.28, 0.10) * twilight * towardSun * horizonBand;

    // Smooth day/night blend
    float dayBlend = smoothstep(-0.12, 0.14, sun.y);
    vec3 col = mix(nightSky, daySky, dayBlend) + twilightGlow;

    // Visible sun disc only while above the horizon
    if (sun.y > -0.02 && dir.y > -0.02)
    {
        float sunCos = dot(dir, sun);
        float disc = smoothstep(0.9992, 0.9997, sunCos) * smoothstep(-0.02, 0.05, sun.y);
        vec3 sunColor = mix(vec3(1.0, 0.45, 0.18), vec3(1.0, 0.98, 0.92), smoothstep(0.0, 0.25, sun.y));
        col += sunColor * disc * 3.0;
    }

    PS_OUT_Color = vec4(col, 1.0);
}
