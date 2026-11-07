// ClockNote - a tiny always-on-top, frameless clock with a note pad.
// Layout: 300px note area on the left, 100x100 analog clock on the right.
// Drag with the RIGHT mouse button. Settings live in ClockNote.json next to the exe.

using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Runtime.InteropServices;
using System.Web.Script.Serialization;
using System.Windows.Forms;

namespace NoteClock
{
    public class Settings
    {
        public int X { get; set; }
        public int Y { get; set; }
        public double Opacity { get; set; }
        public string Note { get; set; }

        public Settings() { X = 100; Y = 100; Opacity = 0.85; Note = ""; }
    }

    public class ClockPanel : Panel
    {
        public bool Active;

        public ClockPanel()
        {
            DoubleBuffered = true;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer, true);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(BackColor);

            float cx = Width / 2f, cy = Height / 2f;
            float r = Math.Min(Width, Height) / 2f - 6;

            using (Pen face = new Pen(Color.FromArgb(200, 200, 200), 2))
                g.DrawEllipse(face, cx - r, cy - r, 2 * r, 2 * r);

            using (Pen tick = new Pen(Color.FromArgb(170, 170, 170), 1.5f))
            {
                for (int i = 0; i < 12; i++)
                {
                    double a = i * Math.PI / 6;
                    float inner = (i % 3 == 0) ? r - 9 : r - 5;
                    g.DrawLine(tick,
                        cx + (float)Math.Sin(a) * inner, cy - (float)Math.Cos(a) * inner,
                        cx + (float)Math.Sin(a) * (r - 1), cy - (float)Math.Cos(a) * (r - 1));
                }
            }

            DateTime now = DateTime.Now;
            double sec = now.Second;
            double min = now.Minute + sec / 60.0;
            double hr = (now.Hour % 12) + min / 60.0;

            // Unfocused: digital time sits under the hands. Focused: it goes on top (with seconds).
            if (!Active) DrawDigital(g, now, cx, cy, r);

            DrawHand(g, cx, cy, hr * Math.PI / 6, r * 0.5f, 3.5f, Color.White);
            DrawHand(g, cx, cy, min * Math.PI / 30, r * 0.75f, 2.5f, Color.White);
            DrawHand(g, cx, cy, sec * Math.PI / 30, r * 0.85f, 1f, Color.OrangeRed);

            using (Brush hub = new SolidBrush(Color.OrangeRed))
                g.FillEllipse(hub, cx - 3, cy - 3, 6, 6);

            if (Active) DrawDigital(g, now, cx, cy, r);
        }

        void DrawDigital(Graphics g, DateTime now, float cx, float cy, float r)
        {
            string digital = now.ToString(Active ? "HH:mm:ss" : "HH:mm");
            SizeF ts = g.MeasureString(digital, Font);
            RectangleF box = new RectangleF(cx - ts.Width / 2, cy + r * 0.35f - ts.Height / 2, ts.Width, ts.Height);
            // On top of the hands, a face-colored patch keeps the text readable.
            if (Active)
                using (Brush bg = new SolidBrush(BackColor))
                    g.FillRectangle(bg, box.X + 2, box.Y + 1, box.Width - 4, box.Height - 2);
            using (Brush text = new SolidBrush(Color.Gainsboro))
                g.DrawString(digital, Font, text, box.X, box.Y);
        }

