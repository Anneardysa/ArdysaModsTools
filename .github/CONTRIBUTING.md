# Contributing to ArdysaModsTools

Thanks for wanting to help. This project runs on an **issues-first** model: every fix,
feature, and mod addition starts life as an issue, and the issue is where the whole
conversation lives until it ships.

Read the short section below on how this repository works first. It explains why this project
does not take pull requests.

---

## How this repository works

AMT is developed in a private upstream repository. **The source in this GitHub repository is a
frozen snapshot** of build 2467 (v2.10.0-beta), the last GPL-3.0 release. It no longer receives
code; from build 2468 on, AMT is proprietary. Issues, releases and documentation still live here.

That has two consequences you need to know about:

1. **The snapshot is read-only.** It is not where AMT is developed, so nothing merged here would
   ever reach the app.
2. **Source comments were stripped** from `.cs` files when it was published. The code is complete
   and compiles, but you will not find explanatory comments in it.

So: **we do not take pull requests or patches.** See
[Code from outside the project](#code-from-outside-the-project) below.

---

## Ways to contribute

| I want to…                             | Do this                                                                                                                    |
| -------------------------------------- | -------------------------------------------------------------------------------------------------------------------------- |
| Report something broken                | [Bug report](https://github.com/Anneardysa/ArdysaModsTools/issues/new?template=1-bug.yml)                                    |
| Ask for a hero set, courier, ward, HUD | [Mod / set request](https://github.com/Anneardysa/ArdysaModsTools/issues/new?template=2-mod-request.yml)                     |
| Suggest a change to how AMT works      | [Feature request](https://github.com/Anneardysa/ArdysaModsTools/issues/new?template=3-feature.yml)                           |
| Fix wrong or awkward text in my language | [Translation fix](https://github.com/Anneardysa/ArdysaModsTools/issues/new?template=4-translation.yml)                      |
| Ask "how do I…"                        | [Discord](https://discord.gg/5xKg4fyumv) — issues are for bugs and requests                                                  |
| Report a security vulnerability        | [Private advisory](https://github.com/Anneardysa/ArdysaModsTools/security/advisories/new) — **never** a public issue         |

Blank issues are disabled on purpose. The templates ask for the handful of fields that make a
report actionable; a free-form issue almost always needs three round-trips to get the same
information.

---

## Writing a bug report that gets fixed

Two things decide whether a report can be solved: **the error code** and **the log file**.

### Error codes

AMT errors look like `DL_006` or `VPK_003`. The prefix tells us which subsystem failed:

| Prefix   | Subsystem                            |
| -------- | ------------------------------------ |
| `DL_`    | downloading assets from the CDN      |
| `VPK_`   | packing / extracting game archives   |
| `PATCH_` | patching Dota 2 game files           |
| `GEN_`   | hero set generation                  |
| `CFG_`   | Dota 2 / Steam detection, settings   |
| `MISC_`  | miscellaneous mods                   |
| `UPD_`   | updating AMT itself                  |
| `SEC_`   | integrity checks                     |

You'll see the code in AMT's console panel (main window) or in the log file.

### Log files

| File                     | Where                                                                                                 | What it's for                      |
| ------------------------ | ----------------------------------------------------------------------------------------------------- | ---------------------------------- |
| `ardysa_fallback.log`    | Installer builds: `%LOCALAPPDATA%\ArdysaModsTools\`<br>Portable builds: next to `ArdysaModsTools.exe` | The main diagnostic log. Attach this. |
| `startup_log.txt`        | Next to `ArdysaModsTools.exe`                                                                          | When AMT won't start at all. Overwritten on every launch, so grab it right after a failed start. |
| `generation_report_*.txt` | `<Dota 2 folder>\game\_ArdysaMods\_temp\`                                                             | Skin Selector / Miscellaneous bugs. Already sanitized — safe to post as-is. |

> **A screenshot of the red failure card is not enough.** That card is deliberately stripped of
> file paths and internal identifiers before it's shown, so it can't tell us what actually broke.
> The log can.

### Before you file

Most reports are one of these, and all three are self-serve:

- **You're on an old build.** `DL_009` means exactly this — the assets have moved on and your
  version can't read them. Update AMT.
- **Dota 2 just updated.** Valve patches change the files AMT rewrites. Click **Patch Update** in
  AMT first; that fixes the majority of "everything broke today" reports.
- **It's already in [TROUBLESHOOTING.md](../docs/TROUBLESHOOTING.md).** Worth two minutes.

---

## Code from outside the project

AMT does not accept code from outside the project. That covers pull requests, patches pasted
into issues, and requests to become a collaborator. A pull request opened here is closed
automatically.

If you found a bug, or you know where the fix is, open an issue and describe it. Name the file
and method and say what goes wrong. A precise report gets fixed fastest.

### Building from source

```powershell
dotnet build ArdysaModsTools.csproj -c Debug
dotnet test  Tests/ArdysaModsTools.Tests.csproj --configuration Release
```

Requires the .NET 8 SDK on Windows. Both commands work on a fresh clone of this repository.

One caveat: the bundled VPK binaries (`vpk.exe`, `HLExtract.exe` and their runtime DLLs) are
third-party and are **not** published to this mirror. The project compiles and the test suite
passes without them — tests that need a real VPK skip themselves — but the app cannot actually
pack or extract game files from a mirror-only build. That's enough to read the code and
reproduce logic bugs for a report; it is not enough to run AMT end-to-end.

---

## What happens to your issue

Labels tell you exactly where an issue stands.

| Label                            | Meaning                                                                  |
| -------------------------------- | ------------------------------------------------------------------------ |
| `status: triage`                 | Just filed, not yet looked at                                             |
| `needs: info` / `needs: log`     | We can't proceed without more from you. Closed automatically after 3 weeks of silence — comment any time to reopen. |
| `status: accepted`               | Confirmed, and it will be worked on                                       |
| `status: in-progress`            | Someone is on it now                                                      |
| `status: fixed-pending-release`  | **Fixed in code, not yet in a release you can download.**                 |
| `status: shipped`                | In a published release — issue closed                                     |
| `blocked: dota-patch`            | Waiting on a Dota 2 update to be worked around                            |

That second-to-last one is deliberate. A fix isn't real to you until the installer is out, so we
don't close issues at commit time — a closed issue would be a lie while you're still running the
broken build. Instead the issue is marked `status: fixed-pending-release`, and a bot closes it
with the version number when the release actually publishes.

---

## Code of Conduct

Participation is governed by our [Code of Conduct](CODE_OF_CONDUCT.md). Be decent to people,
especially people who are new or writing in their second language.

## Licensing

From build 2468, AMT is **proprietary**; its terms ship with the app as the `LICENSE` file. Builds
up to and including 2467, and the source snapshot here, remain under **GPL-3.0**. Note the trademark and brand reservations in
[NOTICE](../NOTICE): they apply to every version.
