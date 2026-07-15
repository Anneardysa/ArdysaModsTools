/*
 * Copyright (C) 2026 Ardysa
 *
 * This program is free software: you can redistribute it and/or modify
 * it under the terms of the GNU General Public License as published by
 * the Free Software Foundation, either version 3 of the License, or
 * (at your option) any later version.
 *
 * This program is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 * GNU General Public License for more details.
 *
 * You should have received a copy of the GNU General Public License
 * along with this program.  If not, see <https://www.gnu.org/licenses/>.
 */

/*
 * Shared WebView i18n helper. The C# host injects the active + English-fallback locale maps via
 * setLocale({...}, {...}) right after navigation, then calls applyI18n(). Translation keys and the
 * {token} interpolation syntax mirror the C# LocalizationService exactly so the JSON catalog under
 * Assets/Locales/ is a single source of truth.
 *
 * Declarative markup attributes scanned by applyI18n:
 *   data-i18n="key"              -> element.textContent
 *   data-i18n-html="key"         -> element.innerHTML  (use only for trusted catalog markup, e.g. &nbsp;)
 *   data-i18n-placeholder="key"  -> element.placeholder
 *   data-i18n-title="key"        -> element.title      (tooltips)
 *   data-i18n-aria-label="key"   -> aria-label
 * Optional data-i18n-vars='{"name":"value"}' supplies interpolation tokens for that element.
 */
