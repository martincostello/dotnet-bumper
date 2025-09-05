FROM mcr.microsoft.com/devcontainers/dotnet:latest@sha256:9182c82253292f4572383b0b8ba56229a097dd250299abba5028c0c1d0da773d

ARG INSTALL_NODE="true"
ARG NODE_VERSION="lts/*"
RUN if [ "${INSTALL_NODE}" = "true" ]; then su vscode -c "umask 0002 && . /usr/local/share/nvm/nvm.sh && nvm install ${NODE_VERSION} 2>&1"; fi
