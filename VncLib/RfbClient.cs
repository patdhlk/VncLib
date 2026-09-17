// Copyright 2017 The VncLib Authors. All rights reserved.
// Use of this source code is governed by a BSD-style
// license that can be found in the LICENSE file.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using VncLib.VncCommands;

namespace VncLib
{
    public class RfbClient : IRfbClient
    {
        private ConnectionProperties _properties = new ConnectionProperties(); //Contains Properties of the Client
        private bool _disconnectionInProgress = false; //Flag for getting, if a Disconnection is in Progress
        private Dictionary<RfbEncoding, RfbEncodingDetails> _encodingDetails = new Dictionary<RfbEncoding, RfbEncodingDetails>(); //Contains the Details for the Encodings
        private UInt16[,] _colorMap = new UInt16[256, 3]; //Stores the Colormap. In Each Dimension is R, G or B
        private Queue<RfbRectangle> _newFrameBuffer = new Queue<RfbRectangle>(); //The FramebufferQueue
        private UInt16[] _largestFrame = new UInt16[2] { 0, 0 }; //The largest known frame (x/Y)
        private BackgroundWorker _receiver; //The BackgroundWorkerthread for receiving Data
        private bool _isConnected; //Is the Client connected?
        private UInt16 _previousX;
        private UInt16 _previousY;
        private DateTime _lastReceive = DateTime.Now; //The Timestamp when the last Received Frame happend

        private Task _receiverTask = null;
        private BlockingCollection<List<RfbRectangle>> _dataChannel = new BlockingCollection<List<RfbRectangle>>();
        private CancellationTokenSource _receiverTaskCancellationTokenSource = new CancellationTokenSource();
 

        //private int _lastReceiveTimeout = 1000; //If no changes were made, a new Frame will be requested after x ms
        //private DispatcherTimer _LastReceiveTimer = new DispatcherTimer(); //The timer, that requests new Frames

        private Dictionary<char, UInt32> _keyCodes = new Dictionary<char, uint>(); //Dictionary for Key-Endcodings (see keys.csv)
        private TcpClient _client; //TCP-Client for Serverconnection
        private NetworkStream _dataStream; //The Stream to read/write Data

        private int _backBuffer2RawStride; //How many Bytes a Row have
        //public byte[] _BackBuffer2PixelData; //The Backbuffer as a Bytearray
        private int _backBuffer2PixelBpp = 24; //The bits per pixel of the Backbuffer
        private bool _shiftDown; //Tracks the shift modifier for resolving printable keys

        private IVncCommand _currentCommand;
        private ObservableCollection<IVncCommand> _executedCommands;
        private int _maxCountsExecutedCommands = 1000;

        /// <summary>
        /// Start connection on default port with no password
        /// </summary>
        /// <param name="server"></param>
        public RfbClient(string server)
        {
            if (PrepareConnection(server, 5900, "") == false)
                Log(Logtype.User, "Connection to the Server " + Properties.Server + " failed.");
        }

        /// <summary>
        /// Start connection with no password
        /// </summary>
        /// <param name="server"></param>
        /// <param name="port"></param>
        public RfbClient(string server, int port)
        {
            if (PrepareConnection(server, port, "") == false)
                Log(Logtype.User, "Connection to the Server " + Properties.Server + " failed.");
        }

        /// <summary>
        /// Start a new connection
        /// </summary>
        /// <param name="server"></param>
        /// <param name="port"></param>
        /// <param name="password"></param>
        public RfbClient(string server, int port, string password)
        {
            if (PrepareConnection(server, port, password) == false)
                Log(Logtype.User, "Connection to the Server " + Properties.Server + " failed.");
        }

        public ObservableCollection<IVncCommand> ExecutedCommands
        {
            get
            {
                if(_executedCommands == null)
                    _executedCommands = new ObservableCollection<IVncCommand>();
                return _executedCommands;
            }
            set => _executedCommands = value;
        }

