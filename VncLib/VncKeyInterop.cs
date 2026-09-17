// Copyright 2017 The VncLib Authors. All rights reserved.
// Use of this source code is governed by a BSD-style
// license that can be found in the LICENSE file.

namespace VncLib
{
    /// <summary>
    /// Maps a <see cref="VncKey"/> to its Win32 virtual-key code.
    /// This replaces the WPF <c>KeyInterop.VirtualKeyFromKey</c> call so that the
    /// protocol engine can compute keysyms for printable keys without a UI framework.
    /// </summary>
    internal static class VncKeyInterop
    {
        public static int VirtualKeyFromKey(VncKey key)
        {
            switch (key)
            {
                // Letters map directly to their ASCII/VK code (A=0x41 .. Z=0x5A)
                case VncKey.A: return 0x41;
                case VncKey.B: return 0x42;
                case VncKey.C: return 0x43;
                case VncKey.D: return 0x44;
                case VncKey.E: return 0x45;
                case VncKey.F: return 0x46;
                case VncKey.G: return 0x47;
                case VncKey.H: return 0x48;
                case VncKey.I: return 0x49;
                case VncKey.J: return 0x4A;
                case VncKey.K: return 0x4B;
                case VncKey.L: return 0x4C;
                case VncKey.M: return 0x4D;
                case VncKey.N: return 0x4E;
                case VncKey.O: return 0x4F;
                case VncKey.P: return 0x50;
                case VncKey.Q: return 0x51;
                case VncKey.R: return 0x52;
                case VncKey.S: return 0x53;
                case VncKey.T: return 0x54;
                case VncKey.U: return 0x55;
                case VncKey.V: return 0x56;
                case VncKey.W: return 0x57;
                case VncKey.X: return 0x58;
                case VncKey.Y: return 0x59;
                case VncKey.Z: return 0x5A;

                // Digits (0x30 .. 0x39)
                case VncKey.D0: return 0x30;
                case VncKey.D1: return 0x31;
                case VncKey.D2: return 0x32;
                case VncKey.D3: return 0x33;
                case VncKey.D4: return 0x34;
                case VncKey.D5: return 0x35;
                case VncKey.D6: return 0x36;
                case VncKey.D7: return 0x37;
                case VncKey.D8: return 0x38;
                case VncKey.D9: return 0x39;

                // Numeric keypad
                case VncKey.NumPad0: return 0x60;
                case VncKey.NumPad1: return 0x61;
                case VncKey.NumPad2: return 0x62;
                case VncKey.NumPad3: return 0x63;
                case VncKey.NumPad4: return 0x64;
                case VncKey.NumPad5: return 0x65;
                case VncKey.NumPad6: return 0x66;
                case VncKey.NumPad7: return 0x67;
                case VncKey.NumPad8: return 0x68;
                case VncKey.NumPad9: return 0x69;
                case VncKey.Multiply: return 0x6A;
                case VncKey.Add: return 0x6B;
                case VncKey.Subtract: return 0x6D;
                case VncKey.Decimal: return 0x6E;
                case VncKey.Divide: return 0x6F;

                // Function keys
                case VncKey.F1: return 0x70;
                case VncKey.F2: return 0x71;
                case VncKey.F3: return 0x72;
                case VncKey.F4: return 0x73;
                case VncKey.F5: return 0x74;
                case VncKey.F6: return 0x75;
                case VncKey.F7: return 0x76;
                case VncKey.F8: return 0x77;
                case VncKey.F9: return 0x78;
                case VncKey.F10: return 0x79;
                case VncKey.F11: return 0x7A;
                case VncKey.F12: return 0x7B;

                // Modifiers / system
                case VncKey.Back: return 0x08;
                case VncKey.Tab: return 0x09;
                case VncKey.Enter: return 0x0D;
                case VncKey.Pause: return 0x13;
                case VncKey.CapsLock: return 0x14;
                case VncKey.Escape: return 0x1B;
                case VncKey.Space: return 0x20;
                case VncKey.PageUp: return 0x21;
                case VncKey.PageDown: return 0x22;
                case VncKey.End: return 0x23;
                case VncKey.Home: return 0x24;
                case VncKey.Left: return 0x25;
                case VncKey.Up: return 0x26;
                case VncKey.Right: return 0x27;
                case VncKey.Down: return 0x28;
                case VncKey.PrintScreen: return 0x2C;
                case VncKey.Insert: return 0x2D;
                case VncKey.Delete: return 0x2E;
                case VncKey.LWin: return 0x5B;
                case VncKey.RWin: return 0x5C;
                case VncKey.Apps: return 0x5D;
                case VncKey.NumLock: return 0x90;
                case VncKey.Scroll: return 0x91;
                case VncKey.LeftShift: return 0xA0;
                case VncKey.RightShift: return 0xA1;
                case VncKey.LeftCtrl: return 0xA2;
                case VncKey.RightCtrl: return 0xA3;
                case VncKey.LeftAlt: return 0xA4;
                case VncKey.RightAlt: return 0xA5;

                // OEM / punctuation
                case VncKey.OemSemicolon: return 0xBA;
                case VncKey.OemPlus: return 0xBB;
                case VncKey.OemComma: return 0xBC;
                case VncKey.OemMinus: return 0xBD;
                case VncKey.OemPeriod: return 0xBE;
                case VncKey.OemQuestion: return 0xBF;
                case VncKey.OemTilde: return 0xC0;
                case VncKey.OemOpenBrackets: return 0xDB;
                case VncKey.OemPipe: return 0xDC;
                case VncKey.OemCloseBrackets: return 0xDD;
                case VncKey.OemQuotes: return 0xDE;
                case VncKey.OemBackslash: return 0xE2;

                default: return 0;
            }
        }
    }
}
