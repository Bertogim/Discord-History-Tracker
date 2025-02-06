#!/bin/bash
set -e

export TZ=UTC

if [ ! -f "DiscordHistoryTracker.sln" ]; then
  echo "Missing DiscordHistoryTracker.sln in working directory!"
  exit 1
fi

makezip() {
  BIN_PATH="$(pwd)/bin"

  pushd "$BIN_PATH/$1"

  find . -type d -exec chmod 755 {} \;
  find . -type f -exec chmod 644 {} \;

  chmod -f 755 DiscordHistoryTracker || true
  chmod -f 755 DiscordHistoryTracker.exe || true

  find . -type f | sort | zip -9 -X "$BIN_PATH/$1.zip" -@

  popd
}

rm -rf "./bin"

configurations=(win-x64 linux-x64 osx-x64)

for cfg in "${configurations[@]}"; do
  dotnet publish Desktop -c Release -r "$cfg" -o "./bin/$cfg" --self-contained true
  makezip "$cfg"
done

dotnet publish Desktop -c Release -o "./bin/portable" -p:PublishSingleFile=false -p:PublishTrimmed=false --self-contained false
rm "./bin/portable/DiscordHistoryTracker"
makezip "portable"