        private bool LoadKeyDictionary()
        {
            try
            {
                if (!_keyCodes.ContainsKey(' ')) _keyCodes.Add(' ', 32);
                if (!_keyCodes.ContainsKey('!')) _keyCodes.Add('!', 33);
                if (!_keyCodes.ContainsKey('"')) _keyCodes.Add('"', 34);
                if (!_keyCodes.ContainsKey('#')) _keyCodes.Add('#', 35);
                if (!_keyCodes.ContainsKey('$')) _keyCodes.Add('$', 36);
                if (!_keyCodes.ContainsKey('%')) _keyCodes.Add('%', 37);
                if (!_keyCodes.ContainsKey('&')) _keyCodes.Add('&', 38);
                if (!_keyCodes.ContainsKey('\'')) _keyCodes.Add('\'', 39);
                if (!_keyCodes.ContainsKey('(')) _keyCodes.Add('(', 40);
                if (!_keyCodes.ContainsKey(')')) _keyCodes.Add(')', 41);
                if (!_keyCodes.ContainsKey('*')) _keyCodes.Add('*', 42);
                if (!_keyCodes.ContainsKey('+')) _keyCodes.Add('+', 43);
                if (!_keyCodes.ContainsKey(',')) _keyCodes.Add(',', 44);
                if (!_keyCodes.ContainsKey('-')) _keyCodes.Add('-', 45);
                if (!_keyCodes.ContainsKey('.')) _keyCodes.Add('.', 46);
                if (!_keyCodes.ContainsKey('.')) _keyCodes.Add('.', 46);
                if (!_keyCodes.ContainsKey('/')) _keyCodes.Add('/', 47);
                if (!_keyCodes.ContainsKey('0')) _keyCodes.Add('0', 48);
                if (!_keyCodes.ContainsKey('1')) _keyCodes.Add('1', 49);
                if (!_keyCodes.ContainsKey('2')) _keyCodes.Add('2', 50);
                if (!_keyCodes.ContainsKey('3')) _keyCodes.Add('3', 51);
                if (!_keyCodes.ContainsKey('4')) _keyCodes.Add('4', 52);
                if (!_keyCodes.ContainsKey('5')) _keyCodes.Add('5', 53);
                if (!_keyCodes.ContainsKey('6')) _keyCodes.Add('6', 54);
                if (!_keyCodes.ContainsKey('7')) _keyCodes.Add('7', 55);
                if (!_keyCodes.ContainsKey('8')) _keyCodes.Add('8', 56);
                if (!_keyCodes.ContainsKey('9')) _keyCodes.Add('9', 57);
                if (!_keyCodes.ContainsKey(':')) _keyCodes.Add(':', 58);
                if (!_keyCodes.ContainsKey(';')) _keyCodes.Add(';', 59);
                if (!_keyCodes.ContainsKey('<')) _keyCodes.Add('<', 60);
                if (!_keyCodes.ContainsKey('<')) _keyCodes.Add('<', 60);
                if (!_keyCodes.ContainsKey('=')) _keyCodes.Add('=', 61);
                if (!_keyCodes.ContainsKey('>')) _keyCodes.Add('>', 62);
                if (!_keyCodes.ContainsKey('>')) _keyCodes.Add('>', 62);
                if (!_keyCodes.ContainsKey('?')) _keyCodes.Add('?', 63);
                if (!_keyCodes.ContainsKey('@')) _keyCodes.Add('@', 64);
                if (!_keyCodes.ContainsKey('A')) _keyCodes.Add('A', 65);
                if (!_keyCodes.ContainsKey('B')) _keyCodes.Add('B', 66);
                if (!_keyCodes.ContainsKey('C')) _keyCodes.Add('C', 67);
                if (!_keyCodes.ContainsKey('D')) _keyCodes.Add('D', 68);
                if (!_keyCodes.ContainsKey('E')) _keyCodes.Add('E', 69);
                if (!_keyCodes.ContainsKey('F')) _keyCodes.Add('F', 70);
                if (!_keyCodes.ContainsKey('G')) _keyCodes.Add('G', 71);
                if (!_keyCodes.ContainsKey('H')) _keyCodes.Add('H', 72);
                if (!_keyCodes.ContainsKey('I')) _keyCodes.Add('I', 73);
                if (!_keyCodes.ContainsKey('J')) _keyCodes.Add('J', 74);
                if (!_keyCodes.ContainsKey('K')) _keyCodes.Add('K', 75);
                if (!_keyCodes.ContainsKey('L')) _keyCodes.Add('L', 76);
                if (!_keyCodes.ContainsKey('M')) _keyCodes.Add('M', 77);
                if (!_keyCodes.ContainsKey('N')) _keyCodes.Add('N', 78);
                if (!_keyCodes.ContainsKey('O')) _keyCodes.Add('O', 79);
                if (!_keyCodes.ContainsKey('P')) _keyCodes.Add('P', 80);
                if (!_keyCodes.ContainsKey('Q')) _keyCodes.Add('Q', 81);
                if (!_keyCodes.ContainsKey('R')) _keyCodes.Add('R', 82);
                if (!_keyCodes.ContainsKey('S')) _keyCodes.Add('S', 83);
                if (!_keyCodes.ContainsKey('T')) _keyCodes.Add('T', 84);
                if (!_keyCodes.ContainsKey('U')) _keyCodes.Add('U', 85);
                if (!_keyCodes.ContainsKey('V')) _keyCodes.Add('V', 86);
                if (!_keyCodes.ContainsKey('W')) _keyCodes.Add('W', 87);
                if (!_keyCodes.ContainsKey('X')) _keyCodes.Add('X', 88);
                if (!_keyCodes.ContainsKey('Y')) _keyCodes.Add('Y', 89);
                if (!_keyCodes.ContainsKey('Z')) _keyCodes.Add('Z', 90);
                if (!_keyCodes.ContainsKey('[')) _keyCodes.Add('[', 91);
                if (!_keyCodes.ContainsKey('\\')) _keyCodes.Add('\\', 92);
                if (!_keyCodes.ContainsKey(']')) _keyCodes.Add(']', 93);
                if (!_keyCodes.ContainsKey('^')) _keyCodes.Add('^', 94);
                if (!_keyCodes.ContainsKey('_')) _keyCodes.Add('_', 95);
                if (!_keyCodes.ContainsKey('_')) _keyCodes.Add('_', 95);
                if (!_keyCodes.ContainsKey('`')) _keyCodes.Add('`', 96);
                if (!_keyCodes.ContainsKey('a')) _keyCodes.Add('a', 97);
                if (!_keyCodes.ContainsKey('b')) _keyCodes.Add('b', 98);
                if (!_keyCodes.ContainsKey('c')) _keyCodes.Add('c', 99);
                if (!_keyCodes.ContainsKey('d')) _keyCodes.Add('d', 100);
                if (!_keyCodes.ContainsKey('e')) _keyCodes.Add('e', 101);
                if (!_keyCodes.ContainsKey('f')) _keyCodes.Add('f', 102);
                if (!_keyCodes.ContainsKey('g')) _keyCodes.Add('g', 103);
                if (!_keyCodes.ContainsKey('h')) _keyCodes.Add('h', 104);
                if (!_keyCodes.ContainsKey('i')) _keyCodes.Add('i', 105);
                if (!_keyCodes.ContainsKey('j')) _keyCodes.Add('j', 106);
                if (!_keyCodes.ContainsKey('k')) _keyCodes.Add('k', 107);
                if (!_keyCodes.ContainsKey('l')) _keyCodes.Add('l', 108);
                if (!_keyCodes.ContainsKey('m')) _keyCodes.Add('m', 109);
                if (!_keyCodes.ContainsKey('n')) _keyCodes.Add('n', 110);
                if (!_keyCodes.ContainsKey('o')) _keyCodes.Add('o', 111);
                if (!_keyCodes.ContainsKey('p')) _keyCodes.Add('p', 112);
                if (!_keyCodes.ContainsKey('q')) _keyCodes.Add('q', 113);
                if (!_keyCodes.ContainsKey('r')) _keyCodes.Add('r', 114);
                if (!_keyCodes.ContainsKey('s')) _keyCodes.Add('s', 115);
                if (!_keyCodes.ContainsKey('t')) _keyCodes.Add('t', 116);
                if (!_keyCodes.ContainsKey('u')) _keyCodes.Add('u', 117);
                if (!_keyCodes.ContainsKey('v')) _keyCodes.Add('v', 118);
                if (!_keyCodes.ContainsKey('w')) _keyCodes.Add('w', 119);
                if (!_keyCodes.ContainsKey('x')) _keyCodes.Add('x', 120);
                if (!_keyCodes.ContainsKey('y')) _keyCodes.Add('y', 121);
                if (!_keyCodes.ContainsKey('z')) _keyCodes.Add('z', 122);
                if (!_keyCodes.ContainsKey('{')) _keyCodes.Add('{', 123);
                if (!_keyCodes.ContainsKey('|')) _keyCodes.Add('|', 124);
                if (!_keyCodes.ContainsKey('}')) _keyCodes.Add('}', 125);
                if (!_keyCodes.ContainsKey('~')) _keyCodes.Add('~', 126);
                if (!_keyCodes.ContainsKey(' ')) _keyCodes.Add(' ', 160);
                if (!_keyCodes.ContainsKey('¡')) _keyCodes.Add('¡', 161);
                if (!_keyCodes.ContainsKey('¢')) _keyCodes.Add('¢', 162);
                if (!_keyCodes.ContainsKey('£')) _keyCodes.Add('£', 163);
                if (!_keyCodes.ContainsKey('¤')) _keyCodes.Add('¤', 164);
                if (!_keyCodes.ContainsKey('¥')) _keyCodes.Add('¥', 165);
                if (!_keyCodes.ContainsKey('¦')) _keyCodes.Add('¦', 166);
                if (!_keyCodes.ContainsKey('§')) _keyCodes.Add('§', 167);
                if (!_keyCodes.ContainsKey('¨')) _keyCodes.Add('¨', 168);
                if (!_keyCodes.ContainsKey('©')) _keyCodes.Add('©', 169);
                if (!_keyCodes.ContainsKey('ª')) _keyCodes.Add('ª', 170);
                if (!_keyCodes.ContainsKey('«')) _keyCodes.Add('«', 171);
                if (!_keyCodes.ContainsKey('¬')) _keyCodes.Add('¬', 172);
                if (!_keyCodes.ContainsKey('­')) _keyCodes.Add('­', 173);
                if (!_keyCodes.ContainsKey('®')) _keyCodes.Add('®', 174);
                if (!_keyCodes.ContainsKey('¯')) _keyCodes.Add('¯', 175);
                if (!_keyCodes.ContainsKey('¯')) _keyCodes.Add('¯', 175);
                if (!_keyCodes.ContainsKey('°')) _keyCodes.Add('°', 176);
                if (!_keyCodes.ContainsKey('±')) _keyCodes.Add('±', 177);
                if (!_keyCodes.ContainsKey('²')) _keyCodes.Add('²', 178);
                if (!_keyCodes.ContainsKey('³')) _keyCodes.Add('³', 179);
                if (!_keyCodes.ContainsKey('´')) _keyCodes.Add('´', 180);
                if (!_keyCodes.ContainsKey('µ')) _keyCodes.Add('µ', 181);
                if (!_keyCodes.ContainsKey('¶')) _keyCodes.Add('¶', 182);
                if (!_keyCodes.ContainsKey('·')) _keyCodes.Add('·', 183);
                if (!_keyCodes.ContainsKey('¸')) _keyCodes.Add('¸', 184);
                if (!_keyCodes.ContainsKey('¹')) _keyCodes.Add('¹', 185);
                if (!_keyCodes.ContainsKey('º')) _keyCodes.Add('º', 186);
                if (!_keyCodes.ContainsKey('»')) _keyCodes.Add('»', 187);
                if (!_keyCodes.ContainsKey('¼')) _keyCodes.Add('¼', 188);
                if (!_keyCodes.ContainsKey('½')) _keyCodes.Add('½', 189);
                if (!_keyCodes.ContainsKey('¾')) _keyCodes.Add('¾', 190);
                if (!_keyCodes.ContainsKey('¿')) _keyCodes.Add('¿', 191);
                if (!_keyCodes.ContainsKey('À')) _keyCodes.Add('À', 192);
                if (!_keyCodes.ContainsKey('Á')) _keyCodes.Add('Á', 193);
                if (!_keyCodes.ContainsKey('Â')) _keyCodes.Add('Â', 194);
                if (!_keyCodes.ContainsKey('Ã')) _keyCodes.Add('Ã', 195);
                if (!_keyCodes.ContainsKey('Ä')) _keyCodes.Add('Ä', 196);
                if (!_keyCodes.ContainsKey('Å')) _keyCodes.Add('Å', 197);
                if (!_keyCodes.ContainsKey('Æ')) _keyCodes.Add('Æ', 198);
                if (!_keyCodes.ContainsKey('Ç')) _keyCodes.Add('Ç', 199);
                if (!_keyCodes.ContainsKey('È')) _keyCodes.Add('È', 200);
                if (!_keyCodes.ContainsKey('É')) _keyCodes.Add('É', 201);
                if (!_keyCodes.ContainsKey('Ê')) _keyCodes.Add('Ê', 202);
                if (!_keyCodes.ContainsKey('Ë')) _keyCodes.Add('Ë', 203);
                if (!_keyCodes.ContainsKey('Ì')) _keyCodes.Add('Ì', 204);
                if (!_keyCodes.ContainsKey('Í')) _keyCodes.Add('Í', 205);
                if (!_keyCodes.ContainsKey('Î')) _keyCodes.Add('Î', 206);
                if (!_keyCodes.ContainsKey('Ï')) _keyCodes.Add('Ï', 207);
                if (!_keyCodes.ContainsKey('Ð')) _keyCodes.Add('Ð', 208);
                if (!_keyCodes.ContainsKey('Ñ')) _keyCodes.Add('Ñ', 209);
                if (!_keyCodes.ContainsKey('Ò')) _keyCodes.Add('Ò', 210);
                if (!_keyCodes.ContainsKey('Ó')) _keyCodes.Add('Ó', 211);
                if (!_keyCodes.ContainsKey('Ô')) _keyCodes.Add('Ô', 212);
                if (!_keyCodes.ContainsKey('Õ')) _keyCodes.Add('Õ', 213);
                if (!_keyCodes.ContainsKey('Ö')) _keyCodes.Add('Ö', 214);
                if (!_keyCodes.ContainsKey('×')) _keyCodes.Add('×', 215);
                if (!_keyCodes.ContainsKey('Ø')) _keyCodes.Add('Ø', 216);
                if (!_keyCodes.ContainsKey('Ø')) _keyCodes.Add('Ø', 216);
                if (!_keyCodes.ContainsKey('Ù')) _keyCodes.Add('Ù', 217);
                if (!_keyCodes.ContainsKey('Ú')) _keyCodes.Add('Ú', 218);
                if (!_keyCodes.ContainsKey('Û')) _keyCodes.Add('Û', 219);
                if (!_keyCodes.ContainsKey('Ü')) _keyCodes.Add('Ü', 220);
                if (!_keyCodes.ContainsKey('Ý')) _keyCodes.Add('Ý', 221);
                if (!_keyCodes.ContainsKey('Þ')) _keyCodes.Add('Þ', 222);
                if (!_keyCodes.ContainsKey('ß')) _keyCodes.Add('ß', 223);
                if (!_keyCodes.ContainsKey('à')) _keyCodes.Add('à', 224);
                if (!_keyCodes.ContainsKey('á')) _keyCodes.Add('á', 225);
                if (!_keyCodes.ContainsKey('â')) _keyCodes.Add('â', 226);
                if (!_keyCodes.ContainsKey('ã')) _keyCodes.Add('ã', 227);
                if (!_keyCodes.ContainsKey('ä')) _keyCodes.Add('ä', 228);
                if (!_keyCodes.ContainsKey('å')) _keyCodes.Add('å', 229);
                if (!_keyCodes.ContainsKey('æ')) _keyCodes.Add('æ', 230);
                if (!_keyCodes.ContainsKey('ç')) _keyCodes.Add('ç', 231);
                if (!_keyCodes.ContainsKey('è')) _keyCodes.Add('è', 232);
                if (!_keyCodes.ContainsKey('é')) _keyCodes.Add('é', 233);
                if (!_keyCodes.ContainsKey('ê')) _keyCodes.Add('ê', 234);
                if (!_keyCodes.ContainsKey('ë')) _keyCodes.Add('ë', 235);
                if (!_keyCodes.ContainsKey('ì')) _keyCodes.Add('ì', 236);
                if (!_keyCodes.ContainsKey('í')) _keyCodes.Add('í', 237);
                if (!_keyCodes.ContainsKey('î')) _keyCodes.Add('î', 238);
                if (!_keyCodes.ContainsKey('ï')) _keyCodes.Add('ï', 239);
                if (!_keyCodes.ContainsKey('ð')) _keyCodes.Add('ð', 240);
                if (!_keyCodes.ContainsKey('ñ')) _keyCodes.Add('ñ', 241);
                if (!_keyCodes.ContainsKey('ò')) _keyCodes.Add('ò', 242);
                if (!_keyCodes.ContainsKey('ó')) _keyCodes.Add('ó', 243);
                if (!_keyCodes.ContainsKey('ô')) _keyCodes.Add('ô', 244);
                if (!_keyCodes.ContainsKey('õ')) _keyCodes.Add('õ', 245);
                if (!_keyCodes.ContainsKey('ö')) _keyCodes.Add('ö', 246);
                if (!_keyCodes.ContainsKey('÷')) _keyCodes.Add('÷', 247);
                if (!_keyCodes.ContainsKey('ø')) _keyCodes.Add('ø', 248);
                if (!_keyCodes.ContainsKey('ø')) _keyCodes.Add('ø', 248);
                if (!_keyCodes.ContainsKey('ù')) _keyCodes.Add('ù', 249);
                if (!_keyCodes.ContainsKey('ú')) _keyCodes.Add('ú', 250);
                if (!_keyCodes.ContainsKey('û')) _keyCodes.Add('û', 251);
                if (!_keyCodes.ContainsKey('ü')) _keyCodes.Add('ü', 252);
                if (!_keyCodes.ContainsKey('ý')) _keyCodes.Add('ý', 253);
                if (!_keyCodes.ContainsKey('þ')) _keyCodes.Add('þ', 254);
                if (!_keyCodes.ContainsKey('ÿ')) _keyCodes.Add('ÿ', 255);
                if (!_keyCodes.ContainsKey('Ā')) _keyCodes.Add('Ā', 256);
                if (!_keyCodes.ContainsKey('ā')) _keyCodes.Add('ā', 257);
                if (!_keyCodes.ContainsKey('Ă')) _keyCodes.Add('Ă', 258);
                if (!_keyCodes.ContainsKey('ă')) _keyCodes.Add('ă', 259);
                if (!_keyCodes.ContainsKey('Ą')) _keyCodes.Add('Ą', 260);
                if (!_keyCodes.ContainsKey('ą')) _keyCodes.Add('ą', 261);
                if (!_keyCodes.ContainsKey('Ć')) _keyCodes.Add('Ć', 262);
                if (!_keyCodes.ContainsKey('ć')) _keyCodes.Add('ć', 263);
                if (!_keyCodes.ContainsKey('Ĉ')) _keyCodes.Add('Ĉ', 264);
                if (!_keyCodes.ContainsKey('ĉ')) _keyCodes.Add('ĉ', 265);
                if (!_keyCodes.ContainsKey('Ċ')) _keyCodes.Add('Ċ', 266);
                if (!_keyCodes.ContainsKey('ċ')) _keyCodes.Add('ċ', 267);
                if (!_keyCodes.ContainsKey('Č')) _keyCodes.Add('Č', 268);
                if (!_keyCodes.ContainsKey('č')) _keyCodes.Add('č', 269);
                if (!_keyCodes.ContainsKey('Ď')) _keyCodes.Add('Ď', 270);
                if (!_keyCodes.ContainsKey('ď')) _keyCodes.Add('ď', 271);
                if (!_keyCodes.ContainsKey('Đ')) _keyCodes.Add('Đ', 272);
                if (!_keyCodes.ContainsKey('đ')) _keyCodes.Add('đ', 273);
                if (!_keyCodes.ContainsKey('Ē')) _keyCodes.Add('Ē', 274);
                if (!_keyCodes.ContainsKey('ē')) _keyCodes.Add('ē', 275);
                if (!_keyCodes.ContainsKey('Ė')) _keyCodes.Add('Ė', 278);
                if (!_keyCodes.ContainsKey('ė')) _keyCodes.Add('ė', 279);
                if (!_keyCodes.ContainsKey('Ę')) _keyCodes.Add('Ę', 280);
                if (!_keyCodes.ContainsKey('ę')) _keyCodes.Add('ę', 281);
                if (!_keyCodes.ContainsKey('Ě')) _keyCodes.Add('Ě', 282);
                if (!_keyCodes.ContainsKey('ě')) _keyCodes.Add('ě', 283);
                if (!_keyCodes.ContainsKey('Ĝ')) _keyCodes.Add('Ĝ', 284);
                if (!_keyCodes.ContainsKey('ĝ')) _keyCodes.Add('ĝ', 285);
                if (!_keyCodes.ContainsKey('Ğ')) _keyCodes.Add('Ğ', 286);
                if (!_keyCodes.ContainsKey('ğ')) _keyCodes.Add('ğ', 287);
                if (!_keyCodes.ContainsKey('Ġ')) _keyCodes.Add('Ġ', 288);
                if (!_keyCodes.ContainsKey('ġ')) _keyCodes.Add('ġ', 289);
                if (!_keyCodes.ContainsKey('Ģ')) _keyCodes.Add('Ģ', 290);
                if (!_keyCodes.ContainsKey('ģ')) _keyCodes.Add('ģ', 291);
                if (!_keyCodes.ContainsKey('Ĥ')) _keyCodes.Add('Ĥ', 292);
                if (!_keyCodes.ContainsKey('ĥ')) _keyCodes.Add('ĥ', 293);
                if (!_keyCodes.ContainsKey('Ħ')) _keyCodes.Add('Ħ', 294);
                if (!_keyCodes.ContainsKey('ħ')) _keyCodes.Add('ħ', 295);
                if (!_keyCodes.ContainsKey('Ĩ')) _keyCodes.Add('Ĩ', 296);
                if (!_keyCodes.ContainsKey('ĩ')) _keyCodes.Add('ĩ', 297);
                if (!_keyCodes.ContainsKey('Ī')) _keyCodes.Add('Ī', 298);
                if (!_keyCodes.ContainsKey('ī')) _keyCodes.Add('ī', 299);
                if (!_keyCodes.ContainsKey('Ĭ')) _keyCodes.Add('Ĭ', 300);
                if (!_keyCodes.ContainsKey('ĭ')) _keyCodes.Add('ĭ', 301);
                if (!_keyCodes.ContainsKey('Į')) _keyCodes.Add('Į', 302);
                if (!_keyCodes.ContainsKey('į')) _keyCodes.Add('į', 303);
                if (!_keyCodes.ContainsKey('İ')) _keyCodes.Add('İ', 304);
                if (!_keyCodes.ContainsKey('ı')) _keyCodes.Add('ı', 305);
                if (!_keyCodes.ContainsKey('Ĵ')) _keyCodes.Add('Ĵ', 308);
                if (!_keyCodes.ContainsKey('ĵ')) _keyCodes.Add('ĵ', 309);
                if (!_keyCodes.ContainsKey('Ķ')) _keyCodes.Add('Ķ', 310);
                if (!_keyCodes.ContainsKey('ķ')) _keyCodes.Add('ķ', 311);
                if (!_keyCodes.ContainsKey('ĸ')) _keyCodes.Add('ĸ', 312);
                if (!_keyCodes.ContainsKey('Ĺ')) _keyCodes.Add('Ĺ', 313);
                if (!_keyCodes.ContainsKey('ĺ')) _keyCodes.Add('ĺ', 314);
                if (!_keyCodes.ContainsKey('Ļ')) _keyCodes.Add('Ļ', 315);
                if (!_keyCodes.ContainsKey('ļ')) _keyCodes.Add('ļ', 316);
                if (!_keyCodes.ContainsKey('Ľ')) _keyCodes.Add('Ľ', 317);
                if (!_keyCodes.ContainsKey('ľ')) _keyCodes.Add('ľ', 318);
                if (!_keyCodes.ContainsKey('Ł')) _keyCodes.Add('Ł', 321);
                if (!_keyCodes.ContainsKey('ł')) _keyCodes.Add('ł', 322);
                if (!_keyCodes.ContainsKey('Ń')) _keyCodes.Add('Ń', 323);
                if (!_keyCodes.ContainsKey('ń')) _keyCodes.Add('ń', 324);
                if (!_keyCodes.ContainsKey('Ņ')) _keyCodes.Add('Ņ', 325);
                if (!_keyCodes.ContainsKey('ņ')) _keyCodes.Add('ņ', 326);
                if (!_keyCodes.ContainsKey('Ň')) _keyCodes.Add('Ň', 327);
                if (!_keyCodes.ContainsKey('ň')) _keyCodes.Add('ň', 328);
                if (!_keyCodes.ContainsKey('Ŋ')) _keyCodes.Add('Ŋ', 330);
                if (!_keyCodes.ContainsKey('ŋ')) _keyCodes.Add('ŋ', 331);
                if (!_keyCodes.ContainsKey('Ō')) _keyCodes.Add('Ō', 332);
                if (!_keyCodes.ContainsKey('ō')) _keyCodes.Add('ō', 333);
                if (!_keyCodes.ContainsKey('Ő')) _keyCodes.Add('Ő', 336);
                if (!_keyCodes.ContainsKey('ő')) _keyCodes.Add('ő', 337);
                if (!_keyCodes.ContainsKey('Œ')) _keyCodes.Add('Œ', 338);
                if (!_keyCodes.ContainsKey('œ')) _keyCodes.Add('œ', 339);
                if (!_keyCodes.ContainsKey('Ŕ')) _keyCodes.Add('Ŕ', 340);
                if (!_keyCodes.ContainsKey('ŕ')) _keyCodes.Add('ŕ', 341);
                if (!_keyCodes.ContainsKey('Ŗ')) _keyCodes.Add('Ŗ', 342);
                if (!_keyCodes.ContainsKey('ŗ')) _keyCodes.Add('ŗ', 343);
                if (!_keyCodes.ContainsKey('Ř')) _keyCodes.Add('Ř', 344);
                if (!_keyCodes.ContainsKey('ř')) _keyCodes.Add('ř', 345);
                if (!_keyCodes.ContainsKey('Ś')) _keyCodes.Add('Ś', 346);
                if (!_keyCodes.ContainsKey('ś')) _keyCodes.Add('ś', 347);
                if (!_keyCodes.ContainsKey('Ŝ')) _keyCodes.Add('Ŝ', 348);
                if (!_keyCodes.ContainsKey('ŝ')) _keyCodes.Add('ŝ', 349);
                if (!_keyCodes.ContainsKey('Ş')) _keyCodes.Add('Ş', 350);
                if (!_keyCodes.ContainsKey('ş')) _keyCodes.Add('ş', 351);
                if (!_keyCodes.ContainsKey('Š')) _keyCodes.Add('Š', 352);
                if (!_keyCodes.ContainsKey('š')) _keyCodes.Add('š', 353);
                if (!_keyCodes.ContainsKey('Ţ')) _keyCodes.Add('Ţ', 354);
                if (!_keyCodes.ContainsKey('ţ')) _keyCodes.Add('ţ', 355);
                if (!_keyCodes.ContainsKey('Ť')) _keyCodes.Add('Ť', 356);
                if (!_keyCodes.ContainsKey('ť')) _keyCodes.Add('ť', 357);
                if (!_keyCodes.ContainsKey('Ŧ')) _keyCodes.Add('Ŧ', 358);
                if (!_keyCodes.ContainsKey('ŧ')) _keyCodes.Add('ŧ', 359);
                if (!_keyCodes.ContainsKey('Ũ')) _keyCodes.Add('Ũ', 360);
                if (!_keyCodes.ContainsKey('ũ')) _keyCodes.Add('ũ', 361);
                if (!_keyCodes.ContainsKey('Ū')) _keyCodes.Add('Ū', 362);
                if (!_keyCodes.ContainsKey('ū')) _keyCodes.Add('ū', 363);
                if (!_keyCodes.ContainsKey('Ŭ')) _keyCodes.Add('Ŭ', 364);
                if (!_keyCodes.ContainsKey('ŭ')) _keyCodes.Add('ŭ', 365);
                if (!_keyCodes.ContainsKey('Ů')) _keyCodes.Add('Ů', 366);
                if (!_keyCodes.ContainsKey('ů')) _keyCodes.Add('ů', 367);
                if (!_keyCodes.ContainsKey('Ű')) _keyCodes.Add('Ű', 368);
                if (!_keyCodes.ContainsKey('ű')) _keyCodes.Add('ű', 369);
                if (!_keyCodes.ContainsKey('Ų')) _keyCodes.Add('Ų', 370);
                if (!_keyCodes.ContainsKey('ų')) _keyCodes.Add('ų', 371);
                if (!_keyCodes.ContainsKey('Ŵ')) _keyCodes.Add('Ŵ', 372);
                if (!_keyCodes.ContainsKey('ŵ')) _keyCodes.Add('ŵ', 373);
                if (!_keyCodes.ContainsKey('Ŷ')) _keyCodes.Add('Ŷ', 374);
                if (!_keyCodes.ContainsKey('ŷ')) _keyCodes.Add('ŷ', 375);
                if (!_keyCodes.ContainsKey('Ÿ')) _keyCodes.Add('Ÿ', 376);
                if (!_keyCodes.ContainsKey('Ź')) _keyCodes.Add('Ź', 377);
                if (!_keyCodes.ContainsKey('ź')) _keyCodes.Add('ź', 378);
                if (!_keyCodes.ContainsKey('Ż')) _keyCodes.Add('Ż', 379);
                if (!_keyCodes.ContainsKey('ż')) _keyCodes.Add('ż', 380);
                if (!_keyCodes.ContainsKey('Ž')) _keyCodes.Add('Ž', 381);
                if (!_keyCodes.ContainsKey('ž')) _keyCodes.Add('ž', 382);
                if (!_keyCodes.ContainsKey('Ə')) _keyCodes.Add('Ə', 399);
                if (!_keyCodes.ContainsKey('ƒ')) _keyCodes.Add('ƒ', 402);
                if (!_keyCodes.ContainsKey('Ɵ')) _keyCodes.Add('Ɵ', 415);
                if (!_keyCodes.ContainsKey('Ơ')) _keyCodes.Add('Ơ', 416);
                if (!_keyCodes.ContainsKey('ơ')) _keyCodes.Add('ơ', 417);
                if (!_keyCodes.ContainsKey('Ư')) _keyCodes.Add('Ư', 431);
                if (!_keyCodes.ContainsKey('ư')) _keyCodes.Add('ư', 432);
                if (!_keyCodes.ContainsKey('Ƶ')) _keyCodes.Add('Ƶ', 437);
                if (!_keyCodes.ContainsKey('ƶ')) _keyCodes.Add('ƶ', 438);
                if (!_keyCodes.ContainsKey('ǒ')) _keyCodes.Add('ǒ', 466);
                if (!_keyCodes.ContainsKey('ǒ')) _keyCodes.Add('ǒ', 466);
                if (!_keyCodes.ContainsKey('Ǧ')) _keyCodes.Add('Ǧ', 486);
                if (!_keyCodes.ContainsKey('ǧ')) _keyCodes.Add('ǧ', 487);
                if (!_keyCodes.ContainsKey('ə')) _keyCodes.Add('ə', 601);
                if (!_keyCodes.ContainsKey('ɵ')) _keyCodes.Add('ɵ', 629);
                if (!_keyCodes.ContainsKey('ˇ')) _keyCodes.Add('ˇ', 711);
                if (!_keyCodes.ContainsKey('˘')) _keyCodes.Add('˘', 728);
                if (!_keyCodes.ContainsKey('˙')) _keyCodes.Add('˙', 729);
                if (!_keyCodes.ContainsKey('˛')) _keyCodes.Add('˛', 731);
                if (!_keyCodes.ContainsKey('˝')) _keyCodes.Add('˝', 733);
                if (!_keyCodes.ContainsKey('΅')) _keyCodes.Add('΅', 901);
                if (!_keyCodes.ContainsKey('Ά')) _keyCodes.Add('Ά', 902);
                if (!_keyCodes.ContainsKey('Έ')) _keyCodes.Add('Έ', 904);
                if (!_keyCodes.ContainsKey('Ή')) _keyCodes.Add('Ή', 905);
                if (!_keyCodes.ContainsKey('Ί')) _keyCodes.Add('Ί', 906);
                if (!_keyCodes.ContainsKey('Ό')) _keyCodes.Add('Ό', 908);
                if (!_keyCodes.ContainsKey('Ύ')) _keyCodes.Add('Ύ', 910);
                if (!_keyCodes.ContainsKey('Ώ')) _keyCodes.Add('Ώ', 911);
                if (!_keyCodes.ContainsKey('ΐ')) _keyCodes.Add('ΐ', 912);
                if (!_keyCodes.ContainsKey('Α')) _keyCodes.Add('Α', 913);
                if (!_keyCodes.ContainsKey('Β')) _keyCodes.Add('Β', 914);
                if (!_keyCodes.ContainsKey('Γ')) _keyCodes.Add('Γ', 915);
                if (!_keyCodes.ContainsKey('Δ')) _keyCodes.Add('Δ', 916);
                if (!_keyCodes.ContainsKey('Ε')) _keyCodes.Add('Ε', 917);
                if (!_keyCodes.ContainsKey('Ζ')) _keyCodes.Add('Ζ', 918);
                if (!_keyCodes.ContainsKey('Η')) _keyCodes.Add('Η', 919);
                if (!_keyCodes.ContainsKey('Θ')) _keyCodes.Add('Θ', 920);
                if (!_keyCodes.ContainsKey('Ι')) _keyCodes.Add('Ι', 921);
                if (!_keyCodes.ContainsKey('Κ')) _keyCodes.Add('Κ', 922);
                if (!_keyCodes.ContainsKey('Λ')) _keyCodes.Add('Λ', 923);
                if (!_keyCodes.ContainsKey('Λ')) _keyCodes.Add('Λ', 923);
                if (!_keyCodes.ContainsKey('Μ')) _keyCodes.Add('Μ', 924);
                if (!_keyCodes.ContainsKey('Ν')) _keyCodes.Add('Ν', 925);
                if (!_keyCodes.ContainsKey('Ξ')) _keyCodes.Add('Ξ', 926);
                if (!_keyCodes.ContainsKey('Ο')) _keyCodes.Add('Ο', 927);
                if (!_keyCodes.ContainsKey('Π')) _keyCodes.Add('Π', 928);
                if (!_keyCodes.ContainsKey('Ρ')) _keyCodes.Add('Ρ', 929);
                if (!_keyCodes.ContainsKey('Σ')) _keyCodes.Add('Σ', 931);
                if (!_keyCodes.ContainsKey('Τ')) _keyCodes.Add('Τ', 932);
                if (!_keyCodes.ContainsKey('Υ')) _keyCodes.Add('Υ', 933);
                if (!_keyCodes.ContainsKey('Φ')) _keyCodes.Add('Φ', 934);
                if (!_keyCodes.ContainsKey('Χ')) _keyCodes.Add('Χ', 935);
                if (!_keyCodes.ContainsKey('Ψ')) _keyCodes.Add('Ψ', 936);
                if (!_keyCodes.ContainsKey('Ω')) _keyCodes.Add('Ω', 937);
                if (!_keyCodes.ContainsKey('Ϊ')) _keyCodes.Add('Ϊ', 938);
                if (!_keyCodes.ContainsKey('Ϋ')) _keyCodes.Add('Ϋ', 939);
                if (!_keyCodes.ContainsKey('ά')) _keyCodes.Add('ά', 940);
                if (!_keyCodes.ContainsKey('έ')) _keyCodes.Add('έ', 941);
                if (!_keyCodes.ContainsKey('ή')) _keyCodes.Add('ή', 942);
                if (!_keyCodes.ContainsKey('ί')) _keyCodes.Add('ί', 943);
                if (!_keyCodes.ContainsKey('ΰ')) _keyCodes.Add('ΰ', 944);
                if (!_keyCodes.ContainsKey('α')) _keyCodes.Add('α', 945);
                if (!_keyCodes.ContainsKey('β')) _keyCodes.Add('β', 946);
                if (!_keyCodes.ContainsKey('γ')) _keyCodes.Add('γ', 947);
                if (!_keyCodes.ContainsKey('δ')) _keyCodes.Add('δ', 948);
                if (!_keyCodes.ContainsKey('ε')) _keyCodes.Add('ε', 949);
                if (!_keyCodes.ContainsKey('ζ')) _keyCodes.Add('ζ', 950);
                if (!_keyCodes.ContainsKey('η')) _keyCodes.Add('η', 951);
                if (!_keyCodes.ContainsKey('θ')) _keyCodes.Add('θ', 952);
                if (!_keyCodes.ContainsKey('ι')) _keyCodes.Add('ι', 953);
                if (!_keyCodes.ContainsKey('κ')) _keyCodes.Add('κ', 954);
                if (!_keyCodes.ContainsKey('λ')) _keyCodes.Add('λ', 955);
                if (!_keyCodes.ContainsKey('λ')) _keyCodes.Add('λ', 955);
                if (!_keyCodes.ContainsKey('μ')) _keyCodes.Add('μ', 956);
                if (!_keyCodes.ContainsKey('ν')) _keyCodes.Add('ν', 957);
                if (!_keyCodes.ContainsKey('ξ')) _keyCodes.Add('ξ', 958);
                if (!_keyCodes.ContainsKey('ο')) _keyCodes.Add('ο', 959);
                if (!_keyCodes.ContainsKey('π')) _keyCodes.Add('π', 960);
                if (!_keyCodes.ContainsKey('ρ')) _keyCodes.Add('ρ', 961);
                if (!_keyCodes.ContainsKey('ς')) _keyCodes.Add('ς', 962);
                if (!_keyCodes.ContainsKey('σ')) _keyCodes.Add('σ', 963);
                if (!_keyCodes.ContainsKey('τ')) _keyCodes.Add('τ', 964);
                if (!_keyCodes.ContainsKey('υ')) _keyCodes.Add('υ', 965);
                if (!_keyCodes.ContainsKey('φ')) _keyCodes.Add('φ', 966);
                if (!_keyCodes.ContainsKey('χ')) _keyCodes.Add('χ', 967);
                if (!_keyCodes.ContainsKey('ψ')) _keyCodes.Add('ψ', 968);
                if (!_keyCodes.ContainsKey('ω')) _keyCodes.Add('ω', 969);
                if (!_keyCodes.ContainsKey('ϊ')) _keyCodes.Add('ϊ', 970);
                if (!_keyCodes.ContainsKey('ϋ')) _keyCodes.Add('ϋ', 971);
                if (!_keyCodes.ContainsKey('ό')) _keyCodes.Add('ό', 972);
                if (!_keyCodes.ContainsKey('ύ')) _keyCodes.Add('ύ', 973);
                if (!_keyCodes.ContainsKey('ώ')) _keyCodes.Add('ώ', 974);
                if (!_keyCodes.ContainsKey('Ё')) _keyCodes.Add('Ё', 1025);
                if (!_keyCodes.ContainsKey('Ђ')) _keyCodes.Add('Ђ', 1026);
                if (!_keyCodes.ContainsKey('Ѓ')) _keyCodes.Add('Ѓ', 1027);
                if (!_keyCodes.ContainsKey('Є')) _keyCodes.Add('Є', 1028);
                if (!_keyCodes.ContainsKey('Ѕ')) _keyCodes.Add('Ѕ', 1029);
                if (!_keyCodes.ContainsKey('І')) _keyCodes.Add('І', 1030);
                if (!_keyCodes.ContainsKey('Ї')) _keyCodes.Add('Ї', 1031);
                if (!_keyCodes.ContainsKey('Ј')) _keyCodes.Add('Ј', 1032);
                if (!_keyCodes.ContainsKey('Љ')) _keyCodes.Add('Љ', 1033);
                if (!_keyCodes.ContainsKey('Њ')) _keyCodes.Add('Њ', 1034);
                if (!_keyCodes.ContainsKey('Ћ')) _keyCodes.Add('Ћ', 1035);
                if (!_keyCodes.ContainsKey('Ќ')) _keyCodes.Add('Ќ', 1036);
                if (!_keyCodes.ContainsKey('Ў')) _keyCodes.Add('Ў', 1038);
                if (!_keyCodes.ContainsKey('Џ')) _keyCodes.Add('Џ', 1039);
                if (!_keyCodes.ContainsKey('А')) _keyCodes.Add('А', 1040);
                if (!_keyCodes.ContainsKey('Б')) _keyCodes.Add('Б', 1041);
                if (!_keyCodes.ContainsKey('В')) _keyCodes.Add('В', 1042);
                if (!_keyCodes.ContainsKey('Г')) _keyCodes.Add('Г', 1043);
                if (!_keyCodes.ContainsKey('Д')) _keyCodes.Add('Д', 1044);
                if (!_keyCodes.ContainsKey('Е')) _keyCodes.Add('Е', 1045);
                if (!_keyCodes.ContainsKey('Ж')) _keyCodes.Add('Ж', 1046);
                if (!_keyCodes.ContainsKey('З')) _keyCodes.Add('З', 1047);
                if (!_keyCodes.ContainsKey('И')) _keyCodes.Add('И', 1048);
                if (!_keyCodes.ContainsKey('Й')) _keyCodes.Add('Й', 1049);
                if (!_keyCodes.ContainsKey('К')) _keyCodes.Add('К', 1050);
                if (!_keyCodes.ContainsKey('Л')) _keyCodes.Add('Л', 1051);
                if (!_keyCodes.ContainsKey('М')) _keyCodes.Add('М', 1052);
                if (!_keyCodes.ContainsKey('Н')) _keyCodes.Add('Н', 1053);
                if (!_keyCodes.ContainsKey('О')) _keyCodes.Add('О', 1054);
                if (!_keyCodes.ContainsKey('П')) _keyCodes.Add('П', 1055);
                if (!_keyCodes.ContainsKey('Р')) _keyCodes.Add('Р', 1056);
                if (!_keyCodes.ContainsKey('С')) _keyCodes.Add('С', 1057);
                if (!_keyCodes.ContainsKey('Т')) _keyCodes.Add('Т', 1058);
                if (!_keyCodes.ContainsKey('У')) _keyCodes.Add('У', 1059);
                if (!_keyCodes.ContainsKey('Ф')) _keyCodes.Add('Ф', 1060);
                if (!_keyCodes.ContainsKey('Х')) _keyCodes.Add('Х', 1061);
                if (!_keyCodes.ContainsKey('Ц')) _keyCodes.Add('Ц', 1062);
                if (!_keyCodes.ContainsKey('Ч')) _keyCodes.Add('Ч', 1063);
                if (!_keyCodes.ContainsKey('Ш')) _keyCodes.Add('Ш', 1064);
                if (!_keyCodes.ContainsKey('Щ')) _keyCodes.Add('Щ', 1065);
                if (!_keyCodes.ContainsKey('Ъ')) _keyCodes.Add('Ъ', 1066);
                if (!_keyCodes.ContainsKey('Ы')) _keyCodes.Add('Ы', 1067);
                if (!_keyCodes.ContainsKey('Ь')) _keyCodes.Add('Ь', 1068);
                if (!_keyCodes.ContainsKey('Э')) _keyCodes.Add('Э', 1069);
                if (!_keyCodes.ContainsKey('Ю')) _keyCodes.Add('Ю', 1070);
                if (!_keyCodes.ContainsKey('Я')) _keyCodes.Add('Я', 1071);
                if (!_keyCodes.ContainsKey('а')) _keyCodes.Add('а', 1072);
                if (!_keyCodes.ContainsKey('б')) _keyCodes.Add('б', 1073);
                if (!_keyCodes.ContainsKey('в')) _keyCodes.Add('в', 1074);
                if (!_keyCodes.ContainsKey('г')) _keyCodes.Add('г', 1075);
                if (!_keyCodes.ContainsKey('д')) _keyCodes.Add('д', 1076);
                if (!_keyCodes.ContainsKey('е')) _keyCodes.Add('е', 1077);
                if (!_keyCodes.ContainsKey('ж')) _keyCodes.Add('ж', 1078);
                if (!_keyCodes.ContainsKey('з')) _keyCodes.Add('з', 1079);
                if (!_keyCodes.ContainsKey('и')) _keyCodes.Add('и', 1080);
                if (!_keyCodes.ContainsKey('й')) _keyCodes.Add('й', 1081);
                if (!_keyCodes.ContainsKey('к')) _keyCodes.Add('к', 1082);
                if (!_keyCodes.ContainsKey('л')) _keyCodes.Add('л', 1083);
                if (!_keyCodes.ContainsKey('м')) _keyCodes.Add('м', 1084);
                if (!_keyCodes.ContainsKey('н')) _keyCodes.Add('н', 1085);
                if (!_keyCodes.ContainsKey('о')) _keyCodes.Add('о', 1086);
                if (!_keyCodes.ContainsKey('п')) _keyCodes.Add('п', 1087);
                if (!_keyCodes.ContainsKey('р')) _keyCodes.Add('р', 1088);
                if (!_keyCodes.ContainsKey('с')) _keyCodes.Add('с', 1089);
                if (!_keyCodes.ContainsKey('т')) _keyCodes.Add('т', 1090);
                if (!_keyCodes.ContainsKey('у')) _keyCodes.Add('у', 1091);
                if (!_keyCodes.ContainsKey('ф')) _keyCodes.Add('ф', 1092);
                if (!_keyCodes.ContainsKey('х')) _keyCodes.Add('х', 1093);
                if (!_keyCodes.ContainsKey('ц')) _keyCodes.Add('ц', 1094);
                if (!_keyCodes.ContainsKey('ч')) _keyCodes.Add('ч', 1095);
                if (!_keyCodes.ContainsKey('ш')) _keyCodes.Add('ш', 1096);
                if (!_keyCodes.ContainsKey('щ')) _keyCodes.Add('щ', 1097);
                if (!_keyCodes.ContainsKey('ъ')) _keyCodes.Add('ъ', 1098);
                if (!_keyCodes.ContainsKey('ы')) _keyCodes.Add('ы', 1099);
                if (!_keyCodes.ContainsKey('ь')) _keyCodes.Add('ь', 1100);
                if (!_keyCodes.ContainsKey('э')) _keyCodes.Add('э', 1101);
                if (!_keyCodes.ContainsKey('ю')) _keyCodes.Add('ю', 1102);
                if (!_keyCodes.ContainsKey('я')) _keyCodes.Add('я', 1103);
                if (!_keyCodes.ContainsKey('ё')) _keyCodes.Add('ё', 1105);
                if (!_keyCodes.ContainsKey('ђ')) _keyCodes.Add('ђ', 1106);
                if (!_keyCodes.ContainsKey('ѓ')) _keyCodes.Add('ѓ', 1107);
                if (!_keyCodes.ContainsKey('є')) _keyCodes.Add('є', 1108);
                if (!_keyCodes.ContainsKey('ѕ')) _keyCodes.Add('ѕ', 1109);
                if (!_keyCodes.ContainsKey('і')) _keyCodes.Add('і', 1110);
                if (!_keyCodes.ContainsKey('ї')) _keyCodes.Add('ї', 1111);
                if (!_keyCodes.ContainsKey('ј')) _keyCodes.Add('ј', 1112);
                if (!_keyCodes.ContainsKey('љ')) _keyCodes.Add('љ', 1113);
                if (!_keyCodes.ContainsKey('њ')) _keyCodes.Add('њ', 1114);
                if (!_keyCodes.ContainsKey('ћ')) _keyCodes.Add('ћ', 1115);
                if (!_keyCodes.ContainsKey('ќ')) _keyCodes.Add('ќ', 1116);
                if (!_keyCodes.ContainsKey('ў')) _keyCodes.Add('ў', 1118);
                if (!_keyCodes.ContainsKey('џ')) _keyCodes.Add('џ', 1119);
                if (!_keyCodes.ContainsKey('Ґ')) _keyCodes.Add('Ґ', 1168);
                if (!_keyCodes.ContainsKey('ґ')) _keyCodes.Add('ґ', 1169);
                if (!_keyCodes.ContainsKey('Ғ')) _keyCodes.Add('Ғ', 1170);
                if (!_keyCodes.ContainsKey('ғ')) _keyCodes.Add('ғ', 1171);
                if (!_keyCodes.ContainsKey('Җ')) _keyCodes.Add('Җ', 1174);
                if (!_keyCodes.ContainsKey('җ')) _keyCodes.Add('җ', 1175);
                if (!_keyCodes.ContainsKey('Қ')) _keyCodes.Add('Қ', 1178);
                if (!_keyCodes.ContainsKey('қ')) _keyCodes.Add('қ', 1179);
                if (!_keyCodes.ContainsKey('Ҝ')) _keyCodes.Add('Ҝ', 1180);
                if (!_keyCodes.ContainsKey('ҝ')) _keyCodes.Add('ҝ', 1181);
                if (!_keyCodes.ContainsKey('Ң')) _keyCodes.Add('Ң', 1186);
                if (!_keyCodes.ContainsKey('ң')) _keyCodes.Add('ң', 1187);
                if (!_keyCodes.ContainsKey('Ү')) _keyCodes.Add('Ү', 1198);
                if (!_keyCodes.ContainsKey('ү')) _keyCodes.Add('ү', 1199);
                if (!_keyCodes.ContainsKey('Ұ')) _keyCodes.Add('Ұ', 1200);
                if (!_keyCodes.ContainsKey('ұ')) _keyCodes.Add('ұ', 1201);
                if (!_keyCodes.ContainsKey('Ҳ')) _keyCodes.Add('Ҳ', 1202);
                if (!_keyCodes.ContainsKey('ҳ')) _keyCodes.Add('ҳ', 1203);
                if (!_keyCodes.ContainsKey('Ҷ')) _keyCodes.Add('Ҷ', 1206);
                if (!_keyCodes.ContainsKey('ҷ')) _keyCodes.Add('ҷ', 1207);
                if (!_keyCodes.ContainsKey('Ҹ')) _keyCodes.Add('Ҹ', 1208);
                if (!_keyCodes.ContainsKey('ҹ')) _keyCodes.Add('ҹ', 1209);
                if (!_keyCodes.ContainsKey('Һ')) _keyCodes.Add('Һ', 1210);
                if (!_keyCodes.ContainsKey('һ')) _keyCodes.Add('һ', 1211);
                if (!_keyCodes.ContainsKey('Ә')) _keyCodes.Add('Ә', 1240);
                if (!_keyCodes.ContainsKey('ә')) _keyCodes.Add('ә', 1241);
                if (!_keyCodes.ContainsKey('Ӣ')) _keyCodes.Add('Ӣ', 1250);
                if (!_keyCodes.ContainsKey('ӣ')) _keyCodes.Add('ӣ', 1251);
                if (!_keyCodes.ContainsKey('Ө')) _keyCodes.Add('Ө', 1256);
                if (!_keyCodes.ContainsKey('ө')) _keyCodes.Add('ө', 1257);
                if (!_keyCodes.ContainsKey('Ӯ')) _keyCodes.Add('Ӯ', 1262);
                if (!_keyCodes.ContainsKey('ӯ')) _keyCodes.Add('ӯ', 1263);
                if (!_keyCodes.ContainsKey('Ա')) _keyCodes.Add('Ա', 1329);
                if (!_keyCodes.ContainsKey('Բ')) _keyCodes.Add('Բ', 1330);
                if (!_keyCodes.ContainsKey('Գ')) _keyCodes.Add('Գ', 1331);
                if (!_keyCodes.ContainsKey('Դ')) _keyCodes.Add('Դ', 1332);
                if (!_keyCodes.ContainsKey('Ե')) _keyCodes.Add('Ե', 1333);
                if (!_keyCodes.ContainsKey('Զ')) _keyCodes.Add('Զ', 1334);
                if (!_keyCodes.ContainsKey('Է')) _keyCodes.Add('Է', 1335);
                if (!_keyCodes.ContainsKey('Ը')) _keyCodes.Add('Ը', 1336);
                if (!_keyCodes.ContainsKey('Թ')) _keyCodes.Add('Թ', 1337);
                if (!_keyCodes.ContainsKey('Ժ')) _keyCodes.Add('Ժ', 1338);
                if (!_keyCodes.ContainsKey('Ի')) _keyCodes.Add('Ի', 1339);
                if (!_keyCodes.ContainsKey('Լ')) _keyCodes.Add('Լ', 1340);
                if (!_keyCodes.ContainsKey('Խ')) _keyCodes.Add('Խ', 1341);
                if (!_keyCodes.ContainsKey('Ծ')) _keyCodes.Add('Ծ', 1342);
                if (!_keyCodes.ContainsKey('Կ')) _keyCodes.Add('Կ', 1343);
                if (!_keyCodes.ContainsKey('Հ')) _keyCodes.Add('Հ', 1344);
                if (!_keyCodes.ContainsKey('Ձ')) _keyCodes.Add('Ձ', 1345);
                if (!_keyCodes.ContainsKey('Ղ')) _keyCodes.Add('Ղ', 1346);
                if (!_keyCodes.ContainsKey('Ճ')) _keyCodes.Add('Ճ', 1347);
                if (!_keyCodes.ContainsKey('Մ')) _keyCodes.Add('Մ', 1348);
                if (!_keyCodes.ContainsKey('Յ')) _keyCodes.Add('Յ', 1349);
                if (!_keyCodes.ContainsKey('Ն')) _keyCodes.Add('Ն', 1350);
                if (!_keyCodes.ContainsKey('Շ')) _keyCodes.Add('Շ', 1351);
                if (!_keyCodes.ContainsKey('Ո')) _keyCodes.Add('Ո', 1352);
                if (!_keyCodes.ContainsKey('Չ')) _keyCodes.Add('Չ', 1353);
                if (!_keyCodes.ContainsKey('Պ')) _keyCodes.Add('Պ', 1354);
                if (!_keyCodes.ContainsKey('Ջ')) _keyCodes.Add('Ջ', 1355);
                if (!_keyCodes.ContainsKey('Ռ')) _keyCodes.Add('Ռ', 1356);
                if (!_keyCodes.ContainsKey('Ս')) _keyCodes.Add('Ս', 1357);
                if (!_keyCodes.ContainsKey('Վ')) _keyCodes.Add('Վ', 1358);
                if (!_keyCodes.ContainsKey('Տ')) _keyCodes.Add('Տ', 1359);
                if (!_keyCodes.ContainsKey('Ր')) _keyCodes.Add('Ր', 1360);
                if (!_keyCodes.ContainsKey('Ց')) _keyCodes.Add('Ց', 1361);
                if (!_keyCodes.ContainsKey('Ւ')) _keyCodes.Add('Ւ', 1362);
                if (!_keyCodes.ContainsKey('Փ')) _keyCodes.Add('Փ', 1363);
                if (!_keyCodes.ContainsKey('Ք')) _keyCodes.Add('Ք', 1364);
                if (!_keyCodes.ContainsKey('Օ')) _keyCodes.Add('Օ', 1365);
                if (!_keyCodes.ContainsKey('Ֆ')) _keyCodes.Add('Ֆ', 1366);
                if (!_keyCodes.ContainsKey('՚')) _keyCodes.Add('՚', 1370);
                if (!_keyCodes.ContainsKey('՛')) _keyCodes.Add('՛', 1371);
                if (!_keyCodes.ContainsKey('՛')) _keyCodes.Add('՛', 1371);
                if (!_keyCodes.ContainsKey('՜')) _keyCodes.Add('՜', 1372);
                if (!_keyCodes.ContainsKey('՜')) _keyCodes.Add('՜', 1372);
                if (!_keyCodes.ContainsKey('՝')) _keyCodes.Add('՝', 1373);
                if (!_keyCodes.ContainsKey('՝')) _keyCodes.Add('՝', 1373);
                if (!_keyCodes.ContainsKey('՞')) _keyCodes.Add('՞', 1374);
                if (!_keyCodes.ContainsKey('՞')) _keyCodes.Add('՞', 1374);
                if (!_keyCodes.ContainsKey('ա')) _keyCodes.Add('ա', 1377);
                if (!_keyCodes.ContainsKey('բ')) _keyCodes.Add('բ', 1378);
                if (!_keyCodes.ContainsKey('գ')) _keyCodes.Add('գ', 1379);
                if (!_keyCodes.ContainsKey('դ')) _keyCodes.Add('դ', 1380);
                if (!_keyCodes.ContainsKey('ե')) _keyCodes.Add('ե', 1381);
                if (!_keyCodes.ContainsKey('զ')) _keyCodes.Add('զ', 1382);
                if (!_keyCodes.ContainsKey('է')) _keyCodes.Add('է', 1383);
                if (!_keyCodes.ContainsKey('ը')) _keyCodes.Add('ը', 1384);
                if (!_keyCodes.ContainsKey('թ')) _keyCodes.Add('թ', 1385);
                if (!_keyCodes.ContainsKey('ժ')) _keyCodes.Add('ժ', 1386);
                if (!_keyCodes.ContainsKey('ի')) _keyCodes.Add('ի', 1387);
                if (!_keyCodes.ContainsKey('լ')) _keyCodes.Add('լ', 1388);
                if (!_keyCodes.ContainsKey('խ')) _keyCodes.Add('խ', 1389);
                if (!_keyCodes.ContainsKey('ծ')) _keyCodes.Add('ծ', 1390);
                if (!_keyCodes.ContainsKey('կ')) _keyCodes.Add('կ', 1391);
                if (!_keyCodes.ContainsKey('հ')) _keyCodes.Add('հ', 1392);
                if (!_keyCodes.ContainsKey('ձ')) _keyCodes.Add('ձ', 1393);
                if (!_keyCodes.ContainsKey('ղ')) _keyCodes.Add('ղ', 1394);
                if (!_keyCodes.ContainsKey('ճ')) _keyCodes.Add('ճ', 1395);
                if (!_keyCodes.ContainsKey('մ')) _keyCodes.Add('մ', 1396);
                if (!_keyCodes.ContainsKey('յ')) _keyCodes.Add('յ', 1397);
                if (!_keyCodes.ContainsKey('ն')) _keyCodes.Add('ն', 1398);
                if (!_keyCodes.ContainsKey('շ')) _keyCodes.Add('շ', 1399);
                if (!_keyCodes.ContainsKey('ո')) _keyCodes.Add('ո', 1400);
                if (!_keyCodes.ContainsKey('չ')) _keyCodes.Add('չ', 1401);
                if (!_keyCodes.ContainsKey('պ')) _keyCodes.Add('պ', 1402);
                if (!_keyCodes.ContainsKey('ջ')) _keyCodes.Add('ջ', 1403);
                if (!_keyCodes.ContainsKey('ռ')) _keyCodes.Add('ռ', 1404);
                if (!_keyCodes.ContainsKey('ս')) _keyCodes.Add('ս', 1405);
                if (!_keyCodes.ContainsKey('վ')) _keyCodes.Add('վ', 1406);
                if (!_keyCodes.ContainsKey('տ')) _keyCodes.Add('տ', 1407);
                if (!_keyCodes.ContainsKey('ր')) _keyCodes.Add('ր', 1408);
                if (!_keyCodes.ContainsKey('ց')) _keyCodes.Add('ց', 1409);
                if (!_keyCodes.ContainsKey('ւ')) _keyCodes.Add('ւ', 1410);
                if (!_keyCodes.ContainsKey('փ')) _keyCodes.Add('փ', 1411);
                if (!_keyCodes.ContainsKey('ք')) _keyCodes.Add('ք', 1412);
                if (!_keyCodes.ContainsKey('օ')) _keyCodes.Add('օ', 1413);
                if (!_keyCodes.ContainsKey('ֆ')) _keyCodes.Add('ֆ', 1414);
                if (!_keyCodes.ContainsKey('և')) _keyCodes.Add('և', 1415);
                if (!_keyCodes.ContainsKey('։')) _keyCodes.Add('։', 1417);
                if (!_keyCodes.ContainsKey('։')) _keyCodes.Add('։', 1417);
                if (!_keyCodes.ContainsKey('֊')) _keyCodes.Add('֊', 1418);
                if (!_keyCodes.ContainsKey('֊')) _keyCodes.Add('֊', 1418);
                if (!_keyCodes.ContainsKey('א')) _keyCodes.Add('א', 1488);
                if (!_keyCodes.ContainsKey('ב')) _keyCodes.Add('ב', 1489);
                if (!_keyCodes.ContainsKey('ג')) _keyCodes.Add('ג', 1490);
                if (!_keyCodes.ContainsKey('ד')) _keyCodes.Add('ד', 1491);
                if (!_keyCodes.ContainsKey('ה')) _keyCodes.Add('ה', 1492);
                if (!_keyCodes.ContainsKey('ו')) _keyCodes.Add('ו', 1493);
                if (!_keyCodes.ContainsKey('ז')) _keyCodes.Add('ז', 1494);
                if (!_keyCodes.ContainsKey('ח')) _keyCodes.Add('ח', 1495);
                if (!_keyCodes.ContainsKey('ט')) _keyCodes.Add('ט', 1496);
                if (!_keyCodes.ContainsKey('י')) _keyCodes.Add('י', 1497);
                if (!_keyCodes.ContainsKey('ך')) _keyCodes.Add('ך', 1498);
                if (!_keyCodes.ContainsKey('כ')) _keyCodes.Add('כ', 1499);
                if (!_keyCodes.ContainsKey('ל')) _keyCodes.Add('ל', 1500);
                if (!_keyCodes.ContainsKey('ם')) _keyCodes.Add('ם', 1501);
                if (!_keyCodes.ContainsKey('מ')) _keyCodes.Add('מ', 1502);
                if (!_keyCodes.ContainsKey('ן')) _keyCodes.Add('ן', 1503);
                if (!_keyCodes.ContainsKey('נ')) _keyCodes.Add('נ', 1504);
                if (!_keyCodes.ContainsKey('ס')) _keyCodes.Add('ס', 1505);
                if (!_keyCodes.ContainsKey('ע')) _keyCodes.Add('ע', 1506);
                if (!_keyCodes.ContainsKey('ף')) _keyCodes.Add('ף', 1507);
                if (!_keyCodes.ContainsKey('פ')) _keyCodes.Add('פ', 1508);
                if (!_keyCodes.ContainsKey('ץ')) _keyCodes.Add('ץ', 1509);
                if (!_keyCodes.ContainsKey('צ')) _keyCodes.Add('צ', 1510);
                if (!_keyCodes.ContainsKey('ק')) _keyCodes.Add('ק', 1511);
                if (!_keyCodes.ContainsKey('ר')) _keyCodes.Add('ר', 1512);
                if (!_keyCodes.ContainsKey('ש')) _keyCodes.Add('ש', 1513);
                if (!_keyCodes.ContainsKey('ת')) _keyCodes.Add('ת', 1514);
                if (!_keyCodes.ContainsKey('،')) _keyCodes.Add('،', 1548);
                if (!_keyCodes.ContainsKey('؛')) _keyCodes.Add('؛', 1563);
                if (!_keyCodes.ContainsKey('؟')) _keyCodes.Add('؟', 1567);
                if (!_keyCodes.ContainsKey('ء')) _keyCodes.Add('ء', 1569);
                if (!_keyCodes.ContainsKey('آ')) _keyCodes.Add('آ', 1570);
                if (!_keyCodes.ContainsKey('أ')) _keyCodes.Add('أ', 1571);
                if (!_keyCodes.ContainsKey('ؤ')) _keyCodes.Add('ؤ', 1572);
                if (!_keyCodes.ContainsKey('إ')) _keyCodes.Add('إ', 1573);
                if (!_keyCodes.ContainsKey('ئ')) _keyCodes.Add('ئ', 1574);
                if (!_keyCodes.ContainsKey('ا')) _keyCodes.Add('ا', 1575);
                if (!_keyCodes.ContainsKey('ب')) _keyCodes.Add('ب', 1576);
                if (!_keyCodes.ContainsKey('ة')) _keyCodes.Add('ة', 1577);
                if (!_keyCodes.ContainsKey('ت')) _keyCodes.Add('ت', 1578);
                if (!_keyCodes.ContainsKey('ث')) _keyCodes.Add('ث', 1579);
                if (!_keyCodes.ContainsKey('ج')) _keyCodes.Add('ج', 1580);
                if (!_keyCodes.ContainsKey('ح')) _keyCodes.Add('ح', 1581);
                if (!_keyCodes.ContainsKey('خ')) _keyCodes.Add('خ', 1582);
                if (!_keyCodes.ContainsKey('د')) _keyCodes.Add('د', 1583);
                if (!_keyCodes.ContainsKey('ذ')) _keyCodes.Add('ذ', 1584);
                if (!_keyCodes.ContainsKey('ر')) _keyCodes.Add('ر', 1585);
                if (!_keyCodes.ContainsKey('ز')) _keyCodes.Add('ز', 1586);
                if (!_keyCodes.ContainsKey('س')) _keyCodes.Add('س', 1587);
                if (!_keyCodes.ContainsKey('ش')) _keyCodes.Add('ش', 1588);
                if (!_keyCodes.ContainsKey('ص')) _keyCodes.Add('ص', 1589);
                if (!_keyCodes.ContainsKey('ض')) _keyCodes.Add('ض', 1590);
                if (!_keyCodes.ContainsKey('ط')) _keyCodes.Add('ط', 1591);
                if (!_keyCodes.ContainsKey('ظ')) _keyCodes.Add('ظ', 1592);
                if (!_keyCodes.ContainsKey('ع')) _keyCodes.Add('ع', 1593);
                if (!_keyCodes.ContainsKey('غ')) _keyCodes.Add('غ', 1594);
                if (!_keyCodes.ContainsKey('ـ')) _keyCodes.Add('ـ', 1600);
                if (!_keyCodes.ContainsKey('ف')) _keyCodes.Add('ف', 1601);
                if (!_keyCodes.ContainsKey('ق')) _keyCodes.Add('ق', 1602);
                if (!_keyCodes.ContainsKey('ك')) _keyCodes.Add('ك', 1603);
                if (!_keyCodes.ContainsKey('ل')) _keyCodes.Add('ل', 1604);
                if (!_keyCodes.ContainsKey('م')) _keyCodes.Add('م', 1605);
                if (!_keyCodes.ContainsKey('ن')) _keyCodes.Add('ن', 1606);
                if (!_keyCodes.ContainsKey('ه')) _keyCodes.Add('ه', 1607);
                if (!_keyCodes.ContainsKey('و')) _keyCodes.Add('و', 1608);
                if (!_keyCodes.ContainsKey('ى')) _keyCodes.Add('ى', 1609);
                if (!_keyCodes.ContainsKey('ي')) _keyCodes.Add('ي', 1610);
                if (!_keyCodes.ContainsKey('ً')) _keyCodes.Add('ً', 1611);
                if (!_keyCodes.ContainsKey('ٌ')) _keyCodes.Add('ٌ', 1612);
                if (!_keyCodes.ContainsKey('ٍ')) _keyCodes.Add('ٍ', 1613);
                if (!_keyCodes.ContainsKey('َ')) _keyCodes.Add('َ', 1614);
                if (!_keyCodes.ContainsKey('ُ')) _keyCodes.Add('ُ', 1615);
                if (!_keyCodes.ContainsKey('ِ')) _keyCodes.Add('ِ', 1616);
                if (!_keyCodes.ContainsKey('ّ')) _keyCodes.Add('ّ', 1617);
                if (!_keyCodes.ContainsKey('ْ')) _keyCodes.Add('ْ', 1618);
                if (!_keyCodes.ContainsKey('ٓ')) _keyCodes.Add('ٓ', 1619);
                if (!_keyCodes.ContainsKey('ٔ')) _keyCodes.Add('ٔ', 1620);
                if (!_keyCodes.ContainsKey('ٕ')) _keyCodes.Add('ٕ', 1621);
                if (!_keyCodes.ContainsKey('0')) _keyCodes.Add('0', 1632);
                if (!_keyCodes.ContainsKey('1')) _keyCodes.Add('1', 1633);
                if (!_keyCodes.ContainsKey('2')) _keyCodes.Add('2', 1634);
                if (!_keyCodes.ContainsKey('3')) _keyCodes.Add('3', 1635);
                if (!_keyCodes.ContainsKey('4')) _keyCodes.Add('4', 1636);
                if (!_keyCodes.ContainsKey('5')) _keyCodes.Add('5', 1637);
                if (!_keyCodes.ContainsKey('6')) _keyCodes.Add('6', 1638);
                if (!_keyCodes.ContainsKey('7')) _keyCodes.Add('7', 1639);
                if (!_keyCodes.ContainsKey('8')) _keyCodes.Add('8', 1640);
                if (!_keyCodes.ContainsKey('9')) _keyCodes.Add('9', 1641);
                if (!_keyCodes.ContainsKey('٪')) _keyCodes.Add('٪', 1642);
                if (!_keyCodes.ContainsKey('ٰ')) _keyCodes.Add('ٰ', 1648);
                if (!_keyCodes.ContainsKey('ٹ')) _keyCodes.Add('ٹ', 1657);
                if (!_keyCodes.ContainsKey('پ')) _keyCodes.Add('پ', 1662);
                if (!_keyCodes.ContainsKey('چ')) _keyCodes.Add('چ', 1670);
                if (!_keyCodes.ContainsKey('ڈ')) _keyCodes.Add('ڈ', 1672);
                if (!_keyCodes.ContainsKey('ڑ')) _keyCodes.Add('ڑ', 1681);
                if (!_keyCodes.ContainsKey('ژ')) _keyCodes.Add('ژ', 1688);
                if (!_keyCodes.ContainsKey('ڤ')) _keyCodes.Add('ڤ', 1700);
                if (!_keyCodes.ContainsKey('ک')) _keyCodes.Add('ک', 1705);
                if (!_keyCodes.ContainsKey('گ')) _keyCodes.Add('گ', 1711);
                if (!_keyCodes.ContainsKey('ں')) _keyCodes.Add('ں', 1722);
                if (!_keyCodes.ContainsKey('ھ')) _keyCodes.Add('ھ', 1726);
                if (!_keyCodes.ContainsKey('ہ')) _keyCodes.Add('ہ', 1729);
                if (!_keyCodes.ContainsKey('ی')) _keyCodes.Add('ی', 1740);
                if (!_keyCodes.ContainsKey('ی')) _keyCodes.Add('ی', 1740);
                if (!_keyCodes.ContainsKey('ے')) _keyCodes.Add('ے', 1746);
                if (!_keyCodes.ContainsKey('۔')) _keyCodes.Add('۔', 1748);
                if (!_keyCodes.ContainsKey('0')) _keyCodes.Add('0', 1776);
                if (!_keyCodes.ContainsKey('1')) _keyCodes.Add('1', 1777);
                if (!_keyCodes.ContainsKey('2')) _keyCodes.Add('2', 1778);
                if (!_keyCodes.ContainsKey('3')) _keyCodes.Add('3', 1779);
                if (!_keyCodes.ContainsKey('4')) _keyCodes.Add('4', 1780);
                if (!_keyCodes.ContainsKey('5')) _keyCodes.Add('5', 1781);
                if (!_keyCodes.ContainsKey('6')) _keyCodes.Add('6', 1782);
                if (!_keyCodes.ContainsKey('7')) _keyCodes.Add('7', 1783);
                if (!_keyCodes.ContainsKey('8')) _keyCodes.Add('8', 1784);
                if (!_keyCodes.ContainsKey('9')) _keyCodes.Add('9', 1785);
                if (!_keyCodes.ContainsKey('ก')) _keyCodes.Add('ก', 3585);
                if (!_keyCodes.ContainsKey('ข')) _keyCodes.Add('ข', 3586);
                if (!_keyCodes.ContainsKey('ฃ')) _keyCodes.Add('ฃ', 3587);
                if (!_keyCodes.ContainsKey('ค')) _keyCodes.Add('ค', 3588);
                if (!_keyCodes.ContainsKey('ฅ')) _keyCodes.Add('ฅ', 3589);
                if (!_keyCodes.ContainsKey('ฆ')) _keyCodes.Add('ฆ', 3590);
                if (!_keyCodes.ContainsKey('ง')) _keyCodes.Add('ง', 3591);
                if (!_keyCodes.ContainsKey('จ')) _keyCodes.Add('จ', 3592);
                if (!_keyCodes.ContainsKey('ฉ')) _keyCodes.Add('ฉ', 3593);
                if (!_keyCodes.ContainsKey('ช')) _keyCodes.Add('ช', 3594);
                if (!_keyCodes.ContainsKey('ซ')) _keyCodes.Add('ซ', 3595);
                if (!_keyCodes.ContainsKey('ฌ')) _keyCodes.Add('ฌ', 3596);
                if (!_keyCodes.ContainsKey('ญ')) _keyCodes.Add('ญ', 3597);
                if (!_keyCodes.ContainsKey('ฎ')) _keyCodes.Add('ฎ', 3598);
                if (!_keyCodes.ContainsKey('ฏ')) _keyCodes.Add('ฏ', 3599);
                if (!_keyCodes.ContainsKey('ฐ')) _keyCodes.Add('ฐ', 3600);
                if (!_keyCodes.ContainsKey('ฑ')) _keyCodes.Add('ฑ', 3601);
                if (!_keyCodes.ContainsKey('ฒ')) _keyCodes.Add('ฒ', 3602);
                if (!_keyCodes.ContainsKey('ณ')) _keyCodes.Add('ณ', 3603);
                if (!_keyCodes.ContainsKey('ด')) _keyCodes.Add('ด', 3604);
                if (!_keyCodes.ContainsKey('ต')) _keyCodes.Add('ต', 3605);
                if (!_keyCodes.ContainsKey('ถ')) _keyCodes.Add('ถ', 3606);
                if (!_keyCodes.ContainsKey('ท')) _keyCodes.Add('ท', 3607);
                if (!_keyCodes.ContainsKey('ธ')) _keyCodes.Add('ธ', 3608);
                if (!_keyCodes.ContainsKey('น')) _keyCodes.Add('น', 3609);
                if (!_keyCodes.ContainsKey('บ')) _keyCodes.Add('บ', 3610);
                if (!_keyCodes.ContainsKey('ป')) _keyCodes.Add('ป', 3611);
                if (!_keyCodes.ContainsKey('ผ')) _keyCodes.Add('ผ', 3612);
                if (!_keyCodes.ContainsKey('ฝ')) _keyCodes.Add('ฝ', 3613);
                if (!_keyCodes.ContainsKey('พ')) _keyCodes.Add('พ', 3614);
                if (!_keyCodes.ContainsKey('ฟ')) _keyCodes.Add('ฟ', 3615);
                if (!_keyCodes.ContainsKey('ภ')) _keyCodes.Add('ภ', 3616);
                if (!_keyCodes.ContainsKey('ม')) _keyCodes.Add('ม', 3617);
                if (!_keyCodes.ContainsKey('ย')) _keyCodes.Add('ย', 3618);
                if (!_keyCodes.ContainsKey('ร')) _keyCodes.Add('ร', 3619);
                if (!_keyCodes.ContainsKey('ฤ')) _keyCodes.Add('ฤ', 3620);
                if (!_keyCodes.ContainsKey('ล')) _keyCodes.Add('ล', 3621);
                if (!_keyCodes.ContainsKey('ฦ')) _keyCodes.Add('ฦ', 3622);
                if (!_keyCodes.ContainsKey('ว')) _keyCodes.Add('ว', 3623);
                if (!_keyCodes.ContainsKey('ศ')) _keyCodes.Add('ศ', 3624);
                if (!_keyCodes.ContainsKey('ษ')) _keyCodes.Add('ษ', 3625);
                if (!_keyCodes.ContainsKey('ส')) _keyCodes.Add('ส', 3626);
                if (!_keyCodes.ContainsKey('ห')) _keyCodes.Add('ห', 3627);
                if (!_keyCodes.ContainsKey('ฬ')) _keyCodes.Add('ฬ', 3628);
                if (!_keyCodes.ContainsKey('อ')) _keyCodes.Add('อ', 3629);
                if (!_keyCodes.ContainsKey('ฮ')) _keyCodes.Add('ฮ', 3630);
                if (!_keyCodes.ContainsKey('ฯ')) _keyCodes.Add('ฯ', 3631);
                if (!_keyCodes.ContainsKey('ะ')) _keyCodes.Add('ะ', 3632);
                if (!_keyCodes.ContainsKey('ั')) _keyCodes.Add('ั', 3633);
                if (!_keyCodes.ContainsKey('า')) _keyCodes.Add('า', 3634);
                if (!_keyCodes.ContainsKey('ำ')) _keyCodes.Add('ำ', 3635);
                if (!_keyCodes.ContainsKey('ิ')) _keyCodes.Add('ิ', 3636);
                if (!_keyCodes.ContainsKey('ี')) _keyCodes.Add('ี', 3637);
                if (!_keyCodes.ContainsKey('ึ')) _keyCodes.Add('ึ', 3638);
                if (!_keyCodes.ContainsKey('ื')) _keyCodes.Add('ื', 3639);
                if (!_keyCodes.ContainsKey('ุ')) _keyCodes.Add('ุ', 3640);
                if (!_keyCodes.ContainsKey('ู')) _keyCodes.Add('ู', 3641);
                if (!_keyCodes.ContainsKey('ฺ')) _keyCodes.Add('ฺ', 3642);
                if (!_keyCodes.ContainsKey('฿')) _keyCodes.Add('฿', 3647);
                if (!_keyCodes.ContainsKey('เ')) _keyCodes.Add('เ', 3648);
                if (!_keyCodes.ContainsKey('แ')) _keyCodes.Add('แ', 3649);
                if (!_keyCodes.ContainsKey('โ')) _keyCodes.Add('โ', 3650);
                if (!_keyCodes.ContainsKey('ใ')) _keyCodes.Add('ใ', 3651);
                if (!_keyCodes.ContainsKey('ไ')) _keyCodes.Add('ไ', 3652);
                if (!_keyCodes.ContainsKey('ๅ')) _keyCodes.Add('ๅ', 3653);
                if (!_keyCodes.ContainsKey('ๆ')) _keyCodes.Add('ๆ', 3654);
                if (!_keyCodes.ContainsKey('็')) _keyCodes.Add('็', 3655);
                if (!_keyCodes.ContainsKey('่')) _keyCodes.Add('่', 3656);
                if (!_keyCodes.ContainsKey('้')) _keyCodes.Add('้', 3657);
                if (!_keyCodes.ContainsKey('๊')) _keyCodes.Add('๊', 3658);
                if (!_keyCodes.ContainsKey('๋')) _keyCodes.Add('๋', 3659);
                if (!_keyCodes.ContainsKey('์')) _keyCodes.Add('์', 3660);
                if (!_keyCodes.ContainsKey('ํ')) _keyCodes.Add('ํ', 3661);
                if (!_keyCodes.ContainsKey('0')) _keyCodes.Add('0', 3664);
                if (!_keyCodes.ContainsKey('1')) _keyCodes.Add('1', 3665);
                if (!_keyCodes.ContainsKey('2')) _keyCodes.Add('2', 3666);
                if (!_keyCodes.ContainsKey('3')) _keyCodes.Add('3', 3667);
                if (!_keyCodes.ContainsKey('4')) _keyCodes.Add('4', 3668);
                if (!_keyCodes.ContainsKey('5')) _keyCodes.Add('5', 3669);
                if (!_keyCodes.ContainsKey('6')) _keyCodes.Add('6', 3670);
                if (!_keyCodes.ContainsKey('7')) _keyCodes.Add('7', 3671);
                if (!_keyCodes.ContainsKey('8')) _keyCodes.Add('8', 3672);
                if (!_keyCodes.ContainsKey('9')) _keyCodes.Add('9', 3673);
                if (!_keyCodes.ContainsKey('ა')) _keyCodes.Add('ა', 4304);
                if (!_keyCodes.ContainsKey('ბ')) _keyCodes.Add('ბ', 4305);
                if (!_keyCodes.ContainsKey('გ')) _keyCodes.Add('გ', 4306);
                if (!_keyCodes.ContainsKey('დ')) _keyCodes.Add('დ', 4307);
                if (!_keyCodes.ContainsKey('ე')) _keyCodes.Add('ე', 4308);
                if (!_keyCodes.ContainsKey('ვ')) _keyCodes.Add('ვ', 4309);
                if (!_keyCodes.ContainsKey('ზ')) _keyCodes.Add('ზ', 4310);
                if (!_keyCodes.ContainsKey('თ')) _keyCodes.Add('თ', 4311);
                if (!_keyCodes.ContainsKey('ი')) _keyCodes.Add('ი', 4312);
                if (!_keyCodes.ContainsKey('კ')) _keyCodes.Add('კ', 4313);
                if (!_keyCodes.ContainsKey('ლ')) _keyCodes.Add('ლ', 4314);
                if (!_keyCodes.ContainsKey('მ')) _keyCodes.Add('მ', 4315);
                if (!_keyCodes.ContainsKey('ნ')) _keyCodes.Add('ნ', 4316);
                if (!_keyCodes.ContainsKey('ო')) _keyCodes.Add('ო', 4317);
                if (!_keyCodes.ContainsKey('პ')) _keyCodes.Add('პ', 4318);
                if (!_keyCodes.ContainsKey('ჟ')) _keyCodes.Add('ჟ', 4319);
                if (!_keyCodes.ContainsKey('რ')) _keyCodes.Add('რ', 4320);
                if (!_keyCodes.ContainsKey('ს')) _keyCodes.Add('ს', 4321);
                if (!_keyCodes.ContainsKey('ტ')) _keyCodes.Add('ტ', 4322);
                if (!_keyCodes.ContainsKey('უ')) _keyCodes.Add('უ', 4323);
                if (!_keyCodes.ContainsKey('ფ')) _keyCodes.Add('ფ', 4324);
                if (!_keyCodes.ContainsKey('ქ')) _keyCodes.Add('ქ', 4325);
                if (!_keyCodes.ContainsKey('ღ')) _keyCodes.Add('ღ', 4326);
                if (!_keyCodes.ContainsKey('ყ')) _keyCodes.Add('ყ', 4327);
                if (!_keyCodes.ContainsKey('შ')) _keyCodes.Add('შ', 4328);
                if (!_keyCodes.ContainsKey('ჩ')) _keyCodes.Add('ჩ', 4329);
                if (!_keyCodes.ContainsKey('ც')) _keyCodes.Add('ც', 4330);
                if (!_keyCodes.ContainsKey('ძ')) _keyCodes.Add('ძ', 4331);
                if (!_keyCodes.ContainsKey('წ')) _keyCodes.Add('წ', 4332);
                if (!_keyCodes.ContainsKey('ჭ')) _keyCodes.Add('ჭ', 4333);
                if (!_keyCodes.ContainsKey('ხ')) _keyCodes.Add('ხ', 4334);
                if (!_keyCodes.ContainsKey('ჯ')) _keyCodes.Add('ჯ', 4335);
                if (!_keyCodes.ContainsKey('ჰ')) _keyCodes.Add('ჰ', 4336);
                if (!_keyCodes.ContainsKey('ჱ')) _keyCodes.Add('ჱ', 4337);
                if (!_keyCodes.ContainsKey('ჲ')) _keyCodes.Add('ჲ', 4338);
                if (!_keyCodes.ContainsKey('ჳ')) _keyCodes.Add('ჳ', 4339);
                if (!_keyCodes.ContainsKey('ჴ')) _keyCodes.Add('ჴ', 4340);
                if (!_keyCodes.ContainsKey('ჵ')) _keyCodes.Add('ჵ', 4341);
                if (!_keyCodes.ContainsKey('ჶ')) _keyCodes.Add('ჶ', 4342);
                if (!_keyCodes.ContainsKey('Ḃ')) _keyCodes.Add('Ḃ', 7682);
                if (!_keyCodes.ContainsKey('ḃ')) _keyCodes.Add('ḃ', 7683);
                if (!_keyCodes.ContainsKey('Ḋ')) _keyCodes.Add('Ḋ', 7690);
                if (!_keyCodes.ContainsKey('ḋ')) _keyCodes.Add('ḋ', 7691);
                if (!_keyCodes.ContainsKey('Ḟ')) _keyCodes.Add('Ḟ', 7710);
                if (!_keyCodes.ContainsKey('ḟ')) _keyCodes.Add('ḟ', 7711);
                if (!_keyCodes.ContainsKey('Ḷ')) _keyCodes.Add('Ḷ', 7734);
                if (!_keyCodes.ContainsKey('ḷ')) _keyCodes.Add('ḷ', 7735);
                if (!_keyCodes.ContainsKey('Ṁ')) _keyCodes.Add('Ṁ', 7744);
                if (!_keyCodes.ContainsKey('ṁ')) _keyCodes.Add('ṁ', 7745);
                if (!_keyCodes.ContainsKey('Ṗ')) _keyCodes.Add('Ṗ', 7766);
                if (!_keyCodes.ContainsKey('ṗ')) _keyCodes.Add('ṗ', 7767);
                if (!_keyCodes.ContainsKey('Ṡ')) _keyCodes.Add('Ṡ', 7776);
                if (!_keyCodes.ContainsKey('ṡ')) _keyCodes.Add('ṡ', 7777);
                if (!_keyCodes.ContainsKey('Ṫ')) _keyCodes.Add('Ṫ', 7786);
                if (!_keyCodes.ContainsKey('ṫ')) _keyCodes.Add('ṫ', 7787);
                if (!_keyCodes.ContainsKey('Ẁ')) _keyCodes.Add('Ẁ', 7808);
                if (!_keyCodes.ContainsKey('ẁ')) _keyCodes.Add('ẁ', 7809);
                if (!_keyCodes.ContainsKey('Ẃ')) _keyCodes.Add('Ẃ', 7810);
                if (!_keyCodes.ContainsKey('ẃ')) _keyCodes.Add('ẃ', 7811);
                if (!_keyCodes.ContainsKey('Ẅ')) _keyCodes.Add('Ẅ', 7812);
                if (!_keyCodes.ContainsKey('ẅ')) _keyCodes.Add('ẅ', 7813);
                if (!_keyCodes.ContainsKey('Ẋ')) _keyCodes.Add('Ẋ', 7818);
                if (!_keyCodes.ContainsKey('ẋ')) _keyCodes.Add('ẋ', 7819);
                if (!_keyCodes.ContainsKey('Ạ')) _keyCodes.Add('Ạ', 7840);
                if (!_keyCodes.ContainsKey('ạ')) _keyCodes.Add('ạ', 7841);
                if (!_keyCodes.ContainsKey('Ả')) _keyCodes.Add('Ả', 7842);
                if (!_keyCodes.ContainsKey('ả')) _keyCodes.Add('ả', 7843);
                if (!_keyCodes.ContainsKey('Ấ')) _keyCodes.Add('Ấ', 7844);
                if (!_keyCodes.ContainsKey('ấ')) _keyCodes.Add('ấ', 7845);
                if (!_keyCodes.ContainsKey('Ầ')) _keyCodes.Add('Ầ', 7846);
                if (!_keyCodes.ContainsKey('ầ')) _keyCodes.Add('ầ', 7847);
                if (!_keyCodes.ContainsKey('Ẩ')) _keyCodes.Add('Ẩ', 7848);
                if (!_keyCodes.ContainsKey('ẩ')) _keyCodes.Add('ẩ', 7849);
                if (!_keyCodes.ContainsKey('Ẫ')) _keyCodes.Add('Ẫ', 7850);
                if (!_keyCodes.ContainsKey('ẫ')) _keyCodes.Add('ẫ', 7851);
                if (!_keyCodes.ContainsKey('Ậ')) _keyCodes.Add('Ậ', 7852);
                if (!_keyCodes.ContainsKey('ậ')) _keyCodes.Add('ậ', 7853);
                if (!_keyCodes.ContainsKey('Ắ')) _keyCodes.Add('Ắ', 7854);
                if (!_keyCodes.ContainsKey('ắ')) _keyCodes.Add('ắ', 7855);
                if (!_keyCodes.ContainsKey('Ằ')) _keyCodes.Add('Ằ', 7856);
                if (!_keyCodes.ContainsKey('ằ')) _keyCodes.Add('ằ', 7857);
                if (!_keyCodes.ContainsKey('Ẳ')) _keyCodes.Add('Ẳ', 7858);
                if (!_keyCodes.ContainsKey('ẳ')) _keyCodes.Add('ẳ', 7859);
                if (!_keyCodes.ContainsKey('Ẵ')) _keyCodes.Add('Ẵ', 7860);
                if (!_keyCodes.ContainsKey('ẵ')) _keyCodes.Add('ẵ', 7861);
                if (!_keyCodes.ContainsKey('Ặ')) _keyCodes.Add('Ặ', 7862);
                if (!_keyCodes.ContainsKey('ặ')) _keyCodes.Add('ặ', 7863);
                if (!_keyCodes.ContainsKey('Ẹ')) _keyCodes.Add('Ẹ', 7864);
                if (!_keyCodes.ContainsKey('ẹ')) _keyCodes.Add('ẹ', 7865);
                if (!_keyCodes.ContainsKey('Ẻ')) _keyCodes.Add('Ẻ', 7866);
                if (!_keyCodes.ContainsKey('ẻ')) _keyCodes.Add('ẻ', 7867);
                if (!_keyCodes.ContainsKey('Ẽ')) _keyCodes.Add('Ẽ', 7868);
                if (!_keyCodes.ContainsKey('ẽ')) _keyCodes.Add('ẽ', 7869);
                if (!_keyCodes.ContainsKey('Ế')) _keyCodes.Add('Ế', 7870);
                if (!_keyCodes.ContainsKey('ế')) _keyCodes.Add('ế', 7871);
                if (!_keyCodes.ContainsKey('Ề')) _keyCodes.Add('Ề', 7872);
                if (!_keyCodes.ContainsKey('ề')) _keyCodes.Add('ề', 7873);
                if (!_keyCodes.ContainsKey('Ể')) _keyCodes.Add('Ể', 7874);
                if (!_keyCodes.ContainsKey('ể')) _keyCodes.Add('ể', 7875);
                if (!_keyCodes.ContainsKey('Ễ')) _keyCodes.Add('Ễ', 7876);
                if (!_keyCodes.ContainsKey('ễ')) _keyCodes.Add('ễ', 7877);
                if (!_keyCodes.ContainsKey('Ệ')) _keyCodes.Add('Ệ', 7878);
                if (!_keyCodes.ContainsKey('ệ')) _keyCodes.Add('ệ', 7879);
                if (!_keyCodes.ContainsKey('Ỉ')) _keyCodes.Add('Ỉ', 7880);
                if (!_keyCodes.ContainsKey('ỉ')) _keyCodes.Add('ỉ', 7881);
                if (!_keyCodes.ContainsKey('Ị')) _keyCodes.Add('Ị', 7882);
                if (!_keyCodes.ContainsKey('ị')) _keyCodes.Add('ị', 7883);
                if (!_keyCodes.ContainsKey('Ọ')) _keyCodes.Add('Ọ', 7884);
                if (!_keyCodes.ContainsKey('ọ')) _keyCodes.Add('ọ', 7885);
                if (!_keyCodes.ContainsKey('Ỏ')) _keyCodes.Add('Ỏ', 7886);
                if (!_keyCodes.ContainsKey('ỏ')) _keyCodes.Add('ỏ', 7887);
                if (!_keyCodes.ContainsKey('Ố')) _keyCodes.Add('Ố', 7888);
                if (!_keyCodes.ContainsKey('ố')) _keyCodes.Add('ố', 7889);
                if (!_keyCodes.ContainsKey('Ồ')) _keyCodes.Add('Ồ', 7890);
                if (!_keyCodes.ContainsKey('ồ')) _keyCodes.Add('ồ', 7891);
                if (!_keyCodes.ContainsKey('Ổ')) _keyCodes.Add('Ổ', 7892);
                if (!_keyCodes.ContainsKey('ổ')) _keyCodes.Add('ổ', 7893);
                if (!_keyCodes.ContainsKey('Ỗ')) _keyCodes.Add('Ỗ', 7894);
                if (!_keyCodes.ContainsKey('ỗ')) _keyCodes.Add('ỗ', 7895);
                if (!_keyCodes.ContainsKey('Ộ')) _keyCodes.Add('Ộ', 7896);
                if (!_keyCodes.ContainsKey('ộ')) _keyCodes.Add('ộ', 7897);
                if (!_keyCodes.ContainsKey('Ớ')) _keyCodes.Add('Ớ', 7898);
                if (!_keyCodes.ContainsKey('ớ')) _keyCodes.Add('ớ', 7899);
                if (!_keyCodes.ContainsKey('Ờ')) _keyCodes.Add('Ờ', 7900);
                if (!_keyCodes.ContainsKey('ờ')) _keyCodes.Add('ờ', 7901);
                if (!_keyCodes.ContainsKey('Ở')) _keyCodes.Add('Ở', 7902);
                if (!_keyCodes.ContainsKey('ở')) _keyCodes.Add('ở', 7903);
                if (!_keyCodes.ContainsKey('Ỡ')) _keyCodes.Add('Ỡ', 7904);
                if (!_keyCodes.ContainsKey('ỡ')) _keyCodes.Add('ỡ', 7905);
                if (!_keyCodes.ContainsKey('Ợ')) _keyCodes.Add('Ợ', 7906);
                if (!_keyCodes.ContainsKey('ợ')) _keyCodes.Add('ợ', 7907);
                if (!_keyCodes.ContainsKey('Ụ')) _keyCodes.Add('Ụ', 7908);
                if (!_keyCodes.ContainsKey('ụ')) _keyCodes.Add('ụ', 7909);
                if (!_keyCodes.ContainsKey('Ủ')) _keyCodes.Add('Ủ', 7910);
                if (!_keyCodes.ContainsKey('ủ')) _keyCodes.Add('ủ', 7911);
                if (!_keyCodes.ContainsKey('Ứ')) _keyCodes.Add('Ứ', 7912);
                if (!_keyCodes.ContainsKey('ứ')) _keyCodes.Add('ứ', 7913);
                if (!_keyCodes.ContainsKey('Ừ')) _keyCodes.Add('Ừ', 7914);
                if (!_keyCodes.ContainsKey('ừ')) _keyCodes.Add('ừ', 7915);
                if (!_keyCodes.ContainsKey('Ử')) _keyCodes.Add('Ử', 7916);
                if (!_keyCodes.ContainsKey('ử')) _keyCodes.Add('ử', 7917);
                if (!_keyCodes.ContainsKey('Ữ')) _keyCodes.Add('Ữ', 7918);
                if (!_keyCodes.ContainsKey('ữ')) _keyCodes.Add('ữ', 7919);
                if (!_keyCodes.ContainsKey('Ự')) _keyCodes.Add('Ự', 7920);
                if (!_keyCodes.ContainsKey('ự')) _keyCodes.Add('ự', 7921);
                if (!_keyCodes.ContainsKey('Ỳ')) _keyCodes.Add('Ỳ', 7922);
                if (!_keyCodes.ContainsKey('ỳ')) _keyCodes.Add('ỳ', 7923);
                if (!_keyCodes.ContainsKey('Ỵ')) _keyCodes.Add('Ỵ', 7924);
                if (!_keyCodes.ContainsKey('ỵ')) _keyCodes.Add('ỵ', 7925);
                if (!_keyCodes.ContainsKey('Ỷ')) _keyCodes.Add('Ỷ', 7926);
                if (!_keyCodes.ContainsKey('ỷ')) _keyCodes.Add('ỷ', 7927);
                if (!_keyCodes.ContainsKey('Ỹ')) _keyCodes.Add('Ỹ', 7928);
                if (!_keyCodes.ContainsKey('ỹ')) _keyCodes.Add('ỹ', 7929);
                if (!_keyCodes.ContainsKey(' ')) _keyCodes.Add(' ', 8194);
                if (!_keyCodes.ContainsKey(' ')) _keyCodes.Add(' ', 8195);
                if (!_keyCodes.ContainsKey(' ')) _keyCodes.Add(' ', 8196);
                if (!_keyCodes.ContainsKey(' ')) _keyCodes.Add(' ', 8197);
                if (!_keyCodes.ContainsKey(' ')) _keyCodes.Add(' ', 8199);
                if (!_keyCodes.ContainsKey(' ')) _keyCodes.Add(' ', 8200);
                if (!_keyCodes.ContainsKey(' ')) _keyCodes.Add(' ', 8201);
                if (!_keyCodes.ContainsKey(' ')) _keyCodes.Add(' ', 8202);
                if (!_keyCodes.ContainsKey('‒')) _keyCodes.Add('‒', 8210);
                if (!_keyCodes.ContainsKey('–')) _keyCodes.Add('–', 8211);
                if (!_keyCodes.ContainsKey('—')) _keyCodes.Add('—', 8212);
                if (!_keyCodes.ContainsKey('―')) _keyCodes.Add('―', 8213);
                if (!_keyCodes.ContainsKey('‗')) _keyCodes.Add('‗', 8215);
                if (!_keyCodes.ContainsKey('‘')) _keyCodes.Add('‘', 8216);
                if (!_keyCodes.ContainsKey('’')) _keyCodes.Add('’', 8217);
                if (!_keyCodes.ContainsKey('‚')) _keyCodes.Add('‚', 8218);
                if (!_keyCodes.ContainsKey('“')) _keyCodes.Add('“', 8220);
                if (!_keyCodes.ContainsKey('”')) _keyCodes.Add('”', 8221);
                if (!_keyCodes.ContainsKey('„')) _keyCodes.Add('„', 8222);
                if (!_keyCodes.ContainsKey('†')) _keyCodes.Add('†', 8224);
                if (!_keyCodes.ContainsKey('‡')) _keyCodes.Add('‡', 8225);
                if (!_keyCodes.ContainsKey('•')) _keyCodes.Add('•', 8226);
                if (!_keyCodes.ContainsKey('‥')) _keyCodes.Add('‥', 8229);
                if (!_keyCodes.ContainsKey('…')) _keyCodes.Add('…', 8230);
                if (!_keyCodes.ContainsKey('′')) _keyCodes.Add('′', 8242);
                if (!_keyCodes.ContainsKey('″')) _keyCodes.Add('″', 8243);
                if (!_keyCodes.ContainsKey('‸')) _keyCodes.Add('‸', 8248);
                if (!_keyCodes.ContainsKey('‾')) _keyCodes.Add('‾', 8254);
                if (!_keyCodes.ContainsKey('₠')) _keyCodes.Add('₠', 8352);
                if (!_keyCodes.ContainsKey('₡')) _keyCodes.Add('₡', 8353);
                if (!_keyCodes.ContainsKey('₢')) _keyCodes.Add('₢', 8354);
                if (!_keyCodes.ContainsKey('₣')) _keyCodes.Add('₣', 8355);
                if (!_keyCodes.ContainsKey('₤')) _keyCodes.Add('₤', 8356);
                if (!_keyCodes.ContainsKey('₥')) _keyCodes.Add('₥', 8357);
                if (!_keyCodes.ContainsKey('₦')) _keyCodes.Add('₦', 8358);
                if (!_keyCodes.ContainsKey('₧')) _keyCodes.Add('₧', 8359);
                if (!_keyCodes.ContainsKey('₨')) _keyCodes.Add('₨', 8360);
                if (!_keyCodes.ContainsKey('₩')) _keyCodes.Add('₩', 8361);
                if (!_keyCodes.ContainsKey('₩')) _keyCodes.Add('₩', 8361);
                if (!_keyCodes.ContainsKey('₪')) _keyCodes.Add('₪', 8362);
                if (!_keyCodes.ContainsKey('₫')) _keyCodes.Add('₫', 8363);
                if (!_keyCodes.ContainsKey('€')) _keyCodes.Add('€', 8364);
                if (!_keyCodes.ContainsKey('℅')) _keyCodes.Add('℅', 8453);
                if (!_keyCodes.ContainsKey('№')) _keyCodes.Add('№', 8470);
                if (!_keyCodes.ContainsKey('℗')) _keyCodes.Add('℗', 8471);
                if (!_keyCodes.ContainsKey('℞')) _keyCodes.Add('℞', 8478);
                if (!_keyCodes.ContainsKey('™')) _keyCodes.Add('™', 8482);
                if (!_keyCodes.ContainsKey('⅓')) _keyCodes.Add('⅓', 8531);
                if (!_keyCodes.ContainsKey('⅔')) _keyCodes.Add('⅔', 8532);
                if (!_keyCodes.ContainsKey('⅕')) _keyCodes.Add('⅕', 8533);
                if (!_keyCodes.ContainsKey('⅖')) _keyCodes.Add('⅖', 8534);
                if (!_keyCodes.ContainsKey('⅗')) _keyCodes.Add('⅗', 8535);
                if (!_keyCodes.ContainsKey('⅘')) _keyCodes.Add('⅘', 8536);
                if (!_keyCodes.ContainsKey('⅙')) _keyCodes.Add('⅙', 8537);
                if (!_keyCodes.ContainsKey('⅚')) _keyCodes.Add('⅚', 8538);
                if (!_keyCodes.ContainsKey('⅛')) _keyCodes.Add('⅛', 8539);
                if (!_keyCodes.ContainsKey('⅜')) _keyCodes.Add('⅜', 8540);
                if (!_keyCodes.ContainsKey('⅝')) _keyCodes.Add('⅝', 8541);
                if (!_keyCodes.ContainsKey('⅞')) _keyCodes.Add('⅞', 8542);
                if (!_keyCodes.ContainsKey('←')) _keyCodes.Add('←', 8592);
                if (!_keyCodes.ContainsKey('↑')) _keyCodes.Add('↑', 8593);
                if (!_keyCodes.ContainsKey('→')) _keyCodes.Add('→', 8594);
                if (!_keyCodes.ContainsKey('↓')) _keyCodes.Add('↓', 8595);
                if (!_keyCodes.ContainsKey('⇒')) _keyCodes.Add('⇒', 8658);
                if (!_keyCodes.ContainsKey('⇔')) _keyCodes.Add('⇔', 8660);
                if (!_keyCodes.ContainsKey('∂')) _keyCodes.Add('∂', 8706);
                if (!_keyCodes.ContainsKey('∇')) _keyCodes.Add('∇', 8711);
                if (!_keyCodes.ContainsKey('∘')) _keyCodes.Add('∘', 8728);
                if (!_keyCodes.ContainsKey('√')) _keyCodes.Add('√', 8730);
                if (!_keyCodes.ContainsKey('∝')) _keyCodes.Add('∝', 8733);
                if (!_keyCodes.ContainsKey('∞')) _keyCodes.Add('∞', 8734);
                if (!_keyCodes.ContainsKey('∧')) _keyCodes.Add('∧', 8743);
                if (!_keyCodes.ContainsKey('∧')) _keyCodes.Add('∧', 8743);
                if (!_keyCodes.ContainsKey('∨')) _keyCodes.Add('∨', 8744);
                if (!_keyCodes.ContainsKey('∨')) _keyCodes.Add('∨', 8744);
                if (!_keyCodes.ContainsKey('∩')) _keyCodes.Add('∩', 8745);
                if (!_keyCodes.ContainsKey('∩')) _keyCodes.Add('∩', 8745);
                if (!_keyCodes.ContainsKey('∪')) _keyCodes.Add('∪', 8746);
                if (!_keyCodes.ContainsKey('∪')) _keyCodes.Add('∪', 8746);
                if (!_keyCodes.ContainsKey('∫')) _keyCodes.Add('∫', 8747);
                if (!_keyCodes.ContainsKey('∴')) _keyCodes.Add('∴', 8756);
                if (!_keyCodes.ContainsKey('∼')) _keyCodes.Add('∼', 8764);
                if (!_keyCodes.ContainsKey('≃')) _keyCodes.Add('≃', 8771);
                if (!_keyCodes.ContainsKey('≠')) _keyCodes.Add('≠', 8800);
                if (!_keyCodes.ContainsKey('≡')) _keyCodes.Add('≡', 8801);
                if (!_keyCodes.ContainsKey('≤')) _keyCodes.Add('≤', 8804);
                if (!_keyCodes.ContainsKey('≥')) _keyCodes.Add('≥', 8805);
                if (!_keyCodes.ContainsKey('⊂')) _keyCodes.Add('⊂', 8834);
                if (!_keyCodes.ContainsKey('⊂')) _keyCodes.Add('⊂', 8834);
                if (!_keyCodes.ContainsKey('⊃')) _keyCodes.Add('⊃', 8835);
                if (!_keyCodes.ContainsKey('⊃')) _keyCodes.Add('⊃', 8835);
                if (!_keyCodes.ContainsKey('⊢')) _keyCodes.Add('⊢', 8866);
                if (!_keyCodes.ContainsKey('⊣')) _keyCodes.Add('⊣', 8867);
                if (!_keyCodes.ContainsKey('⊤')) _keyCodes.Add('⊤', 8868);
                if (!_keyCodes.ContainsKey('⊥')) _keyCodes.Add('⊥', 8869);
                if (!_keyCodes.ContainsKey('⌈')) _keyCodes.Add('⌈', 8968);
                if (!_keyCodes.ContainsKey('⌊')) _keyCodes.Add('⌊', 8970);
                if (!_keyCodes.ContainsKey('⌕')) _keyCodes.Add('⌕', 8981);
                if (!_keyCodes.ContainsKey('⌠')) _keyCodes.Add('⌠', 8992);
                if (!_keyCodes.ContainsKey('⌡')) _keyCodes.Add('⌡', 8993);
                if (!_keyCodes.ContainsKey('⎕')) _keyCodes.Add('⎕', 9109);
                if (!_keyCodes.ContainsKey('⎛')) _keyCodes.Add('⎛', 9115);
                if (!_keyCodes.ContainsKey('⎝')) _keyCodes.Add('⎝', 9117);
                if (!_keyCodes.ContainsKey('⎞')) _keyCodes.Add('⎞', 9118);
                if (!_keyCodes.ContainsKey('⎠')) _keyCodes.Add('⎠', 9120);
                if (!_keyCodes.ContainsKey('⎡')) _keyCodes.Add('⎡', 9121);
                if (!_keyCodes.ContainsKey('⎣')) _keyCodes.Add('⎣', 9123);
                if (!_keyCodes.ContainsKey('⎤')) _keyCodes.Add('⎤', 9124);
                if (!_keyCodes.ContainsKey('⎦')) _keyCodes.Add('⎦', 9126);
                if (!_keyCodes.ContainsKey('⎨')) _keyCodes.Add('⎨', 9128);
                if (!_keyCodes.ContainsKey('⎬')) _keyCodes.Add('⎬', 9132);
                if (!_keyCodes.ContainsKey('⎷')) _keyCodes.Add('⎷', 9143);
                if (!_keyCodes.ContainsKey('⎺')) _keyCodes.Add('⎺', 9146);
                if (!_keyCodes.ContainsKey('⎻')) _keyCodes.Add('⎻', 9147);
                if (!_keyCodes.ContainsKey('⎼')) _keyCodes.Add('⎼', 9148);
                if (!_keyCodes.ContainsKey('⎽')) _keyCodes.Add('⎽', 9149);
                if (!_keyCodes.ContainsKey('␉')) _keyCodes.Add('␉', 9225);
                if (!_keyCodes.ContainsKey('␊')) _keyCodes.Add('␊', 9226);
                if (!_keyCodes.ContainsKey('␋')) _keyCodes.Add('␋', 9227);
                if (!_keyCodes.ContainsKey('␌')) _keyCodes.Add('␌', 9228);
                if (!_keyCodes.ContainsKey('␍')) _keyCodes.Add('␍', 9229);
                if (!_keyCodes.ContainsKey('␣')) _keyCodes.Add('␣', 9251);
                if (!_keyCodes.ContainsKey('␤')) _keyCodes.Add('␤', 9252);
                if (!_keyCodes.ContainsKey('─')) _keyCodes.Add('─', 9472);
                if (!_keyCodes.ContainsKey('─')) _keyCodes.Add('─', 9472);
                if (!_keyCodes.ContainsKey('│')) _keyCodes.Add('│', 9474);
                if (!_keyCodes.ContainsKey('│')) _keyCodes.Add('│', 9474);
                if (!_keyCodes.ContainsKey('┌')) _keyCodes.Add('┌', 9484);
                if (!_keyCodes.ContainsKey('┌')) _keyCodes.Add('┌', 9484);
                if (!_keyCodes.ContainsKey('┐')) _keyCodes.Add('┐', 9488);
                if (!_keyCodes.ContainsKey('└')) _keyCodes.Add('└', 9492);
                if (!_keyCodes.ContainsKey('┘')) _keyCodes.Add('┘', 9496);
                if (!_keyCodes.ContainsKey('├')) _keyCodes.Add('├', 9500);
                if (!_keyCodes.ContainsKey('┤')) _keyCodes.Add('┤', 9508);
                if (!_keyCodes.ContainsKey('┬')) _keyCodes.Add('┬', 9516);
                if (!_keyCodes.ContainsKey('┴')) _keyCodes.Add('┴', 9524);
                if (!_keyCodes.ContainsKey('┼')) _keyCodes.Add('┼', 9532);
                if (!_keyCodes.ContainsKey('▒')) _keyCodes.Add('▒', 9618);
                if (!_keyCodes.ContainsKey('▪')) _keyCodes.Add('▪', 9642);
                if (!_keyCodes.ContainsKey('▫')) _keyCodes.Add('▫', 9643);
                if (!_keyCodes.ContainsKey('▬')) _keyCodes.Add('▬', 9644);
                if (!_keyCodes.ContainsKey('▭')) _keyCodes.Add('▭', 9645);
                if (!_keyCodes.ContainsKey('▮')) _keyCodes.Add('▮', 9646);
                if (!_keyCodes.ContainsKey('▯')) _keyCodes.Add('▯', 9647);
                if (!_keyCodes.ContainsKey('▲')) _keyCodes.Add('▲', 9650);
                if (!_keyCodes.ContainsKey('△')) _keyCodes.Add('△', 9651);
                if (!_keyCodes.ContainsKey('▶')) _keyCodes.Add('▶', 9654);
                if (!_keyCodes.ContainsKey('▷')) _keyCodes.Add('▷', 9655);
                if (!_keyCodes.ContainsKey('▼')) _keyCodes.Add('▼', 9660);
                if (!_keyCodes.ContainsKey('▽')) _keyCodes.Add('▽', 9661);
                if (!_keyCodes.ContainsKey('◀')) _keyCodes.Add('◀', 9664);
                if (!_keyCodes.ContainsKey('◁')) _keyCodes.Add('◁', 9665);
                if (!_keyCodes.ContainsKey('◆')) _keyCodes.Add('◆', 9670);
                if (!_keyCodes.ContainsKey('○')) _keyCodes.Add('○', 9675);
                if (!_keyCodes.ContainsKey('○')) _keyCodes.Add('○', 9675);
                if (!_keyCodes.ContainsKey('●')) _keyCodes.Add('●', 9679);
                if (!_keyCodes.ContainsKey('◦')) _keyCodes.Add('◦', 9702);
                if (!_keyCodes.ContainsKey('☆')) _keyCodes.Add('☆', 9734);
                if (!_keyCodes.ContainsKey('☎')) _keyCodes.Add('☎', 9742);
                if (!_keyCodes.ContainsKey('☓')) _keyCodes.Add('☓', 9747);
                if (!_keyCodes.ContainsKey('☜')) _keyCodes.Add('☜', 9756);
                if (!_keyCodes.ContainsKey('☞')) _keyCodes.Add('☞', 9758);
                if (!_keyCodes.ContainsKey('♀')) _keyCodes.Add('♀', 9792);
                if (!_keyCodes.ContainsKey('♂')) _keyCodes.Add('♂', 9794);
                if (!_keyCodes.ContainsKey('♣')) _keyCodes.Add('♣', 9827);
                if (!_keyCodes.ContainsKey('♥')) _keyCodes.Add('♥', 9829);
                if (!_keyCodes.ContainsKey('♦')) _keyCodes.Add('♦', 9830);
                if (!_keyCodes.ContainsKey('♭')) _keyCodes.Add('♭', 9837);
                if (!_keyCodes.ContainsKey('♯')) _keyCodes.Add('♯', 9839);
                if (!_keyCodes.ContainsKey('✓')) _keyCodes.Add('✓', 10003);
                if (!_keyCodes.ContainsKey('✗')) _keyCodes.Add('✗', 10007);
                if (!_keyCodes.ContainsKey('✝')) _keyCodes.Add('✝', 10013);
                if (!_keyCodes.ContainsKey('✠')) _keyCodes.Add('✠', 10016);
                if (!_keyCodes.ContainsKey('⟨')) _keyCodes.Add('⟨', 10216);
                if (!_keyCodes.ContainsKey('⟩')) _keyCodes.Add('⟩', 10217);
                if (!_keyCodes.ContainsKey('、')) _keyCodes.Add('、', 12289);
                if (!_keyCodes.ContainsKey('。')) _keyCodes.Add('。', 12290);
                if (!_keyCodes.ContainsKey('「')) _keyCodes.Add('「', 12300);
                if (!_keyCodes.ContainsKey('」')) _keyCodes.Add('」', 12301);
                if (!_keyCodes.ContainsKey('゛')) _keyCodes.Add('゛', 12443);
                if (!_keyCodes.ContainsKey('゜')) _keyCodes.Add('゜', 12444);
                if (!_keyCodes.ContainsKey('ァ')) _keyCodes.Add('ァ', 12449);
                if (!_keyCodes.ContainsKey('ア')) _keyCodes.Add('ア', 12450);
                if (!_keyCodes.ContainsKey('ィ')) _keyCodes.Add('ィ', 12451);
                if (!_keyCodes.ContainsKey('イ')) _keyCodes.Add('イ', 12452);
                if (!_keyCodes.ContainsKey('ゥ')) _keyCodes.Add('ゥ', 12453);
                if (!_keyCodes.ContainsKey('ウ')) _keyCodes.Add('ウ', 12454);
                if (!_keyCodes.ContainsKey('ェ')) _keyCodes.Add('ェ', 12455);
                if (!_keyCodes.ContainsKey('エ')) _keyCodes.Add('エ', 12456);
                if (!_keyCodes.ContainsKey('ォ')) _keyCodes.Add('ォ', 12457);
                if (!_keyCodes.ContainsKey('オ')) _keyCodes.Add('オ', 12458);
                if (!_keyCodes.ContainsKey('カ')) _keyCodes.Add('カ', 12459);
                if (!_keyCodes.ContainsKey('キ')) _keyCodes.Add('キ', 12461);
                if (!_keyCodes.ContainsKey('ク')) _keyCodes.Add('ク', 12463);
                if (!_keyCodes.ContainsKey('ケ')) _keyCodes.Add('ケ', 12465);
                if (!_keyCodes.ContainsKey('コ')) _keyCodes.Add('コ', 12467);
                if (!_keyCodes.ContainsKey('サ')) _keyCodes.Add('サ', 12469);
                if (!_keyCodes.ContainsKey('シ')) _keyCodes.Add('シ', 12471);
                if (!_keyCodes.ContainsKey('ス')) _keyCodes.Add('ス', 12473);
                if (!_keyCodes.ContainsKey('セ')) _keyCodes.Add('セ', 12475);
                if (!_keyCodes.ContainsKey('ソ')) _keyCodes.Add('ソ', 12477);
                if (!_keyCodes.ContainsKey('タ')) _keyCodes.Add('タ', 12479);
                if (!_keyCodes.ContainsKey('チ')) _keyCodes.Add('チ', 12481);
                if (!_keyCodes.ContainsKey('ッ')) _keyCodes.Add('ッ', 12483);
                if (!_keyCodes.ContainsKey('ツ')) _keyCodes.Add('ツ', 12484);
                if (!_keyCodes.ContainsKey('テ')) _keyCodes.Add('テ', 12486);
                if (!_keyCodes.ContainsKey('ト')) _keyCodes.Add('ト', 12488);
                if (!_keyCodes.ContainsKey('ナ')) _keyCodes.Add('ナ', 12490);
                if (!_keyCodes.ContainsKey('ニ')) _keyCodes.Add('ニ', 12491);
                if (!_keyCodes.ContainsKey('ヌ')) _keyCodes.Add('ヌ', 12492);
                if (!_keyCodes.ContainsKey('ネ')) _keyCodes.Add('ネ', 12493);
                if (!_keyCodes.ContainsKey('ノ')) _keyCodes.Add('ノ', 12494);
                if (!_keyCodes.ContainsKey('ハ')) _keyCodes.Add('ハ', 12495);
                if (!_keyCodes.ContainsKey('ヒ')) _keyCodes.Add('ヒ', 12498);
                if (!_keyCodes.ContainsKey('フ')) _keyCodes.Add('フ', 12501);
                if (!_keyCodes.ContainsKey('ヘ')) _keyCodes.Add('ヘ', 12504);
                if (!_keyCodes.ContainsKey('ホ')) _keyCodes.Add('ホ', 12507);
                if (!_keyCodes.ContainsKey('マ')) _keyCodes.Add('マ', 12510);
                if (!_keyCodes.ContainsKey('ミ')) _keyCodes.Add('ミ', 12511);
                if (!_keyCodes.ContainsKey('ム')) _keyCodes.Add('ム', 12512);
                if (!_keyCodes.ContainsKey('メ')) _keyCodes.Add('メ', 12513);
                if (!_keyCodes.ContainsKey('モ')) _keyCodes.Add('モ', 12514);
                if (!_keyCodes.ContainsKey('ャ')) _keyCodes.Add('ャ', 12515);
                if (!_keyCodes.ContainsKey('ヤ')) _keyCodes.Add('ヤ', 12516);
                if (!_keyCodes.ContainsKey('ュ')) _keyCodes.Add('ュ', 12517);
                if (!_keyCodes.ContainsKey('ユ')) _keyCodes.Add('ユ', 12518);
                if (!_keyCodes.ContainsKey('ョ')) _keyCodes.Add('ョ', 12519);
                if (!_keyCodes.ContainsKey('ヨ')) _keyCodes.Add('ヨ', 12520);
                if (!_keyCodes.ContainsKey('ラ')) _keyCodes.Add('ラ', 12521);
                if (!_keyCodes.ContainsKey('リ')) _keyCodes.Add('リ', 12522);
                if (!_keyCodes.ContainsKey('ル')) _keyCodes.Add('ル', 12523);
                if (!_keyCodes.ContainsKey('レ')) _keyCodes.Add('レ', 12524);
                if (!_keyCodes.ContainsKey('ロ')) _keyCodes.Add('ロ', 12525);
                if (!_keyCodes.ContainsKey('ワ')) _keyCodes.Add('ワ', 12527);
                if (!_keyCodes.ContainsKey('ヲ')) _keyCodes.Add('ヲ', 12530);
                if (!_keyCodes.ContainsKey('ン')) _keyCodes.Add('ン', 12531);
                if (!_keyCodes.ContainsKey('・')) _keyCodes.Add('・', 12539);
                if (!_keyCodes.ContainsKey('ー')) _keyCodes.Add('ー', 12540);

                return true;
            }
            catch (Exception ea)
            {
                throw new Exception("Cannot read or execute keys.csv. Be sure it is availabe! Error is " + ea.ToString());
            }
        }

