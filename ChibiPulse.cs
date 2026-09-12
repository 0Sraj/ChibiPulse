// ═══════════════════════════════════════════════════════════════════════════
//  ChibiPulse — Tiny-timer HUD for keyboard-map runs
//
//  Copyright (c) 2026 sraj.  All rights reserved.
//  Licensed under the MIT License — see LICENSE. The copyright notice above
//  must be preserved in any copy or substantial portion of this software.
//
//  https://github.com/0Sraj/ChibiPulse
//
//  Single-file WinForms app, no external packages.
//  Build:  build.bat        (uses the .NET Framework C# compiler shipped with Windows)
// ═══════════════════════════════════════════════════════════════════════════

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;

using Timer = System.Windows.Forms.Timer;   // disambiguate from System.Threading.Timer

// Authorship, baked into the executable itself: Windows shows these under
// File Properties -> Details, and they survive renaming or copying the exe.
[assembly: AssemblyTitle("ChibiPulse")]
[assembly: AssemblyProduct("ChibiPulse")]
[assembly: AssemblyDescription("Precision tiny-timer HUD with auto-jump - by sraj")]
[assembly: AssemblyCompany("sraj")]
[assembly: AssemblyCopyright("Copyright (c) 2026 sraj. Licensed under the MIT License.")]
[assembly: AssemblyTrademark("ChibiPulse by sraj")]
[assembly: AssemblyVersion("2.2.0.0")]
[assembly: AssemblyFileVersion("2.2.0.0")]

