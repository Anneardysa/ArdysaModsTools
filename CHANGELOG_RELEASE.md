# Release Notes — v2.2.16-beta

> Build **2236**

**New hero sets now greet you on the main screen.** The **Latest Updates** row from the Skin Selector — the newest hero sets added to the ModsPack in the last week — now also appears on the main window, right under the banner. Each card shows the set's artwork, the hero's name, how recently it arrived (with a **NEW** badge for the freshest ones), and clicking any card takes you straight into the Skin Selector. Browse it like in the Skin Selector: hover to reveal the side arrows, or just scroll with your mouse wheel. If there's nothing new this week (or you're offline), the row simply stays out of the way.

To make room, the main window is a little taller (1280×1000), with a bigger banner to match — on smaller screens the app still sizes itself down automatically, exactly as before.

---

## 🌟 Highlights

- **Latest Updates on the home screen** (2236): see the week's new hero sets the moment the app opens — no need to open the Skin Selector to find out what's new. One click on a card takes you there.
- **Skin Selector-style browsing** (2236): the row pages with hover arrows and responds to your mouse wheel, in both dark and light themes, in all 8 languages.

---

## 🐛 Fixed

- **The "What's New" ModsPack version badge could silently stay stale** (2236): a server-side update that bumped only the ModsPack version (without changing the banner images) was ignored entirely — the badge and the install-card art never refreshed. They now update independently of the banner.
- **Console messages could show up blank for a moment at startup** (2236): log lines written in your language could render as empty lines if they arrived before the translations finished loading. They now always show readable text immediately.

---

# Release Notes — v2.2.15-beta

> Build **2234-2235**

**The app now has a Light theme.** Open **Settings → Appearance → Theme** and pick **Light**: the whole app switches instantly — no restart — to a clean black-on-white look that mirrors the dark design exactly: solid white surfaces, solid black text and icons, and the same single-accent style (highlighted buttons like **Generate ModsPack** become black with white text). Your choice is remembered, and the app opens straight into it with no dark flash. Nothing changes unless you switch: Dark remains the default, pixel-for-pixel identical to before. Artwork stays readable too — hero names and controls that sit on top of images keep their light-on-dark styling in both themes, and the console's colored log lines were re-inked so they're just as easy to read on white.

**Updates no longer make you re-download the whole app.** Until now, every new version meant fetching the full installer again — the same ~200 MB, even when the actual change was a bug fix in a single file. From now on, the update popup offers **Update Now**, which downloads *only the files that actually changed* — typically **a few megabytes instead of hundreds** — verifies each one, and restarts into the new version by itself. The reason it can be this small: most of what ships with the app is the .NET runtime it runs on, and that part is identical between releases, so there is no reason to send it to you again. Your existing download links are untouched and still there for anyone who wants them, and they remain the fallback for anything the quick update can't do. Nothing is applied unless every downloaded file passes an integrity check, and if the update fails part-way, your installation is put back exactly as it was and the app restarts normally.

**One catch, and it only applies once:** this version cannot update *itself* the new way — you'll install it the usual way, one last time. Every version after this one updates in seconds.

---

## 🌟 Highlights

- **Update Now — small, verified, automatic** (2234): The "Update Available" popup now leads with an **Update Now** button showing exactly how much it needs to download (e.g. *"Only 2.4 MB — downloads just the changed files"*). Press it and the app fetches those files, checks each one against a published fingerprint, closes, swaps them in, and reopens on the new version. No browser, no installer, no clicking through setup.
- **Nothing changes if you'd rather not** (2234): The **CDN Server** and **Download from Website** links are exactly where they were. If the quick update isn't available for a release, or anything goes wrong, they're what you'll see — the app never leaves you without a way to update.
- **A failed update can't leave you broken** (2234): Before anything is replaced, the app makes a copy of each original file. If any step fails — a locked file, antivirus, a full disk — everything is put back and the app restarts on the version you already had. If it's interrupted by a crash or power loss, it repairs itself on the next launch.

---

## ✨ Added

