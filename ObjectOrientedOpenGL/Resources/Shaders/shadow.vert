#version 450 core

layout(location = 0) in vec3 aPosition;
layout(location = 3) in vec2 aTexCoord;
layout(location = 4) in vec4 aJoints;
layout(location = 5) in vec4 aWeights;

const int MAX_JOINTS = 128;
uniform mat4 uLightSpaceMatrix;
uniform mat4 uModel;
uniform int uHasSkinning;
uniform mat4 uJointMatrices[MAX_JOINTS];

out vec2 vTexCoord;

void main()
{
    vec4 localPos = vec4(aPosition, 1.0);
    if (uHasSkinning != 0)
    {
        mat4 skinMat =
            aWeights.x * uJointMatrices[clamp(int(aJoints.x), 0, MAX_JOINTS - 1)] +
            aWeights.y * uJointMatrices[clamp(int(aJoints.y), 0, MAX_JOINTS - 1)] +
            aWeights.z * uJointMatrices[clamp(int(aJoints.z), 0, MAX_JOINTS - 1)] +
            aWeights.w * uJointMatrices[clamp(int(aJoints.w), 0, MAX_JOINTS - 1)];
        localPos = skinMat * localPos;
    }

    vTexCoord = aTexCoord;
    gl_Position = uLightSpaceMatrix * uModel * localPos;
}
