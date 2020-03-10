#!/usr/bin/env bash

workspace_path=$1

# Try to clean workspace 
rm -r -f $workspace_path

if [ -d "$workspace_path" ]; then
	echo "Failed to clean workspace... requesting infra retry and reboot"
	$HELIX_PYTHONPATH -c "from helix.workitemutil import request_infra_retry; request_infra_retry('Optional reason string')"
	$HELIX_PYTHONPATH -c "from helix.workitemutil import request_reboot; request_reboot('Optional reason string')"
fi

# Make the workspace path directory.
mkdir $workspace_path

# copy .agent and .credentials files to workspace path
cp -f -r $HELIX_CORRELATION_PAYLOAD/* $workspace_path
cp -f $HELIX_WORKITEM_PAYLOAD/.agent $workspace_path
cp -f $HELIX_WORKITEM_PAYLOAD/.credentials $workspace_path

$workspace_path/run.sh

lastexitcode=$?
if [[ $lastexitcode -ne 0 ]]; then
	echo "Unexpected error returned from agent: $lastexitcode"
	exit $lastexitcode
else
	echo "Agent disconnected successfully, exiting"
	exit 0
fi
