// Copyright 2017 The VncLib Authors. All rights reserved.
// Use of this source code is governed by a BSD-style
// license that can be found in the LICENSE file.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using VncLib.VncCommands;
using Timer = System.Timers.Timer;

namespace VncLib
{
    /// <summary>
    /// UI-framework neutral façade around <see cref="RfbClient"/>.
    /// It maintains a raw BGR32 framebuffer composed from the server's screen updates
    /// and raises <see cref="FrameArrived"/> whenever it changes, so any UI toolkit
    /// (WPF, Avalonia, headless) can render the remote screen without this library
    /// depending on a specific presentation framework.
    /// </summary>
    public class VncConnection
    {
        public delegate void UpdateScreenCallback(ScreenUpdateEventArgs newScreen);

        public readonly Timer TmrScreen;

        private RfbClient _connection;

        private readonly object _lockObj = new object();

        private byte[] _framebuffer;
        private int _framebufferWidth;
        private int _framebufferHeight;

        private VncLibUserCallback _vncLibUserCallback;
        private VncCommandPlayerCommandExecuted _commandExecuted;
        private VncCommandPlayerPreviewCommandExecute _previewCommandExecute;

        public VncConnection()
        {
            ServerPort = 5900;

            TmrScreen = new Timer(UpdateInterval);
            TmrScreen.Elapsed += tmrScreen_Tick;
        }

        /// <summary>
        /// The Port of the Remoteserver (Default: 5900)
        /// </summary>
        public int ServerPort { get; set; }

        public string ServerPassword { get; set; }

        /// <summary>
        /// The IP or Hostname of the Remoteserver
        /// </summary>
        public string ServerAddress { get; set; }

        /// <summary>
        /// Update interval in milliseconds, default = 40
        /// </summary>
        public int UpdateInterval { get; set; } = 40;

        public bool AutoUpdate { get; set; }

        public VncLibUserCallback VncLibUserCallback
        {
            get => _vncLibUserCallback;
            set => _vncLibUserCallback = value;
        }

        public VncCommandPlayerCommandExecuted CommandExecuted
        {
            get => _commandExecuted;
            set => _commandExecuted = value;
        }

        public VncCommandPlayerPreviewCommandExecute PreviewCommandExecute
        {
            get => _previewCommandExecute;
            set => _previewCommandExecute = value;
        }

        /// <summary>
        /// Width of the current framebuffer in pixels, or 0 before the first update.
        /// </summary>
        public int FramebufferWidth => _framebufferWidth;

        /// <summary>
        /// Height of the current framebuffer in pixels, or 0 before the first update.
        /// </summary>
        public int FramebufferHeight => _framebufferHeight;

        /// <summary>
        /// Raised on the background thread after the framebuffer has been updated with
        /// new screen data. Consumers should copy the framebuffer via <see cref="GetFramebuffer"/>
        /// and marshal to their UI thread as needed.
        /// </summary>
        public event EventHandler FrameArrived;

        /// <summary>
        /// Returns a copy of the current framebuffer as raw BGR32 (4 bytes per pixel,
        /// blue/green/red/unused) with a stride of <see cref="FramebufferWidth"/> * 4,
        /// or <c>null</c> if no screen data has been received yet.
        /// </summary>
        public byte[] GetFramebuffer()
        {
            lock (_lockObj)
            {
                if (_framebuffer == null)
                    return null;

                var copy = new byte[_framebuffer.Length];
                Array.Copy(_framebuffer, copy, _framebuffer.Length);
                return copy;
            }
        }

        public ObservableCollection<IVncCommand> ExecutedCommands => _connection.ExecutedCommands;

        /// <summary>
        /// enable the mouse action and position capturing - call after calling Connect()
        /// </summary>
        public void EnableMouseCapturing()
        {
            if (_connection != null)
                _connection.MouseActionCaptureEnabled = true;
        }

