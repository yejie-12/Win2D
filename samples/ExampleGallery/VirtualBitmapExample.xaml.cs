﻿// Copyright (c) Microsoft Corporation. All rights reserved.
//
// Licensed under the MIT License. See LICENSE.txt in the project root for license information.

using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.Effects;
using Microsoft.Graphics.Canvas.Geometry;
using Microsoft.Graphics.Canvas.Text;
using Microsoft.Graphics.Canvas.UI.Xaml;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using Windows.ApplicationModel;
using Windows.Foundation;
using Windows.Storage;
using Windows.Storage.Pickers;
using Windows.Storage.Streams;
using Windows.UI;
using Windows.UI.Popups;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace ExampleGallery
{
    public sealed partial class VirtualBitmapExample : UserControl, INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        public VirtualBitmapExample()
        {
            this.InitializeComponent();

            if (!DesignMode.DesignModeEnabled)
                DataContext = this;
        }

        public string LoadedImageInfo { get; private set; }

        bool smallView;
        public bool SmallView
        {
            get
            {
                return smallView;
            }
            set
            {
                if (smallView != value)
                {
                    smallView = value;
                    Control_SizeChanged(null, null);
                }
            }
        }

        public bool IsImageLoaded { get { return virtualBitmap != null; } }

        ByteCounterStreamProxy imageStream;
        CanvasVirtualBitmap virtualBitmap;
        CanvasVirtualBitmapOptions virtualBitmapOptions;

        List<int> bytesRead = new List<int>();
        float flash = 0;
        int drawCount;

        private void Control_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (smallView)
            {
                ImageScrollViewer.MaxWidth = ActualWidth / 4;
                ImageScrollViewer.MaxHeight = ActualHeight / 4;
            }
            else
            {
                ImageScrollViewer.MaxWidth = double.MaxValue;
                ImageScrollViewer.MaxHeight = double.MaxValue;
            }
        }


        private void ImageVirtualControl_RegionsInvalidated(CanvasVirtualControl sender, CanvasRegionsInvalidatedEventArgs args)
        {
            foreach (var region in args.InvalidatedRegions)
            {
                using (var ds = ImageVirtualControl.CreateDrawingSession(region))
                {
                    if (virtualBitmap != null)
                        ds.DrawImage(virtualBitmap, region, region);
                }
            }
        }

        
        private async void OnOpenClicked(object sender, RoutedEventArgs e)
        {
            await Open(CanvasVirtualBitmapOptions.None);
        }


        private async void OnOpenCacheOnDemandClicked(object sender, RoutedEventArgs e)
        {
            await Open(CanvasVirtualBitmapOptions.CacheOnDemand);
        }


        private async Task Open(CanvasVirtualBitmapOptions options)
        {
            var filePicker = new FileOpenPicker();
            filePicker.SuggestedStartLocation = PickerLocationId.PicturesLibrary;
            filePicker.FileTypeFilter.Add("*");

            var file = await filePicker.PickSingleFileAsync();

            if (file == null)
                return;

            if (imageStream != null)
            {
                imageStream.Dispose();
                imageStream = null;
            }

            try
            {
                imageStream = new ByteCounterStreamProxy(await file.OpenReadAsync());
                virtualBitmapOptions = options;

                IOGraph.Invalidate();
                await LoadVirtualBitmap();
            }
            catch
            {
                var message = string.Format("Error opening '{0}'", file.Name);

                var messageBox = new MessageDialog(message, "Virtual Bitmap Example").ShowAsync();
            }
        }


        private void ImageVirtualControl_CreateResources(CanvasVirtualControl sender, Microsoft.Graphics.Canvas.UI.CanvasCreateResourcesEventArgs args)
        {
            if (imageStream != null)
            {
                args.TrackAsyncAction(LoadVirtualBitmap().AsAsyncAction());
            }
        }


        private async Task LoadVirtualBitmap()
        {
            if (virtualBitmap != null)
            {
                virtualBitmap.Dispose();
                virtualBitmap = null;
            }

            LoadedImageInfo = "";

            NotifyBitmapChanged();

            virtualBitmap = await CanvasVirtualBitmap.LoadAsync(ImageVirtualControl.Device, imageStream, virtualBitmapOptions);

            if (ImageVirtualControl == null)
            {
                // This can happen if the page is unloaded while LoadAsync is running
                return;
            }

            var size = virtualBitmap.Size;
            ImageVirtualControl.Width = size.Width;
            ImageVirtualControl.Height = size.Height;
            ImageVirtualControl.Invalidate();

            LoadedImageInfo = string.Format("{0}x{1} image, is {2}CachedOnDemand",
                size.Width, size.Height, virtualBitmap.IsCachedOnDemand ? "" : "not ");

            NotifyBitmapChanged();
        }


        private void NotifyBitmapChanged()
        {
            if (PropertyChanged == null)
                return;

            foreach (var property in new string[] { "LoadedImageInfo", "IsImageLoaded"})
            {
                PropertyChanged(this, new PropertyChangedEventArgs(property));
            }
        }


        private async void OnSaveAsClicked(object sender, RoutedEventArgs e)
        {
            // the button should be disabled if there's no bitmap loaded
            Debug.Assert(virtualBitmap != null);

            var picker = new FileSavePicker();
            picker.FileTypeChoices.Add("Jpegs", new List<string>() { ".jpg" });

            var file = await picker.PickSaveFileAsync();
            if (file == null)
                return;

            using (var stream = await file.OpenAsync(FileAccessMode.ReadWrite))
            {
                // Stamp a big "Win2D" over the image before we save it, to demonstrate
                // that the image really has been processed.
                var device = CanvasDevice.GetSharedDevice();

                var bounds = virtualBitmap.Bounds;

                var text = new CanvasCommandList(device);
                using (var ds = text.CreateDrawingSession())
                {
                    ds.DrawText("Win2D", bounds, Colors.White,
                        new CanvasTextFormat()
                        {
                            VerticalAlignment = CanvasVerticalAlignment.Center,
                            HorizontalAlignment = CanvasHorizontalAlignment.Center,
                            FontFamily = "Comic Sans MS",
                            FontSize = (float)(bounds.Height / 4)
                        });
                }

                var effect = new BlendEffect()
                {
                    Background = virtualBitmap,
                    Foreground = text,
                    Mode = BlendEffectMode.Difference
                };

                try
                {
                    await CanvasImage.SaveAsync(effect, bounds, 96, device, stream, CanvasBitmapFileFormat.Jpeg);
                    var message = string.Format("Finished saving '{0}'", file.Name);
                    var messageBox = new MessageDialog(message, "Virtual Bitmap Example").ShowAsync();
                }
                catch
                {
                    var message = string.Format("Error saving '{0}'", file.Name);
                    var messageBox = new MessageDialog(message, "Virtual Bitmap Example").ShowAsync();
                }
            }
        }


        private void OnIOGraphDraw(CanvasControl sender, CanvasDrawEventArgs args)
        {
            var ds = args.DrawingSession;

            var mostRecentBytesRead = imageStream == null ? 0 : imageStream.GetBytesRead();

            bytesRead.Add(mostRecentBytesRead);

            // Flash the control red when there's some IO
            if (mostRecentBytesRead != 0)
                flash = 1;
            else
                flash *= 0.95f;

            ds.Clear(new Vector4(flash, 0, 0, 1));

            ds.DrawText("Bytes read", new Rect(0, 0, sender.ActualWidth, sender.ActualHeight), Colors.DarkRed,
                new CanvasTextFormat()
                {
                    VerticalAlignment = CanvasVerticalAlignment.Center,
                    HorizontalAlignment = CanvasHorizontalAlignment.Left,
                    FontSize = 12
                });


            int maxBytesRead = bytesRead.Max();
            maxBytesRead = Math.Max(1, maxBytesRead);

            int displayCount = 120;

            // Draw a moving vertical tick to allow us to see that the graph is
            // updating even if nothing is being read.
            drawCount = (drawCount + 1) % displayCount;
            var vx = (1.0f - (float)drawCount / (float)displayCount) * (float)sender.ActualWidth;
            ds.DrawLine(vx, (float)sender.ActualHeight - 5, vx, (float)sender.ActualHeight, Colors.Gray);


            // Draw the graph
            if (bytesRead.Count > 2)
            {
                var p = new CanvasPathBuilder(sender);
                for (int i = 0; i < Math.Min(displayCount, bytesRead.Count); ++i)
                {
                    var x = ((float)i / (float)displayCount) * (float)sender.ActualWidth;
                    var y = (float)sender.ActualHeight - ((float)bytesRead[i] / (float)maxBytesRead) * (float)sender.ActualHeight;

                    if (i == 0)
                        p.BeginFigure(x, y);
                    else
                        p.AddLine(x, y);
                }
                p.EndFigure(CanvasFigureLoop.Open);

                using (var g = CanvasGeometry.CreatePath(p))
                {
                    ds.DrawGeometry(g, Colors.White, 3, new CanvasStrokeStyle()
                    {
                        LineJoin = CanvasLineJoin.Round
                    });
                }

                int toRemove = bytesRead.Count - displayCount;
                if (toRemove > 0)
                    bytesRead.RemoveRange(0, toRemove);
            }

            sender.Invalidate();
        }


        private void Control_Unloaded(object sender, RoutedEventArgs e)
        {
            IOGraph.RemoveFromVisualTree();
            ImageVirtualControl.RemoveFromVisualTree();

            IOGraph = null;
            ImageVirtualControl = null;
        }


        //
        // This passes everything on to an underlying stream, but tracks how many bytes
        // were read.
        //
        // NOTE: this is not necessary in order to use CanvasVirtualBitmap.
        // It is only used here so that the sample is able to display when IO is taking place.
        //
        class ByteCounterStreamProxy : IRandomAccessStream
        {
            IRandomAccessStream stream;
            int bytesRead = 0;

            public ByteCounterStreamProxy(IRandomAccessStream s)
            {
                stream = s;
            }

            public int GetBytesRead()
            {
                lock (stream)
                {
                    var result = bytesRead;
                    bytesRead = 0;
                    return result;
                }
            }

            public IAsyncOperationWithProgress<IBuffer, uint> ReadAsync(IBuffer buffer, uint count, InputStreamOptions options)
            {
                lock (stream)
                {
                    bytesRead += (int)count;
                }
                return stream.ReadAsync(buffer, count, options);
            }

            public bool CanRead { get { return stream.CanRead; } }
            public bool CanWrite { get { return stream.CanWrite; } }
            public ulong Position { get { return stream.Position; } }
            public ulong Size { get { return stream.Size; } set { stream.Size = value; } }
            public IInputStream GetInputStreamAt(ulong position) { return stream.GetInputStreamAt(position); }
            public IOutputStream GetOutputStreamAt(ulong position) { return stream.GetOutputStreamAt(position); }
            public void Seek(ulong position) { stream.Seek(position); }
            public IRandomAccessStream CloneStream() { return stream.CloneStream(); }
            public IAsyncOperationWithProgress<uint, uint> WriteAsync(IBuffer buffer) { return stream.WriteAsync(buffer); }
            public IAsyncOperation<bool> FlushAsync() { return stream.FlushAsync(); }
            public void Dispose() { stream.Dispose(); }
        }
    }
}