        private bool PrepareConnection(string server, int port, string password)
        {
            LoadKeyDictionary();
            LoadEncodings();

            //Set the Server, Port, Password and RFB-Client-Version Properties
            Properties = new ConnectionProperties(server, password, port);

            Log(Logtype.Information, "+++++++++++++++++++++ Initialize new VNC connection +++++++++++++++++++++");

            return true;
        }

        private bool Initialize()
        {
            Log(Logtype.Information, "Connecting to " + Properties.Server + ":" + Properties.Port);

            //Start the Connection to the Server
            if (Connect(Properties.Server, Properties.Port) == false) { return false; }  //Connect failed
            Thread.Sleep(23);
            HandleRfbVersion(); //Check out the RFB-Version of the Server
            Thread.Sleep(23);
            HandleSecurityType(Properties.Password.Length > 0 ? true : false); //Handle the SecurityType, based on the RfbVersion
            Thread.Sleep(23);

            if (Authenticate()) //Authenticate at the Server with handled SecurityType
            {
                if (SendClientSharedFlag() == false) return false; //Do ClientInit
                Thread.Sleep(23);
                if (ReceiveServerInit() == false) return false; //Receive ServerInit
                Thread.Sleep(23);
                StartServerListener(); //Start the Listener for ServerToClient-Messages
                Thread.Sleep(23);

                //Set the PixelFormat; Currently the only working Format
                SendSetPixelFormat(new PixelFormat(32, 24, false, true, 255, 255, 255, 16, 8, 0));
                Thread.Sleep(23);
                SendSetEncodings(); //Currently only RAW is supported
                Thread.Sleep(23);

                _isConnected = true;
                Thread.Sleep(23);

                SendMouseClick((UInt16)(Properties.FramebufferWidth / 2), (UInt16)(Properties.FramebufferHeight / 2), (byte)0);
                Thread.Sleep(23);
            }
            else
            {
                Disconnect();
                Log(Logtype.User, "Authentication failed");
                return false;
            }
            return true;
        }

