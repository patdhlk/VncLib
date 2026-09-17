// Copyright 2017 The VncLib Authors. All rights reserved.
// Use of this source code is governed by a BSD-style
// license that can be found in the LICENSE file.

using System;
using System.IO;

namespace VncLib
{
    public class Helper
    {
        public static UInt32 ConvertToUInt32(byte[] data, bool isBigEndian)
        {
            if (isBigEndian == false)
                data = ReverseBytes(data);

            return BitConverter.ToUInt32(data, 0);
        }

        public static UInt16 ConvertToUInt16(byte[] data, bool isBigEndian)
        {
            if (isBigEndian == false)
                data = ReverseBytes(data);

            return BitConverter.ToUInt16(data, 0);
        }

        public static Int32 ConvertToInt32(byte[] data, bool isBigEndian)
        {
            if (isBigEndian == false)
                data = ReverseBytes(data);

            return BitConverter.ToInt32(data, 0);
        }

        public static Byte[] ConvertToByteArray(UInt16 data, bool isBigEndian)
        {
            var ret = BitConverter.GetBytes(data);

            if (isBigEndian == false)
                ret = ReverseBytes(ret);

            return ret;
        }

        public static Byte[] ConvertToByteArray(UInt32 data, bool isBigEndian)
        {
            var ret = BitConverter.GetBytes(data);

            if (isBigEndian == false)
                ret = ReverseBytes(ret);

            return ret;
        }

        public static Byte[] ConvertToByteArray(Int32 dataSigned, bool isBigEndian)
        {
            var ret = BitConverter.GetBytes(dataSigned);

            if (isBigEndian == false)
                ret = ReverseBytes(ret);

            return ret;
        }

        public static Byte[] ConvertToByteArray(bool data)
        {
            return BitConverter.GetBytes(data);
        }


        private static byte[] ReverseBytes(byte[] inArray)
        {
            var highCtr = inArray.Length - 1;

            for (var ctr = 0; ctr < inArray.Length / 2; ctr++)
            {
                var temp = inArray[ctr];
                inArray[ctr] = inArray[highCtr];
                inArray[highCtr] = temp;
                highCtr -= 1;
            }
            return inArray;
        }

        public static bool IsInDesignMode()
        {
            var process = System.Diagnostics.Process.GetCurrentProcess();
            var res = process.ProcessName == "devenv";
            process.Dispose();
            return res;
        }

    }
}