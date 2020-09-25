FROM oryxdevmcr.azurecr.io/public/oryx/build:lts-versions AS main

###
# Build run script generators (to be used by the `oryx run-script` command)
###
FROM golang:1.11-stretch as startupScriptGens

# GOPATH is set to "/go" in the base image
WORKDIR /go/src
COPY src/startupscriptgenerator/src .

ARG GIT_COMMIT=unspecified
ARG BUILD_NUMBER=unspecified
ARG RELEASE_TAG_NAME=unspecified
ENV GIT_COMMIT=${GIT_COMMIT}
ENV BUILD_NUMBER=${BUILD_NUMBER}
ENV RELEASE_TAG_NAME=${RELEASE_TAG_NAME}

RUN ./build.sh dotnetcore /opt/startupcmdgen/dotnet
RUN ./build.sh node       /opt/startupcmdgen/nodejs
RUN ./build.sh php        /opt/startupcmdgen/php
RUN ./build.sh python     /opt/startupcmdgen/python
###
# End build run script generators
###

# Install .NET Core
FROM main AS intermediate
COPY --from=support-files-image-for-build /tmp/oryx/ /opt/tmp
COPY --from=yarn-cache-base /usr/local/share/yarn-cache /opt/yarn-cache
COPY --from=startupScriptGens /opt/startupcmdgen/ /opt/startupcmdgen/
ARG BUILD_DIR="/opt/tmp/build"
ARG IMAGES_DIR="/opt/tmp/images"
RUN LANG="C.UTF-8" \
    && apt-get update \
    && apt-get upgrade -y \
    && apt-get install -y --no-install-recommends \
        # For .NET Core 1.1
        libcurl3 \
        libuuid1 \
        libunwind8 \
    && rm -rf /var/lib/apt/lists/*

RUN mkdir /opt/nuget
# Check https://www.microsoft.com/net/platform/support-policy for support policy of .NET Core versions
ENV NUGET_PACKAGES=/opt/nuget
RUN . ${BUILD_DIR}/__dotNetCoreSdkVersions.sh && \
    DOTNET_SDK_VER=$DOT_NET_CORE_22_SDK_VERSION \
    ${IMAGES_DIR}/build/installDotNetCore.sh

RUN . ${BUILD_DIR}/__dotNetCoreSdkVersions.sh && \
    DOTNET_SDK_VER=$DOT_NET_CORE_30_SDK_VERSION \
    ${IMAGES_DIR}/build/installDotNetCore.sh

RUN set -ex \
    rm -rf /tmp/NuGetScratch \
    && find /opt/nuget -type d -exec chmod 777 {} \;

# Install Node.js, NPM, Yarn
RUN apt-get update \
    && apt-get upgrade -y \
    && apt-get install -y --no-install-recommends \
        jq \
    && rm -rf /var/lib/apt/lists/*

FROM main AS final
COPY --from=intermediate /opt /opt
# https://github.com/docker-library/python/issues/147
RUN PYTHONIOENCODING="UTF-8" \
    && apt-get update \
    && apt-get upgrade -y \
    && apt-get install -y --no-install-recommends \
        # Required for .NET Core 1.1
        libunwind8 \
    && rm -rf /var/lib/apt/lists/* \
    && tmpDir="/opt/tmp" \
    && imagesDir="$tmpDir/images" \
    && buildDir="$tmpDir/build" \
    && mkdir -p /var/nuget \
    && cd /opt/nuget \
    && cp -rf . /var/nuget \
    && cd /opt \
    && rm -rf /opt/nuget \
    && mkdir -p /usr/local/share/yarn-cache \
    && cd /opt/yarn-cache \
    && cp -rf . /usr/local/share/yarn-cache \
    && cd /opt \
    && rm -rf /opt/yarn-cache \
    && chmod -R 777 /usr/local/share/yarn-cache \
    && cd $imagesDir \
    && $imagesDir/build/createFlattenedDotNetCoreInstall.sh \
    && cp $imagesDir/build/dotNetCoreAll.Readme.md /opt/dotnet/Readme.md \
    # Copy PHP versions
    && cd $imagesDir \
    && . $buildDir/__phpVersions.sh \
    && rm -rf $tmpDir
