#!/usr/bin/env bash

workspace_path=$1

# Clean workspace
sudo rm -r -f $workspace_path

if [ -d "$workspace_path" ]; then
  declare -i suffix=0
  sudo rm -r -f $workspace_path.$suffix
  while [ -d "$workspace_path.$suffix" ]
  do
    suffix=$suffix+1
    sudo rm -r -f $workspace_path.$suffix
  done
  workspace_path=$workspace_path.$suffix
fi

# Make the workspace path directory.
sudo mkdir $workspace_path

# Make it writeable
sudo chmod -R 777 $workspace_path

# Make its parents readable
if [ -d "$workspace_path/.." ]; then
  sudo chmod +r $workspace_path/..
fi

if [ -d "$workspace_path/../.." ]; then
  sudo chmod +r $workspace_path/../..
fi

# copy .agent and .credentials files to workspace path
cp -f -r $HELIX_CORRELATION_PAYLOAD/* $workspace_path
cp -f $HELIX_WORKITEM_PAYLOAD/.agent $workspace_path
cp -f $HELIX_WORKITEM_PAYLOAD/.credentials $workspace_path

$workspace_path/run.sh --once

# Expect an exit code of 2, which is what is given when the agent connection is revoked
lastexitcode=$?

if [ -d "$workspace_path/_diag" ]; then
  echo "Copying _diag folder to upload root"
  cp -r "$workspace_path/_diag" $HELIX_WORKITEM_UPLOAD_ROOT
fi

echo Requesting reboot after build completes
$HELIX_PYTHONPATH -c "from helix.workitemutil import request_reboot; request_reboot('Reboot for build agent hygiene')"

# Repeat the deletion of the workspace directory because for very large builds this can take minutes to complete,
# which counts towards the queue time of the build it picks up.
sudo rm -r -f $workspace_path

if [[ $lastexitcode -ne 0 ]]; then
  echo "Unexpected error returned from agent: $lastexitcode"
  exit $lastexitcode
else
  echo "Agent disconnected successfully, exiting"
  exit 0
fi
