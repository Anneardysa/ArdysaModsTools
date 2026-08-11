using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;

namespace Amt.Probe;

internal static class Program
{
   [STAThread]
   private static void Main(string[] argv)
   {
      var args = Args.Parse(argv);
      ApplicationConfiguration.Initialize();
      Application.Run(new ProbeForm(args));
   }
}

internal sealed record Args(
   string? Page,
   string? File,
   string? Url,
   bool Light,
   string Locale,
   List<string> Exec,
   List<string> ExecLate,
   int Width,
   int Height,
   string? Shot,
   bool Exit,
   bool AuditLight,
   int Wait)
{
   public static Args Parse(string[] a)
   {
      string? page = null, file = null, url = null, shot = null;
      bool light = false, exit = false, auditLight = false;
      string locale = "en";
      var exec = new List<string>();
      var execLate = new List<string>();
      int w = 560, h = 720, wait = 0;

      for (int i = 0; i < a.Length; i++)
      {
         switch (a[i])
         {
            case "--url": url = Next(a, ref i); break;
            case "--file": file = Next(a, ref i); break;
            case "--wait": _ = int.TryParse(Next(a, ref i), out wait); break;
            case "--theme": light = string.Equals(Next(a, ref i), "light", StringComparison.OrdinalIgnoreCase); break;
            case "--locale": locale = Next(a, ref i); break;
            case "--exec": exec.Add(Next(a, ref i)); break;
            case "--exec-late": execLate.Add(Next(a, ref i)); break;
            case "--shot": shot = Next(a, ref i); break;
            case "--exit": exit = true; break;
            case "--audit-light": auditLight = true; break;
            case "--size":
               var parts = Next(a, ref i).Split('x', 'X');
               if (parts.Length == 2 && int.TryParse(parts[0], out var pw) && int.TryParse(parts[1], out var ph))
               {
                  w = pw;
                  h = ph;
               }
               break;
            default:
               page ??= a[i];
               break;
         }
      }

      return new Args(page, file, url, light, locale, exec, execLate, w, h, shot, exit, auditLight, wait);
   }

   private static string Next(string[] a, ref int i) => ++i < a.Length ? a[i] : string.Empty;
}

internal sealed class ProbeForm : Form
{
   private const string HtmlAnchor = "<html lang=\"en\"";
   private const string HeadAnchor = "</head>";

   private readonly Args _args;
   private readonly WebView2 _web = new() { Dock = DockStyle.Fill };
   private readonly string _repoRoot;

   public ProbeForm(Args args)
   {
      _args = args;
      _repoRoot = FindRepoRoot();

      Text = $"AMT probe — {args.Page ?? args.Url}";
      ClientSize = new Size(args.Width, args.Height);
      StartPosition = FormStartPosition.CenterScreen;
      BackColor = args.Light ? Color.White : Color.Black;
      Controls.Add(_web);
   }

   protected override void OnShown(EventArgs e)
   {
      base.OnShown(e);
      _ = InitAsync();
   }

   private async Task InitAsync()
   {
      await _web.EnsureCoreWebView2Async();
      var core = _web.CoreWebView2;

      core.WebMessageReceived += (_, e) => Console.WriteLine($"[postMessage] {e.WebMessageAsJson}");
      _web.CoreWebView2.Settings.AreDevToolsEnabled = true;

      var ready = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
      void OnDom(object? s, CoreWebView2DOMContentLoadedEventArgs e) => ready.TrySetResult(true);
      core.DOMContentLoaded += OnDom;

      if (_args.Url is not null)
      {
         core.Navigate(_args.Url);
      }
      else
      {
         var path = _args.File ?? HtmlPath(_args.Page ?? "about");
         if (!File.Exists(path))
         {
            Console.WriteLine($"[probe] no such page: {path}");
            Close();
            return;
         }
         core.NavigateToString(ApplyTheme(File.ReadAllText(path)));
      }

      await Task.WhenAny(ready.Task, Task.Delay(10_000));
      core.DOMContentLoaded -= OnDom;

      var helper = Path.Combine(_repoRoot, "Assets", "Html", "i18n.js");
      if (File.Exists(helper))
      {
         var active = ReadLocale(_args.Locale);
         var fallback = ReadLocale("en");
         await core.ExecuteScriptAsync(File.ReadAllText(helper) + $"\n;window.setLocale({active},{fallback});");
      }

      foreach (var script in _args.Exec)
         Console.WriteLine($"[exec] {script} -> {await core.ExecuteScriptAsync(script)}");

      var mounted = await core.ExecuteScriptAsync("document.getElementById('root')?.childElementCount ?? -1");
      Console.WriteLine($"[probe] #root children = {mounted}  (0 or -1 means the bundle did not run)");

      if (_args.Wait > 0) await Task.Delay(_args.Wait);

      foreach (var script in _args.ExecLate)
         Console.WriteLine($"[late] {script} -> {await core.ExecuteScriptAsync(script)}");

      if (_args.AuditLight)
      {
         var report = await core.ExecuteScriptAsync(LightModeAudit);
         Console.WriteLine($"[audit-light] {report}");
      }

      if (_args.Shot is not null)
      {
         var path = Path.GetFullPath(_args.Shot);
         Directory.CreateDirectory(Path.GetDirectoryName(path)!);
         await using var fs = File.Create(path);
         await core.CapturePreviewAsync(CoreWebView2CapturePreviewImageFormat.Png, fs);
         Console.WriteLine($"[shot] {path}");
      }

      if (_args.Exit) Close();
   }


