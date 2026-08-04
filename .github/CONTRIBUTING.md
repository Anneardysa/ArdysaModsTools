# Contributing to ArdysaModsTools

Thanks for wanting to help. This project runs on an **issues-first** model: every fix,
feature, and mod addition starts life as an issue, and the issue is where the whole
conversation lives until it ships.

Read the short section below on how this repository works first — it explains why pull
requests are handled differently here than on most projects.

---

## How this repository works

AMT is developed in a private upstream repository. **This GitHub repository is a published
mirror of it**, updated automatically every time work lands upstream.

That has two consequences you need to know about:

1. **The mirror is one-way and it is rewritten on every publish.** Any branch or merge commit
   created here is discarded by the next sync. This is not a policy choice we can bend — it's
   how the publishing mechanism works.
2. **Source comments are stripped** from `.cs` files during publishing. The code is complete and
   compiles, but you will not find explanatory comments in it.

So: **we do not merge pull requests on GitHub.** See
[Sending code](#sending-code) below for what to do instead.

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

## Sending code

You're welcome to fix things — the flow is just unusual, because a merge here would be undone
by the next mirror sync.

1. Open an issue describing the bug or change (or comment on the existing one).
2. Make your fix in a fork.
3. **Paste the patch into the issue** — `git format-patch` output, or a diff in a fenced code
   block. Small fixes can just be "in `X.cs`, change A to B".
4. A maintainer applies it upstream, credits you in the commit, and the issue closes when the
   fix ships in a release.

If you'd rather open a pull request anyway so the diff renders nicely, that's fine — a bot will
post the same explanation, and we'll treat the PR as the patch. It just won't be merged with the
green button.

### Building from source

```powershell
dotnet build ArdysaModsTools.csproj -c Debug
dotnet test  Tests/ArdysaModsTools.Tests.csproj --configuration Release
```

Requires the .NET 8 SDK on Windows. Both commands work on a fresh clone of this repository.

One caveat: the bundled VPK binaries (`vpk.exe`, `HLExtract.exe` and their runtime DLLs) are
third-party and are **not** published to this mirror. The project compiles and the test suite
passes without them — tests that need a real VPK skip themselves — but the app cannot actually
pack or extract game files from a mirror-only build. That's enough to read the code, reproduce
logic bugs, and verify a fix compiles; it is not enough to run AMT end-to-end.

### If you do write code

- **Architecture is MVP and it's enforced.** Forms wire up UI events only; UI logic lives in a
  presenter; business logic lives in a service behind an interface. No business logic in a Form.
- **Dependency injection only.** Constructor injection — no `new ConcreteService()`, no service
  locator in business logic.
- **Every new public service method gets a test.** NUnit 4 + Moq, one happy path and one error
  or edge case. Follow the nearest existing test file.
- **All I/O is `async`/`await` with a `CancellationToken`.** No `Thread.Sleep`.
- **Anything that writes into the Dota 2 folder goes through the file-transaction service** —
  extract to temp, verify SHA-256, atomic swap, roll back on any failure. Never copy or move a
  file straight into the game directory. A mistake here corrupts someone's Dota 2 install.

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

AMT is licensed under **GPL-3.0**. By contributing code, you agree that your contribution is
licensed under the same terms. Note the trademark and brand reservations in
[NOTICE](../NOTICE) — the code is free, the name and logo are not.
