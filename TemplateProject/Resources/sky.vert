#version 430

uniform mat4 InvViewProj;

out vec3 PS_IN_TexCoord;

void main(void)
{
     const vec3 vertices[4] = vec3[4](vec3(-1.0f, -1.0f, 1.0f),
                                      vec3( 1.0f, -1.0f, 1.0f),
                                      vec3(-1.0f,  1.0f, 1.0f),
                                      vec3( 1.0f,  1.0f, 1.0f));


    vec4 clip_pos = vec4(vertices[gl_VertexID].xy, -1.0, 1.0);
    vec4 view_pos  = InvViewProj * clip_pos;

    vec4 from = InvViewProj * vec4(vertices[gl_VertexID].xy, -1, 1);
    vec4 to = InvViewProj * vec4(vertices[gl_VertexID].xy, 1, 1);
    from /= from.w;
    to /= to.w;

    PS_IN_TexCoord = normalize(to.xyz - from.xyz);

    gl_Position = vec4(vertices[gl_VertexID], 1.0f);
}