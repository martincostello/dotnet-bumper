FROM mcr.microsoft.com/devcontainers/dotnet:latest@sha256:261a5462957e54566e9982e96521c35adc7df56bfd8bdff5163e8d296658e0c5

ARG INSTALL_NODE="true"
ARG NODE_VERSION="lts/*"
RUN if [ "${INSTALL_NODE}" = "true" ]; then su vscode -c "umask 0002 && . /usr/local/share/nvm/nvm.sh && nvm install ${NODE_VERSION} 2>&1"; fi
