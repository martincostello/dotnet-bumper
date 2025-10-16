FROM mcr.microsoft.com/devcontainers/dotnet:latest@sha256:7572080d1ce09283b8bc2504e3e88cab79dede5eb7e66973d544eea86c3fa216

ARG INSTALL_NODE="true"
ARG NODE_VERSION="lts/*"
RUN if [ "${INSTALL_NODE}" = "true" ]; then su vscode -c "umask 0002 && . /usr/local/share/nvm/nvm.sh && nvm install ${NODE_VERSION} 2>&1"; fi
