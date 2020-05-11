using System;

namespace D2dControl
{
    internal static class Disposer
    {
        internal static void SafeDispose<T>(ref T? resource) where T : class
        {
            switch (resource)
            {
                case null:
                    return;
                
                case IDisposable disposer:
                    try
                    {
                        disposer.Dispose();
                    }
                    catch
                    {
                        // ignored
                    }

                    break;
            }

            resource = null;
        }
    }
}