#!/bin/bash
set -ex

YARN_VERSION="$1"

curl -fsSLO --compressed "https://yarnpkg.com/downloads/$YARN_VERSION/yarn-v$YARN_VERSION.tar.gz"
curl -fsSLO --compressed "https://yarnpkg.com/downloads/$YARN_VERSION/yarn-v$YARN_VERSION.tar.gz.asc"
gpg --batch --verify yarn-v$YARN_VERSION.tar.gz.asc yarn-v$YARN_VERSION.tar.gz
targetDir="/tmp/compressedSdk/yarn"
mkdir -p "$targetDir"
cp yarn-v$YARN_VERSION.tar.gz "$targetDir/yarn-$YARN_VERSION.tar.gz"
