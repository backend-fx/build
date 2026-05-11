#!/usr/bin/env bash
dotnet tool restore
dotnet cake build.cake "$@"
