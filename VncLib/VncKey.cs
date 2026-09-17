// Copyright 2017 The VncLib Authors. All rights reserved.
// Use of this source code is governed by a BSD-style
// license that can be found in the LICENSE file.

namespace VncLib
{
    /// <summary>
    /// UI-framework neutral key identifier used by the VNC protocol engine.
    /// The member names intentionally mirror the WPF and Avalonia <c>Key</c> enums so
    /// that a platform control can map its own key to a <see cref="VncKey"/> by name.
    /// </summary>
    public enum VncKey
    {
        None = 0,

        // Letters
        A, B, C, D, E, F, G, H, I, J, K, L, M,
        N, O, P, Q, R, S, T, U, V, W, X, Y, Z,

        // Top-row digits
        D0, D1, D2, D3, D4, D5, D6, D7, D8, D9,

        // Numeric keypad
        NumPad0, NumPad1, NumPad2, NumPad3, NumPad4,
        NumPad5, NumPad6, NumPad7, NumPad8, NumPad9,
        Multiply, Add, Subtract, Decimal, Divide,

        // Function keys
        F1, F2, F3, F4, F5, F6, F7, F8, F9, F10, F11, F12,

        // Modifiers
        LeftShift, RightShift, LeftCtrl, RightCtrl, LeftAlt, RightAlt,
        LWin, RWin, Apps,

        // Navigation / editing
        Space, Tab, Enter, Escape, Back,
        Home, End, Left, Up, Right, Down,
        PageUp, PageDown, Insert, Delete,

        // Locks / system
        CapsLock, NumLock, Scroll, PrintScreen, Pause,

        // OEM / punctuation
        OemPlus, OemMinus, OemComma, OemPeriod,
        OemSemicolon, OemQuestion, OemTilde,
        OemOpenBrackets, OemCloseBrackets, OemPipe,
        OemQuotes, OemBackslash
    }
}
