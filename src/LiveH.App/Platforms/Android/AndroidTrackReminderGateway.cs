using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.OS;
using LiveH.Contracts.Track;
using Microsoft.Maui.ApplicationModel;
using System.Runtime.Versioning;

[assembly: UsesPermission(global::Android.Manifest.Permission.PostNotifications)]

namespace LiveH.App.Platforms.Android;

// Android boundary only. No Android class leaks into the TrackOS domain library.
public sealed class AndroidTrackReminderGateway : ITrackReminderGateway
{
    private const int RequestCode = 6120;
    private static readonly PendingIntentFlags Flags = PendingIntentFlags.UpdateCurrent | PendingIntentFlags.Immutable;

    internal static PendingIntent? Pending(Context context, Guid sessionId)
    {
        var intent = new Intent(context, typeof(TrackCheckReceiver));
        intent.SetAction("com.liveh.os.TRACK_CHECK");
        intent.PutExtra("track_session_id", sessionId.ToString("D"));
        return PendingIntent.GetBroadcast(context, RequestCode, intent, Flags);
    }

    public Task ScheduleAsync(Guid sessionId, DateTimeOffset dueAtUtc)
    {
        var context = global::Android.App.Application.Context;
        var manager = (AlarmManager?)context.GetSystemService(Context.AlarmService);
        var pending = Pending(context, sessionId);
        if (pending is null || manager is null) return Task.CompletedTask;
        // Not an exact alarm: Android may delay delivery in Doze mode.
        // TrackRules, NOT this reminder, is the authority for time accounting.
        manager.SetAndAllowWhileIdle(AlarmType.RtcWakeup, dueAtUtc.ToUnixTimeMilliseconds(), pending);
        if (OperatingSystem.IsAndroidVersionAtLeast(33))
            RequestNotificationPermission(context);
        return Task.CompletedTask;
    }

    [SupportedOSPlatform("android33.0")]
    private static void RequestNotificationPermission(Context context)
    {
        if (context.CheckSelfPermission(global::Android.Manifest.Permission.PostNotifications) == Permission.Granted)
            return;
        MainThread.BeginInvokeOnMainThread(() =>
        {
            var activity = Platform.CurrentActivity;
            if (activity is not null &&
                activity.CheckSelfPermission(global::Android.Manifest.Permission.PostNotifications) != Permission.Granted)
                activity.RequestPermissions(new[] { global::Android.Manifest.Permission.PostNotifications }, 2301);
        });
    }

    public Task CancelAsync()
    {
        var context = global::Android.App.Application.Context;
        var manager = (AlarmManager?)context.GetSystemService(Context.AlarmService);
        var pending = Pending(context, Guid.Empty);
        if (pending is not null) manager?.Cancel(pending);
        pending?.Cancel();
        return Task.CompletedTask;
    }
}

[BroadcastReceiver(Enabled = true, Exported = false)]
public sealed class TrackCheckReceiver : BroadcastReceiver
{
    private const string ChannelId = "liveh-track-activity-v1";

    public override void OnReceive(Context? context, Intent? intent)
    {
        // Android invokes OnReceive on the main thread. Never synchronously wait on
        // ITrackService here: async persistence can depend on that very same thread.
        if (context is null || intent?.Action != "com.liveh.os.TRACK_CHECK") return;
        if (!Guid.TryParse(intent.GetStringExtra("track_session_id"), out var sessionId)) return;

        // ApplicationContext outlives the individual BroadcastReceiver callback.
        var applicationContext = context.ApplicationContext ?? context;
        var pendingResult = GoAsync();
        if (pendingResult is null) return;

        _ = Task.Run(async () =>
        {
            try
            {
                // Keep broadcasts short (<10 s) even if the session store is busy.
                using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(4));
                var track = Microsoft.Maui.IPlatformApplication.Current?.Services
                    .GetService(typeof(ITrackService)) as ITrackService;
                if (track is null) return;

                // Await away from Android's UI thread. An obsolete alarm must never
                // display a prompt for an already closed or paused session.
                var current = await track.CurrentAsync(deadline.Token).ConfigureAwait(false);
                if (current?.Session.Id != sessionId || !current.NeedsConfirmation ||
                    current.ReplyUntilUtc <= DateTimeOffset.UtcNow)
                    return;

                ShowNotification(applicationContext);
            }
            catch (global::System.OperationCanceledException)
            {
                // Non-critical reminder expired. TrackOS domain rules still cap time.
            }
            catch (Exception)
            {
                // Best-effort notification; never crash the app because of a reminder.
            }
            finally
            {
                // Required after GoAsync. Without Finish(), the broadcast can ANR.
                try { pendingResult.Finish(); }
                finally { pendingResult.Dispose(); }
            }
        });
    }

    private static void ShowNotification(Context context)
    {
        var manager = (NotificationManager?)context.GetSystemService(Context.NotificationService);
        if (manager is null) return;
        // Split the API levels behind explicitly annotated methods. On Android 26+
        // use the mandatory notification channel; older Android keeps its legacy API.
        // No compiler-warning suppression and no callback blocking.
        if (OperatingSystem.IsAndroidVersionAtLeast(26))
            ShowModernNotification(context, manager);
        else
            ShowLegacyNotification(context, manager);
    }

    [SupportedOSPlatform("android26.0")]
    private static void ShowModernNotification(Context context, NotificationManager manager)
    {
        using var channel = new NotificationChannel(ChannelId,
            "TrackOS · kontrola pracy", NotificationImportance.High)
        {
            Description = "Potwierdzenie dalszej pracy nad misją"
        };
        manager.CreateNotificationChannel(channel);
        using var builder = new Notification.Builder(context, ChannelId);
        PostNotification(context, manager, builder);
    }

    [UnsupportedOSPlatform("android26.0")]
    private static void ShowLegacyNotification(Context context, NotificationManager manager)
    {
        using var builder = new Notification.Builder(context);
        PostNotification(context, manager, builder);
    }

    private static void PostNotification(Context context, NotificationManager manager,
        Notification.Builder builder)
    {
        var open = new Intent(context, typeof(LiveH.App.MainActivity));
        open.SetAction("com.liveh.os.OPEN_TRACK");
        open.AddFlags(ActivityFlags.SingleTop | ActivityFlags.ClearTop);
        var tap = PendingIntent.GetActivity(context, 6121, open,
            PendingIntentFlags.UpdateCurrent | PendingIntentFlags.Immutable);
        builder.SetSmallIcon(global::Android.Resource.Drawable.IcDialogInfo)
            .SetContentTitle("TrackOS · nadal pracujesz?")
            .SetContentText("Otwórz TrackOS i potwierdź w 5 minut. Bez odpowiedzi licznik zatrzyma się.")
            .SetAutoCancel(true)
            .SetContentIntent(tap);
        manager.Notify(6120, builder.Build());
    }
}
