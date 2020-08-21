#!/bin/bash
# --------------------------------------------------------------------------------------------
# Copyright (c) Microsoft Corporation. All rights reserved.
# Licensed under the MIT license.
# --------------------------------------------------------------------------------------------

set -ex

declare -r REPO_DIR=$( cd $( dirname "$0" ) && cd .. && cd .. && pwd )

source $REPO_DIR/platforms/__common.sh

nodePlatformDir="$REPO_DIR/platforms/nodejs"
hostNodeArtifactsDir="$volumeHostDir/nodejs"
mkdir -p "$hostNodeArtifactsDir"

builtNodeImage=false
buildNodeImage() {
	if ! $builtNodeImage; then
		docker build \
			-f "$nodePlatformDir/Dockerfile" \
			-t $imageName \
			$REPO_DIR
		builtNodeImage=true
	fi
}

getNode() {
	local version="$1"

	if shouldBuildSdk nodejs nodejs-$version.tar.gz || shouldOverwriteSdk || shouldOverwriteNodeSdk; then
		echo "Getting Node version '$version'..."
		echo

		buildNodeImage

		docker run \
			-v $hostNodeArtifactsDir:$volumeContainerDir \
			$imageName \
			bash -c "/tmp/scripts/build.sh $version && cp -f /tmp/compressedSdk/* /tmp/sdk"
		
		echo "Version=$version" >> "$hostNodeArtifactsDir/nodejs-$version-metadata.txt"
	fi
}

getYarn() {
	local version="$1"

	local hostDir="$hostNodeArtifactsDir/yarn"
	if shouldBuildSdk yarn yarn-$version.tar.gz || shouldOverwriteSdk || shouldOverwriteYarnSdk; then
		echo "Getting Yarn version '$version'..."
		echo

		buildNodeImage

		docker run \
			-v $hostDir:$volumeContainerDir \
			$imageName \
			bash -c "/tmp/scripts/getYarn.sh $version && cp -f /tmp/compressedSdk/yarn/* /tmp/sdk"
		
		echo "Version=$version" >> "$hostNodeArtifactsDir/yarn-$version-metadata.txt"
	fi
}

shouldOverwriteNodeSdk() {
	if [ "$OVERWRITE_EXISTING_SDKS_NODE" == "true" ]; then
		return 0
	else
		return 1
	fi
}

shouldOverwriteYarnSdk() {
	if [ "$OVERWRITE_EXISTING_SDKS_YARN" == "true" ]; then
		return 0
	else
		return 1
	fi
}

echo "Getting Node Sdk..."
echo
buildPlatform "$nodePlatformDir/versionsToBuild.txt" getNode

echo "Getting Yarn..."
echo
buildPlatform "$nodePlatformDir/yarn/versionsToBuild.txt" getYarn

# Write the default version
cp "$nodePlatformDir/defaultVersion.txt" $hostNodeArtifactsDir
cp "$nodePlatformDir/yarn/defaultVersion.txt" "$hostNodeArtifactsDir/yarn"

