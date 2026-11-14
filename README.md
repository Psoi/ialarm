# iAlarm

A small offline alarm clock and stopwatch for Android, written in C# (.NET 9 MAUI), with the look of an old green
digital watch. It has no account and no sync, and the app has no internet permission, so nothing leaves the phone.

## Features

- **Two tabs**: Alarm and Stopwatch.
- **Alarms**:
  - The Alarm tab shows the current time in big segments (the colon blinks), the date, and how long until the next
    alarm.
  - **+** adds an alarm: pick a date, a time and an optional label. A new alarm starts about an hour from now.
  - Tap an alarm to edit its date, time or label. Saving turns it on.
  - The switch turns an alarm off or on without deleting it. An alarm whose time has passed can't be turned on; edit
    it to pick a new time.
  - To delete, swipe an alarm to the left, or tap it and use **Delete**. Both ask first.
  - Alarms are listed in time order. Each shows how long until it rings, "snoozed until", "off" or "passed".
  - Alarms are one-off (a date and a time). After ringing, an alarm stays in the list, turned off.
- **When an alarm rings**:
  - The alarm sound plays on the **alarm** volume, so it rings even with the phone on silent. If the alarm volume is
    all the way down, it's raised to two thirds. If no alarm sound can be played, it falls back to a digital beep.
  - The phone vibrates, and a full-screen alarm screen appears, over the lock screen too, with **Snooze** (10 minutes)
    and **Dismiss**. Back doesn't close it. The notification has the same two buttons.
  - Unanswered, it stops after 10 minutes and leaves a "missed" notification.
  - It rings when the app is closed and when the phone is locked or asleep. The status bar shows the alarm icon while
    an alarm is set.
  - After a restart, alarms are set again, even before the phone is first unlocked. An alarm missed while the phone
    was off rings as soon as it's back on, if it's less than 30 minutes late; otherwise it's turned off with a
    "missed" notification. Changing the clock or time zone keeps alarms at the same clock time.
- **Setup warnings**: the Alarm tab lists phone settings that would stop alarms from working, each with **FIX**:
  notifications (Android 13+), exact alarms, full-screen alerts (Android 14+), and battery optimization. On first
  launch Android asks for the notification permission.
- **Stopwatch**:
  - **Start**, **Stop** (pause) and **Reset**. Shows `mm:ss.cc`, and `hh:mm:ss.cc` from one hour on.
  - It keeps counting with the app in the background or the screen off, and stays right after rotating or switching
    apps: the time is kept as timestamps, not as a running counter.
  - While it runs, an ongoing notification counts along, with **Stop** and **Reset**. When paused, the notification
    shows the time with **Start** and **Reset**.
  - A restart of the phone pauses a running stopwatch at the time it had reached before.
- **Look**: 7-segment digits with a slight glow, and the unlit segments faintly visible like a real LCD.
  - Dark theme: black with bright green digits.
  - Light theme: pale green-grey LCD with dark green digits.
  - Follows the phone's dark/light setting.

## Install

The phone needs Android 8.0 or later on a 64-bit ARM processor (nearly every phone from the last several years).

1. Build the app (see below).
2. Copy `src/IAlarm/bin/Release/net9.0-android/com.personal.ialarm-Signed.apk` to the phone and open it, allowing
   installs from that source if asked. Or, with the phone connected over USB with USB debugging on:

```bash
adb install -r src/IAlarm/bin/Release/net9.0-android/com.personal.ialarm-Signed.apk
```

Installing a new build over the old one keeps your alarms.

After installing, open the app once and clear any warnings on the Alarm tab. On Xiaomi, Huawei, OnePlus and some
Samsung phones, also allow iAlarm to **autostart** / run in the background in the phone's own battery or app
settings; these phones can block alarms from apps that aren't the built-in clock.

## Build

Needs the .NET 9 SDK with the `android` workload (`dotnet workload install android`).

Run the tests:

```bash
dotnet test tests/IAlarm.Core.Tests
```

Build the release APK:

```bash
dotnet build src/IAlarm -c Release
```

Build and run on a connected phone:

```bash
dotnet build src/IAlarm -t:Run
```

The release build is set up to keep the app small: 64-bit ARM only (it won't run on x86 emulators), full .NET
trimming, and R8 shrinking of the Android Java code. Settings are in `src/IAlarm/IAlarm.csproj`.

## Project layout

| Path | What |
| --- | --- |
| `src/IAlarm.Core` | Plain .NET, no Android: alarm list changes (add, edit, toggle, due, snooze, missed), stopwatch state, time formats, 7-segment masks, JSON storage |
| `src/IAlarm` | The MAUI Android app: tabs, pages, view models, the 7-segment drawing |
| `src/IAlarm/Platforms/Android` | Alarm scheduling, boot/clock receivers, the ringing service and full-screen ringing screen, stopwatch notification, setup checks |
| `tests/IAlarm.Core.Tests` | xUnit tests for the core |

### How an alarm rings

1. Every change to the alarm list goes through `AlarmRepository`, which saves `alarms.json` and sets one system
   alarm (`AlarmManager.SetAlarmClock`) for the earliest alarm that is on.
2. When it goes off, `AlarmReceiver` turns the due alarms off and starts `AlarmRingService`, a foreground service
   that plays the sound, vibrates and shows `AlarmActivity` (through a full-screen notification when Android doesn't
   allow showing it directly).
3. Snooze turns the alarms back on for 10 minutes later; Dismiss leaves them off. Either way the next alarm is set.
4. `RescheduleReceiver` sets the system alarm again after a restart, an app update, or a clock or time zone change.

## Data

- Alarms are stored in `alarms.json` in the app's device-protected storage, so they can ring after a restart
  before the phone is unlocked. Writes go through a temp file, so a crash can't leave half a file. An unreadable file
  is kept as `alarms.json.corrupt-<time>` instead of being overwritten.
- The stopwatch is kept in the app's preferences.
- Uninstalling the app deletes your alarms.