        /// <summary>
        /// Fills the Encoding-Dictionary with the Content
        /// </summary>
        private void LoadEncodings()
        {
            //Unsupported Encodings are in Comment
            //_EncodingDetails.Add(RfbEncoding.ZRLE_ENCODING, new RfbEncodingDetails(16, "ZRLE", 1));
            //_EncodingDetails.Add(RfbEncoding.Hextile_ENCODING, new RfbEncodingDetails(5, "Hextile", 2));
            //_EncodingDetails.Add(RfbEncoding.RRE_ENCODING, new RfbEncodingDetails(2, "RRE", 3));
            //_EncodingDetails.Add(RfbEncoding.CopyRect_ENCODING, new RfbEncodingDetails(1, "CopyRect", 4));
            _encodingDetails.Add(RfbEncoding.Raw_ENCODING, new RfbEncodingDetails(0, "RAW", 255));
            //_EncodingDetails.Add(RfbEncoding.CoRRE_ENCODING, new RfbEncodingDetails(4, "CoRRE", 5));
            //_EncodingDetails.Add(RfbEncoding.zlib_ENCODING, new RfbEncodingDetails(6, "zlib", 6));
            //_EncodingDetails.Add(RfbEncoding.tight_ENCODING, new RfbEncodingDetails(7, "tight", 7));
            //_EncodingDetails.Add(RfbEncoding.zlibhex_ENCODING, new RfbEncodingDetails(8, "zlibhex", 8));
            //_EncodingDetails.Add(RfbEncoding.TRLE_ENCODING, new RfbEncodingDetails(15, "TRLE", 9));
            //_EncodingDetails.Add(RfbEncoding.Hitachi_ZYWRLE_ENCODING, new RfbEncodingDetails(17, "Hitachi ZYWRLE", 10));
            //_EncodingDetails.Add(RfbEncoding.Adam_Walling_XZ_ENCODING, new RfbEncodingDetails(18, "Adam Walling XZ", 11));
            //_EncodingDetails.Add(RfbEncoding.Adam_Walling_XZYW_ENCODING, new RfbEncodingDetails(19, "Adam Walling XZYW", 12));

            _encodingDetails.Add(RfbEncoding.Pseudo_DesktopSize_ENCODING, new RfbEncodingDetails(-223, "Pseudo DesktopSize", 0)); //FFFFFF21
            //_EncodingDetails.Add(RfbEncoding.Pseudo_Cursor_ENCODING, new RfbEncodingDetails(-239, "Pseudo Cursor", 0)); //FFFFFF11

            //_EncodingDetails.Add(RfbEncoding.Pseudo_Cursor_ENCODING, new RfbEncodingDetails(-250, "Pseudo 250", 0)); //FFFFFF06; UltraVNC
            //_EncodingDetails.Add(RfbEncoding.Pseudo_Cursor_ENCODING, new RfbEncodingDetails(-24, "Pseudo 24", 0)); //FFFFFFE6; UltraVNC
            //_EncodingDetails.Add(RfbEncoding.Pseudo_Cursor_ENCODING, new RfbEncodingDetails(-65530, "Pseudo 65530", 0)); //FFFF0006; UltraVNC
            //_EncodingDetails.Add(RfbEncoding.Pseudo_Cursor_ENCODING, new RfbEncodingDetails(-224, "Pseudo 224", 0)); //FFFFFF20; UltraVNC

        }