- **Light theme** (2235): **Settings → Appearance → Theme → Light**. Pure black-on-white across every window, card, modal, and page — applied live, remembered across restarts, available in all 8 languages. Dark stays the default.
- **Incremental updates** (2234): The app compares its own files against a fingerprint list published with each release and downloads only the differences. Available for **installed** versions; the portable build continues to use the zip download.
- Available in all 8 languages.

---

# Release Notes — v2.2.14-beta

> Build **2220-2232**

This build fixes a startup freeze and cleans out the app's updaters. If your internet was slow or the download servers were unreachable, the app used to sit with **every button greyed out for up to three minutes** right after finding your Dota 2 folder — that was a background check for a newer mod pack, running before it let you touch anything. It now runs quietly in the background: the buttons work immediately, and the check gives up quickly instead of hanging. If a newer pack is found, you're offered it just as before — and now that offer also reaches people who already had their Dota 2 folder saved, who previously never saw it at all. Two more: a crash or forced shutdown during an install could leave the app thinking your mod pack was up to date when the pack itself hadn't finished being built (it now only records "installed" once the pack really is), and if the **Download** button in the update popup ever failed to open your browser, it did so in complete silence — it now tells you. This build hardens **Disable Mods**. Disabling reverts two of Dota's own files, and the app used to change the first one *before* fetching what it needs to restore the second — so if the download servers were unreachable (the most common way it fails), you were left half-way: one file reverted, the other still modded, which is exactly the broken state the app warns you about. Everything is now fetched *before* anything is written, so a failure leaves your game untouched. Two more: a disable that failed part-way would still report success — and, if you'd chosen **Delete Permanently**, go ahead and delete your mod files anyway — and a failed disable said nothing at all, leaving the status light on **Ready** as if you'd never clicked. Failures are now reported, and no leftover temporary files are left in your Dota 2 folder. The restart prompt after deleting your mods is also no longer a plain Windows box in English only — it's an in-app card in your language, and you can now choose **Later**. This build also gives the **mod status** machinery a deep clean: the logic that decides whether your mods are Ready, need a Patch Update, or are in a risky broken-patch state is unchanged, but a pile of never-used code around it was removed, the app now runs a single shared status checker instead of three separate copies, and — for the first time — every one of those status decisions is locked in by automated tests, including the "invalid patch" detection that protects you from a VAC-risk state. One visible fix: the orange **"Update Detected"** status shown when Dota 2 updates was always in English; it now appears in your selected language. This build also makes **Patch Update** verify what it installs. Patching tells Dota 2 to trust one specific game-config file, identified by its exact fingerprint — but the app was installing whatever the download server handed back without ever checking that fingerprint. A stale or corrupted copy would leave Dota trusting a file that no longer matched, which is exactly the broken "invalid patch" state the app warns about. The file is now checked before it's installed, and a server returning an out-of-date copy is skipped in favour of another. A failed Patch Update also no longer leaves a stray temporary file behind in your Dota 2 folder. It also polishes the main window: switching language no longer blanks out your version and mod-status labels, the setup guide's step markers are visible again, notifications stay put while you read them, and — the one that mattered — pressing Enter on a **Cancel** button no longer does the opposite of what you asked. It also hardens Miscellaneous mod generation after reports of it getting stuck or misbehaving. The most important fix: generating Misc mods could, in rare cases (Dota 2 or antivirus holding the file), **destroy your installed mod package** instead of replacing it — it now backs up the current package first and only replaces it once the new one is safely ready, so a locked file now just tells you to close Dota 2 and try again instead of losing anything. The mode-selection popup could also leave you stuck for up to a minute — or clicking a button that quietly did nothing — if you dismissed it a certain way; that's fixed. And when generation *does* fail, the **Show Log** button now actually works, so you can see what happened instead of guessing. It also adds a **Show Log** button to the Skin Selector's "Generation Complete" card — a short, readable summary of what was downloaded, verified, and installed, safe to share when you ask for help, now shown in the same in-app style as everywhere else instead of a separate popup window — plus a fix for a serious Install ModsPack bug and faster installs. The install bug: if you started an install **with Dota 2 still open**, a later failure could **delete** your working mod pack instead of restoring it — the safety net that backs up your current install silently gave up when the game held the file locked, then the undo removed the only copy. It now refuses to delete anything it couldn't first back up. Installs are also quicker — the downloaded pack is no longer checksum-verified twice, and applying hero data no longer wastes seconds in tiny sleeps. And a stalled "Checking version…" can no longer hang for five minutes.

