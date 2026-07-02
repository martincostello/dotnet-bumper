FROM mcr.microsoft.com/devcontainers/dotnet:latest@sha256:be5bd898d964c0d081cb22285c2318e488882103702c8831418fdd59218fd5dd

ARG INSTALL_NODE="true"
ARG NODE_VERSION="lts/*"
RUN if [ "${INSTALL_NODE}" = "true" ]; then su vscode -c "umask 0002 && . /usr/local/share/nvm/nvm.sh && nvm install ${NODE_VERSION} 2>&1"; fi
