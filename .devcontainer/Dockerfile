FROM mcr.microsoft.com/devcontainers/dotnet:latest@sha256:bdced679b0d7031b802a4e2ead0b3f339473f30e61a52c2f675069efe2e8f7b2

ARG INSTALL_NODE="true"
ARG NODE_VERSION="lts/*"
RUN if [ "${INSTALL_NODE}" = "true" ]; then su vscode -c "umask 0002 && . /usr/local/share/nvm/nvm.sh && nvm install ${NODE_VERSION} 2>&1"; fi
