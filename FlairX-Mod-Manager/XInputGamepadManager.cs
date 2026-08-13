using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Windows.Gaming.Input;

namespace FlairX_Mod_Manager
{
    // ============================================================
    // Controller type enum (kept for compat)
    // ============================================================
    public enum ControllerType
    {
        Unknown, Xbox360, XboxOne, XboxSeriesX,
        PS3, PS4, PS5,
        NintendoSwitchPro, NintendoSwitchJoyConLeft, NintendoSwitchJoyConRight, NintendoSwitchJoyConPair,
        SteamController, SteamDeck, Generic
    }

    // ============================================================
    // Event Args (backward compat)
    // ============================================================

    public class SDL3ButtonEventArgs : EventArgs
    {
        public string DisplayName { get; }
        public SDL3ButtonEventArgs(string displayName) { DisplayName = displayName; }
    }

    public class SDL3AxisEventArgs : EventArgs
    {
        public int Direction { get; }
        public string DisplayName { get; }
        public bool IsLeftStick { get; }
        public SDL3AxisEventArgs(int direction, string displayName, bool isLeftStick)
        { Direction = direction; DisplayName = displayName; IsLeftStick = isLeftStick; }
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
        { ControllerName = name; ControllerType = type; }
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
        { LeftX = lx; LeftY = ly; RightX = rx; RightY = ry; LeftTrigger = lt; RightTrigger = rt; }
        public float GetNormalizedRightY() => RightY / 32767.0f;
        public float GetNormalizedRightX() => RightX / 32767.0f;
    }

    // ============================================================
    // XInput P/Invoke (used ONLY for global polling when game has focus)
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

        public const byte  TRIGGER_THRESHOLD = 75;
        public const short STICK_DEADZONE    = 8000;

        public const int ERROR_SUCCESS              = 0;
        public const int ERROR_DEVICE_NOT_CONNECTED = 1167;

        [DllImport("xinput1_4.dll", EntryPoint = "XInputGetState")]
        public static extern int GetState(int dwUserIndex, out XINPUT_STATE pState);