---

## 🐛 Fixed

- **HLExtract failed on fresh Windows installs** (2232): the app's VPK extraction tool (`HLExtract.exe`) needs the Visual C++ 2010 runtime, which isn't installed on clean Windows 10/11 systems. Without it, you'd get *"Failed to extract pak01_dir.vpk using HLExtract"* with no hint that a missing DLL was the real cause. The required runtime DLLs are now bundled directly with the app — no extra installs needed.
- **Every button was greyed out for up to 3 minutes on a slow connection** (2231): right after the app found your Dota 2 folder, it checked online for a newer mod pack — and waited for that check to finish before letting you click anything. On a bad network, or when the download servers were down, that meant minutes of a frozen-looking app with no spinner and no way to cancel. The check now runs in the background: the buttons work straight away, and the check itself gives up quickly instead of grinding through three servers. If a newer pack is found, you still get the same offer to update.
- **The mod pack update offer never reached people who already had their folder saved** (2231): the check only ran when you used **Auto Detect** or **Manual Detect** — so if the app remembered your Dota 2 path from last time (nearly everyone with mods installed), it never once told you a newer pack was out. It does now.
- **A crash mid-install could make the app think your pack was up to date when it wasn't** (2231): the app recorded "this version is installed" *before* it finished building the pack. If the app was killed or crashed in between, it would later see that record, compare it against the server, and report **up to date** — on an install that never completed. It now only records the version once the pack is actually built and in place.
- **The Download button in the update popup could fail silently** (2231): if it couldn't open your browser, nothing happened at all — no message, no error, just a button that appeared to do nothing. It now tells you when it fails.

- **Disabling mods could leave your game half-reverted** (2230): turning mods off restores two of Dota's own files, but the app changed the first one **before** downloading the clean copy it needs for the second. If it couldn't reach the download servers — the most common way this fails — you were left with one file reverted and one still modded: the broken combination the app flags as a risky patch state. It now downloads everything first, so a failure leaves your game files completely untouched.
- **A failed disable could still say it worked — and then delete your mods** (2230): if the app couldn't write the restored game file (Dota 2 or antivirus holding it open), it reported success anyway while your mods stayed active. If you had chosen **Delete Permanently**, it went on to delete your mod files on the strength of that false success. It now falls back to a safe alternative, and only reports success when your mods really are off.
- **A failed disable told you nothing** (2230): the status light simply stayed on **Ready**, so a disable that didn't work looked exactly like one you never clicked. Failures now show a message — including the case where mods are switched off but the files can't be deleted because Dota 2 is still running.
- **Disabling no longer leaves stray temporary files** in your Dota 2 folder if it fails partway (2230).

- **A failed Patch Update left a leftover file in your Dota 2 folder** (2228): if the patch couldn't reach the download server — the most common way it fails — it left a stray temporary file behind in the game folder. The patch now prepares and downloads everything *before* it writes anything, so a failure leaves your game files completely untouched.
- **Patch Update didn't show its warning colour when something was wrong** (2228): when the app detected a bad patch state, the sidebar button stayed its normal colour instead of turning red. It now shows it.

- **Pressing Enter on "Cancel" could do the opposite** (2227): in the confirmation popup, Enter always triggered **Continue** — even when you had tabbed to **Cancel** and it was the highlighted button. Enter now does what the focused button says.
- **Changing the language blanked your version and mod status** (2227): switching language in Settings reset the version number at the bottom of the sidebar and the mod status label back to their startup placeholders ("Checking…", "Not Checked"), even though the status light stayed correct. Both now keep their real values.
- **The setup guide's step dots were invisible** (2227): the little progress markers on the guide card weren't drawing at all. Fixed.
- **The install result card ignored the Escape key** (2227): you could close it with ✕ or by clicking outside, but not with Escape like every other popup. Escape now closes it, and Enter closes it too.

