// Copyright (c) Microsoft Corporation. All rights reserved.
//
// Licensed under the MIT License. See LICENSE.txt in the project root for license information.

using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.Geometry;
using Microsoft.Graphics.Canvas.Text;
using Microsoft.Graphics.Canvas.UI.Xaml;
using System;
using System.Collections.Generic;
using System.Numerics;
using Windows.UI;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace ExampleGallery
{
    public sealed partial class GeometryOperations : UserControl
    {
        CanvasGeometry leftGeometry;

        Matrix3x2 interGeometryTransform;
        CanvasGeometry rightGeometry;

        CanvasGeometry combinedGeometry;
        bool showSourceGeometry;

        float currentDistanceOnContourPath;
        float totalDistanceOnContourPath;
        Vector2 pointOnContourPath;
        Vector2 tangentOnContourPath;

        bool showTessellation;
        CanvasTriangleVertices[] tessellation;

        bool needsToRecreateResources;
        bool enableTransform;

        public GeometryOperations()
        {
            this.InitializeComponent();

            LeftGeometryType = GeometryType.Rectangle;
            RightGeometryType = GeometryType.Star;
            WhichCombineType = CanvasGeometryCombine.Union;

            interGeometryTransform = Matrix3x2.CreateTranslation(200, 100);

            CurrentContourTracingAnimation = ContourTracingAnimationOption.None;

            showSourceGeometry = false;
            showTessellation = false;
            enableTransform = false;

            needsToRecreateResources = true;
        }

        public enum GeometryType
        {
            Rectangle,
            RoundedRectangle,
            Ellipse,
            Star,
            Text,
            Group
        }

        public enum FillOrStroke
        {
            Fill,
            Stroke
        }

        public enum ContourTracingAnimationOption
        {
            None,
            Slow,
            Fast
        }

        public List<GeometryType> Geometries { get { return Utils.GetEnumAsList<GeometryType>(); } }
        public List<FillOrStroke> FillOrStrokeOptions { get { return Utils.GetEnumAsList<FillOrStroke>(); } }
        public List<CanvasGeometryCombine> CanvasGeometryCombines { get { return Utils.GetEnumAsList<CanvasGeometryCombine>(); } }
        public List<ContourTracingAnimationOption> ContourTracingAnimationOptions { get { return Utils.GetEnumAsList<ContourTracingAnimationOption>(); } }
        public GeometryType LeftGeometryType { get; set; }
        public GeometryType RightGeometryType { get; set; }
        public FillOrStroke UseFillOrStroke { get; set; }
        public ContourTracingAnimationOption CurrentContourTracingAnimation { get; set; }
        public CanvasGeometryCombine WhichCombineType { get; set; }

        private CanvasGeometry CreateGeometry(ICanvasResourceCreator resourceCreator, GeometryType type)
        {
            switch (type)
            {
                case GeometryType.Rectangle:
                    return CanvasGeometry.CreateRectangle(resourceCreator, 100, 100, 300, 350);

                case GeometryType.RoundedRectangle:
                    return CanvasGeometry.CreateRoundedRectangle(resourceCreator, 80, 80, 400, 400, 100, 100);

                case GeometryType.Ellipse:
                    return CanvasGeometry.CreateEllipse(resourceCreator, 275, 275, 225, 275);

                case GeometryType.Star:
                    return Utils.CreateStarGeometry(resourceCreator, 250, new Vector2(250, 250));

                case GeometryType.Text:
                    {
                        var textFormat = new CanvasTextFormat
                        {
                            FontFamily = "Comic Sans MS",
                            FontSize = 400,
                        };

                        var textLayout = new CanvasTextLayout(resourceCreator, "2D", textFormat, 1000, 1000);

                        return CanvasGeometry.CreateText(textLayout);
                    }

                case GeometryType.Group:
                    {
                        CanvasGeometry geo0 = CanvasGeometry.CreateRectangle(resourceCreator, 100, 100, 100, 100);
                        CanvasGeometry geo1 = CanvasGeometry.CreateRoundedRectangle(resourceCreator, 300, 100, 100, 100, 50, 50);

                        CanvasPathBuilder pathBuilder = new CanvasPathBuilder(resourceCreator);
                        pathBuilder.BeginFigure(200, 200);
                        pathBuilder.AddLine(500, 200);
                        pathBuilder.AddLine(200, 350);
                        pathBuilder.EndFigure(CanvasFigureLoop.Closed);
                        CanvasGeometry geo2 = CanvasGeometry.CreatePath(pathBuilder);

                        return CanvasGeometry.CreateGroup(resourceCreator, new CanvasGeometry[] { geo0, geo1, geo2 });
                    }
            }
            System.Diagnostics.Debug.Assert(false);
            return null;
        }

        private void Canvas_Draw(ICanvasAnimatedControl sender, CanvasAnimatedDrawEventArgs args)
        {
            Matrix3x2 displayTransform = Utils.GetDisplayTransform(sender.Size.ToVector2(), new Vector2(1000, 1000));
            args.DrawingSession.Transform = displayTransform;

            args.DrawingSession.FillGeometry(combinedGeometry, Colors.Blue);

            if (showSourceGeometry)
            {
                args.DrawingSession.DrawGeometry(leftGeometry, Colors.Red, 5.0f);

                args.DrawingSession.Transform = interGeometryTransform * displayTransform;
                args.DrawingSession.DrawGeometry(rightGeometry, Colors.Red, 5.0f);
                args.DrawingSession.Transform = displayTransform;
            }

            if (showTessellation)
            {
                foreach (var triangle in tessellation)
                {
                    args.DrawingSession.DrawLine(triangle.Vertex1, triangle.Vertex2, Colors.Gray);
                    args.DrawingSession.DrawLine(triangle.Vertex2, triangle.Vertex3, Colors.Gray);
                    args.DrawingSession.DrawLine(triangle.Vertex3, triangle.Vertex1, Colors.Gray);
                }
            }

            if (CurrentContourTracingAnimation != ContourTracingAnimationOption.None)
            {
                args.DrawingSession.FillCircle(pointOnContourPath, 2, Colors.White);

                const float arrowSize = 10.0f;

                Vector2 tangentLeft = new Vector2(
                    -tangentOnContourPath.Y,
                    tangentOnContourPath.X);

                Vector2 tangentRight = new Vector2(
                    tangentOnContourPath.Y,
                    -tangentOnContourPath.X);

                Vector2 bisectorLeft = new Vector2(
                    tangentOnContourPath.X + tangentLeft.X,
                    tangentOnContourPath.Y + tangentLeft.Y);

                Vector2 bisectorRight = new Vector2(
                    tangentOnContourPath.X + tangentRight.X,
                    tangentOnContourPath.Y + tangentRight.Y);

                Vector2 arrowheadFront = new Vector2(
                    pointOnContourPath.X + (tangentOnContourPath.X * arrowSize * 2),
                    pointOnContourPath.Y + (tangentOnContourPath.Y * arrowSize * 2));

                Vector2 arrowheadLeft = new Vector2(
                    arrowheadFront.X - (bisectorLeft.X * arrowSize),
                    arrowheadFront.Y - (bisectorLeft.Y * arrowSize));

                Vector2 arrowheadRight = new Vector2(
                    arrowheadFront.X - (bisectorRight.X * arrowSize),
                    arrowheadFront.Y - (bisectorRight.Y * arrowSize));

                const float strokeWidth = arrowSize / 4.0f;
                args.DrawingSession.DrawLine(pointOnContourPath, arrowheadFront, Colors.White, strokeWidth);
                args.DrawingSession.DrawLine(arrowheadFront, arrowheadLeft, Colors.White, strokeWidth);
                args.DrawingSession.DrawLine(arrowheadFront, arrowheadRight, Colors.White, strokeWidth);
            }
        }

        private void Canvas_Update(ICanvasAnimatedControl sender, CanvasAnimatedUpdateEventArgs args)
        {
            if (needsToRecreateResources)
            {
                RecreateGeometry(sender);
                needsToRecreateResources = false;
            }

            if (CurrentContourTracingAnimation != ContourTracingAnimationOption.None)
            {
                float animationDistanceThisFrame = CurrentContourTracingAnimation == ContourTracingAnimationOption.Slow ? 1.0f : 20.0f;
                currentDistanceOnContourPath = (currentDistanceOnContourPath + animationDistanceThisFrame) % totalDistanceOnContourPath;

                pointOnContourPath = combinedGeometry.ComputePointOnPath(currentDistanceOnContourPath, out tangentOnContourPath);
            }
        }

        private void RecreateGeometry(ICanvasResourceCreator resourceCreator)
        {
            leftGeometry = CreateGeometry(resourceCreator, LeftGeometryType);
            rightGeometry = CreateGeometry(resourceCreator, RightGeometryType);

            if (enableTransform)
            {
                Matrix3x2 placeNearOrigin = Matrix3x2.CreateTranslation(-200, -200);
                Matrix3x2 undoPlaceNearOrigin = Matrix3x2.CreateTranslation(200, 200);

                Matrix3x2 rotate0 = Matrix3x2.CreateRotation((float)Math.PI / 4.0f); // 45 degrees
                Matrix3x2 scale0 = Matrix3x2.CreateScale(1.5f);

                Matrix3x2 rotate1 = Matrix3x2.CreateRotation((float)Math.PI / 6.0f); // 30 degrees
                Matrix3x2 scale1 = Matrix3x2.CreateScale(2.0f);

                leftGeometry = leftGeometry.Transform(placeNearOrigin * rotate0 * scale0 * undoPlaceNearOrigin);
                rightGeometry = rightGeometry.Transform(placeNearOrigin * rotate1 * scale1 * undoPlaceNearOrigin);
            }

            combinedGeometry = leftGeometry.CombineWith(rightGeometry, interGeometryTransform, WhichCombineType);

            if (UseFillOrStroke == FillOrStroke.Stroke)
            {
                CanvasStrokeStyle strokeStyle = new CanvasStrokeStyle();
                strokeStyle.DashStyle = CanvasDashStyle.Dash;
                combinedGeometry = combinedGeometry.Stroke(15.0f, strokeStyle);
            }

            totalDistanceOnContourPath = combinedGeometry.ComputePathLength();

            if (showTessellation)
            {
                tessellation = combinedGeometry.Tessellate();
            }
        }

        private void Canvas_CreateResources(CanvasAnimatedControl sender, object args)
        {
            needsToRecreateResources = true;
        }

        private void SettingsCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            needsToRecreateResources = true;
        }

        void ShowSourceGeometry_Checked(object sender, RoutedEventArgs e)
        {
            showSourceGeometry = true;
        }

        void ShowSourceGeometry_Unchecked(object sender, RoutedEventArgs e)
        {
            showSourceGeometry = false;
        }

        void ShowTessellation_Checked(object sender, RoutedEventArgs e)
        {
            showTessellation = true;
            needsToRecreateResources = true;
        }

        void ShowTessellation_Unchecked(object sender, RoutedEventArgs e)
        {
            showTessellation = false;
        }

        void EnableTransform_Checked(object sender, RoutedEventArgs e)
        {
            enableTransform = true;
            needsToRecreateResources = true;
        }

        void EnableTransform_Unchecked(object sender, RoutedEventArgs e)
        {
            enableTransform = false;
            needsToRecreateResources = true;
        }

        private void control_Unloaded(object sender, RoutedEventArgs e)
        {
            // Explicitly remove references to allow the Win2D controls to get garbage collected
            canvas.RemoveFromVisualTree();
            canvas = null;
        }
    }
}
