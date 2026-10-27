//
// Comment out this preprocessor definition to disable all of the
// sample content.
//
// To remove the content after disabling it:
//     * Remove the unused code from this file.
//     * Delete the Content folder provided with this template.
//
#define DRAW_SAMPLE_CONTENT

using System;
using System.Diagnostics;
using Windows.Gaming.Input;
using Windows.Graphics.DirectX.Direct3D11;
using Windows.Graphics.Holographic;
using Windows.Perception.Spatial;
using Windows.UI.Input.Spatial;
using Windows.UI.Popups;

using HololensSatelliteViewer.Common;
using HololensSatelliteViewer.Models;
using HololensSatelliteViewer.Services;
using System.Threading.Tasks;
using Windows.Foundation;
using System.Collections.Generic;
using Windows.Storage;

#if DRAW_SAMPLE_CONTENT
using HololensSatelliteViewer.Content;
#endif

namespace HololensSatelliteViewer
{
    /// <summary>
    /// Updates, renders, and presents holographic content using Direct3D.
    /// </summary>
    internal class HolographicTemplateAppMain : IDisposable
    {

#if DRAW_SAMPLE_CONTENT
        // Renders satellites as holograms positioned in world space
        // relative to user's GPS location and Earth orbit.
        private SatelliteRenderer satelliteRenderer;

        private SpatialInputHandler spatialInputHandler;
        private CompassService compassService;
#endif

        // Cached reference to device resources.
        private DeviceResources deviceResources;

        // Render loop timer.
        private readonly StepTimer timer = new StepTimer();

        // Keep track of mouse input.
        bool pointerPressed = false;
        private bool settingsDialogShowing;

        // Guard against stacking satellite info dialogs.
        private bool _infoDialogShowing = false;

        // Represents the holographic space around the user.
        HolographicSpace holographicSpace;

        // SpatialLocator that is attached to the default HolographicDisplay.
        SpatialLocator spatialLocator;

        // A stationary reference frame based on spatialLocator.
        SpatialStationaryFrameOfReference stationaryReferenceFrame;

        // Keep track of gamepads.
        private class GamepadWithButtonState
        {
            public Windows.Gaming.Input.Gamepad gamepad;
            public bool buttonAWasPressedLastFrame;
            public GamepadWithButtonState(
                Windows.Gaming.Input.Gamepad gamepad,
                bool buttonAWasPressedLastFrame)
            {
                this.gamepad = gamepad;
                this.buttonAWasPressedLastFrame = buttonAWasPressedLastFrame;
            }
        };
        List<GamepadWithButtonState> gamepads = new List<GamepadWithButtonState>();

        // Cache whether or not the HolographicCamera.Display property can be accessed.
        bool canGetHolographicDisplayForCamera = false;

        // Cache whether or not the HolographicDisplay.GetDefault() method can be called.
        bool canGetDefaultHolographicDisplay = false;

        // Cache whether or not the HolographicCameraRenderingParameters.CommitDirect3D11DepthBuffer() method can be called.
        bool canCommitDirect3D11DepthBuffer = false;

        /// <summary>
        /// Loads and initializes application assets when the application is loaded.
        /// </summary>
        /// <param name="deviceResources"></param>
        public HolographicTemplateAppMain(DeviceResources deviceResources)
        {
            this.deviceResources = deviceResources;

            // Register to be notified if the Direct3D device is lost.
            this.deviceResources.DeviceLost += this.OnDeviceLost;
            this.deviceResources.DeviceRestored += this.OnDeviceRestored;

            // If connected, a game controller can also be used for input.
            Gamepad.GamepadAdded += this.OnGamepadAdded;
            Gamepad.GamepadRemoved += this.OnGamepadRemoved;

            foreach (var gamepad in Gamepad.Gamepads)
            {
                OnGamepadAdded(null, gamepad);
            }

            canGetHolographicDisplayForCamera = Windows.Foundation.Metadata.ApiInformation.IsPropertyPresent("Windows.Graphics.Holographic.HolographicCamera", "Display");
            canGetDefaultHolographicDisplay = Windows.Foundation.Metadata.ApiInformation.IsMethodPresent("Windows.Graphics.Holographic.HolographicDisplay", "GetDefault");
            canCommitDirect3D11DepthBuffer = Windows.Foundation.Metadata.ApiInformation.IsMethodPresent("Windows.Graphics.Holographic.HolographicCameraRenderingParameters", "CommitDirect3D11DepthBuffer");
        }

