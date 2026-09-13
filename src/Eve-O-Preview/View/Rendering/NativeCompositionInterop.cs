using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace EveOPreview.View.Rendering;

/// <summary>
/// Deliberately small Windows SDK ABI boundary (dcomp.h, dcompanimation.h, d2d1.h).
/// MSVC reverses overloaded virtual groups: animation precedes float in the actual
/// IDCompositionVisual/EffectGroup vtable. These slots were checked against SDK
/// 10.0.26100.0-generated x64 call sites. Every pointer owns one COM reference.
/// </summary>
internal static unsafe class NativeCompositionInterop
{
    internal static readonly Guid CompositionDeviceId = new("C37EA93A-E7AA-450D-B16F-9746CB0407F3");
    internal static readonly Guid DxgiDeviceId = new("54EC77FA-1377-44E6-8C32-88FD5F44C84C");
    internal static readonly Guid DxgiSurfaceId = new("CAFCB56C-6AC3-4889-BF47-9E23BBD260EC");
    internal static readonly Guid D2DFactoryId = new("06152247-6F50-465A-9245-118BFD3B6007");

    [DllImport("d3d11.dll", ExactSpelling = true)]
    internal static extern int D3D11CreateDevice(nint adapter, uint driverType, nint software, uint flags,
        nint featureLevels, uint levels, uint sdkVersion, out nint device, out uint featureLevel, out nint context);
    [DllImport("dcomp.dll", ExactSpelling = true)]
    internal static extern int DCompositionCreateDevice(nint dxgiDevice, in Guid iid, out nint device);
    [DllImport("d2d1.dll", ExactSpelling = true)]
    internal static extern int D2D1CreateFactory(uint factoryType, in Guid iid, nint options, out nint factory);

    internal static void Check(int hr) { if (hr < 0) Marshal.ThrowExceptionForHR(hr); }
    private static void** Table(nint value) => *(void***)value;
    internal static void Release(ref nint value)
    {
        nint pointer = value; value = 0;
        if (pointer != 0) ((delegate* unmanaged[Stdcall]<nint, uint>)Table(pointer)[2])(pointer);
    }
    internal static nint Query(nint value, Guid iid)
    {
        nint result = 0;
        Check(((delegate* unmanaged[Stdcall]<nint, Guid*, nint*, int>)Table(value)[0])(value, &iid, &result));
        return result;
    }
    internal static void Commit(nint device) => Check(((delegate* unmanaged[Stdcall]<nint, int>)Table(device)[3])(device));
    internal static void WaitForCommit(nint device) => Check(((delegate* unmanaged[Stdcall]<nint, int>)Table(device)[4])(device));
    // ID3D11DeviceVtbl::GetDeviceRemovedReason, SDK d3d11.h slot 39. This reads
    // device status; it neither submits rendering nor waits for GPU completion.
    internal static int GetDeviceRemovedReason(nint device) =>
        ((delegate* unmanaged[Stdcall]<nint, int>)Table(device)[39])(device);
    internal static nint CreateTarget(nint device, nint hwnd)
    {
        nint result = 0;
        Check(((delegate* unmanaged[Stdcall]<nint, nint, int, nint*, int>)Table(device)[6])(device, hwnd, 1, &result));
        return result;
    }
    internal static nint CreateVisual(nint device) => CreateObject(device, 7);
    internal static nint CreateEffect(nint device) => CreateObject(device, 23);
    internal static nint CreateAnimation(nint device) => CreateObject(device, 25);
    private static nint CreateObject(nint device, int slot)
    {
        nint result = 0;
        Check(((delegate* unmanaged[Stdcall]<nint, nint*, int>)Table(device)[slot])(device, &result));
        return result;
    }
    internal static void SetRoot(nint target, nint visual) => SetObject(target, 3, visual);
    internal static void SetContent(nint visual, nint content) => SetObject(visual, 15, content);
    internal static void SetEffect(nint visual, nint effect) => SetObject(visual, 10, effect);
    internal static void SetOffsetAnimation(nint visual, nint animation) => SetObject(visual, 3, animation);
    internal static void SetOpacityAnimation(nint effect, nint animation) => SetObject(effect, 3, animation);
    private static void SetObject(nint value, int slot, nint argument) =>
        Check(((delegate* unmanaged[Stdcall]<nint, nint, int>)Table(value)[slot])(value, argument));
    internal static void SetOffset(nint visual, float x) => SetFloat(visual, 4, x);
    internal static void SetOffsetY(nint visual, float y) => SetFloat(visual, 6, y);
    internal static void SetScaleAndOffset(nint visual, float width, float height, float x, float y)
    {
        var transform = new Matrix3x2(width, 0, 0, height, x, y);
        Check(((delegate* unmanaged[Stdcall]<nint, Matrix3x2*, int>)Table(visual)[8])(visual, &transform));
    }
    internal static void SetOpacity(nint effect, float opacity) => SetFloat(effect, 4, opacity);
    private static void SetFloat(nint value, int slot, float argument) =>
        Check(((delegate* unmanaged[Stdcall]<nint, float, int>)Table(value)[slot])(value, argument));
    internal static void AddVisual(nint parent, nint child) =>
        Check(((delegate* unmanaged[Stdcall]<nint, nint, int, nint, int>)Table(parent)[16])(parent, child, 1, 0));
    internal static void SetClip(nint visual, int width, int height)
    {
        var rect = new RectF(0, 0, width, height);
        Check(((delegate* unmanaged[Stdcall]<nint, RectF*, int>)Table(visual)[14])(visual, &rect));
    }
    internal static void AddCubic(nint animation, double begin, float constant, float linear = 0, float quadratic = 0, float cubic = 0) =>
        Check(((delegate* unmanaged[Stdcall]<nint, double, float, float, float, float, int>)Table(animation)[5])
            (animation, begin, constant, linear, quadratic, cubic));
    internal static void AddSinusoidal(nint animation, float bias, float amplitude, float frequency, float phase) =>
        Check(((delegate* unmanaged[Stdcall]<nint, double, float, float, float, float, int>)Table(animation)[6])
            (animation, 0, bias, amplitude, frequency, phase));
    internal static void EndAnimation(nint animation, double end, float value) =>
        Check(((delegate* unmanaged[Stdcall]<nint, double, float, int>)Table(animation)[8])(animation, end, value));