namespace ChibiPulse
{
    // ═══════════════════════════════════════════════════════════════════════
    //  ENTRY POINT
    // ═══════════════════════════════════════════════════════════════════════
    static class Program
    {
        [STAThread]
        static void Main()
        {
            bool firstInstance;
            using (var gate = new Mutex(true, "ChibiPulse.SingleInstance", out firstInstance))
            {
                if (!firstInstance) return;

                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);

                using (var g = Graphics.FromHwnd(IntPtr.Zero))
                    Theme.Scale = g.DpiX / 96f;

                Application.Run(new MainForm());
                GC.KeepAlive(gate);
            }
        }
    }

    public enum AppState { Idle, Active, Warning, Cooldown }

    /// <summary>A selectable key: display label + Win32 virtual-key code.</summary>
    public class KeyOption
    {
        public readonly string Label;
        public readonly int VK;
        public KeyOption(string label, int vk) { Label = label; VK = vk; }
        public override string ToString() { return Label; }
    }

    // ═══════════════════════════════════════════════════════════════════════
    //  THEME  — colours, fonts and DPI scale, allocated once
    // ═══════════════════════════════════════════════════════════════════════
    static class Theme
    {
        public static float Scale = 1f;
        public static int P(double v) { return (int)Math.Round(v * Scale); }
        public static float F(double v) { return (float)(v * Scale); }

        // Palette lifted from the project banner: neon cyan + magenta on deep space blue.
        public static readonly Color Bg = Color.FromArgb(8, 10, 20);
        public static readonly Color BgTop = Color.FromArgb(12, 16, 30);
        public static readonly Color Card = Color.FromArgb(16, 21, 38);
        public static readonly Color CardTop = Color.FromArgb(23, 30, 52);
        public static readonly Color Inset = Color.FromArgb(11, 15, 28);
        public static readonly Color Line = Color.FromArgb(38, 50, 82);

        public static readonly Color Cyan = Color.FromArgb(45, 226, 255);
        public static readonly Color Magenta = Color.FromArgb(255, 60, 198);
        public static readonly Color Green = Color.FromArgb(46, 238, 168);
        public static readonly Color Amber = Color.FromArgb(255, 196, 72);
        public static readonly Color Red = Color.FromArgb(255, 74, 118);

        public static readonly Color Text = Color.FromArgb(228, 238, 255);
        public static readonly Color Sub = Color.FromArgb(140, 158, 200);
        public static readonly Color Dim = Color.FromArgb(86, 102, 142);

        // Point sizes scale with the system DPI on their own — do not multiply them.
        public static readonly Font Brand = new Font("Segoe UI", 14f, FontStyle.Bold);
        public static readonly Font Tag = new Font("Segoe UI", 7.5f);
        public static readonly Font Section = new Font("Segoe UI", 9.5f, FontStyle.Bold);
        public static readonly Font Label = new Font("Segoe UI", 8.25f);
        public static readonly Font Body = new Font("Segoe UI", 9f);
        public static readonly Font Chip = new Font("Segoe UI", 8f, FontStyle.Bold);
        public static readonly Font ChipSm = new Font("Segoe UI", 7.5f, FontStyle.Bold);
        public static readonly Font Button = new Font("Segoe UI", 10.5f, FontStyle.Bold);
        public static readonly Font Mono = new Font("Consolas", 11f, FontStyle.Bold);
        public static readonly Font MonoBig = new Font("Consolas", 38f, FontStyle.Bold);
        public static readonly Font MonoMid = new Font("Consolas", 15f, FontStyle.Bold);
        public static readonly Font MonoOv = new Font("Consolas", 21f, FontStyle.Bold);
        public static readonly Font StateEn = new Font("Segoe UI", 15f, FontStyle.Bold);
        public static readonly Font StateAr = new Font("Segoe UI", 10.5f);

        public static readonly StringFormat Center = MakeFmt(StringAlignment.Center, StringAlignment.Center);
        public static readonly StringFormat Left = MakeFmt(StringAlignment.Near, StringAlignment.Center);
        public static readonly StringFormat Right = MakeFmt(StringAlignment.Far, StringAlignment.Center);

        static StringFormat MakeFmt(StringAlignment h, StringAlignment v)
        {
            // Deliberately NOT GenericTypographic: it swallows the spaces between
            // Arabic and Latin runs in the bilingual labels.
            var f = new StringFormat();
            f.Alignment = h;
            f.LineAlignment = v;
            f.FormatFlags |= StringFormatFlags.NoWrap;
            f.Trimming = StringTrimming.None;
            return f;
        }
    }

    // ═══════════════════════════════════════════════════════════════════════
    //  DRAWING HELPERS
    // ═══════════════════════════════════════════════════════════════════════
    static class Gfx
    {
        public static GraphicsPath Round(float x, float y, float w, float h, float r)
        {
            var p = new GraphicsPath();
            float d = r * 2;
            if (d > w) d = w;
            if (d > h) d = h;
            if (d <= 0) { p.AddRectangle(new RectangleF(x, y, w, h)); return p; }
            p.AddArc(x, y, d, d, 180, 90);
            p.AddArc(x + w - d, y, d, d, 270, 90);
            p.AddArc(x + w - d, y + h - d, d, d, 0, 90);
            p.AddArc(x, y + h - d, d, d, 90, 90);
            p.CloseFigure();
            return p;
        }

        public static GraphicsPath Round(RectangleF r, float radius)
        {
            return Round(r.X, r.Y, r.Width, r.Height, radius);
        }

        public static void Fill(Graphics g, GraphicsPath path, Color c)
        {
            using (var b = new SolidBrush(c)) g.FillPath(b, path);
        }

        public static void Stroke(Graphics g, GraphicsPath path, Color c, float w)
        {
            using (var p = new Pen(c, w)) g.DrawPath(p, path);
        }

        /// <summary>Soft outer glow traced around a shape.</summary>
        public static void Glow(Graphics g, GraphicsPath path, Color c, float width, int alpha)
        {
            using (var p = new Pen(Color.FromArgb(alpha, c), width))
            {
                p.LineJoin = LineJoin.Round;
                g.DrawPath(p, path);
            }
        }

        public static void VerticalFill(Graphics g, GraphicsPath path, RectangleF area, Color top, Color bottom)
        {
            using (var b = new LinearGradientBrush(
                new RectangleF(area.X, area.Y - 1, Math.Max(1, area.Width), Math.Max(1, area.Height + 2)),
                top, bottom, LinearGradientMode.Vertical))
                g.FillPath(b, path);
        }

        public static void Text(Graphics g, string s, Font f, Color c, float x, float y)
        {
            using (var b = new SolidBrush(c)) g.DrawString(s, f, b, x, y);
        }

        public static void TextIn(Graphics g, string s, Font f, Color c, RectangleF rc, StringFormat fmt)
        {
            using (var b = new SolidBrush(c)) g.DrawString(s, f, b, rc, fmt);
        }

        /// <summary>Pill-shaped status chip.</summary>
        public static void Pill(Graphics g, RectangleF rc, string text, Color accent, Font font)
        {
            using (var path = Round(rc, rc.Height / 2f))
            {
                Fill(g, path, Color.FromArgb(34, accent));
                Stroke(g, path, Color.FromArgb(170, accent), Theme.F(1.2));
            }
            TextIn(g, text, font, accent, rc, Theme.Center);
        }

        /// <summary>Physical-looking keycap, like the ones on the banner.</summary>
        public static void Keycap(Graphics g, RectangleF rc, string text, Color accent)
        {
            using (var shadow = Round(rc.X, rc.Y + Theme.F(3), rc.Width, rc.Height, Theme.F(7)))
                Fill(g, shadow, Color.FromArgb(120, accent));
            using (var path = Round(rc, Theme.F(7)))
            {
                VerticalFill(g, path, rc, Color.FromArgb(30, 40, 68), Color.FromArgb(16, 22, 40));
                Stroke(g, path, Color.FromArgb(200, accent), Theme.F(1.4));
            }
            TextIn(g, text, Theme.Chip, Color.FromArgb(240, 250, 255), rc, Theme.Center);
        }

        /// <summary>HUD corner brackets.</summary>
        public static void Corners(Graphics g, RectangleF rc, Color c, float len, float inset)
        {
            using (var p = new Pen(c, Theme.F(1.6)))
            {
                float l = rc.Left + inset, t = rc.Top + inset, r = rc.Right - inset, b = rc.Bottom - inset;
                g.DrawLine(p, l, t, l + len, t); g.DrawLine(p, l, t, l, t + len);
                g.DrawLine(p, r, t, r - len, t); g.DrawLine(p, r, t, r, t + len);
                g.DrawLine(p, l, b, l + len, b); g.DrawLine(p, l, b, l, b - len);
                g.DrawLine(p, r, b, r - len, b); g.DrawLine(p, r, b, r, b - len);
            }
        }

        public static void Quality(Graphics g)
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
        }
    }

    // ═══════════════════════════════════════════════════════════════════════
    //  SETTINGS  (config.ini, culture-invariant)
    // ═══════════════════════════════════════════════════════════════════════
    public class Config
    {
        public double Duration = 4.0;
        public double WarnBefore = 0.2;
        public double Cooldown = 0.0;
        public double JumpTime = 3.9;

        public int TrigKey = 0x45;      // E
        public int ToggleKey = 0x77;    // F8
        public int ResetKey = 0x58;     // X
        public bool ResetOn = false;

        public bool Voice = false;
        public bool Beep = false;
        public bool Overlay = true;
        public bool AutoJump = true;
        public bool OverlayLock = false;

        public int OverlayX = 48;
        public int OverlayY = 48;
        public string Phrase = "Ready";

        public void Load(string path)
        {
            try
            {
                if (!File.Exists(path)) return;
                foreach (string raw in File.ReadAllLines(path))
                {
                    string line = raw.Trim();
                    if (line.Length == 0 || line[0] == '#' || line[0] == ';') continue;
                    int eq = line.IndexOf('=');
                    if (eq <= 0) continue;

                    string k = line.Substring(0, eq).Trim();
                    string v = line.Substring(eq + 1).Trim();

                    if (k == "Dur") Duration = Num(v, Duration);
                    else if (k == "Warn") WarnBefore = Num(v, WarnBefore);
                    else if (k == "Cd") Cooldown = Num(v, Cooldown);
                    else if (k == "JumpTime") JumpTime = Num(v, JumpTime);
                    else if (k == "Trig") TrigKey = (int)Num(v, TrigKey);
                    else if (k == "Tog") ToggleKey = (int)Num(v, ToggleKey);
                    else if (k == "Rst") ResetKey = (int)Num(v, ResetKey);
                    else if (k == "RstOn") ResetOn = Flag(v, ResetOn);
                    else if (k == "Voice") Voice = Flag(v, Voice);
                    else if (k == "Beep") Beep = Flag(v, Beep);
                    else if (k == "Ov") Overlay = Flag(v, Overlay);
                    else if (k == "OvLock") OverlayLock = Flag(v, OverlayLock);
                    else if (k == "OvX") OverlayX = (int)Num(v, OverlayX);
                    else if (k == "OvY") OverlayY = (int)Num(v, OverlayY);
                    else if (k == "Jump") AutoJump = Flag(v, AutoJump);
                    else if (k == "Phrase" && v.Length > 0) Phrase = v;
                }
            }
            catch { /* a broken config must never stop the app from starting */ }
        }

        public void Save(string path)
        {
            try
            {
                File.WriteAllLines(path, new string[] {
                    "# ChibiPulse settings - delete this file to restore the defaults",
                    "Dur=" + Fmt(Duration),
                    "Warn=" + Fmt(WarnBefore),
                    "Cd=" + Fmt(Cooldown),
                    "JumpTime=" + Fmt(JumpTime),
                    "Trig=" + TrigKey.ToString(CultureInfo.InvariantCulture),
                    "Tog=" + ToggleKey.ToString(CultureInfo.InvariantCulture),
                    "Rst=" + ResetKey.ToString(CultureInfo.InvariantCulture),
                    "RstOn=" + ResetOn,
                    "Voice=" + Voice,
                    "Beep=" + Beep,
                    "Ov=" + Overlay,
                    "OvLock=" + OverlayLock,
                    "OvX=" + OverlayX.ToString(CultureInfo.InvariantCulture),
                    "OvY=" + OverlayY.ToString(CultureInfo.InvariantCulture),
                    "Jump=" + AutoJump,
                    "Phrase=" + Phrase
                });
            }
            catch { /* read-only folder — keep running with in-memory settings */ }
        }

        static string Fmt(double d) { return d.ToString("0.00", CultureInfo.InvariantCulture); }

        static double Num(string v, double fallback)
        {
            double d;
            if (double.TryParse(v, NumberStyles.Float, CultureInfo.InvariantCulture, out d)) return d;
            if (double.TryParse(v, out d)) return d;   // configs written by older builds
            return fallback;
        }

        static bool Flag(string v, bool fallback)
        {
            bool b;
            return bool.TryParse(v, out b) ? b : fallback;
        }
    }

    // ═══════════════════════════════════════════════════════════════════════
    //  MAIN WINDOW
    // ═══════════════════════════════════════════════════════════════════════
    public class MainForm : Form
    {
        [DllImport("user32.dll")] static extern short GetAsyncKeyState(int vKey);
        [DllImport("user32.dll")] static extern uint SendInput(uint n, INPUT[] inputs, int size);
        [DllImport("user32.dll")] static extern bool ReleaseCapture();
        [DllImport("user32.dll")] static extern int SendMessage(IntPtr hWnd, int msg, int wParam, int lParam);
        [DllImport("gdi32.dll")] static extern IntPtr CreateRoundRectRgn(int l, int t, int r, int b, int w, int h);

        const int WM_NCLBUTTONDOWN = 0xA1, HTCAPTION = 2;
        const uint KEYEVENTF_SCANCODE = 0x0008, KEYEVENTF_KEYUP = 0x0002;
        const ushort SCAN_SPACE = 0x39;

        [StructLayout(LayoutKind.Sequential)] public struct KEYBDINPUT { public ushort wVk, wScan; public uint dwFlags, time; public IntPtr dwExtraInfo; }
        [StructLayout(LayoutKind.Sequential)] public struct MOUSEINPUT { public int dx, dy, mouseData, dwFlags, time; public IntPtr dwExtraInfo; }
        [StructLayout(LayoutKind.Explicit)] public struct INPUTUNION { [FieldOffset(0)] public KEYBDINPUT ki; [FieldOffset(0)] public MOUSEINPUT mi; }
        [StructLayout(LayoutKind.Sequential)] public struct INPUT { public uint type; public INPUTUNION u; }

        // ── window metrics (design pixels, scaled through Theme.P) ──
        const int W = 980, H = 654;
        const int TitleH = 56, Pad = 20, ColGap = 16;
        const int HeroX = 20, HeroY = 68, HeroW = 344, HeroH = 566;
        const int ColX = 380, ColW = 580;

        // ── runtime state ──
        readonly Config cfg = new Config();
        readonly string cfgPath;

        AppState state = AppState.Idle;
        DateTime phaseStart;
        double phaseDur;
        bool listening = true;
        bool wasTrig, wasToggle, wasReset;
        bool jumpDone, pendingJump;
        int glowTick;

        // ── speech (loaded late so the build never needs a System.Speech reference) ──
        object synth;
        MethodInfo speakAsync, speakCancel, synthDispose;

        // ── widgets ──
        HeroDash hero;
        GlowCard cardKeys, cardTiming, cardAlerts;
        GlowButton btnListen, btnTest;
        NeonToggle togReset, togVoice, togBeep, togOverlay, togJump, togLock;
        NeonSpinner spinDur, spinWarn, spinCd, spinJump;
        NeonSelect cmbTrig, cmbToggle, cmbReset, cmbPhrase;
        Timer mainTimer, glowTimer, saveTimer;
        OverlayWindow overlay;

        public MainForm()
        {
            cfgPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "config.ini");
            cfg.Load(cfgPath);
            InitSpeech();
            Build();
            ApplyConfigToUI();
            StartTimers();

            overlay = new OverlayWindow();
            overlay.Location = new Point(cfg.OverlayX, cfg.OverlayY);
            overlay.Show();
            overlay.Visible = cfg.Overlay;
            overlay.ClickThrough = cfg.OverlayLock;
        }

        protected override CreateParams CreateParams
        {
            get
            {
                var cp = base.CreateParams;
                cp.ClassStyle |= 0x00020000;   // CS_DROPSHADOW
                return cp;
            }
        }

        // ───────────────────────────────────────────── layout
        static Rectangle R(int x, int y, int w, int h)
        {
            return new Rectangle(Theme.P(x), Theme.P(y), Theme.P(w), Theme.P(h));
        }

        void Build()
        {
            Text = "ChibiPulse";
            // Pull the win32 icon back out of our own exe so the taskbar and Alt-Tab show it.
            // The window is borderless, so there is no caption bar to carry it otherwise.
            try { Icon = System.Drawing.Icon.ExtractAssociatedIcon(Application.ExecutablePath); }
            catch { /* the icon is cosmetic - never let it stop startup */ }
            ClientSize = new Size(Theme.P(W), Theme.P(H));
            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.CenterScreen;
            AutoScaleMode = AutoScaleMode.None;
            BackColor = Theme.Bg;
            DoubleBuffered = true;
            KeyPreview = true;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint, true);

            Paint += PaintBackdrop;
            MouseDown += DragMove;

            BuildTitleBar();

            hero = new HeroDash { Bounds = R(HeroX, HeroY, HeroW, HeroH), Parent = this };

            cardKeys = NewCard("KEY BINDINGS", "الأزرار", Theme.Cyan, R(ColX, 68, ColW, 124));
            BuildKeys();

            cardTiming = NewCard("PRECISION TIMING", "التوقيت", Theme.Magenta, R(ColX, 204, ColW, 182));
            BuildTiming();

            cardAlerts = NewCard("ALERTS & ASSIST", "التنبيهات", Theme.Green, R(ColX, 398, ColW, 168));
            BuildAlerts();

            BuildActions();
        }

        void BuildTitleBar()
        {
            var bar = new TitleBar { Bounds = R(0, 0, W, TitleH), Parent = this };
            bar.MouseDown += DragMove;

            var close = new IconButton { Text = "✕", HoverColor = Theme.Red, Bounds = R(W - 48, 8, 40, 40), Parent = bar };
            close.Click += delegate { Close(); };

            var min = new IconButton { Text = "─", HoverColor = Color.FromArgb(44, 58, 92), Bounds = R(W - 92, 8, 40, 40), Parent = bar };
            min.Click += delegate { WindowState = FormWindowState.Minimized; };
        }

        GlowCard NewCard(string title, string titleAr, Color accent, Rectangle bounds)
        {
            return new GlowCard { Title = title, TitleAr = titleAr, Accent = accent, Bounds = bounds, Parent = this };
        }

        void BuildKeys()
        {
            const int y = 52, cw = 164;
            int[] cols = { 20, 196, 372 };

            Caption(cardKeys, "زر التحول · Trigger", cols[0], y);
            cmbTrig = NewCombo(cardKeys, cols[0], y + 20, cw, TriggerKeys(), Theme.Cyan);
            cmbTrig.SelectedIndexChanged += delegate { cfg.TrigKey = VK(cmbTrig, cfg.TrigKey); SyncHero(); SyncKeyTints(); MarkDirty(); };

            Caption(cardKeys, "تشغيل / إيقاف · Toggle", cols[1], y);
            cmbToggle = NewCombo(cardKeys, cols[1], y + 20, cw, ToggleKeys(), Theme.Cyan);
            cmbToggle.SelectedIndexChanged += delegate { cfg.ToggleKey = VK(cmbToggle, cfg.ToggleKey); SyncListenButton(); SyncKeyTints(); MarkDirty(); };

            togReset = new NeonToggle
            {
                Text = "إعادة تعيين · Reset",
                Bounds = R(cols[2], y - 4, cw, 24),
                Checked = cfg.ResetOn,
                Accent = Theme.Magenta,
                Parent = cardKeys
            };
            togReset.CheckedChanged += delegate
            {
                cfg.ResetOn = togReset.Checked;
                SyncKeyTints();
                MarkDirty();
            };

            cmbReset = NewCombo(cardKeys, cols[2], y + 20, cw, ResetKeys(), Theme.Magenta);
            cmbReset.SelectedIndexChanged += delegate { cfg.ResetKey = VK(cmbReset, cfg.ResetKey); SyncKeyTints(); MarkDirty(); };
            SyncKeyTints();
        }

        void BuildTiming()
        {
            Caption(cardTiming, "مدة التحول · Tiny duration (s)", 20, 52);
            spinDur = NewSpinner(cardTiming, 20, 72, 170, 34, cfg.Duration, 0.5, 120, 0.01, 0.1, Theme.Magenta);
            spinDur.ValueChanged += delegate { cfg.Duration = spinDur.Value; SyncHero(); MarkDirty(); };

            int px = 210;
            foreach (double preset in new double[] { 5, 7.5, 10, 12.5, 15 })
            {
                double captured = preset;
                var chip = new ChipButton
                {
                    Text = Num(preset) + "s",
                    Accent = Theme.Magenta,
                    Bounds = R(px, 74, 52, 30),
                    Parent = cardTiming
                };
                chip.Click += delegate { spinDur.Value = captured; };
                px += 58;
            }

            // 172 wide, not 150: four hit zones plus a value that can read "120.00" need it.
            Caption(cardTiming, "تحذير مبكر · Warn before", 20, 118);
            spinWarn = NewSpinner(cardTiming, 20, 140, 172, 30, cfg.WarnBefore, 0.1, 15, 0.01, 0.1, Theme.Amber);
            spinWarn.ValueChanged += delegate { cfg.WarnBefore = spinWarn.Value; MarkDirty(); };

            Caption(cardTiming, "كول داون · Cooldown", 204, 118);
            spinCd = NewSpinner(cardTiming, 204, 140, 172, 30, cfg.Cooldown, 0, 60, 0.01, 0.1, Theme.Red);
            spinCd.ValueChanged += delegate { cfg.Cooldown = spinCd.Value; MarkDirty(); };

            Caption(cardTiming, "وقت القفزة · Jump at", 388, 118);
            spinJump = NewSpinner(cardTiming, 388, 140, 172, 30, cfg.JumpTime, 0.1, 120, 0.01, 0.1, Theme.Green);
            spinJump.ValueChanged += delegate { cfg.JumpTime = spinJump.Value; SyncHero(); MarkDirty(); };
        }

        void BuildAlerts()
        {
            togVoice = NewToggle(cardAlerts, "نطق صوتي عند الجاهزية · Voice", 20, 54, 250, cfg.Voice, Theme.Cyan);
            togVoice.CheckedChanged += delegate { cfg.Voice = togVoice.Checked; MarkDirty(); };

            togBeep = NewToggle(cardAlerts, "نغمة تنبيه · Beep", 20, 90, 250, cfg.Beep, Theme.Cyan);
            togBeep.CheckedChanged += delegate { cfg.Beep = togBeep.Checked; MarkDirty(); };

            togOverlay = NewToggle(cardAlerts, "الشاشة العائمة · HUD overlay", 20, 126, 250, cfg.Overlay, Theme.Cyan);
            togOverlay.CheckedChanged += delegate
            {
                cfg.Overlay = togOverlay.Checked;
                if (overlay != null) overlay.Visible = cfg.Overlay;
                MarkDirty();
            };

            togJump = NewToggle(cardAlerts, "قفزة تلقائية · Auto-jump", 300, 54, 250, cfg.AutoJump, Theme.Green);
            togJump.CheckedChanged += delegate { cfg.AutoJump = togJump.Checked; SyncHero(); MarkDirty(); };

            togLock = NewToggle(cardAlerts, "تثبيت الشاشة العائمة · Lock", 300, 90, 250, cfg.OverlayLock, Theme.Green);
            togLock.CheckedChanged += delegate
            {
                cfg.OverlayLock = togLock.Checked;
                if (overlay != null) overlay.ClickThrough = cfg.OverlayLock;
                MarkDirty();
            };

            Caption(cardAlerts, "الكلمة · Word", 300, 126);
            cmbPhrase = NewCombo(cardAlerts, 386, 120, 150, new object[] { "Ready", "Big", "Go", "Now", "Warning" }, Theme.Green);
            cmbPhrase.SelectedIndexChanged += delegate
            {
                if (cmbPhrase.SelectedItem == null) return;
                cfg.Phrase = cmbPhrase.SelectedItem.ToString();
                MarkDirty();
            };
        }

        void BuildActions()
        {
            btnListen = new GlowButton
            {
                Bounds = R(ColX, 578, 356, 56),
                Font = Theme.Button,
                Accent = Theme.Green,
                Parent = this
            };
            btnListen.Click += delegate { ToggleListening(); };

            btnTest = new GlowButton
            {
                Text = "تجربة · Test run",
                Bounds = R(ColX + 368, 578, 212, 56),
                Font = Theme.Button,
                Accent = Theme.Cyan,
                Parent = this
            };
            btnTest.Click += delegate { BeginPhase(); };
        }

        // ───────────────────────────────────────────── widget factories
        void Caption(Control parent, string text, int x, int y)
        {
            new Label
            {
                Text = text,
                AutoSize = true,
                Font = Theme.Label,
                ForeColor = Theme.Dim,
                BackColor = Color.Transparent,
                Location = new Point(Theme.P(x), Theme.P(y)),
                Parent = parent
            };
        }

        NeonToggle NewToggle(Control parent, string text, int x, int y, int w, bool value, Color accent)
        {
            return new NeonToggle
            {
                Text = text,
                Bounds = R(x, y, w, 24),
                Checked = value,
                Accent = accent,
                Parent = parent
            };
        }

        NeonSpinner NewSpinner(Control parent, int x, int y, int w, int h,
                               double value, double min, double max, double step, double bigStep, Color accent)
        {
            var s = new NeonSpinner
            {
                Bounds = R(x, y, w, h),
                Min = min, Max = max, Step = step, BigStep = bigStep,
                Accent = accent, Parent = parent
            };
            s.SetValueQuiet(value);
            return s;
        }

        /// <summary>A themed select sitting inside a rounded neon frame.</summary>
        NeonSelect NewCombo(Control parent, int x, int y, int w, object[] items, Color accent)
        {
            var host = new InsetBox { Bounds = R(x, y, w, 32), Accent = accent, Parent = parent };

            var combo = new NeonSelect { Accent = accent, Parent = host };
            combo.Items.AddRange(items);
            if (combo.Items.Count > 0) combo.SelectedIndex = 0;

            combo.Bounds = new Rectangle(Theme.P(2), Theme.P(2),
                                         host.Width - Theme.P(4), host.Height - Theme.P(4));
            return combo;
        }

        /// <summary>
        /// The reset key stays selectable; it is only dimmed while the feature is off.
        /// A binding that OnTick will ignore because a higher-priority role already claims
        /// that key turns red, so a silent conflict is visible instead of just not working.
        /// </summary>
        void SyncKeyTints()
        {
            if (cmbTrig == null || cmbToggle == null || cmbReset == null) return;

            int trig = VK(cmbTrig, cfg.TrigKey);
            int toggle = VK(cmbToggle, cfg.ToggleKey);
            int reset = VK(cmbReset, cfg.ResetKey);

            bool resetClash = cfg.ResetOn && reset == toggle;
            bool trigClash = trig == toggle || (cfg.ResetOn && !resetClash && trig == reset);

            Tint(cmbTrig, trigClash ? Theme.Red : Theme.Cyan);
            Tint(cmbToggle, Theme.Cyan);
            Tint(cmbReset, !cfg.ResetOn ? Theme.Dim : resetClash ? Theme.Red : Theme.Magenta);
        }

        static void Tint(NeonSelect combo, Color c)
        {
            combo.Accent = c;
            var box = combo.Parent as InsetBox;
            if (box != null) { box.Accent = c; box.Invalidate(); }
            combo.Invalidate();
        }

        // ───────────────────────────────────────────── key tables
        //  Shared groups, so every dropdown offers the same vocabulary and each role
        //  only decides the order it presents them in. Adding a group here widens all
        //  three lists at once; SelectVK falls back to the first entry, so a key that
        //  an older config.ini names but a list no longer offers degrades gracefully.

        static void AddLetters(List<KeyOption> into)
        {
            for (int vk = 0x41; vk <= 0x5A; vk++)          // A–Z
                into.Add(new KeyOption(((char)vk).ToString(), vk));
        }

        static void AddDigits(List<KeyOption> into)
        {
            for (int vk = 0x30; vk <= 0x39; vk++)          // 0–9 on the number row
                into.Add(new KeyOption(((char)vk).ToString(), vk));
        }

        static void AddFunction(List<KeyOption> into)
        {
            for (int i = 1; i <= 12; i++)                  // F1 = 0x70
                into.Add(new KeyOption("F" + i, 0x6F + i));
        }

        static void AddNumpad(List<KeyOption> into)
        {
            for (int i = 0; i <= 9; i++)
                into.Add(new KeyOption("Num " + i, 0x60 + i));
            into.Add(new KeyOption("Num /", 0x6F));
            into.Add(new KeyOption("Num *", 0x6A));
            into.Add(new KeyOption("Num -", 0x6D));
            into.Add(new KeyOption("Num +", 0x6B));
            into.Add(new KeyOption("Num .", 0x6E));
        }

        static void AddEditing(List<KeyOption> into)
        {
            into.Add(new KeyOption("Space", 0x20));
            into.Add(new KeyOption("Tab", 0x09));
            into.Add(new KeyOption("Enter", 0x0D));
            into.Add(new KeyOption("Backspace", 0x08));
        }

        static void AddModifiers(List<KeyOption> into)
        {
            // The bare entries match either side; the L/R ones are side-specific.
            into.Add(new KeyOption("Shift", 0x10));
            into.Add(new KeyOption("L Shift", 0xA0));
            into.Add(new KeyOption("R Shift", 0xA1));
            into.Add(new KeyOption("Ctrl", 0x11));
            into.Add(new KeyOption("L Ctrl", 0xA2));
            into.Add(new KeyOption("R Ctrl", 0xA3));
            into.Add(new KeyOption("Alt", 0x12));
            into.Add(new KeyOption("L Alt", 0xA4));
            into.Add(new KeyOption("R Alt", 0xA5));
        }

        static void AddNavigation(List<KeyOption> into)
        {
            into.Add(new KeyOption("Insert", 0x2D));
            into.Add(new KeyOption("Delete", 0x2E));
            into.Add(new KeyOption("Home", 0x24));
            into.Add(new KeyOption("End", 0x23));
            into.Add(new KeyOption("Page Up", 0x21));
            into.Add(new KeyOption("Page Down", 0x22));
            into.Add(new KeyOption("Up", 0x26));
            into.Add(new KeyOption("Down", 0x28));
            into.Add(new KeyOption("Left", 0x25));
            into.Add(new KeyOption("Right", 0x27));
        }

        static void AddSymbols(List<KeyOption> into)
        {
            // Labels follow a standard US layout — the virtual-key code is the real binding,
            // so on another layout the key sits where that layout puts it.
            into.Add(new KeyOption("- _", 0xBD));
            into.Add(new KeyOption("= +", 0xBB));
            into.Add(new KeyOption("[ {", 0xDB));
            into.Add(new KeyOption("] }", 0xDD));
            into.Add(new KeyOption("\\ |", 0xDC));
            into.Add(new KeyOption("; :", 0xBA));
            into.Add(new KeyOption("' \"", 0xDE));
            into.Add(new KeyOption(", <", 0xBC));
            into.Add(new KeyOption(". >", 0xBE));
            into.Add(new KeyOption("/ ?", 0xBF));
            into.Add(new KeyOption("` ~", 0xC0));
        }

        static void AddMouse(List<KeyOption> into)
        {
            // Mouse1 and Mouse2 are deliberately absent: the key state is polled globally,
            // so binding them would fire on every ordinary click, this window's own included.
            into.Add(new KeyOption("Mouse3", 0x04));
            into.Add(new KeyOption("Mouse4", 0x05));
            into.Add(new KeyOption("Mouse5", 0x06));
        }

        static void AddLocks(List<KeyOption> into)
        {
            into.Add(new KeyOption("CapsLock", 0x14));
            into.Add(new KeyOption("NumLock", 0x90));
            into.Add(new KeyOption("ScrollLock", 0x91));
            into.Add(new KeyOption("Pause", 0x13));
        }

        static KeyOption[] TriggerKeys()
        {
            var k = new List<KeyOption>();
            AddLetters(k); AddDigits(k); AddFunction(k); AddNumpad(k);
            AddEditing(k); AddModifiers(k); AddMouse(k); AddNavigation(k); AddSymbols(k);
            return k.ToArray();
        }

        static KeyOption[] ToggleKeys()
        {
            // Function, lock and navigation keys lead: a toggle you hit by accident
            // mid-game silently stops the whole tool, so the safe keys come first.
            var k = new List<KeyOption>();
            AddFunction(k); AddLocks(k); AddNavigation(k);
            AddNumpad(k); AddLetters(k); AddDigits(k); AddSymbols(k);
            return k.ToArray();
        }

        static KeyOption[] ResetKeys()
        {
            var k = new List<KeyOption>();
            k.Add(new KeyOption("Esc", 0x1B));
            AddLetters(k); AddDigits(k); AddFunction(k); AddNumpad(k);
            AddEditing(k); AddModifiers(k); AddMouse(k); AddNavigation(k); AddSymbols(k);
            return k.ToArray();
        }

        static int VK(NeonSelect c, int fallback)
        {
            var opt = (c == null) ? null : c.SelectedItem as KeyOption;
            return opt != null ? opt.VK : fallback;
        }

        static string KeyName(NeonSelect c, string fallback)
        {
            var opt = (c == null) ? null : c.SelectedItem as KeyOption;
            return opt != null ? opt.Label : fallback;
        }

        static void SelectVK(NeonSelect c, int vk)
        {
            for (int i = 0; i < c.Items.Count; i++)
            {
                var opt = c.Items[i] as KeyOption;
                if (opt != null && opt.VK == vk) { c.SelectedIndex = i; return; }
            }
            if (c.Items.Count > 0) c.SelectedIndex = 0;
        }

        void ApplyConfigToUI()
        {
            SelectVK(cmbTrig, cfg.TrigKey);
            SelectVK(cmbToggle, cfg.ToggleKey);
            SelectVK(cmbReset, cfg.ResetKey);

            if (cmbPhrase.Items.Contains(cfg.Phrase)) cmbPhrase.SelectedItem = cfg.Phrase;
            else cfg.Phrase = cmbPhrase.SelectedItem.ToString();

            SyncHero();
            SyncListenButton();
            SyncKeyTints();
        }

        // ───────────────────────────────────────────── timers & state machine
        void StartTimers()
        {
            mainTimer = new Timer { Interval = 15 };
            mainTimer.Tick += OnTick;
            mainTimer.Start();

            glowTimer = new Timer { Interval = 33 };
            glowTimer.Tick += delegate
            {
                glowTick = (glowTick + 1) % 3600;
                hero.Animate(glowTick);
                Invalidate(new Rectangle(0, 0, Width, Theme.P(TitleH)), false);
            };
            glowTimer.Start();
        }

        void OnTick(object sender, EventArgs e)
        {
            int trig = VK(cmbTrig, cfg.TrigKey);
            int toggle = VK(cmbToggle, cfg.ToggleKey);
            int reset = VK(cmbReset, cfg.ResetKey);

            // The three lists overlap, so one physical key can be picked for more than
            // one role. Resolve it in a fixed order — toggle, then reset, then trigger —
            // instead of letting both act on the same tick, where reset would call
            // GoIdle only for the trigger to call BeginPhase right after it.
            bool resetLive = cfg.ResetOn && reset != toggle;
            bool trigLive = trig != toggle && !(resetLive && trig == reset);

            bool toggleDown = Down(toggle);
            if (toggleDown && !wasToggle) ToggleListening();
            wasToggle = toggleDown;

            bool resetDown = resetLive && Down(reset);
            if (resetDown && !wasReset) GoIdle();
            wasReset = resetDown;

            // Always track the trigger edge, so re-enabling while the key is held
            // does not fire a phantom start.
            bool trigDown = trigLive && Down(trig);
            if (listening && trigDown && !wasTrig && state != AppState.Cooldown) BeginPhase();
            wasTrig = trigDown;

            if (pendingJump) { pendingJump = false; SendJump(); }

            if (state == AppState.Active || state == AppState.Warning)
            {
                double elapsed = (DateTime.UtcNow - phaseStart).TotalSeconds;
                double remain = phaseDur - elapsed;

                if (cfg.AutoJump && !jumpDone && elapsed >= cfg.JumpTime) { jumpDone = true; pendingJump = true; }

                if (remain <= 0)
                {
                    if (cfg.Cooldown > 0)
                    {
                        state = AppState.Cooldown;
                        phaseStart = DateTime.UtcNow;
                        phaseDur = cfg.Cooldown;
                        Alert("Cooldown");
                    }
                    else { GoIdle(); Alert(cfg.Phrase); }
                }
                else if (remain <= cfg.WarnBefore && state != AppState.Warning)
                {
                    state = AppState.Warning;
                    WarnAlert();
                }
            }
            else if (state == AppState.Cooldown)
            {
                if (phaseDur - (DateTime.UtcNow - phaseStart).TotalSeconds <= 0) { GoIdle(); Alert(cfg.Phrase); }
            }

            RefreshReadouts();
        }

        static bool Down(int vk) { return (GetAsyncKeyState(vk) & 0x8000) != 0; }

        void BeginPhase()
        {
            state = AppState.Active;
            phaseStart = DateTime.UtcNow;
            phaseDur = cfg.Duration;
            jumpDone = false;
            pendingJump = false;
        }

        void GoIdle() { state = AppState.Idle; }

        void ToggleListening()
        {
            listening = !listening;
            SyncListenButton();
        }

        void SendJump()
        {
            ThreadPool.QueueUserWorkItem(delegate
            {
                try
                {
                    SendSpace(KEYEVENTF_SCANCODE);
                    Thread.Sleep(40);
                    SendSpace(KEYEVENTF_SCANCODE | KEYEVENTF_KEYUP);
                }
                catch { }
            });
        }

        static void SendSpace(uint flags)
        {
            var input = new INPUT[1];
            input[0].type = 1;                       // INPUT_KEYBOARD
            input[0].u.ki.wVk = 0;
            input[0].u.ki.wScan = SCAN_SPACE;
            input[0].u.ki.dwFlags = flags;
            input[0].u.ki.time = 0;
            input[0].u.ki.dwExtraInfo = IntPtr.Zero;
            SendInput(1, input, Marshal.SizeOf(typeof(INPUT)));
        }

        void Alert(string word)
        {
            if (cfg.Beep)
                ThreadPool.QueueUserWorkItem(delegate
                {
                    try { Console.Beep(1400, 90); Thread.Sleep(35); Console.Beep(1900, 150); } catch { }
                });

            if (cfg.Voice && synth != null)
                ThreadPool.QueueUserWorkItem(delegate
                {
                    try { speakCancel.Invoke(synth, null); speakAsync.Invoke(synth, new object[] { word }); } catch { }
                });
        }

        void WarnAlert()
        {
            if (!cfg.Beep) return;
            ThreadPool.QueueUserWorkItem(delegate
            {
                try { Console.Beep(900, 120); Thread.Sleep(30); Console.Beep(900, 120); } catch { }
            });
        }

        // ───────────────────────────────────────────── readouts
        Color AccentFor(AppState s)
        {
            if (s == AppState.Warning) return Theme.Amber;
            if (s == AppState.Cooldown) return Theme.Red;
            if (s == AppState.Active) return Theme.Cyan;
            return Theme.Green;
        }

        void RefreshReadouts()
        {
            double remain = 0;
            if (state != AppState.Idle)
                remain = Math.Max(0, phaseDur - (DateTime.UtcNow - phaseStart).TotalSeconds);

            Color accent = AccentFor(state);
            string en, ar;
            double progress;

            if (state == AppState.Idle)
            {
                en = "READY"; ar = "جاهز · اضغط زر التحول";
                progress = 1;
                remain = cfg.Duration;
            }
            else if (state == AppState.Active)
            {
                en = "TINY ACTIVE"; ar = "أنت صغير الآن";
                progress = phaseDur > 0 ? remain / phaseDur : 0;
            }
            else if (state == AppState.Warning)
            {
                en = "BIG SOON"; ar = "استعد · ستعود كبيراً";
                progress = phaseDur > 0 ? remain / phaseDur : 0;
            }
            else
            {
                en = "COOLDOWN"; ar = "انتظر قبل الاستخدام";
                progress = phaseDur > 0 ? remain / phaseDur : 0;
            }

            hero.Push(state, Seconds(remain), en, ar, accent, progress, listening);

            if (overlay != null && overlay.Visible)
                overlay.Push(state, remain, progress, accent, cfg.AutoJump);
        }

        void SyncHero()
        {
            hero.KeyTag = KeyName(cmbTrig, "E");
            hero.Duration = cfg.Duration;
            hero.JumpAt = cfg.JumpTime;
            hero.JumpOn = cfg.AutoJump;
            hero.Invalidate();
        }

        void SyncListenButton()
        {
            if (btnListen == null) return;
            string key = KeyName(cmbToggle, "F8");
            btnListen.Text = listening
                ? "الاستشعار يعمل  ·  LIVE  ·  " + key
                : "متوقف  ·  PAUSED  ·  " + key;
            btnListen.Accent = listening ? Theme.Green : Theme.Red;
            btnListen.Invalidate();

            hero.Listening = listening;
            hero.Invalidate();
        }

        static string Seconds(double d) { return d.ToString("00.00", CultureInfo.InvariantCulture); }

        static string Num(double d) { return d.ToString("0.##", CultureInfo.InvariantCulture); }

        // ───────────────────────────────────────────── painting
        void PaintBackdrop(object sender, PaintEventArgs e)
        {
            var g = e.Graphics;
            Gfx.Quality(g);

            using (var b = new LinearGradientBrush(ClientRectangle, Theme.BgTop, Theme.Bg, LinearGradientMode.Vertical))
                g.FillRectangle(b, ClientRectangle);

            AmbientGlow(g, -Theme.P(120), -Theme.P(120), Theme.P(520), Theme.Cyan, 30);
            AmbientGlow(g, Width - Theme.P(360), Height - Theme.P(320), Theme.P(560), Theme.Magenta, 26);

            // title bar underline
            using (var b = new LinearGradientBrush(
                new Rectangle(0, Theme.P(TitleH) - 1, Width, 2), Theme.Cyan, Theme.Magenta, LinearGradientMode.Horizontal))
                g.FillRectangle(b, 0, Theme.P(TitleH) - 1, Width, Theme.F(1.4));

            // brand mark
            float pulse = (float)(0.45 + 0.55 * Math.Abs(Math.Sin(glowTick / 11.0)));
            float cx = Theme.F(34), cy = Theme.F(28);
            using (var b = new SolidBrush(Color.FromArgb((int)(30 + 55 * pulse), Theme.Cyan)))
                g.FillEllipse(b, cx - Theme.F(15), cy - Theme.F(15), Theme.F(30), Theme.F(30));
            using (var p = new Pen(Color.FromArgb(200, Theme.Cyan), Theme.F(2)))
                g.DrawEllipse(p, cx - Theme.F(10), cy - Theme.F(10), Theme.F(20), Theme.F(20));
            using (var b = new SolidBrush(Theme.Magenta))
                g.FillEllipse(b, cx - Theme.F(4), cy - Theme.F(4), Theme.F(8), Theme.F(8));

            Gfx.Text(g, "CHIBI", Theme.Brand, Theme.Text, Theme.F(58), Theme.F(9));
            using (var b = new SolidBrush(Theme.Magenta))
                g.DrawString("PULSE", Theme.Brand, b, Theme.F(58) + g.MeasureString("CHIBI ", Theme.Brand).Width, Theme.F(9));
            Gfx.Text(g, "Tiny timer  ·  HUD overlay  ·  auto-jump", Theme.Tag, Theme.Dim, Theme.F(60), Theme.F(34));

            // Author credit, right-aligned to stop short of the window buttons.
            Gfx.TextIn(g, "by sraj", Theme.Chip, Color.FromArgb(210, Theme.Magenta),
                       new RectangleF(Width - Theme.F(320), Theme.F(17), Theme.F(210), Theme.F(22)), Theme.Right);
        }

        static void AmbientGlow(Graphics g, int x, int y, int size, Color c, int alpha)
        {
            using (var path = new GraphicsPath())
            {
                path.AddEllipse(x, y, size, size);
                using (var brush = new PathGradientBrush(path))
                {
                    brush.CenterColor = Color.FromArgb(alpha, c);
                    brush.SurroundColors = new Color[] { Color.FromArgb(0, c) };
                    g.FillPath(brush, path);
                }
            }
        }

        void DragMove(object sender, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Left) return;
            ReleaseCapture();
            SendMessage(Handle, WM_NCLBUTTONDOWN, HTCAPTION, 0);
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            Region = Region.FromHrgn(CreateRoundRectRgn(0, 0, Width + 1, Height + 1, Theme.P(20), Theme.P(20)));
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Escape) Close();
            base.OnKeyDown(e);
        }

        // ───────────────────────────────────────────── persistence
        /// <summary>Coalesces rapid edits (spinner auto-repeat) into one write.</summary>
        void MarkDirty()
        {
            if (saveTimer == null)
            {
                saveTimer = new Timer { Interval = 700 };
                saveTimer.Tick += delegate { saveTimer.Stop(); SaveNow(); };
            }
            saveTimer.Stop();
            saveTimer.Start();
        }

        void SaveNow()
        {
            if (overlay != null && !overlay.IsDisposed)
            {
                cfg.OverlayX = overlay.Left;
                cfg.OverlayY = overlay.Top;
            }
            cfg.Save(cfgPath);
        }

        void InitSpeech()
        {
            // Bound late: the app still builds and runs on machines without System.Speech.
            try
            {
                var type = Type.GetType("System.Speech.Synthesis.SpeechSynthesizer, System.Speech, Version=4.0.0.0, Culture=neutral, PublicKeyToken=31bf3856ad364e35");
                if (type == null) return;

                synth = Activator.CreateInstance(type);
                type.GetProperty("Rate").SetValue(synth, 1, null);
                type.GetProperty("Volume").SetValue(synth, 100, null);
                type.GetMethod("SetOutputToDefaultAudioDevice").Invoke(synth, null);

                speakAsync = type.GetMethod("SpeakAsync", new Type[] { typeof(string) });
                speakCancel = type.GetMethod("SpeakAsyncCancelAll", Type.EmptyTypes);
                synthDispose = type.GetMethod("Dispose", Type.EmptyTypes);

                if (speakAsync == null || speakCancel == null) synth = null;
            }
            catch { synth = null; }
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (saveTimer != null) saveTimer.Stop();
            SaveNow();

            if (mainTimer != null) mainTimer.Stop();
            if (glowTimer != null) glowTimer.Stop();
            if (overlay != null) overlay.Dispose();
            if (synth != null && synthDispose != null) { try { synthDispose.Invoke(synth, null); } catch { } }

            base.OnFormClosing(e);
        }
    }

    // ═══════════════════════════════════════════════════════════════════════
    //  HERO PANEL — countdown ring, state and live stats
    // ═══════════════════════════════════════════════════════════════════════
    public class HeroDash : Control
    {
        AppState state = AppState.Idle;
        string timeText = "4.00";
        string stateEn = "READY";
        string stateAr = "جاهز";
        Color accent = Theme.Green;
        double target = 1, shown = 1;
        int phase;

        public bool JumpOn = true;
        public bool Listening = true;
        public string KeyTag = "E";
        public double Duration = 4;
        public double JumpAt = 3.9;

        public HeroDash()
        {
            DoubleBuffered = true;
            BackColor = Theme.Bg;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer
                   | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
        }

        public void Push(AppState s, string time, string en, string ar, Color ac, double progress, bool listening)
        {
            state = s; timeText = time; stateEn = en; stateAr = ar; accent = ac;
            target = Math.Max(0, Math.Min(1, progress));
            Listening = listening;
        }

        /// <summary>Drives the pulse and eases the ring toward its target.</summary>
        public void Animate(int tick)
        {
            phase = tick;
            shown += (target - shown) * 0.35;
            if (Math.Abs(target - shown) < 0.0005) shown = target;
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            Gfx.Quality(g);

            var body = new RectangleF(0, 0, Width - 1, Height - 1);
            using (var path = Gfx.Round(body, Theme.F(18)))
            {
                Gfx.VerticalFill(g, path, body, Theme.CardTop, Theme.Card);
                Gfx.Glow(g, path, accent, Theme.F(6), 26);
                Gfx.Stroke(g, path, Color.FromArgb(120, accent), Theme.F(1.4));
            }
            Gfx.Corners(g, body, Color.FromArgb(110, accent), Theme.F(16), Theme.F(10));

            // ── status chips ──
            float chipY = Height * 0.035f;
            Gfx.Keycap(g, new RectangleF(Theme.F(18), chipY, Theme.F(74), Theme.F(30)), KeyTag, accent);
            Color live = Listening ? Theme.Green : Theme.Red;
            Gfx.Pill(g, new RectangleF(Width - Theme.F(102), chipY + Theme.F(2), Theme.F(84), Theme.F(26)),
                     Listening ? "● LIVE" : "❚❚ PAUSED", live, Theme.ChipSm);

            // ── countdown ring ──
            float cx = Width / 2f;
            float cy = Height * 0.355f;
            float r = Width * 0.315f;
            float stroke = Theme.F(13);

            float pulse = (float)(0.35 + 0.3 * Math.Abs(Math.Sin(phase / 12.0)));
            using (var glow = new GraphicsPath())
            {
                glow.AddEllipse(cx - r - Theme.F(26), cy - r - Theme.F(26), (r + Theme.F(26)) * 2, (r + Theme.F(26)) * 2);
                using (var brush = new PathGradientBrush(glow))
                {
                    brush.CenterColor = Color.FromArgb((int)(58 * pulse), accent);
                    brush.SurroundColors = new Color[] { Color.FromArgb(0, accent) };
                    g.FillPath(brush, glow);
                }
            }

            RingTicks(g, cx, cy, r + stroke * 0.95f);

            using (var p = new Pen(Color.FromArgb(46, 60, 96), stroke))
            {
                p.StartCap = LineCap.Round; p.EndCap = LineCap.Round;
                g.DrawArc(p, cx - r, cy - r, r * 2, r * 2, 0, 360);
            }

            float sweep = (float)(360.0 * shown);
            if (sweep > 0.5f)
            {
                using (var p = new Pen(accent, stroke))
                {
                    p.StartCap = LineCap.Round; p.EndCap = LineCap.Round;
                    g.DrawArc(p, cx - r, cy - r, r * 2, r * 2, -90, -sweep);
                }
                // gloss on the leading edge
                using (var p = new Pen(Color.FromArgb(110, Color.White), Theme.F(2.5)))
                    g.DrawArc(p, cx - r + Theme.F(4), cy - r + Theme.F(4), (r - Theme.F(4)) * 2, (r - Theme.F(4)) * 2,
                              -90, -Math.Min(sweep, 26));
            }

            // auto-jump marker on the ring
            if (JumpOn && Duration > 0 && JumpAt < Duration)
            {
                double left = 1.0 - (JumpAt / Duration);
                double angle = (-90 - 360.0 * left) * Math.PI / 180.0;
                float mx = cx + (float)(Math.Cos(angle) * r);
                float my = cy + (float)(Math.Sin(angle) * r);
                using (var b = new SolidBrush(Theme.Green))
                    g.FillEllipse(b, mx - Theme.F(4.5), my - Theme.F(4.5), Theme.F(9), Theme.F(9));
                using (var p = new Pen(Color.FromArgb(90, Theme.Green), Theme.F(3)))
                    g.DrawEllipse(p, mx - Theme.F(7), my - Theme.F(7), Theme.F(14), Theme.F(14));
            }

            // ── centre readout ──
            var timeBox = new RectangleF(cx - r, cy - Theme.F(38), r * 2, Theme.F(58));
            Gfx.TextIn(g, timeText, Theme.MonoBig, accent, timeBox, Theme.Center);
            Gfx.TextIn(g, "SECONDS", Theme.ChipSm, Color.FromArgb(170, accent),
                       new RectangleF(cx - r, cy + Theme.F(24), r * 2, Theme.F(18)), Theme.Center);

            // ── state ──
            Gfx.TextIn(g, stateEn, Theme.StateEn, Theme.Text,
                       new RectangleF(0, Height * 0.585f, Width, Theme.F(26)), Theme.Center);
            Gfx.TextIn(g, stateAr, Theme.StateAr, Color.FromArgb(210, accent),
                       new RectangleF(0, Height * 0.635f, Width, Theme.F(24)), Theme.Center);

            // ── divider ──
            float dy = Height * 0.715f;
            using (var b = new LinearGradientBrush(
                new RectangleF(Theme.F(24), dy, Width - Theme.F(48), Theme.F(2)),
                Color.FromArgb(0, accent), Color.FromArgb(120, accent), LinearGradientMode.Horizontal))
            {
                b.SetBlendTriangularShape(0.5f);
                g.FillRectangle(b, Theme.F(24), dy, Width - Theme.F(48), Math.Max(1, Theme.F(1)));
            }

            // ── stat strip ──
            float sy = Height * 0.745f;
            float colW = (Width - Theme.F(32)) / 3f;
            Stat(g, Theme.F(16), sy, colW, "TRIGGER", KeyTag, Theme.Cyan);
            Stat(g, Theme.F(16) + colW, sy, colW, "DURATION", Duration.ToString("0.0", CultureInfo.InvariantCulture) + "s", Theme.Magenta);
            Stat(g, Theme.F(16) + colW * 2, sy, colW, "AUTO-JUMP",
                 JumpOn ? JumpAt.ToString("0.0", CultureInfo.InvariantCulture) + "s" : "OFF",
                 JumpOn ? Theme.Green : Theme.Dim);

            // ── footer hint ──
            Gfx.TextIn(g, "اضغط زر التحول داخل اللعبة ليبدأ العد تلقائياً", Theme.Label, Theme.Dim,
                       new RectangleF(0, Height - Theme.F(38), Width, Theme.F(20)), Theme.Center);

            // ── author credit ──
            Gfx.TextIn(g, "ChibiPulse  ·  by sraj", Theme.ChipSm, Color.FromArgb(150, accent),
                       new RectangleF(0, Height - Theme.F(20), Width, Theme.F(16)), Theme.Center);
        }

        void RingTicks(Graphics g, float cx, float cy, float radius)
        {
            const int count = 48;
            double filled = shown * count;
            using (var lit = new Pen(Color.FromArgb(150, accent), Theme.F(1.6)))
            using (var dark = new Pen(Color.FromArgb(46, 60, 96), Theme.F(1.4)))
            {
                for (int i = 0; i < count; i++)
                {
                    double a = (-90 - (360.0 * i / count)) * Math.PI / 180.0;
                    float len = (i % 4 == 0) ? Theme.F(7) : Theme.F(4);
                    float x1 = cx + (float)(Math.Cos(a) * radius);
                    float y1 = cy + (float)(Math.Sin(a) * radius);
                    float x2 = cx + (float)(Math.Cos(a) * (radius + len));
                    float y2 = cy + (float)(Math.Sin(a) * (radius + len));
                    g.DrawLine(i < filled ? lit : dark, x1, y1, x2, y2);
                }
            }
        }

        void Stat(Graphics g, float x, float y, float w, string label, string value, Color c)
        {
            Gfx.TextIn(g, label, Theme.ChipSm, Theme.Dim, new RectangleF(x, y, w, Theme.F(16)), Theme.Center);
            Gfx.TextIn(g, value, Theme.MonoMid, c, new RectangleF(x, y + Theme.F(18), w, Theme.F(26)), Theme.Center);
        }
    }

    // ═══════════════════════════════════════════════════════════════════════
    //  CONTROLS
    // ═══════════════════════════════════════════════════════════════════════
    public class TitleBar : Control
    {
        public TitleBar()
        {
            // SupportsTransparentBackColor must be enabled before a transparent BackColor is assigned.
            SetStyle(ControlStyles.SupportsTransparentBackColor | ControlStyles.OptimizedDoubleBuffer, true);
            DoubleBuffered = true;
            BackColor = Color.Transparent;
        }
    }

    public class IconButton : Control
    {
        public Color HoverColor = Theme.Red;
        bool hover;

        public IconButton()
        {
            SetStyle(ControlStyles.SupportsTransparentBackColor | ControlStyles.AllPaintingInWmPaint
                   | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint, true);
            DoubleBuffered = true;
            Cursor = Cursors.Hand;
            Font = Theme.Body;
            BackColor = Color.Transparent;
        }

        protected override void OnMouseEnter(EventArgs e) { hover = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { hover = false; Invalidate(); base.OnMouseLeave(e); }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            Gfx.Quality(g);
            var rc = new RectangleF(Theme.F(4), Theme.F(4), Width - Theme.F(8), Height - Theme.F(8));
            if (hover)
                using (var path = Gfx.Round(rc, Theme.F(8)))
                    Gfx.Fill(g, path, Color.FromArgb(190, HoverColor));
            Gfx.TextIn(g, Text, Font, hover ? Color.White : Theme.Dim, new RectangleF(0, 0, Width, Height), Theme.Center);
        }
    }

    /// <summary>Rounded settings card with a titled header rule.</summary>
    public class GlowCard : Control
    {
        public string Title = "";
        public string TitleAr = "";
        public Color Accent = Theme.Cyan;

        public GlowCard()
        {
            DoubleBuffered = true;
            BackColor = Theme.Bg;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer
                   | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            Gfx.Quality(g);

            var body = new RectangleF(0, 0, Width - 1, Height - 1);
            using (var path = Gfx.Round(body, Theme.F(14)))
            {
                Gfx.VerticalFill(g, path, body, Theme.CardTop, Theme.Card);
                Gfx.Stroke(g, path, Color.FromArgb(85, Accent), Theme.F(1.3));
            }

            Gfx.Text(g, Title, Theme.Section, Accent, Theme.F(20), Theme.F(12));
            Gfx.TextIn(g, TitleAr, Theme.Label, Theme.Dim,
                       new RectangleF(0, Theme.F(13), Width - Theme.F(20), Theme.F(18)), Theme.Right);

            using (var b = new LinearGradientBrush(
                new RectangleF(Theme.F(20), Theme.F(38), Width - Theme.F(40), Theme.F(2)),
                Color.FromArgb(120, Accent), Color.FromArgb(0, Accent), LinearGradientMode.Horizontal))
                g.FillRectangle(b, Theme.F(20), Theme.F(38), Width - Theme.F(40), Math.Max(1, Theme.F(1)));
        }
    }

    /// <summary>Rounded neon frame used to dress a plain ComboBox.</summary>
    public class InsetBox : Panel
    {
        public Color Accent = Theme.Cyan;

        public InsetBox()
        {
            SetStyle(ControlStyles.SupportsTransparentBackColor | ControlStyles.AllPaintingInWmPaint
                   | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
            DoubleBuffered = true;
            BackColor = Color.Transparent;
        }

        protected override void OnEnabledChanged(EventArgs e) { base.OnEnabledChanged(e); Invalidate(); }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            Gfx.Quality(g);
            var rc = new RectangleF(0, 0, Width - 1, Height - 1);
            using (var path = Gfx.Round(rc, Theme.F(9)))
            {
                Gfx.Fill(g, path, Theme.Inset);
                Gfx.Stroke(g, path, Color.FromArgb(Enabled ? 130 : 45, Accent), Theme.F(1.3));
            }
        }
    }

    /// <summary>
    /// Drop-down field drawn entirely by the app.
    ///
    /// This replaces ComboBox for one reason: the popup list's scrollbar is the one part of
    /// a ComboBox that cannot be restyled, and with ~100 key entries that stock white bar was
    /// the loudest thing on screen. Owning the popup also retires the old trick of repainting
    /// over the system drop-down button after every WM_PAINT.
    /// </summary>
    public class NeonSelect : Control
    {
        public readonly List<object> Items = new List<object>();
        public event EventHandler SelectedIndexChanged;

        Color accent = Theme.Cyan;
        int selected = -1;
        bool hover;
        int closedTick;
        DropList popup;

        public Color Accent
        {
            get { return accent; }
            set { accent = value; Invalidate(); }
        }

        public int SelectedIndex
        {
            get { return selected; }
            set
            {
                int v = (value < 0 || value >= Items.Count) ? -1 : value;
                if (v == selected) return;
                selected = v;
                Invalidate();
                if (SelectedIndexChanged != null) SelectedIndexChanged(this, EventArgs.Empty);
            }
        }

        public object SelectedItem
        {
            get { return (selected >= 0 && selected < Items.Count) ? Items[selected] : null; }
            set { SelectedIndex = Items.IndexOf(value); }
        }

        public bool IsOpen { get { return popup != null && !popup.IsDisposed && popup.Visible; } }

        public NeonSelect()
        {
            SetStyle(ControlStyles.SupportsTransparentBackColor | ControlStyles.AllPaintingInWmPaint
                   | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint, true);
            DoubleBuffered = true;
            Cursor = Cursors.Hand;
            Font = Theme.Body;
            BackColor = Color.Transparent;
        }

        protected override void OnMouseEnter(EventArgs e) { hover = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { hover = false; Invalidate(); base.OnMouseLeave(e); }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left) Toggle();
            base.OnMouseDown(e);
        }

        /// <summary>The wheel steps the value while closed, the way a ComboBox does.</summary>
        protected override void OnMouseWheel(MouseEventArgs e)
        {
            if (IsOpen || Items.Count == 0) return;
            int next = selected + (e.Delta > 0 ? -1 : 1);
            if (next >= 0 && next < Items.Count) SelectedIndex = next;
        }

        public void Toggle()
        {
            if (IsOpen) { popup.Close(); return; }
            if (Items.Count == 0) return;

            // The popup closes on deactivation, which lands just before this click is
            // delivered — without the guard, clicking an open field would reopen it.
            if (Environment.TickCount - closedTick < 250) return;

            popup = new DropList(this);
            popup.FormClosed += delegate { popup = null; closedTick = Environment.TickCount; Invalidate(); };

            var owner = FindForm();
            if (owner != null) popup.Owner = owner;

            popup.Show();
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            Gfx.Quality(g);

            float bw = Theme.F(22);
            var text = new RectangleF(Theme.F(9), 0, Width - bw - Theme.F(12), Height);
            Gfx.TextIn(g, SelectedItem == null ? "" : SelectedItem.ToString(), Font,
                       (hover || IsOpen) ? Color.White : accent, text, Theme.Left);

            float bx = Width - bw;
            using (var p = new Pen(Color.FromArgb(70, accent), 1))
                g.DrawLine(p, bx, Theme.F(7), bx, Height - Theme.F(7));

            Chevron(g, bx + bw / 2f, Height / 2f, accent, IsOpen);
        }

        /// <summary>Shared so the field and the popup always draw the same arrow.</summary>
        public static void Chevron(Graphics g, float cx, float cy, Color c, bool pointUp)
        {
            float dir = pointUp ? -1f : 1f;
            cy -= dir * Theme.F(0.7);                       // optical centring
            float w = Theme.F(4.6), h = Theme.F(2.9);

            var pts = new PointF[] {
                new PointF(cx - w, cy - h * dir),
                new PointF(cx,     cy + h * dir),
                new PointF(cx + w, cy - h * dir)
            };

            // One polyline with a round join, not two separate lines: the apex used to be
            // stroked twice, which showed as a lump at partial alpha.
            using (var glow = new Pen(Color.FromArgb(55, c), Theme.F(4.5)))
            {
                glow.StartCap = LineCap.Round; glow.EndCap = LineCap.Round; glow.LineJoin = LineJoin.Round;
                g.DrawLines(glow, pts);
            }
            using (var p = new Pen(Color.FromArgb(240, c), Theme.F(1.9)))
            {
                p.StartCap = LineCap.Round; p.EndCap = LineCap.Round; p.LineJoin = LineJoin.Round;
                g.DrawLines(p, pts);
            }
        }
    }

    /// <summary>
    /// The popup half of NeonSelect. Rows and scrollbar are both painted here, so no part
    /// of the list comes from the Windows theme.
    /// </summary>
    public class DropList : Form
    {
        [DllImport("gdi32.dll")] static extern IntPtr CreateRoundRectRgn(int l, int t, int r, int b, int w, int h);

        const int MaxRows = 13;

        readonly NeonSelect host;
        readonly int rowH, padV, barW, barGap;

        int scroll;                 // pixels from the top of the content
        int hoverRow = -1;
        bool overBar, dragging;
        int dragGrab;
        string typed = "";
        int typedTick;

        public DropList(NeonSelect owner)
        {
            host = owner;
            rowH = Theme.P(24);
            padV = Theme.P(5);
            barW = Theme.P(7);
            barGap = Theme.P(4);

            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.Manual;
            ShowInTaskbar = false;
            BackColor = Theme.Inset;
            Font = Theme.Body;
            DoubleBuffered = true;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer
                   | ControlStyles.UserPaint, true);

            // Line the popup up with the visible neon frame, not the inset field inside it.
            Control anchor = (host.Parent is InsetBox) ? host.Parent : (Control)host;
            var field = new Rectangle(anchor.PointToScreen(Point.Empty), anchor.Size);

            int rows = Math.Min(MaxRows, host.Items.Count);
            int w = field.Width;
            int h = rows * rowH + padV * 2;

            // Flip above the field when there is no room below, and never hang off the edge.
            var work = Screen.FromRectangle(field).WorkingArea;
            int y = field.Bottom + Theme.P(3);
            if (y + h > work.Bottom) y = Math.Max(work.Top, field.Top - Theme.P(3) - h);
            int x = Math.Max(work.Left, Math.Min(field.Left, work.Right - w));

            Bounds = new Rectangle(x, y, w, h);
            CentreOnSelection(rows);
        }

        protected override CreateParams CreateParams
        {
            get { var cp = base.CreateParams; cp.ClassStyle |= 0x00020000; return cp; }   // CS_DROPSHADOW
        }

        int Content { get { return host.Items.Count * rowH; } }
        int Viewport { get { return ClientSize.Height - padV * 2; } }
        int MaxScroll { get { return Math.Max(0, Content - Viewport); } }
        bool NeedsBar { get { return Content > Viewport; } }

        void CentreOnSelection(int rows)
        {
            if (host.SelectedIndex < 0) return;
            scroll = Math.Max(0, Math.Min(MaxScroll, host.SelectedIndex * rowH - (rows / 2) * rowH));
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            Region = Region.FromHrgn(CreateRoundRectRgn(0, 0, Width + 1, Height + 1, Theme.P(11), Theme.P(11)));
        }

        protected override void OnDeactivate(EventArgs e) { base.OnDeactivate(e); Close(); }

        // ── geometry ──
        RectangleF ThumbRect()
        {
            float track = Viewport;
            float thumbH = Math.Max(Theme.F(28), track * Viewport / (float)Content);
            float t = MaxScroll <= 0 ? 0f : scroll / (float)MaxScroll;
            return new RectangleF(ClientSize.Width - barGap - barW, padV + t * (track - thumbH), barW, thumbH);
        }

        int RowAt(int y)
        {
            if (y < padV || y > padV + Viewport) return -1;
            int i = (y - padV + scroll) / rowH;
            return (i >= 0 && i < host.Items.Count) ? i : -1;
        }

        void SetScroll(int v)
        {
            int c = Math.Max(0, Math.Min(MaxScroll, v));
            if (c == scroll) return;
            scroll = c;
            Invalidate();
        }

        void EnsureVisible(int i)
        {
            int top = i * rowH;
            if (top < scroll) SetScroll(top);
            else if (top + rowH > scroll + Viewport) SetScroll(top + rowH - Viewport);
        }

        void Commit(int i)
        {
            if (i >= 0 && i < host.Items.Count) host.SelectedIndex = i;
            Close();
        }

        // ── mouse ──
        protected override void OnMouseDown(MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Left) { base.OnMouseDown(e); return; }

            if (NeedsBar)
            {
                var thumb = ThumbRect();
                if (thumb.Contains(e.X, e.Y))
                {
                    dragging = true;
                    dragGrab = (int)(e.Y - thumb.Y);
                    Capture = true;
                    Invalidate();
                    return;
                }
                if (e.X >= ClientSize.Width - barGap - barW)          // paging click on the rail
                {
                    SetScroll(scroll + (e.Y < thumb.Y ? -Viewport : Viewport));
                    return;
                }
            }

            Commit(RowAt(e.Y));
            base.OnMouseDown(e);
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            if (dragging)
            {
                float span = Viewport - ThumbRect().Height;
                float t = span <= 0 ? 0f : (e.Y - padV - dragGrab) / span;
                SetScroll((int)Math.Round(Math.Max(0f, Math.Min(1f, t)) * MaxScroll));
                base.OnMouseMove(e);
                return;
            }

            bool ob = NeedsBar && ThumbRect().Contains(e.X, e.Y);
            int row = ob ? -1 : RowAt(e.Y);
            if (ob != overBar || row != hoverRow) { overBar = ob; hoverRow = row; Invalidate(); }
            base.OnMouseMove(e);
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            if (dragging) { dragging = false; Capture = false; Invalidate(); }
            base.OnMouseUp(e);
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            if (!dragging && (hoverRow != -1 || overBar)) { hoverRow = -1; overBar = false; Invalidate(); }
            base.OnMouseLeave(e);
        }

        protected override void OnMouseWheel(MouseEventArgs e)
        {
            SetScroll(scroll - Math.Sign(e.Delta) * rowH * 3);
        }

        // ── keyboard ──
        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            switch (keyData)
            {
                case Keys.Escape: Close(); return true;
                case Keys.Return: Commit(hoverRow >= 0 ? hoverRow : host.SelectedIndex); return true;
                case Keys.Up: StepHover(-1); return true;
                case Keys.Down: StepHover(1); return true;
                case Keys.PageUp: StepHover(-Math.Max(1, Viewport / rowH)); return true;
                case Keys.PageDown: StepHover(Math.Max(1, Viewport / rowH)); return true;
                case Keys.Home: StepHover(-host.Items.Count); return true;
                case Keys.End: StepHover(host.Items.Count); return true;
            }
            return base.ProcessCmdKey(ref msg, keyData);
        }

        void StepHover(int delta)
        {
            int cur = hoverRow >= 0 ? hoverRow : Math.Max(0, host.SelectedIndex);
            hoverRow = Math.Max(0, Math.Min(host.Items.Count - 1, cur + delta));
            EnsureVisible(hoverRow);
            Invalidate();
        }

        protected override void OnKeyPress(KeyPressEventArgs e)
        {
            if (e.KeyChar >= ' ')
            {
                // The same incremental search a ComboBox gives for free: "F" then "8" finds F8.
                int now = Environment.TickCount;
                if (now - typedTick > 900) typed = "";
                typedTick = now;
                typed += char.ToUpperInvariant(e.KeyChar);

                int found = Find(typed);
                if (found < 0 && typed.Length > 1)
                {
                    typed = typed.Substring(typed.Length - 1);   // restart the search on this key
                    found = Find(typed);
                }
                if (found >= 0) { hoverRow = found; EnsureVisible(found); Invalidate(); }
            }
            base.OnKeyPress(e);
        }

        int Find(string prefix)
        {
            for (int i = 0; i < host.Items.Count; i++)
                if (host.Items[i].ToString().ToUpperInvariant().StartsWith(prefix)) return i;
            return -1;
        }

        // ── painting ──
        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            Gfx.Quality(g);

            var body = new RectangleF(0, 0, Width - 1, Height - 1);
            using (var path = Gfx.Round(body, Theme.F(11)))
            {
                Gfx.Fill(g, path, Theme.Inset);
                Gfx.Stroke(g, path, Color.FromArgb(150, host.Accent), Theme.F(1.3));
            }

            int listW = ClientSize.Width - (NeedsBar ? barW + barGap * 2 : 0);
            g.SetClip(new Rectangle(0, padV, listW, Viewport));

            int first = Math.Max(0, scroll / rowH);
            int last = Math.Min(host.Items.Count - 1, (scroll + Viewport) / rowH);

            for (int i = first; i <= last; i++)
            {
                float y = padV + i * rowH - scroll;
                var row = new RectangleF(Theme.F(4), y, listW - Theme.F(8), rowH);
                bool sel = i == host.SelectedIndex;
                bool hot = i == hoverRow;

                if (sel || hot)
                    using (var path = Gfx.Round(row, Theme.F(6)))
                        Gfx.Fill(g, path, Color.FromArgb(hot ? 58 : 32, host.Accent));

                // A bar marks the current value even while the pointer highlights another row.
                if (sel)
                    using (var b = new SolidBrush(host.Accent))
                        g.FillRectangle(b, row.X + Theme.F(3), y + Theme.F(6), Theme.F(2.5), rowH - Theme.F(12));

                Gfx.TextIn(g, host.Items[i].ToString(), Font, (hot || sel) ? Color.White : host.Accent,
                           new RectangleF(row.X + Theme.F(13), y, row.Width - Theme.F(17), rowH), Theme.Left);
            }

            g.ResetClip();

            if (NeedsBar) PaintScrollBar(g);
        }

        /// <summary>The reason this class exists: a scrollbar in the app's own palette
        /// instead of the stock white one a ComboBox forces on you.</summary>
        void PaintScrollBar(Graphics g)
        {
            float x = ClientSize.Width - barGap - barW;

            using (var rail = Gfx.Round(x, padV, barW, Viewport, barW / 2f))
                Gfx.Fill(g, rail, Color.FromArgb(90, 30, 40, 68));

            var t = ThumbRect();
            using (var thumb = Gfx.Round(t, t.Width / 2f))
            {
                if (dragging || overBar) Gfx.Glow(g, thumb, host.Accent, Theme.F(4), 75);
                Gfx.Fill(g, thumb, Color.FromArgb(dragging ? 255 : overBar ? 225 : 160, host.Accent));
            }
        }
    }

    /// <summary>Switch with an animated knob and a bilingual caption.</summary>
    public class NeonToggle : Control
    {
        public Color Accent = Theme.Cyan;
        bool on;
        float knob;
        readonly Timer anim;

        public event EventHandler CheckedChanged;

        public bool Checked
        {
            get { return on; }
            set { on = value; knob = value ? 1f : 0f; Invalidate(); }
        }

        public NeonToggle()
        {
            SetStyle(ControlStyles.SupportsTransparentBackColor | ControlStyles.AllPaintingInWmPaint
                   | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint, true);
            DoubleBuffered = true;
            Cursor = Cursors.Hand;
            Font = Theme.Body;
            ForeColor = Theme.Sub;
            BackColor = Color.Transparent;

            anim = new Timer { Interval = 16 };
            anim.Tick += delegate
            {
                float goal = on ? 1f : 0f;
                knob += (goal - knob) * 0.3f;
                if (Math.Abs(goal - knob) < 0.01f) { knob = goal; anim.Stop(); }
                Invalidate();
            };
        }

        protected override void OnClick(EventArgs e)
        {
            on = !on;
            anim.Start();
            if (CheckedChanged != null) CheckedChanged(this, EventArgs.Empty);
            Invalidate();
            base.OnClick(e);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            Gfx.Quality(g);

            float tw = Theme.F(40), th = Theme.F(20), ty = (Height - th) / 2f;
            var track = new RectangleF(0, ty, tw, th);
            Color live = on ? Accent : Color.FromArgb(64, 78, 112);

            using (var path = Gfx.Round(track, th / 2f))
            {
                Gfx.Fill(g, path, on ? Color.FromArgb(52, Accent) : Color.FromArgb(24, 30, 50));
                if (on) Gfx.Glow(g, path, Accent, Theme.F(5), 40);
                Gfx.Stroke(g, path, live, Theme.F(1.4));
            }

            float kd = th - Theme.F(6);
            float kx = Theme.F(3) + knob * (tw - kd - Theme.F(6));
            using (var b = new SolidBrush(on ? Accent : Color.FromArgb(104, 118, 156)))
                g.FillEllipse(b, kx, ty + Theme.F(3), kd, kd);

            Gfx.TextIn(g, Text, Font, Enabled ? ForeColor : Theme.Dim,
                       new RectangleF(tw + Theme.F(12), 0, Width - tw - Theme.F(12), Height), Theme.Left);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing && anim != null) anim.Dispose();
            base.Dispose(disposing);
        }
    }

    /// <summary>Numeric stepper: click or hold −/+, or scroll the wheel.</summary>
    public class NeonSpinner : Control
    {
        public double Min = 0, Max = 100, Step = 0.5;

        /// <summary>
        /// Coarse step for the outer pair of buttons — ten of the fine step, so the digit
        /// one place up moves by one. Leave it at 0 to show only the fine buttons.
        /// </summary>
        public double BigStep = 0;

        public Color Accent = Theme.Cyan;
        public event EventHandler ValueChanged;

        double val = 10;
        int direction;
        int repeats;        // ticks held, so a long press can graduate to a bigger step
        int hoverZone;      // -2 coarse minus, -1 fine minus, 0 none, +1 fine plus, +2 coarse plus
        readonly Timer repeat;

        public double Value
        {
            get { return val; }
            set
            {
                double clamped = Math.Max(Min, Math.Min(Max, Math.Round(value, 2)));
                if (Math.Abs(clamped - val) < 0.0001) return;
                val = clamped;
                Invalidate();
                if (ValueChanged != null) ValueChanged(this, EventArgs.Empty);
            }
        }

        /// <summary>Set the displayed value without raising ValueChanged (used while loading).</summary>
        public void SetValueQuiet(double v)
        {
            val = Math.Max(Min, Math.Min(Max, Math.Round(v, 2)));
            Invalidate();
        }

        public NeonSpinner()
        {
            SetStyle(ControlStyles.SupportsTransparentBackColor | ControlStyles.AllPaintingInWmPaint
                   | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint, true);
            DoubleBuffered = true;
            Cursor = Cursors.Hand;
            Font = Theme.Mono;
            BackColor = Color.Transparent;

            repeat = new Timer { Interval = 380 };
            repeat.Tick += delegate { repeat.Interval = 55; repeats++; Bump(); };
        }

        // Zone widths measured against the text they hold: "+0.1" needs 26px, "−" needs 14,
        // and the value can still read "120.00" at 55px in the 60px left between them.
        int Fine { get { return Theme.P(26); } }
        int Coarse { get { return BigStep > 0 ? Theme.P(30) : 0; } }

        int ZoneAt(int x)
        {
            int c = Coarse, f = Fine;
            if (c > 0 && x < c) return -2;
            if (x < c + f) return -1;
            if (c > 0 && x > Width - c) return 2;
            if (x > Width - c - f) return 1;
            return 0;
        }

        void Bump()
        {
            if (direction == 0) return;
            double d = (Math.Abs(direction) == 2) ? BigStep : Step;

            // Held down, either button graduates one more digit up, so a long press can
            // still cross a wide range: 0.01 -> 0.1 on the fine pair, 0.1 -> 1 on the coarse.
            if (repeats > 15) d *= 10;

            Value = val + Math.Sign(direction) * d;
        }

        static string StepLabel(double d) { return d.ToString("0.##", CultureInfo.InvariantCulture); }

        protected override void OnMouseEnter(EventArgs e)
        {
            if (CanFocus) Focus();          // lets the wheel work without a click
            base.OnMouseEnter(e);
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            int z = ZoneAt(e.X);
            if (z != hoverZone) { hoverZone = z; Invalidate(); }
            base.OnMouseMove(e);
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            direction = ZoneAt(e.X);
            if (direction == 0) return;
            repeats = 0;
            Bump();
            repeat.Interval = 380;
            repeat.Start();
        }

        protected override void OnMouseUp(MouseEventArgs e) { repeat.Stop(); direction = 0; repeats = 0; base.OnMouseUp(e); }

        protected override void OnMouseLeave(EventArgs e)
        {
            repeat.Stop();
            direction = 0;
            repeats = 0;
            hoverZone = 0;
            Invalidate();
            base.OnMouseLeave(e);
        }

        protected override void OnMouseWheel(MouseEventArgs e)
        {
            // Hold Shift for the coarse step, matching the outer pair of buttons.
            double d = (BigStep > 0 && (ModifierKeys & Keys.Shift) != 0) ? BigStep : Step;
            Value = val + (e.Delta > 0 ? d : -d);
            var handled = e as HandledMouseEventArgs;
            if (handled != null) handled.Handled = true;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            Gfx.Quality(g);

            var rc = new RectangleF(0, 0, Width - 1, Height - 1);
            using (var path = Gfx.Round(rc, Theme.F(9)))
            {
                Gfx.Fill(g, path, Theme.Inset);
                Gfx.Stroke(g, path, Color.FromArgb(110, Accent), Theme.F(1.3));
            }

            int c = Coarse, f = Fine;

            if (c > 0)
            {
                string big = StepLabel(BigStep);
                DrawZone(g, "−" + big, new RectangleF(0, 0, c, Height), hoverZone == -2, Theme.ChipSm);
                DrawZone(g, "+" + big, new RectangleF(Width - c, 0, c, Height), hoverZone == 2, Theme.ChipSm);

                // Hairlines separating coarse from fine, so the four zones read as four buttons.
                using (var p = new Pen(Color.FromArgb(55, Accent), 1))
                {
                    g.DrawLine(p, c, Theme.F(6), c, Height - Theme.F(6));
                    g.DrawLine(p, Width - c, Theme.F(6), Width - c, Height - Theme.F(6));
                }
            }

            DrawZone(g, "−", new RectangleF(c, 0, f, Height), hoverZone == -1, Font);
            DrawZone(g, "+", new RectangleF(Width - c - f, 0, f, Height), hoverZone == 1, Font);

            Gfx.TextIn(g, val.ToString("0.00", CultureInfo.InvariantCulture), Font, Accent,
                       new RectangleF(c + f, 0, Width - (c + f) * 2, Height), Theme.Center);
        }

        void DrawZone(Graphics g, string glyph, RectangleF rc, bool hot, Font font)
        {
            if (hot)
                using (var path = Gfx.Round(rc.X + Theme.F(3), rc.Y + Theme.F(3), rc.Width - Theme.F(6), rc.Height - Theme.F(6), Theme.F(6)))
                    Gfx.Fill(g, path, Color.FromArgb(40, Accent));
            Gfx.TextIn(g, glyph, font, hot ? Accent : Theme.Sub, rc, Theme.Center);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing && repeat != null) repeat.Dispose();
            base.Dispose(disposing);
        }
    }

    /// <summary>Small preset button.</summary>
    public class ChipButton : Control
    {
        public Color Accent = Theme.Cyan;
        bool hover, down;

        public ChipButton()
        {
            SetStyle(ControlStyles.SupportsTransparentBackColor | ControlStyles.AllPaintingInWmPaint
                   | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint, true);
            DoubleBuffered = true;
            Cursor = Cursors.Hand;
            Font = Theme.Chip;
            BackColor = Color.Transparent;
        }

        protected override void OnMouseEnter(EventArgs e) { hover = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { hover = false; down = false; Invalidate(); base.OnMouseLeave(e); }
        protected override void OnMouseDown(MouseEventArgs e) { down = true; Invalidate(); base.OnMouseDown(e); }
        protected override void OnMouseUp(MouseEventArgs e) { down = false; Invalidate(); base.OnMouseUp(e); }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            Gfx.Quality(g);
            var rc = new RectangleF(0, 0, Width - 1, Height - 1);
            using (var path = Gfx.Round(rc, Theme.F(8)))
            {
                Gfx.Fill(g, path, down ? Color.FromArgb(80, Accent) : hover ? Color.FromArgb(42, Accent) : Theme.Inset);
                Gfx.Stroke(g, path, Color.FromArgb(hover ? 190 : 90, Accent), Theme.F(1.2));
            }
            Gfx.TextIn(g, Text, Font, hover ? Color.White : Accent, rc, Theme.Center);
        }
    }

    /// <summary>Primary action button with a neon gradient body.</summary>
    public class GlowButton : Control
    {
        public Color Accent = Theme.Cyan;
        public float Radius = 14f;
        bool hover, down;

        public GlowButton()
        {
            DoubleBuffered = true;
            Cursor = Cursors.Hand;
            BackColor = Theme.Bg;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint, true);
        }

        protected override void OnMouseEnter(EventArgs e) { hover = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { hover = false; down = false; Invalidate(); base.OnMouseLeave(e); }
        protected override void OnMouseDown(MouseEventArgs e) { down = true; Invalidate(); base.OnMouseDown(e); }
        protected override void OnMouseUp(MouseEventArgs e) { down = false; Invalidate(); base.OnMouseUp(e); }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            Gfx.Quality(g);

            var rc = new RectangleF(1, 1, Width - 3, Height - 3);
            using (var path = Gfx.Round(rc, Theme.F(Radius)))
            {
                if (hover) Gfx.Glow(g, path, Accent, Theme.F(9), 55);

                Color top = down ? Color.FromArgb(235, Accent) : hover ? Color.FromArgb(190, Accent) : Color.FromArgb(120, Accent);
                Color bottom = Color.FromArgb(down ? 90 : 34, Accent);
                using (var b = new LinearGradientBrush(new RectangleF(rc.X, rc.Y - 1, rc.Width, rc.Height + 2),
                                                       top, bottom, LinearGradientMode.Vertical))
                    g.FillPath(b, path);

                Gfx.Stroke(g, path, Accent, hover ? Theme.F(2.2) : Theme.F(1.5));
            }

            Gfx.TextIn(g, Text, Font, down ? Color.FromArgb(10, 12, 22) : Color.White,
                       new RectangleF(0, 0, Width, Height), Theme.Center);
        }
    }

    // ═══════════════════════════════════════════════════════════════════════
    //  FLOATING HUD
    // ═══════════════════════════════════════════════════════════════════════
    public class OverlayWindow : Form
    {
        [DllImport("user32.dll")] static extern bool ReleaseCapture();
        [DllImport("user32.dll")] static extern int SendMessage(IntPtr hWnd, int msg, int wParam, int lParam);
        [DllImport("gdi32.dll")] static extern IntPtr CreateRoundRectRgn(int l, int t, int r, int b, int w, int h);
        [DllImport("user32.dll")] static extern int GetWindowLong(IntPtr hWnd, int index);
        [DllImport("user32.dll")] static extern int SetWindowLong(IntPtr hWnd, int index, int newLong);

        const int GWL_EXSTYLE = -20, WS_EX_TRANSPARENT = 0x20, WS_EX_LAYERED = 0x80000;

        AppState state = AppState.Idle;
        double remain, progress = 1;
        Color accent = Theme.Green;
        bool jump;
        bool locked;

        // repaint only when something visible actually changed
        string lastKey = "";

        public OverlayWindow()
        {
            Text = "ChibiPulse HUD";
            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.Manual;
            ClientSize = new Size(Theme.P(300), Theme.P(88));
            TopMost = true;
            ShowInTaskbar = false;
            BackColor = Theme.Bg;
            Opacity = 0.96;
            DoubleBuffered = true;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint, true);

            MouseDown += delegate(object s, MouseEventArgs e)
            {
                if (e.Button != MouseButtons.Left) return;
                ReleaseCapture();
                SendMessage(Handle, 0xA1, 2, 0);
            };
        }

        /// <summary>When locked the HUD ignores the mouse so clicks reach the game.</summary>
        public bool ClickThrough
        {
            get { return locked; }
            set
            {
                locked = value;
                if (!IsHandleCreated) return;
                int ex = GetWindowLong(Handle, GWL_EXSTYLE);
                ex = value ? (ex | WS_EX_TRANSPARENT | WS_EX_LAYERED) : (ex & ~WS_EX_TRANSPARENT);
                SetWindowLong(Handle, GWL_EXSTYLE, ex);
                Invalidate();
            }
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            Region = Region.FromHrgn(CreateRoundRectRgn(0, 0, Width + 1, Height + 1, Theme.P(16), Theme.P(16)));
            ClickThrough = locked;
        }

        public void Push(AppState s, double rem, double prog, Color ac, bool autoJump)
        {
            string key = ((int)s) + "|" + rem.ToString("0.00", CultureInfo.InvariantCulture) + "|" + ac.ToArgb() + "|" + autoJump;
            if (key == lastKey) return;
            lastKey = key;

            state = s; remain = rem; progress = Math.Max(0, Math.Min(1, prog)); accent = ac; jump = autoJump;
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            Gfx.Quality(g);

            var body = new RectangleF(0, 0, Width - 1, Height - 1);
            using (var b = new LinearGradientBrush(new Rectangle(0, 0, Width, Height),
                                                   Color.FromArgb(20, 26, 46), Color.FromArgb(8, 10, 20),
                                                   LinearGradientMode.ForwardDiagonal))
                g.FillRectangle(b, 0, 0, Width, Height);

            using (var path = Gfx.Round(body, Theme.F(15)))
                Gfx.Stroke(g, path, Color.FromArgb(170, accent), Theme.F(1.6));

            using (var b = new SolidBrush(accent))
                g.FillRectangle(b, 0, Theme.F(14), Theme.F(3), Height - Theme.F(28));

            // ── mini ring ──
            float d = Theme.F(56), rx = Width - d - Theme.F(18), ry = (Height - d) / 2f;
            using (var p = new Pen(Color.FromArgb(46, 60, 96), Theme.F(6)))
                g.DrawArc(p, rx, ry, d, d, 0, 360);

            float sweep = (float)(360.0 * progress);
            if (sweep > 0.5f)
                using (var p = new Pen(accent, Theme.F(6)))
                {
                    p.StartCap = LineCap.Round; p.EndCap = LineCap.Round;
                    g.DrawArc(p, rx, ry, d, d, -90, -sweep);
                }

            if (jump)
                Gfx.TextIn(g, "⚡", Theme.Chip, Theme.Green, new RectangleF(rx, ry, d, d), Theme.Center);

            // ── text block ──
            string label = state == AppState.Idle ? "READY"
                         : state == AppState.Active ? "TINY ACTIVE"
                         : state == AppState.Warning ? "BIG SOON" : "COOLDOWN";

            Gfx.Text(g, label, Theme.ChipSm, Color.FromArgb(200, accent), Theme.F(16), Theme.F(13));
            Gfx.Text(g, remain.ToString("00.00", CultureInfo.InvariantCulture) + "s", Theme.MonoOv, accent, Theme.F(14), Theme.F(30));

            // Author credit. Sits in the gap between the readout and the ring: the widest
            // the time can get is "120.00s", which still ends left of this band.
            Gfx.TextIn(g, "by sraj", Theme.ChipSm, Color.FromArgb(135, accent),
                       new RectangleF(Width - Theme.F(140), Theme.F(5), Theme.F(62), Theme.F(14)), Theme.Right);

            // Pushed below the credit so the two never overlap.
            if (locked)
                Gfx.TextIn(g, "LOCKED", Theme.ChipSm, Theme.Dim,
                           new RectangleF(Width - Theme.F(150), Theme.F(24), Theme.F(70), Theme.F(16)), Theme.Right);

            // ── progress rail ──
            float railX = Theme.F(14), railW = Width - Theme.F(100), railY = Height - Theme.F(14);
            using (var b = new SolidBrush(Color.FromArgb(30, 40, 66)))
                g.FillRectangle(b, railX, railY, railW, Theme.F(4));
            float fill = railW * (float)progress;
            if (fill > 1)
                using (var b = new LinearGradientBrush(new RectangleF(railX, railY, Math.Max(2, fill), Theme.F(4)),
                                                       Theme.Magenta, accent, LinearGradientMode.Horizontal))
                    g.FillRectangle(b, railX, railY, fill, Theme.F(4));
        }
    }
}
