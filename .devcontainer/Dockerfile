FROM mcr.microsoft.com/devcontainers/dotnet:latest@sha256:ec121a794c07efec78f3c6f9c665119066265c49235904438a79de459c731755

ARG INSTALL_NODE="true"
ARG NODE_VERSION="lts/*"
RUN if [ "${INSTALL_NODE}" = "true" ]; then su vscode -c "umask 0002 && . /usr/local/share/nvm/nvm.sh && nvm install ${NODE_VERSION} 2>&1"; fi
