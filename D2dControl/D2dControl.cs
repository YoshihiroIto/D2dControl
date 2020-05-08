using SharpDX.Direct2D1;
using SharpDX.Direct3D;
using SharpDX.Direct3D11;
using SharpDX.DXGI;
using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Threading;
using SharpDX;
using Microsoft.Win32;

namespace D2dControl
{
    public abstract class D2dControl : System.Windows.Controls.Image
    {
        protected readonly ResourceCache ResourceCache = new ResourceCache();

        private static SharpDX.Direct3D11.Device Device =>
            LazyInitializer.EnsureInitialized(ref _device, () =>
            {
                var device = new SharpDX.Direct3D11.Device(DriverType.Hardware, DeviceCreationFlags.BgraSupport);
                Dx11ImageSource.Initialize();
                return device;
            });

        private static SharpDX.Direct3D11.Device? _device;

        private Texture2D? _sharedTarget;
        private Texture2D? _dx11Target;
        private Dx11ImageSource? _d3DSurface;
        private SharpDX.Direct2D1.DeviceContext? _d2DRenderTarget;

        private bool IsInDesignMode
        {
            get
            {
                if (_IsInDesignMode.HasValue == false)
                    _IsInDesignMode = DesignerProperties.GetIsInDesignMode(this);

                return _IsInDesignMode != null && _IsInDesignMode.Value;
            }
        }

        private bool? _IsInDesignMode;

        private bool _isRequestUpdate = true;

        #region IsAutoFrameUpdate

        public bool IsAutoFrameUpdate
        {
            get => _IsAutoFrameUpdate;
            set
            {
                if (value != _IsAutoFrameUpdate)
                    SetValue(IsAutoFrameUpdateProperty, value);
            }
        }

        private bool _IsAutoFrameUpdate = true;

        public static readonly DependencyProperty IsAutoFrameUpdateProperty =
            DependencyProperty.Register(
                nameof(IsAutoFrameUpdate),
                typeof(bool),
                typeof(D2dControl),
                new PropertyMetadata(
                    true,
                    (s, e) =>
                    {
                        var self = (D2dControl) s;
                        self._IsAutoFrameUpdate = (bool) e.NewValue;
                    }));

        #endregion

        internal static bool IsSoftwareRenderingMode { get; private set; }

        public static void Initialize()
        {
            if (_device != null)
                return;

            MakeIsSoftwareRenderingMode();

            _device = new SharpDX.Direct3D11.Device(DriverType.Hardware, DeviceCreationFlags.BgraSupport);

            Dx11ImageSource.Initialize();
        }

        public static void Destroy()
        {
            if (_device == null)
                return;

            Dx11ImageSource.Destroy();

            Disposer.SafeDispose(ref _device);
        }

        public void Invalidate()
        {
            if (IsAutoFrameUpdate)
                return;

            _isRequestUpdate = true;
        }

        protected D2dControl()
        {
            Loaded += OnLoaded;

            Stretch = Stretch.Fill;
        }

        public abstract void Render(SharpDX.Direct2D1.DeviceContext target);

        private bool _isInitialized;

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            if (IsInDesignMode)
                return;

            if (_isInitialized)
                return;

            _isInitialized = true;

            StartD3D();
            StartRendering();

            // event
            {
                SystemEvents.SessionSwitch += SystemEventsOnSessionSwitch;
                Unloaded += OnUnloaded;

                _parentWindow = GetParent<Window>();
                if (_parentWindow != null)
                    _parentWindow.Closed += OnUnloaded;

                _parentPopup = GetParent<Popup>();
                if (_parentPopup != null)
                    _parentPopup.Closed += OnUnloaded;
            }
        }

        private Window? _parentWindow;
        private Popup? _parentPopup;

