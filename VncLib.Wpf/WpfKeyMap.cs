using VncLib;
using System.Windows.Input;

namespace VncLib.Wpf
{
    internal static class WpfKeyMap
    {
        public static VncKey ToVncKey(Key key)
        {
            switch (key)
            {
                // Letters
                case Key.A: return VncKey.A;
                case Key.B: return VncKey.B;
                case Key.C: return VncKey.C;
                case Key.D: return VncKey.D;
                case Key.E: return VncKey.E;
                case Key.F: return VncKey.F;
                case Key.G: return VncKey.G;
                case Key.H: return VncKey.H;
                case Key.I: return VncKey.I;
                case Key.J: return VncKey.J;
                case Key.K: return VncKey.K;
                case Key.L: return VncKey.L;
                case Key.M: return VncKey.M;
                case Key.N: return VncKey.N;
                case Key.O: return VncKey.O;
                case Key.P: return VncKey.P;
                case Key.Q: return VncKey.Q;
                case Key.R: return VncKey.R;
                case Key.S: return VncKey.S;
                case Key.T: return VncKey.T;
                case Key.U: return VncKey.U;
                case Key.V: return VncKey.V;
                case Key.W: return VncKey.W;
                case Key.X: return VncKey.X;
                case Key.Y: return VncKey.Y;
                case Key.Z: return VncKey.Z;

                // Digits
                case Key.D0: return VncKey.D0;
                case Key.D1: return VncKey.D1;
                case Key.D2: return VncKey.D2;
                case Key.D3: return VncKey.D3;
                case Key.D4: return VncKey.D4;
                case Key.D5: return VncKey.D5;
                case Key.D6: return VncKey.D6;
                case Key.D7: return VncKey.D7;
                case Key.D8: return VncKey.D8;
                case Key.D9: return VncKey.D9;

                // NumPad
                case Key.NumPad0: return VncKey.NumPad0;
                case Key.NumPad1: return VncKey.NumPad1;
                case Key.NumPad2: return VncKey.NumPad2;
                case Key.NumPad3: return VncKey.NumPad3;
                case Key.NumPad4: return VncKey.NumPad4;
                case Key.NumPad5: return VncKey.NumPad5;
                case Key.NumPad6: return VncKey.NumPad6;
                case Key.NumPad7: return VncKey.NumPad7;
                case Key.NumPad8: return VncKey.NumPad8;
                case Key.NumPad9: return VncKey.NumPad9;
                case Key.Multiply: return VncKey.Multiply;
                case Key.Add: return VncKey.Add;
                case Key.Subtract: return VncKey.Subtract;
                case Key.Decimal: return VncKey.Decimal;
                case Key.Divide: return VncKey.Divide;

                // Function Keys
                case Key.F1: return VncKey.F1;
                case Key.F2: return VncKey.F2;
                case Key.F3: return VncKey.F3;
                case Key.F4: return VncKey.F4;
                case Key.F5: return VncKey.F5;
                case Key.F6: return VncKey.F6;
                case Key.F7: return VncKey.F7;
                case Key.F8: return VncKey.F8;
                case Key.F9: return VncKey.F9;
                case Key.F10: return VncKey.F10;
                case Key.F11: return VncKey.F11;
                case Key.F12: return VncKey.F12;

                // Modifier Keys
                case Key.LeftShift: return VncKey.LeftShift;
                case Key.RightShift: return VncKey.RightShift;
                case Key.LeftCtrl: return VncKey.LeftCtrl;
                case Key.RightCtrl: return VncKey.RightCtrl;
                case Key.LeftAlt: return VncKey.LeftAlt;
                case Key.RightAlt: return VncKey.RightAlt;
                case Key.LWin: return VncKey.LWin;
                case Key.RWin: return VncKey.RWin;
                case Key.Apps: return VncKey.Apps;

                // Common Keys
                case Key.Space: return VncKey.Space;
                case Key.Tab: return VncKey.Tab;
                case Key.Return: return VncKey.Enter;
                case Key.Escape: return VncKey.Escape;
                case Key.Back: return VncKey.Back;
                case Key.Home: return VncKey.Home;
                case Key.End: return VncKey.End;
                case Key.Left: return VncKey.Left;
                case Key.Up: return VncKey.Up;
                case Key.Right: return VncKey.Right;
                case Key.Down: return VncKey.Down;
                case Key.PageUp: return VncKey.PageUp;
                case Key.PageDown: return VncKey.PageDown;
                case Key.Insert: return VncKey.Insert;
                case Key.Delete: return VncKey.Delete;
                case Key.CapsLock: return VncKey.CapsLock;
                case Key.NumLock: return VncKey.NumLock;
                case Key.Scroll: return VncKey.Scroll;
                case Key.PrintScreen: return VncKey.PrintScreen;
                case Key.Pause: return VncKey.Pause;

                // OEM Keys
                case Key.OemPlus: return VncKey.OemPlus;
                case Key.OemMinus: return VncKey.OemMinus;
                case Key.OemComma: return VncKey.OemComma;
                case Key.OemPeriod: return VncKey.OemPeriod;
                case Key.OemSemicolon: return VncKey.OemSemicolon;
                case Key.OemQuestion: return VncKey.OemQuestion;
                case Key.OemTilde: return VncKey.OemTilde;
                case Key.OemOpenBrackets: return VncKey.OemOpenBrackets;
                case Key.OemCloseBrackets: return VncKey.OemCloseBrackets;
                case Key.OemPipe: return VncKey.OemPipe;
                case Key.OemQuotes: return VncKey.OemQuotes;
                case Key.OemBackslash: return VncKey.OemBackslash;

                default: return VncKey.None;
            }
        }
    }
}
