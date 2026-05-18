FROM mcr.microsoft.com/devcontainers/dotnet:latest@sha256:08f7b67c24a3ad2f5b059f0b2a847d3a9a5526945ded027f3cd9b535c0f1aa97

ARG INSTALL_NODE="true"
ARG NODE_VERSION="lts/*"
RUN if [ "${INSTALL_NODE}" = "true" ]; then su vscode -c "umask 0002 && . /usr/local/share/nvm/nvm.sh && nvm install ${NODE_VERSION} 2>&1"; fi
