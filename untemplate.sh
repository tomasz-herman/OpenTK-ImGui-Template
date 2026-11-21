#!/bin/bash
set -e

echo -n "Enter project name: "
read -r PROJECT_NAME
NAMESPACE="$(echo -e "${PROJECT_NAME}" | tr -d '[:space:]')"

echo -n "Template type (2d / 3d / both) [3d]: "
read -r MODE
MODE="${MODE:-3d}"

case "$MODE" in
  2d)
    rm -rf Template3D
    dotnet sln OpenTK-ImGui-Template.sln remove Template3D/Template3D.csproj >/dev/null 2>&1 || true
    TARGET_DIR="Template2D"
    ;;
  3d)
    rm -rf Template2D
    dotnet sln OpenTK-ImGui-Template.sln remove Template2D/Template2D.csproj >/dev/null 2>&1 || true
    TARGET_DIR="Template3D"
    ;;
  both)
    TARGET_DIR=""
    ;;
  *)
    echo "Unknown mode: $MODE (expected 2d, 3d, or both)"
    exit 1
    ;;
esac

if [ -n "$TARGET_DIR" ]; then
    sed -i "s/${TARGET_DIR}/${NAMESPACE}/g" OpenTK-ImGui-Template.sln
    find "${TARGET_DIR}" -name '*.cs' -exec sed -i "s/${TARGET_DIR}/${NAMESPACE}/g" {} +
    mv "${TARGET_DIR}/${TARGET_DIR}.csproj" "${TARGET_DIR}/${NAMESPACE}.csproj"
    mv "${TARGET_DIR}" "${NAMESPACE}"
fi

mv OpenTK-ImGui-Template.sln "${NAMESPACE}.sln"
echo "Created ${NAMESPACE}.sln (${MODE})"
