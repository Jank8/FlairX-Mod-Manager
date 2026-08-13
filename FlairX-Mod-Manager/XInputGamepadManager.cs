using System;
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
    // WinRT Windows.Gaming.Input based gamepad manager
    //
    // KEY ADVANTAGE over XInput P/Invoke:
    // WinRT Gamepad API respects window focus automatically.
    // When overlay window has focus → overlay gets input, game does NOT.
    // When game has focus → game gets input, overlay does NOT.
    // No admin rights, no hacks, no SetForegroundWindow tricks needed.
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

        private Gamepad? _gamepad;
        private bool _disposed;
        private CancellationTokenSource? _pollCts;
        private Task? _pollTask;
        private int _pollIntervalMs = 16; // ~60fps

        // State tracking
        private GamepadButtons _prevButtons = GamepadButtons.None;
        private bool _prevLT = false;
        private bool _prevRT = false;
        private int _prevLeftDir  = 0;
        private int _prevRightDir = 0;

        private const double TRIGGER_THRESHOLD = 0.3;
        private const double STICK_DEADZONE    = 0.25;

        // WinRT button → display name mapping
        private static readonly (GamepadButtons Mask, string Name)[] ButtonNames =
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

        public bool IsConnected => _gamepad != null;
        public bool IsPolling   => _pollTask != null && !_pollTask.IsCompleted;
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
            // Subscribe to WinRT connect/disconnect events
            Gamepad.GamepadAdded   += OnGamepadAdded;
            Gamepad.GamepadRemoved += OnGamepadRemoved;

            // Pick up any already-connected gamepad
            var gamepads = Gamepad.Gamepads;
            if (gamepads.Count > 0)
            {
                _gamepad = gamepads[0];
                Logger.LogInfo($"WinRT Gamepad found on startup");
                ControllerConnected?.Invoke(this, new SDL3ControllerEventArgs(ControllerName, ControllerType.Xbox360));
            }
            else
            {
                Logger.LogInfo("No WinRT Gamepad found on startup - waiting for connection");
            }
        }

        #region Connect / Disconnect

        private void OnGamepadAdded(object? sender, Gamepad e)
        {
            if (_gamepad == null)
            {
                _gamepad = e;
                Logger.LogInfo("WinRT Gamepad connected");
                ControllerConnected?.Invoke(this, new SDL3ControllerEventArgs(ControllerName, ControllerType.Xbox360));
            }
        }

        private void OnGamepadRemoved(object? sender, Gamepad e)
        {
            if (_gamepad == e)
            {
                _gamepad = null;
                Logger.LogInfo("WinRT Gamepad disconnected");
                ControllerDisconnected?.Invoke(this, new SDL3ControllerEventArgs("Xbox Controller", ControllerType.Xbox360));

                // Reset all states
                _prevButtons = GamepadButtons.None;
                _prevLT = false; _prevRT = false;
                _prevLeftDir = 0; _prevRightDir = 0;

                // Try to pick up another connected gamepad
                var gamepads = Gamepad.Gamepads;
                if (gamepads.Count > 0)
                {
                    _gamepad = gamepads[0];
                    Logger.LogInfo("WinRT Gamepad switched to next available");
                    ControllerConnected?.Invoke(this, new SDL3ControllerEventArgs(ControllerName, ControllerType.Xbox360));
                }
            }
        }

        #endregion

        #region Public Methods

        public void StartPolling()
        {
            if (IsPolling) return;
            _pollCts  = new CancellationTokenSource();
            _pollTask = Task.Run(() => PollLoop(_pollCts.Token));
            Logger.LogInfo("WinRT Gamepad polling started");
        }

        public void StopPolling()
        {
            if (!IsPolling) return;
            _pollCts?.Cancel();
            try { _pollTask?.Wait(500); } catch (AggregateException) { }
            _pollCts?.Dispose();
            _pollCts  = null;
            _pollTask = null;
            Logger.LogInfo("WinRT Gamepad polling stopped");
        }

        public void Vibrate(ushort leftMotor, ushort rightMotor, int durationMs = 200)
        {
            if (_gamepad == null) return;
            try
            {
                _gamepad.Vibration = new GamepadVibration
                {
                    LeftMotor  = leftMotor  / 65535.0,
                    RightMotor = rightMotor / 65535.0
                };

                if (durationMs > 0)
                {
                    _ = Task.Run(async () =>
                    {
                        await Task.Delay(durationMs);
                        try { if (_gamepad != null) _gamepad.Vibration = new GamepadVibration(); }
                        catch { }
                    });
                }
            }
            catch (Exception ex)
            {
                Logger.LogError("WinRT Gamepad vibration failed", ex);
            }
        }

        public void Rumble(ushort low, ushort high, uint durationMs = 200)
            => Vibrate(low, high, (int)durationMs);

        public bool CheckConnection() => IsConnected;
        public string GetButtonDisplayName(string buttonName) => buttonName;

        #endregion

        #region Poll Loop

        private async Task PollLoop(CancellationToken ct)
        {
            while (!ct.IsCancellationRequested)
            {
                try
                {
                    if (_gamepad != null)
                        PollState();

                    await Task.Delay(_pollIntervalMs, ct);
                }
                catch (OperationCanceledException) { break; }
                catch (Exception ex)
                {
                    Logger.LogError("WinRT Gamepad poll error", ex);
                    await Task.Delay(1000, ct);
                }
            }
        }

        private void PollState()
        {
            if (_gamepad == null) return;

            GamepadReading reading;
            try { reading = _gamepad.GetCurrentReading(); }
            catch { return; }

            // --- Buttons ---
            var curr = reading.Buttons;
            foreach (var (mask, name) in ButtonNames)
            {
                bool isNow   = (curr & mask) != 0;
                bool wasPrev = (_prevButtons & mask) != 0;
                if (isNow  && !wasPrev) ButtonPressed?.Invoke(this,  new SDL3ButtonEventArgs(name));
                else if (!isNow && wasPrev) ButtonReleased?.Invoke(this, new SDL3ButtonEventArgs(name));
            }
            _prevButtons = curr;

            // --- Triggers (treated as digital buttons with threshold) ---
            bool ltNow = reading.LeftTrigger  > TRIGGER_THRESHOLD;
            bool rtNow = reading.RightTrigger > TRIGGER_THRESHOLD;
            if (ltNow  && !_prevLT) ButtonPressed?.Invoke(this,  new SDL3ButtonEventArgs("XB LT"));
            else if (!ltNow && _prevLT) ButtonReleased?.Invoke(this, new SDL3ButtonEventArgs("XB LT"));
            if (rtNow  && !_prevRT) ButtonPressed?.Invoke(this,  new SDL3ButtonEventArgs("XB RT"));
            else if (!rtNow && _prevRT) ButtonReleased?.Invoke(this, new SDL3ButtonEventArgs("XB RT"));
            _prevLT = ltNow;
            _prevRT = rtNow;

            // --- Left stick (discrete navigation) ---
            int leftDir = GetStickDirection(reading.LeftThumbstickX, reading.LeftThumbstickY);
            if (leftDir != _prevLeftDir)
            {
                if (leftDir != 0)
                {
                    var name = GetStickDirectionName("L", leftDir);
                    AxisMoved?.Invoke(this, new SDL3AxisEventArgs(leftDir, name, isLeftStick: true));
                    LeftThumbstickMoved?.Invoke(this, new ThumbstickEventArgs(leftDir));
                }
                _prevLeftDir = leftDir;
            }

            // --- Right stick (discrete navigation) ---
            int rightDir = GetStickDirection(reading.RightThumbstickX, reading.RightThumbstickY);
            if (rightDir != _prevRightDir)
            {
                if (rightDir != 0)
                {
                    var name = GetStickDirectionName("R", rightDir);
                    AxisMoved?.Invoke(this, new SDL3AxisEventArgs(rightDir, name, isLeftStick: false));
                }
                _prevRightDir = rightDir;
            }

            // --- Raw axis (smooth scrolling) - convert WinRT -1..1 to short range ---
            short lx = (short)(reading.LeftThumbstickX  * 32767);
            short ly = (short)(reading.LeftThumbstickY  * 32767);
            short rx = (short)(reading.RightThumbstickX * 32767);
            short ry = (short)(reading.RightThumbstickY * 32767);
            short lt = (short)(reading.LeftTrigger  * 32767);
            short rt = (short)(reading.RightTrigger * 32767);
            RawAxisMoved?.Invoke(this, new SDL3RawAxisEventArgs(lx, ly, rx, ry, lt, rt));
        }

        private static int GetStickDirection(double x, double y)
        {
            if (y >  STICK_DEADZONE) return 1; // Up
            if (y < -STICK_DEADZONE) return 2; // Down
            if (x < -STICK_DEADZONE) return 3; // Left
            if (x >  STICK_DEADZONE) return 4; // Right
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

            Gamepad.GamepadAdded   -= OnGamepadAdded;
            Gamepad.GamepadRemoved -= OnGamepadRemoved;

            StopPolling();

            try { if (_gamepad != null) _gamepad.Vibration = new GamepadVibration(); }
            catch { }
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
