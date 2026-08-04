<!--
  Please read before submitting.

  This repository is a one-way published mirror of a private upstream repo. Every publish
  resets this branch, so a PR merged here would be erased by the next sync — we cannot merge
  pull requests with the green button.

  That does NOT mean your work is wasted. We treat the PR as a patch: a maintainer applies your
  diff upstream, credits you in the commit, and closes this PR with a link to the resulting
  public commit. The linked issue closes when the fix ships in a release.

  Full explanation: .github/CONTRIBUTING.md
-->

## Related issue

<!-- Required. Open one first if it doesn't exist — every change starts as an issue. -->

Closes: #

## What this changes

<!-- What was broken or missing, and what you did about it. -->

## How you tested it

<!--
  At minimum:
    dotnet build ArdysaModsTools.csproj -c Debug
    dotnet test  Tests/ArdysaModsTools.Tests.csproj --configuration Release

  Note: the VPK binaries aren't published to this mirror, so the app can't be run end-to-end
  from a mirror-only clone. Say what you were and weren't able to verify.
-->

## Checklist

- [ ] There's an issue linked above
- [ ] `dotnet build -c Debug` passes
- [ ] `dotnet test` passes
- [ ] New public service methods have tests (happy path + one error case)
- [ ] No business logic added to a Form; no `new ConcreteService()`
- [ ] Nothing writes into the Dota 2 folder outside the file-transaction service
