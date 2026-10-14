# Smart Harness for AI Driven Engineering Kit

This project is a harnessed workflow guided Toolkit helping you to build and test your software with AI agents.
It is designed to be a comprehensive solution for managing the entire software development lifecycle, from code generation to testing and deployment, all while leveraging the power of AI.

## Build

The build uses [Cake](https://cakebuild.net) with `CreativeCoders.CakeBuild` (project in `build/`). It needs the .NET 10 SDK and the full git history, because [GitVersion](https://gitversion.net) derives the version from it.

```sh
./build.sh -t pack                              # build, test, coverage report and the dotnet tool package
./build.sh -t smoketest -t createdistpackages   # self-contained single-file binaries for the host OS, smoke test, archives
```

On Windows use `build.cmd` or `build.ps1`. Results land in `.artifacts/` and `.tests/`.

A release is a tag `v<SemVer>` on `main`; the `release` workflow publishes the tool package to nuget.org and creates a GitHub release with binaries for `osx-arm64`, `linux-x64`, `linux-arm64`, `win-x64` and `win-arm64`.
