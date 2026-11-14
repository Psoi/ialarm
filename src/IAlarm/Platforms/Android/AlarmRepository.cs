using Android.Content;
using IAlarm.Core;

namespace IAlarm.Services;

/// <summary>
/// The one copy of the alarm list in the process, shared by the screens, the alarm receiver and the ringing service.
/// Every change is saved and re-arms the system alarm. The file lives in device-protected storage so alarms can
/// ring after a restart before the phone is unlocked.
/// </summary>
public sealed class AlarmRepository
{
    public const string FileName = "alarms.json";

    static readonly Lock gate = new();
    static AlarmRepository? instance;

    readonly Context context;
    readonly AlarmStore store;
    readonly List<Alarm> alarms;

    /// <summary>Raised after any change, on whatever thread made it.</summary>
    public static event Action? Changed;

    AlarmRepository(Context context)
    {
        this.context = context;
        var dir = context.CreateDeviceProtectedStorageContext()!.FilesDir!.AbsolutePath;
        store = new AlarmStore(Path.Combine(dir, FileName));
        alarms = store.Load().Alarms;
    }

    public static AlarmRepository Get(Context context)
    {
        lock (gate)
            return instance ??= new AlarmRepository(context.ApplicationContext ?? context);
    }

    public IReadOnlyList<Alarm> All
    {
        get
        {
            lock (gate)
                return [.. alarms];
        }
    }

    public Alarm? Find(string id)
    {
        lock (gate)
            return alarms.Find(a => a.Id == id);
    }

    /// <summary>Applies a change from <see cref="AlarmBook"/>, saves, and re-arms the next alarm.</summary>
    public T Change<T>(Func<List<Alarm>, T> change)
    {
        T result;
        lock (gate)
        {
            result = change(alarms);
            store.Save(alarms);
            AlarmScheduler.Arm(context, alarms);
        }
        Changed?.Invoke();
        return result;
    }

    public void Rearm()
    {
        lock (gate)
            AlarmScheduler.Arm(context, alarms);
    }
}
