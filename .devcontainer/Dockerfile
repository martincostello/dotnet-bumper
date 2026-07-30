FROM mcr.microsoft.com/devcontainers/dotnet:latest@sha256:0ad11caaa728e35cbc46b0592dd11ca0df8111e565a96471024a795f4d412c7c

ARG INSTALL_NODE="true"
ARG NODE_VERSION="lts/*"
RUN if [ "${INSTALL_NODE}" = "true" ]; then su vscode -c "umask 0002 && . /usr/local/share/nvm/nvm.sh && nvm install ${NODE_VERSION} 2>&1"; fi