        [DllImport("xinput1_4.dll", EntryPoint = "XInputSetState")]
        public static extern int SetState(int dwUserIndex, ref XINPUT_VIBRATION pVibration);
    }

    // ============================================================
    // Hybrid Gamepad Manager
    //
    // TWO modes in one class:
    //
    // 1. GLOBAL mode (XInput P/Invoke) — always active, ignores window focus.
    //    Used by MainWindow to detect the overlay-open combo even when game has focus.
    //    Emits ButtonPressed/ButtonReleased events.
    //
    // 2. OVERLAY mode (WinRT Windows.Gaming.Input) — active only when overlay is visible.
    //    WinRT respects window focus: when overlay has focus, game does NOT get input.
    //    Used by OverlayWindow for navigation (D-pad, stick, buttons).
    //    Emits the same ButtonPressed/ButtonReleased/AxisMoved events.
    //
    // MainWindow owns ONE instance. OverlayWindow subscribes to it.
    // OverlayWindow sets IsOverlayActive = true when shown, false when hidden.
    // ============================================================

    public class SDL3GamepadManager : IDisposable
    {
        #region Events

        public event EventHandler<SDL3ButtonEventArgs>?     ButtonPressed;
        public event EventHandler<SDL3ButtonEventArgs>?     ButtonReleased;
        public event EventHandler<SDL3AxisEventArgs>?       AxisMoved;
        public event EventHandler<SDL3ControllerEventArgs>? ControllerConnected;
        public event EventHandler<SDL3ControllerEventArgs>? ControllerDisconnected;
        public event EventHandler<SDL3RawAxisEventArgs>?    RawAxisMoved;
        public event EventHandler<ThumbstickEventArgs>?     LeftThumbstickMoved;

        #endregion

        #region Fields

        // XInput state (global polling)
        private int    _xInputIndex  = -1;
        private ushort _xPrevButtons = 0;
        private bool   _xPrevLT      = false;
        private bool   _xPrevRT      = false;
        private int    _xPrevLeftDir  = 0;
        private int    _xPrevRightDir = 0;

        // WinRT state (overlay navigation)
        private Gamepad? _winrtGamepad;
        private GamepadButtons _wPrevButtons = GamepadButtons.None;
        private bool _wPrevLT = false;
        private bool _wPrevRT = false;
        private int  _wPrevLeftDir  = 0;
        private int  _wPrevRightDir = 0;

        private CancellationTokenSource? _pollCts;
        private Task? _pollTask;
        private int _pollIntervalMs = 16;
        private bool _disposed;

        // When true: use WinRT path (overlay visible, overlay has focus)
        // When false: use XInput path (game has focus)
        public bool IsOverlayActive { get; set; } = false;

        private const double WINRT_TRIGGER_THRESHOLD = 0.3;
        private const double WINRT_STICK_DEADZONE    = 0.25;

        private static readonly (ushort Mask, string Name)[] XInputButtonNames =
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

        private static readonly (GamepadButtons Mask, string Name)[] WinRTButtonNames =
        {
            (GamepadButtons.A,               "XB A"),
            (GamepadButtons.B,               "XB B"),
            (GamepadButtons.X,               "XB X"),
            (GamepadButtons.Y,               "XB Y"),
            (GamepadButtons.LeftShoulder,    "XB LB"),
            (GamepadButtons.RightShoulder,   "XB RB"),
            (GamepadButtons.Menu,            "XB Start"),
            (GamepadButtons.View,            "XB Back"),
            (GamepadButtons.LeftThumbstick,  "XB L CLICK"),
            (GamepadButtons.RightThumbstick, "XB R CLICK"),
            (GamepadButtons.DPadUp,          "XB ↑"),
            (GamepadButtons.DPadDown,        "XB ↓"),
            (GamepadButtons.DPadLeft,        "XB ←"),
            (GamepadButtons.DPadRight,       "XB →"),
        };

        #endregion

        #region Properties

        public bool IsConnected   => _xInputIndex >= 0 || _winrtGamepad != null;
        public bool IsPolling     => _pollTask != null && !_pollTask.IsCompleted;
        public ControllerType ControllerType => ControllerType.Xbox360;
        public string ControllerName => IsConnected ? "Xbox Controller" : "Not connected";

        public int PollIntervalMs
        {
            get => _pollIntervalMs;
            set => _pollIntervalMs = Math.Max(1, Math.Min(100, value));
        }

        #endregion

        public SDL3GamepadManager()
        {
            // XInput: scan for controller
            ScanXInput();

            // WinRT: subscribe to connect/disconnect + pick up existing
            Gamepad.GamepadAdded   += OnWinRTGamepadAdded;
            Gamepad.GamepadRemoved += OnWinRTGamepadRemoved;
            var gamepads = Gamepad.Gamepads;
            if (gamepads.Count > 0)
            {
                _winrtGamepad = gamepads[0];
                Logger.LogInfo("WinRT Gamepad found on startup");
            }

            if (_xInputIndex >= 0 || _winrtGamepad != null)
                ControllerConnected?.Invoke(this, new SDL3ControllerEventArgs(ControllerName, ControllerType.Xbox360));
            else
                Logger.LogInfo("No gamepad found on startup");
        }

        #region XInput scanning

        private void ScanXInput()
        {
            for (int i = 0; i < 4; i++)
            {
                if (XInput.GetState(i, out _) == XInput.ERROR_SUCCESS)
                {
                    _xInputIndex = i;
                    Logger.LogInfo($"XInput controller found at index {i}");
                    return;
                }
            }
        }

        #endregion

        #region WinRT connect/disconnect

        private void OnWinRTGamepadAdded(object? sender, Gamepad e)
        {
            if (_winrtGamepad == null)
            {
                _winrtGamepad = e;
                Logger.LogInfo("WinRT Gamepad connected");
                if (_xInputIndex < 0)
                    ControllerConnected?.Invoke(this, new SDL3ControllerEventArgs(ControllerName, ControllerType.Xbox360));
            }
        }

        private void OnWinRTGamepadRemoved(object? sender, Gamepad e)
        {
            if (_winrtGamepad == e)
            {
                _winrtGamepad = null;
                Logger.LogInfo("WinRT Gamepad disconnected");
                var gamepads = Gamepad.Gamepads;
                if (gamepads.Count > 0)
                    _winrtGamepad = gamepads[0];
            }
        }

        #endregion

        #region Public Methods

        public void StartPolling()
        {
            if (IsPolling) return;
            _pollCts  = new CancellationTokenSource();
            _pollTask = Task.Run(() => PollLoop(_pollCts.Token));
            Logger.LogInfo("Hybrid gamepad polling started");
        }

        public void StopPolling()
        {
            if (!IsPolling) return;
            _pollCts?.Cancel();
            try { _pollTask?.Wait(500); } catch (AggregateException) { }
            _pollCts?.Dispose();
            _pollCts  = null;
            _pollTask = null;
            Logger.LogInfo("Hybrid gamepad polling stopped");
        }

        public void Vibrate(ushort leftMotor, ushort rightMotor, int durationMs = 200)
        {
            // Try WinRT first (better), fall back to XInput
            if (_winrtGamepad != null)
            {
                try
                {
                    _winrtGamepad.Vibration = new GamepadVibration
                    {
                        LeftMotor  = leftMotor  / 65535.0,
                        RightMotor = rightMotor / 65535.0
                    };
                    if (durationMs > 0)
                        _ = Task.Run(async () =>
                        {
                            await Task.Delay(durationMs);
                            try { if (_winrtGamepad != null) _winrtGamepad.Vibration = new GamepadVibration(); } catch { }
                        });
                    return;
                }
                catch { }
            }

            if (_xInputIndex >= 0)
            {
                try
                {
                    var vib = new XINPUT_VIBRATION { wLeftMotorSpeed = leftMotor, wRightMotorSpeed = rightMotor };
                    XInput.SetState(_xInputIndex, ref vib);
                    if (durationMs > 0)
                        _ = Task.Run(async () =>
                        {
                            await Task.Delay(durationMs);
                            try { var s = new XINPUT_VIBRATION(); XInput.SetState(_xInputIndex, ref s); } catch { }
                        });
                }
                catch (Exception ex) { Logger.LogError("XInput vibration failed", ex); }
            }
        }

        public void Rumble(ushort low, ushort high, uint durationMs = 200)
            => Vibrate(low, high, (int)durationMs);

        public bool CheckConnection() => IsConnected;
        public string GetButtonDisplayName(string name) => name;

        #endregion

        #region Poll Loop

        private async Task PollLoop(CancellationToken ct)
        {
            int scanCounter = 0;
            while (!ct.IsCancellationRequested)
            {
                try
                {
                    if (IsOverlayActive)
                        PollWinRT();
                    else
                        PollXInput(ref scanCounter);

                    await Task.Delay(_pollIntervalMs, ct);
                }
                catch (OperationCanceledException) { break; }
                catch (Exception ex)
                {
                    Logger.LogError("Gamepad poll error", ex);
                    await Task.Delay(1000, ct);
                }
            }
        }

        // ---- XInput polling (global, game can have focus) ----

        private void PollXInput(ref int scanCounter)
        {
            // Periodically scan for new controller (~every 2s)
            if (_xInputIndex < 0)
            {
                scanCounter++;
                if (scanCounter >= 125) { scanCounter = 0; ScanXInput(); }
                return;
            }

            int result = XInput.GetState(_xInputIndex, out XINPUT_STATE state);

            if (result == XInput.ERROR_DEVICE_NOT_CONNECTED)
            {
                _xInputIndex = -1;
                _xPrevButtons = 0; _xPrevLT = false; _xPrevRT = false;
                _xPrevLeftDir = 0; _xPrevRightDir = 0;
                ControllerDisconnected?.Invoke(this, new SDL3ControllerEventArgs("Xbox Controller", ControllerType.Xbox360));
                return;
            }

            if (result != XInput.ERROR_SUCCESS) return;

            var gp = state.Gamepad;
            ushort curr = gp.wButtons;

            foreach (var (mask, name) in XInputButtonNames)
            {
                bool isNow   = (curr & mask) != 0;
                bool wasPrev = (_xPrevButtons & mask) != 0;
                if (isNow  && !wasPrev) ButtonPressed?.Invoke(this,  new SDL3ButtonEventArgs(name));
                else if (!isNow && wasPrev) ButtonReleased?.Invoke(this, new SDL3ButtonEventArgs(name));
            }
            _xPrevButtons = curr;

            bool ltNow = gp.bLeftTrigger  > XInput.TRIGGER_THRESHOLD;
            bool rtNow = gp.bRightTrigger > XInput.TRIGGER_THRESHOLD;
            if (ltNow  && !_xPrevLT) ButtonPressed?.Invoke(this,  new SDL3ButtonEventArgs("XB LT"));
            else if (!ltNow && _xPrevLT) ButtonReleased?.Invoke(this, new SDL3ButtonEventArgs("XB LT"));
            if (rtNow  && !_xPrevRT) ButtonPressed?.Invoke(this,  new SDL3ButtonEventArgs("XB RT"));
            else if (!rtNow && _xPrevRT) ButtonReleased?.Invoke(this, new SDL3ButtonEventArgs("XB RT"));
            _xPrevLT = ltNow; _xPrevRT = rtNow;

            int leftDir = GetStickDirectionShort(gp.sThumbLX, gp.sThumbLY);
            if (leftDir != _xPrevLeftDir)
            {
                if (leftDir != 0) { var n = GetStickName("L", leftDir); AxisMoved?.Invoke(this, new SDL3AxisEventArgs(leftDir, n, true)); LeftThumbstickMoved?.Invoke(this, new ThumbstickEventArgs(leftDir)); }
                _xPrevLeftDir = leftDir;
            }

            int rightDir = GetStickDirectionShort(gp.sThumbRX, gp.sThumbRY);
            if (rightDir != _xPrevRightDir)
            {
                if (rightDir != 0) { var n = GetStickName("R", rightDir); AxisMoved?.Invoke(this, new SDL3AxisEventArgs(rightDir, n, false)); }
                _xPrevRightDir = rightDir;
            }

            short lx = gp.sThumbLX, ly = gp.sThumbLY, rx = gp.sThumbRX, ry = gp.sThumbRY;
            short lt = (short)(gp.bLeftTrigger * 128), rt = (short)(gp.bRightTrigger * 128);
            RawAxisMoved?.Invoke(this, new SDL3RawAxisEventArgs(lx, ly, rx, ry, lt, rt));
        }

        // ---- WinRT polling (overlay visible, overlay has focus) ----

        private void PollWinRT()
        {
            if (_winrtGamepad == null) return;

            GamepadReading r;
            try { r = _winrtGamepad.GetCurrentReading(); }
            catch { return; }

            var curr = r.Buttons;
            foreach (var (mask, name) in WinRTButtonNames)
            {
                bool isNow   = (curr & mask) != 0;
                bool wasPrev = (_wPrevButtons & mask) != 0;
                if (isNow  && !wasPrev) ButtonPressed?.Invoke(this,  new SDL3ButtonEventArgs(name));
                else if (!isNow && wasPrev) ButtonReleased?.Invoke(this, new SDL3ButtonEventArgs(name));
            }
            _wPrevButtons = curr;

            bool ltNow = r.LeftTrigger  > WINRT_TRIGGER_THRESHOLD;
            bool rtNow = r.RightTrigger > WINRT_TRIGGER_THRESHOLD;
            if (ltNow  && !_wPrevLT) ButtonPressed?.Invoke(this,  new SDL3ButtonEventArgs("XB LT"));
            else if (!ltNow && _wPrevLT) ButtonReleased?.Invoke(this, new SDL3ButtonEventArgs("XB LT"));
            if (rtNow  && !_wPrevRT) ButtonPressed?.Invoke(this,  new SDL3ButtonEventArgs("XB RT"));
            else if (!rtNow && _wPrevRT) ButtonReleased?.Invoke(this, new SDL3ButtonEventArgs("XB RT"));
            _wPrevLT = ltNow; _wPrevRT = rtNow;

            int leftDir = GetStickDirectionDouble(r.LeftThumbstickX, r.LeftThumbstickY);
            if (leftDir != _wPrevLeftDir)
            {
                if (leftDir != 0) { var n = GetStickName("L", leftDir); AxisMoved?.Invoke(this, new SDL3AxisEventArgs(leftDir, n, true)); LeftThumbstickMoved?.Invoke(this, new ThumbstickEventArgs(leftDir)); }
                _wPrevLeftDir = leftDir;
            }

            int rightDir = GetStickDirectionDouble(r.RightThumbstickX, r.RightThumbstickY);
            if (rightDir != _wPrevRightDir)
            {
                if (rightDir != 0) { var n = GetStickName("R", rightDir); AxisMoved?.Invoke(this, new SDL3AxisEventArgs(rightDir, n, false)); }
                _wPrevRightDir = rightDir;
            }

            short lx = (short)(r.LeftThumbstickX  * 32767);
            short ly = (short)(r.LeftThumbstickY  * 32767);
            short rx = (short)(r.RightThumbstickX * 32767);
            short ry = (short)(r.RightThumbstickY * 32767);
            short lt = (short)(r.LeftTrigger  * 32767);
            short rt = (short)(r.RightTrigger * 32767);
            RawAxisMoved?.Invoke(this, new SDL3RawAxisEventArgs(lx, ly, rx, ry, lt, rt));
        }

        #endregion

        #region Helpers

        private static int GetStickDirectionShort(short x, short y)
        {
            if (y >  XInput.STICK_DEADZONE) return 1;
            if (y < -XInput.STICK_DEADZONE) return 2;
            if (x < -XInput.STICK_DEADZONE) return 3;
            if (x >  XInput.STICK_DEADZONE) return 4;
            return 0;
        }

        private static int GetStickDirectionDouble(double x, double y)
        {
            if (y >  0.25) return 1;
            if (y < -0.25) return 2;
            if (x < -0.25) return 3;
            if (x >  0.25) return 4;
            return 0;
        }

        private static string GetStickName(string stick, int dir)
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

            Gamepad.GamepadAdded   -= OnWinRTGamepadAdded;
            Gamepad.GamepadRemoved -= OnWinRTGamepadRemoved;

            StopPolling();

            try { if (_winrtGamepad != null) _winrtGamepad.Vibration = new GamepadVibration(); } catch { }
            if (_xInputIndex >= 0) { try { var s = new XINPUT_VIBRATION(); XInput.SetState(_xInputIndex, ref s); } catch { } }
        }

        #endregion
    }

    // ============================================================
    // GamepadManager — alias for backward compatibility
    // ============================================================

    public class GamepadManager : SDL3GamepadManager
    {
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
