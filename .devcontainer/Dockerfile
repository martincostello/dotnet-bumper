FROM mcr.microsoft.com/devcontainers/dotnet:latest@sha256:da953a31996cf89df8c506a5d7ed9374f4285af6f728b55d772cd2d8e205cdef

ARG INSTALL_NODE="true"
ARG NODE_VERSION="lts/*"
RUN if [ "${INSTALL_NODE}" = "true" ]; then su vscode -c "umask 0002 && . /usr/local/share/nvm/nvm.sh && nvm install ${NODE_VERSION} 2>&1"; fi
