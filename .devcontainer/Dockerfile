FROM mcr.microsoft.com/devcontainers/dotnet:latest@sha256:8f4d2e325d0c07af71374436657b2627eb13f35e974596ec425d71ad49cb0196

ARG INSTALL_NODE="true"
ARG NODE_VERSION="lts/*"
RUN if [ "${INSTALL_NODE}" = "true" ]; then su vscode -c "umask 0002 && . /usr/local/share/nvm/nvm.sh && nvm install ${NODE_VERSION} 2>&1"; fi