        public void SetHolographicSpace(HolographicSpace holographicSpace)
        {
            this.holographicSpace = holographicSpace;

            // 
            // TODO: Add code here to initialize your content.
            // 

#if DRAW_SAMPLE_CONTENT
            // Initialize the sample hologram.
            satelliteRenderer = new SatelliteRenderer(deviceResources);

            spatialInputHandler = new SpatialInputHandler();

            // Initialize compass for heading-based dome rotation
            compassService = new CompassService();
            compassService.Initialize();
#endif

            if (canGetDefaultHolographicDisplay)
            {
                // Subscribe for notifications about changes to the state of the default HolographicDisplay 
                // and its SpatialLocator.
                HolographicSpace.IsAvailableChanged += this.OnHolographicDisplayIsAvailableChanged;
            }

            // Acquire the current state of the default HolographicDisplay and its SpatialLocator.
            OnHolographicDisplayIsAvailableChanged(null, null);

            // Respond to camera added events by creating any resources that are specific
            // to that camera, such as the back buffer render target view.
            // When we add an event handler for CameraAdded, the API layer will avoid putting
            // the new camera in new HolographicFrames until we complete the deferral we created
            // for that handler, or return from the handler without creating a deferral. This
            // allows the app to take more than one frame to finish creating resources and
            // loading assets for the new holographic camera.
            // This function should be registered before the app creates any HolographicFrames.
            holographicSpace.CameraAdded += this.OnCameraAdded;

            // Respond to camera removed events by releasing resources that were created for that
            // camera.
            // When the app receives a CameraRemoved event, it releases all references to the back
            // buffer right away. This includes render target views, Direct2D target bitmaps, and so on.
            // The app must also ensure that the back buffer is not attached as a render target, as
            // shown in DeviceResources.ReleaseResourcesForBackBuffer.
            holographicSpace.CameraRemoved += this.OnCameraRemoved;

            // Notes on spatial tracking APIs:
            // * Stationary reference frames are designed to provide a best-fit position relative to the
            //   overall space. Individual positions within that reference frame are allowed to drift slightly
            //   as the device learns more about the environment.
            // * When precise placement of individual holograms is required, a SpatialAnchor should be used to
            //   anchor the individual hologram to a position in the real world - for example, a point the user
            //   indicates to be of special interest. Anchor positions do not drift, but can be corrected; the
            //   anchor will use the corrected position starting in the next frame after the correction has
            //   occurred.
        }

        public void Dispose()
        {
#if DRAW_SAMPLE_CONTENT
            if (satelliteRenderer != null)
            {
                satelliteRenderer.Dispose();
                satelliteRenderer = null;
            }
#endif
            if (compassService != null)
            {
                compassService.Dispose();
                compassService = null;
            }
        }

