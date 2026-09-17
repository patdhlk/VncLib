// Copyright 2017 The VncLib Authors. All rights reserved.
// Use of this source code is governed by a BSD-style
// license that can be found in the LICENSE file.

using System;
using System.ComponentModel;
using System.Windows;
using VncLib;

namespace VncLib.Client
{
    /// <summary>
    /// Interaktionslogik für MainWindow.xaml
    /// </summary>
    public partial class MainWindow
    {
        public MainWindow()
        {
            InitializeComponent();
            DataContext = new MainWindowViewModel();
        }

        private void connect_click(object sender, RoutedEventArgs e)
        {
            VncLibControl.VncLibUserCallback = VncLibUserCallback;
            VncLibControl.Connect();
            VncLibControl.UpdateScreen();
            VncLibControl.EnableMouseCapturing();
        }

        private void VncLibUserCallback(VncPointerEventArgs e, double x, double y)
        {
            Console.WriteLine($@"X: {x} Y: {y}");
        }

        private void MainWindow_OnClosing(object sender, CancelEventArgs e)
        {
            VncLibControl.Disconnect();
        }
    }
}
