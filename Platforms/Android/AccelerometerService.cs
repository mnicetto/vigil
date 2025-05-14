using Android.App;
using Android.Content;
using Android.OS;
using Microsoft.Maui.Devices.Sensors;
using System.Text;
using System.Text.Json;
using System.Threading;
using Timer = System.Threading.Timer; // Alias to avoid conflict

namespace Vigil.Platforms.Android
{
    [Service(Name = "com.vigil.accelerometerservice", // Unique name for the service
             ForegroundServiceType = global::Android.Content.PM.ForegroundService.TypeDataSync,
             Exported = false)] // Exported = false as it's started by the app itself
    public class AccelerometerService : Service
    {
        private const string NotificationChannelId = "AccelerometerServiceChannel";
        private const int NotificationId = 1001; // Unique ID for the notification
        private const string ApiUrl = "http://217.18.123.143/testAcc.php";
        private const int DataSendIntervalMilliseconds = 30 * 1000; // 30 seconds

        private Timer _timer;
        private AccelerometerData _lastAccelerometerData;
        private static readonly HttpClient _httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(20) };
        private bool _isAccelerometerMonitoring;
        private readonly object _lock = new object(); // For thread-safe access to _lastAccelerometerData

        public override IBinder OnBind(Intent intent)
        {
            return null; // We don't provide binding, so return null
        }

        public override void OnCreate()
        {
            base.OnCreate();
            CreateNotificationChannel();
            System.Diagnostics.Debug.WriteLine("VigilService: OnCreate");
        }

        public override StartCommandResult OnStartCommand(Intent intent, StartCommandFlags flags, int startId)
        {
            System.Diagnostics.Debug.WriteLine("VigilService: OnStartCommand");
            StartForegroundServiceNotification();
            StartAccelerometerMonitoring();

            // Start the timer to send data periodically
            _timer = new Timer(SendDataCallback, null, TimeSpan.Zero, TimeSpan.FromMilliseconds(DataSendIntervalMilliseconds));

            return StartCommandResult.Sticky; // If the service is killed, the system will try to restart it
        }

        private void StartAccelerometerMonitoring()
        {
            if (!Accelerometer.Default.IsSupported)
            {
                System.Diagnostics.Debug.WriteLine("VigilService: Accelerometer not supported on this device.");
                StopSelf(); // Stop the service if accelerometer is not supported
                return;
            }

            if (_isAccelerometerMonitoring) return;

            try
            {
                Accelerometer.Default.ReadingChanged += OnAccelerometerReadingChanged;
                Accelerometer.Default.Start(SensorSpeed.UI); // Adjust speed as needed (UI, Normal, Game, Fastest)
                _isAccelerometerMonitoring = true;
                System.Diagnostics.Debug.WriteLine("VigilService: Accelerometer monitoring started.");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"VigilService: Error starting accelerometer: {ex.Message}");
                StopSelf();
            }
        }

        private void StopAccelerometerMonitoring()
        {
            if (!_isAccelerometerMonitoring) return;

            try
            {
                Accelerometer.Default.ReadingChanged -= OnAccelerometerReadingChanged;
                Accelerometer.Default.Stop();
                _isAccelerometerMonitoring = false;
                System.Diagnostics.Debug.WriteLine("VigilService: Accelerometer monitoring stopped.");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"VigilService: Error stopping accelerometer: {ex.Message}");
            }
        }

        void OnAccelerometerReadingChanged(object sender, AccelerometerChangedEventArgs e)
        {
            lock (_lock)
            {
                _lastAccelerometerData = e.Reading;
            }
        }

        private async void SendDataCallback(object state)
        {
            AccelerometerData dataToSend;
            lock (_lock)
            {
                if (_lastAccelerometerData == null)
                {
                    System.Diagnostics.Debug.WriteLine("VigilService: No accelerometer data to send yet.");
                    return;
                }
                dataToSend = _lastAccelerometerData; // Create a copy of the struct value
            }

            try
            {
                var payload = new
                {
                    x = dataToSend.Acceleration.X,
                    y = dataToSend.Acceleration.Y,
                    z = dataToSend.Acceleration.Z
                };
                var jsonPayload = JsonSerializer.Serialize(payload);
                var content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");

                System.Diagnostics.Debug.WriteLine($"VigilService: Sending data: {jsonPayload}");
                HttpResponseMessage response = await _httpClient.PostAsync(ApiUrl, content);

                if (response.IsSuccessStatusCode)
                {
                    var responseContent = await response.Content.ReadAsStringAsync();
                    System.Diagnostics.Debug.WriteLine($"VigilService: Data sent successfully. Response: {responseContent}");
                }
                else
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    System.Diagnostics.Debug.WriteLine($"VigilService: Error sending data. Status: {response.StatusCode}. Response: {errorContent}");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"VigilService: Exception during data send: {ex.Message}");
            }
        }

        public override void OnDestroy()
        {
            System.Diagnostics.Debug.WriteLine("VigilService: OnDestroy");
            _timer?.Change(Timeout.Infinite, 0); // Stop the timer
            _timer?.Dispose();
            _timer = null;

            StopAccelerometerMonitoring();
            StopForeground(true); // Remove notification and stop foreground state
            base.OnDestroy();
        }

        private void StartForegroundServiceNotification()
        {
            var notificationIntent = new Intent(this, typeof(Vigil.MainActivity)); // Assuming MainActivity is your launch activity
            var pendingIntentFlags = PendingIntentFlags.UpdateCurrent | (OperatingSystem.IsAndroidVersionAtLeast(31) ? PendingIntentFlags.Immutable : 0);
            var pendingIntent = PendingIntent.GetActivity(this, 0, notificationIntent, pendingIntentFlags);

            var notificationBuilder = new Notification.Builder(this, NotificationChannelId)
                .SetContentTitle("Vigil Service Active")
                .SetContentText("Collecting accelerometer data.")
                .SetSmallIcon(Resource.Mipmap.appicon) // Ensure appicon.png is in Resources/mipmap folders
                .SetContentIntent(pendingIntent)
                .SetOngoing(true); // Makes the notification non-dismissible

            StartForeground(NotificationId, notificationBuilder.Build());
        }

        private void CreateNotificationChannel()
        {
            if (!OperatingSystem.IsAndroidVersionAtLeast(26)) return; // Channels are for Oreo (API 26) and above

            var channelName = "Vigil Accelerometer Service";
            var channelDescription = "Notifications for Vigil background accelerometer monitoring.";
            var channel = new NotificationChannel(NotificationChannelId, channelName, NotificationImportance.Default)
            {
                Description = channelDescription
            };

            var notificationManager = (NotificationManager)GetSystemService(NotificationService);
            notificationManager.CreateNotificationChannel(channel);
            System.Diagnostics.Debug.WriteLine("VigilService: Notification channel created.");
        }
    }
}