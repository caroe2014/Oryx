#!/bin/bash
set -ex

YARN_VERSION="$1"

baseUrl="https://github.com/yarnpkg/yarn/releases/download"
curl -fsSLO --compressed "$baseUrl/v$YARN_VERSION/yarn-v$YARN_VERSION.tar.gz"
curl -fsSLO --compressed "$baseUrl/v$YARN_VERSION/yarn-v$YARN_VERSION.tar.gz.asc"
gpg --batch --verify yarn-v$YARN_VERSION.tar.gz.asc yarn-v$YARN_VERSION.tar.gz
targetDir="/tmp/compressedSdk/yarn"
mkdir -p "$targetDir"
cp yarn-v$YARN_VERSION.tar.gz "$targetDir/yarn-$YARN_VERSION.tar.gz"
