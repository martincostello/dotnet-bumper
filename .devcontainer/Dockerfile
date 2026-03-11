FROM mcr.microsoft.com/devcontainers/dotnet:latest@sha256:0c412724a61b030f27d4ca38a876db27a88c09a7c32344013562710f4117aec3

ARG INSTALL_NODE="true"
ARG NODE_VERSION="lts/*"
RUN if [ "${INSTALL_NODE}" = "true" ]; then su vscode -c "umask 0002 && . /usr/local/share/nvm/nvm.sh && nvm install ${NODE_VERSION} 2>&1"; fi