        private void OnUnloaded(object? sender, EventArgs e)
        {
            if (IsInDesignMode)
                return;

            if (_isInitialized == false)
                return;

            _isInitialized = false;

            Shutdown();

            // event
            {
                SystemEvents.SessionSwitch -= SystemEventsOnSessionSwitch;
                Unloaded -= OnUnloaded;

                if (_parentWindow != null)
                    _parentWindow.Closed -= OnUnloaded;

                if (_parentPopup != null)
                    _parentPopup.Closed -= OnUnloaded;
            }
        }

        private void SystemEventsOnSessionSwitch(object sender, SessionSwitchEventArgs e)
        {
            // アンロック以降、描画されない。
            // デバイスロスト時の振る舞いに似ているが、デバイスロストとしては検知されないため明示的に再描画している
            if (e.Reason == SessionSwitchReason.SessionUnlock)
            {
                var timer = new DispatcherTimer(DispatcherPriority.ApplicationIdle)
                {
                    Interval = TimeSpan.FromMilliseconds(1000)
                };

                timer.Tick += (_, __) =>
                {
                    CreateAndBindTargets();
                    Invalidate();

                    timer.Stop();
                };

                timer.Start();
            }
        }

        protected void Shutdown()
        {
            StopRendering();
            EndD3D();
        }

        private void InvalidateInternal()
        {
            if (_d3DSurface == null)
                return;

            try
            {
                PrepareAndCallRender();

                _d3DSurface.Lock();

                Device.ImmediateContext.ResolveSubresource(_dx11Target, 0, _sharedTarget, 0, Format.B8G8R8A8_UNorm);
                _d3DSurface.InvalidateD3DImage();

                _d3DSurface.Unlock();

                Device.ImmediateContext.Flush();
            }
            catch (SharpDXException ex)
            {
                if (ex.ResultCode == SharpDX.DXGI.ResultCode.DeviceRemoved ||
                    ex.ResultCode == SharpDX.DXGI.ResultCode.DeviceReset)
                {
                    CreateAndBindTargets();
                    Invalidate();
                }
            }
        }

        private void OnRendering(object? sender, EventArgs e)
        {
            if (IsAutoFrameUpdate == false &&
                _isRequestUpdate == false)
                return;

            _isRequestUpdate = false;

            InvalidateInternal();
        }

        protected override void OnRenderSizeChanged(SizeChangedInfo sizeInfo)
        {
            CreateAndBindTargets();

            Invalidate();

            base.OnRenderSizeChanged(sizeInfo);
        }

        private void OnIsFrontBufferAvailableChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            // https://github.com/Sascha-L/WPF-MediaKit/issues/3
            if (IsSoftwareRenderingMode)
                return;

            if (_d3DSurface == null)
                return;

