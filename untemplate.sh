#!/bin/bash
echo -n "Enter project name: "
read -r PROJECT_NAME
NAMESPACE="$(echo -e "${PROJECT_NAME}" | tr -d '[:space:]')"

sed -i "s/TemplateProject/${NAMESPACE}/g" OpenTK-ImGui-Template.sln

for file in {**/**,**}/*.cs
do
    sed -i "s/TemplateProject/${NAMESPACE}/g" "${file}"
    sed -i "s/Project Title/${PROJECT_NAME}/g" "${file}"
done

mv OpenTK-ImGui-Template.sln "${NAMESPACE}".sln
mv TemplateProject/TemplateProject.csproj TemplateProject/"${NAMESPACE}".csproj
mv TemplateProject "${NAMESPACE}"