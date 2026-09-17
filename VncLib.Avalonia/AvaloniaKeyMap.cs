using VncLib;
using Avalonia.Input;

namespace VncLib.Avalonia;

internal static class AvaloniaKeyMap
{
    public static VncKey ToVncKey(Key key)
    {
        return key switch
        {
            // Letters
            Key.A => VncKey.A,
            Key.B => VncKey.B,
            Key.C => VncKey.C,
            Key.D => VncKey.D,
            Key.E => VncKey.E,
            Key.F => VncKey.F,
            Key.G => VncKey.G,
            Key.H => VncKey.H,
            Key.I => VncKey.I,
            Key.J => VncKey.J,
            Key.K => VncKey.K,
            Key.L => VncKey.L,
            Key.M => VncKey.M,
            Key.N => VncKey.N,
            Key.O => VncKey.O,
            Key.P => VncKey.P,
            Key.Q => VncKey.Q,
            Key.R => VncKey.R,
            Key.S => VncKey.S,
            Key.T => VncKey.T,
            Key.U => VncKey.U,
            Key.V => VncKey.V,
            Key.W => VncKey.W,
            Key.X => VncKey.X,
            Key.Y => VncKey.Y,
            Key.Z => VncKey.Z,

            // Digits
            Key.D0 => VncKey.D0,
            Key.D1 => VncKey.D1,
            Key.D2 => VncKey.D2,
            Key.D3 => VncKey.D3,
            Key.D4 => VncKey.D4,
            Key.D5 => VncKey.D5,
            Key.D6 => VncKey.D6,
            Key.D7 => VncKey.D7,
            Key.D8 => VncKey.D8,
            Key.D9 => VncKey.D9,

            // NumPad
            Key.NumPad0 => VncKey.NumPad0,
            Key.NumPad1 => VncKey.NumPad1,
            Key.NumPad2 => VncKey.NumPad2,
            Key.NumPad3 => VncKey.NumPad3,
            Key.NumPad4 => VncKey.NumPad4,
            Key.NumPad5 => VncKey.NumPad5,
            Key.NumPad6 => VncKey.NumPad6,
            Key.NumPad7 => VncKey.NumPad7,
            Key.NumPad8 => VncKey.NumPad8,
            Key.NumPad9 => VncKey.NumPad9,
            Key.Multiply => VncKey.Multiply,
            Key.Add => VncKey.Add,
            Key.Subtract => VncKey.Subtract,
            Key.Decimal => VncKey.Decimal,
            Key.Divide => VncKey.Divide,

            // Function keys
            Key.F1 => VncKey.F1,
            Key.F2 => VncKey.F2,
            Key.F3 => VncKey.F3,
            Key.F4 => VncKey.F4,
            Key.F5 => VncKey.F5,
            Key.F6 => VncKey.F6,
            Key.F7 => VncKey.F7,
            Key.F8 => VncKey.F8,
            Key.F9 => VncKey.F9,
            Key.F10 => VncKey.F10,
            Key.F11 => VncKey.F11,
            Key.F12 => VncKey.F12,

            // Modifiers
            Key.LeftShift => VncKey.LeftShift,
            Key.RightShift => VncKey.RightShift,
            Key.LeftCtrl => VncKey.LeftCtrl,
            Key.RightCtrl => VncKey.RightCtrl,
            Key.LeftAlt => VncKey.LeftAlt,
            Key.RightAlt => VncKey.RightAlt,
            Key.LWin => VncKey.LWin,
            Key.RWin => VncKey.RWin,
            Key.Apps => VncKey.Apps,

            // Common keys
            Key.Space => VncKey.Space,
            Key.Tab => VncKey.Tab,
            Key.Enter => VncKey.Enter,
            Key.Escape => VncKey.Escape,
            Key.Back => VncKey.Back,
            Key.Home => VncKey.Home,
            Key.End => VncKey.End,
            Key.Left => VncKey.Left,
            Key.Up => VncKey.Up,
            Key.Right => VncKey.Right,
            Key.Down => VncKey.Down,
            Key.PageUp => VncKey.PageUp,
            Key.PageDown => VncKey.PageDown,
            Key.Insert => VncKey.Insert,
            Key.Delete => VncKey.Delete,

            // Lock keys
            Key.CapsLock => VncKey.CapsLock,
            Key.NumLock => VncKey.NumLock,
            Key.Scroll => VncKey.Scroll,
            Key.PrintScreen => VncKey.PrintScreen,
            Key.Pause => VncKey.Pause,

            // OEM keys
            Key.OemPlus => VncKey.OemPlus,
            Key.OemMinus => VncKey.OemMinus,
            Key.OemComma => VncKey.OemComma,
            Key.OemPeriod => VncKey.OemPeriod,
            Key.OemSemicolon => VncKey.OemSemicolon,
            Key.OemQuestion => VncKey.OemQuestion,
            Key.OemTilde => VncKey.OemTilde,
            Key.OemOpenBrackets => VncKey.OemOpenBrackets,
            Key.OemCloseBrackets => VncKey.OemCloseBrackets,
            Key.OemPipe => VncKey.OemPipe,
            Key.OemQuotes => VncKey.OemQuotes,
            Key.OemBackslash => VncKey.OemBackslash,

            _ => VncKey.None
        };
    }
}
