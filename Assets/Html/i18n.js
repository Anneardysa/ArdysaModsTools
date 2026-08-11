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

(function () {
   window.__locale = window.__locale || {};
   window.__localeFallback = window.__localeFallback || {};

   function lookup(key) {
      if (key == null) return key;
      if (Object.prototype.hasOwnProperty.call(window.__locale, key)) return window.__locale[key];
      if (Object.prototype.hasOwnProperty.call(window.__localeFallback, key)) return window.__localeFallback[key];
      return key;
   }

   function interpolate(template, vars) {
      if (!template || !vars) return template;
      return template.replace(/\{(\w+)\}/g, function (m, name) {
         return Object.prototype.hasOwnProperty.call(vars, name) ? String(vars[name]) : m;
      });
   }

   window.t = function (key, vars) {
      return interpolate(lookup(key), vars);
   };

   window.tp = function (key, count, vars) {
      var suffix = count === 1 ? "one" : count === 0 ? "zero" : "other";
      var merged = Object.assign({ count: count }, vars || {});
      var hasZero =
         suffix !== "zero" ||
         Object.prototype.hasOwnProperty.call(window.__locale, key + ".zero") ||
         Object.prototype.hasOwnProperty.call(window.__localeFallback, key + ".zero");
      var fullKey = key + "." + (hasZero ? suffix : "other");
      return interpolate(lookup(fullKey), merged);
   };

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

   window.setLocale = function (active, fallback) {
      window.__locale = active || {};
      if (fallback) window.__localeFallback = fallback;
      updateCjkFlag();
      applyI18n();
      window.dispatchEvent(new CustomEvent("amt:locale"));
   };

   var FADE_STYLE_ID = "i18n-fade-style";
   var FADE_HOLD_MS = 4000;
   var FADE_DUR_MS = 400;
   var CJK_RE = /[㐀-鿿豈-﫿]/;
   var fadeRegistry = [];
   var fadeTimer = null;
   var fadeGen = 0;
   var fadeWasHidden = false;
   var fadeShowingEn = false;
   var fadeNextAt = 0;
   var fadeFlipping = false;

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

   function teardownButtonFade() {
      fadeGen++;
      if (fadeTimer) { clearInterval(fadeTimer); fadeTimer = null; }
      fadeFlipping = false;
      fadeShowingEn = false;
      fadeRegistry.forEach(function (r) {
         r.el.removeAttribute("data-i18n-loc");
         r.el.removeAttribute("data-i18n-en");
         r.el.textContent = window.t(r.el.getAttribute("data-i18n"));
      });
      fadeRegistry = [];
   }

   function fadeFlipAll() {
      var gen = fadeGen;
      fadeFlipping = true;
      fadeRegistry.forEach(function (r) { r.span.style.opacity = "0"; });
      setTimeout(function () {
         if (gen !== fadeGen) return;
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
      if (document.hidden) { fadeWasHidden = true; return; }
      var now = Date.now();
      if (fadeWasHidden) {
         fadeWasHidden = false;
         fadeNextAt = now + FADE_HOLD_MS;
         return;
      }
      if (!fadeFlipping && now >= fadeNextAt) fadeFlipAll();
   }

   function setupButtonBilingualFade() {
      teardownButtonFade();
      if (fadeReducedMotion()) return;
      document.querySelectorAll("[data-i18n]").forEach(function (el) {
         var key = el.getAttribute("data-i18n");
         if (key == null || key.lastIndexOf("shell.nav.", 0) !== 0) return;
         if (!el.closest || !el.closest("button")) return;
         var loc = window.t(key);
         if (!Object.prototype.hasOwnProperty.call(window.__localeFallback, key)) return;
         var en = window.__localeFallback[key];
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
      fadeShowingEn = false;
      fadeNextAt = Date.now() + FADE_HOLD_MS;
      fadeTimer = setInterval(fadeTick, 200);
   }

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

      root.querySelectorAll(".log-line[data-i18n-log]").forEach(function (line) {
         var segs;
         try { segs = JSON.parse(line.getAttribute("data-i18n-log")); } catch (e) { return; }
         var msg = line.querySelector(".msg");
         if (msg) msg.textContent = window.renderLogSegments(segs);
      });

      setupButtonBilingualFade();
   };
})();
