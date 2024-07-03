#version 330 core

uniform mat4 projectionMatrix;

layout(location = 0) in vec2 position;
layout(location = 1) in vec2 texCoord;
layout(location = 2) in vec4 color;

out VertexData {
    vec4 color;
    vec2 texCoord;
} vs_out;

void main()
{
    gl_Position = projectionMatrix * vec4(position, 0, 1);
    vs_out.color = color;
    vs_out.texCoord = texCoord;
}