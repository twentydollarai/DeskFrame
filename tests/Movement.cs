using System;
using System.IO;
using System.Linq;
using System.Drawing;
using System.Drawing.Imaging;
using System.Reflection;
using System.Windows.Forms;
using System.Runtime.InteropServices;
using System.Diagnostics;
using System.Collections.Generic;
using System.Security.Cryptography;
namespace DeskFrame {
 internal static class MovementTests {
  [DllImport("user32.dll")] static extern bool GetWindowRect(IntPtr h,out Rect r);
  [DllImport("user32.dll")] static extern IntPtr SendMessage(IntPtr h,int m,IntPtr w,IntPtr l);
  struct Rect {public int L,T,R,B;}
  static readonly BindingFlags F=BindingFlags.NonPublic|BindingFlags.Instance;
  static string output,fixtures;static int checks;
  static void Check(bool ok,string name){File.AppendAllText(Path.Combine(output,"results.txt"),(ok?"PASS ":"FAIL ")+name+"\r\n");if(!ok)throw new Exception(name);checks++;}
  static void Pump(int ms){var s=Stopwatch.StartNew();while(s.ElapsedMilliseconds<ms){Application.DoEvents();System.Threading.Thread.Sleep(5);}}
  static Rectangle Bounds(Viewer v){Rect r;GetWindowRect(v.Handle,out r);return Rectangle.FromLTRB(r.L,r.T,r.R,r.B);}
  static void Set(Viewer v,string name,object value){typeof(Viewer).GetField(name,F).SetValue(v,value);}
  static object Get(Viewer v,string name){return typeof(Viewer).GetField(name,F).GetValue(v);}
  static object Call(Viewer v,string name,params object[] args){return typeof(Viewer).GetMethod(name,F).Invoke(v,args);}
  static string Pixels(Viewer v){using(var b=new Bitmap(v.Width,v.Height)){using(var g=Graphics.FromImage(b))Call(v,"OnPaint",new PaintEventArgs(g,new Rectangle(Point.Empty,b.Size)));using(var s=new MemoryStream()){b.Save(s,ImageFormat.Png);using(var h=SHA256.Create())return Convert.ToBase64String(h.ComputeHash(s.ToArray()));}}}
  static void Click(Button b){Check(b.Enabled,"Movement button enabled: "+b.AccessibleName);SendMessage(b.Handle,0xF5,IntPtr.Zero,IntPtr.Zero);}
  static Preferences PreferencesForTest(){
   var area=Screen.PrimaryScreen.WorkingArea;
   return new Preferences{Files=new[]{Path.Combine(fixtures,"01-landscape.png"),Path.Combine(fixtures,"02-portrait.png")},Width=420,Opacity=1,IntervalSeconds=3,Corner="bottom-right",ShowTray=false,
    Hotkey="Ctrl+Alt+F11",FrameSizing="Fixed frame",ImageFitting="Fill",RememberPosition=true,
    AnchorX=area.Left+area.Width/2+210,AnchorY=area.Top+area.Height/2+315};
  }
  [STAThread] static void Main(string[] args){
   Native.SetProcessDPIAware();Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);
   output=args[0];fixtures=args[1];Directory.CreateDirectory(output);File.WriteAllText(Path.Combine(output,"results.txt"),"Movement candidate targeted regression\r\n");
   var owned=new List<int>();using(var blocker=new Form()){
    var handle=blocker.Handle;
    try{
     // Ensure conflicts even on a machine where these bindings were initially free.
     for(int i=0;i<4;i++)if(Native.RegisterHotKey(handle,710+i,0x4007,(uint)Keys.Left+(uint)i))owned.Add(710+i);
     if(Native.RegisterHotKey(handle,720,0x4003,(uint)Keys.F11))owned.Add(720);
     Run();
     File.AppendAllText(Path.Combine(output,"results.txt"),"ALL MOVEMENT TESTS PASSED: "+checks+" checks\r\n");
    }catch(Exception e){File.AppendAllText(Path.Combine(output,"results.txt"),e+"\r\n");Environment.ExitCode=1;}
    finally{foreach(int id in owned)Native.UnregisterHotKey(handle,id);}
   }
  }
  static void Run(){
   var store=new Store(Path.Combine(output,"portable-profile"));var p=PreferencesForTest();store.Save(p);
   Point expectedAnchor;Rectangle expectedBounds;
   using(var app=new Controller(store,true,"Local\\DeskFrame.MovementTest."+Guid.NewGuid())){
    Pump(450);app.Frame.SetPaused(true);
    Check(!app.Tray.Visible,"Tray disabled throughout warning test");
    Check(app.Controls!=null && app.Controls.Visible,"Shortcut failures automatically open Controls with tray disabled");
    Check(app.Controls.ShortcutStatus.Visible,"Persistent warning visible");
    foreach(string key in new[]{"Left","Up","Right","Down","Ctrl+Alt+F11"})Check(app.Controls.ShortcutStatus.Text.Contains(key),"Warning names failed binding "+key);
    Check(app.Controls.ShortcutStatus.Text.Contains("Move frame buttons"),"Warning explains usable alternative");
    Check(app.Controls.MoveUpButton.AccessibleName=="Move frame up","Movement action has accessible name");
    var before=Bounds(app.Frame);
    Click(app.Controls.MoveLeftButton);Check(Bounds(app.Frame).Location==new Point(before.X-20,before.Y),"Native Left click moves 20px");
    Click(app.Controls.MoveUpButton);Check(Bounds(app.Frame).Location==new Point(before.X-20,before.Y-20),"Native Up click moves 20px");
    Click(app.Controls.MoveRightButton);Check(Bounds(app.Frame).Location==new Point(before.X,before.Y-20),"Native Right click moves 20px");
    Click(app.Controls.MoveDownButton);Check(Bounds(app.Frame)==before,"Native Down click restores rectangle");
    Click(app.Controls.MoveLeftButton);Click(app.Controls.MoveUpButton);
    expectedAnchor=app.Frame.AnchorPosition;expectedBounds=Bounds(app.Frame);
    var saved=store.Load();Check(saved.AnchorX==expectedAnchor.X && saved.AnchorY==expectedAnchor.Y,"Movement saves corner anchor");
    Check(saved.X==expectedBounds.X && saved.Y==expectedBounds.Y,"Movement saves displayed coordinates");
    app.Controls.ToggleButton.PerformClick();
    Check(app.Frame.IsHidden && !app.Controls.MoveLeftButton.Enabled && !app.Controls.MoveUpButton.Enabled && !app.Controls.MoveRightButton.Enabled && !app.Controls.MoveDownButton.Enabled,"All movement buttons disabled while hidden");
    app.Controls.ToggleButton.PerformClick();Check(app.Controls.MoveLeftButton.Enabled,"Movement reenabled when shown");
    var flow=(FlowLayoutPanel)app.Controls.Controls[0];flow.ScrollControlIntoView(app.Controls.ExitButton);
    Check(app.Controls.ExitButton.RectangleToScreen(app.Controls.ExitButton.ClientRectangle).IntersectsWith(flow.RectangleToScreen(flow.ClientRectangle)),"Exit remains reachable by scrolling");
    flow.ScrollControlIntoView(app.Controls.MoveUpButton);
    using(var shot=new Bitmap(app.Controls.Width,app.Controls.Height)){app.Controls.DrawToBitmap(shot,new Rectangle(Point.Empty,shot.Size));shot.Save(Path.Combine(output,"controls-only.png"));}
    app.Shutdown();
   }
   using(var app=new Controller(store,true,"Local\\DeskFrame.MovementTest."+Guid.NewGuid())){
    Pump(400);app.Frame.SetPaused(true);
    Check(Bounds(app.Frame)==expectedBounds && app.Frame.AnchorPosition==expectedAnchor,"Restart restores arbitrary moved position from isolated Store");
    foreach(var screen in Screen.AllScreens)foreach(string mode in new[]{"Auto-fit","Fit","Fill"})foreach(string corner in new[]{"top-left","top-right","bottom-left","bottom-right"}){
     p=PreferencesForTest();p.Hotkey="";p.Monitor=screen.DeviceName;p.Corner=corner;
     p.FrameSizing=mode=="Auto-fit"?"Auto-fit":"Fixed frame";p.ImageFitting=mode=="Auto-fit"?"Fit":mode;
     p.AnchorX=null;p.AnchorY=null;p.X=null;p.Y=null;
     Check(app.Apply(p)==null,"Apply "+screen.DeviceName+" "+mode+" "+corner);
     var area=screen.WorkingArea;
     app.Frame.MoveFrame(-10000,0);Check(Bounds(app.Frame).Left==area.Left && area.Contains(Bounds(app.Frame)),"Clamp left "+mode+" "+corner);
     var edge=Bounds(app.Frame);Click(app.Controls.MoveLeftButton);Check(Bounds(app.Frame)==edge,"Outward left click stays in bounds");
     app.Frame.MoveFrame(10000,0);Check(Bounds(app.Frame).Right==area.Right && area.Contains(Bounds(app.Frame)),"Clamp right");
     app.Frame.MoveFrame(0,-10000);Check(Bounds(app.Frame).Top==area.Top && area.Contains(Bounds(app.Frame)),"Clamp top");
     app.Frame.MoveFrame(0,10000);Check(Bounds(app.Frame).Bottom==area.Bottom && area.Contains(Bounds(app.Frame)),"Clamp bottom");
     var rect=Bounds(app.Frame);var anchor=app.Frame.AnchorPosition;
     Check(anchor.X==(corner.EndsWith("right")?rect.Right:rect.Left) && anchor.Y==(corner.StartsWith("bottom")?rect.Bottom:rect.Top),"Selected corner remains anchor");
    }
    p=PreferencesForTest();p.Hotkey="";p.RememberPosition=false;p.AnchorX=null;p.AnchorY=null;
    Check(app.Apply(p)==null,"Apply remember off");var unchanged=File.ReadAllText(store.FilePath);
    Click(app.Controls.MoveLeftButton);Check(File.ReadAllText(store.FilePath)==unchanged,"Remember off does not persist movement");
    p=PreferencesForTest();p.Hotkey="";p.FrameSizing="Auto-fit";
    Check(app.Apply(p)==null,"Apply Auto-fit transition case");app.Frame.SetPaused(false);
    for(int direction=0;direction<2;direction++){
     Set(app.Frame,"nextChange",-1.0);Call(app.Frame,"Tick",null,EventArgs.Empty);
     Check(app.Frame.Fading,"Transition active direction "+direction);
     double start=(double)Get(app.Frame,"fadeStart");Call(app.Frame,"AdvanceTransition",start+.22);
     var current=Get(app.Frame,"current");var next=Get(app.Frame,"next");var progress=Get(app.Frame,"transitionProgress");
     var rect=Bounds(app.Frame);var pixels=Pixels(app.Frame);
     Click(app.Controls.MoveLeftButton);
     Check(Bounds(app.Frame)==new Rectangle(rect.X-20,rect.Y,rect.Width,rect.Height),"Move during transition changes position only");
     Check(Object.ReferenceEquals(current,Get(app.Frame,"current")) && Object.ReferenceEquals(next,Get(app.Frame,"next")),"Both transition images retained");
     Check(progress.Equals(Get(app.Frame,"transitionProgress")) && pixels==Pixels(app.Frame),"Blend progress and rendered pixels unchanged by move");
     var anchor=app.Frame.AnchorPosition;Call(app.Frame,"AdvanceTransition",start+.5);
     Check(!app.Frame.Fading && app.Frame.AnchorPosition==anchor,"Final transition preserves moved anchor");
     Check(app.Frame.Width==420 && app.Frame.Height==(direction==0?630:280),"Exact incoming bounds after movement");
     Check((byte)Get(app.Frame,"presentedAlpha")==255,"100 percent presentation alpha retained");
    }
    app.Frame.SetPaused(true);
    var missing=p.Copy();missing.Folder=Path.Combine(output,"does-not-exist");app.Frame.Configure(missing);
    Check(!app.Frame.HasImage && !app.Controls.MoveLeftButton.Enabled && !app.Controls.MoveUpButton.Enabled && !app.Controls.MoveRightButton.Enabled && !app.Controls.MoveDownButton.Enabled,"Missing source disables all movement buttons");
    var old=app.Frame.AnchorPosition;app.Frame.MoveFrame(20,20);Check(app.Frame.AnchorPosition==old,"Missing source movement is a no-op");
    app.Frame.Configure(p);Check(app.Controls.MoveLeftButton.Enabled,"Recovered source reenables movement");
    app.Shutdown();
   }
  }
 }
}


