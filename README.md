# Note-Clock

A tiny, always-on-top desktop widget for Windows: an analog clock with a small note pad next to it.
It stays on top of all other windows, has no frame or title bar, and keeps out of the taskbar and Alt+Tab,
so it is as unobtrusive as possible.

some more [BLAH-BLAH about it on my website](https://0x8.in.th/18658)

**It's not pretty, but it does exactly one job and stays out of the way. That's all I want from a desktop tool.**

![Note-Clock at 85% opacity, sitting on top of a busy game window](docs/noteclock-in-use.png)

*Note-Clock in the lower right corner, semi-transparent on top of a crowded window: a quick note on the left,
the clock on the right.*

Layout:

```
+------------------------------------+----------+
|  note area (300 x 100)             |  analog  |
|  type anything here                |  clock   |
|                                    |  13:27   |
+------------------------------------+----------+
```

## Features

- **Analog clock** (100 x 100 px) with a digital time in the lower part of the face.
  - Not focused: shows `HH:mm`, the hands are drawn over it.
  - Focused: shows `HH:mm:ss`, drawn on top of the hands.
- **Note pad** (300 x 100 px) on the left; just click and type.
- **Alert**: start the note with a time like `23:59` and at that minute you get a sound and a popup
  (see [Alerts](#alerts)).
- **Move** the window by dragging it with the **right** mouse button (works anywhere, including the note area).
- **Poor man's click-through**: double-click the clock face and the window disappears for 10 seconds, so you can
  click whatever is behind it. It comes back without stealing keyboard focus.
- **Transparency** from 100% down to 20%, set from the tray icon menu.
- **Tray icon** menu: Transparency, Reset position, Exit. Double-click the tray icon to bring the window back
  immediately and focus the note.
- **Persistence**: position, transparency and note text are saved to `NoteClock.json` next to the exe and restored
  on start. If the saved position is off every screen (e.g. a monitor was unplugged), it falls back to a default spot.

## Usage

Download the Release file, put it anywhere you want, Run `NoteClock.exe`.
To start it with Windows, put a shortcut to it in the folder that opens with `Win+R` → `shell:startup`.

### Alerts

If the note **starts** with a time, NoteClock uses it as an alert. Everything after the time is just your text:

```
14:30 call the bank
check the server logs later
```

When the time is recognized as an alert it turns **green** in the note, as confirmation that the alert is set.
If it stays white (e.g. `25:99`, or the time isn't at the very start), no alert will happen.

At 14:30 the Windows warning sound plays 3 times (2 seconds apart) and a popup shows the first line of the note.
The popup stays until you click OK. If the window was hidden by a double-click, it comes back first.

- `9:05` and `09:05` both work (24-hour format); spaces before the time are ignored.
- A time somewhere else in the note (e.g. `call at 14:30`) does not trigger anything.
- Each alert fires once per day. If you leave the note as it is, it rings again the next day at the same time,
  like a daily alarm. Remove or change the time to stop it.
- If the PC is asleep during that minute, the alert is skipped.

## Versions

- **1.1**: alert from a time at the start of the note, shown in green when it's set; version shown in the
  tray tooltip; paste always inserts plain text.
- **1.0**: first release.

## Build yourself

No SDK or extra tools needed; it uses the C# compiler that ships with Windows (.NET Framework 4.x):

```
build.bat
```

This produces `NoteClock.exe` from `NoteClock.cs`.

## Files

| File             | Purpose                                      |
|------------------|----------------------------------------------|
| `NoteClock.cs`   | The whole application (WinForms, ~400 lines) |
| `build.bat`      | Compiles `NoteClock.exe`                     |
| `NoteClock.exe`  | The prebuilt app                             |
| `NoteClock.json` | Saved position, transparency and note        |
| `docs/`          | Screenshot used in this README               |

## License

MIT [LICENSE](LICENSE).
