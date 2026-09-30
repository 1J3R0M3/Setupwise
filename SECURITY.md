# Security policy

Setupwise installs software, so security matters a lot to us.

## Reporting a vulnerability

Please **do not open a public issue**. Use GitHub's
[private vulnerability reporting](https://github.com/1J3R0M3/Setupwise/security/advisories/new) instead.
We will confirm receipt within a few days and keep you updated.

## What Setupwise does and does not do

- Apps are installed only through `winget` from the official `winget` source; Setupwise never downloads installers itself.
- The only other network requests are: the homepage and icon of an app (if icons are enabled) and the GitHub Releases API (if the update check is enabled).
- Setupwise collects no telemetry.

## Supported versions

Only the latest release receives fixes.