        /// <summary>
        /// Starts the Receiverthread to wait for new Data from the Server
        /// </summary>
        private void StartServerListener()
        {
            if (_receiverTask == null)
            {
                var cancellationToken = _receiverTaskCancellationTokenSource.Token;
                _receiverTask = Task.Run(() => Receiver_ProgressThread(cancellationToken), cancellationToken);
            }
            _receiver = new BackgroundWorker();
            _receiver.ProgressChanged += _Receiver_ProgressChanged;
            _receiver.DoWork += Receiver;
            _receiver.WorkerReportsProgress = true;
            _receiver.RunWorkerAsync();
        }

        /// <summary>
        /// Triggers the Thread for Backbufferchanges
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e">The Color/Pixel-Information</param>
        void _Receiver_ProgressChanged(object sender, ProgressChangedEventArgs e)
        {
            if (!_dataChannel.IsAddingCompleted)
            {
                _dataChannel.Add((List<RfbRectangle>) e.UserState);
            }                        
        }

        /// <summary>
        /// The Thread to update the Backbuffer
        /// </summary>
        void Receiver_ProgressThread(CancellationToken cancelToken)
        {
            foreach (var changeDatas in _dataChannel.GetConsumingEnumerable(cancelToken))
            {
                try
                {
                    ScreenUpdate?.Invoke(this, new ScreenUpdateEventArgs()
                    {
                        Rects = changeDatas,
                    });
                }
                catch (Exception ea)
                {
                    Log(Logtype.Error, ea.ToString());
                }
            }
        }

        
        private void _LastReceiveTimer_Tick(object sender, EventArgs e)
        {
            Log(Logtype.Debug, "Query a new Frame automatically");

            //Request a new Frame
            SendFramebufferUpdateRequest(true, 0, 0, Properties.FramebufferWidth, Properties.FramebufferHeight);

            _lastReceive = DateTime.Now;
        }