            if (_d3DSurface.IsFrontBufferAvailable)
                StartRendering();
            else
                StopRendering();
        }

        private void StartD3D()
        {
            _d3DSurface = new Dx11ImageSource();
            _d3DSurface.IsFrontBufferAvailableChanged += OnIsFrontBufferAvailableChanged;

            CreateAndBindTargets();

            Source = _d3DSurface;
        }

        private void EndD3D()
        {
            if (_d3DSurface != null)
                _d3DSurface.IsFrontBufferAvailableChanged -= OnIsFrontBufferAvailableChanged;

            Source = null;

            Disposer.SafeDispose(ref _d2DRenderTarget);
            Disposer.SafeDispose(ref _d3DSurface);
            Disposer.SafeDispose(ref _sharedTarget);
            Disposer.SafeDispose(ref _dx11Target);
        }

        private void CreateAndBindTargets()
        {
            if (_d3DSurface == null)
                return;

            _d3DSurface.SetRenderTarget(null);

            Disposer.SafeDispose(ref _d2DRenderTarget);
            Disposer.SafeDispose(ref _sharedTarget);
            Disposer.SafeDispose(ref _dx11Target);

            var width = Math.Max((int) ActualWidth, 100);
            var height = Math.Max((int) ActualHeight, 100);

            var frontDesc = new Texture2DDescription
            {
                BindFlags = BindFlags.RenderTarget | BindFlags.ShaderResource,
                Format = Format.B8G8R8A8_UNorm,
                Width = width,
                Height = height,
                MipLevels = 1,
                SampleDescription = new SampleDescription(1, 0),
                Usage = ResourceUsage.Default,
                OptionFlags = ResourceOptionFlags.Shared,
                CpuAccessFlags = CpuAccessFlags.None,
                ArraySize = 1
            };

            var backDesc = new Texture2DDescription
            {
                BindFlags = BindFlags.RenderTarget | BindFlags.ShaderResource,
                Format = Format.B8G8R8A8_UNorm,
                Width = width,
                Height = height,
                MipLevels = 1,
                SampleDescription = new SampleDescription(1, 0),
                Usage = ResourceUsage.Default,
                OptionFlags = ResourceOptionFlags.None,
                CpuAccessFlags = CpuAccessFlags.None,
                ArraySize = 1
            };

            _sharedTarget = new Texture2D(Device, frontDesc);
            _dx11Target = new Texture2D(Device, backDesc);

            using (var surface = _dx11Target.QueryInterface<Surface>())
            {
                _d2DRenderTarget = new SharpDX.Direct2D1.DeviceContext(surface, new CreationProperties()
                {
                    Options = DeviceContextOptions.EnableMultithreadedOptimizations,
                    ThreadingMode = ThreadingMode.SingleThreaded
                });
            }

            ResourceCache.RenderTarget = _d2DRenderTarget;

            _d3DSurface.SetRenderTarget(_sharedTarget);

            Device.ImmediateContext.Rasterizer.SetViewport(0, 0, width, height);
        }

        private void StartRendering()
        {
            CompositionTarget.Rendering += OnRendering;
        }

        private void StopRendering()
        {
            CompositionTarget.Rendering -= OnRendering;
        }

        private void PrepareAndCallRender()
        {
            if (_d2DRenderTarget == null)
                return;

            _d2DRenderTarget.BeginDraw();

            Render(_d2DRenderTarget);

            _d2DRenderTarget.EndDraw();
        }

        private T? GetParent<T>() where T : class
        {
            var parent = this as DependencyObject;

            do
            {
                if (parent is T tp)
                    return tp;

                parent = VisualTreeHelper.GetParent(parent);
            } while (parent != null);

            return null;
        }

        private static void MakeIsSoftwareRenderingMode()
        {
            // https://stackoverflow.com/questions/56849171/direct2d-with-wpf-over-rdp
            // https://github.com/Sascha-L/WPF-MediaKit/issues/3
            // https://docs.microsoft.com/en-us/archive/blogs/wpf3d/d3dimage-and-software-rendering
            // https://docs.microsoft.com/en-us/dotnet/framework/wpf/graphics-multimedia/graphics-rendering-registry-settings

            // Rendering tier 
            var renderingTier = RenderCapability.Tier >> 16;
            if (renderingTier == 0)
            {
                IsSoftwareRenderingMode = true;
                return;
            }

            // Remote desktop
            if (GetSystemMetrics(SM_REMOTESESSION) != 0)
            {
                IsSoftwareRenderingMode = true;
                return;
            }

            // DisableHWAcceleration
            try
            {
                var subKey = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Avalon.Graphics");

                var d = (int) subKey.GetValue("DisableHWAcceleration");
                if (d != 0)
                {
                    IsSoftwareRenderingMode = true;
                    // ReSharper disable once RedundantJumpStatement
                    return;
                }
            }
            catch
            {
                // ignored
            }
        }

        // ReSharper disable once InconsistentNaming
        // ReSharper disable once IdentifierTypo
        private const int SM_REMOTESESSION = 0x1000;

        [DllImport("user32")]
        private static extern int GetSystemMetrics(int index);
    }
}