- **Generating Miscellaneous mods could destroy your installed mod package** (2224): in rare cases — Dota 2 or antivirus holding the file — a failed replace could wipe out your working package with nothing to restore. The app now backs up the current package before replacing it, so a locked file now just tells you to close Dota 2 and try again instead of losing your mods.
- **The mode-selection popup could get stuck, or silently ignore your click** (2224): dismissing it a certain way, or waiting too long, could leave the app hanging for up to a minute — or eat your next click with nothing happening. Both are fixed.
- **"Show Log" on a failed Misc generation didn't actually show anything** (2224): the button was only offered on success, so a failure gave you no way to see what happened. It now works on failures too.
- **"Generate Only" mode could look frozen on first run and hide real problems** (2224): a first-time download now tells you it's in progress instead of sitting silently, and problems that used to fail silently (a locked file, a failed game-file update) are now reported instead of hidden behind a false "success."

---

## 🎨 Improved

- **The restart prompt after deleting your mods is in your language** (2230): it was a plain Windows box, always in English, with a single **OK** you couldn't decline. It's now an in-app card like every other dialog, translated in all 8 languages, and you can choose **Restart Now** or **Later**. The Disable Options dialog also shows the proper Ardysa logo in its title bar.

- **"Update Detected" now speaks your language** (2229): the orange status shown when the app notices a Dota 2 update was hard-coded in English; it's now translated in all 8 supported languages.
- **The mod status checker got a spring clean** (2229): a large amount of status-checking code that never actually ran was removed, and the app now uses one shared status checker instead of three separate copies. Nothing changes in what you see — but every status decision (Ready, Update Required, Disabled, Not Installed, and the broken-patch warning that guards against a VAC-risk state) is now covered by automated tests, so future updates can't silently break them.

- **Notifications wait while you read them** (2227): the small pop-up cards in the corner disappeared on a fixed timer even if you were mid-sentence. Hovering one now keeps it on screen; it dismisses shortly after you move away.
- **The banner no longer skips ahead while the window is hidden** (2227): minimise the app and come back, and the banner used to have moved on without you. It now pauses while the window isn't visible, and a banner image that fails to load is simply left out instead of showing a broken-image icon.

- **Skin Selector's "Show Log" now looks like the rest of the app** (2225): the button (added last build) opened in a separate popup window with its own look. It now opens inline, in the same style as the Miscellaneous log — no functional change, just a consistent look everywhere you can view a log.

- **A failed install could delete your mod pack if Dota 2 was open** (2220): Before an install, the app moves your current mod pack aside so it can restore it if anything goes wrong. If Dota 2 was running and holding that file open, the "move aside" quietly failed — and then, if the install itself failed, the cleanup deleted the file with no backup to put back, even though the app said your previous install was restored. Now the app only ever deletes or restores files it actually managed to back up first, so your working install is never destroyed. (Best practice is still to close Dota 2 before installing.)
- **"Checking version…" could hang for up to 5 minutes** (2220): The step that looks up the latest ModsPack had no time limit of its own, so a slow or stalled server could freeze the very start of an install. It now gives up after 60 seconds and cancelling is instant.

---

## ⚡ Faster

- **Installs verify the download once, not twice** (2220): The downloaded package is checksum-verified the moment it arrives; the app used to then re-check the same multi-hundred-megabyte file a second time for nothing. That redundant pass is gone, shaving several seconds off every install.
- **Applying hero data no longer stalls in tiny sleeps** (2220): Building your pack applies data one hero item at a time, and each step paused for a sliver that Windows rounds up to ~15 ms — which added up to seconds on a full pack. That pause is now effectively free. (Also speeds up Skin Selector generation, which shares the same step.)

---

## 🌟 Highlights