        static void DrawHand(Graphics g, float cx, float cy, double angle, float len, float width, Color color)
        {
            using (Pen p = new Pen(color, width))
            {
                p.StartCap = LineCap.Round;
                p.EndCap = LineCap.Round;
                g.DrawLine(p, cx, cy, cx + (float)Math.Sin(angle) * len, cy - (float)Math.Cos(angle) * len);
            }
        }
    }

    public class ClockForm : Form
    {
        static readonly string SettingsPath = Path.Combine(Application.StartupPath, "ClockNote.json");

        Settings settings;
        TextBox note;
        ClockPanel clock;
        NotifyIcon tray;
        Timer tickTimer;
        Timer saveTimer;
        Timer hideTimer;

        bool dragging;
        Point dragOffset;

        public ClockForm()
        {
            settings = LoadSettings();

            Text = "ClockNote";
            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            TopMost = true;
            StartPosition = FormStartPosition.Manual;
            ClientSize = new Size(400, 100);
            BackColor = Color.FromArgb(25, 25, 25);
            Opacity = Clamp(settings.Opacity);
            Location = OnScreenLocation(new Point(settings.X, settings.Y));

            note = new TextBox();
            note.Multiline = true;
            note.AcceptsReturn = true;
            note.AcceptsTab = true;
            note.WordWrap = true;
            note.ScrollBars = ScrollBars.None;
            note.BorderStyle = BorderStyle.None;
            note.BackColor = Color.FromArgb(35, 35, 35);
            note.ForeColor = Color.Gainsboro;
            note.Font = new Font("Consolas", 9f);
            note.Bounds = new Rectangle(4, 4, 292, 92);
            note.Text = settings.Note ?? "";
            note.ContextMenu = new ContextMenu(); // suppress default right-click menu so right-drag works
            note.TextChanged += delegate { saveTimer.Stop(); saveTimer.Start(); };
            // TextBox selects all text when it first gets focus; put the caret at the end instead so typing can't wipe the note.
            note.GotFocus += delegate { BeginInvoke((MethodInvoker)delegate { if (note.SelectionLength == note.TextLength) note.Select(note.TextLength, 0); }); };

            clock = new ClockPanel();
            clock.Bounds = new Rectangle(300, 0, 100, 100);
            clock.BackColor = BackColor;
            clock.Font = note.Font;

            Controls.Add(note);
            Controls.Add(clock);

            // While focused the digital time shows seconds and is drawn above the hands.
            Activated += delegate { clock.Active = true; clock.Invalidate(); };
            Deactivate += delegate { clock.Active = false; clock.Invalidate(); };

            HookDrag(this);
            HookDrag(note);
            HookDrag(clock);

            tickTimer = new Timer();
            tickTimer.Interval = 1000;
            tickTimer.Tick += delegate { clock.Invalidate(); };
            tickTimer.Start();

            // Debounce note saves so we don't write on every keystroke.
            saveTimer = new Timer();
            saveTimer.Interval = 800;
            saveTimer.Tick += delegate { saveTimer.Stop(); SaveSettings(); };

            // Double-click the clock face: hide for 10 s so whatever is behind can be clicked.
            hideTimer = new Timer();
            hideTimer.Interval = 10000;
            hideTimer.Tick += delegate { hideTimer.Stop(); ShowNoActivate(); };
            clock.MouseDoubleClick += (s, e) =>
            {
                if (e.Button != MouseButtons.Left) return;
                Hide();
                hideTimer.Start();
            };

            BuildTray();
        }

        [DllImport("user32.dll")] static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);
        [DllImport("user32.dll")] static extern bool SetWindowPos(IntPtr hWnd, IntPtr after, int x, int y, int cx, int cy, uint flags);

        // Reappear (after the 10 s hide) without taking keyboard focus from whatever the user is in.
        // Form.Show() would re-activate the window, so go through Win32 directly.
        void ShowNoActivate()
        {
            const int SW_SHOWNOACTIVATE = 4;
            const uint SWP_NOSIZE = 0x1, SWP_NOMOVE = 0x2, SWP_NOACTIVATE = 0x10;
            ShowWindow(Handle, SW_SHOWNOACTIVATE);
            SetWindowPos(Handle, new IntPtr(-1) /* HWND_TOPMOST */, 0, 0, 0, 0, SWP_NOSIZE | SWP_NOMOVE | SWP_NOACTIVATE);
        }

        // WS_EX_TOOLWINDOW keeps it out of Alt+Tab.
        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams cp = base.CreateParams;
                cp.ExStyle |= 0x80;
                return cp;
            }
        }

        void HookDrag(Control c)
        {
            c.MouseDown += (s, e) =>
            {
                if (e.Button != MouseButtons.Right) return;
                dragging = true;
                dragOffset = new Point(Cursor.Position.X - Left, Cursor.Position.Y - Top);
                ((Control)s).Capture = true;
            };
            c.MouseMove += (s, e) =>
            {
                if (!dragging) return;
                Location = new Point(Cursor.Position.X - dragOffset.X, Cursor.Position.Y - dragOffset.Y);
            };
            c.MouseUp += (s, e) =>
            {
                if (e.Button != MouseButtons.Right || !dragging) return;
                dragging = false;
                ((Control)s).Capture = false;
                SaveSettings();
            };
        }

        void BuildTray()
        {
            ContextMenu menu = new ContextMenu();
            MenuItem opacityMenu = new MenuItem("Transparency");
            for (int pct = 100; pct >= 20; pct -= 10)
            {
                double value = pct / 100.0;
                MenuItem item = new MenuItem(pct + "% opaque");
                item.RadioCheck = true;
                item.Click += delegate
                {
                    Opacity = value;
                    SaveSettings();
                };
                opacityMenu.MenuItems.Add(item);
            }
            opacityMenu.Popup += delegate
            {
                foreach (MenuItem mi in opacityMenu.MenuItems)
                    mi.Checked = mi.Text.StartsWith(((int)Math.Round(Opacity * 100)) + "%");
            };
            menu.MenuItems.Add(opacityMenu);
            menu.MenuItems.Add("Reset position", delegate { Location = new Point(100, 100); SaveSettings(); });
            menu.MenuItems.Add("-");
            menu.MenuItems.Add("Exit", delegate { Close(); });

            tray = new NotifyIcon();
            tray.Icon = MakeIcon();
            tray.Text = "ClockNote";
            tray.ContextMenu = menu;
            tray.DoubleClick += delegate { hideTimer.Stop(); Show(); Activate(); note.Focus(); };
            tray.Visible = true;
        }

        static Icon MakeIcon()
        {
            using (Bitmap bmp = new Bitmap(16, 16))
            {
                using (Graphics g = Graphics.FromImage(bmp))
                {
                    g.SmoothingMode = SmoothingMode.AntiAlias;
                    g.FillEllipse(Brushes.Black, 0, 0, 15, 15);
                    g.DrawEllipse(Pens.White, 0, 0, 15, 15);
                    g.DrawLine(Pens.White, 8, 8, 8, 3);
                    g.DrawLine(Pens.White, 8, 8, 12, 8);
                }
                return Icon.FromHandle(bmp.GetHicon());
            }
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            SaveSettings();
            tray.Visible = false;
            base.OnFormClosing(e);
        }

        static double Clamp(double o)
        {
            if (double.IsNaN(o) || o < 0.2) return 0.2;
            if (o > 1.0) return 1.0;
            return o;
        }

        // Fall back to a default spot if the saved position is off every screen (e.g. a monitor was unplugged).
        static Point OnScreenLocation(Point p)
        {
            Rectangle r = new Rectangle(p, new Size(400, 100));
            foreach (Screen s in Screen.AllScreens)
                if (s.WorkingArea.IntersectsWith(r)) return p;
            Rectangle wa = Screen.PrimaryScreen.WorkingArea;
            return new Point(wa.Left + 100, wa.Top + 100);
        }

        static Settings LoadSettings()
        {
            try
            {
                if (File.Exists(SettingsPath))
                    return new JavaScriptSerializer().Deserialize<Settings>(File.ReadAllText(SettingsPath)) ?? new Settings();
            }
            catch { }
            return new Settings();
        }

        void SaveSettings()
        {
            try
            {
                settings.X = Left;
                settings.Y = Top;
                settings.Opacity = Math.Round(Opacity, 2);
                settings.Note = note.Text;
                string tmp = SettingsPath + ".tmp";
                File.WriteAllText(tmp, new JavaScriptSerializer().Serialize(settings));
                if (File.Exists(SettingsPath)) File.Replace(tmp, SettingsPath, null);
                else File.Move(tmp, SettingsPath);
            }
            catch { }
        }

        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new ClockForm());
        }
    }
}
