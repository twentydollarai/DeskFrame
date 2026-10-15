using System;
using System.Drawing;
namespace DeskFrame {
    // Pure physical-pixel geometry shared by the viewer and regression tests.
    internal static class ImageLayout {
        internal static Size Fit(Size source,Size limit) {
            double scale=Math.Min((double)Math.Max(1,limit.Width)/Math.Max(1,source.Width),(double)Math.Max(1,limit.Height)/Math.Max(1,source.Height));
            return new Size(Math.Max(1,Math.Min(Math.Max(1,limit.Width),(int)Math.Round(source.Width*scale))),Math.Max(1,Math.Min(Math.Max(1,limit.Height),(int)Math.Round(source.Height*scale))));
        }
        internal static Rectangle Inset(Rectangle area,int margin) {
            int x=Math.Min(Math.Max(0,margin),Math.Max(0,(area.Width-1)/2)),y=Math.Min(Math.Max(0,margin),Math.Max(0,(area.Height-1)/2));
            return new Rectangle(area.X+x,area.Y+y,Math.Max(1,area.Width-2*x),Math.Max(1,area.Height-2*y));
        }
        internal static Point Corner(Rectangle area,string corner) {
            return new Point(corner.EndsWith("right")?area.Right:area.Left,corner.StartsWith("bottom")?area.Bottom:area.Top);
        }
        internal static Rectangle Place(Size size,Rectangle area,Point anchor,string corner) {
            long x=(long)anchor.X-(corner.EndsWith("right")?size.Width:0),y=(long)anchor.Y-(corner.StartsWith("bottom")?size.Height:0);
            return new Rectangle((int)Math.Max(area.Left,Math.Min((long)area.Right-size.Width,x)),(int)Math.Max(area.Top,Math.Min((long)area.Bottom-size.Height,y)),size.Width,size.Height);
        }
        internal static RectangleF Crop(Size source,Size target) {
            double scale=Math.Max((double)target.Width/source.Width,(double)target.Height/source.Height);
            float w=(float)(target.Width/scale),h=(float)(target.Height/scale);
            return new RectangleF((source.Width-w)/2,(source.Height-h)/2,w,h);
        }
    }
}
