using System;
using System.IO;
using System.Linq;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using System.Runtime.InteropServices;
using System.Web.Script.Serialization;
using System.Threading;
using System.Security.Principal;
using Microsoft.Win32;
using System.Reflection;

[assembly: AssemblyTitle("DeskFrame")]
[assembly: AssemblyProduct("DeskFrame")]
[assembly: AssemblyDescription("A quiet desktop picture frame")]
[assembly: AssemblyVersion("1.0.0.0")]
[assembly: AssemblyFileVersion("1.0.0.0")]
[assembly: AssemblyCopyright("Copyright (c) 2026 DeskFrame contributors")]

namespace DeskFrame {
    internal sealed class Preferences {
        public string Folder = "";
        public string[] Files = new string[0];
        public int Width = 320, Margin = 24;
        public double Opacity = .9, IntervalSeconds = 15;
        public string Corner = "bottom-right", Hotkey = "Ctrl+Alt+D";
        public bool Topmost = true, ClickThrough = true, Crossfade = true, Startup = false, ShowTray = true;
        public bool RememberPosition = true;
        public int? X, Y, AnchorX, AnchorY;
        public string FrameSizing = "Auto-fit", ImageFitting = "Fit", Monitor = "";
        public Preferences Copy() { return Store.Json.Deserialize<Preferences>(Store.Json.Serialize(this)); }
        public void Validate() {
            if(Width<80 || Width>1600 || Margin<0 || Margin>500 || Double.IsNaN(Opacity) || Opacity<.1 || Opacity>1 || Double.IsNaN(IntervalSeconds) || IntervalSeconds<2 || IntervalSeconds>3600)
                throw new ArgumentException("Please check size, opacity, interval and margin.");
            if(!new[]{"bottom-right","bottom-left","top-right","top-left"}.Contains(Corner)) throw new ArgumentException("Choose a screen corner.");
            if(FrameSizing!="Auto-fit" && FrameSizing!="Fixed frame")FrameSizing="Auto-fit";
            if(ImageFitting!="Fit" && ImageFitting!="Fill")ImageFitting="Fit";
            if(Monitor==null)Monitor="";
            uint mods,key; Native.ParseHotkey(Hotkey,out mods,out key);
            if(Files==null) Files=new string[0];
            if(Folder==null) Folder="";
        }
    }
    internal sealed class Store {
        internal static readonly JavaScriptSerializer Json=new JavaScriptSerializer();
        internal readonly string DirectoryPath;
        internal string FilePath {get{return Path.Combine(DirectoryPath,"settings.json");}}
        internal string LoadNotice;
        internal Store(string path) { DirectoryPath=path; Directory.CreateDirectory(path); }
        internal Preferences Load() {
            if(!File.Exists(FilePath)) return new Preferences();
            try {var p=Json.Deserialize<Preferences>(File.ReadAllText(FilePath)); if(p==null) throw new Exception(); p.Validate(); return p;}
            catch {LoadNotice="Saved preferences could not be read. Please choose your images and settings again."; return new Preferences();}
        }
        internal void Save(Preferences p) {
            p.Validate();
            string temporary=FilePath+".tmp";
            File.WriteAllText(temporary,Json.Serialize(p));
            if(File.Exists(FilePath)) File.Replace(temporary,FilePath,null); else File.Move(temporary,FilePath);
        }
    }
    internal static class ImageSource {
        internal static readonly string[] Extensions={".png",".jpg",".jpeg",".bmp",".gif"};
        internal static bool Supported(string p) {return Extensions.Contains(Path.GetExtension(p).ToLowerInvariant());}
        internal static string[] Find(Preferences p) {
            try {
                return (String.IsNullOrWhiteSpace(p.Folder) ? p.Files : Directory.GetFiles(p.Folder))
                    .Where(f=>Supported(f) && File.Exists(f)).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(f=>f,StringComparer.OrdinalIgnoreCase).ToArray();
            } catch(IOException) {return new string[0];} catch(UnauthorizedAccessException) {return new string[0];} catch(ArgumentException) {return new string[0];}
        }
    }
    internal static class Native {
        [DllImport("user32.dll")] internal static extern bool SetProcessDPIAware();
        [DllImport("user32.dll")] internal static extern bool RegisterHotKey(IntPtr h,int id,uint mods,uint key);
        [DllImport("user32.dll")] internal static extern bool UnregisterHotKey(IntPtr h,int id);
        [DllImport("user32.dll")] internal static extern bool ShowWindow(IntPtr h,int command);
        [DllImport("user32.dll")] internal static extern bool SetForegroundWindow(IntPtr h);
        [DllImport("user32.dll")] internal static extern int GetWindowLong(IntPtr h,int index);
        [DllImport("user32.dll")] internal static extern int SetWindowLong(IntPtr h,int index,int value);
        [DllImport("user32.dll")] internal static extern bool SetWindowPos(IntPtr h,IntPtr after,int x,int y,int w,int height,uint flags);
        [DllImport("user32.dll",SetLastError=true)] internal static extern bool SetLayeredWindowAttributes(IntPtr h,uint key,byte alpha,uint flags);
        [DllImport("user32.dll")] internal static extern bool DestroyIcon(IntPtr h);
        internal static void ParseHotkey(string value,out uint mods,out uint key) {
            mods=0;key=0;
            if(String.IsNullOrWhiteSpace(value)) return;
            var parts=value.Split('+');
            if(parts.Length<2) throw new ArgumentException("Use a shortcut such as Ctrl+Alt+D, or leave it blank to disable.");
            for(int i=0;i<parts.Length-1;i++) {
                switch(parts[i].Trim().ToLowerInvariant()) {
                    case "ctrl":case "control":mods|=2;break; case "alt":mods|=1;break;
                    case "shift":mods|=4;break; default:throw new ArgumentException("Hotkey modifiers: Ctrl, Alt, Shift.");
                }
            }
            Keys parsed;
            if(!Enum.TryParse<Keys>(parts.Last().Trim(),true,out parsed) || !((parsed>=Keys.A && parsed<=Keys.Z) || (parsed>=Keys.F1 && parsed<=Keys.F12) || (parsed>=Keys.D0 && parsed<=Keys.D9)))
                throw new ArgumentException("Hotkey key must be a letter, number (D0-D9), or F1-F12.");
            if((mods&3)==0) throw new ArgumentException("Include Ctrl or Alt in the shortcut.");
            key=(uint)parsed;
        }
    }
    internal static class Startup {
        const string Key=@"Software\Microsoft\Windows\CurrentVersion\Run";
        internal static bool Enabled {
            get {using(var k=Registry.CurrentUser.OpenSubKey(Key)) return k!=null && k.GetValue("DeskFrame")!=null;}
        }
        internal static void Set(bool enabled,bool portable) {
            using(var k=Registry.CurrentUser.CreateSubKey(Key)) {
                if(enabled) k.SetValue("DeskFrame","\""+Application.ExecutablePath+"\""+(portable ? " --portable" : ""));
                else k.DeleteValue("DeskFrame",false);
            }
        }
    }
    internal static class Program {
        internal static string EventPrefix {get{return "Local\\DeskFrame."+WindowsIdentity.GetCurrent().User.Value;}}
        [STAThread] static void Main(string[] args) {
            Native.SetProcessDPIAware(); Application.EnableVisualStyles(); Application.SetCompatibleTextRenderingDefault(false);
            string prefix=EventPrefix;
            if(args.Contains("--exit")) {Signal(prefix+".Exit");return;}
            bool created;
            using(var mutex=new Mutex(true,prefix+".Instance",out created)) {
                if(!created) {Signal(prefix+".Activate");return;}
                try {
                    bool portable=args.Contains("--portable") || File.Exists(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"portable.flag"));
                    string path=portable ? Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"DeskFrameData") : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"DeskFrame");
                    using(var app=new Controller(new Store(path),portable,prefix)) Application.Run(app);
                } catch(Exception ex) { MessageBox.Show("DeskFrame could not start. Check that its preferences folder is writable.\n\n"+ex.Message,"DeskFrame",MessageBoxButtons.OK,MessageBoxIcon.Information); }
            }
        }
        static void Signal(string name) {try {using(var e=EventWaitHandle.OpenExisting(name)) e.Set();} catch(WaitHandleCannotBeOpenedException) {}}
    }
    internal sealed class Controller : ApplicationContext {
        internal readonly Store Store;
        internal Preferences Settings;
        internal readonly Viewer Frame=new Viewer();
        internal readonly NotifyIcon Tray;
        internal readonly ContextMenuStrip Menu=new ContextMenuStrip();
        internal readonly ToolStripMenuItem ControlsItem,ToggleItem,SourceItem,SettingsItem,PauseItem,ExitItem;
        internal SettingsWindow Dialog;
        internal ControlWindow Controls;
        readonly bool portable;
        readonly Icon icon;
        readonly System.Windows.Forms.Timer timer=new System.Windows.Forms.Timer();
        readonly EventWaitHandle exit,activate;
        bool firstTick=true,closing;
        internal Controller(Store store,bool portableMode,string prefix) {
            Store=store; portable=portableMode; Settings=store.Load();
            if(!portable) Settings.Startup=Startup.Enabled;
            exit=new EventWaitHandle(false,EventResetMode.AutoReset,prefix+".Exit");
            activate=new EventWaitHandle(false,EventResetMode.AutoReset,prefix+".Activate");
            icon=MakeIcon();
            Tray=new NotifyIcon{Icon=icon,Text="DeskFrame",ContextMenuStrip=Menu,Visible=Settings.ShowTray};
            ControlsItem=Item("Controls...",OpenControls);
            ToggleItem=Item("Show / Hide",delegate {Frame.Toggle();UpdateMenu();});
            SourceItem=Item("Choose Images / Change Folder...",delegate {OpenSettings(true);});
            SettingsItem=Item("Settings...",delegate {OpenSettings(false);});
            PauseItem=Item("Pause Slideshow",delegate {Frame.SetPaused(!Frame.Paused);UpdateMenu();});
            Menu.Items.Add(new ToolStripSeparator());
            ExitItem=Item("Exit",delegate {Shutdown();});
            Menu.Opening+=delegate {UpdateMenu();};
            Tray.DoubleClick+=delegate {OpenControls();};
            Frame.SourceUnavailable+=delegate {if(Tray.Visible)Tray.ShowBalloonTip(5000,"DeskFrame","No readable images are available. Open DeskFrame.exe again to change pictures. DeskFrame will retry automatically.",ToolTipIcon.Info);};
            Frame.PositionChanged+=delegate {SavePosition();};
            Frame.StateChanged+=UpdateMenu;
            string warning=Frame.Configure(Settings);
            if(warning!=null && Tray.Visible) Tray.ShowBalloonTip(5000,"DeskFrame",warning,ToolTipIcon.Info);
            timer.Interval=300;timer.Tick+=delegate {
                if(exit.WaitOne(0)) {Shutdown();return;}
                if(activate.WaitOne(0)) OpenControls();
                if(firstTick) {
                    firstTick=false;
                    if(Store.LoadNotice!=null) MessageBox.Show(Store.LoadNotice,"DeskFrame");
                    if(!Frame.HasImage) OpenSettings(true);
                    else if(!String.IsNullOrEmpty(Frame.ShortcutWarning)) OpenControls();
                }
            }; timer.Start();
        }
        ToolStripMenuItem Item(string name,Action action) {var item=new ToolStripMenuItem(name);item.Click+=delegate {action();};Menu.Items.Add(item);return item;}
        internal void UpdateMenu() {ToggleItem.Enabled=Frame.HasImage;ToggleItem.Text=!Frame.HasImage ? "Frame unavailable" : Frame.IsHidden ? "Show" : "Hide";PauseItem.Text=Frame.Paused ? "Resume Slideshow" : "Pause Slideshow";if(Controls!=null)Controls.RefreshState(Frame);}
        internal void OpenControls() {
            if(closing)return;
            if(Controls==null) {
                Controls=new ControlWindow(this);
                Controls.FormClosed+=delegate {Controls=null;};
                Controls.Show();
            }
            Controls.RefreshState(Frame);Controls.WindowState=FormWindowState.Normal;
            Native.ShowWindow(Controls.Handle,9);Controls.BringToFront();Controls.Activate();Native.SetForegroundWindow(Controls.Handle);
        }
        internal void OpenSettings(bool source) {
            if(closing)return;
            if(Dialog!=null) {Dialog.Activate();return;}
            Dialog=new SettingsWindow(Settings,source,portable,Apply);
            Dialog.FormClosed+=delegate {Dialog=null;if(!closing && (!Frame.HasImage || !Settings.ShowTray))OpenControls();};
            Dialog.Show(); Native.ShowWindow(Dialog.Handle,5); Dialog.Activate();
        }
        internal string Apply(Preferences p) {
            Preferences previous=Settings.Copy();
            try {
                p.Validate();
                if(!Viewer.AnyReadable(p)) return "No readable images found. Choose an available folder or PNG/JPEG/BMP/GIF files.";
                string hotkeyError=Frame.CheckHotkey(p.Hotkey);
                if(hotkeyError!=null) return hotkeyError;
                bool startupChanged=p.Startup!=Settings.Startup;
                if(startupChanged) Startup.Set(p.Startup,portable);
                try {Store.Save(p);} catch {if(startupChanged) Startup.Set(previous.Startup,portable);throw;}
                Settings=p;
                string warning=Frame.Configure(p);
                Tray.Visible=p.ShowTray;
                if(warning!=null && Tray.Visible) Tray.ShowBalloonTip(5000,"DeskFrame",warning,ToolTipIcon.Info);
                UpdateMenu();
                if(warning!=null)OpenControls();
                return null;
            } catch(Exception ex) {return "Could not apply settings: "+ex.Message;}
        }
        void SavePosition() {
            if(!Frame.HasImage || !Settings.RememberPosition)return;
            Settings.X=Frame.Left;Settings.Y=Frame.Top;Settings.AnchorX=Frame.AnchorPosition.X;Settings.AnchorY=Frame.AnchorPosition.Y;
            try {Store.Save(Settings);} catch {if(Tray.Visible)Tray.ShowBalloonTip(3000,"DeskFrame","Position could not be saved. Check preference-folder permissions.",ToolTipIcon.Info);}
        }
        internal void Shutdown() {
            if(closing)return;closing=true;timer.Stop();SavePosition();
            if(Dialog!=null)Dialog.Close();if(Controls!=null)Controls.Close();Tray.Visible=false;Frame.Close();ExitThread();
        }
        protected override void Dispose(bool disposing) {
            if(disposing) {timer.Dispose();Tray.Dispose();Menu.Dispose();icon.Dispose();Frame.Dispose();exit.Dispose();activate.Dispose();}
            base.Dispose(disposing);
        }
        static Icon MakeIcon() {
            using(var b=new Bitmap(32,32)) {
                using(var g=Graphics.FromImage(b)) {
                    g.SmoothingMode=SmoothingMode.AntiAlias;g.Clear(Color.Transparent);
                    using(var bg=new SolidBrush(Color.FromArgb(38,53,62))) g.FillRectangle(bg,2,2,28,28);
                    using(var sky=new SolidBrush(Color.FromArgb(146,199,205))) g.FillRectangle(sky,5,5,22,22);
                    using(var sun=new SolidBrush(Color.FromArgb(255,219,143))) g.FillEllipse(sun,18,8,6,6);
                    using(var land=new SolidBrush(Color.FromArgb(56,112,97))) g.FillPolygon(land,new[]{new Point(5,27),new Point(5,21),new Point(13,13),new Point(27,27)});
                }
                IntPtr handle=b.GetHicon();try {using(var borrowed=Icon.FromHandle(handle)) return (Icon)borrowed.Clone();} finally {Native.DestroyIcon(handle);}
            }
        }
    }
}

