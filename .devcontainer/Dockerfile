FROM mcr.microsoft.com/devcontainers/dotnet:latest@sha256:86b40dd4bb6d4b58e9cee1f997203ca084e8447aa5a29df50c70c7bf9f1ffcc9

ARG INSTALL_NODE="true"
ARG NODE_VERSION="lts/*"
RUN if [ "${INSTALL_NODE}" = "true" ]; then su vscode -c "umask 0002 && . /usr/local/share/nvm/nvm.sh && nvm install ${NODE_VERSION} 2>&1"; fi
