FROM mcr.microsoft.com/devcontainers/dotnet:latest@sha256:694076b55f447d4b9f5b49bc6616c2e904d63e1a61e43680b2bbea46011b6783

ARG INSTALL_NODE="true"
ARG NODE_VERSION="lts/*"
RUN if [ "${INSTALL_NODE}" = "true" ]; then su vscode -c "umask 0002 && . /usr/local/share/nvm/nvm.sh && nvm install ${NODE_VERSION} 2>&1"; fi