        /// <summary>
        /// Updates the application state once per frame.
        /// </summary>
        public HolographicFrame Update()
        {
            // Before doing the timer update, there is some work to do per-frame
            // to maintain holographic rendering. First, we will get information
            // about the current frame.

            // The HolographicFrame has information that the app needs in order
            // to update and render the current frame. The app begins each new
            // frame by calling CreateNextFrame.
            HolographicFrame holographicFrame = holographicSpace.CreateNextFrame();

            // Get a prediction of where holographic cameras will be when this frame
            // is presented.
            HolographicFramePrediction prediction = holographicFrame.CurrentPrediction;

            // Back buffers can change from frame to frame. Validate each buffer, and recreate
            // resource views and depth buffers as needed.
            deviceResources.EnsureCameraResources(holographicFrame, prediction);

#if DRAW_SAMPLE_CONTENT
            if (stationaryReferenceFrame != null)
            {
                // Check for new input state since the last frame.
                for (int i = 0; i < gamepads.Count; ++i)
                {
                    bool buttonDownThisUpdate = (gamepads[i].gamepad.GetCurrentReading().Buttons & GamepadButtons.A) == GamepadButtons.A;
                    if (buttonDownThisUpdate && !gamepads[i].buttonAWasPressedLastFrame)
                    {
                        pointerPressed = true;
                    }
                    gamepads[i].buttonAWasPressedLastFrame = buttonDownThisUpdate;
                }

                SpatialInteractionSourceState pointerState = spatialInputHandler.CheckForInput();
                if (pointerState != null)
                {
                    pointerPressed = true;
                }

                // Always obtain the current head pose every frame so the sky-dome
                // tracks the user as they move through the room.
                // SpatialPointerPose.TryGetAtTimestamp reads directly from the
                // prediction and is always valid while positional tracking is active.
                SpatialPointerPose headPose = SpatialPointerPose.TryGetAtTimestamp(
                    stationaryReferenceFrame.CoordinateSystem, prediction.Timestamp);

                // Check for satellite tap
                if (pointerPressed && !_infoDialogShowing)
                {
                    var hitSat = satelliteRenderer.CheckSatelliteHit(headPose);
                    if (hitSat != null)
                    {
                        ShowSatelliteInfoDialog(hitSat);
                    }
                }

                pointerPressed = false;

                // Read the latest compass heading (updated on background thread by CompassService)
                float compassHeading = compassService?.CalibratedHeading ?? 0f;

                satelliteRenderer.PositionHologram(headPose);
                satelliteRenderer.SetCompassHeading(compassHeading);

                if (pointerPressed &&
                    !settingsDialogShowing &&
                    satelliteRenderer.CheckSettingsHit(headPose))
                {
                    OpenSettingsDialog();
                }

                pointerPressed = false;
            }
#endif

            timer.Tick(() =>
            {
                //
                // TODO: Update scene objects.
                //
                // Put time-based updates here. By default this code will run once per frame,
                // but if you change the StepTimer to use a fixed time step this code will
                // run as many times as needed to get to the current step.
                //

#if DRAW_SAMPLE_CONTENT
                satelliteRenderer.Update(timer);
#endif
            });

            if (!canCommitDirect3D11DepthBuffer)
            {
                // On versions of the platform that do not support the CommitDirect3D11DepthBuffer API, we can control
                // image stabilization by setting a focus point with optional plane normal and velocity.
                foreach (var cameraPose in prediction.CameraPoses)
                {
#if DRAW_SAMPLE_CONTENT
                    // The HolographicCameraRenderingParameters class provides access to set
                    // the image stabilization parameters.
                    HolographicCameraRenderingParameters renderingParameters = holographicFrame.GetRenderingParameters(cameraPose);

                    // SetFocusPoint informs the system about a specific point in your scene to
                    // prioritize for image stabilization. The focus point is set independently
                    // for each holographic camera. When setting the focus point, put it on or 
                    // near content that the user is looking at.
                    // In this example, we put the focus point at the center of the sample hologram.
                    // You can also set the relative velocity and facing of the stabilization
                    // plane using overloads of this method.
                    if (stationaryReferenceFrame != null)
                    {
                        renderingParameters.SetFocusPoint(
                            stationaryReferenceFrame.CoordinateSystem,
                            satelliteRenderer.Position
                            );
                    }
#endif
                }
            }

            // The holographic frame will be used to get up-to-date view and projection matrices and
            // to present the swap chain.
            return holographicFrame;
        }