   private const string LightModeAudit = @"
(function () {
   function lum(c) {
      var m = (c || '').match(/[\d.]+/g);
      if (!m) return null;
      var a = m.length > 3 ? parseFloat(m[3]) : 1;
      if (a < 0.5) return null;                       // effectively transparent: it shows what is behind
      var f = m.slice(0, 3).map(function (v) {
         v = v / 255;
         return v <= 0.03928 ? v / 12.92 : Math.pow((v + 0.055) / 1.055, 2.4);
      });
      return 0.2126 * f[0] + 0.7152 * f[1] + 0.0722 * f[2];
   }
   function ratio(a, b) { return (Math.max(a, b) + 0.05) / (Math.min(a, b) + 0.05); }
   function path(el) {
      var s = el.tagName.toLowerCase();
      if (el.id) s += '#' + el.id;
      if (el.className && typeof el.className === 'string')
         s += '.' + el.className.trim().split(/\s+/).slice(0, 2).join('.');
      return s;
   }

   var bodyLum = lum(getComputedStyle(document.body).backgroundColor);
   if (bodyLum === null) bodyLum = 1;                  // transparent body over the host's light paint

   var darker = [], lowContrast = [];
   var all = document.querySelectorAll('body *');
   for (var i = 0; i < all.length; i++) {
      var el = all[i];
      var cs = getComputedStyle(el);
      if (cs.display === 'none' || cs.visibility === 'hidden') continue;
      var r = el.getBoundingClientRect();
      if (r.width < 4 || r.height < 4) continue;

      var bg = lum(cs.backgroundColor);
      // Informational only. A dark surface in light mode is often deliberate — the primary button
      // inverts to a black fill, a 'not determined' dot stays grey. What is never deliberate is dark
      // text left on it, which the contrast pass below catches on its own.
      if (bg !== null && bodyLum - bg > 0.15) darker.push(path(el) + ' bg=' + cs.backgroundColor);

      // Text contrast against the nearest painted ancestor.
      if (el.children.length === 0 && (el.textContent || '').trim().length > 1) {
         var surface = bg, p = el;
         while (surface === null && (p = p.parentElement)) surface = lum(getComputedStyle(p).backgroundColor);
         if (surface === null) surface = bodyLum;
         var ink = lum(cs.color);
         if (ink !== null) {
            var cr = ratio(ink, surface);
            var size = parseFloat(cs.fontSize) || 14;
            var large = size >= 24 || (size >= 18.66 && parseInt(cs.fontWeight, 10) >= 700);
            if (cr < (large ? 3 : 4.5))
               lowContrast.push(path(el) + ' ' + cr.toFixed(2) + ':1 @' + size + 'px');
         }
      }
   }
   return JSON.stringify({
      bodyLuminance: +bodyLum.toFixed(3),
      darkSurfaces: darker.slice(0, 12),          // context, not a failure
      belowAA: lowContrast.slice(0, 12),          // the actual failure signal
      verdict: lowContrast.length === 0 ? 'CLEAN' : 'ISSUES'
   });
})();
";

   private string ApplyTheme(string html)
   {
      foreach (var (id, file) in new[] { ("amt-fonts", "fonts.css"), ("amt-theme", "theme.css") })
      {
         var path = Path.Combine(_repoRoot, "Assets", "Html", file);
         if (!File.Exists(path)) continue;
         var head = html.IndexOf(HeadAnchor, StringComparison.OrdinalIgnoreCase);
         if (head >= 0)
            html = html.Insert(head, $"<style id=\"{id}\">\n{File.ReadAllText(path)}\n</style>\n");
      }

      if (_args.Light)
      {
         var tag = html.IndexOf(HtmlAnchor, StringComparison.OrdinalIgnoreCase);
         if (tag >= 0) html = html.Insert(tag + HtmlAnchor.Length, " data-theme=\"light\"");
      }

      return html;
   }

   private string ReadLocale(string code)
   {
      var path = Path.Combine(_repoRoot, "Assets", "Locales", $"{code}.json");
      return File.Exists(path) ? File.ReadAllText(path) : "{}";
   }

   private string HtmlPath(string page) => Path.Combine(_repoRoot, "Assets", "Html", $"{page}.html");

   private static string FindRepoRoot()
   {
      var dir = new DirectoryInfo(AppContext.BaseDirectory);
      while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "ArdysaModsTools.csproj")))
         dir = dir.Parent;
      return dir?.FullName ?? Directory.GetCurrentDirectory();
   }
}