- **Hero set files are now protected on our servers** (2219): Until now, the mod files behind every hero set sat on our CDN as ordinary zips — anyone with a link could grab them without the app. They're now encrypted, and only ArdysaModsTools can unpack them. Nothing changes for you: sets download, verify and apply exactly as before.
- **Miscellaneous mods repair a damaged package by themselves** (2218): If your installed mod package gets damaged — an interrupted write, a bad disk, antivirus chewing on it — generating Miscellaneous mods with **Add to Current** used to just fail and tell you to go re-run it yourself in "Generate Only" mode. Now it detects the damage, **rebuilds the package from a clean base with your selections applied**, and finishes successfully. It tells you afterwards that your hero sets need applying again (the damaged package's contents can't be recovered — but nothing is lost that wasn't already gone).
- **"Generation Failed" now tells you what actually failed** (2218): Miscellaneous generation could only ever say "Extraction failed." — no reason, no code, nothing to send us. It turned out the entire feature was writing its diagnostics to nowhere. Failures now name the step that broke, show an error code, and save the full details to `ardysa_fallback.log` for when you ask for help.
- **A failed install can no longer damage your Dota 2 install** (2217): We found — and fixed — a bug where a file operation that failed **part-way** could leave one of Dota's core files (`dota.signatures` or `gameinfo`) **missing** rather than restored, because the very operation that failed was skipped during the undo. Installs now roll back completely and safely, whether they fail, are cancelled, or are interrupted mid-write. The manual install also patches both core files **together** now — previously a failure could leave one file updated and the other not, which quietly broke mod loading.
- **Install results you can actually read** (2215–2216): Installing the ModsPack (Auto or Manual) now ends with a clear in-app card instead of a small toast. On success: a green **Complete** card with **Done** and **Show Log** — press Show Log to see what was installed, including every customized hero ("Hero: Abaddon (#3)") and what was downloaded. On failure: a red card that opens with the reason already visible and a **Copy Log** button for sharing when you ask for help.
- **Failure reasons in plain language** (2216): Instead of "Installation failed. Check the console.", the card now says what actually went wrong and what to do — "Download failed — check your internet connection and try again", "The downloaded package was corrupted — please try again", "Could not unpack the download — check free disk space and antivirus", "verify game files in Steam". And when a failed install rolls back, the card tells you your previous install was restored.

---

## 🔒 Security

- **Patch Update now verifies the game config before installing it** (2228): Patching works by telling Dota 2 to accept one specific game-config file, identified by its exact fingerprint. The app downloaded that file and installed it **without ever checking the fingerprint matched** — so a stale copy on a download server, or a corrupted transfer, could leave Dota trusting a file that no longer matched what it was told to expect. That's the same broken "invalid patch" state the app already warns you about. The downloaded file is now checked against the expected fingerprint *before* it's installed, and a server handing back an out-of-date copy is simply skipped in favour of the next one — so this makes patching safer without making it any more likely to fail. The same check now also covers the manual install path, which patches the same two game files.

- **Protected sets now keep a low profile on disk** (2221–2222): When a Skin Selector generation uses protected (encrypted) sets, the installed mod package and the app's temporary work folders are hidden from Windows Explorer — they stay invisible even with "Show hidden files" turned on. Nothing changes for you: Dota loads the package exactly as before, installs and Miscellaneous generation keep working, and you can still reach the files by typing the folder path or using Windows Search. Generations without protected sets leave everything visible as usual. A follow-up (2222) makes sure Miscellaneous **Add to Current** keeps the package hidden instead of quietly un-hiding it, and that future file operations can always update a hidden package safely.
- **Encrypted hero set assets** (2219): Set archives on the CDN are now AES-256 encrypted and are decrypted by the app only while a set is being applied — the unencrypted copy never stays on your disk. Downloads are still checksum-verified first, so a corrupted or interrupted download is caught exactly like before.

---

## ✨ Added

- **Skin Selector — Show Log after generation** (2223): The "Generation Complete" card now has a **Show Log** button (OK just closes, as always). The log is short on purpose — one line per step: which sets were downloaded or taken from cache, that their integrity was verified, how patching went, and the build/install steps — exactly enough to see where something went wrong. It's also **safe to share**: file paths and download links are trimmed to just the file name, so it never reveals your Windows username, folder layout, or server addresses. On a failed generation you don't need the button — the same log is already included in the error details you copy. Available in all 8 languages.
- **Self-healing Miscellaneous generation** (2218): A damaged mod package is rebuilt from scratch automatically, with your Miscellaneous selections re-applied, instead of failing. You're told when it happens and reminded to re-apply your hero sets.
- **Install complete card with Show Log** (2216): Done + Show Log actions after every successful Auto/Manual install or reinstall; the log lists each step and hero in simple terms — no technical noise.
- **Install failure card** (2215): Failed installs open a card with the failure log and one-click **Copy Log**, in all 8 languages.

---

## 🐛 Fixed

- **Miscellaneous generation gave the same useless error for every problem** (2218): Whatever went wrong — a corrupted package, a missing tool, a wedged unpacker — you got "Generation Failed / Extraction failed." and nothing else. Every failure now names the step that broke, shows an error code, and writes the real details (including the unpacking tool's own output) to `ardysa_fallback.log`.
- **A failed install could delete a Dota 2 core file instead of restoring it** (2217): The safety net that undoes a failed install skipped the exact step that failed — so if a write broke half-way through, the file it was replacing could end up **gone**. Fixed, along with two related holes: undoing twice could itself delete the restored file, and cancelling between steps left earlier changes applied. Covered by a new regression test.
- **Manual install could half-patch the game** (2217): The two Dota core files are now updated as one all-or-nothing step. Before, a failure after the first file was written left your game with one new file and one old one — mods would silently fail to load.
- **Cancelling an install said "Installation Failed"** (2217): Closing the progress window now shows a neutral "Installation Canceled" message instead of a red failure card, and confirms your previous setup is untouched.
- **Double-clicking Install could start two installs at once** (2217): Install and Disable now ignore a second click while the first is still running — including while the install-method dialog is open, which is where the duplicate used to slip through.
- **Installs no longer hang forever** (2217): If the packaging tool wedges, the install now gives up after a bounded wait instead of freezing with no way out.
- **Corrupt downloads are caught earlier** (2217): The downloaded ModsPack is now verified against its published checksum *before* it's put into place, so a bad copy from a CDN is rejected rather than unpacked.
- **Performance Tweak — Medium preset turning the map black on hover** (2214): The Medium preset combined a shadow setting that doesn't work with its shadow-texture size, so hovering over a hero, creep, or any NPC could black out the entire map. Medium now uses the same safe shadow setting as the Competitive preset — same performance, no more black screen. **If you already applied Medium, re-apply it (or press DELETE) on the Performance tab to clear the old setting from your config.**

---

# Release Notes — v2.2.12-beta

> Build **2213**

Your Dota 2 folder is now a first-class setting: a new **Dota 2** card in Settings shows exactly where the app is pointed, with a one-click **Change** button and an **Auto-detect** switch. On a fresh install the app now finds Dota 2 for you on first launch, and a quick green "Dota 2 path found" confirmation shows you the folder it attached.

---

## 🌟 Highlights

- **See and change your Dota 2 folder in Settings** (2212): A new **Dota 2** section shows the current install path (with the important tail always visible) and a **Change** button that opens the folder picker right at your current path — pick a new "dota 2 beta" folder and everything re-syncs instantly. Handy if you have multiple installs or moved the game.
- **Finds Dota 2 for you on first launch** (2212): When no path is saved yet, the app auto-detects your Dota 2 install at startup instead of waiting for you to press **Auto Detect** — controlled by a new **Auto-detect Dota 2 Path** toggle (on by default). Your own saved or hand-picked path is never overridden.
- **Clear confirmation of the attached folder** (2212): A brief green "Dota 2 path found" banner shows the exact folder whenever the path is detected or changed, so you always know what the tools are pointed at.

---

## ✨ Added

- **Dota 2 install-path card** (2212): Current path, **Change** button (picker opens at the current folder), and an **Auto-detect Dota 2 Path** toggle — all in a new Dota 2 section at the top of Settings. Available in all 8 languages.
- **Startup auto-detect** (2212): First launch attaches your Dota 2 install automatically when no path is saved.

---

## 🛠️ Changed

- **Auto Detect grays out once a folder is attached** (2212): After the app has a valid Dota 2 path, **Auto Detect** is done and turns non-clickable — while **Manual Detect** stays available so you can always re-point to a different install.
- **A clearer message when Dota 2 isn't found** (2212): If startup auto-detect comes up empty, a warning now points you at the highlighted detect buttons instead of failing silently.

---

## 📝 Documentation

- **Added `CLAUDE.md` contributor/agent guide** (2213): Repo-root guide capturing the AMT ground rules — MVP layering, the new-service sequence, DI-only, mandatory tests, the file-safety subsystem rules (`FileTransactionService`, `ILogService`, CDN fallback order, Security), `[AMT:TIER]` annotations, grounding/anti-hallucination, and the commit format. No code change.

---


# Release Notes — v2.2.11-beta

> Build **2211**

Manual install now verifies where a pack comes from before installing it: official packs install as before, while packs from other creators — previously rejected — are accepted with a notice. Verification is instant and fully offline. Plus the portable build no longer falsely asks for the WebView2 runtime, install now recovers on its own from a corrupted download, and WebView2 profile self-healing from build 2204.

---

## 🌟 Highlights

- **Origin verification, instant and offline** (2207): Official status is decided from the pack itself with no network call and no waiting — the check runs off a single fast content listing. A shared pack you generated yourself no longer gets flagged with a scary warning; it's simply treated as your own pack and installs on confirmation.
- **Manual install tells you what you're installing** (2205): When you pick a `pak01_dir.vpk`, the app checks its origin. Official packs install exactly as before, and packs from other creators — previously rejected — are now **accepted** with an **"Unofficial pack"** notice that makes clear bugs from such packs are not ArdysaMods' responsibility. Nothing installs without your confirmation.
- **Third-party packs install untouched** (2205): Packs without the ArdysaMods signature are installed as-is — the app no longer rejects them, and it doesn't overwrite their data with ArdysaMods package data either.
- **Corrupted WebView2 profile auto-repairs** (2204): If the browser profile gets corrupted or locked (e.g. after a crash or force-close), the app now resets it automatically on the next launch instead of throwing an error.

---

## ✨ Added

- **Offline origin verification** (2207): The origin check no longer contacts the server at all — it reads the pack's own contents in a single fast pass, so it's instant whether you're online or off. A pack you generated yourself is recognised as your own and installs without an alarming "modified" warning.
- **VPK origin check on manual install** (2205): The verdict decides the flow: official → rebuild pipeline; unofficial → warning, installed as-is. Available in all 8 languages.
- **Line-ending normalization** (2204): All source files standardized to LF line endings across the repository.

---

## 🐛 Fixed

- **No more false "WebView2 timed out / reinstall the runtime" error** (2210): On slower connections the app could show a "navigation timed out — please reinstall the WebView2 runtime" error even though WebView2 was working fine — it was waiting for background images to finish downloading before considering the window ready. It now treats the window as ready as soon as the interface itself has loaded, so slow-loading images no longer block startup or trigger a bogus reinstall prompt.
- **Portable no longer asks for WebView2 when it's already installed** (2209): The portable version could wrongly report the WebView2 runtime as missing — or fail to start it — even when it was present. The portable build is now fully self-contained (no separate .NET runtime needed) and keeps the WebView2 loader beside the app, and startup now double-checks the registry before ever claiming the runtime is missing. When something does go wrong, the real reason is written to `startup_log.txt` and the error message walks you through Unblock → antivirus → reinstall.
- **Install recovers from a corrupted download** (2208): If `Original.zip` downloaded incompletely, the install used to fail on every retry and needed a manual cache wipe to unstick. It now clears the bad download automatically and re-fetches a clean copy on the next run.
- **Copied signature file no longer passes as official** (2207): The public `version/_ArdysaMods` file is now only a decoy — copying it into another pack grants nothing. Official status is decided by a separate, non-obvious marker that isn't advertised, so a hand-assembled pack can't fake it.
- **Fake-signature packs can no longer pass as official** (2205): The origin check requires an exact match at an exact location — packs with lookalike file names no longer slip through.
- **WebView2 won't start after a crash** (2204): `CoreWebView2Environment.CreateAsync()` throws when the EBWebView profile is corrupted or stale-locked. The helper now catches the failure, renames the bad profile folder, and retries — so a one-time reset fixes what used to require manual folder deletion.

---

# Release Notes — v2.2.10-beta

> Build **2203**

Install robustness and consistency pass: failed or cancelled installs now **restore your previous working mod pack** instead of deleting it, and every install — fat or slim — goes through the same rebuild pipeline so the output is deterministic.

---

## 🌟 Highlights

- **No more false "install WebView2" prompt**: Setup no longer asks you to install the WebView2 runtime when it's already on your PC — it now checks for it exactly the way the app does, so the two can't disagree.
- **Failed installs restore what you had**: If a mod install or rebuild fails (or you cancel it), the previous working `pak01_dir.vpk` is restored instead of being deleted. You keep your mods while retrying.
- **Nothing gets cut off at larger UI sizes**: On smaller screens, choosing a bigger **UI Size** could crop pages, dialogs, and buttons. The whole interface now scales to fit the screen, and anything still too tall scrolls instead of clipping.
- **Drag-and-drop a VPK actually works**: Dropping a `.vpk` onto the manual-install zone was showing a "blocked" cursor. It now accepts the file (and keeps working even when the app is running as administrator).
- **Live progress while building the ModsPack**: The "Building game data…" step used to sit on one line for a long time and look stuck. It now advances a real progress bar with an `Applying package data… N%` readout as it works through the package and language files.
- **Every install starts clean**: Both legacy "fat" packs and modern slim packs now go through the same strip → rebuild pipeline, guaranteeing a deterministic, up-to-date package regardless of what was downloaded.
- **The installer can't delete the wrong folder**: Choosing a custom location now always installs into an `ArdysaModsTools` subfolder, and the uninstaller refuses to recursively delete drive roots or common folders like Desktop and Documents — even if something points it there.
- **A failed update won't wipe your working install**: The old version is only removed after the new files are unpacked and verified, so a corrupt or interrupted download leaves your existing install untouched.
- **Quiet uninstall is actually quiet**: Removing the app from Windows "Add or remove programs" no longer briefly flashes the installer window, and the uninstaller now reliably cleans itself up afterward.

---

## ✨ Added

- **Install ModsPack can be paused for maintenance**: Like Skin Selector and Miscellaneous, the Install action can now be temporarily turned off from the server. If it's off you'll see a notice with a **Join Discord** button for updates.
- **Utilities (coming soon)**: A new **Utilities** entry appears in the sidebar under a new **Extra** group, shown greyed-out with a `SOON` tag until the page is ready.

## 🐛 Fixed

- **"WebView2 is required" when you already have it**: The installer used to check for the WebView2 runtime differently than the app, so it could offer to install one that was already there. It now uses the same check as the app, ending the false prompt. (No change to what gets downloaded — it still uses Microsoft's small WebView2 setup only when the runtime is genuinely missing.)
- **Failed install restores previous mod pack**: Canceling or a failed rebuild now restores the previously working `pak01_dir.vpk` and hash marker instead of deleting them, so you don't lose your mods on a retry.
- **Stale data in legacy packs**: Legacy "fat" mod packs that bundled their own `items_game.txt` and localization are now stripped and repacked with the current game data, fixing cases where an old pack's stale `items_game.txt` survived into the installed VPK.
- **Interface cropped at larger UI sizes**: Pages, dialogs, and cards no longer get cut off when UI Size is set to Large or Extra Large — the app scales to fit and scrolls when needed.
- **Manual VPK drag-and-drop**: Dropping a `.vpk` file onto the manual-install zone now works (previously showed a blocked cursor), including when the app runs elevated.
- **Install progress looked frozen**: The game-data build step now reports a live percentage across its phases instead of a single static line.
- **Installer could remove the wrong folder**: Custom paths are kept inside an app subfolder, and every delete step is guarded against drive roots and well-known user folders.
- **Failed update could remove a good install**: Cleanup of the old version now happens only after the new one is verified.
- **Silent uninstall flashed a window / didn't fully clean up**: Fixed the quiet uninstall UI flash, a crash when launching a second installer, and the leftover uninstaller after removal.

## 🛠️ Changed

- **Install — always rebuild, no more fat-vs-slim branch**: Every install now follows the same strip → rebuild pipeline regardless of pack type. The `VpkBundlesItemsGameAsync` shortcut is removed.
- **Clearer "Dota 2 is running" message**: The banner now reads "Dota 2 is still running — tools are unavailable until you close the game."
- **Quieter build log**: The game-data build no longer logs internal file paths or block counts.

---

_For the full per-build breakdown, see [CHANGELOG.md](CHANGELOG.md)._