        /// <summary>
        /// Renders the current frame to each holographic display, according to the 
        /// current application and spatial positioning state. Returns true if the 
        /// frame was rendered to at least one display.
        /// </summary>
        public bool Render(HolographicFrame holographicFrame)
        {
            // Don't try to render anything before the first Update.
            if (timer.FrameCount == 0)
            {
                return false;
            }

            //
            // TODO: Add code for pre-pass rendering here.
            //
            // Take care of any tasks that are not specific to an individual holographic
            // camera. This includes anything that doesn't need the final view or projection
            // matrix, such as lighting maps.
            //

            // Up-to-date frame predictions enhance the effectiveness of image stablization and
            // allow more accurate positioning of holograms.
            holographicFrame.UpdateCurrentPrediction();
            HolographicFramePrediction prediction = holographicFrame.CurrentPrediction;

            // Lock the set of holographic camera resources, then draw to each camera
            // in this frame.
            return deviceResources.UseHolographicCameraResources(
                (Dictionary<uint, CameraResources> cameraResourceDictionary) =>
                {
                    bool atLeastOneCameraRendered = false;

                    foreach (var cameraPose in prediction.CameraPoses)
                    {
                        // This represents the device-based resources for a HolographicCamera.
                        CameraResources cameraResources = cameraResourceDictionary[cameraPose.HolographicCamera.Id];

                        // Get the device context.
                        var context = deviceResources.D3DDeviceContext;
                        var renderTargetView = cameraResources.BackBufferRenderTargetView;
                        var depthStencilView = cameraResources.DepthStencilView;

                        // Set render targets to the current holographic camera.
                        context.OutputMerger.SetRenderTargets(depthStencilView, renderTargetView);

                        // Clear the back buffer and depth stencil view.
                        if (canGetHolographicDisplayForCamera &&
                            cameraPose.HolographicCamera.Display.IsOpaque)
                        {
                            SharpDX.Mathematics.Interop.RawColor4 cornflowerBlue = new SharpDX.Mathematics.Interop.RawColor4(0.392156899f, 0.58431375f, 0.929411829f, 1.0f);
                            context.ClearRenderTargetView(renderTargetView, cornflowerBlue);
                        }
                        else
                        {
                            SharpDX.Mathematics.Interop.RawColor4 transparent = new SharpDX.Mathematics.Interop.RawColor4(0.0f, 0.0f, 0.0f, 0.0f);
                            context.ClearRenderTargetView(renderTargetView, transparent);
                        }
                        context.ClearDepthStencilView(
                            depthStencilView,
                            SharpDX.Direct3D11.DepthStencilClearFlags.Depth | SharpDX.Direct3D11.DepthStencilClearFlags.Stencil,
                            1.0f,
                            0);

                        //
                        // TODO: Replace the sample content with your own content.
                        //
                        // Notes regarding holographic content:
                        //    * For drawing, remember that you have the potential to fill twice as many pixels
                        //      in a stereoscopic render target as compared to a non-stereoscopic render target
                        //      of the same resolution. Avoid unnecessary or repeated writes to the same pixel,
                        //      and only draw holograms that the user can see.
                        //    * To help occlude hologram geometry, you can create a depth map using geometry
                        //      data obtained via the surface mapping APIs. You can use this depth map to avoid
                        //      rendering holograms that are intended to be hidden behind tables, walls,
                        //      monitors, and so on.
                        //    * On HolographicDisplays that are transparent, black pixels will appear transparent 
                        //      to the user. On such devices, you should clear the screen to Transparent as shown 
                        //      above. You should still use alpha blending to draw semitransparent holograms. 
                        //


                        // The view and projection matrices for each holographic camera will change
                        // every frame. This function refreshes the data in the constant buffer for
                        // the holographic camera indicated by cameraPose.
                        if (stationaryReferenceFrame != null)
                        {
                            cameraResources.UpdateViewProjectionBuffer(deviceResources, cameraPose, stationaryReferenceFrame.CoordinateSystem);
                        }

                        // Attach the view/projection constant buffer for this camera to the graphics pipeline.
                        bool cameraActive = cameraResources.AttachViewProjectionBuffer(deviceResources);

#if DRAW_SAMPLE_CONTENT
                        // Only render world-locked content when positional tracking is active.
                        if (cameraActive)
                        {
                            // Draw the sample hologram.
                            satelliteRenderer.Render();

                            if (canCommitDirect3D11DepthBuffer)
                            {
                                // On versions of the platform that support the CommitDirect3D11DepthBuffer API, we can 
                                // provide the depth buffer to the system, and it will use depth information to stabilize 
                                // the image at a per-pixel level.
                                HolographicCameraRenderingParameters renderingParameters = holographicFrame.GetRenderingParameters(cameraPose);
                                SharpDX.Direct3D11.Texture2D depthBuffer = cameraResources.DepthBufferTexture2D;

                                // Direct3D interop APIs are used to provide the buffer to the WinRT API.
                                SharpDX.DXGI.Resource1 depthStencilResource = depthBuffer.QueryInterface<SharpDX.DXGI.Resource1>();
                                SharpDX.DXGI.Surface2 depthDxgiSurface = new SharpDX.DXGI.Surface2(depthStencilResource, 0);
                                IDirect3DSurface depthD3DSurface = InteropStatics.CreateDirect3DSurface(depthDxgiSurface.NativePointer);
                                if (depthD3DSurface != null)
                                {
                                    // Calling CommitDirect3D11DepthBuffer causes the system to queue Direct3D commands to 
                                    // read the depth buffer. It will then use that information to stabilize the image as
                                    // the HolographicFrame is presented.
                                    renderingParameters.CommitDirect3D11DepthBuffer(depthD3DSurface);
                                }
                            }
                        }
#endif
                        atLeastOneCameraRendered = true;
                    }

                    return atLeastOneCameraRendered;
                });
        }

        public void SaveAppState()
        {
            //
            // TODO: Insert code here to save your app state.
            //       This method is called when the app is about to suspend.
            //
            //       For example, store information in the SpatialAnchorStore.
            //
        }

