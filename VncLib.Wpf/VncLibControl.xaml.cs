using System;
using VncLib;
using System.IO;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using VncLib.VncCommands;
using Color = System.Windows.Media.Color;

namespace VncLib.Wpf
{
    /// <summary>
    /// Interaktionslogik für VncLibControl.xaml
    /// </summary>
    public partial class VncLibControl : UserControl
    {
        public delegate void UpdateScreenCallback(ScreenUpdateEventArgs newScreen);

        private readonly List<Key> _nonSpecialKeys = new List<Key>(); //A List of all Keys, that are handled by the transparent Textbox

        public readonly DispatcherTimer TmrClipboardCheck;

        public readonly DispatcherTimer TmrEllipse;
        public readonly DispatcherTimer TmrScreen;
        private WriteableBitmap _bitmap;
        private RfbClient _connection;

        // private bool _ignoreNextKey;

        private int _lastClipboardHash; //To check, if the text has changed
        private DateTime _lastMouseMove = DateTime.Now;

        DateTime _lastUpdate = DateTime.MinValue;
        private WriteableBitmap _remoteScreen;

        private VncLibUserCallback _vncLibUserCallback;
        private VncCommandPlayerCommandExecuted _commandExecuted;
        private VncCommandPlayerPreviewCommandExecute _previewCommandExecute;

        public VncLibControl()
        {
            InitializeComponent();
            FakeInput.CaretBrush = new SolidColorBrush(Color.FromArgb(0, 0, 0, 0));
            ServerPort = 5900;
            CreateSpecialKeys();

            TmrEllipse = new DispatcherTimer();
            TmrEllipse.Interval = TimeSpan.FromMilliseconds(200);
            TmrEllipse.Tick += tmrEllipse_Tick;

            //TODO
            //TmrClipboardCheck = new DispatcherTimer();
            //TmrClipboardCheck.Interval = TimeSpan.FromMilliseconds(500);
            //TmrClipboardCheck.Tick += tmrClipboard_Tick;
            //TmrClipboardCheck.IsEnabled = true;

            TmrScreen = new DispatcherTimer();
            TmrScreen.Interval = TimeSpan.FromMilliseconds(4);
            TmrScreen.Tick += tmrScreen_Tick;
            TmrScreen.Start();

            Focus();
        }

        public static readonly DependencyProperty ServerAddressProperty = DependencyProperty.Register(
            "ServerAddress", typeof(string), typeof(VncLibControl), new PropertyMetadata(default(string)));

        public static readonly DependencyProperty ServerPasswordProperty = DependencyProperty.Register(
            "ServerPassword", typeof(string), typeof(VncLibControl), new PropertyMetadata(default(string)));

        public static readonly DependencyProperty ServerPortProperty = DependencyProperty.Register(
            "ServerPort", typeof(int), typeof(VncLibControl), new PropertyMetadata(default(int)));


        /// <summary>
        /// The Port of the Remoteserver (Default: 5900)
        /// </summary>
        public int ServerPort
        {
            get => (int)GetValue(ServerPortProperty);
            set => SetValue(ServerPortProperty, value);
        }

        public string ServerPassword
        {
            get => (string)GetValue(ServerPasswordProperty);
            set => SetValue(ServerPasswordProperty, value);
        }

        /// <summary>
        /// The IP or Hostname of the Remoteserver
        /// </summary>
        public string ServerAddress
        {
            get => (string)GetValue(ServerAddressProperty);
            set => SetValue(ServerAddressProperty, value);
        }

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
        /// A snapshot of the current remote screen as a WPF <see cref="BitmapSource"/>,
        /// or <c>null</c> if no frame has been received yet.
        /// </summary>
        public BitmapSource Screenshot => _bitmap?.Clone();

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

