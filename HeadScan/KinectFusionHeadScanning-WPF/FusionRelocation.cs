// -----------------------------------------------------------------------
// <copyright file="FusionRelocation.cs" company="Microsoft">
//     Copyright (c) Microsoft Corporation.  All rights reserved.
// </copyright>

namespace Microsoft.Samples.Kinect.KinectFusionHeadScanning
{
    using System;
    using System.Collections.Generic;
    using Microsoft.Kinect;
    using Microsoft.Kinect.Toolkit.FaceTracking;

    /// <summary>
    /// Class for relocation when lost during reconstruction
    /// </summary>
    public class FusionRelocation
    {
        #region Constants

        /// <summary>
        /// The count of location frames we saved
        /// </summary>
        private const int SavedFrameCount = 50;

        /// <summary>
        /// The interval of saving a frame
        /// </summary>
        private const int SavingFrameInterval = 3;

        /// <summary>
        /// The translation tolerance metric in m when retrieving location matrix
        /// </summary>
        private const double TranslationTolerance = 0.03;

        /// <summary>
        /// The rotation tolerance metric in degree when retrieving location matrix
        /// </summary>
        private const double RotationTolerance = 7;

        #endregion

        #region Fields

        /// <summary>
        /// Save the location frame data as well as relocation database
        /// </summary>
        private LocationFrame[] locationFrames;

        /// <summary>
        /// Frames number of has updated indicating whether reach the saved interval
        /// </summary>
        private int processedFrames = 0;

        /// <summary>
        /// Index of the latest frame in the database
        /// </summary>
        private int savedFrameIndex = 0;

        #endregion

        /// <summary>
        /// Initializes a new instance of the <see cref="FusionRelocation" /> class.
        /// </summary>
        public FusionRelocation()
        {
            // We will save latest <paramref name="SavedFrameCount"/> frames as relocation database
            locationFrames = new LocationFrame[SavedFrameCount];
        }

        /// <summary>
        /// Update the relocation database
        /// </summary>
        /// <param name="faceInfo">The input face tracking information.</param>
        /// <param name="worldToCameraTransform">The corresponding transform matrix.</param>
        public void UpdateRelocation(FaceTrackInfo faceInfo, Matrix4 worldToCameraTransform)
        {
            processedFrames++;

            if (!faceInfo.TrackValid)
            {
                return;
            }

            // We save a frame in every SavingFrameInterval frames
            if (processedFrames >= SavingFrameInterval)
            {
                // Update the frame index
                savedFrameIndex = ++savedFrameIndex % SavedFrameCount;

                // Write input data to database
                locationFrames[savedFrameIndex] = new LocationFrame { FaceInfo = faceInfo, WorldToCameraTransform = worldToCameraTransform };

                processedFrames = 0;
            }
        }

        /// <summary>
        /// Retrieve the transform matrix based on the input FaceTrackInfo
        /// Note: We find the one that has minimum rotation gap compared with input, 
        /// and meanwhile lower than translation, rotation tolerance
        /// </summary>
        /// <param name="faceInfo">The input face tracking information used for searching.</param>
        /// <param name="resultMatrix">Output result transform matrix.</param>
        /// <returns>Whether successfully get the transform matrix</returns>
        public bool RetrieveTransformMatrix(FaceTrackInfo faceInfo, out Matrix4 resultMatrix)
        {
            bool retrieveSucceeded = false;
            resultMatrix = Matrix4.Identity;

            // Track the face tracking information is valid
            if (faceInfo.TrackValid)
            {
                List<int> candidateIndexes = new List<int>();

                // Get the candidates
                for (int i = 0; i < SavedFrameCount; i++)
                {
                    if (null != locationFrames[i])
                    {
                        double translationGap = GetDistance(faceInfo.Translation, locationFrames[i].FaceInfo.Translation);

                        // A valid candidate
                        if (translationGap < TranslationTolerance)
                        {
                            candidateIndexes.Add(i);
                        }
                    }
                }

                // Retrieve the best candidate and also lower than the tolerance
                double minimumRotationGap = double.MaxValue;
                int retrieveIndex = 0;
                foreach (var candidateIndex in candidateIndexes)
                {
                    double rotationGap = GetAbsoluteGap(faceInfo.Rotation, locationFrames[candidateIndex].FaceInfo.Rotation);
                    if (rotationGap < minimumRotationGap)
                    {
                        minimumRotationGap = rotationGap;

                        retrieveIndex = candidateIndex;
                    }
                }

                if (minimumRotationGap < RotationTolerance)
                {
                    retrieveSucceeded = true;
                    resultMatrix = locationFrames[retrieveIndex].WorldToCameraTransform;
                }
            }

            return retrieveSucceeded;
        }

        /// <summary>
        /// Computes the distance of two points
        /// </summary>
        /// <param name="point1">The first point.</param>
        /// <param name="point2">The second point.</param>
        /// <returns>The distance of two points</returns>
        private static double GetDistance(Vector3DF point1, Vector3DF point2)
        {
            return Math.Sqrt(((point1.X - point2.X) * (point1.X - point2.X)) 
                + ((point1.Y - point2.Y) * (point1.Y - point2.Y)) + ((point1.Z - point2.Z) * (point1.Z - point2.Z)));
        }

        /// <summary>
        /// Computes the absolute gap between two vectors just at numeric view
        /// </summary>
        /// <param name="vector1">The first vector.</param>
        /// <param name="vector2">The second vector.</param>
        /// <returns>The absolute gap</returns>
        private static double GetAbsoluteGap(Vector3DF vector1, Vector3DF vector2)
        {
            return Math.Abs(vector1.X - vector2.X) + Math.Abs(vector1.Y - vector2.Y) + Math.Abs(vector1.Z - vector2.Z);
        }

        /// <summary>
        /// Represent a pair data used for relocation
        /// </summary>
        private class LocationFrame
        {
            /// <summary>
            /// Face tracking infomation
            /// </summary>
            public FaceTrackInfo FaceInfo { get; set; }

            /// <summary>
            /// The corresponding transform matrix
            /// </summary>
            public Matrix4 WorldToCameraTransform { get; set; }
        }
    }
}
