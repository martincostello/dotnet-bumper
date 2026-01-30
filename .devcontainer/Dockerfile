FROM mcr.microsoft.com/devcontainers/dotnet:latest@sha256:537a38efbafe5153db2db1c6cc8a1cad5286e91d0e5da9bb7b310d3897d1c6cb

ARG INSTALL_NODE="true"
ARG NODE_VERSION="lts/*"
RUN if [ "${INSTALL_NODE}" = "true" ]; then su vscode -c "umask 0002 && . /usr/local/share/nvm/nvm.sh && nvm install ${NODE_VERSION} 2>&1"; fi
