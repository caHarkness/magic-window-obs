using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Drawing;
using System.Reflection;
using System.IO;

namespace caHarkness
{
    static class Program
    {
        [STAThread]
        static void Main(string[] args)
       {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new MagicWindow());
        }
    }

    class MagicWindow : Form
    {
        [DllImport("user32.dll", SetLastError = true)]
        public static extern int GetWindowLong(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll")]
        public static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

        [DllImport("user32.dll", CharSet=CharSet.Auto)]
        public static extern int FlashWindow(IntPtr hWnd, int bInvert);

        const int GWL_EXSTYLE = -20;
        const int WS_EX_LAYERED = 0x80000;
        const int WS_EX_TRANSPARENT = 0x20;

        private Dictionary<string, string> prefs;

        private void SetPrefs(string[] bits)
        {
            if (prefs == null)
                prefs = new Dictionary<string, string>();

            if (bits == null)
                bits = Environment.GetCommandLineArgs();

            for (int i = 0; i < bits.Length; i++)
            {
                string arg = bits[i];

                try
                {
                    if (bits.Length > i + 1)
                    {
                        do
                        {
                            arg = arg.Substring(1);
                        }
                        while (arg.StartsWith("-"));

                        string _key = arg;
                        string _value = bits[i+1];

                        if (_value.StartsWith("-"))
                            _value = "true";

                        prefs[_key] = _value;
                    }
                }
                catch
                {

                }
            }
        }

        private string GetPref(string key, string def)
        {
            if (prefs == null)
                prefs = new Dictionary<string, string>();

            if (prefs.ContainsKey(key))
                return prefs[key];
            
            prefs[key] = def;
            return def;
        }

        private void MakeMagical()
        {
            TopMost = true;
            Opacity = double.Parse(GetPref("opacity", "0"));

            SetWindowLong(
                Handle,
                GWL_EXSTYLE,
                GetWindowLong(Handle, GWL_EXSTYLE) |
                    WS_EX_LAYERED |
                    WS_EX_TRANSPARENT);
        }

        private PictureBox pictureBox;
        private Timer mainTimer;
        private Timer sideTimer;
        private Graphics graphics;
        private NotifyIcon notifyIcon;

        private int StartingWidth;
        private int StartingHeight;

        public PictureBox GetPictureBox()
        {
            if (pictureBox == null)
            {
                pictureBox = new PictureBox();

                pictureBox.Dock = DockStyle.Fill;
                pictureBox.TabIndex = 0;
                pictureBox.TabStop = false;
            }

            return pictureBox;
        }

        public Point GetPictureBoxPoint()
        {
            //return
            //GetPictureBox()
            //    .PointToScreen(GetPictureBox().Location);

            return
            GetPictureBox()
                .PointToCurrentScreen(GetPictureBox().Location);
        }

        public Bitmap ShrinkAndExpand(Bitmap input)
        {
            Bitmap b =
                new Bitmap(
                    input,
                    new Size(
                        input.Size.Width / 32,
                        input.Size.Height / 32));

            return
            new Bitmap(b, input.Size);
        }

        public Timer GetMainTimer()
        {
            if (mainTimer == null)
            {
                mainTimer = new Timer()
                {
                    Interval = 1000 / int.Parse(GetPref("fps", "24")),
                    Enabled = true
                };

                mainTimer.Tick += delegate(object sender, EventArgs e)
                {
                    Point mouse = MousePosition;
                    Screen screen = Screen.FromPoint(mouse);

                    Left = mouse.X - Width / 2;
                    Top = mouse.Y - Height / 2;
                    int ox = 0 + screen.Bounds.X;
                    int oy = 0 + screen.Bounds.Y;

                    int w = screen.Bounds.Width;
                    int h = screen.Bounds.Height;

                    int left_adjust = 0;
                    int top_adjust = 0;
                    int right_adjust = 0;
                    int bottom_adjust = 0;

                    if (bool.Parse(GetPref("confine", "true")))
                    {
                        if (Left < ox)
                        {
                            left_adjust = ox - Left;
                            Left = ox;
                        }

                        if (Top < oy)
                        {
                            top_adjust = oy - Top;
                            Top = oy;
                        }

                        if (Left + Width > (w + ox))
                        {
                            right_adjust = (w + ox) - (Left + Width);
                            Left = (w + ox) - Width;
                        }

                        if (Top + Height > (h + oy))
                        {
                            bottom_adjust = (h + oy) - (Top + Height);
                            Top = (h + oy) - Height;
                        }
                    }

                    Point p = GetPictureBoxPoint();
                    Size size = GetPictureBox().Size;

                    if (GetPictureBox().Image == null)
                        GetPictureBox().Image = new Bitmap(size.Width, size.Height);

                    graphics = Graphics.FromImage(GetPictureBox().Image);
                    graphics.Clear(Color.Black);

                    graphics.CopyFromScreen(
                        p.X + ox,
                        p.Y + oy,
                        0,
                        0,
                        size);

                    if (bool.Parse(GetPref("cursor", "true")))
                    {
                        Rectangle geo = new Rectangle(
                            (0 - Cursor.HotSpot.X) + (size.Width / 2),
                            (0 - Cursor.HotSpot.Y) + (size.Height / 2),
                            Cursor.Size.Width,
                            Cursor.Size.Height);

                        if (bool.Parse(GetPref("confine", "true")))
                        {
                            geo.X -= left_adjust;
                            geo.Y -= top_adjust;
                            geo.X -= right_adjust;
                            geo.Y -= bottom_adjust;
                        }

                        Cursor.Draw(graphics, geo);
                    }

                    GetPictureBox()
                        .Refresh();
                };
            }

            return mainTimer;
        }
        
        public Timer GetSideTimer()
        {
            if (sideTimer == null)
            {
                sideTimer = new Timer()
                {
                    Interval = 1000,
                    Enabled = true
                };

                sideTimer.Tick += delegate(object sender, EventArgs e)
                {
                    FlashWindow(Handle, 1);
                };
            }

            return sideTimer;
        }

        public NotifyIcon GetNotifyIcon()
        {
            if (notifyIcon == null)
            {
                notifyIcon = new NotifyIcon();
                notifyIcon.ShowBalloonTip(1000, "Title", "Message", ToolTipIcon.Warning);
                
                try
                {
                    notifyIcon.Icon = Properties.Resources.MagicWindowIcon;
                    notifyIcon.Visible = true;

                    notifyIcon.ContextMenuStrip = new ContextMenuStrip();

                    var m = notifyIcon.ContextMenuStrip.Items;
                    
                    if (File.Exists("profiles.txt"))
                    {
                        string buffer = "";
                        string[] lines = File.ReadAllLines("profiles.txt");


                        for (int i = 0; i < lines.Length; i++)
                        {
                            string line = lines[i];
                            string next = null;

                            try
                            {
                                next = lines[i+1];
                            }
                            catch {}

                            if (line.StartsWith("#") || line.Length < 1)
                                continue;

                            if (line.StartsWith("-"))
                            {
                                m.Add("-");
                                continue;
                            }

                            buffer += line;

                            if (next != null)
                            {
                                if (next.StartsWith(" "))
                                {
                                    //buffer += line;
                                    continue;
                                }
                            }

                            while (buffer.Contains("  "))
                                buffer = buffer.Replace("  ", " ");

                            buffer = buffer.Trim();

                            string[] args = buffer.Split(' ');

                            try
                            {
                                m.Add(
                                    args[0],
                                    null,
                                    delegate(object Sender, EventArgs a)
                                    {
                                        SetPrefs(args);

                                        GetPictureBox().Image = null;
                                        ConfigureWindow();

                                    });
                            }
                            catch {}

                            buffer = "";
                        }

                        m.Add("-");

                        m.Add(
                            "Reload profiles",
                            null,
                            delegate(object Sender, EventArgs a)
                            {
                                GetNotifyIcon().Dispose();
                                notifyIcon = null;

                                GetNotifyIcon();
                            });
                    }

                    m.Add(
                        "Quit",
                        null,
                        delegate(object Sender, EventArgs a)
                        {
                            Close();
                        });
                }
                catch
                {
                }  
            }

            return notifyIcon;
        }

        public void ConfigureWindow()
        {
            Name = GetPref("title", "Magic Window");

            ClientSize = new Size(
                int.Parse(GetPref("width", "854")),
                int.Parse(GetPref("height", "480")));

            GetMainTimer().Interval = 1000 / int.Parse(GetPref("fps", "24"));

            Opacity = double.Parse(GetPref("opacity", "0"));
        }

        public MagicWindow()
        {
            Controls.Add(GetPictureBox());
            GetMainTimer();
            GetSideTimer();
            GetNotifyIcon();

            SetPrefs(null);
            ConfigureWindow();

            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;

            Icon = Properties.Resources.MagicWindowIcon;
            MakeMagical();
        }
    }

    static class Extensions
    {
        public static Point PointToCurrentScreen(this Control self, Point location)
        {
            var screenBounds = Screen.FromControl(self).Bounds;
            var globalCoordinates = self.PointToScreen(location);
            return new Point(globalCoordinates.X - screenBounds.X, globalCoordinates.Y - screenBounds.Y);
        }
    }
}