        public delegate void ConnectionFailedEventHandler(object sender, ConnectionFailedEventArgs e);
        public event ConnectionFailedEventHandler ConnectionFailed;

        public delegate void NotSupportedServerMessageEventHandler(object sender, NotSupportedServerMessageEventArgs e);
        public event NotSupportedServerMessageEventHandler NotSupportedServerMessage;

        public delegate void LogMessageEventHandler(object sender, LogMessageEventArgs e);
        //public event LogMessageEventHandler LogMessage;

        public delegate void ScreenUpdateEventHandler(object sender, ScreenUpdateEventArgs e);
        public event ScreenUpdateEventHandler ScreenUpdate;

        public delegate void ServerCutTextEventHandler(object sender, ServerCutTextEventArgs e);
        public event ServerCutTextEventHandler ServerCutText;

        /// <summary>
        /// Start the Connection to the VNC-Server
        /// </summary>
        /// <param name="server">The IP or Hostname of the Server</param>
        /// <param name="port">The Port of the Server</param>
        /// <returns></returns>
        private bool Connect(String server, int port)
        {
            try
            {
                //Create a TcpClient
                _client = new TcpClient();
                _client.Connect(server, port);
                //Get a client stream for reading and writing.
                _dataStream = _client.GetStream();
                return true;
            }
            catch (SocketException ea)
            {
                Log(Logtype.Warning, "SocketException: {0}" + ea.ToString());
                return false;
            }
            catch (Exception ea)
            {
                Log(Logtype.Error, "Exception: {0}" + ea.ToString());
                return false;
            }
        }

