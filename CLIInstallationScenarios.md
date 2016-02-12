# General rules
- No http links in any readme page (use https)
- All installers signed

# Readme pages
[Getting Started Page](https://dotnet.github.io/getting-started/) - for customers
[Repo landing page](https://github.com/dotnet/cli/blob/rel/1.0.0/README.md) - for contributors

# Interactive installation
Interactive installations are driven by users who explicitly want to get access to dotnet CLI bits. Users reach these experiences through Web searches, marketing content, or links from other sites.

# Copy & paste installation script
## install.sh/install.ps1

- Local installation (consumed by build scripts)
- Global installation (consumed by users who like command line)

## Getting Started Page
http://dotnet.github.io/getting-started/
* Installation targets: native installers
* Source branch: rel/1.0.0
* Linked builds: LKG ?? latest green build of rel/1.0.0;
* Debian feed: Release Nightly
* Installation script links: Latest from rel/1.0.0

This is the main curated first-run experience for the dotnet CLI. The intent of the page is to help users "kick the tires" quickly and become familiar with what the platform offers. This should be the most stable and curated experience we can offer.

- Should include debian package feed (release nightly)

## Repo Landing Page
https://github.com/dotnet/cli/readme.md
* Installation targets: native installers and ZIPs
* Source branch: rel/1.0.0
* Linked builds: latest green build of rel/1.0.0;

Folks coming to the repo should be aggressively redirected to the Getting Started Page. The Repo Landing Page should not be treated as a source of installers.

The Repo Landing Page should be used primarily by contributors to the CLI. The links on the CLI landing page should point to the latest green builds of the active release branch. There should also be a separate page that points to the latest Dev build.

## Integrating into other products [e.g. VS]