(function () {
   window.__locale = window.__locale || {};
   window.__localeFallback = window.__localeFallback || {};

   function lookup(key) {
      if (key == null) return key;
      if (Object.prototype.hasOwnProperty.call(window.__locale, key)) return window.__locale[key];
      if (Object.prototype.hasOwnProperty.call(window.__localeFallback, key)) return window.__localeFallback[key];
      return key; // surface the key rather than blank when missing.
   }

   function interpolate(template, vars) {
      if (!template || !vars) return template;
      return template.replace(/\{(\w+)\}/g, function (m, name) {
         return Object.prototype.hasOwnProperty.call(vars, name) ? String(vars[name]) : m;
      });
   }

   // t(key) or t(key, { name: value }) — translate + interpolate.
   window.t = function (key, vars) {
      return interpolate(lookup(key), vars);
   };

   // tp(key, count) or tp(key, count, vars) — pluralize (.zero/.one/.other) + interpolate {count}.
   window.tp = function (key, count, vars) {
      var suffix = count === 1 ? "one" : count === 0 ? "zero" : "other";
      var merged = Object.assign({ count: count }, vars || {});
      // Fall back to .other when an authored .zero is absent.
      var hasZero =
         suffix !== "zero" ||
         Object.prototype.hasOwnProperty.call(window.__locale, key + ".zero") ||
         Object.prototype.hasOwnProperty.call(window.__localeFallback, key + ".zero");
      var fullKey = key + "." + (hasZero ? suffix : "other");
      return interpolate(lookup(fullKey), merged);
   };

   /* Renders a keyed console log line from its segments. Each segment is either a literal string or
    * a translation token { k: key, v: vars }. Shared by appendLogI18n (main_shell.html) and the
    * re-translate pass in applyI18n so console history follows a language switch. Mirrors the C#
    * LogSegment serialization in Logger.cs. */
   window.renderLogSegments = function (segs) {
      if (!Array.isArray(segs)) return "";
      var out = "";
      for (var i = 0; i < segs.length; i++) {
         var s = segs[i];
         if (typeof s === "string") out += s;
         else if (s && s.k) out += window.t(s.k, s.v || null);
      }
      return out;
   };

   // Replaces the active + fallback maps (called by the C# host).
   window.setLocale = function (active, fallback) {
      window.__locale = active || {};
      if (fallback) window.__localeFallback = fallback;
      updateCjkFlag();
      applyI18n();
   };

   /* ════════ Bilingual nav-button fade (main_shell left bar, any non-English UI) ════════
    * Restricted to the seven left-sidebar nav buttons in main_shell (keys "shell.nav.*": Auto Detect
    * … Performance Tweak); every other button in the app is excluded by that key prefix. Whenever the
    * UI is in a non-English language each nav button gently crossfades its localized label to the
    * English original (window.__localeFallback[key]) and back on a slow, synchronized loop — so it
    * works the same for Spanish, German, French, Portuguese, Russian and both Chinese scripts. An
    * English UI (or any label kept in English) is inert because the localized text equals the fallback.
    * The fade animates an inner wrapper span — never the button's own opacity — so disabled-button
    * styling is untouched. Lives in this shared helper. */
   var FADE_STYLE_ID = "i18n-fade-style";
   var FADE_HOLD_MS = 4000; // how long each language is shown before crossfading
   var FADE_DUR_MS = 400;   // opacity transition length
   var CJK_RE = /[㐀-鿿豈-﫿]/; // CJK ideographs => Chinese text
   var fadeRegistry = [];
   var fadeTimer = null;
   var fadeGen = 0;         // bumped on teardown to abort in-flight crossfades
   var fadeWasHidden = false;
   // Single shared clock so every nav button crossfades in lockstep (one schedule, not per-button).
   var fadeShowingEn = false; // true => English original showing; false => localized label. Same for all buttons
   var fadeNextAt = 0;        // timestamp of the next synchronized flip
   var fadeFlipping = false;  // a crossfade is mid-flight

   // Flag <html data-cjk> while the active locale contains Chinese, so the page can swap in a CJK-capable
   // font fallback (the JetBrains Mono stack has no Chinese glyphs). Re-evaluated on every setLocale, so a
   // live switch back to a non-Chinese language clears it. Cheap one-pass scan of the active map.
   function updateCjkFlag() {
      var hasCjk = false;
      for (var k in window.__locale) {
         if (Object.prototype.hasOwnProperty.call(window.__locale, k) && CJK_RE.test(window.__locale[k])) {
            hasCjk = true;
            break;
         }
      }
      var de = document.documentElement;
      if (!de) return;
      if (hasCjk) de.setAttribute("data-cjk", "1");
      else de.removeAttribute("data-cjk");
   }

   function ensureFadeStyle() {
      if (document.getElementById(FADE_STYLE_ID)) return;
      var st = document.createElement("style");
      st.id = FADE_STYLE_ID;
      st.textContent = ".i18n-fade{transition:opacity " + FADE_DUR_MS + "ms ease;will-change:opacity}";
      (document.head || document.documentElement).appendChild(st);
   }

   function fadeReducedMotion() {
      return !!(window.matchMedia && window.matchMedia("(prefers-reduced-motion: reduce)").matches);
   }

   // Rebuild from scratch: tear down timers/spans so re-applies and switching away from Chinese leave
   // no residue and every button shows its plain Chinese label again.
   function teardownButtonFade() {
      fadeGen++;
      if (fadeTimer) { clearInterval(fadeTimer); fadeTimer = null; }
      fadeFlipping = false;
      fadeShowingEn = false;
      fadeRegistry.forEach(function (r) {
         r.el.removeAttribute("data-i18n-loc");
         r.el.removeAttribute("data-i18n-en");
         // Drop the wrapper span by re-rendering the element's CURRENT active translation. Using the
         // live key (not the stored localized text) keeps a language switch correct: applyI18n has
         // already set the new text, and partial/subtree re-applies still collapse the span cleanly.
         r.el.textContent = window.t(r.el.getAttribute("data-i18n"));
      });
      fadeRegistry = [];
   }

   // One synchronized crossfade for the whole nav: fade every button out together, swap text, fade in.
   function fadeFlipAll() {
      var gen = fadeGen;
      fadeFlipping = true;
      fadeRegistry.forEach(function (r) { r.span.style.opacity = "0"; });
      setTimeout(function () {
         if (gen !== fadeGen) return; // torn down mid-crossfade
         fadeShowingEn = !fadeShowingEn;
         fadeRegistry.forEach(function (r) {
            r.span.textContent = fadeShowingEn ? r.en : r.loc;
            r.span.style.opacity = "1";
         });
         fadeFlipping = false;
         fadeNextAt = Date.now() + FADE_HOLD_MS;
      }, FADE_DUR_MS);
   }

   function fadeTick() {
      if (document.hidden) { fadeWasHidden = true; return; } // pause in the background
      var now = Date.now();
      if (fadeWasHidden) { // returned to foreground: restart the shared hold so it doesn't flip instantly
         fadeWasHidden = false;
         fadeNextAt = now + FADE_HOLD_MS;
         return;
      }
      if (!fadeFlipping && now >= fadeNextAt) fadeFlipAll();
   }

   function setupButtonBilingualFade() {
      teardownButtonFade(); // idempotent — fully rebuilt on every applyI18n
      if (fadeReducedMotion()) return;
      document.querySelectorAll("[data-i18n]").forEach(function (el) {
         var key = el.getAttribute("data-i18n");
         if (key == null || key.lastIndexOf("shell.nav.", 0) !== 0) return; // left nav bar only
         if (!el.closest || !el.closest("button")) return; // and only when it's a button
         var loc = window.t(key);
         if (!Object.prototype.hasOwnProperty.call(window.__localeFallback, key)) return;
         var en = window.__localeFallback[key];
         // Run for any locale whose label actually differs from the English original. An English UI (or
         // a label deliberately kept in English) has loc === en and is skipped, so the effect is inert.
         if (en == null || en === loc) return;
         var span = document.createElement("span");
         span.className = "i18n-fade";
         span.textContent = loc;
         el.textContent = "";
         el.appendChild(span);
         el.setAttribute("data-i18n-loc", loc);
         el.setAttribute("data-i18n-en", en);
         fadeRegistry.push({ el: el, span: span, loc: loc, en: en });
      });
      if (!fadeRegistry.length) return;
      ensureFadeStyle();
      fadeShowingEn = false;                  // all buttons start showing the localized label together
      fadeNextAt = Date.now() + FADE_HOLD_MS; // first synchronized flip after one shared hold
      fadeTimer = setInterval(fadeTick, 200);
   }

   // Walks the DOM (or a subtree) and fills in every data-i18n* attribute.
   window.applyI18n = function (root) {
      root = root || document;

      function varsFor(el) {
         var raw = el.getAttribute("data-i18n-vars");
         if (!raw) return null;
         try {
            return JSON.parse(raw);
         } catch (e) {
            return null;
         }
      }

      root.querySelectorAll("[data-i18n]").forEach(function (el) {
         el.textContent = window.t(el.getAttribute("data-i18n"), varsFor(el));
      });
      root.querySelectorAll("[data-i18n-html]").forEach(function (el) {
         el.innerHTML = window.t(el.getAttribute("data-i18n-html"), varsFor(el));
      });
      root.querySelectorAll("[data-i18n-placeholder]").forEach(function (el) {
         el.setAttribute("placeholder", window.t(el.getAttribute("data-i18n-placeholder"), varsFor(el)));
      });
      root.querySelectorAll("[data-i18n-title]").forEach(function (el) {
         el.setAttribute("title", window.t(el.getAttribute("data-i18n-title"), varsFor(el)));
      });
      root.querySelectorAll("[data-i18n-aria-label]").forEach(function (el) {
         el.setAttribute("aria-label", window.t(el.getAttribute("data-i18n-aria-label"), varsFor(el)));
      });

      // Re-translate keyed console lines so the log history follows a language switch. Lines logged as
      // raw text (appendLog) have no data-i18n-log attribute and are left untouched.
      root.querySelectorAll(".log-line[data-i18n-log]").forEach(function (line) {
         var segs;
         try { segs = JSON.parse(line.getAttribute("data-i18n-log")); } catch (e) { return; }
         var msg = line.querySelector(".msg");
         if (msg) msg.textContent = window.renderLogSegments(segs);
      });

      // After the catalog text is in place, (re)build the non-English bilingual nav-button fade.
      setupButtonBilingualFade();
   };
})();
