# PDFsharp & MigraDoc 7 – MIT fork

> **This is an independent fork under the MIT license.**  
> It is based on the last version of PDFsharp & MigraDoc that was published under the MIT license:
> **7.0.0 Preview 1** (upstream commit `8d818d5`, 2026-05-05).
> Later upstream versions are licensed under the AGPL and are **not** part of this fork.  
> This fork is not affiliated with or endorsed by empira Software GmbH.  
> **Never merge or "Sync fork" with the upstream repository**: that would bring AGPL-licensed code into this fork.

### Platforms and target framework

There is **one code base for Windows, Linux, and macOS**. All projects target **.NET 10** (`net10.0`) only.

* The same build and the same NuGet packages (`PDFsharp` and `PDFsharp-MigraDoc`) run on all three operating systems.
* The former Windows-only GDI and WPF builds (System.Drawing and WPF rendering, desktop preview controls, and printing) are not part of this fork.
* .NET Framework, .NET Standard, and .NET 8/9 are not supported.

### License

Published Open Source under the MIT license, see [LICENSE](LICENSE).
The copyright notices of the original authors must be kept:

PDFsharp: Copyright (c) 2005-2026 empira Software GmbH, Troisdorf (Cologne Area), Germany  
MigraDoc: Copyright (c) 2001-2026 empira Software GmbH, Troisdorf (Cologne Area), Germany

## Read this FIRST

The documentation of the original project can be found at <https://docs.pdfsharp.net>.
Note that it describes the upstream versions, which may differ from this fork.

Note that PowerShell 7 is required to execute the PowerShell scripts that come with PDFsharp.

### Download assets first

Assets like bitmaps, fonts, or PDF files are not part of the repository anymore.
You must download them before compiling the solution for the first time.
Use `download-assets.ps1` in the `dev` folder to create `assets` folder required for some unit tests and needed by some projects.

Execute

```ps
.\dev\download-assets.ps1
```

### Build the solution

`dotnet build` should build the solution without any warnings or errors.

* You need the latest .NET SDK version installed
* ~~Please note that you need a git repository with at least one commit in order to build the PDFsharp solution.  
  Without a git repository with at least one commit, you will get an error message from `GitVersion.MsBuild` while building the solution.
  You can set a tag to define a valid version, e.g.: `git tag v6.2.0` to make it build with a specific version number. Without tag, version 0.1.0 will be used.~~  
  (Is now done automatically by BuildConfig)

### Central package management

The solution uses central package management.
Version numbers for all referenced packages are stored in file `Directory.Packages.props` in the `src` folder.
When adding new packages, add the required version here.

## Authors

PDFsharp and MigraDoc was mainly written by the following software developers.
With support of a lot of community developers who found issues and fixed bugs.

### Original PDFsharp developers

Stefan Lange  
Niklas Schneider  
David Stephensen

### Original MigraDoc developers

Klaus Potzesny  
Niklas Schneider  
Stefan Lange

### Current PDFsharp and MigraDoc developers

Stefan Lange  
Thomas Hövel  
Martin Ossendorf  
Andreas Seifert

## Libraries used by PDFsharp

The Core build of PDFsharp uses BigGustave to read PNG images. BigGustave was released into the public domain and does not restrict the MIT license used by PDFsharp.  
Link to project repository: https://github.com/EliotJones/BigGustave