        public void DisableMouseCapturing()
        {
            if (_connection != null)
                _connection.MouseActionCaptureEnabled = false;
        }

        /// <summary>
        /// Should the interval of sending MouseMoveCommands to the VNC-Server be limited? Default=true
        /// </summary>
        public bool LimitMouseEvents { get; set; } = true;

        void tmrScreen_Tick(object sender, EventArgs e)
        {
            if (AutoUpdate)
                _connection?.RefreshScreen();
        }

        private void ConnectInternal(string serverAddress, int serverPort, string serverPassword)
        {
            //Create a connection
            _connection = new RfbClient(serverAddress, serverPort, serverPassword);

            //Is Triggered when the Screen is beeing updated
            _connection.ScreenUpdate += new RfbClient.ScreenUpdateEventHandler(Connection_ScreenUpdate);

            _connection.StartConnection();

            if (AutoUpdate)
                TmrScreen.Enabled = true;
        }

        private void Connection_ScreenUpdate(object sender, ScreenUpdateEventArgs e)
        {
            if (_connection != null && _connection.IsConnected)
            {
                lock (_lockObj)
                {
                    UpdateFramebuffer(e);
                }

                FrameArrived?.Invoke(this, EventArgs.Empty);
            }
        }

        /// <summary>
        /// Compose the incoming rectangles into the raw BGR32 framebuffer.
        /// </summary>
        private void UpdateFramebuffer(ScreenUpdateEventArgs newScreens)
        {
            if (newScreens.Rects == null || newScreens.Rects.Count == 0)
                return;

            var first = newScreens.Rects[0];

            if (_framebuffer == null)
            {
                _framebufferWidth = first.Width;
                _framebufferHeight = first.Height;
                _framebuffer = new byte[_framebufferWidth * _framebufferHeight * 4];
            }

            foreach (var rect in newScreens.Rects)
            {
                if (rect.PixelData == null)
                    continue;

                var rowBytes = rect.Width * 4;
                var maxRowBytes = (_framebufferWidth - rect.PosX) * 4;
                if (maxRowBytes <= 0)
                    continue;

                for (var y = 0; y < rect.Height; y++)
                {
                    var destY = rect.PosY + y;
                    if (destY < 0 || destY >= _framebufferHeight)
                        continue;

                    var srcOffset = y * rowBytes;
                    if (srcOffset >= rect.PixelData.Length)
                        break;

                    var destOffset = (destY * _framebufferWidth + rect.PosX) * 4;

                    var copyLen = rowBytes;
                    if (copyLen > maxRowBytes)
                        copyLen = maxRowBytes;
                    if (srcOffset + copyLen > rect.PixelData.Length)
                        copyLen = rect.PixelData.Length - srcOffset;
                    if (copyLen <= 0)
                        continue;

                    Array.Copy(rect.PixelData, srcOffset, _framebuffer, destOffset, copyLen);
                }
            }
        }

        public void Connect()
        {
            ConnectInternal(ServerAddress, ServerPort, ServerPassword);
        }

        public void Connect(string serverAddress)
        {
            ConnectInternal(serverAddress, ServerPort, "");
        }

        public void Connect(string serverAddress, string serverPassword)
        {
            ConnectInternal(serverAddress, ServerPort, serverPassword);
        }

        public void Connect(string serverAddress, int serverPort, string serverPassword)
        {
            ConnectInternal(serverAddress, serverPort, serverPassword);
        }

        public void SendKeyCombination(KeyCombination keyComb)
        {
            _connection.SendKeyCombination(keyComb);
        }

        public void Disconnect()
        {
            TmrScreen?.Stop();
            _connection?.Disconnect();
        }

        public void UpdateScreen()
        {
            _connection.RefreshScreen();
        }

        public async Task PlayCommands(IEnumerable<IVncCommand> commands)
        {
            await _connection.Play(commands, PreviewCommandExecute, CommandExecuted);
        }
    }
}
