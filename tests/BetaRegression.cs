using System;
using System.IO;
using System.Linq;
using System.Drawing;
using System.Drawing.Imaging;
using System.Windows.Forms;
using System.Windows.Automation;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
class BetaRegression {
 [DllImport("user32.dll")] static extern bool SetProcessDPIAware();
 [DllImport("user32.dll")] static extern bool SetWindowPos(IntPtr h,IntPtr after,int x,int y,int width,int height,uint flags);
 [DllImport("gdi32.dll")] static extern uint GetPixel(IntPtr dc,int x,int y);
 [DllImport("user32.dll")] static extern bool GetWindowRect(IntPtr h,out Rect r);
 [DllImport("user32.dll")] static extern bool GetLayeredWindowAttributes(IntPtr h,out uint key,out byte alpha,out uint flags);
 [DllImport("user32.dll")] static extern IntPtr GetDC(IntPtr h);
 [DllImport("user32.dll")] static extern int ReleaseDC(IntPtr h,IntPtr dc);
 [DllImport("gdi32.dll")] static extern bool BitBlt(IntPtr d,int x,int y,int w,int h,IntPtr s,int sx,int sy,uint op);
 [DllImport("user32.dll")] static extern bool PrintWindow(IntPtr h,IntPtr dc,uint flags);
 struct Rect { public int L,T,R,B; public Rectangle Bounds {get{return Rectangle.FromLTRB(L,T,R,B);}} }
 static string root,exe,source,log; static bool baseline; static Process app; static Form backdrop;
 static JavaScriptSerializer json=new JavaScriptSerializer();
 static void Check(bool ok,string label){File.AppendAllText(log,(ok?"PASS ":"FAIL ")+label+Environment.NewLine);if(!ok)throw new Exception(label);}
 static AutomationElement Window(string title){
  return AutomationElement.RootElement.FindFirst(TreeScope.Children,new AndCondition(new PropertyCondition(AutomationElement.ProcessIdProperty,app.Id),new PropertyCondition(AutomationElement.NameProperty,title)));
 }
 static AutomationElement Find(AutomationElement w,string name){return w.FindFirst(TreeScope.Descendants,new PropertyCondition(AutomationElement.NameProperty,name));}
 static void Invoke(AutomationElement w,string name){((InvokePattern)Find(w,name).GetCurrentPattern(InvokePattern.Pattern)).Invoke();}
 static async Task<AutomationElement> WaitWindow(string title){for(int i=0;i<60;i++){var w=Window(title);if(w!=null)return w;await Task.Delay(100);}throw new Exception("Window absent: "+title);}
 static void Start(){app=Process.Start(new ProcessStartInfo(exe,"--portable"){UseShellExecute=false});}
 static async Task Stop(){if(app==null)return;if(!app.HasExited){using(var exit=Process.Start(new ProcessStartInfo(exe,"--exit"){UseShellExecute=false})){await Task.Run(()=>exit.WaitForExit(5000));}Check(await Task.Run(()=>app.WaitForExit(5000)),"Complete exit");}app.Dispose();app=null;}
 static void Preferences(string mode,int opacity){
  var p=new {Folder=source,Files=new string[0],Width=320,Margin=24,Opacity=opacity/100.0,IntervalSeconds=2,Corner="bottom-right",Hotkey="",Topmost=true,ClickThrough=true,Crossfade=true,Startup=false,ShowTray=false,RememberPosition=false,FrameSizing=mode=="Auto"?"Auto-fit":"Fixed frame",ImageFitting=mode=="Fill"?"Fill":"Fit"};
  Directory.CreateDirectory(Path.Combine(root,"app","DeskFrameData"));File.WriteAllText(Path.Combine(root,"app","DeskFrameData","settings.json"),json.Serialize(p));
 }
 static Bitmap Capture(Rectangle r){
  Rect safe;if(backdrop==null || !GetWindowRect(backdrop.Handle,out safe) || !safe.Bounds.Contains(r))
   throw new Exception("Capture outside verified synthetic backdrop; no image retained");
  IntPtr probe=GetDC(IntPtr.Zero);
  try {
   foreach(var p in new[]{new Point(safe.L+4,safe.T+4),new Point(safe.R-5,safe.T+4),new Point(safe.L+4,safe.B-5),new Point(safe.R-5,safe.B-5)})
    if(GetPixel(probe,p.X,p.Y)!=0x0078b41e)throw new Exception("Synthetic backdrop obscured; no image retained");
  } finally {ReleaseDC(IntPtr.Zero,probe);}
  var b=new Bitmap(r.Width,r.Height);using(var g=Graphics.FromImage(b)){IntPtr screen=GetDC(IntPtr.Zero),dc=g.GetHdc();try{if(!BitBlt(dc,0,0,r.Width,r.Height,screen,r.X,r.Y,0x40CC0020))throw new Exception("Desktop capture failed");}finally{g.ReleaseHdc(dc);ReleaseDC(IntPtr.Zero,screen);}}return b;
 }
 static IntPtr Handle(AutomationElement w){return new IntPtr(w.Current.NativeWindowHandle);}
 static void Render(AutomationElement w,string name){Rect r;GetWindowRect(Handle(w),out r);using(var b=new Bitmap(r.R-r.L,r.B-r.T)){using(var g=Graphics.FromImage(b)){var dc=g.GetHdc();try{PrintWindow(Handle(w),dc,2);}finally{g.ReleaseHdc(dc);}}b.Save(Path.Combine(root,name+"-render.png"));}}
 static void CheckDisplay(AutomationElement w,string name,int opacity,bool save){
  Rect r;GetWindowRect(Handle(w),out r);uint key,flags;byte alpha;
  bool attributeMode=GetLayeredWindowAttributes(Handle(w),out key,out alpha,out flags);
  Check(!attributeMode,name+" uses composed layered surface (attribute getter is inapplicable)");
  bool invisible=false;
  using(var b=Capture(r.Bounds)){
   int foreground=0,black=0,total=0;
   for(int y=4;y<b.Height-4;y+=9)for(int x=4;x<b.Width-4;x+=9){var c=b.GetPixel(x,y);total++;if(c.R>70 || c.B>160)foreground++;if(c.R<8 && c.G<8 && c.B<8)black++;}
   Color edge=b.GetPixel(b.Width-10,b.Height/2);
   if(!name.StartsWith("transition-"))b.Save(Path.Combine(root,name+"-opacity-evidence.png"));
   // Fixture edge is Coral or SlateBlue at this location; accept either endpoint
   // during a live sequence and measure against its independently known background.
   double opacityValue=(byte)(opacity/100.0*255)/255.0;
   // Resolve both foreground weights from the known RGB vectors. This remains
   // valid during crossfades; their sum independently measures global opacity.
   double[] u={225,-53,-40},v={76,-90,85},d={edge.R-30,edge.G-180,edge.B-120};
   double uu=0,vv=0,uv=0,ud=0,vd=0;
   for(int j=0;j<3;j++){uu+=u[j]*u[j];vv+=v[j]*v[j];uv+=u[j]*v[j];ud+=u[j]*d[j];vd+=v[j]*d[j];}
   double determinant=uu*vv-uv*uv;
   double first=(ud*vv-vd*uv)/determinant,second=(vd*uu-ud*uv)/determinant,residual=0;
   for(int j=0;j<3;j++)residual+=Math.Abs(d[j]-first*u[j]-second*v[j]);
   if(!name.StartsWith("transition-"))Check(Math.Abs(first+second-opacityValue)<.015 && residual<6,name+" actual-desktop opacity="+(first+second)+" expected="+opacityValue+" residual="+residual);
   Check(invisible?foreground==0:foreground>total*.7,name+" actual desktop visibility, foreground="+foreground+"/"+total);
   Check(black==0,name+" no black pixels in sampled desktop");
   if(save)b.Save(Path.Combine(root,name+"-desktop.png"));
  }
 }
 static async Task<AutomationElement> Controls(){using(var second=Process.Start(new ProcessStartInfo(exe,"--portable"){UseShellExecute=false})){Check(await Task.Run(()=>second.WaitForExit(5000)),"Second launch exits");}return await WaitWindow("DeskFrame Controls");}
 static AutomationElement Number(AutomationElement w,string name){return w.FindFirst(TreeScope.Descendants,new AndCondition(new PropertyCondition(AutomationElement.NameProperty,name),new PropertyCondition(AutomationElement.IsRangeValuePatternAvailableProperty,true)));}
 static async Task AccessibilityAndSettings(){
  var controls=await Controls();Invoke(controls,"Settings...");var settings=await WaitWindow("DeskFrame Settings");
  string[] names={"Max width (px)","Opacity (%)","Interval (seconds)","Edge margin (px)"};
  double[] min={80,10,2,0},max={1600,100,3600,500},value={320,99,2,24},changed={321,100,3,25};
  for(int i=0;i<4;i++){
   var n=Number(settings,names[i]);if(n==null)File.AppendAllText(log,String.Join(Environment.NewLine,settings.FindAll(TreeScope.Descendants,Condition.TrueCondition).Cast<AutomationElement>().Select(e=>e.Current.Name+" | "+e.Current.ControlType.ProgrammaticName+" | "+e.Current.ClassName))+Environment.NewLine);Check(n!=null,"Numeric control found: "+names[i]);
   var range=(RangeValuePattern)n.GetCurrentPattern(RangeValuePattern.Pattern);var r=range.Current;
   File.AppendAllText(log,names[i]+" value/min/max="+r.Value+"/"+r.Minimum+"/"+r.Maximum+Environment.NewLine);
   Check(baseline ? r.Minimum==0 && r.Maximum==0 && r.Value==0 : r.Minimum==min[i] && r.Maximum==max[i] && r.Value==value[i],"Accessible range: "+names[i]);
   if(!baseline){range.SetValue(min[i]);Check(range.Current.Value==min[i],"Minimum accepted: "+names[i]);range.SetValue(max[i]);Check(range.Current.Value==max[i],"Maximum accepted: "+names[i]);bool rejected=false;try{range.SetValue(max[i]+1);}catch(ArgumentException){rejected=true;}Check(rejected && range.Current.Value==max[i],"Out-of-range edit rejected: "+names[i]);range.SetValue(changed[i]);Check(range.Current.Value==changed[i],"RangeValue writes: "+names[i]);range.SetValue(i==1?100:value[i]);}
  }
  var hint=Find(settings,"Open DeskFrame.exe again for controls or to exit, even with the tray icon off.");
  Check(baseline?hint==null:hint!=null && !hint.Current.IsOffscreen,"Control-recovery hint in initial viewport");
  Check(Find(settings,baseline?"Fade between images (1.5 seconds)":"Fade between images")!=null,"Fade label matches expected version");
  Render(settings,"settings");
  if(baseline){Invoke(settings,"Cancel");return;}
  Invoke(settings,"Save");await Task.Delay(400);
  Check(Window("DeskFrame Controls")!=null,"Saving tray-free setup exposes existing controls");
  var saved=json.Deserialize<System.Collections.Generic.Dictionary<string,object>>(File.ReadAllText(Path.Combine(root,"app","DeskFrameData","settings.json")));
  Check(Convert.ToDouble(saved["Opacity"])==1 && Convert.ToInt32(saved["Width"])==320,"UIA numeric values persist");
  CheckDisplay(await WaitWindow("DeskFrame"),"settings-99-to-100",100,true);
 }
 static string Texts(AutomationElement w){return String.Join(" | ",w.FindAll(TreeScope.Descendants,Condition.TrueCondition).Cast<AutomationElement>().Select(e=>e.Current.Name));}
 static async Task SourceRecovery(){
  var controls=await Controls();Directory.Move(source,source+"-offline");
  try {
   await Task.Delay(4500);controls=Window("DeskFrame Controls");string text=Texts(controls);File.AppendAllText(log,"Unavailable controls: "+text+Environment.NewLine);
   Check(baseline?text.Contains("DeskFrame is running"):text.Contains("Pictures unavailable. Retrying automatically."),"Source-loss explanation");
   Check(Find(controls,baseline?"Hide Frame":"Frame unavailable")!=null,"Source-loss toggle wording");
   if(!baseline)Check(!Find(controls,"Frame unavailable").Current.IsEnabled,"Unavailable frame cannot misleadingly Hide");
   Render(controls,"source-unavailable");Invoke(controls,"Pause Slideshow");
  } finally {Directory.Move(source+"-offline",source);}
  await Task.Delay(11000);CheckDisplay(await WaitWindow("DeskFrame"),"source-recovered-paused",baseline?99:100,true);
  if(!baseline)Check(Texts(Window("DeskFrame Controls")).Contains("Slideshow paused"),"Recovery refreshes existing status while paused");
  await Stop();Directory.Move(source,source+"-offline");
  try {
   Start();var setup=await WaitWindow("Welcome to DeskFrame");Invoke(setup,"Cancel");await Task.Delay(500);
   Check(baseline?Window("DeskFrame Controls")==null:Window("DeskFrame Controls")!=null,"Cancelled unavailable-source setup exposes recovery controls");
   controls=await Controls();Check(Find(controls,"Change Pictures...")!=null && Find(controls,"Exit DeskFrame")!=null,"Missing-source controls offer recovery and exit");
   Render(controls,"startup-recovery");Invoke(controls,"Exit DeskFrame");Check(await Task.Run(()=>app.WaitForExit(5000)),"Exit button closes unavailable-source application");
  } finally {Directory.Move(source+"-offline",source);}
  await Stop();
 }
 static async Task Sequence(){
  var w=await WaitWindow("DeskFrame");var area=Screen.PrimaryScreen.WorkingArea;var fixedArea=new Rectangle(area.Right-354,area.Bottom-514,340,500);
  for(int i=0;i<100;i++){
   CheckDisplay(w,"transition-"+i,100,false);Rect r;GetWindowRect(Handle(w),out r);
   Check(r.R==area.Right-24 && r.B==area.Bottom-24,"Transition retains bottom-right anchor");
   if(i%3==0)using(var b=Capture(fixedArea))b.Save(Path.Combine(root,"transition-"+i.ToString("D3")+"-desktop.png"));
   await Task.Delay(80);
  }
 }
 static async Task Run(){
  try {
   if(!Environment.GetCommandLineArgs().Contains("--ui-only")) foreach(string mode in new[]{"Auto","Fit","Fill"})foreach(int opacity in new[]{90,99,100}){
    Preferences(mode,opacity);Start();var w=await WaitWindow("DeskFrame");await Task.Delay(400);CheckDisplay(w,mode+"-"+opacity,opacity,true);if(opacity==100){Render(w,mode+"-100");await Task.Delay(4200);CheckDisplay(w,mode+"-100-after-slideshow",100,true);}await Stop();
   }
   if(!baseline && !Environment.GetCommandLineArgs().Contains("--ui-only")){Preferences("Auto",100);Start();await Sequence();await Stop();}
   Preferences("Auto",99);Start();await WaitWindow("DeskFrame");await AccessibilityAndSettings();await SourceRecovery();
   File.AppendAllText(log,"ALL TARGETED BETA CHECKS PASSED"+Environment.NewLine);
  } catch(Exception e){File.AppendAllText(log,e.ToString()+Environment.NewLine);Environment.ExitCode=1;}
  try {await Stop();} finally {backdrop.Close();}
 }
 [STAThread] static void Main(string[] args){
  SetProcessDPIAware();Application.EnableVisualStyles();
  if(Process.GetProcessesByName("DeskFrame").Length!=0)throw new Exception("Close existing DeskFrame before isolated test");
  baseline=args.Contains("--baseline");root=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"beta-fixes-20260926",(baseline?"baseline-":"candidate-")+DateTime.Now.ToString("yyyyMMdd-HHmmss"));
  Directory.CreateDirectory(Path.Combine(root,"app"));source=Path.Combine(root,"samples");Directory.CreateDirectory(source);log=Path.Combine(root,"results.txt");
  exe=Path.Combine(root,"app","DeskFrame.exe");File.Copy(args[0],exe);File.Copy(args[0]+".config",exe+".config");
  for(int i=0;i<2;i++)using(var b=new Bitmap(i==0?640:400,i==0?400:600)){
   using(var g=Graphics.FromImage(b)){g.Clear(i==0?Color.Coral:Color.SlateBlue);g.FillEllipse(Brushes.Gold,80,80,160,160);g.DrawRectangle(Pens.White,2,2,b.Width-5,b.Height-5);}b.Save(Path.Combine(source,i+".png"));
  }
  var area=Screen.PrimaryScreen.WorkingArea;
  backdrop=new Form{Text="DeskFrame isolated regression backdrop",FormBorderStyle=FormBorderStyle.None,StartPosition=FormStartPosition.Manual,Bounds=new Rectangle(area.Right-650,area.Bottom-800,650,800),BackColor=Color.FromArgb(30,180,120),TopMost=true,ShowInTaskbar=false};
  backdrop.Shown+=async delegate {
   SetWindowPos(backdrop.Handle,new IntPtr(-1),area.Right-650,area.Bottom-800,650,800,0x10);
   await Task.Delay(200);await Run();
  };Application.Run(backdrop);
 }
}