        public void LoadAppState()
        {
            //
            // TODO: Insert code here to load your app state.
            //       This method is called when the app resumes.
            //
            //       For example, load information from the SpatialAnchorStore.
            //
        }

        public void OnPointerPressed()
        {
            this.pointerPressed = true;
        }

        private async void OpenSettingsDialog()
        {
            if (settingsDialogShowing)
            {
                return;
            }

            settingsDialogShowing = true;
            try
            {
                await RunLocationSettingsAsync();
            }
            catch (Exception ex)
            {
                Debug.WriteLine("[Settings] Dialog error: " + ex.Message);
            }
            finally
            {
                settingsDialogShowing = false;
            }
        }

        private async Task RunLocationSettingsAsync()
        {
            while (true)
            {
                double latitude;
                double longitude;
                bool manual = LocationOverrideStore.TryGet(out latitude, out longitude);
                bool hasFix = manual || satelliteRenderer.HasObserverFix;
                if (!manual)
                {
                    latitude = satelliteRenderer.HasObserverFix
                        ? satelliteRenderer.CurrentLatitude
                        : LocationSettingsModel.Presets[0].Latitude;
                    longitude = satelliteRenderer.HasObserverFix
                        ? satelliteRenderer.CurrentLongitude
                        : LocationSettingsModel.Presets[0].Longitude;
                }

                string status = string.Format(
                    "{0}\n{1}",
                    manual ? "Manual GPS location" : "Device GPS location",
                    LocationSettingsModel.FormatCoordinates(latitude, longitude));
                if (!hasFix)
                {
                    status = "Waiting for device GPS. Choose a preset or adjust coordinates.\n" +
                        LocationSettingsModel.FormatCoordinates(latitude, longitude);
                }

                int choice = await ShowChoiceDialogAsync(
                    status,
                    "Location Settings",
                    "Change GPS",
                    "Set North",
                    "Close");

                if (choice == 0)
                {
                    await RunLocationActionsAsync(latitude, longitude);
                }
                else if (choice == 1)
                {
                    if (compassService == null || !compassService.HasHeading)
                    {
                        await ShowChoiceDialogAsync(
                            "No compass heading is available. Try again when the compass is active.",
                            "Set North",
                            "OK");
                        continue;
                    }

                    compassService.SetCalibrationOffset(compassService.CurrentHeadingDegrees);
                    await ShowChoiceDialogAsync(
                        "North aligned to the direction you are facing. The correction is saved for future sessions.",
                        "Set North",
                        "OK");
                }
                else
                {
                    return;
                }
            }
        }

        private async Task RunLocationActionsAsync(double latitude, double longitude)
        {
            while (true)
            {
                int choice = await ShowChoiceDialogAsync(
                    "Choose how to set the observer location.",
                    "Change GPS Location",
                    "Choose city",
                    "Enter location",
                    "Use device GPS");

                if (choice == 0)
                {
                    LocationPreset preset = await ChooseLocationPresetAsync();
                    if (preset != null)
                    {
                        LocationOverrideStore.Set(preset.Latitude, preset.Longitude);
                        return;
                    }
                }
                else if (choice == 1)
                {
                    int entryChoice = await ShowChoiceDialogAsync(
                        LocationSettingsModel.FormatCoordinates(latitude, longitude),
                        "Enter Location",
                        "Nearby airports",
                        "Geohash examples",
                        "More options");
                    if (entryChoice == 0)
                    {
                        await PickNearbyAirportAsync(latitude, longitude);
                        return;
                    }
                    if (entryChoice == 1)
                    {
                        await PickGeohashExampleAsync();
                        return;
                    }
                    if (entryChoice == 2)
                    {
                        int adjustChoice = await ShowChoiceDialogAsync(
                            LocationSettingsModel.FormatCoordinates(latitude, longitude),
                            "Location Options",
                            "Adjust coordinates",
                            "Geohash examples",
                            "Back");
                        if (adjustChoice == 0)
                        {
                            var coordinates = await AdjustCoordinatesAsync(latitude, longitude);
                            LocationOverrideStore.Set(coordinates.Item1, coordinates.Item2);
                            return;
                        }
                        if (adjustChoice == 1)
                        {
                            await PickGeohashExampleAsync();
                            return;
                        }
                    }
                }
                else if (choice == 2)
                {
                    LocationOverrideStore.Clear();
                    return;
                }
                else
                {
                    return;
                }
            }
        }

