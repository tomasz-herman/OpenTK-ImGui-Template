#version 330 core

uniform sampler2D fontTexture;

in VertexData {
    vec4 color;
    vec2 texCoord;
} fs_in;

out vec4 FragColor;

void main()
{
    FragColor = fs_in.color * texture(fontTexture, fs_in.texCoord);
}