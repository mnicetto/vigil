using Microsoft.Maui.ApplicationModel; // Required for Permissions

namespace Vigil;

public partial class MainPage : ContentPage
{
    private bool _isServiceRunning = false;

    public MainPage()
    {
        InitializeComponent();
    }

    private async void OnStartStopButtonClicked(object sender, EventArgs e)
    {
        if (!_isServiceRunning)
        {
            if (await RequestNotificationsPermissionIfNeeded())
            {
                StartAccelerometerService();
                StartStopButton.Text = "Stop";
                _isServiceRunning = true;
            }
            else
            {
                await DisplayAlert("Permission Denied", "Notification permission is required to run the service in the background.", "OK");
            }
        }
        else
        {
            StopAccelerometerService();
            StartStopButton.Text = "Start";
            _isServiceRunning = false;
        }
    }

    private async Task<bool> RequestNotificationsPermissionIfNeeded()
    {
#if ANDROID
        if (OperatingSystem.IsAndroidVersionAtLeast(33)) // Android 13 (API 33)
        {
            var status = await Permissions.CheckStatusAsync<Permissions.PostNotifications>();
            if (status == PermissionStatus.Granted)
                return true;

            status = await Permissions.RequestAsync<Permissions.PostNotifications>();
            return status == PermissionStatus.Granted;
        }
#endif
        return true; // Permission not required for older Android versions or other platforms
    }

    private void StartAccelerometerService()
    {
#if ANDROID
        var context = Android.App.Application.Context;
        var intent = new Android.Content.Intent(context, typeof(Vigil.Platforms.Android.AccelerometerService));

        if (OperatingSystem.IsAndroidVersionAtLeast(26)) // Android 8.0 (Oreo) for StartForegroundService
        {
            context.StartForegroundService(intent);
        }
        else
        {
            context.StartService(intent);
        }
#endif
    }

    private void StopAccelerometerService()
    {
#if ANDROID
        var context = Android.App.Application.Context;
        var intent = new Android.Content.Intent(context, typeof(Vigil.Platforms.Android.AccelerometerService));
        context.StopService(intent);
#endif
    }
}