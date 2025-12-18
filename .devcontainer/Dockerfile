FROM mcr.microsoft.com/devcontainers/dotnet:latest@sha256:97961d9ef283e2404a79fb9bdca5837b6af57f5f8688556e21cbc5686af72770

ARG INSTALL_NODE="true"
ARG NODE_VERSION="lts/*"
RUN if [ "${INSTALL_NODE}" = "true" ]; then su vscode -c "umask 0002 && . /usr/local/share/nvm/nvm.sh && nvm install ${NODE_VERSION} 2>&1"; fi
