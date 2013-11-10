// --------------------------------------------------------------------------------------------------------------------
// <copyright file="FaceTrackingViewer.xaml.cs" company="Microsoft">
//     Copyright (c) Microsoft Corporation.  All rights reserved.
// </copyright>
// --------------------------------------------------------------------------------------------------------------------

namespace Microsoft.Samples.Kinect.KinectFusionHeadScanning
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Windows;
    using System.Windows.Controls;
    using System.Windows.Media;
    using Microsoft.Kinect;
    using Microsoft.Kinect.Toolkit.FaceTracking;

    /// <summary>
    /// UserControl that uses the Face Tracking SDK to tracking a face,
    /// If has detected a face and not on reconstructing state, it will draw a face rectangle
    /// </summary>
    public partial class FaceTrackingViewer : UserControl, IDisposable
    {
        #region Fields

        /// <summary>
        /// Indicate whether it is reconstructing. If false, it will draw face rectangle in the UI.
        /// </summary>
        public static readonly DependencyProperty ReconstructingProperty = DependencyProperty.Register(
            "Reconstructing",
            typeof(bool),
            typeof(FaceTrackingViewer),
            new PropertyMetadata(false));

        /// <summary>
        /// Indicate MirrorDepth flag
        /// </summary>
        public static readonly DependencyProperty MirrorDepthProperty = DependencyProperty.Register(
            "MirrorDepth",
            typeof(bool),
            typeof(FaceTrackingViewer),
            new PropertyMetadata(false));

        /// <summary>
        /// Width of color image
        /// </summary>
        private double colorWidth = 640;

        /// <summary>
        /// Track whether Dispose has been called
        /// </summary>
        private bool disposed;

        /// <summary>
        /// Class for face tracking
        /// </summary>
        private FaceTracker faceTracker;

        /// <summary>
        /// Result of face tracking
        /// </summary>
        private FaceTrackInfo faceInfo = new FaceTrackInfo();

        /// <summary>
        /// Minimum point of face mask bounding-box
        /// </summary>
        private Vector3DF minimumPoint;

        /// <summary>
        /// Maximum point of face mask bounding box
        /// </summary>
        private Vector3DF maximumPoint;

        #endregion

        /// <summary>
        /// Initializes a new instance of the <see cref="FaceTrackingViewer" /> class.
        /// </summary>
        public FaceTrackingViewer()
        {
            this.InitializeComponent();
        }

        /// <summary>
        /// Finalizes an instance of the <see cref="FaceTrackingViewer" /> class.
        /// </summary>
        ~FaceTrackingViewer()
        {
            this.Dispose(false);
        }

        #region Properties

        /// <summary>
        /// Bind property to Reconstructing
        /// </summary>
        public bool Reconstructing
        {
            get
            {
                return (bool)GetValue(ReconstructingProperty);
            }

            set
            {
                this.SetValue(ReconstructingProperty, value);
            }
        }

        /// <summary>
        /// Bind property to MirrorDepth
        /// </summary>
        public bool MirrorDepth
        {
            get
            {
                return (bool)GetValue(MirrorDepthProperty);
            }

            set
            {
                this.SetValue(MirrorDepthProperty, value);
            }
        }

        /// <summary>
        /// Tracking result
        /// </summary>
        public FaceTrackInfo FaceInfo
        {
            get
            {
                return this.faceInfo;
            }
        }

        /// <summary>
        /// Minimum point of face mask bounding box
        /// </summary>
        public Vector3DF MinimumPoint
        {
            get
            {
                return this.minimumPoint;
            }
        }

        /// <summary>
        /// Maximum point of face mask bounding box
        /// </summary>
        public Vector3DF MaximumPoint
        {
            get
            {
                return this.maximumPoint;
            }
        }

        #endregion

        /// <summary>
        /// Dispose resources
        /// </summary>
        public void Dispose()
        {
            this.Dispose(true);

            // This object will be cleaned up by the Dispose method.
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Track a face and update the states.
        /// </summary>
        /// <param name="sensor">Instance of KinectSensor</param>
        /// <param name="colorImageFormat">Format of the colorImage array</param>
        /// <param name="colorImage">Input color image frame retrieved from Kinect sensor</param>
        /// <param name="depthImageFormat">Format of the depthImage array</param>
        /// <param name="depthImage">Input depth image frame retrieved from Kinect sensor</param>
        /// <param name="skeletonOfInterest">Input skeleton to track. Head and shoulder joints in the skeleton are used to calculate the head vector</param>
        /// <param name="computedBoundingBox">Whether compute the bounding box of the face mask</param>
        public void TrackFace(
            KinectSensor sensor,
            ColorImageFormat colorImageFormat,
            byte[] colorImage,
            DepthImageFormat depthImageFormat,
            short[] depthImage,
            Skeleton skeletonOfInterest,
            bool computedBoundingBox)
        {
            // Reset the valid flag
            this.faceInfo.TrackValid = false;

            if (null == this.faceTracker)
            {
                try
                {
                    this.faceTracker = new FaceTracker(sensor);
                }
                catch (InvalidOperationException)
                {
                    // Fail silently
                    this.faceTracker = null;

                    return;
                }
            }

            // Set the color image width
            Size colorImageSize = Helper.GetImageSize(colorImageFormat);
            this.colorWidth = colorImageSize.Width;

            // Track the face and update the states
            if (this.faceTracker != null && skeletonOfInterest != null && skeletonOfInterest.TrackingState == SkeletonTrackingState.Tracked)
            {
                FaceTrackFrame faceTrackFrame = this.faceTracker.Track(
                    colorImageFormat, colorImage, depthImageFormat, depthImage, skeletonOfInterest);

                this.faceInfo.TrackValid = faceTrackFrame.TrackSuccessful;
                if (this.faceInfo.TrackValid)
                {
                    this.faceInfo.FaceRect = faceTrackFrame.FaceRect;
                    this.faceInfo.Rotation = faceTrackFrame.Rotation;
                    this.faceInfo.Translation = faceTrackFrame.Translation;

                    // Get the bounding box of face mask
                    if (computedBoundingBox)
                    {
                        var shapePoints = faceTrackFrame.Get3DShape();
                        this.ResetBoundingBox();  // Reset the minimum and maximum points of bounding box

                        foreach (var point in shapePoints)
                        {
                            if (point.X < this.minimumPoint.X)
                            {
                                this.minimumPoint.X = point.X;
                            }

                            if (point.X > this.maximumPoint.X)
                            {
                                this.maximumPoint.X = point.X;
                            }

                            if (point.Y < this.minimumPoint.Y)
                            {
                                this.minimumPoint.Y = point.Y;
                            }

                            if (point.Y > this.maximumPoint.Y)
                            {
                                this.maximumPoint.Y = point.Y;
                            }

                            if (point.Z < this.minimumPoint.Z)
                            {
                                this.minimumPoint.Z = point.Z;
                            }

                            if (point.Z > this.maximumPoint.Z)
                            {
                                this.maximumPoint.Z = point.Z;
                            }
                        }
                    }
                }
            }

            // To render the face rectangle
            Dispatcher.BeginInvoke((Action)(() => this.InvalidateVisual()));
        }

        /// <summary>
        /// Frees all memory associated with the FaceTracker.
        /// </summary>
        /// <param name="disposing">Whether the function was called from Dispose.</param>
        protected virtual void Dispose(bool disposing)
        {
            if (!this.disposed)
            {
                if (null != this.faceTracker)
                {
                    this.faceTracker.Dispose();
                    this.faceTracker = null;
                }

                this.disposed = true;
            }
        }

        /// <summary>
        /// Override the render method
        /// </summary>
        /// <param name="drawingContext">Describes visual content using draw, push, and pop commands.</param>
        protected override void OnRender(DrawingContext drawingContext)
        {
            base.OnRender(drawingContext);

            // If it is not on reconstructing state and detecting a face, draw the rectangle
            if (!this.Reconstructing)
            {
                this.DrawFaceRectangle(drawingContext);
            }
        }

        /// <summary>
        /// Draw a rectangle containing the face on the UI
        /// </summary>
        /// <param name="drawingContext">Describes visual content using draw, push, and pop commands.</param>
        private void DrawFaceRectangle(DrawingContext drawingContext)
        {
            // We have a tracked face
            if (this.faceInfo.TrackValid)
            {
                double left = this.faceInfo.FaceRect.Left;

                // Keep corresponding based on "MirrorDepth" flag
                if (!this.MirrorDepth)
                {
                    left = this.colorWidth - this.faceInfo.FaceRect.Right;
                }

                var rect = new System.Windows.Rect(left, this.faceInfo.FaceRect.Top, this.faceInfo.FaceRect.Width, this.faceInfo.FaceRect.Height);

                drawingContext.DrawRectangle(null, new Pen(Brushes.Yellow, 2.0), rect);
            }
        }

        /// <summary>
        /// Reset the mimimum and maximum points of face mask bounding box
        /// </summary>
        private void ResetBoundingBox()
        {
            // Reset minimum point
            this.minimumPoint.X = float.MaxValue;
            this.minimumPoint.Y = float.MaxValue;
            this.minimumPoint.Z = float.MaxValue;

            // Reset maximum point
            this.maximumPoint.X = float.MinValue;
            this.maximumPoint.Y = float.MinValue;
            this.maximumPoint.Z = float.MinValue;
        }
    }
}
