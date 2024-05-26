#version 330 core

uniform vec4 viewport;
uniform mat4 invViewProj;
uniform sampler2D sampler;

out vec4 FragColor;

void main() {
    vec4 ndc;
    ndc.xy = ((2.0 * gl_FragCoord.xy) - (2.0 * viewport.xy)) / (viewport.zw) - 1;
    ndc.z = (2.0 * gl_FragCoord.z - gl_DepthRange.near - gl_DepthRange.far) / (gl_DepthRange.far - gl_DepthRange.near);
    ndc.w = 1.0;

    vec4 clip = invViewProj * ndc;
    vec3 world = (clip / clip.w).xyz;
    vec2 uv = fract(world.xz);

    FragColor = texture(sampler, uv);
}