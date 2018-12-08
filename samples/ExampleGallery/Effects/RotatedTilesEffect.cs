// Copyright (c) Microsoft Corporation. All rights reserved.
//
// Licensed under the MIT License. See LICENSE.txt in the project root for license information.

using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.Effects;
using System;
using System.Collections.Generic;
using System.Numerics;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Graphics.DirectX.Direct3D11;
using Windows.Media.Effects;
using Windows.Media.MediaProperties;
using Windows.UI;

namespace ExampleGallery.Effects
{
    public sealed class RotatedTilesEffect : IBasicVideoEffect
    {
        CanvasDevice canvasDevice;
        uint numColumns, numRows;
        const uint pixelsPerTile = 60;

        Transform2DEffect[,] transforms;
        CropEffect[,] crops;

        // Scales a number from SinCos range [-1, 1] to range [outputMin, outputMax].
        private float Rescale(float input, float outputMin, float outputMax)
        {
            return outputMin + (input + 1) / 2 * (outputMax - outputMin);
        }

        public IReadOnlyList<VideoEncodingProperties> SupportedEncodingProperties { get { return new List<VideoEncodingProperties>(); } }

        public bool IsReadOnly { get { return false; } }

        public MediaMemoryTypes SupportedMemoryTypes { get { return MediaMemoryTypes.Gpu; } }

        public void SetProperties(IPropertySet configuration) { }

        public bool TimeIndependent { get { return false; } }

        public void Close(MediaEffectClosedReason reason) { if (canvasDevice != null) { canvasDevice.Dispose(); } }

        public void DiscardQueuedFrames() { }

        public void ProcessFrame(ProcessVideoFrameContext context)
        {
            using (CanvasBitmap input = CanvasBitmap.CreateFromDirect3D11Surface(canvasDevice, context.InputFrame.Direct3DSurface))
            using (CanvasRenderTarget output = CanvasRenderTarget.CreateFromDirect3D11Surface(canvasDevice, context.OutputFrame.Direct3DSurface))
            using (CanvasDrawingSession ds = output.CreateDrawingSession())
            {
                TimeSpan time = context.InputFrame.RelativeTime.HasValue ? context.InputFrame.RelativeTime.Value : new TimeSpan();

                ds.Clear(Colors.Black);

                for (uint i = 0; i < numColumns; i++)
                {
                    for (uint j = 0; j < numRows; j++)
                    {
                        crops[i, j].Source = input;
                        float scale = Rescale((float)(Math.Cos(time.TotalSeconds * 2f + 0.2f * (i + j))), 0.6f, 0.95f);
                        float rotation = (float)time.TotalSeconds * 1.5f + 0.2f * (i + j);

                        Vector2 centerPoint = new Vector2((i + 0.5f) * pixelsPerTile, (j + 0.5f) * pixelsPerTile);

                        transforms[i, j].TransformMatrix =
                            Matrix3x2.CreateRotation(rotation, centerPoint) *
                            Matrix3x2.CreateScale(scale, centerPoint);

                        ds.DrawImage(transforms[i, j]);
                    }
                }
            }
        }

        public void SetEncodingProperties(VideoEncodingProperties encodingProperties, IDirect3DDevice device)
        {
            canvasDevice = CanvasDevice.CreateFromDirect3D11Device(device);
            numColumns = (uint)(encodingProperties.Width / pixelsPerTile);
            numRows = (uint)(encodingProperties.Height / pixelsPerTile);
            transforms = new Transform2DEffect[numColumns, numRows];
            crops = new CropEffect[numColumns, numRows];

            for (uint i = 0; i < numColumns; i++)
            {
                for (uint j = 0; j < numRows; j++)
                {
                    crops[i, j] = new CropEffect();
                    crops[i, j].SourceRectangle = new Rect(i * pixelsPerTile, j * pixelsPerTile, pixelsPerTile, pixelsPerTile);
                    transforms[i, j] = new Transform2DEffect();
                    transforms[i, j].Source = crops[i, j];
                }
            }
        }
    }
}
