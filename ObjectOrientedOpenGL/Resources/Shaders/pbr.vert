#version 450 core

layout(location = 0) in vec3 aPosition;
layout(location = 1) in vec3 aNormal;
layout(location = 2) in vec4 aTangent;
layout(location = 3) in vec2 aTexCoord;
layout(location = 4) in vec4 aJoints;
layout(location = 5) in vec4 aWeights;

const int MAX_JOINTS = 128;
uniform mat4 uModel;
uniform mat4 uViewProj;
uniform mat3 uNormalMatrix;
uniform mat4 uLightSpaceMatrix;
uniform int uHasSkinning;
uniform mat4 uJointMatrices[MAX_JOINTS];

out vec3 vWorldPos;
out vec3 vNormal;
out vec4 vTangent;
out vec2 vTexCoord;
out vec4 vLightSpacePos;

void main()
{
    vec4 localPos = vec4(aPosition, 1.0);
    vec3 localNormal = aNormal;
    vec3 localTangent = aTangent.xyz;

    if (uHasSkinning != 0)
    {
        mat4 skinMat =
            aWeights.x * uJointMatrices[clamp(int(aJoints.x), 0, MAX_JOINTS - 1)] +
            aWeights.y * uJointMatrices[clamp(int(aJoints.y), 0, MAX_JOINTS - 1)] +
            aWeights.z * uJointMatrices[clamp(int(aJoints.z), 0, MAX_JOINTS - 1)] +
            aWeights.w * uJointMatrices[clamp(int(aJoints.w), 0, MAX_JOINTS - 1)];
        localPos = skinMat * localPos;
        mat3 skinNormMat = mat3(skinMat);
        localNormal = skinNormMat * localNormal;
        localTangent = skinNormMat * localTangent;
    }

    vec4 worldPos = uModel * localPos;
    vWorldPos = worldPos.xyz;
    vNormal = normalize(uNormalMatrix * localNormal);
    vTangent = vec4(normalize(uNormalMatrix * localTangent), aTangent.w);
    vTexCoord = aTexCoord;
    vLightSpacePos = uLightSpaceMatrix * worldPos;
    gl_Position = uViewProj * worldPos;
}
