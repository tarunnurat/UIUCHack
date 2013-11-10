// -----------------------------------------------------------------------
// <copyright file="FaceTrackInfo.cs" company="Microsoft">
//     Copyright (c) Microsoft Corporation.  All rights reserved.
// </copyright>

namespace Microsoft.Samples.Kinect.KinectFusionHeadScanning
{
    using System;
    using System.Collections.Generic;
    using Microsoft.Kinect.Toolkit.FaceTracking;

    /// <summary>
    /// Struct represent tracking results for a face
    /// </summary>
    public struct FaceTrackInfo
    {
        /// <summary>
        /// Indicate the tracking data is valid or not
        /// </summary>
        public bool TrackValid { get; set; }

        /// <summary>
        /// Face rectangle in video frame coordinates
        /// </summary>
        public Rect FaceRect { get; set; }

        /// <summary>
        /// Translation in X, Y, Z axes
        /// </summary>
        public Vector3DF Translation { get; set; }

        /// <summary>
        /// Rotation around X, Y, Z axes
        /// </summary>
        public Vector3DF Rotation { get; set; }

        /// <summary>
        /// Override the equality operator
        /// </summary>
        /// <returns>Returns true if equivalent, otherwise false</returns>
        public static bool operator ==(FaceTrackInfo face1, FaceTrackInfo face2)
        {
            return face1.Equals(face2);
        }

        /// <summary>
        /// Override the inequality operator
        /// </summary>
        /// <returns>Returns true if not equivalent, otherwise false</returns>
        public static bool operator !=(FaceTrackInfo face1, FaceTrackInfo face2)
        {
            return !face1.Equals(face2);
        }

        /// <summary>
        /// Override the GetHashCode method
        /// </summary>
        /// <returns>Returns hash code.</returns>
        public override int GetHashCode()
        {
            return TrackValid.GetHashCode() ^ FaceRect.GetHashCode() ^ Translation.GetHashCode() ^ Rotation.GetHashCode();
        }

        /// <summary>
        /// Override the Equals method
        /// </summary>
        /// <returns>Returns true if not equivalent, otherwise false</returns>
        public override bool Equals(object obj)
        {
            if (!(obj is FaceTrackInfo))
            {
                return false;
            }

            return this.Equals((FaceTrackInfo)obj);
        }

        /// <summary>
        /// Equals method
        /// </summary>
        /// <returns>Returns true if not equivalent, otherwise false</returns>
        public bool Equals(FaceTrackInfo other)
        {
            if (this.TrackValid != other.TrackValid || this.FaceRect != other.FaceRect
                || this.Rotation != other.Rotation || this.Translation != other.Translation)
            {
                return false;
            }

            return true;
        }
    }
}
