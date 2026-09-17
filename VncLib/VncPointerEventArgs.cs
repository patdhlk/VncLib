// Copyright 2017 The VncLib Authors. All rights reserved.
// Use of this source code is governed by a BSD-style
// license that can be found in the LICENSE file.

using System;

namespace VncLib
{
    /// <summary>
    /// UI-framework neutral pointer state, passed to <see cref="VncLibUserCallback"/>.
    /// Platform controls construct this from their own mouse/pointer event before
    /// forwarding it, so the callback contract never depends on WPF or Avalonia types.
    /// </summary>
    public class VncPointerEventArgs : EventArgs
    {
        public VncPointerEventArgs()
        {
        }

        public VncPointerEventArgs(bool leftButton, bool middleButton, bool rightButton)
        {
            LeftButton = leftButton;
            MiddleButton = middleButton;
            RightButton = rightButton;
        }

        /// <summary>Left mouse button pressed.</summary>
        public bool LeftButton { get; set; }

        /// <summary>Middle mouse button pressed.</summary>
        public bool MiddleButton { get; set; }

        /// <summary>Right mouse button pressed.</summary>
        public bool RightButton { get; set; }
    }
}
