FROM mcr.microsoft.com/devcontainers/dotnet:latest@sha256:734d73c596e73d558145c6d2c2fc646021f48cc7fb94b7446cbc1eeeddb90db9

ARG INSTALL_NODE="true"
ARG NODE_VERSION="lts/*"
RUN if [ "${INSTALL_NODE}" = "true" ]; then su vscode -c "umask 0002 && . /usr/local/share/nvm/nvm.sh && nvm install ${NODE_VERSION} 2>&1"; fi
