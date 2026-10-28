FROM mcr.microsoft.com/devcontainers/dotnet:latest@sha256:2d9655219342cfe3ec8981de26afffa527308000c848bc98ec12551f31f2ee1c

ARG INSTALL_NODE="true"
ARG NODE_VERSION="lts/*"
RUN if [ "${INSTALL_NODE}" = "true" ]; then su vscode -c "umask 0002 && . /usr/local/share/nvm/nvm.sh && nvm install ${NODE_VERSION} 2>&1"; fi
