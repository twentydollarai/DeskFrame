using System;
using System.IO;
using System.Linq;
using System.Drawing;
using System.Drawing.Imaging;
using System.Windows.Forms;
using System.Runtime.InteropServices;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Security.Cryptography;
using System.Windows.Automation;

namespace DeskFrame {
    internal static class Integration {
        [DllImport("user32.dll")] static extern IntPtr GetForegroundWindow();
        [DllImport("user32.dll")] static extern bool SetForegroundWindow(IntPtr h);
        [DllImport("user32.dll",CharSet=CharSet.Unicode)] static extern IntPtr FindWindow(string cls,string title);
        [DllImport("user32.dll",CharSet=CharSet.Unicode)] static extern int GetWindowText(IntPtr h,System.Text.StringBuilder text,int max);
        [DllImport("user32.dll")] static extern IntPtr GetDlgItem(IntPtr h,int id);
        [DllImport("user32.dll",CharSet=CharSet.Unicode)] static extern int GetClassName(IntPtr h,System.Text.StringBuilder text,int max);
        [DllImport("user32.dll")] static extern IntPtr SendMessage(IntPtr h,int msg,IntPtr w,IntPtr l);
        [DllImport("user32.dll")] static extern bool SetCursorPos(int x,int y);
        [DllImport("user32.dll")] static extern void mouse_event(uint flags,uint x,uint y,uint data,UIntPtr extra);
        [DllImport("user32.dll")] static extern void keybd_event(byte key,byte scan,uint flags,UIntPtr extra);
        static byte SubmittedAlpha(Viewer v) {return (byte)typeof(Viewer).GetField("presentedAlpha",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).GetValue(v);}
        static string root,log,samples;
        static Controller app;
        static Point originalCursor;
        static System.Threading.Timer watchdog;
        [STAThread] static void Main() {
            Native.SetProcessDPIAware();Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);
            root=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"test-work",Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);
            log=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"test-results.txt");File.WriteAllText(log,"DeskFrame 1.0.0 integration tests\r\n");
            Application.ThreadException+=delegate(object sender,System.Threading.ThreadExceptionEventArgs e) {File.AppendAllText(log,"UI ERROR "+e.Exception.ToString()+"\r\n");Environment.Exit(1);};
            AppDomain.CurrentDomain.UnhandledException+=delegate(object sender,UnhandledExceptionEventArgs e) {File.AppendAllText(log,"UNHANDLED "+e.ExceptionObject.ToString()+"\r\n");};
            watchdog=new System.Threading.Timer(delegate {File.AppendAllText(log,"FAIL unattended test timed out\r\n");Environment.Exit(1);},null,90000,System.Threading.Timeout.Infinite);
            samples=Path.Combine(root,"samples");Directory.CreateDirectory(samples);CreateSample("a.png",400,600,Color.Teal,ImageFormat.Png);CreateSample("b.jpg",600,400,Color.Coral,ImageFormat.Jpeg);CreateSample("c.jpeg",500,500,Color.SlateBlue,ImageFormat.Jpeg);
            File.WriteAllText(Path.Combine(samples,"bad.png"),"deliberately invalid image");
            originalCursor=Cursor.Position;
            app=new Controller(new Store(Path.Combine(root,"prefs")),true,"Local\\DeskFrame.Test."+Guid.NewGuid().ToString("N"));
            var start=new Timer{Interval=800};start.Tick+=async delegate {start.Stop();start.Dispose();await Run();};start.Start();
            Application.Run(app);app.Dispose();watchdog.Dispose();
        }
        static void Check(bool ok,string text) {File.AppendAllText(log,(ok?"PASS ":"FAIL ")+text+"\r\n");if(!ok)throw new Exception(text);}
        static void CreateSample(string name,int w,int h,Color color,ImageFormat format) {
            using(var b=new Bitmap(w,h)) {using(var g=Graphics.FromImage(b)) {g.Clear(color);using(var brush=new SolidBrush(Color.FromArgb(245,227,161)))g.FillEllipse(brush,w/4,h/5,w/3,w/3);using(var brush=new SolidBrush(Color.FromArgb(42,70,78)))g.FillPolygon(brush,new[]{new Point(0,h),new Point(w/2,h/2),new Point(w,h)});}b.Save(Path.Combine(samples,name),format);}
        }
        static string Hash(string path) {using(var h=SHA256.Create())using(var f=File.OpenRead(path))return BitConverter.ToString(h.ComputeHash(f));}
        static void Schedule(Action action,int delay) {var t=new Timer{Interval=delay};t.Tick+=delegate {t.Stop();t.Dispose();action();};t.Start();}
        static void Hotkey() {
            keybd_event(17,0,0,UIntPtr.Zero);keybd_event(18,0,0,UIntPtr.Zero);keybd_event((byte)Keys.D,0,0,UIntPtr.Zero);keybd_event((byte)Keys.D,0,2,UIntPtr.Zero);keybd_event(18,0,2,UIntPtr.Zero);keybd_event(17,0,2,UIntPtr.Zero);
        }
        static Task PickerAction(string[] files) {
            return Task.Run(async delegate {
                try {
                    IntPtr picker=IntPtr.Zero;
                    for(int i=0;i<40 && picker==IntPtr.Zero;i++) {await Task.Delay(100);picker=FindWindow(null,"Choose pictures for DeskFrame");}
                    if(picker==IntPtr.Zero)throw new Exception("Native picture picker did not open");
                    await Task.Delay(900); // Let the shell finish creating filename/action controls.
                    var dialog=AutomationElement.FromHandle(picker);
                    if(dialog.Current.ProcessId!=Process.GetCurrentProcess().Id)throw new Exception("Picker belongs to another process");
                    if(files!=null) {
                        var filename=dialog.FindFirst(TreeScope.Descendants,new AndCondition(new PropertyCondition(AutomationElement.ControlTypeProperty,ControlType.Edit),new PropertyCondition(AutomationElement.AutomationIdProperty,"1148")));
                        object pattern;
                        if(filename==null || !filename.TryGetCurrentPattern(ValuePattern.Pattern,out pattern))throw new Exception("Native filename edit field is unavailable");
                        ((ValuePattern)pattern).SetValue(String.Join(" ",files.Select(f=>"\""+f+"\"")));
                        await Task.Delay(250);
                    }
                    // UI Automation omits Open's expected Button node on some shell versions.
                    // Resolve the actual dialog child, not reused shell-list automation IDs.
                    string action=files==null ? "Cancel" : "Open";
                    IntPtr button=GetDlgItem(picker,files==null ? 2 : 1);
                    var cls=new System.Text.StringBuilder(128);var caption=new System.Text.StringBuilder(128);
                    GetClassName(button,cls,128);GetWindowText(button,caption,128);
                    if(button==IntPtr.Zero || cls.ToString()!="Button" || !caption.ToString().Replace("&","").StartsWith(action,StringComparison.Ordinal))
                        throw new Exception("Native picker action is not the expected dialog button: "+action+" / "+cls+" / "+caption);
                    File.AppendAllText(log,"PICKER ACTION "+action+" verified native Button\r\n");
                    SendMessage(button,0xF5,IntPtr.Zero,IntPtr.Zero); // BM_CLICK on the verified picker action only
                } catch(Exception ex) {File.AppendAllText(log,"PICKER FAILURE FULL TRACE\r\n"+ex.ToString()+"\r\n");Environment.Exit(1);}
            });
        }
        static async Task Run() {
            Form probe=null;
            try {
                var hashes=Directory.GetFiles(samples).ToDictionary(p=>p,Hash);
                Check(app.Dialog!=null && app.Dialog.Visible && !app.Frame.HasImage,"Clean first launch opens native image selection/settings");
                app.Dialog.Close();
                var directSetup=new Preferences{Folder=samples,FrameSizing="Fixed frame",IntervalSeconds=2,Corner="top-left"};
                Check(app.Apply(directSetup)==null,"Window-only setup uses generated source directly; picker acceptance excluded");
                await Task.Delay(300);
                Check(ImageSource.Find(app.Settings).Length==4 && app.Frame.HasImage,"Folder discovery and corrupt-file tolerance");
                Check(app.Frame.Width==320 && app.Frame.Height==480,"Portrait ratio retained at configured width");
                Check(new Preferences().IntervalSeconds==15,"New slideshow default is 15 seconds");
                var invalidInterval=app.Settings.Copy();invalidInterval.IntervalSeconds=1;
                try {invalidInterval.Validate();Check(false,"Interval minimum validation");} catch(ArgumentException) {Check(true,"Interval minimum validation");}
                invalidInterval.IntervalSeconds=3601;
                try {invalidInterval.Validate();Check(false,"Interval maximum validation");} catch(ArgumentException) {Check(true,"Interval maximum validation");}
                int style=Native.GetWindowLong(app.Frame.Handle,-20);
                Check((style&0x080800A8)==0x080800A8,"Native topmost, toolwindow, layered, no-activate and click-through styles");
                Check(!app.Frame.ShowInTaskbar && app.Frame.FormBorderStyle==FormBorderStyle.None,"No taskbar entry or window border");
                app.Frame.SetPaused(true);
                probe=new Form{Text="DeskFrame nonpersonal test surface",StartPosition=FormStartPosition.Manual,Bounds=app.Frame.Bounds,TopMost=true,BackColor=Color.White};
                bool clicked=false;probe.MouseDown+=delegate {clicked=true;};probe.Show();Native.ShowWindow(probe.Handle,5);SetForegroundWindow(probe.Handle);Native.SetWindowPos(app.Frame.Handle,new IntPtr(-1),0,0,0,0,0x13);
                await Task.Delay(600);
                SetCursorPos(app.Frame.Left+160,app.Frame.Top+180);await Task.Delay(150);mouse_event(2,0,0,0,UIntPtr.Zero);mouse_event(4,0,0,0,UIntPtr.Zero);await Task.Delay(300);
                Check(clicked,"Real mouse click reaches underlying test window");
                Check(GetForegroundWindow()==probe.Handle,"Click activates underlying window, never frame");
                Hotkey();await Task.Delay(250);Check(!app.Frame.Visible,"Real global shortcut hides frame");
                Hotkey();await Task.Delay(250);Check(app.Frame.Visible,"Real global shortcut restores frame");
                Check(GetForegroundWindow()==probe.Handle,"Hotkey restore preserves keyboard focus");
                app.ToggleItem.PerformClick();Check(!app.Frame.Visible,"Tray Hide command");app.ToggleItem.PerformClick();Check(app.Frame.Visible,"Tray Show command");
                Check(GetForegroundWindow()==probe.Handle,"Tray command restore does not activate frame");
                app.Menu.Show(probe,new Point(10,10));await Task.Delay(200);Check(app.Menu.Visible && app.Tray.Visible,"Tray icon active and native context menu opens");app.Menu.Close();
                probe.Close();probe=null;
                app.OpenControls();Check(app.Controls!=null && app.Controls.Visible,"Controls window opens for running instance");
                app.Controls.ToggleButton.PerformClick();Check(app.Frame.IsHidden,"Controls hide frame");
                app.Controls.ToggleButton.PerformClick();Check(!app.Frame.IsHidden && app.Frame.Visible,"Controls show frame");
                app.Controls.PauseButton.PerformClick();Check(!app.Frame.Paused,"Controls resume slideshow");
                app.Controls.PauseButton.PerformClick();Check(app.Frame.Paused,"Controls pause slideshow");
                app.Controls.SourceButton.PerformClick();Check(app.Dialog!=null,"Controls change pictures opens source selection");app.Dialog.Close();
                app.Controls.SettingsButton.PerformClick();Check(app.Dialog!=null,"Controls Settings opens preferences");app.Dialog.Close();
                app.Controls.Close();
                app.OpenSettings(false);app.Dialog.TrayBox.Checked=false;app.Dialog.SaveButton.PerformClick();
                Check(!app.Tray.Visible && !app.Store.Load().ShowTray,"Tray icon can be disabled and persists");
                app.OpenControls();Check(app.Controls.Visible,"Controls stay available with tray icon disabled");app.Controls.Close();
                app.OpenSettings(false);app.Dialog.TrayBox.Checked=true;app.Dialog.SaveButton.PerformClick();
                Check(app.Tray.Visible && app.Store.Load().ShowTray,"Tray icon can be restored through Settings");
                app.PauseItem.PerformClick();Check(!app.Frame.Paused,"Tray Resume command");
                bool sawFade=false,stableBounds=true,capturedFade=false;int old=app.Frame.Rotations;Size stable=app.Frame.Size;
                for(int i=0;i<44;i++) {
                    await Task.Delay(250);sawFade|=app.Frame.Fading;stableBounds&=app.Frame.Size==stable;
                    if(!capturedFade && app.Frame.Fading) {
                        await Task.Delay(650);
                        using(var fadeShot=new Bitmap(app.Frame.Width,app.Frame.Height)) {
                            app.Frame.DrawToBitmap(fadeShot,new Rectangle(Point.Empty,fadeShot.Size));
                            fadeShot.Save(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"mixed-fade.png"));
                        }
                        capturedFade=true;
                    }
                }
                Check(stableBounds,"Mixed-size transitions keep window bounds stable");
                Check(capturedFade,"Mixed-size fade captured for visual inspection");
                Check(sawFade && app.Frame.Rotations>=old+3,"Real timer drives crossfade and complete slideshow loop");
                app.PauseItem.PerformClick();old=app.Frame.Rotations;await Task.Delay(2200);Check(app.Frame.Paused && app.Frame.Rotations==old,"Tray Pause freezes slideshow");
                app.SettingsItem.PerformClick();await Task.Delay(100);
                app.Dialog.FrameWidth.Value=280;app.Dialog.OpacityValue.Value=75;app.Dialog.Interval.Value=3;app.Dialog.EdgeMargin.Value=30;app.Dialog.Topmost.Checked=false;app.Dialog.ClickThrough.Checked=false;app.Dialog.Fade.Checked=false;app.Dialog.Hotkey.Text="Ctrl+Alt+F8";app.Dialog.SaveButton.PerformClick();
                Check(app.Dialog==null,"Settings save through native controls");
                style=Native.GetWindowLong(app.Frame.Handle,-20);
                Check((style&0x28)==0 && app.Frame.Width==280 && SubmittedAlpha(app.Frame)==191,"Topmost/click-through off, width and opacity applied live");
                var saved=app.Store.Load();Check(saved.Width==280 && saved.Margin==30 && saved.IntervalSeconds==3 && !saved.Crossfade && saved.Hotkey=="Ctrl+Alt+F8","Settings and interval persist without manual JSON");
                app.Frame.SetPaused(false);old=app.Frame.Rotations;await Task.Delay(3500);Check(app.Frame.Rotations>old && !app.Frame.Fading,"Crossfade disabled performs direct rotation");
                // Confirm every retained source is byte-for-byte unchanged before testing removals.
                Check(hashes.All(pair=>Hash(pair.Key)==pair.Value),"All source image hashes unchanged");
                File.Delete(Path.Combine(samples,"a.png"));await Task.Delay(2300);Check(app.Frame.HasImage,"Removed image is skipped without crash or file lock");
                string moved=samples+"-offline";Directory.Move(samples,moved);await Task.Delay(2400);Check(!app.Frame.HasImage && !app.Frame.Visible && app.Tray.Visible,"Unavailable folder hides frame and keeps recovery controls");
                Directory.Move(moved,samples);await Task.Delay(10500);Check(app.Frame.HasImage && app.Frame.Visible,"Unavailable folder automatically recovers");
                var invalid=saved.Copy();invalid.Folder=Path.Combine(root,"absent");Check(app.Apply(invalid)!=null && app.Frame.HasImage,"Bad source rejected without discarding working frame");
                app.SettingsItem.PerformClick();app.Dialog.Hotkey.Text="broken shortcut";app.Dialog.SaveButton.PerformClick();Check(app.Dialog!=null && app.Dialog.Error.Text.Length>0,"Invalid hotkey yields inline validation");app.Dialog.Close();
                using(var shot=new Bitmap(app.Frame.Width,app.Frame.Height)) {app.Frame.DrawToBitmap(shot,new Rectangle(Point.Empty,shot.Size));shot.Save(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"sample-frame.png"));}
                app.OpenSettings(false);app.Dialog.Sizing.SelectedItem="Fixed frame";app.Dialog.Fitting.SelectedItem="Fill";app.Dialog.SaveButton.PerformClick();
                Check(app.Store.Load().ImageFitting=="Fill" && app.Store.Load().FrameSizing=="Fixed frame","Fixed Fill persists through Settings UI");
                app.OpenSettings(false);app.Dialog.Sizing.SelectedItem="Auto-fit";app.Dialog.Fade.Checked=true;app.Dialog.SaveButton.PerformClick();
                Check(app.Store.Load().FrameSizing=="Auto-fit" && app.Store.Load().ImageFitting=="Fill","Auto-fit persists and retains inactive Fill preference");
                probe=new Form{Text="DeskFrame Auto-fit focus probe",StartPosition=FormStartPosition.Manual,Bounds=new Rectangle(600,100,200,100)};probe.Show();SetForegroundWindow(probe.Handle);
                Point anchor=app.Frame.AnchorPosition;bool cleanResize=true,anchored=true;int resizeCount=0;
                EventHandler resized=delegate {resizeCount++;cleanResize &= SubmittedAlpha(app.Frame)==(byte)(app.Settings.Opacity*255);};app.Frame.SizeChanged+=resized;
                old=app.Frame.Rotations;bool sawAutoFade=false;
                for(int i=0;i<75;i++) {await Task.Delay(100);sawAutoFade|=app.Frame.Fading;cleanResize &= SubmittedAlpha(app.Frame)==(byte)(app.Settings.Opacity*255);anchored &= ImageLayout.Corner(app.Frame.Bounds,app.Settings.Corner)==anchor && Screen.PrimaryScreen.WorkingArea.Contains(app.Frame.Bounds);}
                app.Frame.SizeChanged-=resized;
                Check(sawAutoFade && app.Frame.Rotations>=old+2 && resizeCount>=2 && cleanResize,"Real Auto-fit timer retains configured window opacity through alternating aspect ratios");
                Check(anchored,"Live Auto-fit stays on screen at its saved corner anchor");
                Check(GetForegroundWindow()==probe.Handle,"Auto-fit opacity and resize transitions never steal focus");
                app.Frame.SetPaused(true);byte pausedOpacity=SubmittedAlpha(app.Frame);Rectangle pausedBounds=app.Frame.Bounds;await Task.Delay(300);
                Check(SubmittedAlpha(app.Frame)==pausedOpacity && app.Frame.Bounds==pausedBounds,"Auto-fit pause freezes opacity and geometry");
                app.Frame.Toggle();app.Frame.Toggle();Check(GetForegroundWindow()==probe.Handle,"Auto-fit hide/restore preserves focus");
                probe.Close();probe=null;
                long memory=Process.GetCurrentProcess().WorkingSet64/1024/1024;
                Check(memory<160,"Working set below 160 MB with test dialogs: "+memory+" MB");
                File.AppendAllText(log,"ALL WINDOW-ONLY INTEGRATION CHECKS PASSED (PICKERS EXCLUDED)\r\n");
                app.OpenControls();app.Controls.ExitButton.PerformClick();Check(!app.Tray.Visible,"Controls Exit removes icon and exits message loop");
            } catch(Exception ex) {File.AppendAllText(log,"ERROR "+ex.ToString()+"\r\n");Environment.ExitCode=1;app.Shutdown();}
            finally {if(probe!=null)probe.Close();SetCursorPos(originalCursor.X,originalCursor.Y);}
        }
    }
}