        public void Disconnect()
        {
            try
            {
                _disconnectionInProgress = true;
                // Close everything.
                _receiverTaskCancellationTokenSource.Cancel();
                _dataChannel.CompleteAdding();
                _dataStream.Close();
                _client.Close();
            }
            catch (SocketException ea)
            {
                Console.WriteLine("SocketException: {0}", ea);
                throw new SocketException(ea.ErrorCode);
            }
            catch (Exception ea)
            {
                Console.WriteLine("Exception: {0}", ea);
                throw new Exception(ea.Message, ea);
            }
        }

        public void StartConnection()
        {
            Initialize();
        }

        /// <summary>
        /// Get servers VNC-Version and Client-VNC-Version; see 6.1.1
        /// </summary>
        private void HandleRfbVersion()
        {
            //Read the of the TcpServer response bytes.
            var recData = new Byte[12]; //estimate a 12 Byte long message with VNC-Version
            var bytes = _dataStream.Read(recData, 0, recData.Length);
            Properties.RfbServerVersion = System.Text.Encoding.ASCII.GetString(recData, 0, 12);

            Log(Logtype.Information, "Servers RFB-Version: " + Properties.RfbServerVersion);

            //Write the Clientversion
            var sendData = System.Text.Encoding.ASCII.GetBytes(Properties.RfbClientVersion);
            _dataStream.Write(sendData, 0, sendData.Length);
        }

        /// <summary>
        /// Handles the SecurtiyType that is used by this connection; see 6.1.2 + 6.1.3
        /// </summary>
        private void HandleSecurityType(bool hasPassword)
        {
            Byte[] recData;
            int bytes;
            Byte[] sendData;

            if (Properties.RfbServerVersion2.Minor < 7) //For Version 3.3 and other Versions
            {
                // Buffer to store the response bytes (4 Bytes with Securitytype; SecurityType will be 0 = invalid; 1 = None or 2 = VNC)
                recData = new Byte[4];
                bytes = _dataStream.Read(recData, 0, recData.Length);
                SetSecurityType(recData);
            }
            else //Versions higher or equal to 3.7
            {
                //Get Count of Securitytypes
                var secTypeCount = (byte)_dataStream.ReadByte();

                Log(Logtype.Debug, "Supported Server authorisationtypes: " + secTypeCount);

                if (secTypeCount == 0) //Failed/Invalid
                {
                    //Set SecurityType to Failure
                    SetSecurityType(new Byte[4] { 0, 0, 0, 0 });

                    //Get the Failure Reason Lenght
                    var failLenght = ReadUInt32();

                    //Get the Failure Reason Text
                    recData = new Byte[failLenght];
                    bytes = _dataStream.Read(recData, 0, recData.Length);

                    Log(Logtype.User, "Authorisation failed because no Securitytypes are supported. Reason: " + System.Text.Encoding.ASCII.GetString(recData, 0, bytes));

                    //Trigger ConnectionFailed-Event
                    if (ConnectionFailed != null)
                    {
                        ConnectionFailed(null,
                            new ConnectionFailedEventArgs("Connection failed",
                                System.Text.Encoding.ASCII.GetString(recData, 0, bytes), 0));
                    }
                    return;
                }
                else //Successful
                {
                    //Buffer to store the response bytes; each Type 1 Byte (secTypeCount Bytes with Securitytypenumbers)
                    recData = new Byte[secTypeCount];
                    bytes = _dataStream.Read(recData, 0, recData.Length);

                    //TODO: Choose a SecurtyType dynamically when more note None & VNC are implemented

                    //Send the chosen SecurityType to the Server
                    if (hasPassword)
                    {
                        sendData = new Byte[1] { 2 }; //Choose VNC-Authentication
                        Log(Logtype.Debug, "VNC authorisation set by Client");
                    }
                    else
                    {
                        sendData = new Byte[1] { 1 }; //Choose No Authentication
                        Log(Logtype.Debug, "No authorisation set by Client");
                    }

                    _dataStream.Write(sendData, 0, sendData.Length); //Send used SecurityType to Server

                    SetSecurityType(new Byte[4] { 0, 0, 0, sendData[0] }); //Set SecurityType locally to VNC
                }
            }
        }

        /// <summary>
        /// Authenticates with using the handled SecurityType (see 6.2)
        /// </summary>
        /// <returns>True, if authentication was successful</returns>
        private bool Authenticate()
        {
            Byte[] recData;
            int bytes;

            switch (Properties.RfbSecurityType) //Related on the SecurityType authenticate
            {
                default: //A not supported Securitytype
                    if (ConnectionFailed != null)
                    {
                        Log(Logtype.Warning, "The SecurityType " + Properties.RfbSecurityType.ToString() + " is not supported.");
                        ConnectionFailed(null, new ConnectionFailedEventArgs("SecurityType not supported",
                            "The SecurityType " + Properties.RfbSecurityType.ToString() +
                            " is not supported.", 0));
                    }
                    return false;

                case SecurityType.Invalid:
                    throw new Exception("This should not happen :(");

                case SecurityType.None:
                    if (Properties.RfbServerVersion2.Minor > 7) //on 3.8 and above - Read SecurityResult
                    {
                        if (ReadUInt32() == 0)
                        {
                            Log(Logtype.Information, "Authentication Successful, because authentication set to none");
                            return true;
                        }
                        else //== 1 => Failed
                        {
                            AuthenticationFailed(); //Handle a failed authentication
                            return false;
                        }
                    }
                    else //on 3.7 and below  - do nothing
                    {
                        Log(Logtype.Information, "Authentication Successful, because authentication set to none");
                        return true;
                    }

                case SecurityType.VNCAuthentication:
                    Log(Logtype.Information, "Authenticate using VNC-Authentication");

                    //Get the 16 Byte-Challenge from the Server:
                    recData = new Byte[16];
                    bytes = _dataStream.Read(recData, 0, recData.Length);


                    //Passwordbyte-Array
                    var pwVnc = new Byte[8];

                    //Password maximum lenght is 8 Bytes
                    if (Properties.Password.Length < 8)
                    {
                        System.Text.Encoding.ASCII.GetBytes(Properties.Password, 0, Properties.Password.Length, pwVnc, 0);
                    }
                    else //If the Length is longer than 8 Bytes (=Signs), only use the first 8 characters
                    {
                        System.Text.Encoding.ASCII.GetBytes(Properties.Password, 0, 8, pwVnc, 0);
                    }

                    //Change order of bytes by Bitshifting
                    for (var i = 0; i < 8; i++)
                    {
                        pwVnc[i] = (byte)(((pwVnc[i] & 1) << 7) |
                                          ((pwVnc[i] & 2) << 5) |
                                          ((pwVnc[i] & 4) << 3) |
                                          ((pwVnc[i] & 8) << 1) |
                                          ((pwVnc[i] & 16) >> 1) |
                                          ((pwVnc[i] & 32) >> 3) |
                                          ((pwVnc[i] & 64) >> 5) |
                                          ((pwVnc[i] & 128) >> 7));
                    }

                    //DES Encryption
                    DES desEncryption = DES.Create();
                    desEncryption.Mode = CipherMode.ECB;
                    desEncryption.Padding = PaddingMode.None;

                    var encryptor = desEncryption.CreateEncryptor(pwVnc, desEncryption.IV);

                    //Generate the Responsekey for the Challenge
                    var challengeResponse = new Byte[16];
                    encryptor.TransformBlock(recData, 0, recData.Length, challengeResponse, 0);

                    //Send the Challengeresponse
                    _dataStream.Write(challengeResponse, 0, challengeResponse.Length);

                    //Get the SecurityResult
                    if (ReadUInt32() == 0) //OK
                    {
                        Log(Logtype.Information, "VNC Authentication successful");
                        return true;
                    }
                    else //== 1 => Failed
                    {
                        AuthenticationFailed(); //Handle a failed authentication
                        return false;
                    }
            }
        }

        /// <summary>
        /// Handles a failed authentication
        /// </summary>
        private void AuthenticationFailed()
        {
            if (Properties.RfbClientVersion2.Minor > 7) //In Version 3,8 and higher, get the Reason
            {
                //Get the Failure Reason Lenght
                var failLenght = ReadUInt32();

                //Get the Failure Reason Text
                var recData = new Byte[failLenght];
                var bytes = _dataStream.Read(recData, 0, recData.Length);

                Log(Logtype.User, "Authentication failed: " + System.Text.Encoding.ASCII.GetString(recData, 0, bytes));
                if (ConnectionFailed != null)
                {
                    ConnectionFailed(null,
                        new ConnectionFailedEventArgs("Authentication failed",
                            System.Text.Encoding.ASCII.GetString(recData, 0, bytes),
                            0));
                }
            }
            else //Version 3.7 and below
            {
                Log(Logtype.Warning, "Authentication failed: Password wrong?");
                if (ConnectionFailed != null)
                {
                    ConnectionFailed(null,
                        new ConnectionFailedEventArgs("Authentication failed",
                            "Authentication failed; using wrong password?",
                            0));
                }
            }
        }

        /// <summary>
        /// Sends the SharedFlag to the Server (sse. 6.3.1)
        /// </summary>
        private bool SendClientSharedFlag()
        {
            try
            {
                Log(Logtype.Information, "Send ClientSharedFlag (aka ClientInit)");

                //Send the Shared-Flag (ClientInit)
                var sharedFlag = new Byte[1] { Properties.SharedFlag ? (byte)1 : (byte)0 };
                _dataStream.Write(sharedFlag, 0, sharedFlag.Length); //Send the SharedFlag

                return true;
            }
            catch (Exception ea)
            {
                Log(Logtype.Error, "SendClientSharedFlag failed: " + ea);
                return false;
            }
        }

        /// <summary>
        /// Receive the ServerInit (see 6.3.2)
        /// </summary>
        private bool ReceiveServerInit()
        {
            try
            {
                Log(Logtype.Information, "Receive Server Initialisation Parameter");

                //Receive the ServerInit
                //Framebuffer Width
                Properties.FramebufferWidth = ReadUInt16();

                //Framebuffer Height
                Properties.FramebufferHeight = ReadUInt16();

                //Set how many byte a Row have
                _backBuffer2RawStride = (Properties.FramebufferWidth * _backBuffer2PixelBpp + 7) / 8;

                //Initialize the Backend-Backbuffer with the correct size
                //_BackBuffer2PixelData = new byte[_BackBuffer2RawStride * Properties.FramebufferHeight];

                //Server Pixel Format
                var newPxf = new PixelFormat(ReadByte(),
                    ReadByte(),
                    ReadBool(),
                    ReadBool(),
                    ReadUInt16(),
                    ReadUInt16(),
                    ReadUInt16(),
                    ReadByte(),
                    ReadByte(),
                    ReadByte());
                Properties.PxFormat = newPxf;

                ReadBytePadding(3);

                //Name Lenght
                var nameLenght = ReadUInt32();

                //Name String
                var recData = new Byte[nameLenght];
                var bytes = _dataStream.Read(recData, 0, recData.Length);
                Properties.ConnectionName = System.Text.Encoding.ASCII.GetString(recData, 0, recData.Length);

                Log(Logtype.Debug, "Server Initialisationparameter Received. w:" + Properties.FramebufferWidth + " h:" + Properties.FramebufferHeight +
                                   " bbp:" + Properties.PxFormat.BitsPerPixel + " dep:" + Properties.PxFormat.Depth + " big:" + Properties.PxFormat.BigEndianFlag +
                                   " true:" + Properties.PxFormat.TrueColourFlag + " rmax:" + Properties.PxFormat.RedMax + " gmax:" + Properties.PxFormat.GreenMax +
                                   " bmax:" + Properties.PxFormat.BlueMax + " rsh:" + Properties.PxFormat.RedShift + " gsh:" + Properties.PxFormat.GreenShift +
                                   " bsh:" + Properties.PxFormat.BlueShift + " Remotename: " + Properties.ConnectionName);

                return true;
            }
            catch (Exception ea)
            {
                Log(Logtype.Error, "Error during receiving ServerInit:" + ea.ToString());
                return false;
            }
        }

        //#region Screenhandling
        //public byte[] GetScreen()
        //{
        //	 return (_BackBuffer2PixelData);
        //}
        //#endregion

        /// <summary>
        /// Waits for Data from the Server
        /// </summary>
        private void Receiver(object sender, DoWorkEventArgs e)
        {
            while (!_disconnectionInProgress) //Wait for ServerMessages until the Client (or Server) wants to disconnect
            {
                var srvMsg = GetServerMessageType();
                switch (srvMsg)
                {
                    default:
                        Log(Logtype.Warning, "Receiving unsupported Messagetype");
                        if (NotSupportedServerMessage != null)
                        {
                            NotSupportedServerMessage(null, new NotSupportedServerMessageEventArgs(srvMsg.ToString()));
                        }
                        break;
                    case ServerMessageType.Unknown:
                        Log(Logtype.Warning, "Receiving unknown Messagetype");
                        if (NotSupportedServerMessage != null)
                        {
                            NotSupportedServerMessage(null, new NotSupportedServerMessageEventArgs("Unknown Servermessagetype"));
                        }
                        break;
                    case ServerMessageType.Bell:
                        Log(Logtype.Information, "Receiving Bell");
                        ReceiveBell();
                        break;
                    case ServerMessageType.SetColourMapEntries:
                        Log(Logtype.Debug, "Receiving SetColourMapEntries");
                        ReceiveSetColourMapEntries();
                        break;
                    case ServerMessageType.FramebufferUpdate:
                        Log(Logtype.Debug, "Receiving FramebufferUpdate");
                        ReceiveFramebufferUpdate();
                        break;
                    case ServerMessageType.ServerCutText:
                        Log(Logtype.Debug, "Receiving ServerCutText");
                        ReceiveServerCutText();
                        break;
                }
            }
        }

        /// <summary>
        /// Gets the ServerMessageType by the Next Byte
        /// </summary>
        /// <returns></returns>
        private ServerMessageType GetServerMessageType()
        {
            var recData = new Byte[1];
            _dataStream.Read(recData, 0, 1);
            return GetServerMessageType(recData[0]);
        }

        /// <summary>
        /// Gets the ServerMessageType by the Next Byte
        /// </summary>
        /// <returns></returns>
        private ServerMessageType GetServerMessageType(Byte serverMessageId)
        {
            switch (serverMessageId)
            {
                default:
                    return ServerMessageType.Unknown;
                case 0:
                    return ServerMessageType.FramebufferUpdate;
                case 1:
                    return ServerMessageType.SetColourMapEntries;
                case 2:
                    return ServerMessageType.Bell;
                case 3:
                    return ServerMessageType.ServerCutText;
                case 249:
                    return ServerMessageType.OLIVE_Call_Control;
                case 250:
                    return ServerMessageType.Colin_dean_xvp;
                case 252:
                    return ServerMessageType.tight;
                case 253:
                    return ServerMessageType.gii;
                case 127:
                case 254:
                    return ServerMessageType.VMWare;
                case 255:
                    return ServerMessageType.Anthony_Liguori;
            }
        }

        /// <summary>
        /// Receives ne Frames after a Framebufferupdaterquest (see 6.5.1)
        /// </summary>
        private void ReceiveFramebufferUpdate()
        {
            //_LastReceiveTimer.Stop(); //Stop the Timer for Frameupdates

            ReadBytePadding(1);

            var rectCount = ReadUInt16();

            Log(Logtype.Debug, "FramebufferUpdate received. Frames: " + rectCount);
            var rects = new List<RfbRectangle>(rectCount);

            for (var i = 0; i < rectCount; i++)
            {
                var rRec = new RfbRectangle(ReadUInt16(), ReadUInt16(), ReadUInt16(), ReadUInt16(), ReadByte(4));
                rRec.PixelData = ReceiveRectangleData(rRec.EncodingType, rRec.Width, rRec.Height);

                //Read the Pixelinformation, depending on EncryptionType and report it to the UI-Mainthread
                if (rRec.PixelData != null)
                    rects.Add(rRec);
            }

            _receiver.ReportProgress(0, rects);

            _dataStream.Flush();

            //_LastReceiveTimer.Start(); //Start the Timer for Frameupdates
        }


        private void ReceiveSetColourMapEntries()
        {
            var start = DateTime.Now;
            Log(Logtype.Debug, "Receive ColorMapEntries started");

            var firstColor = ReadUInt16();
            var colorCount = ReadUInt16();

            for (var i = 0; i < colorCount; i++)
            {
                _colorMap[firstColor + i, 0] = ReadUInt16();
                _colorMap[firstColor + i, 1] = ReadUInt16();
                _colorMap[firstColor + i, 2] = ReadUInt16();
            }

            _dataStream.Flush();

            Log(Logtype.Debug, "Receiving ColormapEntries finished. Duration: " + DateTime.Now.Subtract(start).TotalMilliseconds + "ms");
        }

        /// <summary>
        /// Beeps the Client (see 6.5.3)
        /// </summary>
        private void ReceiveBell()
        {
            Log(Logtype.Information, "DingDong");
            Console.Beep(1000, 500);
            Console.Beep(800, 500);
        }

        /// <summary>
        /// Copys a text to the local cache (see 6.5.4)
        /// </summary>
        private void ReceiveServerCutText()
        {
            var recData = new Byte[3];
            _dataStream.Read(recData, 0, recData.Length);

            recData = new Byte[4];
            _dataStream.Read(recData, 0, recData.Length);

            var textLength = Helper.ConvertToUInt32(recData, _properties.PxFormat.BigEndianFlag);

            recData = new Byte[textLength];
            _dataStream.Read(recData, 0, recData.Length);

            var cacheText = System.Text.Encoding.ASCII.GetString(recData);

            Log(Logtype.Debug, "New ServerCutText received. Text: " + cacheText);

            ServerCutText?.Invoke(this, new ServerCutTextEventArgs(cacheText));
        }

        /// <summary>
        /// (see 6.4.1)
        /// </summary>
        private void SendSetPixelFormat(PixelFormat pxFormat)
        {
            Log(Logtype.Debug, "Send SetPixelFormat");

            var data = new Byte[20];
            data[0] = 0; //SetPixel

            pxFormat.getPixelFormat(_properties.PxFormat.BigEndianFlag).CopyTo(data, 4);
            _dataStream.Write(data, 0, data.Length);

            _dataStream.Flush();
        }

        /// <summary>
        /// SetEncodings (see 6.4.2)
        /// </summary>
        private void SendSetEncodings()
        {
            Log(Logtype.Debug, "Send SetEncoding (" + _encodingDetails.Count + ")");

            var data = new Byte[4];
            data[0] = 2;

            //Send Count of Supported Encodings by this client
            Helper.ConvertToByteArray((UInt16)_encodingDetails.Count, _properties.PxFormat.BigEndianFlag).CopyTo(data, 2);
            _dataStream.Write(data, 0, data.Length);

            //Send Encoding Details for each supported Encoding
            foreach (var kvp in _encodingDetails)
            {
                var sendByte = Helper.ConvertToByteArray(kvp.Value.Id, _properties.PxFormat.BigEndianFlag);
                _dataStream.Write(new Byte[4] { sendByte[0], sendByte[1], sendByte[2], sendByte[3] }, 0, 4);
            }

            _dataStream.Flush();
        }

