//------------------------------------------------------------------------------
// <copyright file="MainWindow.xaml.cs" company="Microsoft">
//     Copyright (c) Microsoft Corporation.  All rights reserved.
// </copyright>
//------------------------------------------------------------------------------

namespace Microsoft.Samples.Kinect.KinectFusionHeadScanning
{
    using System;
    using System.ComponentModel;
    using System.Globalization;
    using System.IO;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using System.Windows;
    using System.Windows.Data;
    using System.Windows.Media;
    using System.Windows.Media.Imaging;
    using System.Windows.Media.Media3D;
    using System.Windows.Threading;
    using Microsoft.Kinect;
    using Microsoft.Kinect.Toolkit;
    using Microsoft.Kinect.Toolkit.FaceTracking;
    using Microsoft.Kinect.Toolkit.Fusion;
    using Wpf3DTools;

    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window, INotifyPropertyChanged, IDisposable
    {
        #region Constants

        /// <summary>
        /// Format of depth image to use
        /// </summary>
        private const DepthImageFormat DepthFormat = DepthImageFormat.Resolution640x480Fps30;

        /// <summary>
        /// Format of color image to use
        /// </summary>
        private const ColorImageFormat ColorFormat = ColorImageFormat.RgbResolution640x480Fps30;

        /// <summary>
        /// The reconstruction volume processor type. This parameter sets whether AMP or CPU processing
        /// is used. Note that CPU processing will likely be too slow for real-time processing.
        /// </summary>
        private const ReconstructionProcessor ProcessorType = ReconstructionProcessor.Amp;

        /// <summary>
        /// The zero-based device index to choose for reconstruction processing if the 
        /// ReconstructionProcessor AMP options are selected.
        /// Here we automatically choose a device to use for processing by passing -1, 
        /// </summary>
        private const int DeviceToUse = -1;

        /// <summary>
        /// If set true, will automatically reset the reconstruction when the timestamp changes by
        /// ResetOnTimeStampSkippedMillisecondsGPU or ResetOnTimeStampSkippedMillisecondsCPU for the 
        /// different processor types respectively. This is useful for automatically resetting when
        /// scrubbing through a .xed file or on loop of a .xed file during playback. Note that setting
        /// this true may cause constant resets on slow machines that cannot process frames in less
        /// time that the reset threshold. If this occurs, set to false or increase the timeout.
        /// </summary>
        private const bool AutoResetReconstructionOnTimeSkip = true;

        /// <summary>
        /// Time threshold to reset the reconstruction if tracking can't be restored within it.
        /// This value is valid if GPU is used
        /// </summary>
        private const int ResetOnTimeStampSkippedMillisecondsGPU = 1000;

        /// <summary>
        /// Time threshold to reset the reconstruction if tracking can't be restored within it.
        /// This value is valid if CPU is used
        /// </summary>
        private const int ResetOnTimeStampSkippedMillisecondsCPU = 6000;

        /// <summary>
        /// Event interval for FPS timer
        /// </summary>
        private const int FpsInterval = 5;

        /// <summary>
        /// Force a point cloud calculation and render at least every 100ms
        /// </summary>
        private const int RenderIntervalMilliseconds = 100;

        /// <summary>
        /// The frame interval where we execute face tracking
        /// </summary>
        private const int FaceTrackingInterval = 2;

        /// <summary>
        /// The frame interval where we integrate color.
        /// Capturing color has an associated processing cost, so we do not capture every frame here.
        /// </summary>
        private const int ColorIntegrationInterval = 2;

        /// <summary>
        /// Frame interval we calculate the deltaFromReferenceFrame 
        /// </summary>
        private const int DeltaFrameCalculationInterval = 3;

        /// <summary>
        /// Volume Cube and WPF3D Origin coordinate cross axis 3D graphics line thickness in screen pixels
        /// </summary>
        private const int LineThickness = 2;

        /// <summary>
        /// WPF3D Origin coordinate cross 3D graphics axis size in m
        /// </summary>
        private const float OriginCoordinateCrossAxisSize = 0.1f;

        /// <summary>
        /// Extended size of head at up direction
        /// We take the face mask bounding box as the initial position of head,
        /// and extend the size align +Y axis to make sure containing head
        /// </summary>
        private const float UpExtendedHeadSize = 0.1f;

        /// <summary>
        /// Extended size in meter of head align X, Z axes
        /// </summary>
        private const float ExtendedHeadSizeAlignXZ = 0.15f;

        /// <summary>
        /// Here we set a low limit on the residual alignment energy, below which we reject a tracking
        /// success report and believe it to have failed. Typically this value would be around 0.001f, as
        /// values below this (i.e. close to 0 which is perfect alignment) most likely come from frames
        /// where the majority of the image is obscured (i.e. 0 depth) or mis-matched (i.e. similar depths
        /// but different scene or camera pose).
        /// </summary>
        private const float MinAlignmentEnergyForSuccess = 0.001f;

        #endregion

        #region Fields

        /// <summary>
        /// Volume Cube 3D graphics line color
        /// </summary>
        private static System.Windows.Media.Color volumeCubeLineColor = System.Windows.Media.Color.FromArgb(200, 0, 200, 0);   // Green, partly transparent

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

        /// <summary>
        /// Track whether Dispose has been called
        /// </summary>
        private bool disposed;

        /// <summary>
        /// Whether has lost head tracking
        /// </summary>
        private bool hasLostTracking = false;

        /// <summary>
        /// Saving mesh flag
        /// </summary>
        private bool savingMesh;

        /// <summary>
        /// To display shaded surface normals frame instead of shaded surface frame
        /// </summary>
        private bool displayNormals = false;

        /// <summary>
        /// Capture, integrate and display color when true
        /// </summary>
        private bool captureColor;

        /// <summary>
        /// Pause or resume image integration
        /// </summary>
        private bool pauseIntegration;

        /// <summary>
        /// If near mode is enabled
        /// </summary>
        private bool nearMode;

        /// <summary>
        /// Whether render the face model at kinect camera view
        /// </summary>
        private bool kinectView = true;

        /// <summary>
        /// Depth image is mirrored
        /// </summary>
        private bool mirrorDepth;

        /// <summary>
        /// Image Width of depth frame
        /// </summary>
        private int depthWidth = 0;

        /// <summary>
        /// Image height of depth frame
        /// </summary>
        private int depthHeight = 0;

        /// <summary>
        /// Image width of color frame
        /// </summary>
        private int colorWidth = 0;

        /// <summary>
        /// Image height of color frame
        /// </summary>
        private int colorHeight = 0;

        /// <summary>
        /// The counter for color frames that have been processed
        /// </summary>
        private int processedColorFrameCount = 0;

        /// <summary>
        /// The counter for frames that have been processed
        /// </summary>
        private int processedFrameCount = 0;

        /// <summary>
        /// Timestamp of last depth frame in milliseconds
        /// </summary>
        private long lastFrameTimestamp = 0;

        /// <summary>
        /// Timer to count FPS
        /// </summary>
        private DispatcherTimer fpsTimer;

        /// <summary>
        /// Timer stamp of last computation of FPS
        /// </summary>
        private DateTime lastFPSTimestamp = DateTime.UtcNow;

        /// <summary>
        /// Timer stamp of last raycast and render
        /// </summary>
        private DateTime lastRenderTimestamp = DateTime.UtcNow;

        /// <summary>
        /// Active Kinect sensor
        /// </summary>
        private KinectSensor sensor = null;

        /// <summary>
        /// Kinect sensor chooser object
        /// </summary>
        private KinectSensorChooser sensorChooser;

        /// <summary>
        /// Intermediate storage for the extended depth data received from the camera in the current frame
        /// </summary>
        private DepthImagePixel[] depthImagePixels;

        /// <summary>
        /// Intermediate storage for the raw depth data received from the camera in the current frame
        /// </summary>
        private short[] depthImageData;

        /// <summary>
        /// Intermediate storage for the color data received from the camera in 32bit color
        /// </summary>
        private byte[] colorImageRawPixels;

        /// <summary>
        /// Intermediate storage for the color data received from the camera in 32bit color
        /// We may map this buffer for keep corresponding with depth data
        /// </summary>
        private byte[] colorImagePixels;

        /// <summary>
        /// Bitmap that will hold color information
        /// </summary>
        private WriteableBitmap colorFrameBitmap;

        /// <summary>
        /// Black value bitmap used for default value of shadeSurfaceImage
        /// </summary>
        private WriteableBitmap defaultBitmap;

        /// <summary>
        /// Width of defaultBitmap
        /// </summary>
        private int widthOfDefaultBitmap = 4;

        /// <summary>
        /// Height of defaultBitmap
        /// </summary>
        private int heightOfDefaultBitmap = 3;

        /// <summary>
        /// The Kinect Fusion volume, enabling color reconstruction
        /// </summary>
        private ColorReconstruction volume;

        /// <summary>
        /// The first rendering frame flag
        /// </summary>
        private bool firstFrame = true;

        /// <summary>
        /// Intermediate storage for the depth float data converted from depth image frame
        /// </summary>
        private FusionFloatImageFrame depthFloatFrame;

        /// <summary>
        /// Kinect color mapped into depth frame
        /// </summary>
        private FusionColorImageFrame mappedColorFrame;

        /// <summary>
        /// Per-pixel alignment values
        /// </summary>
        private FusionFloatImageFrame deltaFromReferenceFrame;

        /// <summary>
        /// Shaded surface frame from shading point cloud frame
        /// </summary>
        private FusionColorImageFrame shadedSurfaceFrame;

        /// <summary>
        /// Shaded surface normals frame from shading point cloud frame
        /// </summary>
        private FusionColorImageFrame shadedSurfaceNormalsFrame;

        /// <summary>
        /// Calculated point cloud frame from image integration
        /// </summary>
        private FusionPointCloudImageFrame pointCloudFrame;

        /// <summary>
        /// Bitmap contains depth float frame data for rendering
        /// </summary>
        private WriteableBitmap depthFloatFrameBitmap;

        //// <summary>
        //// Bitmap contains delta from reference frame data for rendering
        //// </summary>
        private WriteableBitmap deltaFromReferenceFrameBitmap;

        /// <summary>
        /// Bitmap contains shaded surface frame data for rendering
        /// </summary>
        private WriteableBitmap shadedSurfaceFrameBitmap;

        /// <summary>
        /// Pixel buffer of depth float frame with pixel data in float format
        /// </summary>
        private float[] depthFloatFrameDepthPixels;

        /// <summary>
        /// Pixel buffer of delta from reference frame with pixel data in float format
        /// </summary>
        private float[] deltaFromReferenceFrameFloatPixels;

        /// <summary>
        /// Pixel buffer of depth float frame with pixel data in 32bit color
        /// </summary>
        private int[] depthFloatFramePixelsArgb;

        //// <summary>
        //// Pixel buffer of delta from reference frame in 32bit color
        //// </summary>
        private int[] deltaFromReferenceFramePixelsArgb;

        /// <summary>
        /// Pixels buffer of shaded surface frame in 32bit color
        /// </summary>
        private int[] shadedSurfaceFramePixelsArgb;

        /// <summary>
        /// Mapping of depth pixels into color image
        /// </summary>
        private ColorImagePoint[] colorCoordinates;

        /// <summary>
        /// Mapped color pixels in depth frame of reference
        /// </summary>
        private int[] mappedColorPixels;

        /// <summary>
        /// The coordinate mapper to convert between depth and color frames of reference
        /// </summary>
        private CoordinateMapper mapper;

        /// <summary>
        /// Alignment energy from AlignDepthFloatToReconstruction for current frame 
        /// </summary>
        private float alignmentEnergy;

        /// <summary>
        /// The worker thread to process the depth and color data
        /// </summary>
        private Thread workerThread = null;

        /// <summary>
        /// Event to stop worker thread
        /// </summary>
        private ManualResetEvent workerThreadStopEvent;

        /// <summary>
        /// The worker thread to track face
        /// </summary>
        private Thread faceTrackingThread = null;

        /// <summary>
        /// Event to stop face tracking thread
        /// </summary>
        private ManualResetEvent faceTrackingThreadStopEvent;

        /// <summary>
        /// Event to notify that all data is ready for process
        /// </summary>
        private ManualResetEvent allDataReadyEvent;

        /// <summary>
        /// Event to notify that depth data is ready for process
        /// </summary>
        private ManualResetEvent depthReadyEvent;

        /// <summary>
        /// Event to notify that color data is ready for process
        /// </summary>
        private ManualResetEvent colorReadyEvent;

        /// <summary>
        /// Event to notify that face tracking data is ready for process
        /// </summary>
        private ManualResetEvent faceTrackingResultReadyEvent;

        /// <summary>
        /// Event to notify that start reconstructing
        /// </summary>
        private ManualResetEvent reconstructingEvent;

        /// <summary>
        /// Lock object for raw data from sensor
        /// </summary>
        private object inputLock = new object();

        /// <summary>
        /// Lock object for volume re-creation and meshing
        /// </summary>
        private object volumeLock = new object();

        /// <summary>
        /// Lock object for face tracking result
        /// </summary>
        private object faceTrackingLock = new object();

        /// <summary>
        /// Flag to signal to worker thread to reset the reconstruction
        /// </summary>
        private bool resetReconstruction = false;

        /// <summary>
        /// Flag to signal to worker thread to re-create the reconstruction
        /// </summary>
        private bool recreateReconstruction = false;

        /// <summary>
        /// The transformation between the world and camera view coordinate system
        /// </summary>
        private Matrix4 worldToCameraTransform;

        /// <summary>
        /// The default transformation between the world and volume coordinate system
        /// </summary>
        private Matrix4 defaultWorldToVolumeTransform;

        /// <summary>
        /// Minimum depth distance threshold in meters. Depth pixels below this value will be
        /// returned as invalid (0). Min depth must be positive or 0.
        /// </summary>
        private float minDepthClip = FusionDepthProcessor.DefaultMinimumDepth;

        /// <summary>
        /// Maximum depth distance threshold in meters. Depth pixels above this value will be
        /// returned as invalid (0). Max depth must be greater than 0.
        /// </summary>
        private float maxDepthClip = FusionDepthProcessor.DefaultMaximumDepth;

        /// <summary>
        /// Image integration weight
        /// </summary>
        private short integrationWeight = FusionDepthProcessor.DefaultIntegrationWeight;

        /// <summary>
        /// Flag set true if at some point color has been captured. 
        /// Used when writing .Ply mesh files to output vertex color.
        /// </summary>
        private bool colorCaptured;

        /// <summary>
        /// Whether it is reconstructing head model
        /// </summary>
        private bool reconstructing = false;

        /// <summary>
        /// Indicate whether the 3D view port has added the volume cube
        /// </summary>
        private bool haveAddedVolumeCube = false;

        /// <summary>
        /// The volume cube 3D graphical representation
        /// </summary>
        private ScreenSpaceLines3D volumeCube;

        /// <summary>
        /// The volume cube 3D graphical representation
        /// </summary>
        private ScreenSpaceLines3D volumeCubeAxisX;

        /// <summary>
        /// The volume cube 3D graphical representation
        /// </summary>
        private ScreenSpaceLines3D volumeCubeAxisY;

        /// <summary>
        /// The volume cube 3D graphical representation
        /// </summary>
        private ScreenSpaceLines3D volumeCubeAxisZ;

        /// <summary>
        /// Flag boolean set true to force the reconstruction visualization to be updated after graphics camera movements
        /// </summary>
        private bool viewChanged = true;

        /// <summary>
        /// The virtual 3rd person camera view that can be controlled by the mouse
        /// </summary>
        private GraphicsCamera virtualCamera;

        /// <summary>
        /// The virtual 3rd person camera view that can be controlled by the mouse - start rotation
        /// </summary>
        private Quaternion virtualCameraStartRotation = Quaternion.Identity;

        /// <summary>
        /// The virtual 3rd person camera view that can be controlled by the mouse - start translation
        /// </summary>
        private Point3D virtualCameraStartTranslation = new Point3D();  // 0,0,0

        /// <summary>
        /// The reconstruction volume voxel density in voxels per meter (vpm)
        /// </summary>
        private float voxelsPerMeter = 640.0f;

        /// <summary>
        /// The reconstruction volume voxel resolution in the X axis
        /// Note : it is an initial value here, and we will change the value to fit the head
        /// </summary>
        private int voxelsX = 256;

        /// <summary>
        /// The reconstruction volume voxel resolution in the Y axis
        /// Note : it is an initial value here, and we will change the value to fit the head
        /// </summary>
        private int voxelsY = 128;

        /// <summary>
        /// The reconstruction volume voxel resolution in the Z axis
        /// Note : it is an initial value here, and we will change the value to fit the head
        /// </summary>
        private int voxelsZ = 256;

        /// <summary>
        /// The skeletons
        /// </summary>
        private Skeleton[] skeletonData;

        /// <summary>
        /// The selected skeleton
        /// </summary>
        private Skeleton selectedSkeleton;

        /// <summary>
        /// Skeleton Id of the tracking user
        /// </summary>
        private int trackingId = 0;

        /// <summary>
        /// Parameter to translate the reconstruction based on the minimum depth setting. When set to
        /// false, the reconstruction volume +Z axis starts at the camera lens and extends into the scene.
        /// Setting this true in the constructor will move the volume forward along +Z away from the
        /// camera by the minimum depth threshold to enable capture of very small reconstruction volumes
        /// by setting a non-identity world-volume transformation in the ResetReconstruction call.
        /// Small volumes should be shifted, as the Kinect hardware has a minimum sensing limit of ~0.35m,
        /// inside which no valid depth is returned, hence it is difficult to initialize and track robustly  
        /// when the majority of a small volume is inside this distance.
        /// </summary>
        private bool translateResetPoseByMinDepthThreshold = false;  // We calculate a different translation for head capture

        /// <summary>
        /// Head offset, for axis aligned volume translation on creation/reset
        /// </summary>
        private bool translateResetPoseByHeadOffset = true;

        /// <summary>
        /// Used for relocation when lost during reconstructing
        /// </summary>
        private FusionRelocation relocation = new FusionRelocation();

        /// <summary>
        /// The virtual camera pose.
        /// </summary>
        private Matrix4 virtualCameraWorldToCameraMatrix4 = new Matrix4();

        #endregion

        /// <summary>
        /// Initializes a new instance of the MainWindow class.
        /// </summary>
        public MainWindow()
        {
            this.InitializeComponent();

            var reconstructingBinding = new Binding("Reconstructing") { Source = this };
            faceTrackingViewer.SetBinding(FaceTrackingViewer.ReconstructingProperty, reconstructingBinding);

            var mirrorDepthBinding = new Binding("MirrorDepth") { Source = this };
            faceTrackingViewer.SetBinding(FaceTrackingViewer.MirrorDepthProperty, mirrorDepthBinding);
        }

        /// <summary>
        /// Finalizes an instance of the MainWindow class.
        /// This destructor will run only if the Dispose method does not get called.
        /// </summary>
        ~MainWindow()
        {
            this.Dispose(false);
        }

        #region Properties

        /// <summary>
        /// Property change event
        /// </summary>
        public event PropertyChangedEventHandler PropertyChanged;

        /// <summary>
        /// The Mesh type.
        /// </summary>
        private enum MeshType
        {
            /// <summary>
            /// Stereo-lithography .STL file
            /// </summary>
            Stl = 1,

            /// <summary>
            /// ASCII Wavefront .OBJ file
            /// </summary>
            Obj = 2,

            /// <summary>
            /// Polygon mesh .PLY file with per-vertex color
            /// </summary>
            Ply = 3,
        }

        /// <summary>
        /// Gets or sets the timestamp of the current depth frame
        /// </summary>
        public long FrameTimestamp { get; set; }

        /// <summary>
        /// Binding property to Reconstructing
        /// </summary>
        public bool Reconstructing
        {
            get
            {
                return this.reconstructing;
            }

            set
            {
                this.reconstructing = value;

                if (null != this.PropertyChanged)
                {
                    this.PropertyChanged.Invoke(this, new PropertyChangedEventArgs("Reconstructing"));
                }
            }
        }

        /// <summary>
        /// Binding property to integration weight slider
        /// </summary>
        public double IntegrationWeight
        {
            get
            {
                return (double)this.integrationWeight;
            }

            set
            {
                this.integrationWeight = (short)(value + 0.5);
                if (null != this.PropertyChanged)
                {
                    this.PropertyChanged.Invoke(this, new PropertyChangedEventArgs("IntegrationWeight"));
                }
            }
        }

        /// <summary>
        /// Binding property to voxels per meter slider
        /// </summary>
        public double VoxelsPerMeter
        {
            get
            {
                return (double)this.voxelsPerMeter;
            }

            set
            {
                this.voxelsPerMeter = (float)value;
                if (null != this.PropertyChanged)
                {
                    this.PropertyChanged.Invoke(this, new PropertyChangedEventArgs("VoxelsPerMeter"));
                }
            }
        }

        /// <summary>
        /// Binding property to X-axis volume resolution slider
        /// </summary>
        public double VoxelsX
        {
            get
            {
                return (double)this.voxelsX;
            }

            set
            {
                this.voxelsX = (int)(value + 0.5);
                if (null != this.PropertyChanged)
                {
                    this.PropertyChanged.Invoke(this, new PropertyChangedEventArgs("VoxelsX"));
                }
            }
        }

        /// <summary>
        /// Binding property to Y-axis volume resolution slider
        /// </summary>
        public double VoxelsY
        {
            get
            {
                return (double)this.voxelsY;
            }

            set
            {
                this.voxelsY = (int)(value + 0.5);
                if (null != this.PropertyChanged)
                {
                    this.PropertyChanged.Invoke(this, new PropertyChangedEventArgs("VoxelsY"));
                }
            }
        }

        /// <summary>
        /// Binding property to Z-axis volume resolution slider
        /// </summary>
        public double VoxelsZ
        {
            get
            {
                return (double)this.voxelsZ;
            }

            set
            {
                this.voxelsZ = (int)(value + 0.5);
                if (null != this.PropertyChanged)
                {
                    this.PropertyChanged.Invoke(this, new PropertyChangedEventArgs("VoxelsZ"));
                }
            }
        }

        /// <summary>
        /// Binding property to check box "Capture Color"
        /// </summary>
        public bool CaptureColor
        {
            get
            {
                return this.captureColor;
            }

            set
            {
                this.captureColor = value;

                if (null != this.PropertyChanged)
                {
                    this.PropertyChanged.Invoke(this, new PropertyChangedEventArgs("CaptureColor"));
                }
            }
        }

        /// <summary>
        /// Binding property to check box "Near Mode"
        /// </summary>
        public bool NearMode
        {
            get
            {
                return this.nearMode;
            }

            set
            {
                if (this.nearMode != value)
                {
                    this.nearMode = value;
                    this.OnNearModeChanged();

                    if (null != this.PropertyChanged)
                    {
                        this.PropertyChanged.Invoke(this, new PropertyChangedEventArgs("NearMode"));
                    }
                }
            }
        }

        /// <summary>
        /// Binding property to check box "Pause Integration"
        /// </summary>
        public bool PauseIntegration
        {
            get
            {
                return this.pauseIntegration;
            }

            set
            {
                this.pauseIntegration = value;

                if (null != this.PropertyChanged)
                {
                    this.PropertyChanged.Invoke(this, new PropertyChangedEventArgs("PauseIntegration"));
                }
            }
        }

        /// <summary>
        /// Binding property to check box "Kinect View"
        /// </summary>
        public bool KinectView
        {
            get
            {
                return this.kinectView;
            }

            set
            {
                this.kinectView = value;

                if (null != this.PropertyChanged)
                {
                    this.PropertyChanged.Invoke(this, new PropertyChangedEventArgs("KinectView"));
                }

                // Decide whether render the volume cube
                if (this.kinectView)
                {
                    this.virtualCamera.CameraTransformationChanged -= this.OnVirtualCameraTransformationChanged;
                    this.virtualCamera.Detach(this.shadedSurfaceImage);
                    this.virtualCamera.RemoveFrustum3DGraphics();
                    this.RemoveVolumeCube3DGraphics();
                }
                else
                {
                    this.virtualCamera.Attach(this.shadedSurfaceImage);
                    this.virtualCamera.CameraTransformationChanged += this.OnVirtualCameraTransformationChanged;
                    this.virtualCamera.AddFrustum3DGraphics();
                    this.AddVolumeCube3DGraphics();

                    // Reset the virtual camera
                    this.virtualCamera.Reset();
                }

                this.viewChanged = true;

                this.GraphicsViewport.InvalidateVisual();
            }
        }

        /// <summary>
        /// Property to mirrorDepth flag
        /// </summary>
        public bool MirrorDepth
        {
            get
            {
                return this.mirrorDepth;
            }

            set
            {
                this.mirrorDepth = value;
                if (null != this.PropertyChanged)
                {
                    this.PropertyChanged.Invoke(this, new PropertyChangedEventArgs("MirrorDepth"));
                }
            }
        }

        /// <summary>
        /// Property to calculate whether rendering is overdue 
        /// (i.e. time interval since last render > RenderIntervalMilliseconds)
        /// </summary>
        public bool IsRenderOverdue
        {
            get
            {
                return (DateTime.UtcNow - this.lastRenderTimestamp).TotalMilliseconds >= RenderIntervalMilliseconds;
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
        /// Frees all memory associated with the ReconstructionVolume and FusionImageFrames.
        /// </summary>
        /// <param name="disposing">Whether the function was called from Dispose.</param>
        protected virtual void Dispose(bool disposing)
        {
            if (!this.disposed)
            {
                if (disposing)
                {
                    if (null != this.faceTrackingViewer)
                    {
                        this.faceTrackingViewer.Dispose();
                    }

                    this.SafeDisposeFusionResources();

                    if (null != this.volume)
                    {
                        this.volume.Dispose();
                    }

                    if (null != this.allDataReadyEvent)
                    {
                        this.allDataReadyEvent.Dispose();
                    }

                    if (null != this.depthReadyEvent)
                    {
                        this.depthReadyEvent.Dispose();
                    }

                    if (null != this.colorReadyEvent)
                    {
                        this.colorReadyEvent.Dispose();
                    }

                    if (null != this.faceTrackingResultReadyEvent)
                    {
                        this.faceTrackingResultReadyEvent.Dispose();
                    }

                    if (null != this.workerThreadStopEvent)
                    {
                        this.workerThreadStopEvent.Dispose();
                    }

                    if (null != this.faceTrackingThreadStopEvent)
                    {
                        this.faceTrackingThreadStopEvent.Dispose();
                    }

                    if (null != this.reconstructingEvent)
                    {
                        this.reconstructingEvent.Dispose();
                    }

                    this.RemoveVolumeCube3DGraphics();
                    this.DisposeVolumeCube3DGraphics();

                    if (null != this.virtualCamera)
                    {
                        this.virtualCamera.CameraTransformationChanged -= this.OnVirtualCameraTransformationChanged;
                        this.virtualCamera.Detach(this.shadedSurfaceImage);     // Stop getting mouse events from the image
                        this.virtualCamera.Dispose();
                    }
                }
            }

            this.disposed = true;
        }

        /// <summary>
        /// Render Fusion color frame to UI
        /// </summary>
        /// <param name="colorFrame">Fusion color frame</param>
        /// <param name="colorPixels">Pixel buffer for fusion color frame</param>
        /// <param name="bitmap">Bitmap contains color frame data for rendering</param>
        /// <param name="image">UI image component to render the color frame</param>
        private static void RenderColorImage(FusionColorImageFrame colorFrame, ref int[] colorPixels, ref WriteableBitmap bitmap, System.Windows.Controls.Image image)
        {
            if (null == image || null == colorFrame)
            {
                return;
            }

            if (null == colorPixels || colorFrame.PixelDataLength != colorPixels.Length)
            {
                // Create pixel array of correct format
                colorPixels = new int[colorFrame.PixelDataLength];
            }

            if (null == bitmap || colorFrame.Width != bitmap.Width || colorFrame.Height != bitmap.Height)
            {
                // Create bitmap of correct format
                bitmap = new WriteableBitmap(colorFrame.Width, colorFrame.Height, 96.0, 96.0, PixelFormats.Bgr32, null);
            }

            // Set bitmap as source to UI image object
            image.Source = bitmap;

            // Copy pixel data to pixel buffer
            colorFrame.CopyPixelDataTo(colorPixels);

            // Write pixels to bitmap
            bitmap.WritePixels(
                        new Int32Rect(0, 0, colorFrame.Width, colorFrame.Height),
                        colorPixels,
                        bitmap.PixelWidth * sizeof(int),
                        0);
        }

        /// <summary>
        /// Render Fusion color frame to UI direct from byte buffer
        /// </summary>
        /// <param name="colorPixels">Pixel buffer for fusion color frame.</param>
        /// <param name="width">The width of the image.</param>
        /// <param name="height">The height of the image.</param>
        /// <param name="bitmap">Bitmap contains color frame data for rendering.</param>
        /// <param name="image">UI image component to render the color frame.</param>
        private static void RenderColorImage(byte[] colorPixels, int width, int height, ref WriteableBitmap bitmap, System.Windows.Controls.Image image)
        {
            if (null == colorPixels)
            {
                return;
            }

            if (null == bitmap || width != bitmap.Width || height != bitmap.Height)
            {
                // Create bitmap of correct format
                bitmap = new WriteableBitmap(width, height, 96.0, 96.0, PixelFormats.Bgr32, null);
            }

            // Set bitmap as source to UI image object
            image.Source = bitmap;

            // Write pixels to bitmap
            bitmap.WritePixels(
                        new Int32Rect(0, 0, width, height),
                        colorPixels,
                        bitmap.PixelWidth * sizeof(byte) * 4,   // rgba
                        0);
        }

        /// <summary>
        /// Mirror the color image in-place for display to match the depth image
        /// </summary>
        /// <param name="sourceColorPixels">The sensor color pixel byte data.</param>
        /// <param name="colorWidth">The width of the color pixel data image.</param>
        /// <param name="colorHeight">The height of the color pixel data image.</param>
        private static unsafe void MirrorColorHorizontalInPlace(byte[] sourceColorPixels, int colorWidth, int colorHeight)
        {
            if (null == sourceColorPixels)
            {
                return;
            }

            // Here we make use of unsafe code to just copy the whole pixel as an int for performance reasons, as we do
            // not need access to the individual rgba components.
            fixed (byte* ptrColorPixels = sourceColorPixels)
            {
                int* rawColorPixels = (int*)ptrColorPixels;

                Parallel.For(
                    0,
                    colorHeight,
                    y =>
                    {
                        int index = y * colorWidth;
                        int mirrorIndex = index + colorWidth - 1;

                        for (int x = 0; x < (colorWidth / 2); ++x, ++index, --mirrorIndex)
                        {
                            // In-place swap to mirror
                            int temp = rawColorPixels[index];
                            rawColorPixels[index] = rawColorPixels[mirrorIndex];
                            rawColorPixels[mirrorIndex] = temp;
                        }
                    });
            }
        }

        /// <summary>
        /// Execute startup tasks
        /// </summary>
        /// <param name="sender">object sending the event</param>
        /// <param name="e">event arguments</param>
        private void WindowLoaded(object sender, RoutedEventArgs e)
        {
            // Check to ensure suitable DirectX11 compatible hardware exists
            try
            {
                string deviceDescription = string.Empty;
                string deviceInstancePath = string.Empty;
                int deviceMemory = 0;

                FusionDepthProcessor.GetDeviceInfo(ProcessorType, DeviceToUse, out deviceDescription, out deviceInstancePath, out deviceMemory);
            }
            catch (IndexOutOfRangeException)
            {
                // Thrown when index is out of range for processor type or there is no DirectX11 capable device installed.
                // As we set -1 (auto-select default) for the DeviceToUse above, this indicates that there is no DirectX11 
                // capable device. The options for users in this case are to either install a DirectX11 capable device 
                // (see documentation for recommended GPUs) or to switch to non-real-time CPU based reconstruction by 
                // changing ProcessorType to ReconstructionProcessor.Cpu
                this.statusBarText.Text = Properties.Resources.NoDirectX11CompatibleDeviceOrInvalidDeviceIndex;
                return;
            }
            catch (DllNotFoundException)
            {
                this.statusBarText.Text = Properties.Resources.MissingPrerequisite;
                return;
            }
            catch (InvalidOperationException ex)
            {
                this.statusBarText.Text = ex.Message;
                return;
            }

            // Allocate temporary storage for image size
            Size depthImageSize = Helper.GetImageSize(DepthFormat);
            this.depthWidth = (int)depthImageSize.Width;
            this.depthHeight = (int)depthImageSize.Height;

            Size colorImageSize = Helper.GetImageSize(ColorFormat);
            this.colorWidth = (int)colorImageSize.Width;
            this.colorHeight = (int)colorImageSize.Height;

            // Setup the graphics rendering

            // Create virtualCamera for non-Kinect viewpoint rendering
            this.virtualCameraStartTranslation = new Point3D(0, 0, this.voxelsZ / this.voxelsPerMeter); // default position translated along Z axis, looking back at origin
            this.virtualCamera = new GraphicsCamera(this.virtualCameraStartTranslation, this.virtualCameraStartRotation, (float)Width / (float)Height);

            // Attach this camera to the viewport
            this.GraphicsViewport.Camera = this.virtualCamera.Camera;

            // Add the camera frustum graphics
            this.virtualCamera.CreateFrustum3DGraphics(this.GraphicsViewport, 0.001f, 0.25f, this.depthWidth, this.depthHeight, System.Windows.Media.Color.FromArgb(180, 240, 240, 0), 2);

            // Setup default bitmap
            byte[] defaultBitmapPixels = new byte[this.widthOfDefaultBitmap * this.heightOfDefaultBitmap * 4];
            this.defaultBitmap = new WriteableBitmap(this.widthOfDefaultBitmap, this.heightOfDefaultBitmap, 96.0, 96.0, PixelFormats.Bgr32, null);
            this.defaultBitmap.WritePixels(new Int32Rect(0, 0, this.widthOfDefaultBitmap, this.heightOfDefaultBitmap), defaultBitmapPixels, this.widthOfDefaultBitmap * sizeof(int), 0);

            // Start Kinect sensor chooser
            this.sensorChooser = new KinectSensorChooser();
            this.sensorChooserUI.KinectSensorChooser = this.sensorChooser;
            this.sensorChooser.KinectChanged += this.OnKinectSensorChanged;
            this.sensorChooser.Start();

            // Add callback which is called every time WPF renders
            System.Windows.Media.CompositionTarget.Rendering += this.CompositionTargetRendering;

            // Initialize events
            this.allDataReadyEvent = new ManualResetEvent(false);
            this.depthReadyEvent = new ManualResetEvent(false);
            this.colorReadyEvent = new ManualResetEvent(false);
            this.faceTrackingResultReadyEvent = new ManualResetEvent(false);
            this.reconstructingEvent = new ManualResetEvent(false);

            // Start worker thread for depth processing
            this.StartWorkerThread();

            // Start face tracking worker thread
            this.StartFaceTrackingThread();

            // Start fps timer
            this.fpsTimer = new DispatcherTimer(DispatcherPriority.Send);
            this.fpsTimer.Interval = new TimeSpan(0, 0, FpsInterval);
            this.fpsTimer.Tick += this.FpsTimerTick;
            this.fpsTimer.Start();

            // Set last fps timestamp as now
            this.lastFPSTimestamp = DateTime.UtcNow;
        }

        /// <summary>
        /// Execute shutdown tasks
        /// </summary>
        /// <param name="sender">Object sending the event</param>
        /// <param name="e">Event arguments</param>
        private void WindowClosing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            // Stop timer
            if (null != this.fpsTimer)
            {
                this.fpsTimer.Stop();
                this.fpsTimer.Tick -= this.FpsTimerTick;
            }

            // Unregister Kinect sensor chooser event
            if (null != this.sensorChooser)
            {
                this.sensorChooser.KinectChanged -= this.OnKinectSensorChanged;
            }

            // Stop sensor
            if (null != this.sensor)
            {
                this.StopStreams();
            }

            // Stop worker thread
            this.StopWorkerThread();

            // Stop face tracking worker thread
            this.StopFaceTrackingThread();
        }

        /// <summary>
        /// Handler function for Kinect changed event
        /// </summary>
        /// <param name="sender">Event generator</param>
        /// <param name="e">Event parameter</param>
        private void OnKinectSensorChanged(object sender, KinectChangedEventArgs e)
        {
            // Check new sensor's status
            if (this.sensor != e.NewSensor)
            {
                // Stop old sensor
                if (null != this.sensor)
                {
                    this.StopStreams();
                }

                this.sensor = null;

                if (null != e.NewSensor && KinectStatus.Connected == e.NewSensor.Status)
                {
                    // Start new sensor
                    this.sensor = e.NewSensor;
                    this.StartStreams(DepthFormat, ColorFormat);
                }
            }
        }

        /// <summary>
        /// Handler for FPS timer tick
        /// </summary>
        /// <param name="sender">Object sending the event</param>
        /// <param name="e">Event arguments</param>
        private void FpsTimerTick(object sender, EventArgs e)
        {
            if (!this.savingMesh)
            {
                if (null == this.sensor)
                {
                    // Show "No ready Kinect found!" on status bar
                    this.statusBarText.Text = Properties.Resources.NoReadyKinect;
                }
                else
                {
                    // Calculate time span from last calculation of FPS
                    double intervalSeconds = (DateTime.UtcNow - this.lastFPSTimestamp).TotalSeconds;

                    // Calculate and show fps on status bar
                    this.statusBarText.Text = string.Format(
                        System.Globalization.CultureInfo.InvariantCulture,
                        Properties.Resources.Fps,
                        (double)this.processedFrameCount / intervalSeconds);
                }
            }

            // Reset frame counter
            this.processedFrameCount = 0;
            this.lastFPSTimestamp = DateTime.UtcNow;
        }

        /// <summary>
        /// Reset FPS timer and counter
        /// </summary>
        private void ResetFps()
        {
            // Restart fps timer
            if (null != this.fpsTimer)
            {
                this.fpsTimer.Stop();
                this.fpsTimer.Start();
            }

            // Reset frame counter
            this.processedFrameCount = 0;
            this.lastFPSTimestamp = DateTime.UtcNow;
        }

        /// <summary>
        /// Start depth and color stream at specific resolutions
        /// </summary>
        /// <param name="depthFormat">The resolution of image in depth stream</param>
        /// <param name="colorFormat">The resolution of image in color stream</param>
        private void StartStreams(DepthImageFormat depthFormat, ColorImageFormat colorFormat)
        {
            try
            {
                // Enable streams, register event handler and start
                this.sensor.DepthStream.Enable(depthFormat);
                this.sensor.ColorStream.Enable(colorFormat);

                this.sensor.SkeletonStream.TrackingMode = SkeletonTrackingMode.Seated;
                this.sensor.SkeletonStream.Enable();

                this.sensor.AllFramesReady += this.OnAllFramesReady;

                // Allocate frames for Kinect Fusion now a sensor is present
                this.AllocateFrames();

                this.sensor.Start();
            }
            catch (IOException ex)
            {
                // Device is in use
                this.sensor = null;
                this.ShowStatusMessage(ex.Message);

                return;
            }
            catch (InvalidOperationException ex)
            {
                // Device is not valid, not supported or hardware feature unavailable
                this.sensor = null;
                this.ShowStatusMessage(ex.Message);

                return;
            }

            // Check if sensor supports near mode
            try
            {
                this.NearMode = true;
                this.sensor.SkeletonStream.EnableTrackingInNearRange = true;

                this.nearModeCheckBox.IsEnabled = true;
            }
            catch (InvalidOperationException)
            {
                // Near mode not supported on device, silently fail during initialization
                this.nearModeCheckBox.IsEnabled = false;
            }

            // Reset the virtual camera
            if (this.virtualCamera != null)
            {
                this.virtualCamera.Reset();
            }

            this.ResetReconstructing();

            if (!this.firstFrame)
            {
                this.ResetShadedSurfaceImage();
            }

            this.processedColorFrameCount = 0;

            // Set recreate reconstruction flag
            this.recreateReconstruction = true;

            // Show introductory message
            this.ShowStatusMessage(Properties.Resources.IntroductoryMessage);
        }

        /// <summary>
        /// Stop streams by disabling, then stopping sensor
        /// </summary>
        private void StopStreams()
        {
            if (null != this.sensor)
            {
                try
                {
                    this.sensor.AllFramesReady -= this.OnAllFramesReady;

                    this.sensor.DepthStream.Disable();
                    this.sensor.ColorStream.Disable();
                    this.sensor.SkeletonStream.Disable();

                    this.sensor.Stop();
                }
                catch (InvalidOperationException)
                {
                    // Silently ignore
                }
            }
        }

        /// <summary>
        /// Start the work thread to process incoming depth data
        /// </summary>
        private void StartWorkerThread()
        {
            if (null == this.workerThread)
            {
                this.workerThreadStopEvent = new ManualResetEvent(false);

                // Create worker thread and start
                this.workerThread = new Thread(this.WorkerThreadProc);
                this.workerThread.Start();
            }
        }

        /// <summary>
        /// Stop worker thread
        /// </summary>
        private void StopWorkerThread()
        {
            if (null != this.workerThread)
            {
                // Set stop event to stop thread
                this.workerThreadStopEvent.Set();

                // Wait for exit of thread
                this.workerThread.Join();
            }
        }

        /// <summary>
        /// Start the work thread to track face
        /// </summary>
        private void StartFaceTrackingThread()
        {
            if (null == this.faceTrackingThread)
            {
                this.faceTrackingThreadStopEvent = new ManualResetEvent(false);

                // Create worker thread and start
                this.faceTrackingThread = new Thread(this.FaceTrackingThreadProc);
                this.faceTrackingThread.Start();
            }
        }

        /// <summary>
        /// Stop face tracking worker thread
        /// </summary>
        private void StopFaceTrackingThread()
        {
            if (null != this.faceTrackingThread)
            {
                // Set stop event to stop thread
                this.faceTrackingThreadStopEvent.Set();

                // Wait for exit of thread
                this.faceTrackingThread.Join();
            }
        }

        /// <summary>
        /// Worker thread in which depth data is processed
        /// </summary>
        private void WorkerThreadProc()
        {
            WaitHandle[] events = new WaitHandle[2] { this.workerThreadStopEvent, this.depthReadyEvent };
            while (true)
            {
                int index = WaitHandle.WaitAny(events);

                if (0 == index)
                {
                    // Stop event has been set. Exit thread
                    break;
                }

                // Reset depth ready event
                this.depthReadyEvent.Reset();

                // Pass data to process
                this.Process();
            }
        }

        /// <summary>
        /// Face tracking worker thread
        /// </summary>
        private void FaceTrackingThreadProc()
        {
            WaitHandle[] events = new WaitHandle[2] { this.faceTrackingThreadStopEvent, this.allDataReadyEvent };
            while (true)
            {
                int index = WaitHandle.WaitAny(events);

                if (0 == index)
                {
                    // Stop event has been set. Exit thread
                    break;
                }

                // Reset depth ready event
                this.allDataReadyEvent.Reset();

                // Pass data to process
                if (this.processedColorFrameCount % FaceTrackingInterval == 0)
                {
                    this.TrackFace();
                }
            }
        }

        /// <summary>
        /// Called on each render of WPF (usually around 60Hz)
        /// </summary>
        /// <param name="sender">Object sending the event</param>
        /// <param name="e">Event arguments</param>
        private void CompositionTargetRendering(object sender, EventArgs e)
        {
            // If the viewChanged flag is used so we only raycast the volume when something changes
            // When reconstructing we call RenderReconstruction manually for every integrated depth frame (see ReconstructDepthData)
            if (this.viewChanged)
            {
                this.RenderReconstruction();
                this.viewChanged = false;
            }
        }

        /// <summary>
        /// Event handler for Kinect sensor's AllFramesReady event
        /// </summary>
        /// <param name="sender">object sending the event</param>
        /// <param name="allFramesReadyEventArgs">event arguments</param>
        private void OnAllFramesReady(object sender, AllFramesReadyEventArgs allFramesReadyEventArgs)
        {
            // In the middle of shutting down etc, nothing to do
            if (null == this.sensor)
            {
                return;
            }

            this.OnColorFrameReady(sender, allFramesReadyEventArgs);

            this.OnDepthFrameReady(sender, allFramesReadyEventArgs);

            this.OnSkeletonFrameReady(sender, allFramesReadyEventArgs);

            // Signal face tracking thread to process
            this.allDataReadyEvent.Set();
        }

        /// <summary>
        /// Handler for Kinect sensor's color data
        /// </summary>
        /// <param name="sender">object sending the event</param>
        /// <param name="e">event arguments</param>
        private void OnColorFrameReady(object sender, AllFramesReadyEventArgs e)
        {
            bool validColor = false;

            // Open color frame
            using (ColorImageFrame colorFrame = e.OpenColorImageFrame())
            {
                if (null != colorFrame)
                {
                    lock (this.inputLock)
                    {
                        // If the input color data changes size, re-allocate frame resources
                        if (this.colorImagePixels.Length != colorFrame.PixelDataLength)
                        {
                            this.colorWidth = colorFrame.Width;
                            this.colorHeight = colorFrame.Height;

                            this.AllocateFrames();
                        }

                        // Copy color pixels to local buffer
                        colorFrame.CopyPixelDataTo(this.colorImagePixels);
                        colorFrame.CopyPixelDataTo(this.colorImageRawPixels);

                        validColor = true;

                        // When we are not on reconstructing state, render the color frame
                        if (!this.firstFrame && (!this.reconstructing || null == this.volume))
                        {
                            if (!this.mirrorDepth)
                            {
                                MirrorColorHorizontalInPlace(this.colorImagePixels, this.colorWidth, this.colorHeight);
                            }

                            // Use dispatcher object to invoke PreProcessDepthData function to process
                            RenderColorImage(
                                 this.colorImagePixels,
                                 this.colorWidth,
                                 this.colorHeight,
                                 ref this.colorFrameBitmap,
                                 this.colorAndDeltaImage);
                        }
                    }
                }
            }

            if (validColor)
            {
                // Signal worker thread to process
                this.colorReadyEvent.Set();
            }

            this.processedColorFrameCount++;
        }

        /// <summary>
        /// Handler for Kinect sensor's depth data
        /// </summary>
        /// <param name="sender">object sending the event</param>
        /// <param name="e">event arguments</param>
        private void OnDepthFrameReady(object sender, AllFramesReadyEventArgs e)
        {
            bool validDepth = false;

            // Open depth frame
            using (DepthImageFrame depthFrame = e.OpenDepthImageFrame())
            {
                if (null != depthFrame)
                {
                    lock (this.inputLock)
                    {
                        // Save frame timestamp
                        this.FrameTimestamp = depthFrame.Timestamp;

                        // If the input depth data changes size, re-allocate frame resources
                        if (this.depthImagePixels.Length != depthFrame.PixelDataLength)
                        {
                            this.depthWidth = depthFrame.Width;
                            this.depthHeight = depthFrame.Height;

                            this.AllocateFrames();
                        }

                        // Copy depth pixels to local buffer
                        depthFrame.CopyPixelDataTo(this.depthImageData);
                        depthFrame.CopyDepthImagePixelDataTo(this.depthImagePixels);

                        validDepth = true;
                    }
                }
            }

            if (validDepth)
            {
                // Signal worker thread to process
                this.depthReadyEvent.Set();
            }
        }

        /// <summary>
        /// Handler for Kinect sensor's skeleton data
        /// </summary>
        /// <param name="sender">object sending the event</param>
        /// <param name="e">event arguments</param>
        private void OnSkeletonFrameReady(object sender, AllFramesReadyEventArgs e)
        {
            // Open skeleton frame
            using (SkeletonFrame skeletonFrame = e.OpenSkeletonFrame())
            {
                // Reset the selected skeleton
                this.selectedSkeleton = null;
                this.trackingId = 0;

                if (null != skeletonFrame)
                {
                    lock (this.inputLock)
                    {
                        // Reallocate if necessary
                        if (null == this.skeletonData || this.skeletonData.Length != skeletonFrame.SkeletonArrayLength)
                        {
                            this.skeletonData = new Skeleton[skeletonFrame.SkeletonArrayLength];
                        }

                        // Copy skeleton data to local buffer
                        skeletonFrame.CopySkeletonDataTo(this.skeletonData);

                        // Select a proper skeleton
                        if (this.trackingId != 0)
                        {
                            this.selectedSkeleton = (from s in this.skeletonData where s != null && s.TrackingId == this.trackingId && s.TrackingState == SkeletonTrackingState.Tracked select s)
                                .FirstOrDefault();
                        }
                        else if (null == this.selectedSkeleton)
                        {
                            this.selectedSkeleton = (from s in this.skeletonData where s != null && s.TrackingState == SkeletonTrackingState.Tracked select s)
                                .FirstOrDefault();
                        }

                        if (null != this.selectedSkeleton)
                        {
                            this.trackingId = this.selectedSkeleton.TrackingId;
                        }
                    }
                }
            }
        }

        /// <summary>
        /// The main Kinect Fusion process function
        /// </summary>
        private void Process()
        {
            if (this.recreateReconstruction)
            {
                lock (this.volumeLock)
                {
                    this.recreateReconstruction = !this.RecreateReconstruction();
                }
            }

            if (this.resetReconstruction)
            {
                this.resetReconstruction = false;
                lock (this.volumeLock)
                {
                    this.ResetReconstruction();
                }
            }

            // Convert depth to float and render depth frame
            this.ProcessDepthData();

            if (null != this.volume && !this.savingMesh && this.reconstructing)
            {
                try
                {
                    // Track camera pose
                    this.TrackCamera();

                    // Only continue if we do not have tracking errors
                    if (!this.hasLostTracking)
                    {
                        // Update relocation database
                        lock (this.faceTrackingLock)
                        {
                            this.relocation.UpdateRelocation(this.faceInfo, this.worldToCameraTransform);
                        }

                        // Integrate depth
                        lock (this.volumeLock)
                        {
                            this.IntegrateData();
                        }

                        // Check to see if another depth frame is already available. 
                        // If not we have time to calculate a point cloud and render, 
                        // but if so we make sure we force a render at least every 
                        // RenderIntervalMilliseconds.
                        if (!this.depthReadyEvent.WaitOne(0) || this.IsRenderOverdue)
                        {
                            // Raycast and render
                            Dispatcher.BeginInvoke((Action)(() => this.RenderReconstruction()));
                        }
                    }
                }
                catch (InvalidOperationException ex)
                {
                    this.ShowStatusMessage(ex.Message);
                }
            }
        }

        /// <summary>
        /// Face Tracking
        /// </summary>
        private void TrackFace()
        {
            // Track the face
            bool computedBoundingbox = null == this.volume;

            this.faceTrackingViewer.TrackFace(
                this.sensor, 
                ColorFormat, 
                this.colorImageRawPixels,
                DepthFormat, 
                this.depthImageData, 
                this.selectedSkeleton, 
                computedBoundingbox);

            // Copy data to local
            lock (this.faceTrackingLock)
            {
                this.faceInfo = this.faceTrackingViewer.FaceInfo;
                this.minimumPoint = this.faceTrackingViewer.MinimumPoint;
                this.maximumPoint = this.faceTrackingViewer.MaximumPoint;
            }

            // We have tracked a face successfully and computed head size
            if (this.faceInfo.TrackValid && computedBoundingbox)
            {
                this.faceTrackingResultReadyEvent.Set();
            }
        }

        /// <summary>
        /// Process the color and depth inputs, converting the color into the depth space
        /// </summary>
        private unsafe void MapColorToDepth()
        {
            if (null == this.mapper)
            {
                // Create a coordinate mapper
                this.mapper = new CoordinateMapper(this.sensor);
            }

            this.mapper.MapDepthFrameToColorFrame(DepthFormat, this.depthImagePixels, ColorFormat, this.colorCoordinates);

            lock (this.inputLock)
            {
                if (this.mirrorDepth)
                {
                    // Here we make use of unsafe code to just copy the whole pixel as an int for performance reasons, as we do
                    // not need access to the individual rgba components.
                    fixed (byte* ptrColorPixels = this.colorImagePixels)
                    {
                        int* rawColorPixels = (int*)ptrColorPixels;

                        Parallel.For(
                            0,
                            this.depthHeight,
                            y =>
                                {
                                    int destIndex = y * this.depthWidth;

                                    for (int x = 0; x < this.depthWidth; ++x, ++destIndex)
                                    {
                                        // calculate index into depth array
                                        int colorInDepthX = colorCoordinates[destIndex].X;
                                        int colorInDepthY = colorCoordinates[destIndex].Y;

                                        // make sure the depth pixel maps to a valid point in color space
                                        if (colorInDepthX >= 0 && colorInDepthX < this.colorWidth && colorInDepthY >= 0
                                            && colorInDepthY < this.colorHeight && depthImagePixels[destIndex].Depth != 0)
                                        {
                                            // Calculate index into color array
                                            int sourceColorIndex = colorInDepthX + (colorInDepthY * this.colorWidth);

                                            // Copy color pixel
                                            this.mappedColorPixels[destIndex] = rawColorPixels[sourceColorIndex];
                                        }
                                        else
                                        {
                                            this.mappedColorPixels[destIndex] = 0;
                                        }
                                    }
                                });
                    }
                }
                else
                {
                    // Here we make use of unsafe code to just copy the whole pixel as an int for performance reasons, as we do
                    // not need access to the individual rgba components.
                    fixed (byte* ptrColorPixels = this.colorImagePixels)
                    {
                        int* rawColorPixels = (int*)ptrColorPixels;

                        // Horizontal flip the color image as the standard depth image is flipped internally in Kinect Fusion
                        // to give a viewpoint as though from behind the Kinect looking forward by default.
                        Parallel.For(
                            0,
                            this.depthHeight,
                            y =>
                                {
                                    int destIndex = y * this.depthWidth;
                                    int flippedDestIndex = destIndex + (this.depthWidth - 1); // horizontally mirrored

                                    for (int x = 0; x < this.depthWidth; ++x, ++destIndex, --flippedDestIndex)
                                    {
                                        // calculate index into depth array
                                        int colorInDepthX = colorCoordinates[destIndex].X;
                                        int colorInDepthY = colorCoordinates[destIndex].Y;

                                        // make sure the depth pixel maps to a valid point in color space
                                        if (colorInDepthX >= 0 && colorInDepthX < this.colorWidth && colorInDepthY >= 0
                                            && colorInDepthY < this.colorHeight && depthImagePixels[destIndex].Depth != 0)
                                        {
                                            // Calculate index into color array- this will perform a horizontal flip as well
                                            int sourceColorIndex = colorInDepthX + (colorInDepthY * this.colorWidth);

                                            // Copy color pixel
                                            this.mappedColorPixels[flippedDestIndex] = rawColorPixels[sourceColorIndex];
                                        }
                                        else
                                        {
                                            this.mappedColorPixels[flippedDestIndex] = 0;
                                        }
                                    }
                                });
                    }
                }
            }

            this.mappedColorFrame.CopyPixelDataFrom(this.mappedColorPixels);
        }

        /// <summary>
        /// Process the depth input for camera tracking
        /// </summary>
        private void ProcessDepthData()
        {
            // To enable playback of a .xed file through Kinect Studio and reset of the reconstruction
            // if the .xed loops, we test for when the frame timestamp has skipped a large number. 
            // Note: this will potentially continually reset live reconstructions on slow machines which
            // cannot process a live frame in less time than the reset threshold. Increase the number of
            // milliseconds if this is a problem.
            if (AutoResetReconstructionOnTimeSkip)
            {
                this.CheckResetTimeStamp(this.FrameTimestamp);
            }

            // Lock the depth operations
            lock (this.inputLock)
            {
                // Convert depth frame to depth float frame
                if (null != this.volume)
                {
                    this.volume.DepthToDepthFloatFrame(
                        this.depthImagePixels,
                        this.depthFloatFrame,
                        this.minDepthClip,
                        this.maxDepthClip,
                        this.MirrorDepth);
                }
                else
                {
                    FusionDepthProcessor.DepthToDepthFloatFrame(
                        this.depthImagePixels,
                        this.depthWidth,
                        this.depthHeight,
                        this.depthFloatFrame,
                        this.minDepthClip,
                        this.maxDepthClip,
                        this.mirrorDepth);
                }
            }

            // Render depth float frame
            Dispatcher.BeginInvoke((Action)(() => this.DepthFrameComplete()));
        }

        /// <summary>
        /// Process the depth input for camera tracking
        /// </summary>
        private void TrackCamera()
        {
            bool trackingSucceeded = false;
            bool calculateDeltaFrame = this.processedFrameCount % DeltaFrameCalculationInterval == 0;

            // Align new depth float image with reconstruction
            // Note that here we only calculate the deltaFromReferenceFrame every other frame
            // to reduce computation time
            if (calculateDeltaFrame)
            {
                trackingSucceeded = this.volume.AlignDepthFloatToReconstruction(
                    this.depthFloatFrame,
                    FusionDepthProcessor.DefaultAlignIterationCount,
                    this.deltaFromReferenceFrame,
                    out this.alignmentEnergy,
                    this.worldToCameraTransform);
            }
            else
            {
                // Don't bother getting the residual delta from reference frame to cut computation time
                trackingSucceeded = this.volume.AlignDepthFloatToReconstruction(
                    this.depthFloatFrame,
                    FusionDepthProcessor.DefaultAlignIterationCount,
                    null,
                    out this.alignmentEnergy,
                    this.worldToCameraTransform);
            }

            if (!trackingSucceeded || (this.processedFrameCount > 1 && this.alignmentEnergy < MinAlignmentEnergyForSuccess))
            {
                lock (this.faceTrackingLock)
                {
                    bool retrieveSucceeded = this.relocation.RetrieveTransformMatrix(this.faceInfo, out this.worldToCameraTransform);
                    if (retrieveSucceeded)
                    {
                        trackingSucceeded = true;

                        // Show relocation success
                        this.ShowStatusMessage(Properties.Resources.SuccessfulRelocation);
                    }
                    else
                    {
                        trackingSucceeded = false;

                        // Show tracking lost on status bar
                        this.ShowStatusMessage(Properties.Resources.CameraTrackingFailed);
                    }
                }
            }
            else
            {
                // Get updated camera transform from image alignment
                Matrix4 calculatedCameraPos = this.volume.GetCurrentWorldToCameraTransform();

                // When we are on reconstructing state, render the delta frame
                if (this.reconstructing && calculateDeltaFrame)
                {
                    // Render delta from reference frame
                    Dispatcher.BeginInvoke(
                        (Action)
                        (() =>
                            this.RenderAlignDeltasFloatImage(
                                this.deltaFromReferenceFrame,
                                ref this.deltaFromReferenceFrameBitmap,
                                this.colorAndDeltaImage)));
                }

                this.worldToCameraTransform = calculatedCameraPos;
            }

            if (trackingSucceeded)
            {
                // Here we could also update this.virtualCamera.WorldToCameraMatrix4
                // if we wanted to show the volume cube graphics in the Kinect view
                Dispatcher.BeginInvoke((Action)(() => this.virtualCamera.UpdateFrustumTransformMatrix4(this.worldToCameraTransform)));

                // Increase processed frame counter
                this.processedFrameCount++;
            }

            // Set to tracking flag
            this.hasLostTracking = !trackingSucceeded;
        }

        /// <summary>
        /// Perform volume depth data integration
        /// </summary>
        private void IntegrateData()
        {
            // Integrate the frame to volume
            if (!this.PauseIntegration)
            {
                bool integrateColor = this.processedFrameCount % ColorIntegrationInterval == 0;

                // Color may opportunistically be available here - check
                if (this.captureColor && integrateColor && this.colorReadyEvent.WaitOne(0))
                {
                    // Pre-process color
                    this.MapColorToDepth();

                    // Integrate color and depth
                    this.volume.IntegrateFrame(
                        this.depthFloatFrame, 
                        this.mappedColorFrame, 
                        this.integrationWeight,
                        FusionDepthProcessor.DefaultColorIntegrationOfAllAngles,
                        this.worldToCameraTransform);

                    // Flag that we have captured color
                    this.colorCaptured = true;
                }
                else
                {
                    // Just integrate depth
                    this.volume.IntegrateFrame(
                        this.depthFloatFrame, 
                        this.integrationWeight, 
                        this.worldToCameraTransform);
                }

                // Reset color ready event
                this.colorReadyEvent.Reset();
            }
        }

        /// <summary>
        /// Render the reconstruction
        /// </summary>
        private void RenderReconstruction()
        {
            Matrix4 cameraView = this.KinectView ? this.worldToCameraTransform : this.virtualCameraWorldToCameraMatrix4;

            lock (this.volumeLock)
            {
                if (null == this.volume || this.savingMesh || null == this.pointCloudFrame
                    || null == this.shadedSurfaceFrame || null == this.shadedSurfaceNormalsFrame)
                {
                    return;
                }

                if (this.captureColor)
                {
                    this.volume.CalculatePointCloud(this.pointCloudFrame, this.shadedSurfaceFrame, cameraView);
                }
                else
                {
                    this.volume.CalculatePointCloud(this.pointCloudFrame, cameraView);

                    // Shade point cloud frame for rendering
                    FusionDepthProcessor.ShadePointCloud(
                        this.pointCloudFrame,
                        cameraView,
                        this.displayNormals ? null : this.shadedSurfaceFrame,
                        this.displayNormals ? this.shadedSurfaceNormalsFrame : null);
                }

                // Update the rendered UI image
                if (this.reconstructing)
                {
                    this.ReconstructFrameComplete();
                }

                this.lastRenderTimestamp = DateTime.UtcNow;
            }
        }

        /// <summary>
        /// Called when a ray-casted view of the reconstruction is available for display in the UI 
        /// </summary>
        private void ReconstructFrameComplete()
        {
            // Render shaded surface frame or shaded surface normals frame
            RenderColorImage(
                this.captureColor ? this.shadedSurfaceFrame : (this.displayNormals ? this.shadedSurfaceNormalsFrame : this.shadedSurfaceFrame),
                ref this.shadedSurfaceFramePixelsArgb,
                ref this.shadedSurfaceFrameBitmap,
                this.shadedSurfaceImage);
        }

        /// <summary>
        /// Render Fusion AlignDepthFloatToReconstruction float deltas frame to UI
        /// </summary>
        /// <param name="alignDeltasFloatFrame">Fusion depth float frame</param>
        /// <param name="bitmap">Bitmap contains float frame data for rendering</param>
        /// <param name="image">UI image component to render float frame to</param>
        private void RenderAlignDeltasFloatImage(FusionFloatImageFrame alignDeltasFloatFrame, ref WriteableBitmap bitmap, System.Windows.Controls.Image image)
        {
            if (null == alignDeltasFloatFrame)
            {
                return;
            }

            if (null == bitmap || alignDeltasFloatFrame.Width != bitmap.Width || alignDeltasFloatFrame.Height != bitmap.Height)
            {
                // Create bitmap of correct format
                bitmap = new WriteableBitmap(alignDeltasFloatFrame.Width, alignDeltasFloatFrame.Height, 96.0, 96.0, PixelFormats.Bgr32, null);
            }

            // Set bitmap as source to UI image object
            image.Source = bitmap;

            alignDeltasFloatFrame.CopyPixelDataTo(this.deltaFromReferenceFrameFloatPixels);

            Parallel.For(
            0, 
            alignDeltasFloatFrame.Height, 
            y => 
            {
                int index = y * alignDeltasFloatFrame.Width;
                for (int x = 0; x < alignDeltasFloatFrame.Width; ++x, ++index)
                {
                    float residue = this.deltaFromReferenceFrameFloatPixels[index];

                    if (residue < 1.0f)
                    {
                        this.deltaFromReferenceFramePixelsArgb[index] = (byte)(255.0f * Helper.ClampFloatingPoint(1.0f - residue, 0.0f, 1.0f)); // blue
                        this.deltaFromReferenceFramePixelsArgb[index] |= ((byte)(255.0f * Helper.ClampFloatingPoint(1.0f - Math.Abs(residue), 0.0f, 1.0f))) << 8; // green
                        this.deltaFromReferenceFramePixelsArgb[index] |= ((byte)(255.0f * Helper.ClampFloatingPoint(1.0f + residue, 0.0f, 1.0f))) << 16; // red
                    }
                    else
                    {
                        this.deltaFromReferenceFramePixelsArgb[index] = 0;
                    }
                }
            });

            // Copy colored pixels to bitmap
            bitmap.WritePixels(
                        new Int32Rect(0, 0, alignDeltasFloatFrame.Width, alignDeltasFloatFrame.Height),
                        this.deltaFromReferenceFramePixelsArgb,
                        bitmap.PixelWidth * sizeof(int),
                        0);
        }

        /// <summary>
        /// Called when a depth frame is available for display in the UI
        /// </summary>
        private void DepthFrameComplete()
        {
            if (this.firstFrame)
            {
                this.firstFrame = false;

                // Render shaded surface frame or shaded surface normals frame - blank at this point
                RenderColorImage(
                    this.shadedSurfaceFrame,
                    ref this.shadedSurfaceFramePixelsArgb,
                    ref this.shadedSurfaceFrameBitmap,
                    this.shadedSurfaceImage);
            }

            // Render depth float frame
            this.RenderDepthFloatImage(ref this.depthFloatFrameBitmap, this.depthFloatImage);
        }

        /// <summary>
        /// Render Fusion depth float frame to UI
        /// </summary>
        /// <param name="bitmap">Bitmap contains depth float frame data for rendering</param>
        /// <param name="image">UI image component to render depth float frame to</param>
        private void RenderDepthFloatImage(ref WriteableBitmap bitmap, System.Windows.Controls.Image image)
        {
            if (null == this.depthFloatFrame)
            {
                return;
            }

            if (null == bitmap || this.depthFloatFrame.Width != bitmap.Width || this.depthFloatFrame.Height != bitmap.Height)
            {
                // Create bitmap of correct format
                bitmap = new WriteableBitmap(this.depthFloatFrame.Width, this.depthFloatFrame.Height, 96.0, 96.0, PixelFormats.Bgr32, null);

                // Set bitmap as source to UI image object
                image.Source = bitmap;
            }

            this.depthFloatFrame.CopyPixelDataTo(this.depthFloatFrameDepthPixels);

            // Calculate color of pixels based on depth of each pixel
            float range = 4.0f;
            float oneOverRange = (1.0f / range) * 256.0f;
            float minRange = 0.0f;

            Parallel.For(
            0,
            this.depthFloatFrame.Height,
            y =>
            {
                int index = y * this.depthFloatFrame.Width;
                for (int x = 0; x < this.depthFloatFrame.Width; ++x, ++index)
                {
                    float depth = this.depthFloatFrameDepthPixels[index];
                    int intensity = (depth >= minRange) ? ((byte)((depth - minRange) * oneOverRange)) : 0;

                    this.depthFloatFramePixelsArgb[index] = (255 << 24) | (intensity << 16) | (intensity << 8) | intensity; // set blue, green, red
                }
            });

            // Copy colored pixels to bitmap
            bitmap.WritePixels(
                        new Int32Rect(0, 0, this.depthFloatFrame.Width, this.depthFloatFrame.Height),
                        this.depthFloatFramePixelsArgb,
                        bitmap.PixelWidth * sizeof(int),
                        0);
        }

        /// <summary>
        /// Allocate the frame buffers used in the process
        /// </summary>
        private void AllocateFrames()
        {
            this.SafeDisposeFusionResources();

            // Allocate depth float frame
            this.depthFloatFrame = new FusionFloatImageFrame(this.depthWidth, this.depthHeight);

            // Allocate color frame for color data from Kinect mapped into depth frame
            this.mappedColorFrame = new FusionColorImageFrame(this.depthWidth, this.depthHeight);

            // Allocate delta from reference frame
            this.deltaFromReferenceFrame = new FusionFloatImageFrame(this.depthWidth, this.depthHeight);

            // Allocate point cloud frame
            this.pointCloudFrame = new FusionPointCloudImageFrame(this.depthWidth, this.depthHeight);

            // Allocate shaded surface frame
            this.shadedSurfaceFrame = new FusionColorImageFrame(this.depthWidth, this.depthHeight);

            // Allocate shaded surface normals frame
            this.shadedSurfaceNormalsFrame = new FusionColorImageFrame(this.depthWidth, this.depthHeight);

            int depthImageSize = this.depthWidth * this.depthHeight;
            int colorImageSize = this.colorWidth * this.colorHeight * sizeof(int);

            // Create local depth pixels buffer
            this.depthImagePixels = new DepthImagePixel[depthImageSize];

            // Create local depth data buffer
            this.depthImageData = new short[depthImageSize];

            // Create local raw color pixels buffer
            this.colorImageRawPixels = new byte[colorImageSize];

            // Create local color pixels buffer
            this.colorImagePixels = new byte[colorImageSize];

            // Create float pixel array
            this.depthFloatFrameDepthPixels = new float[depthImageSize];

            // Create float pixel array
            this.deltaFromReferenceFrameFloatPixels = new float[depthImageSize];

            // Create colored pixel array of correct format
            this.depthFloatFramePixelsArgb = new int[depthImageSize];

            // Create colored pixel array of correct format
            this.deltaFromReferenceFramePixelsArgb = new int[depthImageSize];

            // Allocate the depth-color mapping points
            this.colorCoordinates = new ColorImagePoint[depthImageSize];

            // Allocate mapped color points (i.e. color in depth frame of reference)
            this.mappedColorPixels = new int[depthImageSize];
        }

        /// <summary>
        /// Check and enable or disable near mode
        /// </summary>
        private void OnNearModeChanged()
        {
            if (null != this.sensor)
            {
                try
                {
                    this.sensor.DepthStream.Range = this.nearMode ? DepthRange.Near : DepthRange.Default;
                }
                catch (InvalidOperationException)
                {
                    // Near mode not supported on this device
                    this.ShowStatusMessage(Properties.Resources.NearModeNotSupported);
                    this.nearMode = false;
                }
            }
        }

        /// <summary>
        /// Check if the gap between 2 frames has reached reset time threshold. If yes, reset the reconstruction
        /// </summary>
        private void CheckResetTimeStamp(long frameTimestamp)
        {
            if (0 != this.lastFrameTimestamp && !this.savingMesh)
            {
                long timeThreshold = (ReconstructionProcessor.Amp == ProcessorType) ? ResetOnTimeStampSkippedMillisecondsGPU : ResetOnTimeStampSkippedMillisecondsCPU;

                // Calculate skipped milliseconds between 2 frames
                long skippedMilliseconds = Math.Abs(frameTimestamp - this.lastFrameTimestamp);

                if (skippedMilliseconds >= timeThreshold)
                {
                    this.ShowStatusMessage(Properties.Resources.ResetVolume);
                    this.resetReconstruction = true;
                }
            }

            // Set timestamp of last frame
            this.lastFrameTimestamp = frameTimestamp;
        }

        /// <summary>
        /// Re-create the reconstruction object
        /// </summary>
        /// <returns>Indicate success or failure</returns>
        private bool RecreateReconstruction()
        {
            // Check if sensor has been initialized
            if (null == this.sensor)
            {
                return false;
            }

            if (null != this.volume)
            {
                this.volume.Dispose();
                this.volume = null;
            }

            // Only create a volume when we have a detected face, computed its bounding box and start scan head
            WaitHandle[] events = new WaitHandle[2] { this.reconstructingEvent, this.faceTrackingResultReadyEvent };
            if (WaitHandle.WaitAll(events, 10))
            {
                // Show the status
                this.ShowStatusMessage(Properties.Resources.CreateVolume);

                lock (this.faceTrackingLock)
                {
                    // Calculate volume size, based on face tracking result
                    float width = this.maximumPoint.X - this.minimumPoint.X;
                    float height = this.maximumPoint.Y - this.minimumPoint.Y;
                    float depth = this.maximumPoint.Z - this.minimumPoint.Z;

                    // We extend the size to make sure containing head
                    this.VoxelsX = this.RoundVolumeEdgeLength(width + ExtendedHeadSizeAlignXZ);
                    this.VoxelsY = this.RoundVolumeEdgeLength(height + UpExtendedHeadSize);
                    this.VoxelsZ = this.RoundVolumeEdgeLength(depth + ExtendedHeadSizeAlignXZ);
                }

                try
                {
                    ReconstructionParameters volParam = new ReconstructionParameters(
                        this.voxelsPerMeter, this.voxelsX, this.voxelsY, this.voxelsZ);

                    // Set the world-view transform to identity, so the world origin is the initial camera location.
                    this.worldToCameraTransform = Matrix4.Identity;

                    this.volume = ColorReconstruction.FusionCreateReconstruction(volParam, ProcessorType, DeviceToUse, this.worldToCameraTransform);

                    this.defaultWorldToVolumeTransform = this.volume.GetCurrentWorldToVolumeTransform();

                    // Force adjustment
                    if (this.translateResetPoseByMinDepthThreshold || this.translateResetPoseByHeadOffset)
                    {
                        this.ResetReconstruction();
                    }
                    else
                    {
                        this.ResetColorImage();
                    }

                    // Reset "Pause Integration"
                    if (this.PauseIntegration)
                    {
                        this.PauseIntegration = false;
                    }

                    return true;
                }
                catch (ArgumentException)
                {
                    this.volume = null;
                    this.ShowStatusMessage(Properties.Resources.VolumeResolution);
                }
                catch (InvalidOperationException ex)
                {
                    this.volume = null;
                    this.ShowStatusMessage(ex.Message);
                }
                catch (DllNotFoundException)
                {
                    this.volume = null;
                    this.ShowStatusMessage(Properties.Resources.MissingPrerequisite);
                }
                catch (OutOfMemoryException)
                {
                    this.volume = null;
                    this.ShowStatusMessage(Properties.Resources.OutOfMemory);
                }

                return true;
            }

            // Reset the face tracking result event --- to ensure use the latest data
            this.faceTrackingResultReadyEvent.Reset();

            return false;
        }

        /// <summary>
        /// Reset reconstruction object to initial state
        /// </summary>
        private void ResetReconstruction()
        {
            if (null == this.sensor)
            {
                return;
            }

            // Set the world-view transform to identity, so the world origin is the initial camera location.
            this.worldToCameraTransform = Matrix4.Identity;

            // Reset volume
            if (null != this.volume)
            {
                try
                {
                    // Translate the reconstruction volume location away from the world origin by an amount equal
                    // to the minimum depth threshold. This ensures that some depth signal falls inside the volume.
                    // If set false, the default world origin is set to the center of the front face of the 
                    // volume, which has the effect of locating the volume directly in front of the initial camera
                    // position with the +Z axis into the volume along the initial camera direction of view.
                    if (this.translateResetPoseByMinDepthThreshold)
                    {
                        Matrix4 worldToVolumeTransform = this.defaultWorldToVolumeTransform;

                        // Translate the volume in the Z axis by the minDepthClip distance
                        float minDist = (this.minDepthClip < this.maxDepthClip) ? this.minDepthClip : this.maxDepthClip;
                        worldToVolumeTransform.M43 -= minDist * this.voxelsPerMeter;

                        this.volume.ResetReconstruction(this.worldToCameraTransform, worldToVolumeTransform); 
                    }
                    else if (this.translateResetPoseByHeadOffset)
                    {
                        // Calculate the location of the head wrt to camera
                        // Keep camera at world origin
                        Matrix4 worldToVolumeTransform = this.defaultWorldToVolumeTransform;    // this puts camera at center of front face of volume

                        // Get the translation vector
                        Vector3 translation = new Vector3();
                        lock (this.faceTrackingLock)
                        {
                            // Here we will translate the volume center to the head center
                            // Considering we extend the volume size align +Y axis to make sure containing head,
                            float translateY = this.minimumPoint.Y;
                            float translateX = (this.minimumPoint.X + this.maximumPoint.X) / 2;
                            float translateZ = (this.minimumPoint.Z + this.maximumPoint.Z) / 2;

                            translation.X = translateX * this.voxelsPerMeter;
                            translation.Y = (translateY * this.voxelsPerMeter) + (this.voxelsY / 2);
                            translation.Z = -(translateZ * this.voxelsPerMeter) + (this.voxelsZ / 2);
                        }

                        worldToVolumeTransform.M41 += translation.X;
                        worldToVolumeTransform.M42 += translation.Y;
                        worldToVolumeTransform.M43 += translation.Z;

                        this.volume.ResetReconstruction(this.worldToCameraTransform, worldToVolumeTransform);

                        // Force raycast of one frame
                        this.viewChanged = true;

                        Vector3D translationScaled = new Vector3D(-translation.X / this.voxelsPerMeter, translation.Y / this.voxelsPerMeter, translation.Z / this.voxelsPerMeter);

                        // Update the graphics volume cube rendering
                        Dispatcher.BeginInvoke((Action)(() =>
                        {
                            this.RemoveVolumeCube3DGraphics();
                            this.DisposeVolumeCube3DGraphics();
                            this.CreateCube3DGraphics(volumeCubeLineColor, LineThickness, translationScaled);
                            if (!this.kinectView)
                            {
                                this.AddVolumeCube3DGraphics();
                            }
                        }));

                        this.viewChanged = true;

                        this.virtualCamera.RotationOrigin = new Point3D(translationScaled.X, translationScaled.Y, translationScaled.Z);
                    }
                    else
                    {
                        this.volume.ResetReconstruction(this.worldToCameraTransform);
                    }

                    if (this.PauseIntegration)
                    {
                        this.PauseIntegration = false;
                    }

                    this.ResetColorImage();
                }
                catch (InvalidOperationException)
                {
                    this.ShowStatusMessage(Properties.Resources.ResetFailed);
                }
            }

            this.ShowStatusMessage(Properties.Resources.ResetVolume);
        }

        /// <summary>
        /// Reset the mapped color image on reset or re-create of volume
        /// </summary>
        private void ResetColorImage()
        {
            if (null != this.mappedColorFrame && null != this.mappedColorPixels)
            {
                // Clear the mapped color image
                Array.Clear(this.mappedColorPixels, 0, this.mappedColorPixels.Length);
                this.mappedColorFrame.CopyPixelDataFrom(this.mappedColorPixels);
            }

            this.colorCaptured = false;
        }

        /// <summary>
        /// Handler for click event from "Scan Head" button
        /// </summary>
        /// <param name="sender">Event sender</param>
        /// <param name="e">Event arguments</param>
        private void ScanHeadButtonClick(object sender, RoutedEventArgs e)
        {
            if (null == this.sensor)
            {
                return;
            }

            this.Reconstructing = true;
            if (null != this.reconstructingEvent)
            {
                this.reconstructingEvent.Set();
            }
        }

        /// <summary>
        /// Handler for click event from "Reset Virtual Camera" button
        /// </summary>
        /// <param name="sender">Event sender</param>
        /// <param name="e">Event arguments</param>
        private void ResetCameraButtonClick(object sender, RoutedEventArgs e)
        {
            if (null != this.virtualCamera)
            {
                this.virtualCamera.Reset();
                this.viewChanged = true;
            }
        }

        /// <summary>
        /// Handler for click event from "Reset Reconstruction" button
        /// </summary>
        /// <param name="sender">Event sender</param>
        /// <param name="e">Event arguments</param>
        private void ResetReconstructionButtonClick(object sender, RoutedEventArgs e)
        {
            if (null == this.sensor)
            {
                return;
            }

            this.ResetReconstructing();

            // Signal the worker thread to recreate the volume
            this.recreateReconstruction = true;

            // Reset the shadeSurfaceImage source to make sure it discards previous shaded frame
            this.ResetShadedSurfaceImage();

            // Reset the virtual camera
            if (null != this.virtualCamera)
            {
                this.virtualCamera.Reset();
                this.viewChanged = true;
            }

            // Update manual reset information to status bar
            this.ShowStatusMessage(Properties.Resources.ResetVolume);
        }

        /// <summary>
        /// Handler for click event from "Create Mesh" button
        /// </summary>
        /// <param name="sender">Event sender</param>
        /// <param name="e">Event arguments</param>
        private void CreateMeshButtonClick(object sender, RoutedEventArgs e)
        {
            try
            {
                this.ShowStatusMessage(Properties.Resources.SavingMesh);

                ColorMesh mesh = null;

                lock (this.volumeLock)
                {
                    this.savingMesh = true;

                    if (null == this.volume)
                    {
                        this.ShowStatusMessage(Properties.Resources.MeshNullVolume);
                        return;
                    }

                    mesh = this.volume.CalculateMesh(1);
                }

                if (null == mesh)
                {
                    this.ShowStatusMessage(Properties.Resources.ErrorSaveMesh);
                    return;
                }

                Win32.SaveFileDialog dialog = new Win32.SaveFileDialog();

                if (true == this.stlFormat.IsChecked)
                {
                    dialog.FileName = "MeshedReconstruction.stl";
                    dialog.Filter = "STL Mesh Files|*.stl|All Files|*.*";
                }
                else if (true == this.objFormat.IsChecked)
                {
                    dialog.FileName = "MeshedReconstruction.obj";
                    dialog.Filter = "OBJ Mesh Files|*.obj|All Files|*.*";
                }
                else
                {
                    dialog.FileName = "MeshedReconstruction.ply";
                    dialog.Filter = "PLY Mesh Files|*.ply|All Files|*.*";
                }

                if (true == dialog.ShowDialog())
                {
                    if (true == this.stlFormat.IsChecked)
                    {
                        using (BinaryWriter writer = new BinaryWriter(dialog.OpenFile()))
                        {
                            // Default to flip Y,Z coordinates on save
                            Helper.SaveBinaryStlMesh(mesh, writer, true);
                        }
                    }
                    else if (true == this.objFormat.IsChecked)
                    {
                        using (StreamWriter writer = new StreamWriter(dialog.FileName))
                        {
                            // Default to flip Y,Z coordinates on save
                            Helper.SaveAsciiObjMesh(mesh, writer, true);
                        }
                    }
                    else
                    {
                        using (StreamWriter writer = new StreamWriter(dialog.FileName))
                        {
                            // Default to flip Y,Z coordinates on save
                            Helper.SaveAsciiPlyMesh(mesh, writer, true, this.colorCaptured);
                        }
                    }

                    this.ShowStatusMessage(Properties.Resources.MeshSaved);
                }
                else
                {
                    this.ShowStatusMessage(Properties.Resources.MeshSaveCanceled);
                }
            }
            catch (ArgumentException)
            {
                this.ShowStatusMessage(Properties.Resources.ErrorSaveMesh);
            }
            catch (InvalidOperationException)
            {
                this.ShowStatusMessage(Properties.Resources.ErrorSaveMesh);
            }
            catch (IOException)
            {
                this.ShowStatusMessage(Properties.Resources.ErrorSaveMesh);
            }
            catch (OutOfMemoryException)
            {
                this.ShowStatusMessage(Properties.Resources.ErrorSaveMeshOutOfMemory);
            }
            finally
            {
                // Update timestamp of last frame to avoid auto reset reconstruction
                this.lastFrameTimestamp = 0;

                this.savingMesh = false;
            }
        }

        /// <summary>
        /// Handler for volume setting changing event
        /// </summary>
        /// <param name="sender">Event sender</param>
        /// <param name="e">Event argument</param>
        private void VolumeSettingsChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            // Signal the worker thread to recreate the volume
            this.recreateReconstruction = true;
        }

        /// <summary>
        /// Show exception info on status bar
        /// </summary>
        /// <param name="message">Message to show on status bar</param>
        private void ShowStatusMessage(string message)
        {
            this.Dispatcher.BeginInvoke((Action)(() =>
            {
                this.ResetFps();
                this.statusBarText.Text = message;
            }));
        }

        /// <summary>
        /// Event raised when the mouse updates the graphics camera transformation for the virtual camera
        /// Here we set the viewChanged flag to true, to cause a volume render when the WPF composite update event occurs
        /// </summary>
        /// <param name="sender">Event generator</param>
        /// <param name="e">Event parameter</param>
        private void OnVirtualCameraTransformationChanged(object sender, EventArgs e)
        {
            this.virtualCameraWorldToCameraMatrix4 = this.virtualCamera.WorldToCameraMatrix4;
            this.viewChanged = true;
        }

        /// <summary>
        /// Calculate the volume edge length 
        /// </summary>
        /// <returns>Return the rounded edge length</returns>
        private int RoundVolumeEdgeLength(float edgeLength)
        {
            // Calculate this in voxels
            float voxelEdgeLength = edgeLength * this.voxelsPerMeter;

            // Round to multiple of 32
            int roundedVoxelEdgeLength = ((int)voxelEdgeLength + 16) / 32;  // round by truncate
            int volumeEdgeLength = roundedVoxelEdgeLength * 32;

            return (0 == volumeEdgeLength) ? 32 : volumeEdgeLength; // minimum of 32 size
        }

        /// <summary>
        /// Create an axis-aligned volume cube for rendering.
        /// </summary>
        /// <param name="color">The color of the volume cube.</param>
        /// <param name="thickness">The thickness of the lines in screen pixels.</param>
        /// <param name="translation">World to volume translation vector.</param>
        private void CreateCube3DGraphics(System.Windows.Media.Color color, int thickness, Vector3D translation)
        {
            // Scaler for cube size
            float cubeSizeScaler = 1.0f;

            // Before we created a volume which contains the head
            // Here we create a graphical representation of this volume cube
            float oneOverVpm = 1.0f / this.voxelsPerMeter;

            // This cube is world axis aligned
            float cubeSideX = this.voxelsX * oneOverVpm * cubeSizeScaler;
            float halfSideX = cubeSideX * 0.5f;

            float cubeSideY = this.voxelsY * oneOverVpm * cubeSizeScaler;
            float halfSideY = cubeSideY * 0.5f;

            float cubeSideZ = this.voxelsZ * oneOverVpm * cubeSizeScaler;
            float halfSideZ = cubeSideZ * 0.5f;

            // The translation vector is from the origin to the volume front face
            // And here we describe the translation Z as from the origin to the cube center
            // So we continue to translate half volume size align Z
            translation.Z -= halfSideZ / cubeSizeScaler;

            this.volumeCube = new ScreenSpaceLines3D();
            this.volumeCube.Points = new Point3DCollection();

            // Front face
            this.volumeCube.Points.Add(new Point3D(-halfSideX + translation.X, halfSideY + translation.Y, -halfSideZ + translation.Z));   // TL front
            this.volumeCube.Points.Add(new Point3D(halfSideX + translation.X, halfSideY + translation.Y, -halfSideZ + translation.Z));   // TR front

            this.volumeCube.Points.Add(new Point3D(halfSideX + translation.X, halfSideY + translation.Y, -halfSideZ + translation.Z));   // TR front
            this.volumeCube.Points.Add(new Point3D(halfSideX + translation.X, -halfSideY + translation.Y, -halfSideZ + translation.Z));   // BR front

            this.volumeCube.Points.Add(new Point3D(halfSideX + translation.X, -halfSideY + translation.Y, -halfSideZ + translation.Z));   // BR front
            this.volumeCube.Points.Add(new Point3D(-halfSideX + translation.X, -halfSideY + translation.Y, -halfSideZ + translation.Z));   // BL front

            this.volumeCube.Points.Add(new Point3D(-halfSideX + translation.X, -halfSideY + translation.Y, -halfSideZ + translation.Z));   // BL front
            this.volumeCube.Points.Add(new Point3D(-halfSideX + translation.X, halfSideY + translation.Y, -halfSideZ + translation.Z));   // TL front

            // Rear face
            this.volumeCube.Points.Add(new Point3D(-halfSideX + translation.X, halfSideY + translation.Y, halfSideZ + translation.Z));   // TL rear
            this.volumeCube.Points.Add(new Point3D(halfSideX + translation.X, halfSideY + translation.Y, halfSideZ + translation.Z));   // TR rear

            this.volumeCube.Points.Add(new Point3D(halfSideX + translation.X, halfSideY + translation.Y, halfSideZ + translation.Z));   // TR rear
            this.volumeCube.Points.Add(new Point3D(halfSideX + translation.X, -halfSideY + translation.Y, halfSideZ + translation.Z));   // BR rear

            this.volumeCube.Points.Add(new Point3D(halfSideX + translation.X, -halfSideY + translation.Y, halfSideZ + translation.Z));   // BR rear
            this.volumeCube.Points.Add(new Point3D(-halfSideX + translation.X, -halfSideY + translation.Y, halfSideZ + translation.Z));   // BL rear

            this.volumeCube.Points.Add(new Point3D(-halfSideX + translation.X, -halfSideY + translation.Y, halfSideZ + translation.Z));   // BL rear
            this.volumeCube.Points.Add(new Point3D(-halfSideX + translation.X, halfSideY + translation.Y, halfSideZ + translation.Z));   // TL rear

            // Connecting lines
            this.volumeCube.Points.Add(new Point3D(-halfSideX + translation.X, halfSideY + translation.Y, -halfSideZ + translation.Z));   // TL front
            this.volumeCube.Points.Add(new Point3D(-halfSideX + translation.X, halfSideY + translation.Y, halfSideZ + translation.Z));   // TL rear

            this.volumeCube.Points.Add(new Point3D(halfSideX + translation.X, halfSideY + translation.Y, -halfSideZ + translation.Z));   // TR front
            this.volumeCube.Points.Add(new Point3D(halfSideX + translation.X, halfSideY + translation.Y, halfSideZ + translation.Z));   // TR rear

            this.volumeCube.Points.Add(new Point3D(halfSideX + translation.X, -halfSideY + translation.Y, -halfSideZ + translation.Z));   // BR front
            this.volumeCube.Points.Add(new Point3D(halfSideX + translation.X, -halfSideY + translation.Y, halfSideZ + translation.Z));   // BR rear

            this.volumeCube.Points.Add(new Point3D(-halfSideX + translation.X, -halfSideY + translation.Y, -halfSideZ + translation.Z));   // BL front
            this.volumeCube.Points.Add(new Point3D(-halfSideX + translation.X, -halfSideY + translation.Y, halfSideZ + translation.Z));   // BL rear

            this.volumeCube.Thickness = thickness;
            this.volumeCube.Color = color;

            this.volumeCubeAxisX = new ScreenSpaceLines3D();

            this.volumeCubeAxisX.Points = new Point3DCollection();
            this.volumeCubeAxisX.Points.Add(new Point3D(-halfSideX + translation.X, halfSideY + translation.Y, halfSideZ + translation.Z));
            this.volumeCubeAxisX.Points.Add(new Point3D(-halfSideX + 0.1f + translation.X, halfSideY + translation.Y, halfSideZ + translation.Z));

            this.volumeCubeAxisX.Thickness = thickness + 2;
            this.volumeCubeAxisX.Color = System.Windows.Media.Color.FromArgb(200, 255, 0, 0);   // Red (X)

            this.volumeCubeAxisY = new ScreenSpaceLines3D();

            this.volumeCubeAxisY.Points = new Point3DCollection();
            this.volumeCubeAxisY.Points.Add(new Point3D(-halfSideX + translation.X, halfSideY + translation.Y, halfSideZ + translation.Z));
            this.volumeCubeAxisY.Points.Add(new Point3D(-halfSideX + translation.X, halfSideY - 0.1f + translation.Y, halfSideZ + translation.Z));

            this.volumeCubeAxisY.Thickness = thickness + 2;
            this.volumeCubeAxisY.Color = System.Windows.Media.Color.FromArgb(200, 0, 255, 0);   // Green (Y)

            this.volumeCubeAxisZ = new ScreenSpaceLines3D();

            this.volumeCubeAxisZ.Points = new Point3DCollection();
            this.volumeCubeAxisZ.Points.Add(new Point3D(-halfSideX + translation.X, halfSideY + translation.Y, halfSideZ + translation.Z));
            this.volumeCubeAxisZ.Points.Add(new Point3D(-halfSideX + translation.X, halfSideY + translation.Y, halfSideZ - 0.1f + translation.Z));

            this.volumeCubeAxisZ.Thickness = thickness + 2;
            this.volumeCubeAxisZ.Color = System.Windows.Media.Color.FromArgb(200, 0, 0, 255);   // Blue (Z)
        }

        /// <summary>
        /// Add the volume cube and axes to the visual tree
        /// </summary>
        private void AddVolumeCube3DGraphics()
        {
            if (this.haveAddedVolumeCube)
            {
                return;
            }

            if (null != this.volumeCube)
            {
                this.GraphicsViewport.Children.Add(this.volumeCube);

                this.haveAddedVolumeCube = true;
            }

            if (null != this.volumeCubeAxisX)
            {
                this.GraphicsViewport.Children.Add(this.volumeCubeAxisX);
            }

            if (null != this.volumeCubeAxisY)
            {
                this.GraphicsViewport.Children.Add(this.volumeCubeAxisY);
            }

            if (null != this.volumeCubeAxisZ)
            {
                this.GraphicsViewport.Children.Add(this.volumeCubeAxisZ);
            }
        }

        /// <summary>
        /// Remove the volume cube and axes from the visual tree
        /// </summary>
        private void RemoveVolumeCube3DGraphics()
        {
            if (!this.haveAddedVolumeCube)
            {
                return;
            }

            if (null != this.volumeCube)
            {
                this.GraphicsViewport.Children.Remove(this.volumeCube);
            }

            if (null != this.volumeCubeAxisX)
            {
                this.GraphicsViewport.Children.Remove(this.volumeCubeAxisX);
            }

            if (null != this.volumeCubeAxisY)
            {
                this.GraphicsViewport.Children.Remove(this.volumeCubeAxisY);
            }

            if (null != this.volumeCubeAxisZ)
            {
                this.GraphicsViewport.Children.Remove(this.volumeCubeAxisZ);
            }

            this.haveAddedVolumeCube = false;
        }

        /// <summary>
        /// Dispose the volume cube and axes
        /// </summary>
        private void DisposeVolumeCube3DGraphics()
        {
            if (this.haveAddedVolumeCube)
            {
                this.RemoveVolumeCube3DGraphics();
            }

            if (null != this.volumeCube)
            {
                this.volumeCube.Dispose();
                this.volumeCube = null;
            }

            if (null != this.volumeCubeAxisX)
            {
                this.volumeCubeAxisX.Dispose();
                this.volumeCubeAxisX = null;
            }

            if (null != this.volumeCubeAxisY)
            {
                this.volumeCubeAxisY.Dispose();
                this.volumeCubeAxisY = null;
            }

            if (null != this.volumeCubeAxisZ)
            {
                this.volumeCubeAxisZ.Dispose();
                this.volumeCubeAxisZ = null;
            }
        }

        /// <summary>
        /// Reset the reconstructing flag
        /// </summary>
        private void ResetReconstructing()
        {
            this.Reconstructing = false;
            if (null != this.reconstructingEvent)
            {
                this.reconstructingEvent.Reset();
            }
        }

        /// <summary>
        /// Reset the shaded surface image
        /// </summary>
        private void ResetShadedSurfaceImage()
        {
            if (null != this.defaultBitmap)
            {
                this.shadedSurfaceImage.Source = this.defaultBitmap;

                byte[] defaultBitmapPixels = new byte[this.widthOfDefaultBitmap * this.heightOfDefaultBitmap * 4];
                this.defaultBitmap.WritePixels(new Int32Rect(0, 0, this.widthOfDefaultBitmap, this.heightOfDefaultBitmap), defaultBitmapPixels, this.widthOfDefaultBitmap * sizeof(int), 0);
            }
        }

        /// <summary>
        /// Dispose fusion resources safely
        /// </summary>
        private void SafeDisposeFusionResources()
        {
            if (null != this.depthFloatFrame)
            {
                this.depthFloatFrame.Dispose();
            }

            if (null != this.mappedColorFrame)
            {
                this.mappedColorFrame.Dispose();
            }

            if (null != this.deltaFromReferenceFrame)
            {
                this.deltaFromReferenceFrame.Dispose();
            }

            if (null != this.shadedSurfaceFrame)
            {
                this.shadedSurfaceFrame.Dispose();
            }

            if (null != this.shadedSurfaceNormalsFrame)
            {
                this.shadedSurfaceNormalsFrame.Dispose();
            }

            if (null != this.pointCloudFrame)
            {
                this.pointCloudFrame.Dispose();
            }
        }
    }

    /// <summary>
    /// Convert depth to UI text
    /// </summary>
    public class DepthToTextConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            return ((double)value).ToString("0.00", CultureInfo.CurrentCulture) + "m";
        }

        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