        private async Task<LocationPreset> ChooseLocationPresetAsync()
        {
            int choice = await ShowChoiceDialogAsync(
                "Choose a preset location.",
                "GPS Presets",
                "Oslo",
                "London",
                "More");
            if (choice == 0) return LocationSettingsModel.Presets[0];
            if (choice == 1) return LocationSettingsModel.Presets[1];
            if (choice != 2) return null;

            choice = await ShowChoiceDialogAsync(
                "Choose a preset location.",
                "GPS Presets",
                "New York",
                "Tokyo",
                "Sydney");
            if (choice >= 0 && choice < 3)
            {
                return LocationSettingsModel.Presets[choice + 2];
            }

            return null;
        }

        private async Task PickNearbyAirportAsync(double latitude, double longitude)
        {
            AirportLocation[] airports;
            try
            {
                var catalogFile = await StorageFile.GetFileFromApplicationUriAsync(
                    new Uri("ms-appx:///Services/airports.tsv"));
                IList<string> catalogLines = await FileIO.ReadLinesAsync(catalogFile);
                airports = LocationSettingsModel.FindThreeClosestAirports(
                    LocationSettingsModel.ParseAirportCatalog(catalogLines),
                    latitude,
                    longitude);
            }
            catch (Exception exception)
            {
                await ShowChoiceDialogAsync(
                    "Airport data could not be loaded: " + exception.Message,
                    "Nearby Airports",
                    "OK");
                return;
            }

            if (airports.Length == 0)
            {
                await ShowChoiceDialogAsync(
                    "No airports were found in the airport catalog.",
                    "Nearby Airports",
                    "OK");
                return;
            }

            int index = 0;
            while (true)
            {
                AirportLocation airport = airports[index];
                string details = string.Format(
                    "{0} {1}\n{2}",
                    airport.Iata,
                    airport.Name,
                    LocationSettingsModel.FormatCoordinates(airport.Latitude, airport.Longitude));
                int choice = await ShowChoiceDialogAsync(
                    details,
                    string.Format("Nearby Airports ({0}/{1})", index + 1, airports.Length),
                    "Use airport",
                    "Next",
                    "Back");

                if (choice == 0)
                {
                    LocationOverrideStore.Set(airport.Latitude, airport.Longitude);
                    return;
                }
                if (choice == 1)
                {
                    index = (index + 1) % airports.Length;
                    continue;
                }
                return;
            }
        }

        private async Task PickGeohashExampleAsync()
        {
            string[] hashes = { "u4pruydqqvj", "u4pruyd", "u4pruy" };
            int index = 0;
            while (true)
            {
                string geohash = hashes[index];
                double latitude;
                double longitude;
                if (!LocationSettingsModel.TryDecodeGeohash(geohash, out latitude, out longitude))
                {
                    await ShowChoiceDialogAsync(
                        "The sample geohash could not be decoded.",
                        "Geohash",
                        "OK");
                    return;
                }

                int choice = await ShowChoiceDialogAsync(
                    string.Format(
                        "Geohash: {0}\n{1}",
                        geohash,
                        LocationSettingsModel.FormatCoordinates(latitude, longitude)),
                    "Geohash Examples",
                    "Use location",
                    "Next",
                    "Back");

                if (choice == 0)
                {
                    LocationOverrideStore.Set(latitude, longitude);
                    return;
                }
                if (choice == 1)
                {
                    index = (index + 1) % hashes.Length;
                    continue;
                }
                return;
            }
        }

        private async Task<Tuple<double, double>> AdjustCoordinatesAsync(double latitude, double longitude)
        {
            while (true)
            {
                int choice = await ShowChoiceDialogAsync(
                    LocationSettingsModel.FormatCoordinates(latitude, longitude),
                    "Adjust GPS",
                    "Edit latitude",
                    "Edit longitude",
                    "Done");
                if (choice == 0)
                {
                    latitude = await AdjustCoordinateAsync(latitude, true, latitude, longitude);
                }
                else if (choice == 1)
                {
                    longitude = await AdjustCoordinateAsync(longitude, false, latitude, longitude);
                }
                else
                {
                    return Tuple.Create(latitude, longitude);
                }
            }
        }

