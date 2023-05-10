using Esri.ArcGISRuntime.Geometry;
using Esri.ArcGISRuntime.Mapping;
using Esri.ArcGISRuntime.Ogc;
using Esri.ArcGISRuntime.Symbology;
using Esri.ArcGISRuntime.UI;
using Esri.ArcGISRuntime.UI.Controls;
using Microsoft.Maui;
using Microsoft.Maui.Controls;
using SkiaSharp;
using System;
using System.Runtime.InteropServices;
using Map = Esri.ArcGISRuntime.Mapping.Map;

namespace FlightpathandCoverageImage
{
    public partial class MainPage : ContentPage
    {
        public MainPage()
        {
            InitializeComponent();
            Esri.ArcGISRuntime.ArcGISRuntimeEnvironment.ApiKey = "AAPKeca6f69f2e5e4342bb4e57af0e62de84qUM-jASpAEwUq5ATo_OurYpHh728so-cmdj_wak6ngy31s0mNzFgbwR-6IV5cIpJ";
            // Initialize the MapView with a basemap.
            MyMapView.Map = new Map(BasemapStyle.ArcGISImageryStandard);
        }

        private Random random = new Random();

        private async void RandomLocationButton_Clicked(object sender, EventArgs e)
        {
            Console.WriteLine("Button clicked.");

            double latitude = random.NextDouble() * 180 - 90;
            double longitude = random.NextDouble() * 360 - 180;

            double minScaleLog = Math.Log10(50000);  
            double maxScaleLog = Math.Log10(50000000);  
            double scaleLog = random.NextDouble() * (maxScaleLog - minScaleLog) + minScaleLog;
            double scale = Math.Pow(10, scaleLog);  // Convert logarithmic scale back to regular scale

            Console.WriteLine($"Chosen scale: {scale}");

            // Create a MapPoint with the random location.
            MapPoint mapPoint = new MapPoint(longitude, latitude, SpatialReferences.Wgs84);

            // Set the MapView's viewpoint to a large scale to zoom out.
            Viewpoint viewpointZoomOut = new Viewpoint(mapPoint, 50000000);
            await MyMapView.SetViewpointAsync(viewpointZoomOut, TimeSpan.FromSeconds(1));

            // Create a Viewpoint with the MapPoint and the random scale.
            Viewpoint viewpointZoomIn = new Viewpoint(mapPoint, scale);

            // Set the MapView's viewpoint to the new random Viewpoint to zoom in.
            await MyMapView.SetViewpointAsync(viewpointZoomIn, TimeSpan.FromSeconds(1));
        }

        private async void SaveImageButton_Clicked(object sender, EventArgs e)
        {
            RuntimeImage runtimeImage = await MyMapView.ExportImageAsync();
            var imageDataStream = await runtimeImage.GetRawBufferAsync();

            byte[] imageData = new byte[imageDataStream.Length];
            await imageDataStream.ReadAsync(imageData, 0, imageData.Length);

            var imageWidth = runtimeImage.Width;
            var imageHeight = runtimeImage.Height;

            // Allocate unmanaged memory for the image data
            IntPtr imageDataPtr = Marshal.AllocHGlobal(imageData.Length);
            Marshal.Copy(imageData, 0, imageDataPtr, imageData.Length);

            // Creating SKImageInfo and SKBitmap
            var skiaImageInfo = new SKImageInfo(imageWidth, imageHeight, SKColorType.Bgra8888);
            var skBitmap = new SKBitmap(skiaImageInfo);
            skBitmap.InstallPixels(skiaImageInfo, imageDataPtr);

            // Create SKImage from SKBitmap
            var skiaImage = SKImage.FromBitmap(skBitmap);
            var outputPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyPictures), "MyImage.png");

            // Save SKImage to a file
            using (var stream = File.OpenWrite(outputPath))
            {
                skiaImage.Encode(SKEncodedImageFormat.Png, 100).SaveTo(stream);
            }

            // Free the unmanaged memory
            Marshal.FreeHGlobal(imageDataPtr);

            Console.WriteLine($"Saved image to: {outputPath}");

            // Show a dialog indicating that the image was saved successfully.
            await DisplayAlert("Image Saved", $"Image was saved to: {outputPath}", "OK");
        }
        private KmlLayer flightPathLayer;
        private KmlLayer coverageLayer;

        private async void LoadPathButton_Clicked(object sender, EventArgs e)
        {
            var fileResult = await PickKmlFile();
            if (fileResult != null)
            {
                var kmlDataset = await LoadKmlDataset(fileResult);
                flightPathLayer = new KmlLayer(kmlDataset);
                MyMapView.Map.OperationalLayers.Add(flightPathLayer);

                await ZoomToLayer(flightPathLayer, true);
            }
        }

        private async void LoadCoverageButton_Clicked(object sender, EventArgs e)
        {
            var fileResult = await PickKmlFile();
            if (fileResult != null)
            {
                var kmlDataset = await LoadKmlDataset(fileResult);
                coverageLayer = new KmlLayer(kmlDataset);
                MyMapView.Map.OperationalLayers.Add(coverageLayer);

                await ZoomToLayer(coverageLayer, false);
            }
        }

        private async Task ZoomToLayer(KmlLayer layer, bool isPathLayer)
        {
            double zoomOutScale = isPathLayer ? 50000000 : 2000000; // Adjust the zoom out scale here

            // First, set the MapView's viewpoint to a large scale to zoom out.
            Viewpoint viewpointZoomOut = new Viewpoint(layer.FullExtent.GetCenter(), zoomOutScale);
            await MyMapView.SetViewpointAsync(viewpointZoomOut, TimeSpan.FromSeconds(1));

            // Then, set the MapView's viewpoint to the layer's extent to zoom in.
            Viewpoint viewpointZoomIn = new Viewpoint(layer.FullExtent);
            await MyMapView.SetViewpointAsync(viewpointZoomIn, TimeSpan.FromSeconds(1));
        }
        private async Task<FileResult> PickKmlFile()
        {
            var kmlFileType = new FilePickerFileType(new Dictionary<DevicePlatform, IEnumerable<string>>
            {
                { DevicePlatform.iOS, new[] { "org.opengis.kml" } },
                { DevicePlatform.Android, new[] { "application/vnd.google-earth.kml+xml" } },
                { DevicePlatform.WinUI, new[] { ".kml" } },
                { DevicePlatform.macOS, new[] { "kml" } }
            });

            var options = new PickOptions
            {
                PickerTitle = "Please select a KML file",
                FileTypes = kmlFileType
            };

            return await FilePicker.PickAsync(options);
        }

        private async Task<KmlDataset> LoadKmlDataset(FileResult fileResult)
        {
            var tempFilePath = Path.Combine(FileSystem.CacheDirectory, fileResult.FileName);
            using (var fileStream = File.Create(tempFilePath))
            {
                using var stream = await fileResult.OpenReadAsync();
                await stream.CopyToAsync(fileStream);
            }

            var tempFileUri = new Uri(tempFilePath);
            var kmlDataset = new KmlDataset(tempFileUri);
            await kmlDataset.LoadAsync();
            return kmlDataset;
        }

        private async Task ZoomToLayerAsync(KmlLayer kmlLayer)
        {
            if (kmlLayer.FullExtent != null)
            {
                await MyMapView.SetViewpointGeometryAsync(kmlLayer.FullExtent, 50);
            }
        }
    }
}