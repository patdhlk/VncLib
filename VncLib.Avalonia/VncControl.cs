using System;
using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using VncLib;

namespace VncLib.Avalonia;

public class VncControl : Control
{
    // Styled properties
    public static readonly StyledProperty<string> ServerAddressProperty =
        AvaloniaProperty.Register<VncControl, string>(nameof(ServerAddress), defaultValue: string.Empty);

    public static readonly StyledProperty<string> ServerPasswordProperty =
        AvaloniaProperty.Register<VncControl, string>(nameof(ServerPassword), defaultValue: string.Empty);

    public static readonly StyledProperty<int> ServerPortProperty =
        AvaloniaProperty.Register<VncControl, int>(nameof(ServerPort), defaultValue: 5900);

    public string ServerAddress
    {
        get => GetValue(ServerAddressProperty);
        set => SetValue(ServerAddressProperty, value);
    }

    public string ServerPassword
    {
        get => GetValue(ServerPasswordProperty);
        set => SetValue(ServerPasswordProperty, value);
    }

    public int ServerPort
    {
        get => GetValue(ServerPortProperty);
        set => SetValue(ServerPortProperty, value);
    }

    // Private fields
    private RfbClient? _client;
    private byte[]? _backbuffer;
    private int _fbW;
    private int _fbH;
    private WriteableBitmap? _bitmap;
    private readonly object _lock = new object();

    public VncControl()
    {
        Focusable = true;
    }

    public void Connect()
    {
        if (_client != null)
        {
            Disconnect();
        }

        _client = new RfbClient(ServerAddress, ServerPort, ServerPassword ?? string.Empty);
        _client.ScreenUpdate += OnScreenUpdate;
        _client.StartConnection();
    }

    public void Disconnect()
    {
        if (_client != null)
        {
            _client.ScreenUpdate -= OnScreenUpdate;
            _client.Disconnect();
            _client = null;
        }
    }

    private void OnScreenUpdate(object? sender, ScreenUpdateEventArgs e)
    {
        if (e.Rects == null || e.Rects.Count == 0)
            return;

        lock (_lock)
        {
            // Initialize backbuffer on first update
            if (_backbuffer == null && e.Rects.Count > 0)
            {
                var firstRect = e.Rects[0];
                _fbW = firstRect.Width;
                _fbH = firstRect.Height;
                _backbuffer = new byte[_fbW * _fbH * 4];
            }

            if (_backbuffer == null)
                return;

            // Copy each rect into the backbuffer
            foreach (var rect in e.Rects)
            {
                if (rect.PixelData == null)
                    continue;

                for (int y = 0; y < rect.Height; y++)
                {
                    int srcY = rect.PosY + y;
                    if (srcY >= _fbH)
                        continue;

                    int srcOffset = y * rect.Width * 4;
                    int dstOffset = (srcY * _fbW + rect.PosX) * 4;
                    int copyWidth = Math.Min(rect.Width, _fbW - rect.PosX);

                    if (copyWidth > 0 && dstOffset >= 0 && dstOffset + copyWidth * 4 <= _backbuffer.Length)
                    {
                        Array.Copy(rect.PixelData, srcOffset, _backbuffer, dstOffset, copyWidth * 4);
                    }
                }
            }
        }

        // Post bitmap update to UI thread
        global::Avalonia.Threading.Dispatcher.UIThread.Post(UpdateBitmap);
    }

    private void UpdateBitmap()
    {
        lock (_lock)
        {
            if (_backbuffer == null)
                return;

            // Create or recreate bitmap if size changed
            if (_bitmap == null || _bitmap.PixelSize.Width != _fbW || _bitmap.PixelSize.Height != _fbH)
            {
                _bitmap = new WriteableBitmap(
                    new PixelSize(_fbW, _fbH),
                    new Vector(96, 96),
                    PixelFormats.Bgra8888,
                    AlphaFormat.Premul);
            }

            // Lock and copy data
            using (var fb = _bitmap.Lock())
            {
                // Process row by row, forcing alpha to 255
                byte[] tempRow = new byte[_fbW * 4];
                for (int y = 0; y < _fbH; y++)
                {
                    int srcOffset = y * _fbW * 4;
                    Array.Copy(_backbuffer, srcOffset, tempRow, 0, _fbW * 4);

                    // Force alpha channel to 255 (opaque)
                    for (int x = 0; x < _fbW; x++)
                    {
                        tempRow[x * 4 + 3] = 255;
                    }

                    // Copy to bitmap
                    IntPtr dstPtr = fb.Address + y * fb.RowBytes;
                    Marshal.Copy(tempRow, 0, dstPtr, _fbW * 4);
                }
            }
        }

        InvalidateVisual();
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);

        if (_bitmap != null)
        {
            var sourceRect = new Rect(0, 0, _fbW, _fbH);
            var destRect = new Rect(Bounds.Size);
            context.DrawImage(_bitmap, sourceRect, destRect);
        }
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);

        if (_client != null && _client.IsConnected)
        {
            var vncKey = AvaloniaKeyMap.ToVncKey(e.Key);
            if (vncKey != VncKey.None)
            {
                _client.SendKey(vncKey, true);
                e.Handled = true;
            }
        }
    }

    protected override void OnKeyUp(KeyEventArgs e)
    {
        base.OnKeyUp(e);

        if (_client != null && _client.IsConnected)
        {
            var vncKey = AvaloniaKeyMap.ToVncKey(e.Key);
            if (vncKey != VncKey.None)
            {
                _client.SendKey(vncKey, false);
                e.Handled = true;
            }
        }
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        Focus();
        SendMouseEvent(e);
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        SendMouseEvent(e);
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        SendMouseEvent(e);
    }

    protected override void OnPointerWheelChanged(PointerWheelEventArgs e)
    {
        base.OnPointerWheelChanged(e);

        if (_client != null && _client.IsConnected && _fbW > 0 && _fbH > 0)
        {
            var pos = e.GetPosition(this);
            double scaleX = Bounds.Width > 0 ? _fbW / Bounds.Width : 1.0;
            double scaleY = Bounds.Height > 0 ? _fbH / Bounds.Height : 1.0;

            ushort x = (ushort)Math.Max(0, Math.Min(_fbW - 1, pos.X * scaleX));
            ushort y = (ushort)Math.Max(0, Math.Min(_fbH - 1, pos.Y * scaleY));

            byte mask = (byte)(e.Delta.Y > 0 ? 8 : 16);
            _client.SendMouseClick(x, y, mask);
            e.Handled = true;
        }
    }

    private void SendMouseEvent(PointerEventArgs e)
    {
        if (_client == null || !_client.IsConnected || _fbW <= 0 || _fbH <= 0)
            return;

        var pos = e.GetPosition(this);
        double scaleX = Bounds.Width > 0 ? _fbW / Bounds.Width : 1.0;
        double scaleY = Bounds.Height > 0 ? _fbH / Bounds.Height : 1.0;

        ushort x = (ushort)Math.Max(0, Math.Min(_fbW - 1, pos.X * scaleX));
        ushort y = (ushort)Math.Max(0, Math.Min(_fbH - 1, pos.Y * scaleY));

        var props = e.GetCurrentPoint(this).Properties;
        byte mask = 0;

        if (props.IsLeftButtonPressed)
            mask |= 1;
        if (props.IsMiddleButtonPressed)
            mask |= 2;
        if (props.IsRightButtonPressed)
            mask |= 4;

        _client.SendMouseClick(x, y, mask);
        e.Handled = true;
    }
}