        private async Task<double> AdjustCoordinateAsync(
            double coordinate,
            bool isLatitude,
            double latitude,
            double longitude)
        {
            int stepChoice = await ShowChoiceDialogAsync(
                LocationSettingsModel.FormatCoordinates(latitude, longitude),
                isLatitude ? "Latitude step" : "Longitude step",
                "1 degree",
                "0.1 degree",
                "0.01 degree");
            if (stepChoice < 0 || stepChoice > 2)
            {
                return coordinate;
            }

            double[] steps = { 1.0, 0.1, 0.01 };
            double step = steps[stepChoice];
            while (true)
            {
                string formattedStep = LocationSettingsModel.FormatStep(step);
                int choice = await ShowChoiceDialogAsync(
                    LocationSettingsModel.FormatCoordinates(latitude, longitude),
                    isLatitude ? "Adjust latitude" : "Adjust longitude",
                    "+ " + formattedStep,
                    "- " + formattedStep,
                    "Done");
                if (choice == 2 || choice < 0)
                {
                    return coordinate;
                }

                double delta = choice == 0 ? step : -step;
                coordinate = isLatitude
                    ? LocationSettingsModel.AdjustLatitude(coordinate, delta)
                    : LocationSettingsModel.AdjustLongitude(coordinate, delta);
                if (isLatitude)
                {
                    latitude = coordinate;
                }
                else
                {
                    longitude = coordinate;
                }
            }
        }

        private static async Task<int> ShowChoiceDialogAsync(
            string content,
            string title,
            params string[] labels)
        {
            if (labels == null || labels.Length == 0 || labels.Length > 3)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(labels),
                    "MessageDialog supports between one and three commands.");
            }

            int selected = -1;
            var dialog = new MessageDialog(content, title);
            for (int i = 0; i < labels.Length; i++)
            {
                int index = i;
                dialog.Commands.Add(new UICommand(labels[i], command => selected = index));
            }

