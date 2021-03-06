﻿using SharpDX.Direct3D9;
using System;
using System.Windows.Interop;
using System.Runtime.InteropServices;

namespace D2dControl
{
    internal class Dx11ImageSource : D3DImage, IDisposable
    {
        private Direct3DEx? _d3DContext;
        private DeviceEx? _d3DDevice;
        private Texture? _renderTarget;

        internal Dx11ImageSource()
        {
            var presentParams = GetPresentParameters();
            const CreateFlags createFlags = CreateFlags.HardwareVertexProcessing | CreateFlags.Multithreaded | CreateFlags.FpuPreserve;
            
            _d3DContext = new Direct3DEx();
            _d3DDevice = new DeviceEx(_d3DContext, 0, DeviceType.Hardware, IntPtr.Zero, createFlags, presentParams);
            
            StartD3D();
        }

        public void Dispose()
        {
            SetRenderTarget(null);

            Disposer.SafeDispose(ref _renderTarget);

            EndD3D();
            
            Disposer.SafeDispose(ref _d3DDevice);
            Disposer.SafeDispose(ref _d3DContext);
        }

        internal void InvalidateD3DImage()
        {
            if (_renderTarget != null)
                AddDirtyRect(new System.Windows.Int32Rect(0, 0, PixelWidth, PixelHeight));
        }

        internal void SetRenderTarget(SharpDX.Direct3D11.Texture2D? target)
        {
            if (_renderTarget != null)
            {
                try
                {
                    Lock();
                    SetBackBuffer(D3DResourceType.IDirect3DSurface9, IntPtr.Zero);
                }
                finally
                {
                    Unlock();
                }

                Disposer.SafeDispose(ref _renderTarget);
            }

            if (target is null)
                return;

            var format = TranslateFormat(target);
            var handle = GetSharedHandle(target);

            if (IsShareable(target) == false)
                throw new ArgumentException("Texture must be created with ResourceOptionFlags.Shared");

            if (format == Format.Unknown)
                throw new ArgumentException("Texture format is not compatible with OpenSharedResource");

            if (handle == IntPtr.Zero)
                throw new ArgumentException("Invalid handle");

            _renderTarget = new Texture(_d3DDevice, target.Description.Width, target.Description.Height, 1,
                Usage.RenderTarget, format, Pool.Default, ref handle);

            using var surface = _renderTarget.GetSurfaceLevel(0);

            try
            {
                Lock();
                SetBackBuffer(D3DResourceType.IDirect3DSurface9, surface.NativePointer, D2dControl.IsSoftwareRenderingMode);
            }
            finally
            {
                Unlock();
            }
        }

        private void StartD3D()
        {
            // do nothing
        }

        private void EndD3D()
        {
            Disposer.SafeDispose(ref _renderTarget);
        }

        private static PresentParameters GetPresentParameters()
        {
            var presentParams = new PresentParameters
            {
                Windowed = true,
                SwapEffect = SwapEffect.Discard,
                DeviceWindowHandle = GetDesktopWindow(),
                PresentationInterval = PresentInterval.Default
            };

            return presentParams;
        }

        private static IntPtr GetSharedHandle(SharpDX.Direct3D11.Texture2D texture)
        {
            using var resource = texture.QueryInterface<SharpDX.DXGI.Resource>();

            return resource.SharedHandle;
        }

        private static Format TranslateFormat(SharpDX.Direct3D11.Texture2D texture)
        {
            return texture.Description.Format switch
            {
                SharpDX.DXGI.Format.R10G10B10A2_UNorm => Format.A2B10G10R10,
                SharpDX.DXGI.Format.R16G16B16A16_Float => Format.A16B16G16R16F,
                SharpDX.DXGI.Format.B8G8R8A8_UNorm => Format.A8R8G8B8,
                _ => Format.Unknown
            };
        }

        private static bool IsShareable(SharpDX.Direct3D11.Texture2D texture)
        {
            return (texture.Description.OptionFlags & SharpDX.Direct3D11.ResourceOptionFlags.Shared) != 0;
        }

        [DllImport("user32.dll", SetLastError = false)]
        private static extern IntPtr GetDesktopWindow();
    }
}