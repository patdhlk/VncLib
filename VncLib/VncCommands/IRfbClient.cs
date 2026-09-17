// Copyright 2017 The VncLib Authors. All rights reserved.
// Use of this source code is governed by a BSD-style
// license that can be found in the LICENSE file.

using System;

namespace VncLib.VncCommands
{
    public interface IRfbClient
    {
        void SendKey(VncKey key, bool isDown);

        void SendMouseClick(UInt16 posX, UInt16 posY, byte button);
    }
}