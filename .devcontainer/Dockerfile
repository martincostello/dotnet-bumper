FROM mcr.microsoft.com/devcontainers/dotnet:latest@sha256:8ba49796e4cb84c1d7bcd676a14d6a03ec88b1afab8d75762ff9abbcbb33c707

ARG INSTALL_NODE="true"
ARG NODE_VERSION="lts/*"
RUN if [ "${INSTALL_NODE}" = "true" ]; then su vscode -c "umask 0002 && . /usr/local/share/nvm/nvm.sh && nvm install ${NODE_VERSION} 2>&1"; fi
