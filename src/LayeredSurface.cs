using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
namespace DeskFrame {
    // Owns a reusable top-down premultiplied DIB. Geometry and pixels are committed
    // together, with no SetWindowPos resize, background erase, or alpha-mode switch.
    internal sealed class LayeredSurface : IDisposable {
        [StructLayout(LayoutKind.Sequential)] struct PointI {public int X,Y;public PointI(int x,int y){X=x;Y=y;}}
        [StructLayout(LayoutKind.Sequential)] struct SizeI {public int Width,Height;public SizeI(int w,int h){Width=w;Height=h;}}
        [StructLayout(LayoutKind.Sequential,Pack=1)] struct Blend {public byte Operation,Flags,Alpha,Format;}
        [StructLayout(LayoutKind.Sequential)] struct Header {
            public uint Size;public int Width,Height;public ushort Planes,BitCount;
            public uint Compression,SizeImage;public int XPels,YPels;public uint ColorsUsed,ColorsImportant;
        }
        [DllImport("gdi32.dll",SetLastError=true)] static extern IntPtr CreateCompatibleDC(IntPtr dc);
        [DllImport("gdi32.dll",SetLastError=true)] static extern IntPtr CreateDIBSection(IntPtr dc,ref Header header,uint usage,out IntPtr bits,IntPtr section,uint offset);
        [DllImport("gdi32.dll")] static extern IntPtr SelectObject(IntPtr dc,IntPtr obj);
        [DllImport("gdi32.dll")] static extern bool DeleteObject(IntPtr obj);
        [DllImport("gdi32.dll")] static extern bool DeleteDC(IntPtr dc);
        [DllImport("user32.dll",SetLastError=true)] static extern bool UpdateLayeredWindow(IntPtr hwnd,IntPtr screen,ref PointI position,ref SizeI size,IntPtr source,ref PointI origin,uint color,ref Blend blend,uint flags);
        IntPtr dc,bitmap,previous;Bitmap pixels;Size capacity;
        internal void Reserve(Size size) {
            if(pixels!=null && capacity.Width>=size.Width && capacity.Height>=size.Height)return;
            Size wanted=new Size(Math.Max(capacity.Width,size.Width),Math.Max(capacity.Height,size.Height));
            Dispose();
            try {
                dc=CreateCompatibleDC(IntPtr.Zero);if(dc==IntPtr.Zero)throw new System.ComponentModel.Win32Exception();
                var header=new Header{Size=40,Width=wanted.Width,Height=-wanted.Height,Planes=1,BitCount=32};
                IntPtr bits;bitmap=CreateDIBSection(dc,ref header,0,out bits,IntPtr.Zero,0);
                if(bitmap==IntPtr.Zero)throw new System.ComponentModel.Win32Exception();
                previous=SelectObject(dc,bitmap);
                if(previous==IntPtr.Zero || previous==new IntPtr(-1))throw new System.ComponentModel.Win32Exception();
                pixels=new Bitmap(wanted.Width,wanted.Height,wanted.Width*4,PixelFormat.Format32bppPArgb,bits);
                capacity=wanted;
            } catch {Dispose();throw;}
        }
        internal Graphics CreateGraphics() {return Graphics.FromImage(pixels);}
        internal void Present(IntPtr hwnd,Rectangle bounds,byte alpha) {
            var destination=new PointI(bounds.X,bounds.Y);var size=new SizeI(bounds.Width,bounds.Height);var origin=new PointI(0,0);
            var blend=new Blend{Operation=0,Flags=0,Alpha=alpha,Format=1};
            if(!UpdateLayeredWindow(hwnd,IntPtr.Zero,ref destination,ref size,dc,ref origin,0,ref blend,2))throw new System.ComponentModel.Win32Exception();
        }
        public void Dispose() {
            if(pixels!=null){pixels.Dispose();pixels=null;}
            if(dc!=IntPtr.Zero && previous!=IntPtr.Zero && previous!=new IntPtr(-1))SelectObject(dc,previous);
            if(bitmap!=IntPtr.Zero)DeleteObject(bitmap);
            if(dc!=IntPtr.Zero)DeleteDC(dc);
            dc=bitmap=previous=IntPtr.Zero;capacity=Size.Empty;
        }
    }
}
