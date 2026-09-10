FROM mcr.microsoft.com/devcontainers/dotnet:latest@sha256:9eb314432a5fa67773a53ab754cdc381cae614906162f9fc44cdb7f7837b91c7

ARG INSTALL_NODE="true"
ARG NODE_VERSION="lts/*"
RUN if [ "${INSTALL_NODE}" = "true" ]; then su vscode -c "umask 0002 && . /usr/local/share/nvm/nvm.sh && nvm install ${NODE_VERSION} 2>&1"; fi
