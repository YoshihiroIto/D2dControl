using SharpDX.Direct2D1;
using SharpDX.Mathematics.Interop;
using System;

namespace Sample
{
    class SampleControl : D2dControl.D2dControl
    {
        private float x;
        private float y;
        private readonly float w = 10;
        private readonly float h = 10;
        private float dx = 1;
        private float dy = 1;

        private readonly Random rnd = new Random();

        public SampleControl()
        {
            ResourceCache.Add("RedBrush".GetHashCode(), t => new SolidColorBrush(t, new RawColor4(1.0f, 0.0f, 0.0f, 1.0f)));
            ResourceCache.Add("GreenBrush".GetHashCode(), t => new SolidColorBrush(t, new RawColor4(0.0f, 1.0f, 0.0f, 1.0f)));
            ResourceCache.Add("BlueBrush".GetHashCode(), t => new SolidColorBrush(t, new RawColor4(0.0f, 0.0f, 1.0f, 1.0f)));
        }

        protected override void Render(DeviceContext target)
        {
            target.Clear(new RawColor4(1.0f, 1.0f, 1.0f, 1.0f));
            var brush = rnd.Next(3) switch
            {
                0 => ResourceCache["RedBrush".GetHashCode()] as Brush,
                1 => ResourceCache["GreenBrush".GetHashCode()] as Brush,
                2 => ResourceCache["BlueBrush".GetHashCode()] as Brush,
                _ => null
            };

            target.DrawRectangle(new RawRectangleF(x, y, x + w, y + h), brush);

            x += dx;
            y += dy;
            if (x >= ActualWidth - w || x <= 0)
            {
                dx = -dx;
            }

            if (y >= ActualHeight - h || y <= 0)
            {
                dy = -dy;
            }
        }
    }
}