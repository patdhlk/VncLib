﻿// Copyright 2017 The VncLib Authors. All rights reserved.
// Use of this source code is governed by a BSD-style
// license that can be found in the LICENSE file.

using System.Windows.Input;
using VncLib.VncCommands;

namespace VncLib
{
    public delegate void VncLibUserCallback(MouseEventArgs e, double x, double y);

    public delegate void VncCommandPlayerPreviewCommandExecute(object sender, IVncCommand command);

    public delegate void VncCommandPlayerCommandExecuted(object sender, IVncCommand command);
}