        /// <summary>
        /// Sends a Request for Screenupdate (see 6.4.3)
        /// </summary>
        /// <param name="isIncremental"></param>
        /// <param name="posX"></param>
        /// <param name="posY"></param>
        /// <param name="width"></param>
        /// <param name="height"></param>
        private void SendFramebufferUpdateRequest(bool isIncremental, UInt16 posX, UInt16 posY, UInt16 width, UInt16 height)
        {
            if (_dataChannel.IsAddingCompleted)
                return;
            Log(Logtype.Debug, "Send SetFramebufferUpdateRequest with the following parameters: Incr:" + isIncremental + ", PosX:" + posX + ", PosY:" + posY + ", Width:" + width + ", Height:" + height);

            var data = new Byte[10];
            data[0] = 3;
            data[1] = (Byte)(isIncremental ? 1 : 0);
            Helper.ConvertToByteArray(posX, _properties.PxFormat.BigEndianFlag).CopyTo(data, 2);
            Helper.ConvertToByteArray(posY, _properties.PxFormat.BigEndianFlag).CopyTo(data, 4);
            Helper.ConvertToByteArray(width, _properties.PxFormat.BigEndianFlag).CopyTo(data, 6);
            Helper.ConvertToByteArray(height, _properties.PxFormat.BigEndianFlag).CopyTo(data, 8);

            if (_dataStream != null)
            {
                _dataStream.Write(data, 0, data.Length);
                _dataStream.Flush();
            }
            
        }

        /// <summary>
        /// Send a Key in pressed or released state (see http://www.cl.cam.ac.uk/~mgk25/ucs/keysymdef.h)
        /// </summary>
        /// <param name="e"></param>
        /// <param name="isDown"></param>
        private void SendKeyEvent(VncKey e, bool isDown)
        {
            if (_isConnected == false) return;

            //Track the shift modifier so printable keys resolve without a UI framework
            if (e == VncKey.LeftShift || e == VncKey.RightShift)
                _shiftDown = isDown;

            Log(Logtype.Debug, "Sending Key: " + e.ToString());

            UInt32 keyCode = 0;

            Log(Logtype.Information, "Sending " + e.ToString());

            keyCode = GetKeyCode(e);

            if (keyCode == 0) //Was not a Special Sign
            {
                //Get the Keycode
                var key = VncKeyInterop.VirtualKeyFromKey(e);

                //Get the related Char
                var enteredChar = System.Text.Encoding.ASCII.GetChars(Helper.ConvertToByteArray(key, true))[0];

                //Offset for Keycode of pressed Sign
                UInt32 offset = 0;

                var rgExAz = new Regex("[A-Z]"); //for A-Z and a-z
                var rgEx09 = new Regex("[0-9]"); //For 0-9

                if (rgExAz.IsMatch(enteredChar.ToString())) //If it should be a small letter
                {
                    if (!_shiftDown)
                    {
                        key += 32;
                        enteredChar = System.Text.Encoding.ASCII.GetChars(Helper.ConvertToByteArray(key, true))[0];
                    }
                }

                else if (rgEx09.IsMatch(enteredChar.ToString()) && !_shiftDown) //It is a number
                {
                    //Do nothing
                }

                else
                {
                    offset = 0xFF00; //65280
                }
                keyCode = (UInt32)key + offset;
            }

            var data = new Byte[8];
            data[0] = 4;

            //Press or Release the Key
            if (isDown)
                data[1] = 1;
            else
                data[1] = 0;

            Helper.ConvertToByteArray(keyCode, _properties.PxFormat.BigEndianFlag).CopyTo(data, 4);
            _dataStream.Write(data, 0, data.Length);
        }

        private uint GetKeyCode(VncKey e)
        {
            uint keyCode = 0x00;
            switch (e)
            {
                case VncKey.LeftShift:
                    keyCode = 0x0000ffe1;
                    break;

                case VncKey.Space:
                    keyCode = 0x00000020;
                    break;
                case VncKey.Tab:
                    keyCode = 0x0000FF09;
                    break;
                case VncKey.Enter:
                    keyCode = 0x0000FF0D;
                    break;
                case VncKey.Escape:
                    keyCode = 0x0000FF1B;
                    break;
                case VncKey.Home:
                    keyCode = 0x0000FF50;
                    break;
                case VncKey.Left:
                    keyCode = 0x0000FF51;
                    break;
                case VncKey.Up:
                    keyCode = 0x0000FF52;
                    break;
                case VncKey.Right:
                    keyCode = 0x0000FF53;
                    break;
                case VncKey.Down:
                    keyCode = 0x0000FF54;
                    break;
                case VncKey.PageUp:
                    keyCode = 0x0000FF55;
                    break;
                case VncKey.PageDown:
                    keyCode = 0x0000FF56;
                    break;
                case VncKey.End:
                    keyCode = 0x0000FF57;
                    break;
                case VncKey.Insert:
                    keyCode = 0x0000FF63;
                    break;
                case VncKey.Delete:
                    keyCode = 0x0000FFFF;
                    break;

                case VncKey.CapsLock:
                    keyCode = 0x0000FFE5;
                    break;
                case VncKey.LeftAlt:
                    keyCode = 0x0000FFE9;
                    break;
                case VncKey.RightAlt:
                    keyCode = 0x0000FFEA;
                    break;
                case VncKey.LeftCtrl:
                    keyCode = 0x0000FFE3;
                    break;
                case VncKey.RightCtrl:
                    keyCode = 0x0000FFE4;
                    break;
                case VncKey.LWin:
                    keyCode = 0x0000FFEB;
                    break;
                case VncKey.RWin:
                    keyCode = 0x0000FFEC;
                    break;
                case VncKey.Apps:
                    keyCode = 0x0000FFEE;
                    break;

                case VncKey.F1:
                    keyCode = 0x0000FFBE;
                    break;
                case VncKey.F2:
                    keyCode = 0x0000FFBF;
                    break;
                case VncKey.F3:
                    keyCode = 0x0000FFC0;
                    break;
                case VncKey.F4:
                    keyCode = 0x0000FFC1;
                    break;
                case VncKey.F5:
                    keyCode = 0x0000FFC2;
                    break;
                case VncKey.F6:
                    keyCode = 0x0000FFC3;
                    break;
                case VncKey.F7:
                    keyCode = 0x0000FFC4;
                    break;
                case VncKey.F8:
                    keyCode = 0x0000FFC5;
                    break;
                case VncKey.F9:
                    keyCode = 0x0000FFC6;
                    break;
                case VncKey.F10:
                    keyCode = 0x0000FFC7;
                    break;
                case VncKey.F11:
                    keyCode = 0x0000FFC8;
                    break;
                case VncKey.F12:
                    keyCode = 0x0000FFC9;
                    break;

                case VncKey.NumLock:
                    keyCode = 0x0000FF7F;
                    break;
                case VncKey.NumPad0:
                    keyCode = 0x0000FFB0;
                    break;
                case VncKey.NumPad1:
                    keyCode = 0x0000FFB1;
                    break;
                case VncKey.NumPad2:
                    keyCode = 0x0000FFB2;
                    break;
                case VncKey.NumPad3:
                    keyCode = 0x0000FFB3;
                    break;
                case VncKey.NumPad4:
                    keyCode = 0x0000FFB4;
                    break;
                case VncKey.NumPad5:
                    keyCode = 0x0000FFB5;
                    break;
                case VncKey.NumPad6:
                    keyCode = 0x0000FFB6;
                    break;
                case VncKey.NumPad7:
                    keyCode = 0x0000FFB7;
                    break;
                case VncKey.NumPad8:
                    keyCode = 0x0000FFB8;
                    break;
                case VncKey.NumPad9:
                    keyCode = 0x0000FFB9;
                    break;

            }
            return keyCode;
        }

        /// <summary>
        /// Send a KeyEvent to the Server based on a sign (see 6.4.4)
        /// </summary>
        /// <param name="sign">the Sign to send like typing a key on a keyboard</param>
        private void SendSignEvent(char sign)
        {
            if (_isConnected == false) return;

            Log(Logtype.Information, "Sending " + sign);

            var data = new Byte[8];
            data[0] = 4;

            //Press or Release the Key (currently defiend as released)
            data[1] = 1;

            Helper.ConvertToByteArray(_keyCodes[sign], _properties.PxFormat.BigEndianFlag).CopyTo(data, 4);
            _dataStream.Write(data, 0, data.Length);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="buttonMask">bit1=left; bit2=middle; bit3=right; bit4=wheelup; bit5=wheeldown</param>
        /// <param name="posX"></param>
        /// <param name="posY"></param>
        private void SendPointerEvent(UInt16 posX, UInt16 posY, Byte buttonMask)
        {
            if (_isConnected == false) return;

            Log(Logtype.Debug, "Send Pointer: Button:" + buttonMask + ", PosX:" + posX + ", PosY:" + posY);
            //Console.WriteLine("Send Pointer: Button:" + buttonMask + ", PosX:" + posX + ", PosY:" + posY);
            var data = new Byte[6];
            data[0] = 5;
            data[1] = buttonMask;
            Helper.ConvertToByteArray(posX, _properties.PxFormat.BigEndianFlag).CopyTo(data, 2);
            Helper.ConvertToByteArray(posY, _properties.PxFormat.BigEndianFlag).CopyTo(data, 4);

            _dataStream.Write(data, 0, data.Length);

            //update executed commands list
            if(MouseActionCaptureEnabled)
                UpdateExecutedCommands(posX, posY, buttonMask);
        }

        public bool MouseActionCaptureEnabled { get; set; }

        private byte _previousMouseAction;
        private void UpdateExecutedCommands(ushort posX, ushort posY, byte buttonMask)
        {
            if (ExecutedCommands.Count >= MaxCountsExecutedCommands)
            {
                ExecutedCommands.Clear();
            }

            switch (buttonMask)
            {
                case 0:
                    if (_previousMouseAction == 1 && _currentCommand != null)
                    {
                        if (_currentCommand.Action == Command.Drag)
                        {
                            VncCommand temp = new VncDragCommand(){X = _currentCommand.X, Y = _currentCommand.Y, X2 = posX, Y2 = posY};
                            ExecutedCommands.Add(temp);
                            _currentCommand = null;
                        }
                        else if (_currentCommand.Action == Command.Click)
                        {
                            var temp = _currentCommand;
                            ExecutedCommands.Add( temp);
                            _currentCommand = null;
                        }
                    }
                    break;
                case 1:
                    if (_previousMouseAction == 0) // klick
                    {
                        _currentCommand = new VncPointCommand(){Action = Command.Click, X = posX, Y = posY};
                    }
                    else if (_previousMouseAction == 1 && _currentCommand.Action != Command.Drag && (_previousX != posX || _previousY != posY)) //drag
                    {
                        _currentCommand.Action = Command.Drag;
                    }
                    break;
                case 2:
                case 4:
                    if (_previousMouseAction == 0)
                    {
                        VncCommand temp = new VncCommand(){Action = Command.Home};
                        ExecutedCommands.Add(temp);
                    }
                    break;
                default:
                    break;

            }

            _previousMouseAction = buttonMask;
            _previousX = posX;
            _previousY = posY;
        }

        public Task Play(IEnumerable<IVncCommand> commands, VncCommandPlayerPreviewCommandExecute preview,
            VncCommandPlayerCommandExecuted executed)
        {
            return Task.Run(async () =>
            {
                foreach (var cmd in commands)
                {
                    await Task.Run(() => preview?.Invoke(this, cmd));

                    await cmd.Execute(this);

                    await Task.Run(() => executed?.Invoke(this, cmd));

                    await Task.Delay(500);
                }
            });
        }

        public int MaxCountsExecutedCommands
        {
            get => _maxCountsExecutedCommands;
            set => _maxCountsExecutedCommands = value;
        }

        /// <summary>
        /// Sends a Text to the Cache of the Server (see 6.4.6)
        /// </summary>
        /// <param name="text"></param>
        private void SendClientCutText(string text)
        {
            if (_isConnected == false) return;

            Log(Logtype.Debug, "Send ClientCutText. Text: " + text);

            var textData = System.Text.Encoding.ASCII.GetBytes(text);

            var data = new Byte[8 + textData.Length];
            data[0] = 6;
            Helper.ConvertToByteArray((UInt32)textData.Length, _properties.PxFormat.BigEndianFlag).CopyTo(data, 4);
            textData.CopyTo(data, 8);

            _dataStream.Write(data, 0, data.Length);
        }

        private void SetSecurityType(byte[] responseData)
        {
            var secTypeNo = responseData[3] + responseData[2] * 2 + responseData[1] * 4 + responseData[0] * 8;
            switch (secTypeNo)
            {
                default:
                    Properties.RfbSecurityType = SecurityType.Unknown;
                    break;
                case 0:
                    Properties.RfbSecurityType = SecurityType.Invalid;
                    break;
                case 1:
                    Properties.RfbSecurityType = SecurityType.None;
                    break;
                case 2:
                    Properties.RfbSecurityType = SecurityType.VNCAuthentication;
                    break;
                case 5:
                    Properties.RfbSecurityType = SecurityType.RA2;
                    break;
                case 6:
                    Properties.RfbSecurityType = SecurityType.RA2ne;
                    break;
                case 16:
                    Properties.RfbSecurityType = SecurityType.Tight;
                    break;
                case 17:
                    Properties.RfbSecurityType = SecurityType.UltraVNC;
                    break;
                case 18:
                    Properties.RfbSecurityType = SecurityType.TLS;
                    break;
                case 19:
                    Properties.RfbSecurityType = SecurityType.VeNCrypt;
                    break;
                case 20:
                    Properties.RfbSecurityType = SecurityType.GTK_VNC_SASL;
                    break;
                case 21:
                    Properties.RfbSecurityType = SecurityType.MD5_hash_authentication;
                    break;
                case 22:
                    Properties.RfbSecurityType = SecurityType.Colin_Dean_xvp;
                    break;
            }

            Log(Logtype.Information, "Securitytype is " + Properties.RfbSecurityType.ToString());
        }

        private Byte[] ReceiveRectangleData(Byte[] encryptionType, UInt16 width, UInt16 height)
        {
            //Byte[, ,] retValue = new Byte[width, height, 4]; //3rd Dimension is the Color (RBGA)
            var start = DateTime.Now;

            try
            {
                var test = new List<byte>();

                Log(Logtype.Information, "Receiving RectangleData. Encodingtype is " + encryptionType[0] + "; Framesize: " + width + "x" + height);

                var encrNr = Helper.ConvertToUInt32(encryptionType, _properties.PxFormat.BigEndianFlag);
                switch (encrNr)
                {
                    default:
                    case 1: //CopyRect
                    case 2: //RRE
                    case 5: //Hextile
                    case 16: //ZRLE
                        Log(Logtype.Information, "Encodingtype " + encrNr + " currently not supported");
                        break;

                    case 0: //Raw

                        _properties.EncodingType = RfbEncoding.Raw_ENCODING;

                        var myData = new Byte[width * height * 4];
                        //Console.WriteLine("!!!!! W: {0}", width);

                        var readCount = 0;

                        while (readCount != myData.Length) //Get all Bytes
                        {
                            readCount += _dataStream.Read(myData, readCount, myData.Length - readCount);
                            //Debug.WriteLine("{0}/{1}", readCount, myData.Length);
                        }

                        return myData;

                    //Encode the received data
                    //retValue = RawRectangle.EncodeRawRectangle(height, width, myData);
                    //break;

                    case 4294967073: //See 6.7.2
                        //Set a new Backbuffersize
                        //_BackBuffer2PixelData = new byte[width*height*4];

                        //Console.WriteLine("!!!!! INIT W: {0}", width);
                        //Request new Screen
                        SendFramebufferUpdateRequest(true, 0, 0, width, height);
                        break;
                }


            }
            catch (Exception ea)
            {
                Log(Logtype.Error, "Error on receiving a RawRectangle: " + ea.ToString());
            }

            Log(Logtype.Debug, "Finished receiving RectangleData. Duration: " + DateTime.Now.Subtract(start).TotalMilliseconds + "ms");
            //return (retValue);
            return null;
        }

        private Boolean ReadBool()
        {
            try
            {
                if (ReadByte(1)[0] == 0)
                    return false;
                else
                    return true;
            }
            catch (Exception)
            {
                Log(Logtype.Warning, "Remoteconnection closed by Server");
                return false;
            }
        }

        private Byte ReadByte()
        {
            return ReadByte(1)[0];
        }

        private Byte[] ReadByte(UInt64 count)
        {
            try
            {
                var recData = new Byte[count];
                _dataStream.Read(recData, 0, recData.Length);

                //foreach (var b in recData)
                //	Debug.Write(string.Format("{0:X2} ", b));

                //Debug.Write("-> ");

                //foreach (var b in recData)
                //	Debug.Write(string.Format("{0} ", b));

                //Debug.Write("-> ");

                //foreach (var b in recData)
                //	Debug.Write(string.Format("{0} ", (char)b));

                //Debug.WriteLine("");

                return recData;
            }
            catch (Exception)
            {
                Log(Logtype.Warning, "Remoteconnection closed by Server");
                return new Byte[0];
            }
        }

        private UInt16 ReadUInt16()
        {
            return ReadUInt16(false);
        }

        private UInt16 ReadUInt16(bool isBigEndian)
        {
            try
            {
                var recData = ReadByte(2);
                return Helper.ConvertToUInt16(recData, isBigEndian);
            }
            catch (Exception)
            {
                Log(Logtype.Warning, "Remoteconnection closed by Server");
                return 0;
            }
        }

        private UInt32 ReadUInt32()
        {
            return ReadUInt32(false);
        }

        private UInt32 ReadUInt32(bool isBigEndian)
        {
            try
            {
                var recData = ReadByte(4);
                return Helper.ConvertToUInt32(recData, isBigEndian);
            }
            catch (Exception)
            {
                Log(Logtype.Warning, "Remoteconnection closed by Server");
                return 0;
            }

        }

        private UInt64 ReadUInt64()
        {
            throw new NotImplementedException("Not done");
        }

        private void ReadBytePadding(UInt64 count)
        {
            try
            {
                ReadByte(count);
            }
            catch (Exception)
            {
                Log(Logtype.Warning, "Remoteconnection closed by Server");
            }
        }


        private void Log(Logtype lt, string lm) { } //logging interface


        public void RefreshScreen()
        {
            SendFramebufferUpdateRequest(true, 0, 0, _properties.FramebufferWidth, _properties.FramebufferHeight);
        }

        /// <summary>
        /// Send a pressed Key to the Server (i.e. Enter, Tab, Cntr, Alt etc.)
        /// </summary>
        /// <param name="e"></param>
        public void SendKey(VncKey key, bool isDown)
        {
            SendKeyEvent(key, isDown);
        }

        /// <summary>
        /// Send a Sign to the Server
        /// </summary>
        /// <param name="sign"></param>
        public void SendSign(char sign)
        {
            SendSignEvent(sign);
        }

        /// <summary>
        /// Sends a Mousemove to the Server
        /// </summary>
        /// <param name="posX">X-Position of the Mouse</param>
        /// <param name="posY">Y-Position of the Mouse</param>
        public void SendMousePosition(UInt16 posX, UInt16 posY)
        {
            SendPointerEvent(posX, posY, 0);
        }

        /// <summary>
        /// Sends a Click to the Server
        /// </summary>
        /// <param name="posX">X-Position of the Mouse</param>
        /// <param name="posY">Y-Position of the Mouse</param>
        /// <param name="button">1=left 4=right 2=middle 8=wheelup 16=wheeldown</param>
        public void SendMouseClick(UInt16 posX, UInt16 posY, byte button)
        {
            SendPointerEvent(posX, posY, button);
        }

        /// <summary>
        /// Send a special Key Combination to the Server
        /// </summary>
        /// <param name="keyComb"></param>
        public void SendKeyCombination(KeyCombination keyComb)
        {
            var aKey1 = default(VncKey);
            var aKey2 = default(VncKey);
            var aKey3 = default(VncKey);

            switch (keyComb)
            {
                case KeyCombination.AltF4:
                    aKey1 = VncKey.LeftAlt;
                    aKey2 = VncKey.F4;
                    break;
                case KeyCombination.AltTab:
                    aKey1 = VncKey.LeftAlt;
                    aKey2 = VncKey.Tab;
                    break;
                case KeyCombination.CapsLock:
                    aKey1 = VncKey.CapsLock;
                    break;
                case KeyCombination.CtrlAltDel:
                    aKey1 = VncKey.LeftCtrl;
                    aKey2 = VncKey.LeftAlt;
                    aKey3 = VncKey.Delete;
                    break;
                case KeyCombination.CtrlAltEnd:
                    aKey1 = VncKey.LeftCtrl;
                    aKey2 = VncKey.LeftAlt;
                    aKey3 = VncKey.End;
                    break;
                case KeyCombination.CtrlEsc:
                    aKey1 = VncKey.LeftCtrl;
                    aKey2 = VncKey.Escape;
                    break;
                case KeyCombination.NumLock:
                    aKey1 = VncKey.NumLock;
                    break;
                case KeyCombination.Print:
                    aKey1 = VncKey.PrintScreen;
                    break;
                case KeyCombination.Scroll:
                    aKey1 = VncKey.Scroll;
                    break;
            }

            SendKeyEvent(aKey1, true);
            if (aKey2 != default(VncKey)) SendKeyEvent(aKey2, true);
            if (aKey3 != default(VncKey)) SendKeyEvent(aKey3, true);

            Thread.Sleep(100);

            if (aKey3 != default(VncKey)) SendKeyEvent(aKey3, false);
            if (aKey2 != default(VncKey)) SendKeyEvent(aKey2, false);
            SendKeyEvent(aKey1, false);
        }

        public ConnectionProperties Properties
        {
            get => _properties;
            set => _properties = value;
        }

        public bool IsConnected => _isConnected;
    }
}