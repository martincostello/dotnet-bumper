FROM mcr.microsoft.com/devcontainers/dotnet:latest@sha256:8fd6c758b5b5e71dacefb3312930ea050ef9afcd21159ac47225edfd6a3b207a

ARG INSTALL_NODE="true"
ARG NODE_VERSION="lts/*"
RUN if [ "${INSTALL_NODE}" = "true" ]; then su vscode -c "umask 0002 && . /usr/local/share/nvm/nvm.sh && nvm install ${NODE_VERSION} 2>&1"; fi
