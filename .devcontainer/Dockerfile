FROM mcr.microsoft.com/devcontainers/dotnet:latest@sha256:e1ad6d87ed91afc347d9efd214531268f0f83bace13a9309ccdf162dd2876bf5

ARG INSTALL_NODE="true"
ARG NODE_VERSION="lts/*"
RUN if [ "${INSTALL_NODE}" = "true" ]; then su vscode -c "umask 0002 && . /usr/local/share/nvm/nvm.sh && nvm install ${NODE_VERSION} 2>&1"; fi
