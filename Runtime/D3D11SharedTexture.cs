using System;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.Rendering;

namespace Ceffy
{
    /// <summary>
    /// Creates Unity textures from D3D11 shared texture handles (HANDLE from CreateSharedHandle).
    /// Supports both D3D11 and D3D12 Unity graphics backends via zero-copy texture sharing.
    /// </summary>
    public static class D3D11SharedTexture
    {
        private static readonly Guid IID_ID3D11Texture2D = new Guid("6f15aaf2-d208-4e89-9ab4-489535d34f9c");
        private static readonly Guid IID_ID3D12Resource = new Guid("696442be-a72e-4059-bc79-5b5c98040fad");

        /// <summary>
        /// Creates a Unity external texture from a native D3D shared-texture handle.
        /// </summary>
        /// <returns>The external texture, or null when the handle or graphics API is unsupported.</returns>
        public static Texture2D CreateFromSharedHandle(IntPtr sharedHandle, int width, int height)
        {
            if (sharedHandle == IntPtr.Zero)
            {
                Debug.LogError("D3D11SharedTexture: Invalid shared handle (null)");
                return null;
            }

            var gfxType = SystemInfo.graphicsDeviceType;
            if (WebBrowserRuntime.VerboseLogging)
                Debug.Log($"D3D11SharedTexture: Unity graphics API: {gfxType}");

            try
            {
                if (gfxType == GraphicsDeviceType.Direct3D12)
                    return CreateD3D12(sharedHandle, width, height);
                if (gfxType == GraphicsDeviceType.Direct3D11)
                    return CreateD3D11(sharedHandle, width, height);

                Debug.LogError($"D3D11SharedTexture: Unsupported graphics API: {gfxType}. Only D3D11 and D3D12 are supported.");
                return null;
            }
            catch (Exception e)
            {
                Debug.LogError($"D3D11SharedTexture: Exception creating shared texture: {e}");
                return null;
            }
        }

        #region D3D12

        private static Texture2D CreateD3D12(IntPtr sharedHandle, int width, int height)
        {
            IntPtr d3d12Device = GetD3D12Device();
            if (d3d12Device == IntPtr.Zero)
            {
                Debug.LogError("D3D11SharedTexture: Failed to get Unity's D3D12 device");
                return null;
            }

            IntPtr resource = OpenSharedHandleD3D12(d3d12Device, sharedHandle);
            if (resource == IntPtr.Zero)
            {
                Debug.LogError("D3D11SharedTexture: Failed to open shared handle in D3D12");
                return null;
            }

            var texture = Texture2D.CreateExternalTexture(width, height, TextureFormat.BGRA32, false, true, resource);
            if (texture == null)
            {
                Debug.LogError("D3D11SharedTexture: Failed to create external texture from D3D12 resource");
                Marshal.Release(resource);
                return null;
            }

            if (WebBrowserRuntime.VerboseLogging)
                Debug.Log($"D3D11SharedTexture: Created D3D12 shared texture {width}x{height}");
            return texture;
        }

        private static IntPtr GetD3D12Device()
        {
            var tempTex = new Texture2D(4, 4, TextureFormat.RGBA32, false);
            tempTex.Apply();
            IntPtr texPtr = tempTex.GetNativeTexturePtr();

            if (texPtr == IntPtr.Zero)
            {
                UnityEngine.Object.Destroy(tempTex);
                return IntPtr.Zero;
            }

            // ID3D12DeviceChild::GetDevice is at vtable index 7
            IntPtr device = IntPtr.Zero;
            try
            {
                IntPtr vtable = Marshal.ReadIntPtr(texPtr);
                IntPtr getDevicePtr = Marshal.ReadIntPtr(vtable, 7 * IntPtr.Size);
                var getDevice = Marshal.GetDelegateForFunctionPointer<GetDeviceD3D12Delegate>(getDevicePtr);
                Guid iid = new Guid("189819f1-1db6-4b57-be54-1821339b85f7"); // IID_ID3D12Device
                int hr = getDevice(texPtr, ref iid, out device);
                if (hr < 0) device = IntPtr.Zero;
            }
            catch (Exception e)
            {
                Debug.LogError($"D3D11SharedTexture: Exception getting D3D12 device: {e}");
            }

            UnityEngine.Object.Destroy(tempTex);
            return device;
        }

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate int GetDeviceD3D12Delegate(IntPtr pThis, ref Guid riid, out IntPtr ppDevice);

        // ID3D12Device::OpenSharedHandle is at vtable index 32
        private static IntPtr OpenSharedHandleD3D12(IntPtr device, IntPtr sharedHandle)
        {
            try
            {
                IntPtr vtable = Marshal.ReadIntPtr(device);
                IntPtr funcPtr = Marshal.ReadIntPtr(vtable, 32 * IntPtr.Size);
                var func = Marshal.GetDelegateForFunctionPointer<OpenSharedHandleD3D12Delegate>(funcPtr);

                Guid iid = IID_ID3D12Resource;
                int hr = func(device, sharedHandle, ref iid, out IntPtr resource);
                if (hr < 0)
                {
                    Debug.LogError($"D3D11SharedTexture: D3D12 OpenSharedHandle failed (HRESULT 0x{hr:X8})");
                    return IntPtr.Zero;
                }
                return resource;
            }
            catch (Exception e)
            {
                Debug.LogError($"D3D11SharedTexture: Exception in D3D12 OpenSharedHandle: {e}");
                return IntPtr.Zero;
            }
        }

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate int OpenSharedHandleD3D12Delegate(IntPtr pThis, IntPtr ntHandle, ref Guid riid, out IntPtr ppvObj);