        private void CreateSpecialKeys()
        {
            _nonSpecialKeys.Add(Key.A);
            _nonSpecialKeys.Add(Key.B);
            _nonSpecialKeys.Add(Key.C);
            _nonSpecialKeys.Add(Key.D);
            _nonSpecialKeys.Add(Key.E);
            _nonSpecialKeys.Add(Key.F);
            _nonSpecialKeys.Add(Key.G);
            _nonSpecialKeys.Add(Key.H);
            _nonSpecialKeys.Add(Key.I);
            _nonSpecialKeys.Add(Key.J);
            _nonSpecialKeys.Add(Key.K);
            _nonSpecialKeys.Add(Key.L);
            _nonSpecialKeys.Add(Key.M);
            _nonSpecialKeys.Add(Key.N);
            _nonSpecialKeys.Add(Key.O);
            _nonSpecialKeys.Add(Key.P);
            _nonSpecialKeys.Add(Key.Q);
            _nonSpecialKeys.Add(Key.R);
            _nonSpecialKeys.Add(Key.S);
            _nonSpecialKeys.Add(Key.T);
            _nonSpecialKeys.Add(Key.U);
            _nonSpecialKeys.Add(Key.V);
            _nonSpecialKeys.Add(Key.W);
            _nonSpecialKeys.Add(Key.X);
            _nonSpecialKeys.Add(Key.Y);
            _nonSpecialKeys.Add(Key.Z);

            _nonSpecialKeys.Add(Key.D0);
            _nonSpecialKeys.Add(Key.D1);
            _nonSpecialKeys.Add(Key.D2);
            _nonSpecialKeys.Add(Key.D3);
            _nonSpecialKeys.Add(Key.D4);
            _nonSpecialKeys.Add(Key.D5);
            _nonSpecialKeys.Add(Key.D6);
            _nonSpecialKeys.Add(Key.D7);
            _nonSpecialKeys.Add(Key.D8);
            _nonSpecialKeys.Add(Key.D9);

            _nonSpecialKeys.Add(Key.Add);
            _nonSpecialKeys.Add(Key.Decimal);
            _nonSpecialKeys.Add(Key.Divide);
            _nonSpecialKeys.Add(Key.Multiply);
            _nonSpecialKeys.Add(Key.OemBackslash);
            _nonSpecialKeys.Add(Key.OemCloseBrackets);
            _nonSpecialKeys.Add(Key.OemComma);
            _nonSpecialKeys.Add(Key.OemMinus);
            _nonSpecialKeys.Add(Key.OemOpenBrackets);
            _nonSpecialKeys.Add(Key.OemPeriod);
            _nonSpecialKeys.Add(Key.OemPipe);
            _nonSpecialKeys.Add(Key.OemPlus);
            _nonSpecialKeys.Add(Key.OemQuestion);
            _nonSpecialKeys.Add(Key.OemQuotes);
            _nonSpecialKeys.Add(Key.OemSemicolon);
            _nonSpecialKeys.Add(Key.OemTilde);

            _nonSpecialKeys.Add(Key.NumPad0);
            _nonSpecialKeys.Add(Key.NumPad1);
            _nonSpecialKeys.Add(Key.NumPad2);
            _nonSpecialKeys.Add(Key.NumPad3);
            _nonSpecialKeys.Add(Key.NumPad4);
            _nonSpecialKeys.Add(Key.NumPad5);
            _nonSpecialKeys.Add(Key.NumPad6);
            _nonSpecialKeys.Add(Key.NumPad7);
            _nonSpecialKeys.Add(Key.NumPad8);
            _nonSpecialKeys.Add(Key.NumPad9);
        }

        public async Task PlayCommands(IEnumerable<IVncCommand> commands)
        {
            await _connection.Play(commands, PreviewCommandExecute, CommandExecuted);
        }

        void tmrScreen_Tick(object sender, EventArgs e)
        {
            if (_connection != null)
                if (DateTime.Now.Subtract(_lastUpdate).TotalMilliseconds > 42)
                    _connection.RefreshScreen();
        }

        void tmrEllipse_Tick(object sender, EventArgs e)
        {
            El.Visibility = Visibility.Collapsed;
            TmrEllipse.Stop();
        }

        private void ConnectInternal(string serverAddress, int serverPort, string serverPassword)
        {
            //Create a connection
            _connection = new RfbClient(serverAddress, serverPort, serverPassword);

            //Is Triggered when the Screen is beeing updated
            _connection.ScreenUpdate += new RfbClient.ScreenUpdateEventHandler(Connection_ScreenUpdate);
            //Is Triggered, when the RfbClient sends a Log-Event
            //_connection.LogMessage += new RfbClient.LogMessageEventHandler(Connection_LogMessage);

            _connection.StartConnection();
        }

        private void Connection_LogMessage(object sender, LogMessageEventArgs e)
        {
            if (e.LogType == Logtype.User)
                MessageBox.Show(e.LogMessage);
        }

        private void Connection_ScreenResolutionChange(object sender, ScreenResolutionChangeEventArgs e)
        {
            if (_remoteScreen == null)
                _remoteScreen = new WriteableBitmap(e.ResX, e.ResY, 96, 96, PixelFormats.Rgb24, null);
        }

        private void Connection_ScreenUpdate(object sender, ScreenUpdateEventArgs e)
        {
            if (_connection != null && _connection.IsConnected)
            {
                VncImage.Dispatcher.Invoke(new UpdateScreenCallback(UpdateImage), new object[] { e });
            }
        }

        private void UserControl_Unloaded(object sender, RoutedEventArgs e)
        {
            _connection.Disconnect();
        }

        /// <summary>
        /// Update the RemoteImage
        /// </summary>
        /// <param name="newScreens"></param>
        private void UpdateImage(ScreenUpdateEventArgs newScreens)
        {
            try
            {
                if (newScreens.Rects.Count == 0)
                    return;
                _lastUpdate = DateTime.Now;

                if (_bitmap == null)
                {
                    _bitmap = new WriteableBitmap(newScreens.Rects.First().Width, newScreens.Rects.First().Height, 96,
                        96, PixelFormats.Bgr32, null);
                    VncImage.Source = _bitmap;
                }
                foreach (var newScreen in newScreens.Rects)
                    _bitmap.WritePixels(new Int32Rect(0, 0, newScreen.Width, newScreen.Height), newScreen.PixelData,
                        newScreen.Width * 4, newScreen.PosX, newScreen.PosY);
                VncImage.InvalidateVisual();
            }
            catch (Exception)
            {
                Console.WriteLine();
                throw;
            }
        }