    internal static nint CreateSurface(nint device, int width, int height)
    {
        nint surface = 0;
        // DXGI_FORMAT_B8G8R8A8_UNORM = 87; DXGI_ALPHA_MODE_PREMULTIPLIED = 1.
        Check(((delegate* unmanaged[Stdcall]<nint, uint, uint, uint, uint, nint*, int>)Table(device)[8])
            (device, (uint)width, (uint)height, 87, 1, &surface));
        return surface;
    }

    internal static void Upload(nint surface, nint factory, Bitmap bitmap)
    {
        nint dxgiSurface = 0, target = 0, d2dBitmap = 0;
        bool drawingSurface = false, drawingTarget = false;
        BitmapData data = null;
        try
        {
            var iid = DxgiSurfaceId;
            Point offset;
            Check(((delegate* unmanaged[Stdcall]<nint, nint, Guid*, nint*, Point*, int>)Table(surface)[3])
                (surface, 0, &iid, &dxgiSurface, &offset));
            drawingSurface = true;
            var targetProperties = new RenderTargetProperties { Format = 87, AlphaMode = 1, DpiX = 96, DpiY = 96 };
            Check(((delegate* unmanaged[Stdcall]<nint, nint, RenderTargetProperties*, nint*, int>)Table(factory)[15])
                (factory, dxgiSurface, &targetProperties, &target));
            var properties = new BitmapProperties { Format = 87, AlphaMode = 1, DpiX = 96, DpiY = 96 };
            var size = new SizeU((uint)bitmap.Width, (uint)bitmap.Height);
            data = bitmap.LockBits(new Rectangle(0, 0, bitmap.Width, bitmap.Height), ImageLockMode.ReadOnly, PixelFormat.Format32bppPArgb);
            Check(((delegate* unmanaged[Stdcall]<nint, SizeU, nint, uint, BitmapProperties*, nint*, int>)Table(target)[4])
                (target, size, data.Scan0, (uint)data.Stride, &properties, &d2dBitmap));
            bitmap.UnlockBits(data); data = null;
            ((delegate* unmanaged[Stdcall]<nint, void>)Table(target)[48])(target);
            drawingTarget = true;
            var clip = new RectF(offset.X, offset.Y, offset.X + bitmap.Width, offset.Y + bitmap.Height);
            // BeginDraw may return an atlas offset. Never clear another surface's region.
            ((delegate* unmanaged[Stdcall]<nint, RectF*, uint, void>)Table(target)[45])(target, &clip, 1);
            ColorF clear = default;
            ((delegate* unmanaged[Stdcall]<nint, ColorF*, void>)Table(target)[47])(target, &clear);
            ((delegate* unmanaged[Stdcall]<nint, nint, RectF*, float, uint, nint, void>)Table(target)[26])
                (target, d2dBitmap, &clip, 1, 0, 0);
            ((delegate* unmanaged[Stdcall]<nint, void>)Table(target)[46])(target);
            int endResult = ((delegate* unmanaged[Stdcall]<nint, nint, nint, int>)Table(target)[49])(target, 0, 0);
            drawingTarget = false;
            Check(endResult);
            endResult = ((delegate* unmanaged[Stdcall]<nint, int>)Table(surface)[4])(surface);
            drawingSurface = false;
            Check(endResult);
        }
        finally
        {
            if (data != null) bitmap.UnlockBits(data);
            if (drawingTarget) ((delegate* unmanaged[Stdcall]<nint, nint, nint, int>)Table(target)[49])(target, 0, 0);
            if (drawingSurface) ((delegate* unmanaged[Stdcall]<nint, int>)Table(surface)[4])(surface);
            Release(ref d2dBitmap); Release(ref target); Release(ref dxgiSurface);
        }
    }

    [StructLayout(LayoutKind.Sequential)] private readonly record struct SizeU(uint Width, uint Height);
    [StructLayout(LayoutKind.Sequential)] private readonly record struct RectF(float Left, float Top, float Right, float Bottom);
    [StructLayout(LayoutKind.Sequential)] private readonly record struct Matrix3x2(float M11, float M12, float M21, float M22, float Dx, float Dy);
    [StructLayout(LayoutKind.Sequential)] private struct ColorF { public float R, G, B, A; }
    [StructLayout(LayoutKind.Sequential)] private struct BitmapProperties { public uint Format, AlphaMode; public float DpiX, DpiY; }
    [StructLayout(LayoutKind.Sequential)] private struct RenderTargetProperties
    { public uint Type, Format, AlphaMode; public float DpiX, DpiY; public uint Usage, MinimumLevel; }
}
