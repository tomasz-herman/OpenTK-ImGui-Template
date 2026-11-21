#version 450 core

in vec2 vTexCoord;

uniform int uAlphaMode;
uniform float uAlphaCutoff;
uniform float uBaseColorAlpha;
uniform sampler2D uBaseColorMap;

void main()
{
    if (uAlphaMode == 1)
    {
        float alpha = texture(uBaseColorMap, vTexCoord).a * uBaseColorAlpha;
        if (alpha < uAlphaCutoff)
            discard;
    }
}
