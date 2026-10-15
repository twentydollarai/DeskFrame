using System;
using System.IO;
using System.Linq;
using System.Drawing;
using System.Drawing.Imaging;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using System.Diagnostics;

namespace DeskFrame {
    // Keep the native layered/no-activate viewer and bounded, display-sized image cache.
    internal sealed class Viewer : Form {
        Preferences settings=new Preferences();
        readonly Timer timer=new Timer();
        readonly Stopwatch clock=Stopwatch.StartNew();
        Bitmap current,next;
        const double AutoFadeSeconds=.44;
        readonly LayeredSurface surface=new LayeredSurface();
        byte presentedAlpha;
        float transitionProgress;
        Size canvasSize;
        Rectangle workArea;
        Point anchor;
        internal Point AnchorPosition {get{return anchor;}}
        bool AutoFit {get{return settings.FrameSizing=="Auto-fit";}}
        string currentPath,nextPath;
        double nextChange,fadeStart=-1,retryAt;
        bool hidden,unavailable;
        string registeredHotkey="";
        string movementShortcutWarning,showHideShortcutWarning;
        internal string ShortcutWarning {
            get {return String.Join("\n",new[]{movementShortcutWarning,showHideShortcutWarning}.Where(s=>!String.IsNullOrEmpty(s)));}
        }
        internal bool Paused {get;private set;}
        internal bool HasImage {get{return current!=null;}}
        internal bool IsHidden {get{return hidden;}}
        internal bool Fading {get{return fadeStart>=0;}}
        internal int Rotations {get;private set;}
        internal event Action SourceUnavailable,PositionChanged;
        internal event Action StateChanged;
        internal Viewer() {
            Text="DeskFrame";FormBorderStyle=FormBorderStyle.None;ShowInTaskbar=false;
            StartPosition=FormStartPosition.Manual;DoubleBuffered=true;
            BackColor=Color.FromArgb(232,232,232);AutoScaleMode=AutoScaleMode.None;
            timer.Interval=250;timer.Tick+=Tick;timer.Start();
            Microsoft.Win32.SystemEvents.DisplaySettingsChanged+=DisplayChanged;
        }
        protected override bool ShowWithoutActivation {get{return true;}}
        protected override CreateParams CreateParams {
            get {var p=base.CreateParams;p.ExStyle|=0x08000000|0x80|0x80000;if(settings.ClickThrough)p.ExStyle|=0x20;return p;}
        }
        protected override void OnHandleCreated(EventArgs e) {
            base.OnHandleCreated(e);
            var failed=new System.Collections.Generic.List<string>();
            for(int i=0;i<4;i++) {
                if(!Native.RegisterHotKey(Handle,10+i,0x4007,(uint)Keys.Left+(uint)i))
                    failed.Add(((Keys)((int)Keys.Left+i)).ToString());
            }
            movementShortcutWarning=failed.Count==0 ? null :
                "Movement shortcuts unavailable: Ctrl+Alt+Shift with "+String.Join(", ",failed)+". Use the Move frame buttons in Controls.";
        }
        protected override void OnHandleDestroyed(EventArgs e) {
            Native.UnregisterHotKey(Handle,1);Native.UnregisterHotKey(Handle,2);
            for(int i=0;i<4;i++)Native.UnregisterHotKey(Handle,10+i);
            base.OnHandleDestroyed(e);
        }
        internal string CheckHotkey(string value) {
            if(value==registeredHotkey)return null;
            uint mods,key;Native.ParseHotkey(value,out mods,out key);
            if(key==0)return null;
            if(!Native.RegisterHotKey(Handle,2,mods|0x4000,key))return "That shortcut is already in use. Choose another, or leave it blank. Use Controls to show or hide the frame.";
            Native.UnregisterHotKey(Handle,2);return null;
        }
        internal string Configure(Preferences p) {
            settings=p.Copy();settings.Validate();
            workArea=(Screen.AllScreens.FirstOrDefault(s=>s.DeviceName==settings.Monitor) ?? Screen.PrimaryScreen).WorkingArea;
            anchor=ImageLayout.Corner(ImageLayout.Inset(workArea,settings.Margin),settings.Corner);
            Native.UnregisterHotKey(Handle,1);registeredHotkey="";
            uint mods,key;Native.ParseHotkey(p.Hotkey,out mods,out key);
            showHideShortcutWarning=null;
            if(key!=0) {
                if(Native.RegisterHotKey(Handle,1,mods|0x4000,key))registeredHotkey=p.Hotkey;
                else showHideShortcutWarning="Show/hide shortcut unavailable: "+p.Hotkey+". Change it in Settings or use Controls.";
            }
            int style=Native.GetWindowLong(Handle,-20);
            style=p.ClickThrough ? style|0x20 : style&~0x20;
            Native.SetWindowLong(Handle,-20,style|0x08080080);
            TopMost=p.Topmost;
            Native.SetWindowPos(Handle,new IntPtr(p.Topmost ? -1 : -2),0,0,0,0,0x33);
            DisposeImages();
            string[] paths=ImageSource.Find(settings);
            canvasSize=AutoFit ? Size.Empty : DetermineCanvas(paths);
            current=ReadNext(paths,0,out currentPath);fadeStart=-1;timer.Interval=250;
            unavailable=false;
            if(current!=null) {
                if(settings.RememberPosition) {
                    if(settings.AnchorX.HasValue && settings.AnchorY.HasValue && settings.AnchorX>=workArea.Left && settings.AnchorX<=workArea.Right && settings.AnchorY>=workArea.Top && settings.AnchorY<=workArea.Bottom)
                        anchor=new Point(settings.AnchorX.Value,settings.AnchorY.Value);
                    else if(settings.X.HasValue && settings.Y.HasValue) {
                        var legacy=new Rectangle(settings.X.Value,settings.Y.Value,current.Width,current.Height);
                        if(workArea.Contains(legacy))anchor=ImageLayout.Corner(legacy,settings.Corner);
                    }
                }
                PresentFrame();
                if(!hidden)ShowQuietly();
            } else MarkUnavailable();
            nextChange=clock.Elapsed.TotalSeconds+settings.IntervalSeconds;
            Invalidate();if(StateChanged!=null)StateChanged();return String.IsNullOrEmpty(ShortcutWarning) ? null : ShortcutWarning;
        }
        void ShowQuietly() {
            Native.ShowWindow(Handle,4); // SW_SHOWNOACTIVATE, including restoration from tray/hotkey
            Native.SetWindowPos(Handle,new IntPtr(settings.Topmost ? -1 : -2),0,0,0,0,0x13);
        }
        internal void MoveFrame(int dx,int dy) {
            if(!HasImage)return;
            Point moved=new Point(anchor.X+dx,anchor.Y+dy);
            Rectangle placed=ImageLayout.Place(ViewportSize(),workArea,moved,settings.Corner);
            anchor=ImageLayout.Corner(placed,settings.Corner);PresentFrame();
            if(PositionChanged!=null)PositionChanged();
        }
        internal void Toggle() {hidden=!hidden;if(hidden)Hide();else if(HasImage)ShowQuietly();if(StateChanged!=null)StateChanged();}
        internal void SetPaused(bool value) {Paused=value;if(value)clock.Stop();else clock.Start();if(StateChanged!=null)StateChanged();}
        internal static bool AnyReadable(Preferences p) {
            foreach(string path in ImageSource.Find(p)) {
                try {using(var image=Image.FromFile(path)) if(image.Width>0 && image.Height>0)return true;} catch(ArgumentException) {} catch(IOException) {} catch(OutOfMemoryException) {} catch(UnauthorizedAccessException) {}
            }return false;
        }
        Size FitSize(int width,int height) {
            var area=ImageLayout.Inset(workArea.Width>0 ? workArea : Screen.PrimaryScreen.WorkingArea,settings.Margin);
            return ImageLayout.Fit(new Size(width,height),new Size(Math.Min(settings.Width,area.Width),area.Height));
        }
        Size DetermineCanvas(string[] paths) {
            int w=1,h=1;
            foreach(string path in paths) {
                try {using(var source=Image.FromFile(path)) {
                    bool sideways=false;
                    if(source.PropertyIdList.Contains(0x112)) {int o=source.GetPropertyItem(0x112).Value[0];sideways=o>=5 && o<=8;}
                    Size fit=FitSize(sideways ? source.Height : source.Width,sideways ? source.Width : source.Height);
                    w=Math.Max(w,fit.Width);h=Math.Max(h,fit.Height);
                }} catch(ArgumentException) {} catch(IOException) {} catch(OutOfMemoryException) {} catch(UnauthorizedAccessException) {}
            }
            return new Size(w,h);
        }
        Bitmap ReadNext(string[] paths,int start,out string selected) {
            for(int k=0;k<paths.Length;k++) {
                string path=paths[(start+k)%paths.Length];
                try {
                    using(var source=Image.FromFile(path)) {
                        if(source.PropertyIdList.Contains(0x112)) {
                            int o=source.GetPropertyItem(0x112).Value[0];
                            RotateFlipType[] flips={RotateFlipType.RotateNoneFlipNone,RotateFlipType.RotateNoneFlipNone,RotateFlipType.RotateNoneFlipX,RotateFlipType.Rotate180FlipNone,RotateFlipType.Rotate180FlipX,RotateFlipType.Rotate90FlipX,RotateFlipType.Rotate90FlipNone,RotateFlipType.Rotate270FlipX,RotateFlipType.Rotate270FlipNone};
                            if(o>=1 && o<=8)source.RotateFlip(flips[o]);
                        }
                        Size target=AutoFit ? FitSize(source.Width,source.Height) : canvasSize;
                        var b=new Bitmap(target.Width,target.Height,PixelFormat.Format32bppPArgb);
                        try {
                            using(var g=Graphics.FromImage(b)) {
                                g.Clear(MatteColor(source));g.InterpolationMode=InterpolationMode.HighQualityBicubic;g.PixelOffsetMode=PixelOffsetMode.HighQuality;
                                Rectangle destination=new Rectangle(Point.Empty,target);
                                RectangleF crop=new RectangleF(0,0,source.Width,source.Height);
                                if(!AutoFit && settings.ImageFitting=="Fill")crop=ImageLayout.Crop(source.Size,target);
                                else if(!AutoFit) {
                                    Size fit=ImageLayout.Fit(source.Size,target);
                                    destination=new Rectangle((target.Width-fit.Width)/2,(target.Height-fit.Height)/2,fit.Width,fit.Height);
                                }
                                using(var attrs=new ImageAttributes()) {
                                    attrs.SetWrapMode(WrapMode.TileFlipXY);
                                    g.DrawImage(source,destination,crop.X,crop.Y,crop.Width,crop.Height,GraphicsUnit.Pixel,attrs);
                                }
                            }
                        } catch {b.Dispose();throw;}
                        selected=path;return b;
                    }
                } catch(ArgumentException) {} catch(IOException) {} catch(OutOfMemoryException) {} catch(UnauthorizedAccessException) {}
            }
            selected=null;return null;
        }
        // Sample only at decode time. Flatten transparent edges onto a neutral base,
        // then mute/lighten the average so dark photographs never create black mats.
        static Color MatteColor(Image source) {
            const int n=16, neutral=232;
            using(var sample=new Bitmap(n,n,PixelFormat.Format32bppPArgb)) {
                using(var g=Graphics.FromImage(sample)) {
                    g.Clear(Color.FromArgb(neutral,neutral,neutral));
                    g.InterpolationMode=InterpolationMode.HighQualityBilinear;
                    using(var attrs=new ImageAttributes()) {
                        attrs.SetWrapMode(WrapMode.TileFlipXY);
                        g.DrawImage(source,new Rectangle(0,0,n,n),0,0,source.Width,source.Height,GraphicsUnit.Pixel,attrs);
                    }
                }
                int r=0,gc=0,b=0,count=0;
                for(int y=0;y<n;y++)for(int x=0;x<n;x++) {
                    if(x!=0 && y!=0 && x!=n-1 && y!=n-1)continue;
                    Color c=sample.GetPixel(x,y);r+=c.R;gc+=c.G;b+=c.B;count++;
                }
                return Color.FromArgb((r/count+3*neutral)/4,(gc/count+3*neutral)/4,(b/count+3*neutral)/4);
            }
        }
        Size ViewportSize() {
            if(!AutoFit || next==null || fadeStart<0)return current.Size;
            return new Size(Math.Max(1,(int)Math.Round(current.Width+(next.Width-current.Width)*transitionProgress)),
                Math.Max(1,(int)Math.Round(current.Height+(next.Height-current.Height)*transitionProgress)));
        }
        void PresentFrame() {
            if(current==null)return;
            Size size=ViewportSize();
            Rectangle bounds=ImageLayout.Place(size,workArea.Width>0 ? workArea : Screen.PrimaryScreen.WorkingArea,anchor,settings.Corner);
            surface.Reserve(size);
            using(var g=surface.CreateGraphics())PaintScene(g,size);
            byte alpha=(byte)(settings.Opacity*255);
            surface.Present(Handle,bounds,alpha);
            // UpdateLayeredWindow changes native geometry without updating WinForms cached bounds.
            // Synchronize managed metadata only; do not issue a second native resize.
            UpdateBounds(bounds.X,bounds.Y,bounds.Width,bounds.Height);
            presentedAlpha=alpha;
        }
        void DisplayChanged(object sender,EventArgs e) {
            if(!IsDisposed && IsHandleCreated)BeginInvoke(new Action(delegate {
                if(IsDisposed)return;
                var p=settings.Copy();p.AnchorX=anchor.X;p.AnchorY=anchor.Y;
                Configure(p); // Invisible rebuild bounds both cached images to the new work area.
            }));
        }
        void MarkUnavailable() {
            Hide();
            if(!unavailable) {unavailable=true;if(SourceUnavailable!=null)SourceUnavailable();if(StateChanged!=null)StateChanged();}
            retryAt=Environment.TickCount;
        }
        void Tick(object sender,EventArgs e) {
            // Source recovery remains active while the slideshow is paused.
            if(current==null) {
                if(unchecked(Environment.TickCount-(int)retryAt)<10000)return;
                retryAt=Environment.TickCount;
                var paths=ImageSource.Find(settings);canvasSize=AutoFit ? Size.Empty : DetermineCanvas(paths);current=ReadNext(paths,0,out currentPath);
                if(current!=null) {unavailable=false;PresentFrame();if(!hidden)ShowQuietly();nextChange=clock.Elapsed.TotalSeconds+settings.IntervalSeconds;Invalidate();if(StateChanged!=null)StateChanged();}
                return;
            }
            if(Paused)return;
            double now=clock.Elapsed.TotalSeconds;
            if(fadeStart>=0) {
                AdvanceTransition(now);
                return;
            }
            if(now<nextChange)return;
            nextChange=now+settings.IntervalSeconds;
            var list=ImageSource.Find(settings);
            int i=Array.FindIndex(list,p=>String.Equals(p,currentPath,StringComparison.OrdinalIgnoreCase));
            next=ReadNext(list,list.Length==0 ? 0 : (i+1)%list.Length,out nextPath);
            if(next==null) {DisposeImages();MarkUnavailable();return;}
            if(!settings.Crossfade) {CompleteTransition();return;}
            fadeStart=clock.Elapsed.TotalSeconds;transitionProgress=0;
            if(AutoFit)surface.Reserve(new Size(Math.Max(current.Width,next.Width),Math.Max(current.Height,next.Height)));
            timer.Interval=AutoFit ? 16 : 33;PresentFrame();
        }
        void SwapImage() {
            if(next==null)return;
            if(current!=null)current.Dispose();current=next;next=null;currentPath=nextPath;
            PresentFrame();
        }
        void AdvanceTransition(double now) {
            double elapsed=Math.Max(0,now-fadeStart);
            if(elapsed>=(AutoFit ? AutoFadeSeconds : 1.5)) {CompleteTransition();return;}
            transitionProgress=SmoothStep(elapsed/AutoFadeSeconds);
            PresentFrame();
        }
        static float SmoothStep(double value) {
            double t=Math.Max(0,Math.Min(1,value));return (float)(t*t*(3-2*t));
        }
        void CompleteTransition() {
            transitionProgress=0;
            SwapImage();
            fadeStart=-1;Rotations++;timer.Interval=250;
        }
        static void DrawImage(Graphics g,Bitmap image,float alpha) {
            if(alpha<=0)return;
            if(alpha>=1) {g.DrawImageUnscaled(image,0,0);return;}
            using(var attrs=new ImageAttributes()) {
                var matrix=new ColorMatrix();matrix.Matrix33=alpha;attrs.SetColorMatrix(matrix);
                g.DrawImage(image,new Rectangle(0,0,image.Width,image.Height),0,0,image.Width,image.Height,GraphicsUnit.Pixel,attrs);
            }
        }
        static void DrawCover(Graphics g,Bitmap image,Size viewport,float alpha) {
            if(alpha<=0)return;
            RectangleF crop=ImageLayout.Crop(image.Size,viewport);
            g.InterpolationMode=InterpolationMode.HighQualityBicubic;
            g.PixelOffsetMode=PixelOffsetMode.HighQuality;
            using(var attributes=new ImageAttributes()) {
                attributes.SetWrapMode(WrapMode.TileFlipXY);
                var matrix=new ColorMatrix();matrix.Matrix33=alpha;attributes.SetColorMatrix(matrix);
                g.DrawImage(image,new Rectangle(Point.Empty,viewport),crop.X,crop.Y,crop.Width,crop.Height,GraphicsUnit.Pixel,attributes);
            }
        }
        void PaintScene(Graphics g,Size viewport) {
            g.SetClip(new Rectangle(Point.Empty,viewport));
            g.Clear(BackColor);
            if(current==null)return;
            if(AutoFit && next!=null && fadeStart>=0) {
                DrawCover(g,current,viewport,1);
                DrawCover(g,next,viewport,transitionProgress);
            } else {
                DrawImage(g,current,1);
                if(next!=null && !AutoFit) {
                    float alpha=(float)Math.Max(0,Math.Min(1,(clock.Elapsed.TotalSeconds-fadeStart)/1.5));
                    DrawImage(g,next,alpha);
                }
            }
        }
        protected override void OnPaint(PaintEventArgs e) {
            // WM_PAINT is retained for offscreen rendering only. Desktop presentation
            // is one UpdateLayeredWindow call containing geometry and complete pixels.
            base.OnPaint(e);PaintScene(e.Graphics,ClientSize);
        }
        protected override void WndProc(ref Message m) {
            if(m.Msg==0x24) {
                base.WndProc(ref m);
                // MINMAXINFO.ptMinTrackSize: the default minimum can become 2px at 150% DPI.
                // A borderless photo may legitimately be only one physical pixel wide/tall.
                System.Runtime.InteropServices.Marshal.WriteInt32(m.LParam,24,1);
                System.Runtime.InteropServices.Marshal.WriteInt32(m.LParam,28,1);return;
            }
            if(m.Msg==0x21) {m.Result=new IntPtr(3);return;}
            if(m.Msg==0x84 && settings.ClickThrough) {m.Result=new IntPtr(-1);return;}
            if(m.Msg==0x312) {
                int id=m.WParam.ToInt32();if(id==1)Toggle();
                if(id>=10 && id<=13 && HasImage) {
                    MoveFrame(id==10 ? -20 : id==12 ? 20 : 0,id==11 ? -20 : id==13 ? 20 : 0);
                }return;
            }
            base.WndProc(ref m);
        }
        void DisposeImages() {transitionProgress=0;if(current!=null)current.Dispose();if(next!=null)next.Dispose();current=null;next=null;fadeStart=-1;}
        protected override void Dispose(bool disposing) {
            if(disposing) {
                timer.Dispose();Microsoft.Win32.SystemEvents.DisplaySettingsChanged-=DisplayChanged;
                if(IsHandleCreated) {Native.UnregisterHotKey(Handle,1);Native.UnregisterHotKey(Handle,2);for(int i=0;i<4;i++)Native.UnregisterHotKey(Handle,10+i);}
                DisposeImages();surface.Dispose();
            }base.Dispose(disposing);
        }
    }
}








