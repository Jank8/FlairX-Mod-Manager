using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace FlairX_Mod_Manager
{
    // ============================================================
    // XInput P/Invoke definitions
    // ============================================================

    [StructLayout(LayoutKind.Sequential)]
    internal struct XINPUT_GAMEPAD
    {
        public ushort wButtons;
        public byte bLeftTrigger;
        public byte bRightTrigger;
        public short sThumbLX;
        public short sThumbLY;
        public short sThumbRX;
        public short sThumbRY;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct XINPUT_STATE
    {
        public uint dwPacketNumber;
        public XINPUT_GAMEPAD Gamepad;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct XINPUT_VIBRATION
    {
        public ushort wLeftMotorSpeed;
        public ushort wRightMotorSpeed;
    }

    internal static class XInput
    {
        // XInput button bitmasks
        public const ushort XINPUT_GAMEPAD_DPAD_UP        = 0x0001;
        public const ushort XINPUT_GAMEPAD_DPAD_DOWN      = 0x0002;
        public const ushort XINPUT_GAMEPAD_DPAD_LEFT      = 0x0004;
        public const ushort XINPUT_GAMEPAD_DPAD_RIGHT     = 0x0008;
        public const ushort XINPUT_GAMEPAD_START          = 0x0010;
        public const ushort XINPUT_GAMEPAD_BACK           = 0x0020;
        public const ushort XINPUT_GAMEPAD_LEFT_THUMB     = 0x0040;
        public const ushort XINPUT_GAMEPAD_RIGHT_THUMB    = 0x0080;
        public const ushort XINPUT_GAMEPAD_LEFT_SHOULDER  = 0x0100;
        public const ushort XINPUT_GAMEPAD_RIGHT_SHOULDER = 0x0200;
        public const ushort XINPUT_GAMEPAD_A              = 0x1000;
        public const ushort XINPUT_GAMEPAD_B              = 0x2000;
        public const ushort XINPUT_GAMEPAD_X              = 0x4000;
        public const ushort XINPUT_GAMEPAD_Y              = 0x8000;

        public const int ERROR_SUCCESS       = 0;
        public const int ERROR_DEVICE_NOT_CONNECTED = 1167;

        // Trigger threshold: XInput triggers are 0-255, ~30% threshold
        public const byte TRIGGER_THRESHOLD = 75;

        // Stick deadzone (same as XInput default: 7849 for left, 8689 for right)
        public const short STICK_DEADZONE = 8000;

        [DllImport("xinput1_4.dll", EntryPoint = "XInputGetState")]
        public static extern int GetState(int dwUserIndex, out XINPUT_STATE pState);

        [DllImport("xinput1_4.dll", EntryPoint = "XInputSetState")]
        public static extern int SetState(int dwUserIndex, ref XINPUT_VIBRATION pVibration);
    }

    // ============================================================
    // Controller type enum (kept for compat with existing code)
    // ============================================================
    public enum ControllerType
    {
        Unknown,
        Xbox360,
        XboxOne,
        XboxSeriesX,
        PS3,
        PS4,
        PS5,
        NintendoSwitchPro,
        NintendoSwitchJoyConLeft,
        NintendoSwitchJoyConRight,
        NintendoSwitchJoyConPair,
        SteamController,
        SteamDeck,
        Generic
    }

    // ============================================================
    // Event Args (kept for full backward compat)
    // ============================================================

    public class SDL3ButtonEventArgs : EventArgs
    {
        public string DisplayName { get; }
        public SDL3ButtonEventArgs(string displayName) { DisplayName = displayName; }
    }

    public class SDL3AxisEventArgs : EventArgs
    {
        public int Direction { get; } // 1=Up, 2=Down, 3=Left, 4=Right
        public string DisplayName { get; }
        public bool IsLeftStick { get; }
        public SDL3AxisEventArgs(int direction, string displayName, bool isLeftStick)
        {
            Direction = direction; DisplayName = displayName; IsLeftStick = isLeftStick;
        }
        public bool IsUp    => Direction == 1;
        public bool IsDown  => Direction == 2;
        public bool IsLeft  => Direction == 3;
        public bool IsRight => Direction == 4;
    }

    public class SDL3ControllerEventArgs : EventArgs
    {
        public string ControllerName { get; }
        public ControllerType ControllerType { get; }
        public SDL3ControllerEventArgs(string name, ControllerType type)
        {
            ControllerName = name; ControllerType = type;
        }
    }

    public class ThumbstickEventArgs : EventArgs
    {
        public int Direction { get; }
        public ThumbstickEventArgs(int direction) { Direction = direction; }
        public bool IsUp    => Direction == 1;
        public bool IsDown  => Direction == 2;
        public bool IsLeft  => Direction == 3;
        public bool IsRight => Direction == 4;
    }

    public class GamepadButtonEventArgs : EventArgs
    {
        public string DisplayName { get; }
        public GamepadButtonEventArgs(string displayName) { DisplayName = displayName; }
        public string GetButtonDisplayName() => DisplayName;
    }

    public class SDL3RawAxisEventArgs : EventArgs
    {
        public short LeftX  { get; }
        public short LeftY  { get; }
        public short RightX { get; }
        public short RightY { get; }
        public short LeftTrigger  { get; }
        public short RightTrigger { get; }

        public SDL3RawAxisEventArgs(short lx, short ly, short rx, short ry, short lt, short rt)
        {
            LeftX = lx; LeftY = ly; RightX = rx; RightY = ry; LeftTrigger = lt; RightTrigger = rt;
        }

        public float GetNormalizedRightY() => RightY / 32767.0f;
        public float GetNormalizedRightX() => RightX / 32767.0f;
    }

    // ============================================================
    // XInput-based gamepad manager (replaces SDL3)
    // Single instance shared between MainWindow and OverlayWindow
    // ============================================================

    /// <summary>
    /// Gamepad manager using pure XInput (xinput1_4.dll P/Invoke).
    /// No external dependencies. Xbox controllers only.
    /// Single shared instance — MainWindow owns it, OverlayWindow borrows it.
    /// </summary>
    public class SDL3GamepadManager : IDisposable
    {
        #region Events

        public event EventHandler<SDL3ButtonEventArgs>?    ButtonPressed;
        public event EventHandler<SDL3ButtonEventArgs>?    ButtonReleased;
        public event EventHandler<SDL3AxisEventArgs>?      AxisMoved;
        public event EventHandler<SDL3ControllerEventArgs>? ControllerConnected;
        public event EventHandler<SDL3ControllerEventArgs>? ControllerDisconnected;
        public event EventHandler<SDL3RawAxisEventArgs>?   RawAxisMoved;
        public event EventHandler<ThumbstickEventArgs>?    LeftThumbstickMoved;

        #endregion

        #region Fields

        private int  _userIndex = -1;          // XInput player index (0-3), -1 = none connected
        private bool _wasConnected = false;
        private bool _disposed = false;

        private CancellationTokenSource? _pollCts;
        private Task? _pollTask;

        private int _pollIntervalMs = 16; // ~60 fps polling

        // Button state tracking
        private ushort _prevButtons  = 0;
        private bool   _prevLT       = false;
        private bool   _prevRT       = false;

        // Stick direction tracking (discrete)
        private int _prevLeftDir  = 0;
        private int _prevRightDir = 0;

        // XInput button → display name map (Xbox layout)
        private static readonly (ushort Mask, string Name)[] ButtonNames =
        {
            (XInput.XINPUT_GAMEPAD_A,              "XB A"),
            (XInput.XINPUT_GAMEPAD_B,              "XB B"),
            (XInput.XINPUT_GAMEPAD_X,              "XB X"),
            (XInput.XINPUT_GAMEPAD_Y,              "XB Y"),
            (XInput.XINPUT_GAMEPAD_LEFT_SHOULDER,  "XB LB"),
            (XInput.XINPUT_GAMEPAD_RIGHT_SHOULDER, "XB RB"),
            (XInput.XINPUT_GAMEPAD_START,          "XB Start"),
            (XInput.XINPUT_GAMEPAD_BACK,           "XB Back"),
            (XInput.XINPUT_GAMEPAD_LEFT_THUMB,     "XB L CLICK"),
            (XInput.XINPUT_GAMEPAD_RIGHT_THUMB,    "XB R CLICK"),
            (XInput.XINPUT_GAMEPAD_DPAD_UP,        "XB ↑"),
            (XInput.XINPUT_GAMEPAD_DPAD_DOWN,      "XB ↓"),
            (XInput.XINPUT_GAMEPAD_DPAD_LEFT,      "XB ←"),
            (XInput.XINPUT_GAMEPAD_DPAD_RIGHT,     "XB →"),
        };

        #endregion

        #region Properties

        public bool IsConnected   => _userIndex >= 0;
        public bool IsPolling     => _pollTask != null && !_pollTask.IsCompleted;
        public ControllerType ControllerType => ControllerType.Xbox360; // XInput = Xbox
        public string ControllerName => IsConnected ? $"XInput Controller {_userIndex}" : "Not connected";

        public int PollIntervalMs
        {
            get => _pollIntervalMs;
            set => _pollIntervalMs = Math.Max(1, Math.Min(100, value));
        }

        #endregion

        public SDL3GamepadManager()
        {
            // Scan for first connected XInput device
            ScanForControllers();
        }

        #region Controller scanning

        private void ScanForControllers()
        {
            for (int i = 0; i < 4; i++)
            {
                int result = XInput.GetState(i, out _);
                if (result == XInput.ERROR_SUCCESS)
                {
                    _userIndex = i;
                    _wasConnected = true;
                    Logger.LogInfo($"XInput controller found at index {i}");
                    ControllerConnected?.Invoke(this, new SDL3ControllerEventArgs(ControllerName, ControllerType.Xbox360));
                    return;
                }
            }
            Logger.LogInfo("No XInput controller found at startup");
        }

        #endregion

        #region Public Methods

        public void StartPolling()
        {
            if (IsPolling) return;
            _pollCts = new CancellationTokenSource();
            _pollTask = Task.Run(() => PollLoop(_pollCts.Token));
            Logger.LogInfo("XInput gamepad polling started");
        }

        public void StopPolling()
        {
            if (!IsPolling) return;
            _pollCts?.Cancel();
            try { _pollTask?.Wait(500); } catch (AggregateException) { }
            _pollCts?.Dispose();
            _pollCts  = null;
            _pollTask = null;
            Logger.LogInfo("XInput gamepad polling stopped");
        }

        public void Vibrate(ushort leftMotor, ushort rightMotor, int durationMs = 200)
        {
            if (_userIndex < 0) return;
            try
            {
                var vib = new XINPUT_VIBRATION { wLeftMotorSpeed = leftMotor, wRightMotorSpeed = rightMotor };
                XInput.SetState(_userIndex, ref vib);

                if (durationMs > 0)
                {
                    // Stop vibration after duration
                    _ = Task.Run(async () =>
                    {
                        await Task.Delay(durationMs);
                        var stop = new XINPUT_VIBRATION { wLeftMotorSpeed = 0, wRightMotorSpeed = 0 };
                        try { XInput.SetState(_userIndex, ref stop); } catch { }
                    });
                }
            }
            catch (Exception ex)
            {
                Logger.LogError("XInput vibration failed", ex);
            }
        }

        public void Rumble(ushort lowFrequency, ushort highFrequency, uint durationMs = 200)
            => Vibrate(lowFrequency, highFrequency, (int)durationMs);

        public bool CheckConnection() => IsConnected;

        public string GetButtonDisplayName(string buttonName) => buttonName;

        #endregion

        #region Poll loop

        private async Task PollLoop(CancellationToken ct)
        {
            while (!ct.IsCancellationRequested)
            {
                try
                {
                    PollState();
                    await Task.Delay(_pollIntervalMs, ct);
                }
                catch (OperationCanceledException) { break; }
                catch (Exception ex)
                {
                    Logger.LogError("XInput poll loop error", ex);
                    await Task.Delay(1000, ct);
                }
            }
        }

        private void PollState()
        {
            // If no controller known, scan periodically (every ~2s at 16ms interval = ~125 polls)
            if (_userIndex < 0)
            {
                ScanForControllers();
                return;
            }

            int result = XInput.GetState(_userIndex, out XINPUT_STATE state);

            if (result == XInput.ERROR_DEVICE_NOT_CONNECTED)
            {
                if (_wasConnected)
                {
                    _wasConnected = false;
                    var oldName = ControllerName;
                    _userIndex = -1;
                    Logger.LogInfo($"XInput controller disconnected");
                    ControllerDisconnected?.Invoke(this, new SDL3ControllerEventArgs(oldName, ControllerType.Xbox360));
                    // Reset states
                    _prevButtons = 0;
                    _prevLT = false; _prevRT = false;
                    _prevLeftDir = 0; _prevRightDir = 0;
                }
                return;
            }

            if (result != XInput.ERROR_SUCCESS) return;

            if (!_wasConnected)
            {
                _wasConnected = true;
                Logger.LogInfo($"XInput controller reconnected at index {_userIndex}");
                ControllerConnected?.Invoke(this, new SDL3ControllerEventArgs(ControllerName, ControllerType.Xbox360));
            }

            var gp = state.Gamepad;

            // --- Buttons ---
            ushort curr = gp.wButtons;
            ushort prev = _prevButtons;

            foreach (var (mask, name) in ButtonNames)
            {
                bool isNow  = (curr & mask) != 0;
                bool wasPrev = (prev & mask) != 0;

                if (isNow && !wasPrev)
                    ButtonPressed?.Invoke(this, new SDL3ButtonEventArgs(name));
                else if (!isNow && wasPrev)
                    ButtonReleased?.Invoke(this, new SDL3ButtonEventArgs(name));
            }
            _prevButtons = curr;

            // --- Triggers (treated as buttons with threshold) ---
            bool ltNow = gp.bLeftTrigger  > XInput.TRIGGER_THRESHOLD;
            bool rtNow = gp.bRightTrigger > XInput.TRIGGER_THRESHOLD;

            if (ltNow && !_prevLT) ButtonPressed?.Invoke(this,  new SDL3ButtonEventArgs("XB LT"));
            else if (!ltNow && _prevLT) ButtonReleased?.Invoke(this, new SDL3ButtonEventArgs("XB LT"));
            if (rtNow && !_prevRT) ButtonPressed?.Invoke(this,  new SDL3ButtonEventArgs("XB RT"));
            else if (!rtNow && _prevRT) ButtonReleased?.Invoke(this, new SDL3ButtonEventArgs("XB RT"));
            _prevLT = ltNow;
            _prevRT = rtNow;

            // --- Left stick (discrete navigation) ---
            int leftDir = GetStickDirection(gp.sThumbLX, gp.sThumbLY);
            if (leftDir != _prevLeftDir)
            {
                if (leftDir != 0)
                {
                    string name = GetStickDirectionName("L", leftDir);
                    AxisMoved?.Invoke(this, new SDL3AxisEventArgs(leftDir, name, isLeftStick: true));
                    LeftThumbstickMoved?.Invoke(this, new ThumbstickEventArgs(leftDir));
                }
                _prevLeftDir = leftDir;
            }

            // --- Right stick (discrete navigation) ---
            int rightDir = GetStickDirection(gp.sThumbRX, gp.sThumbRY);
            if (rightDir != _prevRightDir)
            {
                if (rightDir != 0)
                {
                    string name = GetStickDirectionName("R", rightDir);
                    AxisMoved?.Invoke(this, new SDL3AxisEventArgs(rightDir, name, isLeftStick: false));
                }
                _prevRightDir = rightDir;
            }

            // --- Raw axis event (for smooth scrolling) ---
            short lt16 = (short)(gp.bLeftTrigger  * 128);
            short rt16 = (short)(gp.bRightTrigger * 128);
            RawAxisMoved?.Invoke(this, new SDL3RawAxisEventArgs(
                gp.sThumbLX, gp.sThumbLY,
                gp.sThumbRX, gp.sThumbRY,
                lt16, rt16));
        }

        private static int GetStickDirection(short x, short y)
        {
            if (y >  XInput.STICK_DEADZONE) return 1; // Up
            if (y < -XInput.STICK_DEADZONE) return 2; // Down
            if (x < -XInput.STICK_DEADZONE) return 3; // Left
            if (x >  XInput.STICK_DEADZONE) return 4; // Right
            return 0;
        }

        private static string GetStickDirectionName(string stick, int dir)
        {
            string arrow = dir switch { 1 => "↑", 2 => "↓", 3 => "←", 4 => "→", _ => "?" };
            return $"XB {stick}{arrow}";
        }

        #endregion

        #region IDisposable

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            StopPolling();
            // Stop vibration on dispose
            if (_userIndex >= 0)
            {
                try
                {
                    var stop = new XINPUT_VIBRATION();
                    XInput.SetState(_userIndex, ref stop);
                }
                catch { }
            }
        }

        #endregion
    }

    // ============================================================
    // GamepadManager — alias for backward compatibility
    // ============================================================

    /// <summary>
    /// Backward-compatible alias for SDL3GamepadManager (now XInput-based).
    /// Used by MainWindow, OverlayWindow, and settings pages.
    /// </summary>
    public class GamepadManager : SDL3GamepadManager
    {
        // Compatibility events wrapping base events into GamepadButtonEventArgs
        public new event EventHandler<GamepadButtonEventArgs>? ButtonPressed;
        public new event EventHandler<GamepadButtonEventArgs>? ButtonReleased;
        public event EventHandler<GamepadButtonEventArgs>?     StickMoved;

        public GamepadManager() : base()
        {
            base.ButtonPressed  += (s, e) => ButtonPressed?.Invoke(this,  new GamepadButtonEventArgs(e.DisplayName));
            base.ButtonReleased += (s, e) => ButtonReleased?.Invoke(this, new GamepadButtonEventArgs(e.DisplayName));
            base.AxisMoved      += (s, e) => StickMoved?.Invoke(this,     new GamepadButtonEventArgs(e.DisplayName));
        }
    }
}
