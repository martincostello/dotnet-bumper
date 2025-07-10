FROM mcr.microsoft.com/devcontainers/dotnet:latest@sha256:b878b60a68aadab1a1d7e7ace12504073ce5a6ce568c785f54a9ef4b834e373f

ARG INSTALL_NODE="true"
ARG NODE_VERSION="lts/*"
RUN if [ "${INSTALL_NODE}" = "true" ]; then su vscode -c "umask 0002 && . /usr/local/share/nvm/nvm.sh && nvm install ${NODE_VERSION} 2>&1"; fi