        /// <summary>
        /// Send Special Keys
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void UserControl_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (_connection == null) return;

            if (!_nonSpecialKeys.Contains(e.Key) || Keyboard.IsKeyDown(Key.LeftCtrl)
            ) //If Control-Key is pressed, don't Send NonSpecialKey as a Sign
            {
                //if (Keyboard.IsKeyDown(Key.LeftCtrl))
                //    _ignoreNextKey = true;

                _connection.SendKey(WpfKeyMap.ToVncKey(e.Key), e.IsDown);
            }
        }

        /// <summary>
        /// Send Special Keys
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void UserControl_PreviewKeyUp(object sender, KeyEventArgs e)
        {
            if (_connection == null) return;

            if (!_nonSpecialKeys.Contains(e.Key) || Keyboard.IsKeyDown(Key.LeftCtrl))
            {
                //if (Keyboard.IsKeyDown(Key.LeftCtrl))
                //    _ignoreNextKey = true;

                _connection.SendKey(WpfKeyMap.ToVncKey(e.Key), e.IsDown);
            }
        }

        /// <summary>
        /// Captures the Mouseclicks
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void UserControl_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            HandleMouseState(e);
        }

        /// <summary>
        /// Captures the Mouseclicks
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void UserControl_PreviewMouseUp(object sender, MouseButtonEventArgs e)
        {
            HandleMouseState(e);
        }

        /// <summary>
        /// Send Mouse Movements to the Server
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void UserControl_PreviewMouseMove(object sender, MouseEventArgs e)
        {
            HandleMouseState(e);
        }

        /// <summary>
        /// Triggers the MouseWheelActions
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void UserControl_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            byte buttonValue = 0;
            if (e.Delta > 0) //Wheel moved Up
                buttonValue = 8;
            else if (e.Delta < 0) //Wheel moved down
                buttonValue = 16;

            _connection.SendMouseClick((UInt16)e.GetPosition(this).X, (UInt16)e.GetPosition(this).Y, buttonValue);
        }

        /// <summary>
        /// Send all typed Signes
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void tbInsert_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (FakeInput.Text.Length > 0)
            {
                var sign = FakeInput.Text;
                FakeInput.Clear();

                _connection.SendSign(sign.ToCharArray()[0]);
                //_ignoreNextKey = true;
            }
        }

        /// <summary>
        /// Thats what to do, when a Mousebutton was clicked
        /// </summary>
        /// <param name="e"></param>
        private void HandleMouseState(MouseEventArgs e)
        {
            byte buttonValue = 0;
            if (e.LeftButton == MouseButtonState.Pressed)
                buttonValue = 1;
            if (e.RightButton == MouseButtonState.Pressed)
                buttonValue += 4;
            if (e.MiddleButton == MouseButtonState.Pressed)
                buttonValue += 2;

            //Don't send, if there is no MouseClick and a Event was triggered less then 1/5 second before.
            //if (LimitMouseEvents && buttonValue == 0 && DateTime.Now.Subtract(_lastMouseMove).TotalMilliseconds < 200)
            //    return;

            if (_connection != null && _connection.IsConnected)
            {
                var xPos = e.GetPosition(FakeInput).X / VncImage.ActualWidth *
                           _connection.Properties.FramebufferWidth + 1;
                var yPos = e.GetPosition(FakeInput).Y / VncImage.ActualHeight *
                           _connection.Properties.FramebufferHeight + 1;

                //call a callback method so the user can get the mouseposition
                VncLibUserCallback?.Invoke(new VncPointerEventArgs(e.LeftButton == MouseButtonState.Pressed, e.MiddleButton == MouseButtonState.Pressed, e.RightButton == MouseButtonState.Pressed), xPos, yPos);

                _connection.SendMouseClick((UInt16)xPos, (UInt16)yPos, buttonValue);
            }

            if (LimitMouseEvents)
                _lastMouseMove = DateTime.Now;
        }




        /// <summary>
        /// Checkinterval to check, if the Clipboard changed. Not a stylish way, but it works
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        void tmrClipboard_Tick(object sender, EventArgs e)
        {
            if (_connection == null)
                return;

            if (Clipboard.ContainsText()) //Check if Clipboard contains Text
            {
                try
                {
                    if (Clipboard.GetText().Length < Int32.MaxValue) //normally it should be UInt32.MaxValue, but this is larger then .Length ever can be. So 2.1Million signs are maximum
                    {
                        if (_lastClipboardHash != Clipboard.GetText().GetHashCode()
                        ) //If the Text has changed since last check
                        {
                            //_Connection.SendClientCutText(Clipboard.GetText());
                            _lastClipboardHash = Clipboard.GetText().GetHashCode(); //Set the new Hash
                        }
                    }
                }
                catch (Exception) //
                {
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
            TmrEllipse?.Stop();
            TmrClipboardCheck?.Stop();
            _connection?.Disconnect();
        }

        public void UpdateScreen()
        {
            _connection.RefreshScreen();
        }
    }
}