            dialog.DefaultCommandIndex = 0;
            await dialog.ShowAsync();
            return selected;
        }

        /// <summary>
        /// Notifies renderers that device resources need to be released.
        /// </summary>
        public void OnDeviceLost(Object sender, EventArgs e)
        {

#if DRAW_SAMPLE_CONTENT
            satelliteRenderer.ReleaseDeviceDependentResources();
#endif

        }

        /// <summary>
        /// Notifies renderers that device resources may now be recreated.
        /// </summary>
        public void OnDeviceRestored(Object sender, EventArgs e)
        {
#if DRAW_SAMPLE_CONTENT
            satelliteRenderer.CreateDeviceDependentResourcesAsync();
#endif
        }

        void OnLocatabilityChanged(SpatialLocator sender, Object args)
        {
            switch (sender.Locatability)
            {
                case SpatialLocatability.Unavailable:
                    // Holograms cannot be rendered.
                    {
                        String message = "Warning! Positional tracking is " + sender.Locatability + ".";
                        Debug.WriteLine(message);
                    }
                    break;

                // In the following three cases, it is still possible to place holograms using a
                // SpatialLocatorAttachedFrameOfReference.
                case SpatialLocatability.PositionalTrackingActivating:
                // The system is preparing to use positional tracking.

                case SpatialLocatability.OrientationOnly:
                // Positional tracking has not been activated.

                case SpatialLocatability.PositionalTrackingInhibited:
                    // Positional tracking is temporarily inhibited. User action may be required
                    // in order to restore positional tracking.
                    break;

                case SpatialLocatability.PositionalTrackingActive:
                    // Positional tracking is active. World-locked content can be rendered.
                    break;
            }
        }

        public void OnCameraAdded(
            HolographicSpace sender,
            HolographicSpaceCameraAddedEventArgs args
            )
        {
            Deferral deferral = args.GetDeferral();
            HolographicCamera holographicCamera = args.Camera;

            Task task1 = new Task(() =>
            {
                //
                // TODO: Allocate resources for the new camera and load any content specific to
                //       that camera. Note that the render target size (in pixels) is a property
                //       of the HolographicCamera object, and can be used to create off-screen
                //       render targets that match the resolution of the HolographicCamera.
                //

                // Create device-based resources for the holographic camera and add it to the list of
                // cameras used for updates and rendering. Notes:
                //   * Since this function may be called at any time, the AddHolographicCamera function
                //     waits until it can get a lock on the set of holographic camera resources before
                //     adding the new camera. At 60 frames per second this wait should not take long.
                //   * A subsequent Update will take the back buffer from the RenderingParameters of this
                //     camera's CameraPose and use it to create the ID3D11RenderTargetView for this camera.
                //     Content can then be rendered for the HolographicCamera.
                deviceResources.AddHolographicCamera(holographicCamera);

                // Holographic frame predictions will not include any information about this camera until
                // the deferral is completed.
                deferral.Complete();
            });
            task1.Start();
        }

        public void OnCameraRemoved(
            HolographicSpace sender,
            HolographicSpaceCameraRemovedEventArgs args
            )
        {
            Task task2 = new Task(() =>
            {
                //
                // TODO: Asynchronously unload or deactivate content resources (not back buffer 
                //       resources) that are specific only to the camera that was removed.
                //
            });
            task2.Start();

            // Before letting this callback return, ensure that all references to the back buffer 
            // are released.
            // Since this function may be called at any time, the RemoveHolographicCamera function
            // waits until it can get a lock on the set of holographic camera resources before
            // deallocating resources for this camera. At 60 frames per second this wait should
            // not take long.
            deviceResources.RemoveHolographicCamera(args.Camera);
        }

        public void OnGamepadAdded(Object o, Gamepad args)
        {
            foreach (var gamepadWithButtonState in gamepads)
            {
                if (args == gamepadWithButtonState.gamepad)
                {
                    // This gamepad is already in the list.
                    return;
                }
            }

            gamepads.Add(new GamepadWithButtonState(args, false));
        }

        public void OnGamepadRemoved(Object o, Gamepad args)
        {
            foreach (var gamepadWithButtonState in gamepads)
            {
                if (args == gamepadWithButtonState.gamepad)
                {
                    // This gamepad is in the list; remove it.
                    gamepads.Remove(gamepadWithButtonState);
                    return;
                }
            }
        }

        void OnHolographicDisplayIsAvailableChanged(Object o, Object args)
        {
            // Get the spatial locator for the default HolographicDisplay, if one is available.
            SpatialLocator spatialLocator = null;
            if (canGetDefaultHolographicDisplay)
            {
                HolographicDisplay defaultHolographicDisplay = HolographicDisplay.GetDefault();
                if (defaultHolographicDisplay != null)
                {
                    spatialLocator = defaultHolographicDisplay.SpatialLocator;
                }
            }
            else
            {
                spatialLocator = SpatialLocator.GetDefault();
            }

            if (this.spatialLocator != spatialLocator)
            {
                // If the spatial locator is disconnected or replaced, we should discard any state that was
                // based on it.
                if (this.spatialLocator != null)
                {
                    this.spatialLocator.LocatabilityChanged -= this.OnLocatabilityChanged;
                    this.spatialLocator = null;
                }

                this.stationaryReferenceFrame = null;

                if (spatialLocator != null)
                {
                    // Use the SpatialLocator from the default HolographicDisplay to track the motion of the device.
                    this.spatialLocator = spatialLocator;

                    // Respond to changes in the positional tracking state.
                    this.spatialLocator.LocatabilityChanged += this.OnLocatabilityChanged;

                    // The simplest way to render world-locked holograms is to create a stationary reference frame
                    // based on a SpatialLocator. This is roughly analogous to creating a "world" coordinate system
                    // with the origin placed at the device's position as the app is launched.
                    this.stationaryReferenceFrame = this.spatialLocator.CreateStationaryFrameOfReferenceAtCurrentLocation();
                }
            }
        }

        private async void ShowSatelliteInfoDialog(Satellite sat)
        {
            if (_infoDialogShowing) return;
            _infoDialogShowing = true;

            try
            {
                double altKm = sat.AltitudeKm;
                double rangeKm = sat.RangeKm;
                double velKmS = sat.VelocityKmPerSec;

                var detail = string.Format(
                    "Name: {0}\n" +
                    "NORAD ID: {1}\n" +
                    "Azimuth: {2:F1}°\n" +
                    "Elevation: {3:F1}°\n" +
                    "Range: {4:F0} km\n" +
                    "Altitude: {5:F0} km\n" +
                    "Velocity: {6:F2} km/s",
                    sat.Name,
                    sat.NoradId,
                    sat.Azimuth,
                    sat.Elevation,
                    rangeKm,
                    altKm,
                    velKmS);

                var dialog = new MessageDialog(detail, "Satellite Details");
                dialog.Commands.Add(new UICommand("Close", cmd => { }));
                dialog.DefaultCommandIndex = 0;
                dialog.CancelCommandIndex = 0;
                await dialog.ShowAsync();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[SatelliteInfo] Dialog error: {ex.Message}");
            }
            finally
            {
                _infoDialogShowing = false;
            }
        }
    }
}