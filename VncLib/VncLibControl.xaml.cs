﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace VncLib
{
    /// <summary>
    /// Interaktionslogik für VncLibControl.xaml
    /// </summary>
    public partial class VncLibControl : UserControl
    {
        private RfbClient _connection;
        private WriteableBitmap _remoteScreen;
        private DateTime _lastMouseMove = DateTime.Now;
        private bool _limitMouseMove = true;
        private WriteableBitmap _bitmap;
        private readonly List<Key> _nonSpecialKeys = new List<Key>(); //A List of all Keys, that are handled by the transparent Textbox

        private string _serverAddress = ""; //The Remoteserveraddress
        private int _serverPort = 5900; //The Remoteserverport
        private string _serverPassword = ""; //The Remoteserverpassword

        public delegate void UpdateScreenCallback(ScreenUpdateEventArgs newScreen);

        public readonly DispatcherTimer TmrEllipse;
        public readonly DispatcherTimer TmrClipboardCheck;
        public readonly DispatcherTimer TmrScreen;

        public VncLibControl()
        {
            InitializeComponent();

            CreateSpecialKeys();

            TmrEllipse = new DispatcherTimer();
            TmrEllipse.Interval = TimeSpan.FromMilliseconds(200);
            TmrEllipse.Tick += tmrEllipse_Tick;

            TmrClipboardCheck = new DispatcherTimer();
            TmrClipboardCheck.Interval = TimeSpan.FromMilliseconds(500);
            TmrClipboardCheck.Tick += tmrClipboard_Tick;
            TmrClipboardCheck.IsEnabled = true;

            TmrScreen = new DispatcherTimer();
            TmrScreen.Interval = TimeSpan.FromMilliseconds(23);
            TmrScreen.Tick += tmrScreen_Tick;
            TmrScreen.Start();

            this.Focus();
        }

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

        DateTime _lastUpdate = DateTime.MinValue;
        void tmrScreen_Tick(object sender, EventArgs e)
        {
            if (_connection != null)
                if (DateTime.Now.Subtract(_lastUpdate).TotalMilliseconds > 42)
                    _connection.RefreshScreen();
        }

        void tmrEllipse_Tick(object sender, EventArgs e)
        {
            ellipse1.Visibility = System.Windows.Visibility.Collapsed;
            TmrEllipse.Stop();
        }

        private void ConnectInternal(string serverAddress, int serverPort, string serverPassword)
        {
            //Create a connection
            _connection = new RfbClient(serverAddress, serverPort, serverPassword);

            //Is Triggered when the Screen is beeing updated
            _connection.ScreenUpdate += new RfbClient.ScreenUpdateEventHandler(Connection_ScreenUpdate);
            //Is Triggered, when the RfbClient sends a Log-Event
            _connection.LogMessage += new RfbClient.LogMessageEventHandler(Connection_LogMessage);

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
                image1.Dispatcher.Invoke(new UpdateScreenCallback(UpdateImage), new object[] { e });
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
                    _bitmap = new WriteableBitmap(newScreens.Rects.First().Width, newScreens.Rects.First().Height, 96, 96, PixelFormats.Bgr32, null);
                    image1.Source = _bitmap;
                }
                foreach (var newScreen in newScreens.Rects)
                    _bitmap.WritePixels(new Int32Rect(0, 0, newScreen.Width, newScreen.Height), newScreen.PixelData, newScreen.Width * 4, newScreen.PosX, newScreen.PosY);
                image1.InvalidateVisual();

            }
            catch (Exception)
            {
                Console.WriteLine();
                throw;
            }
        }

        bool _ignoreNextKey;

        /// <summary>
        /// Send Special Keys
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void UserControl_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (_connection == null) return;

            if (!_nonSpecialKeys.Contains(e.Key) || Keyboard.IsKeyDown(Key.LeftCtrl)) //If Control-Key is pressed, don't Send NonSpecialKey as a Sign
            {
                if (Keyboard.IsKeyDown(Key.LeftCtrl))
                    _ignoreNextKey = true;

                _connection.SendKey(e);
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
                if (Keyboard.IsKeyDown(Key.LeftCtrl))
                    _ignoreNextKey = true;

                _connection.SendKey(e);
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
            if (tbInput.Text.Length > 0)
            {
                var sign = tbInput.Text;
                tbInput.Clear();

                _connection.SendSign(sign.ToCharArray()[0]);
                _ignoreNextKey = true;
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
            if (_limitMouseMove && buttonValue == 0 && DateTime.Now.Subtract(_lastMouseMove).TotalMilliseconds < 200)
                return;

            if (_connection != null && _connection.IsConnected)
            {
                _connection.SendMouseClick(
                     (UInt16)(e.GetPosition(tbInput).X / image1.ActualWidth * _connection.Properties.FramebufferWidth + 1),
                     (UInt16)(e.GetPosition(tbInput).Y / image1.ActualHeight * _connection.Properties.FramebufferHeight + 1)
                     , buttonValue);
            }

            if (_limitMouseMove)
                _lastMouseMove = DateTime.Now;
        }

        private int _lastClipboardHash; //To check, if the text has changed

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
                        if (_lastClipboardHash != Clipboard.GetText().GetHashCode()) //If the Text has changed since last check
                        {
                            //_Connection.SendClientCutText(Clipboard.GetText());
                            _lastClipboardHash = Clipboard.GetText().GetHashCode(); //Set the new Hash
                        }
                    }
                }
                catch (Exception ) //
                {
                    
                }
            }
        }

        /// <summary>
        /// The IP or Hostname of the Remoteserver
        /// </summary>
        public string ServerAddress
        {
            get { return _serverAddress; }
            set
            {
                //Check if it is a possible correct server address
                var ipAddressRegex = @"^(([0-9]|[1-9][0-9]|1[0-9]{2}|2[0-4][0-9]|25[0-5])\.){3}([0-9]|[1-9][0-9]|1[0-9]{2}|2[0-4][0-9]|25[0-5])$";
                var hostnameRegex = @"^(([a-zA-Z0-9]|[a-zA-Z0-9][a-zA-Z0-9\-]*[a-zA-Z0-9])\.)*([A-Za-z0-9]|[A-Za-z0-9][A-Za-z0-9\-]*[A-Za-z0-9])$";

                var validateIp = new System.Text.RegularExpressions.Regex(ipAddressRegex);
                var validateHost = new System.Text.RegularExpressions.Regex(hostnameRegex);

                if (validateIp.IsMatch(value) && validateHost.IsMatch(value))
                    _serverAddress = value;
            }
        }

        /// <summary>
        /// The Port of the Remoteserver (Default: 5900)
        /// </summary>
        public int ServerPort
        {
            get { return _serverPort; }
            set
            {
                //Validate of port is valid
                if (value > 0 && value < 65536)
                {
                    _serverPort = value;
                }
            }
        }

        /// <summary>
        /// The Password to join the Server
        /// </summary>
        public string ServerPassword
        {
            get { return _serverPassword; }
            set { _serverPassword = value; }
        }

        /// <summary>
        /// Should the interval of sending MouseMoveCommands to the VNC-Server be limited? Default=true
        /// </summary>
        public bool LimitMouseEvents
        {
            get { return _limitMouseMove; }
            set { _limitMouseMove = value; }
        }

        public void Connect()
        {
            ConnectInternal(_serverAddress, _serverPort, _serverPassword);
        }

        public void Connect(string serverAddress)
        {
            ConnectInternal(serverAddress, _serverPort, "");
        }

        public void Connect(string serverAddress, string serverPassword)
        {
            ConnectInternal(serverAddress, _serverPort, serverPassword);
        }

        public void Connect(string serverAddress, int serverPort, string serverPassword)
        {
            ConnectInternal(serverAddress, serverPort, serverPassword);
        }

        public void SendKeyCombination(KeyCombination keyComb)
        {
            _connection.SendKeyCombination(keyComb);
        }

        public void UpdateScreen()
        {
            _connection.RefreshScreen();
        }
    }
}