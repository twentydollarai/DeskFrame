using System;
using System.IO;
using System.Linq;
using System.Drawing;
using System.Drawing.Imaging;
using System.Reflection;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Windows.Forms;
namespace DeskFrame {
 internal static class RenderingTests {
  [System.Runtime.InteropServices.DllImport("user32.dll")] static extern bool GetWindowRect(IntPtr hwnd,out NativeRect rect);
  struct NativeRect {public int L,T,R,B;}
  static string NativeSize(Viewer v) {NativeRect r;GetWindowRect(v.Handle,out r);return (r.R-r.L)+"x"+(r.B-r.T);}
  static BindingFlags flags=BindingFlags.Instance|BindingFlags.NonPublic;
  static string output;
  static int checks;
  static void Check(bool ok,string name) { File.AppendAllText(Path.Combine(output,"render-results.txt"),(ok?"PASS ":"FAIL ")+name+"\r\n"); if(!ok)throw new Exception(name);checks++; }
  static void Set(Viewer v,string name,object value) {typeof(Viewer).GetField(name,flags).SetValue(v,value);}
  static Bitmap Read(Viewer v,string path) {return (Bitmap)typeof(Viewer).GetMethod("ReadNext",flags).Invoke(v,new object[]{new[]{path},0,null});}
  static string Hash(string path) {using(var h=SHA256.Create())return Convert.ToBase64String(h.ComputeHash(File.ReadAllBytes(path)));}
  static Bitmap Paint(Viewer v) {var b=new Bitmap(320,480);using(var g=Graphics.FromImage(b))typeof(Viewer).GetMethod("OnPaint",flags).Invoke(v,new object[]{new PaintEventArgs(g,new Rectangle(0,0,320,480))});return b;}
  [STAThread] static void Main() {
   output=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"render-investigation");Directory.CreateDirectory(output);
   File.WriteAllText(Path.Combine(output,"render-results.txt"),"DeskFrame rendering regression\r\n");
   try {Run();RunModes();File.AppendAllText(Path.Combine(output,"render-results.txt"),"ALL RENDERING TESTS PASSED: "+checks+" checks\r\n");}
   catch(Exception e) {File.AppendAllText(Path.Combine(output,"render-results.txt"),e+"\r\n");Environment.ExitCode=1;}
  }
  static void Run() {
   Native.SetProcessDPIAware();
   using(var viewer=new Viewer()) {
    Set(viewer,"settings",new Preferences{FrameSizing="Fixed frame"});Set(viewer,"canvasSize",new Size(320,480));viewer.ClientSize=new Size(320,480);
    Size[] sizes={new Size(400,600),new Size(600,400),new Size(500,500),new Size(300,600),new Size(450,600),new Size(40,60),new Size(2400,3600),new Size(2000,100),new Size(100,2000)};
    using(var sheet=new Bitmap(320*5,480*2)) {
     using(var sheetGraphics=Graphics.FromImage(sheet)) {
      for(int i=0;i<sizes.Length;i++) {
       Size s=sizes[i];string path=Path.Combine(output,"fit-"+i+".png");
       using(var source=new Bitmap(s.Width,s.Height)) {
        using(var g=Graphics.FromImage(source)) {
         g.Clear(Color.Coral);
         // Four corner patches expose cropping, displacement, and aspect distortion.
         g.FillRectangle(Brushes.Red,0,0,s.Width/4,s.Height/4);
         g.FillRectangle(Brushes.Lime,3*s.Width/4,0,s.Width/4+1,s.Height/4);
         g.FillRectangle(Brushes.Blue,0,3*s.Height/4,s.Width/4,s.Height/4+1);
         g.FillRectangle(Brushes.Yellow,3*s.Width/4,3*s.Height/4,s.Width/4+1,s.Height/4+1);
        }source.Save(path);
       }
       string hash=Hash(path);
       using(var rendered=Read(viewer,path)) {
        double scale=Math.Min(320.0/s.Width,480.0/s.Height);
        int w=Math.Max(1,(int)Math.Round(s.Width*scale)),h=Math.Max(1,(int)Math.Round(s.Height*scale));int left=(320-w)/2,top=(480-h)/2;
        Color[] colors={Color.Red,Color.Lime,Color.Blue,Color.Yellow};
        for(int corner=0;corner<4;corner++) {
         int x=left+Math.Min(w-1,(int)(w*((corner%2==0)?.12:.88)));
         int y=top+Math.Min(h-1,(int)(h*((corner<2)?.12:.88)));
         Color actual=rendered.GetPixel(x,y),expected=colors[corner];
         Check(Math.Abs(actual.R-expected.R)<10 && Math.Abs(actual.G-expected.G)<10 && Math.Abs(actual.B-expected.B)<10,"Complete centered Fit corner "+corner+" at "+s);
        }
        bool valid=true;
        for(int y=0;y<480;y++)for(int x=0;x<320;x++) {
         Color c=rendered.GetPixel(x,y);valid &= c.A==255;
         if(x<left || x>=left+w || y<top || y>=top+h)valid &= c.R>=174 && c.G>=174 && c.B>=174;
        }
        Check(valid,"Opaque canvas and light margins at "+s);
        sheetGraphics.DrawImageUnscaled(rendered,(i%5)*320,(i/5)*480);
       }
       Check(hash==Hash(path),"Source unchanged at "+s);
      }
     }
     sheet.Save(Path.Combine(output,"fit-contact-sheet.png"));
    }
    string transparent=Path.Combine(output,"alpha.png");
    using(var b=new Bitmap(600,400)) {using(var g=Graphics.FromImage(b)) {g.Clear(Color.Transparent);g.FillRectangle(Brushes.Red,200,100,200,200);using(var brush=new SolidBrush(Color.FromArgb(128,0,0,255)))g.FillRectangle(brush,0,0,150,400);}b.Save(transparent);}
    string alphaHash=Hash(transparent);
    using(var b=Read(viewer,transparent)) {
     Check(b.GetPixel(319,240).ToArgb()==b.GetPixel(319,0).ToArgb(),"Transparent pixels reveal matte without black");
     Color c=b.GetPixel(20,240),mat=b.GetPixel(319,0);
     Check(Math.Abs(c.R-mat.R/2)<3 && Math.Abs(c.B-(mat.B+255)/2)<3 && c.A==255,"Partial alpha composites correctly onto opaque matte");
     b.Save(Path.Combine(output,"alpha-after.png"));
    }
    Check(alphaHash==Hash(transparent),"Transparent source unchanged");
    string black=Path.Combine(output,"black.png");using(var b=new Bitmap(600,400)) {using(var g=Graphics.FromImage(b))g.Clear(Color.Black);b.Save(black);}
    using(var b=Read(viewer,black)) {Check(b.GetPixel(160,240).R==0 && b.GetPixel(160,0).R>=174,"Embedded source black preserved; matte stays light");}
    Set(viewer,"current",Read(viewer,Path.Combine(output,"fit-0.png")));
    Set(viewer,"next",Read(viewer,transparent));
    var clock=(Stopwatch)typeof(Viewer).GetField("clock",flags).GetValue(viewer);clock.Stop();Rectangle bounds=viewer.Bounds;
    foreach(double fraction in new[]{0.0,.25,.5,.75,1.0}) {
     Set(viewer,"fadeStart",clock.Elapsed.TotalSeconds-1.5*fraction);
     using(var b=Paint(viewer)) {
      var current=(Bitmap)typeof(Viewer).GetField("current",flags).GetValue(viewer);var next=(Bitmap)typeof(Viewer).GetField("next",flags).GetValue(viewer);
      bool valid=true;for(int y=0;y<480;y+=11)for(int x=0;x<320;x+=11) {
       Color a=current.GetPixel(x,y),n=next.GetPixel(x,y),c=b.GetPixel(x,y);
       valid &= Math.Abs(c.R-(a.R*(1-fraction)+n.R*fraction))<=3 && Math.Abs(c.G-(a.G*(1-fraction)+n.G*fraction))<=3 && Math.Abs(c.B-(a.B*(1-fraction)+n.B*fraction))<=3;
      }
      Check(valid,"Whole canvas dissolve at "+fraction+" without stale pixels");
      if(fraction==.5)b.Save(Path.Combine(output,"alpha-mid-fade.png"));
     }
     Check(viewer.Bounds==bounds,"Position and size stable at "+fraction);
    }
    typeof(Viewer).GetMethod("CompleteTransition",flags).Invoke(viewer,null);
    using(var b=Paint(viewer))using(var expected=Read(viewer,transparent)) {Check(b.GetPixel(160,0)==expected.GetPixel(160,0) && b.GetPixel(160,240)==expected.GetPixel(160,240),"Completed fade retains no previous image");}
    GC.Collect();GC.WaitForPendingFinalizers();long before=Process.GetCurrentProcess().PrivateMemorySize64;
    for(int i=0;i<150;i++)using(var b=Read(viewer,transparent)){}
    GC.Collect();GC.WaitForPendingFinalizers();long after=Process.GetCurrentProcess().PrivateMemorySize64;
    Check(after-before<20*1024*1024,"150 decode/dispose cycles private-memory growth below 20 MiB: "+((after-before)/1024)+" KiB");
    File.AppendAllText(Path.Combine(output,"render-results.txt"),"Rendering process working set: "+Process.GetCurrentProcess().WorkingSet64/1024/1024+" MiB; two 320x480 canvases: 1.17 MiB\r\n");
   }
  }
  static object Get(Viewer v,string field) {return typeof(Viewer).GetField(field,flags).GetValue(v);}
  static void Call(Viewer v,string method,params object[] args) {typeof(Viewer).GetMethod(method,flags).Invoke(v,args);}
  static Bitmap CoverReference(Bitmap source,Size destination) {
   var result=new Bitmap(destination.Width,destination.Height,PixelFormat.Format32bppPArgb);
   double scale=Math.Max((double)destination.Width/source.Width,(double)destination.Height/source.Height);
   float width=(float)(destination.Width/scale),height=(float)(destination.Height/scale);
   using(var g=Graphics.FromImage(result))using(var attributes=new ImageAttributes()) {
    g.Clear(Color.FromArgb(232,232,232));g.InterpolationMode=System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;g.PixelOffsetMode=System.Drawing.Drawing2D.PixelOffsetMode.HighQuality;
    attributes.SetWrapMode(System.Drawing.Drawing2D.WrapMode.TileFlipXY);
    g.DrawImage(source,new Rectangle(Point.Empty,destination),(source.Width-width)/2,(source.Height-height)/2,width,height,GraphicsUnit.Pixel,attributes);
   }return result;
  }
  static void RunModes() {
   var legacy=Store.Json.Deserialize<Preferences>("{\"Width\":280,\"Opacity\":0.75,\"X\":50,\"Y\":60}");legacy.Validate();
   Check(legacy.FrameSizing=="Auto-fit" && legacy.ImageFitting=="Fit" && legacy.Width==280 && legacy.X==50,"Legacy preferences migrate to Auto-fit with prior fields intact");
   var store=new Store(Path.Combine(output,"mode-prefs"));
   var p=new Preferences{FrameSizing="Fixed frame",ImageFitting="Fill",AnchorX=700,AnchorY=800,Monitor="sample-monitor"};store.Save(p);var loaded=store.Load();
   Check(loaded.FrameSizing==p.FrameSizing && loaded.ImageFitting==p.ImageFitting && loaded.AnchorX==700 && loaded.Monitor==p.Monitor,"Mode, fitting, monitor and anchor persist");
   using(var v=new Viewer()) {
    Set(v,"workArea",new Rectangle(0,0,1920,1080));
    Set(v,"settings",new Preferences());
    for(int i=0;i<9;i++)using(var b=Read(v,Path.Combine(output,"fit-"+i+".png"))) {
     using(var src=Image.FromFile(Path.Combine(output,"fit-"+i+".png"))) {
      double scale=Math.Min(320.0/src.Width,1032.0/src.Height);
      Check(b.Width==Math.Max(1,(int)Math.Round(src.Width*scale)) && b.Height==Math.Max(1,(int)Math.Round(src.Height*scale)),"Auto-fit exact source bounds "+src.Size);
      Color[] corners={Color.Red,Color.Lime,Color.Blue,Color.Yellow};
      for(int c=0;c<4;c++) {
       Color actual=b.GetPixel(Math.Min(b.Width-1,(int)(b.Width*(c%2==0?.12:.88))),Math.Min(b.Height-1,(int)(b.Height*(c<2?.12:.88))));Color expected=corners[c];
       Check(Math.Abs(actual.R-expected.R)<10 && Math.Abs(actual.G-expected.G)<10 && Math.Abs(actual.B-expected.B)<10,"Auto-fit complete corner "+c+" source "+i);
      }
     }
    }
    foreach(Size size in new[]{new Size(1,12000),new Size(12000,1),new Size(1,1)}) {
     string path=Path.Combine(output,"extreme-"+size.Width+"-"+size.Height+".png");
     using(var b=new Bitmap(size.Width,size.Height)){using(var g=Graphics.FromImage(b))g.Clear(Color.Coral);b.Save(path);}
     string hash=Hash(path);
     foreach(string mode in new[]{"Auto-fit","Fit","Fill"}) {
      Set(v,"settings",new Preferences{FrameSizing=mode=="Auto-fit"?"Auto-fit":"Fixed frame",ImageFitting=mode=="Fill"?"Fill":"Fit"});Set(v,"canvasSize",new Size(320,480));
      using(var b=Read(v,path))Check(b.Width>=1 && b.Height>=1 && b.Width<=320 && b.Height<=1032,"Valid extreme dimensions "+size+" in "+mode);
     }
     Check(Hash(path)==hash,"Extreme source unchanged "+size);
    }
    Set(v,"settings",new Preferences());
    using(var b=Read(v,Path.Combine(output,"alpha.png")))Check(b.Size==new Size(320,213) && b.GetPixel(319,100).A==255 && b.GetPixel(319,100).R>=174,"Auto-fit transparency uses opaque matte only within source bounds");
    string border=Path.Combine(output,"border.png");
    using(var b=new Bitmap(600,400)){using(var g=Graphics.FromImage(b)){g.Clear(Color.Black);g.FillRectangle(Brushes.Coral,60,40,480,320);}b.Save(border);}
    string borderHash=Hash(border);
    using(var b=Read(v,border))Check(b.GetPixel(3,3).R==0 && b.GetPixel(160,100).R==255,"Auto-fit preserves embedded black borders");
    Check(Hash(border)==borderHash,"Embedded-border source unchanged");
    // Analytic color ramp verifies centered Fill mapping and equal scale on both axes.
    string ramp=Path.Combine(output,"ramp.png");
    using(var b=new Bitmap(600,400)){for(int y=0;y<400;y++)for(int x=0;x<600;x++)b.SetPixel(x,y,Color.FromArgb(x*255/599,y*255/399,80));b.Save(ramp);}
    string rampHash=Hash(ramp);
    Set(v,"settings",new Preferences{FrameSizing="Fixed frame",ImageFitting="Fill"});Set(v,"canvasSize",new Size(320,480));
    using(var b=Read(v,ramp)) {
     bool valid=true;
     for(int y=10;y<480;y+=31)for(int x=10;x<320;x+=29) {
      Color c=b.GetPixel(x,y);double sourceX=(600-320/1.2)/2+x/1.2,sourceY=y/1.2;
      valid &= Math.Abs(c.R-sourceX*255/599)<3 && Math.Abs(c.G-sourceY*255/399)<3 && c.B==80 && c.A==255;
     }
     Check(valid,"Fixed Fill centered crop maps both axes at equal scale without padding");b.Save(Path.Combine(output,"fill-after.png"));
    }
    Check(Hash(ramp)==rampHash,"Fill source unchanged");
   }
   foreach(Rectangle area in new[]{new Rectangle(0,0,1920,1040),new Rectangle(-1280,0,1280,984),new Rectangle(0,-900,1440,860),new Rectangle(0,0,800,560),new Rectangle(0,0,3840,2120)})
   foreach(int margin in new[]{0,24,500})foreach(string corner in new[]{"top-left","top-right","bottom-left","bottom-right"}) {
    Rectangle usable=ImageLayout.Inset(area,margin);Point anchor=ImageLayout.Corner(usable,corner);
    bool valid=true;
    foreach(Size source in new[]{new Size(600,400),new Size(400,600),new Size(1,12000),new Size(12000,1),new Size(10000,10000)}) {
     Size fit=ImageLayout.Fit(source,new Size(Math.Min(1600,usable.Width),usable.Height));Rectangle bounds=ImageLayout.Place(fit,area,anchor,corner);
     valid &= area.Contains(bounds) && ImageLayout.Corner(bounds,corner)==anchor && fit.Width>=1 && fit.Height>=1;
    }
    Check(valid,"Work-area limits and stable corner "+area+" margin "+margin+" "+corner);
   }
   // Analytic geometry plus independently calculated proportional-cover reference scenes.
   foreach(string corner in new[]{"top-left","top-right","bottom-left","bottom-right"})using(var v=new Viewer()) {
    var p2=new Preferences{Files=new[]{Path.Combine(output,"fit-0.png")},Corner=corner,Hotkey="",Opacity=.75};
    v.Configure(p2);v.SetPaused(true);Point anchor=v.AnchorPosition;
    foreach(int i in new[]{1,0,1,1,2,7,8,0}) {
     var old=(Bitmap)Get(v,"current");var incoming=Read(v,Path.Combine(output,"fit-"+i+".png"));
     using(var oldCopy=new Bitmap(old))using(var newCopy=new Bitmap(incoming)) {
      Set(v,"next",incoming);Set(v,"fadeStart",10.0);
      foreach(double elapsed in new[]{0.0,.055,.11,.165,.22,.275,.33,.385,.439,.441,.46}) {
       Call(v,"AdvanceTransition",10+elapsed);
       bool active=elapsed<.44;
       double t=Math.Min(1,elapsed/.44);t=t*t*(3-2*t);
       Size wanted=active ? new Size(Math.Max(1,(int)Math.Round(oldCopy.Width+(newCopy.Width-oldCopy.Width)*t)),Math.Max(1,(int)Math.Round(oldCopy.Height+(newCopy.Height-oldCopy.Height)*t))) : newCopy.Size;
       Check(v.ClientSize==wanted,"Exact interpolated native viewport at "+elapsed+" "+corner+" actual="+v.ClientSize+" wanted="+wanted+" progress="+Get(v,"transitionProgress")+" native="+NativeSize(v));
       Check((byte)Get(v,"presentedAlpha")==191,"Successful layered presentation submitted configured opacity "+corner);
       if(active)Check(ReferenceEquals(Get(v,"current"),old) && ReferenceEquals(Get(v,"next"),incoming),"Both originals retained until completion "+corner);
       else Check(Get(v,"next")==null && v.ClientSize==newCopy.Size,"Incoming full image and exact final bounds "+corner);
       using(var shot=new Bitmap(v.Width,v.Height))using(var from=CoverReference(oldCopy,wanted))using(var to=CoverReference(newCopy,wanted)) {
        using(var g=Graphics.FromImage(shot)) {
         g.Clear(Color.Magenta);
         typeof(Viewer).GetMethod("OnPaint",flags).Invoke(v,new object[]{new PaintEventArgs(g,new Rectangle(Point.Empty,shot.Size))});
        }
        bool same=true;int nonMatte=0;
        for(int y=0;y<shot.Height;y++)for(int x=0;x<shot.Width;x++) {
         Color actual=shot.GetPixel(x,y),aa=from.GetPixel(x,y),bb=active?to.GetPixel(x,y):newCopy.GetPixel(x,y);
         double f=active?t:1;
         same &= actual.A==255 && Math.Abs(actual.R-(aa.R*(1-f)+bb.R*f))<=4 && Math.Abs(actual.G-(aa.G*(1-f)+bb.G*f))<=4 && Math.Abs(actual.B-(aa.B*(1-f)+bb.B*f))<=4;
         if(Math.Abs(actual.R-232)+Math.Abs(actual.G-232)+Math.Abs(actual.B-232)>20)nonMatte++;
        }
        Check(same,"Full viewport is proportional-cover crossfade with no added bands or displaced rectangles "+elapsed+" "+corner);
        Check(nonMatte>0,"Image content remains throughout transition "+elapsed+" "+corner);
        if(corner=="bottom-right" && i==2)shot.Save(Path.Combine(output,"animated-phase-"+elapsed.ToString("F3",System.Globalization.CultureInfo.InvariantCulture)+".png"));
       }
       Check(ImageLayout.Corner(v.Bounds,corner)==anchor && Screen.PrimaryScreen.WorkingArea.Contains(v.Bounds),"Fixed selected corner and work-area containment "+corner);
      }
     }
    }
    Set(v,"next",Read(v,Path.Combine(output,"fit-1.png")));Set(v,"fadeStart",10.0);Call(v,"AdvanceTransition",20.0);
    Check(!v.Fading && Get(v,"next")==null && v.ClientSize==((Bitmap)Get(v,"current")).Size,"Late tick settles directly to exact incoming bounds "+corner);
    Set(v,"next",Read(v,Path.Combine(output,"fit-2.png")));Set(v,"fadeStart",10.0);Call(v,"AdvanceTransition",10.22);
    float progress=(float)Get(v,"transitionProgress");var retained=Get(v,"next");Size pausedSize=v.Size;
    Call(v,"Tick",null,EventArgs.Empty);
    Check((float)Get(v,"transitionProgress")==progress && ReferenceEquals(Get(v,"next"),retained) && v.Size==pausedSize,"Paused tick freezes both geometry and blend "+corner);
    p2.AnchorX=anchor.X;p2.AnchorY=anchor.Y;p2.X=v.Left;p2.Y=v.Top;v.Configure(p2);
    Check(Get(v,"next")==null && ImageLayout.Corner(v.Bounds,corner)==anchor,"Reconfiguration disposes next and restores anchor "+corner);
    Set(v,"next",Read(v,Path.Combine(output,"fit-1.png")));Call(v,"CompleteTransition");
    Check(v.ClientSize==((Bitmap)Get(v,"current")).Size && (byte)Get(v,"presentedAlpha")==191,"No-fade direct presentation has exact bounds and user alpha "+corner);
   }
   foreach(var screen in Screen.AllScreens)foreach(string corner in new[]{"top-left","top-right","bottom-left","bottom-right"})using(var v=new Viewer()) {
    foreach(string file in new[]{"fit-6.png","extreme-1-12000.png","extreme-12000-1.png"}) {
     v.Configure(new Preferences{Files=new[]{Path.Combine(output,file)},Monitor=screen.DeviceName,Corner=corner,Hotkey="",Width=1600,Opacity=1});
     Check(screen.WorkingArea.Contains(v.Bounds) && v.ClientSize==((Bitmap)Get(v,"current")).Size,"Native monitor contains exact image bounds "+screen.DeviceName+" "+corner+" "+file+" actual="+v.Bounds+" bitmap="+((Bitmap)Get(v,"current")).Size);
     Check((Native.GetWindowLong(v.Handle,-20)&0x08080020)==0x08080020,"Layered/click-through/no-activate retained at 100% opacity");
    }
    using(var g=v.CreateGraphics())File.AppendAllText(Path.Combine(output,"render-results.txt"),"Native viewer DPI "+screen.DeviceName+": "+g.DpiX+"; work area "+screen.WorkingArea+"\r\n");
   }   using(var v=new Viewer()) {
    v.Configure(new Preferences{Files=new[]{Path.Combine(output,"fit-0.png")},Hotkey="",Monitor="disconnected-monitor",AnchorX=-9999,AnchorY=-9999});
    Check(Screen.PrimaryScreen.WorkingArea.Contains(v.Bounds),"Disconnected monitor and stale coordinates fall back on screen");
   }
  }
 }
}







