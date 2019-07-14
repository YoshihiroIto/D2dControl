using SharpDX;
using SharpDX.Direct2D1;
using SharpDX.Mathematics.Interop;
using System;

namespace Sample {
    class SampleControl : D2dControl.D2dControl {

        private float x = 0;
        private float y = 0;
        private float w = 10;
        private float h = 10;
        private float dx = 1;
        private float dy = 1;

        private Random rnd = new Random();

        public SampleControl() {
            ResCache.Add( "RedBrush"  , t => new SolidColorBrush( t, new RawColor4( 1.0f, 0.0f, 0.0f, 1.0f ) ) );
            ResCache.Add( "GreenBrush", t => new SolidColorBrush( t, new RawColor4( 0.0f, 1.0f, 0.0f, 1.0f ) ) );
            ResCache.Add( "BlueBrush" , t => new SolidColorBrush( t, new RawColor4( 0.0f, 0.0f, 1.0f, 1.0f ) ) );
        }

        public override void Render(DeviceContext target)
        {
            var renderTarget = ResCache.RenderTarget;

            renderTarget.Clear( new RawColor4( 1.0f, 1.0f, 1.0f, 1.0f ) );
            Brush brush = null;
            switch( rnd.Next( 3 ) ) {
                case 0: brush = ResCache["RedBrush"  ] as Brush; break;
                case 1: brush = ResCache["GreenBrush"] as Brush; break;
                case 2: brush = ResCache["BlueBrush" ] as Brush; break;
            }
            renderTarget.DrawRectangle( new RawRectangleF( x, y, x + w, y + h ), brush );

            x = x + dx;
            y = y + dy;
            if ( x >= ActualWidth - w || x <= 0 ) {
                dx = -dx;
            }
            if ( y >= ActualHeight - h || y <= 0 ) {
                dy = -dy;
            }
        }
    }
}
