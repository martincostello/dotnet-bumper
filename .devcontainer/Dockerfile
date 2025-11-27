FROM mcr.microsoft.com/devcontainers/dotnet:latest@sha256:3af834758c4b1c7bc240e4751e5793d715024bc44cf7366f4bbb176331476db6

ARG INSTALL_NODE="true"
ARG NODE_VERSION="lts/*"
RUN if [ "${INSTALL_NODE}" = "true" ]; then su vscode -c "umask 0002 && . /usr/local/share/nvm/nvm.sh && nvm install ${NODE_VERSION} 2>&1"; fi
