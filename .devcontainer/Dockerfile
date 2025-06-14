FROM mcr.microsoft.com/devcontainers/dotnet:latest@sha256:d99e4e4a3de2bf9249b985e7cb05024d9227361bd346611c0b7f352360c4dc3b

ARG INSTALL_NODE="true"
ARG NODE_VERSION="lts/*"
RUN if [ "${INSTALL_NODE}" = "true" ]; then su vscode -c "umask 0002 && . /usr/local/share/nvm/nvm.sh && nvm install ${NODE_VERSION} 2>&1"; fi