        #endregion

        #region D3D11

        private static Texture2D CreateD3D11(IntPtr sharedHandle, int width, int height)
        {
            IntPtr device = GetD3D11Device();
            if (device == IntPtr.Zero)
            {
                Debug.LogError("D3D11SharedTexture: Failed to get Unity's D3D11 device");
                return null;
            }

            IntPtr sharedTexture = OpenSharedD3D11Resource(device, sharedHandle);
            IntPtr srv = CreateShaderResourceView(device, sharedTexture);
            if (srv == IntPtr.Zero)
            {
                Debug.LogError("D3D11SharedTexture: Failed to create shader resource view");
                Marshal.Release(sharedTexture);
                return null;
            }

            var texture = Texture2D.CreateExternalTexture(width, height, TextureFormat.BGRA32, false, true, srv);
            if (WebBrowserRuntime.VerboseLogging)
                Debug.Log($"D3D11SharedTexture: Created D3D11 shared texture {width}x{height}");
            return texture;
        }

        private static IntPtr GetD3D11Device()
        {
            var tempTex = new Texture2D(4, 4, TextureFormat.RGBA32, false);
            tempTex.Apply();
            IntPtr texPtr = tempTex.GetNativeTexturePtr();

            if (texPtr == IntPtr.Zero)
            {
                UnityEngine.Object.Destroy(tempTex);
                return IntPtr.Zero;
            }

            // ID3D11DeviceChild::GetDevice is at vtable index 3
            IntPtr device = IntPtr.Zero;
            try
            {
                IntPtr vtable = Marshal.ReadIntPtr(texPtr);
                IntPtr getDevicePtr = Marshal.ReadIntPtr(vtable, 3 * IntPtr.Size);
                var getDevice = Marshal.GetDelegateForFunctionPointer<GetDeviceD3D11Delegate>(getDevicePtr);
                getDevice(texPtr, out device);
            }
            catch
            {
                device = IntPtr.Zero;
            }

            UnityEngine.Object.Destroy(tempTex);
            return device;
        }

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate void GetDeviceD3D11Delegate(IntPtr pThis, out IntPtr ppDevice);

        // ID3D11Device::OpenSharedResource is at vtable index 28
        private static IntPtr OpenSharedD3D11Resource(IntPtr device, IntPtr sharedHandle)
        {
            try
            {
                IntPtr vtable = Marshal.ReadIntPtr(device);
                IntPtr funcPtr = Marshal.ReadIntPtr(vtable, 28 * IntPtr.Size);
                var func = Marshal.GetDelegateForFunctionPointer<OpenSharedResourceD3D11Delegate>(funcPtr);

                Guid iid = IID_ID3D11Texture2D;
                int hr = func(device, sharedHandle, ref iid, out IntPtr resource);
                if (hr < 0)
                {
                    Debug.LogError($"D3D11SharedTexture: D3D11 OpenSharedResource failed (HRESULT 0x{hr:X8})");
                    return IntPtr.Zero;
                }
                return resource;
            }
            catch (Exception e)
            {
                Debug.LogError($"D3D11SharedTexture: Exception in D3D11 OpenSharedResource: {e}");
                return IntPtr.Zero;
            }
        }

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate int OpenSharedResourceD3D11Delegate(IntPtr pThis, IntPtr hResource, ref Guid riid, out IntPtr ppResource);

        // ID3D11Device::CreateShaderResourceView is at vtable index 7
        private static IntPtr CreateShaderResourceView(IntPtr device, IntPtr texture)
        {
            try
            {
                IntPtr vtable = Marshal.ReadIntPtr(device);
                IntPtr funcPtr = Marshal.ReadIntPtr(vtable, 7 * IntPtr.Size);
                var func = Marshal.GetDelegateForFunctionPointer<CreateSRVDelegate>(funcPtr);

                int hr = func(device, texture, IntPtr.Zero, out IntPtr srv);
                if (hr < 0)
                {
                    Debug.LogError($"D3D11SharedTexture: CreateShaderResourceView failed (HRESULT 0x{hr:X8})");
                    return IntPtr.Zero;
                }
                return srv;
            }
            catch (Exception e)
            {
                Debug.LogError($"D3D11SharedTexture: Exception in CreateShaderResourceView: {e}");
                return IntPtr.Zero;
            }
        }

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate int CreateSRVDelegate(IntPtr pThis, IntPtr pResource, IntPtr pDesc, out IntPtr ppSRView);

        #endregion
    }
}
