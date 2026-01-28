FROM mcr.microsoft.com/devcontainers/dotnet:latest@sha256:83b3ef06623ee1ca350b12359f6be24cda4d206727d21412646f3cbdd5de21c1

ARG INSTALL_NODE="true"
ARG NODE_VERSION="lts/*"
RUN if [ "${INSTALL_NODE}" = "true" ]; then su vscode -c "umask 0002 && . /usr/local/share/nvm/nvm.sh && nvm install ${NODE_VERSION} 2>&1"; fi
