using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using SDL;

namespace FlairX_Mod_Manager
{
    /// <summary>
    /// Controller type detected by SDL3
    /// </summary>
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

    /// <summary>
    /// Manager for gamepad input using SDL3 - supports Xbox, PlayStation, Nintendo, Steam Deck and more
    /// </summary>
    public class SDL3GamepadManager : IDisposable
    {
        #region Events

        public event EventHandler<SDL3ButtonEventArgs>? ButtonPressed;
        public event EventHandler<SDL3ButtonEventArgs>? ButtonReleased;
        public event EventHandler<SDL3AxisEventArgs>? AxisMoved;
        public event EventHandler<SDL3ControllerEventArgs>? ControllerConnected;
        public event EventHandler<SDL3ControllerEventArgs>? ControllerDisconnected;

        /// <summary>
        /// Compatibility event for old GamepadManager API - fires when left thumbstick moves
        /// </summary>
        public event EventHandler<ThumbstickEventArgs>? LeftThumbstickMoved;

        #endregion

        #region Fields

        private unsafe SDL_Gamepad* _gamepad = null;
        private SDL_JoystickID _joystickId;
        private CancellationTokenSource? _pollCts;
        private Task? _pollTask;
        private bool _disposed;
        private bool _sdlInitialized;
        private ControllerType _controllerType = ControllerType.Unknown;
        private string _controllerName = "";

        private readonly Dictionary<SDL_GamepadButton, bool> _buttonStates = new();
        private readonly Dictionary<SDL_GamepadAxis, short> _axisStates = new();

        private const short AXIS_DEADZONE = 8000;
        private const short TRIGGER_THRESHOLD = 8000;
        private int _pollIntervalMs = 16;

        private int _leftStickDirection = 0;
        private int _rightStickDirection = 0;

        #endregion

        #region Properties

        public unsafe bool IsConnected => _gamepad != null;
        public bool IsPolling => _pollTask != null && !_pollTask.IsCompleted;
        public ControllerType ControllerType => _controllerType;
        public string ControllerName => _controllerName;

        public int PollIntervalMs
        {
            get => _pollIntervalMs;
            set => _pollIntervalMs = Math.Max(1, Math.Min(100, value));
        }

        #endregion

        public SDL3GamepadManager()
        {
            InitializeSDL();
        }

        #region Initialization

        private unsafe void InitializeSDL()
        {
            try
            {
                if (!SDL3.SDL_Init(SDL_InitFlags.SDL_INIT_GAMEPAD | SDL_InitFlags.SDL_INIT_JOYSTICK))
                {
                    Logger.LogError($"SDL3 initialization failed: {SDL3.SDL_GetError()}");
                    return;
                }

                _sdlInitialized = true;
                Logger.LogInfo("SDL3 initialized successfully");

                TryConnectGamepad();
            }
            catch (Exception ex)
            {
                Logger.LogError("Failed to initialize SDL3", ex);
            }
        }

        private unsafe void TryConnectGamepad()
        {
            int count;
            SDL_JoystickID* joysticks = SDL3.SDL_GetGamepads(&count);

            if (joysticks != null && count > 0)
            {
                OpenGamepad(joysticks[0]);
            }

            if (joysticks != null)
            {
                SDL3.SDL_free(joysticks);
            }
        }

        private unsafe void OpenGamepad(SDL_JoystickID id)
        {
            if (_gamepad != null)
            {
                SDL3.SDL_CloseGamepad(_gamepad);
            }

            _gamepad = SDL3.SDL_OpenGamepad(id);

            if (_gamepad != null)
            {
                _joystickId = id;
                _controllerName = SDL3.SDL_GetGamepadName(_gamepad) ?? "Unknown Controller";
                _controllerType = DetectControllerType();

                Logger.LogInfo($"Gamepad connected: {_controllerName} (Type: {_controllerType})");
                ControllerConnected?.Invoke(this, new SDL3ControllerEventArgs(_controllerName, _controllerType));
            }
        }

        private unsafe ControllerType DetectControllerType()
        {
            if (_gamepad == null) return ControllerType.Unknown;

            var type = SDL3.SDL_GetGamepadType(_gamepad);

            return type switch
            {
                SDL_GamepadType.SDL_GAMEPAD_TYPE_XBOX360 => ControllerType.Xbox360,
                SDL_GamepadType.SDL_GAMEPAD_TYPE_XBOXONE => ControllerType.XboxOne,
                SDL_GamepadType.SDL_GAMEPAD_TYPE_PS3 => ControllerType.PS3,
                SDL_GamepadType.SDL_GAMEPAD_TYPE_PS4 => ControllerType.PS4,
                SDL_GamepadType.SDL_GAMEPAD_TYPE_PS5 => ControllerType.PS5,
                SDL_GamepadType.SDL_GAMEPAD_TYPE_NINTENDO_SWITCH_PRO => ControllerType.NintendoSwitchPro,
                SDL_GamepadType.SDL_GAMEPAD_TYPE_NINTENDO_SWITCH_JOYCON_LEFT => ControllerType.NintendoSwitchJoyConLeft,
                SDL_GamepadType.SDL_GAMEPAD_TYPE_NINTENDO_SWITCH_JOYCON_RIGHT => ControllerType.NintendoSwitchJoyConRight,
                SDL_GamepadType.SDL_GAMEPAD_TYPE_NINTENDO_SWITCH_JOYCON_PAIR => ControllerType.NintendoSwitchJoyConPair,
                _ => ControllerType.Generic
            };
        }

        #endregion

        #region Public Methods

        public void StartPolling()
        {
            if (!_sdlInitialized || IsPolling) return;

            _pollCts = new CancellationTokenSource();
            _pollTask = Task.Run(() => PollLoop(_pollCts.Token));
            Logger.LogInfo("SDL3 gamepad polling started");
        }

        public void StopPolling()
        {
            if (!IsPolling) return;

            _pollCts?.Cancel();
            try { _pollTask?.Wait(500); } catch (AggregateException) { }

            _pollCts?.Dispose();
            _pollCts = null;
            _pollTask = null;
            Logger.LogInfo("SDL3 gamepad polling stopped");
        }

        public unsafe void Rumble(ushort lowFrequency, ushort highFrequency, uint durationMs = 200)
        {
            if (_gamepad == null) return;
            SDL3.SDL_RumbleGamepad(_gamepad, lowFrequency, highFrequency, durationMs);
        }

        /// <summary>
        /// Compatibility method for old GamepadManager API
        /// </summary>
        public void Vibrate(ushort leftMotor, ushort rightMotor, int durationMs = 200)
        {
            Rumble(leftMotor, rightMotor, (uint)durationMs);
        }

        /// <summary>
        /// Compatibility method for old GamepadManager API
        /// </summary>
        public bool CheckConnection()
        {
            return IsConnected;
        }

        public string GetButtonDisplayName(SDL_GamepadButton button)
        {
            return _controllerType switch
            {
                ControllerType.PS3 or ControllerType.PS4 or ControllerType.PS5 => GetPlayStationButtonName(button),
                ControllerType.NintendoSwitchPro or ControllerType.NintendoSwitchJoyConLeft or
                ControllerType.NintendoSwitchJoyConRight or ControllerType.NintendoSwitchJoyConPair => GetNintendoButtonName(button),
                ControllerType.SteamDeck => GetSteamDeckButtonName(button),
                _ => GetXboxButtonName(button)
            };
        }

        #endregion

        #region Button Name Mappings

        private static string GetXboxButtonName(SDL_GamepadButton button)
        {
            return button switch
            {
                SDL_GamepadButton.SDL_GAMEPAD_BUTTON_SOUTH => "XB A",
                SDL_GamepadButton.SDL_GAMEPAD_BUTTON_EAST => "XB B",
                SDL_GamepadButton.SDL_GAMEPAD_BUTTON_WEST => "XB X",
                SDL_GamepadButton.SDL_GAMEPAD_BUTTON_NORTH => "XB Y",
                SDL_GamepadButton.SDL_GAMEPAD_BUTTON_BACK => "XB Back",
                SDL_GamepadButton.SDL_GAMEPAD_BUTTON_GUIDE => "XB Home",
                SDL_GamepadButton.SDL_GAMEPAD_BUTTON_START => "XB Start",
                SDL_GamepadButton.SDL_GAMEPAD_BUTTON_LEFT_STICK => "XB L CLICK",
                SDL_GamepadButton.SDL_GAMEPAD_BUTTON_RIGHT_STICK => "XB R CLICK",
                SDL_GamepadButton.SDL_GAMEPAD_BUTTON_LEFT_SHOULDER => "XB LB",
                SDL_GamepadButton.SDL_GAMEPAD_BUTTON_RIGHT_SHOULDER => "XB RB",
                SDL_GamepadButton.SDL_GAMEPAD_BUTTON_DPAD_UP => "XB ↑",
                SDL_GamepadButton.SDL_GAMEPAD_BUTTON_DPAD_DOWN => "XB ↓",
                SDL_GamepadButton.SDL_GAMEPAD_BUTTON_DPAD_LEFT => "XB ←",
                SDL_GamepadButton.SDL_GAMEPAD_BUTTON_DPAD_RIGHT => "XB →",
                SDL_GamepadButton.SDL_GAMEPAD_BUTTON_MISC1 => "XB Share",
                SDL_GamepadButton.SDL_GAMEPAD_BUTTON_LEFT_PADDLE1 => "XB P1",
                SDL_GamepadButton.SDL_GAMEPAD_BUTTON_LEFT_PADDLE2 => "XB P2",
                SDL_GamepadButton.SDL_GAMEPAD_BUTTON_RIGHT_PADDLE1 => "XB P3",
                SDL_GamepadButton.SDL_GAMEPAD_BUTTON_RIGHT_PADDLE2 => "XB P4",
                _ => button.ToString()
            };
        }

        private static string GetPlayStationButtonName(SDL_GamepadButton button)
        {
            return button switch
            {
                SDL_GamepadButton.SDL_GAMEPAD_BUTTON_SOUTH => "PS ×",
                SDL_GamepadButton.SDL_GAMEPAD_BUTTON_EAST => "PS ○",
                SDL_GamepadButton.SDL_GAMEPAD_BUTTON_WEST => "PS □",
                SDL_GamepadButton.SDL_GAMEPAD_BUTTON_NORTH => "PS △",
                SDL_GamepadButton.SDL_GAMEPAD_BUTTON_BACK => "PS Share",
                SDL_GamepadButton.SDL_GAMEPAD_BUTTON_GUIDE => "PS Home",
                SDL_GamepadButton.SDL_GAMEPAD_BUTTON_START => "PS Options",
                SDL_GamepadButton.SDL_GAMEPAD_BUTTON_LEFT_STICK => "PS L CLICK",
                SDL_GamepadButton.SDL_GAMEPAD_BUTTON_RIGHT_STICK => "PS R CLICK",
                SDL_GamepadButton.SDL_GAMEPAD_BUTTON_LEFT_SHOULDER => "PS L1",
                SDL_GamepadButton.SDL_GAMEPAD_BUTTON_RIGHT_SHOULDER => "PS R1",
                SDL_GamepadButton.SDL_GAMEPAD_BUTTON_DPAD_UP => "PS ↑",
                SDL_GamepadButton.SDL_GAMEPAD_BUTTON_DPAD_DOWN => "PS ↓",
                SDL_GamepadButton.SDL_GAMEPAD_BUTTON_DPAD_LEFT => "PS ←",
                SDL_GamepadButton.SDL_GAMEPAD_BUTTON_DPAD_RIGHT => "PS →",
                SDL_GamepadButton.SDL_GAMEPAD_BUTTON_TOUCHPAD => "PS Touchpad",
                SDL_GamepadButton.SDL_GAMEPAD_BUTTON_MISC1 => "PS5 Mute",
                SDL_GamepadButton.SDL_GAMEPAD_BUTTON_MISC2 => "PS Misc2",
                SDL_GamepadButton.SDL_GAMEPAD_BUTTON_MISC3 => "PS Misc3",
                _ => button.ToString()
            };
        }

        private static string GetNintendoButtonName(SDL_GamepadButton button)
        {
            return button switch
            {
                // Nintendo has A/B and X/Y swapped compared to Xbox
                SDL_GamepadButton.SDL_GAMEPAD_BUTTON_SOUTH => "NIN B",
                SDL_GamepadButton.SDL_GAMEPAD_BUTTON_EAST => "NIN A",
                SDL_GamepadButton.SDL_GAMEPAD_BUTTON_WEST => "NIN Y",
                SDL_GamepadButton.SDL_GAMEPAD_BUTTON_NORTH => "NIN X",
                SDL_GamepadButton.SDL_GAMEPAD_BUTTON_BACK => "NIN -",
                SDL_GamepadButton.SDL_GAMEPAD_BUTTON_GUIDE => "NIN Home",
                SDL_GamepadButton.SDL_GAMEPAD_BUTTON_START => "NIN +",
                SDL_GamepadButton.SDL_GAMEPAD_BUTTON_LEFT_STICK => "NIN L CLICK",
                SDL_GamepadButton.SDL_GAMEPAD_BUTTON_RIGHT_STICK => "NIN R CLICK",
                SDL_GamepadButton.SDL_GAMEPAD_BUTTON_LEFT_SHOULDER => "NIN L",
                SDL_GamepadButton.SDL_GAMEPAD_BUTTON_RIGHT_SHOULDER => "NIN R",
                SDL_GamepadButton.SDL_GAMEPAD_BUTTON_DPAD_UP => "NIN ↑",
                SDL_GamepadButton.SDL_GAMEPAD_BUTTON_DPAD_DOWN => "NIN ↓",
                SDL_GamepadButton.SDL_GAMEPAD_BUTTON_DPAD_LEFT => "NIN ←",
                SDL_GamepadButton.SDL_GAMEPAD_BUTTON_DPAD_RIGHT => "NIN →",
                SDL_GamepadButton.SDL_GAMEPAD_BUTTON_MISC1 => "NIN Capture",
                _ => button.ToString()
            };
        }

        private static string GetSteamDeckButtonName(SDL_GamepadButton button)
        {
            return button switch
            {
                SDL_GamepadButton.SDL_GAMEPAD_BUTTON_SOUTH => "SD A",
                SDL_GamepadButton.SDL_GAMEPAD_BUTTON_EAST => "SD B",
                SDL_GamepadButton.SDL_GAMEPAD_BUTTON_WEST => "SD X",
                SDL_GamepadButton.SDL_GAMEPAD_BUTTON_NORTH => "SD Y",
                SDL_GamepadButton.SDL_GAMEPAD_BUTTON_BACK => "SD View",
                SDL_GamepadButton.SDL_GAMEPAD_BUTTON_GUIDE => "SD Guide",
                SDL_GamepadButton.SDL_GAMEPAD_BUTTON_START => "SD Options",
                SDL_GamepadButton.SDL_GAMEPAD_BUTTON_LEFT_STICK => "SD L CLICK",
                SDL_GamepadButton.SDL_GAMEPAD_BUTTON_RIGHT_STICK => "SD R CLICK",
                SDL_GamepadButton.SDL_GAMEPAD_BUTTON_LEFT_SHOULDER => "SD L1",
                SDL_GamepadButton.SDL_GAMEPAD_BUTTON_RIGHT_SHOULDER => "SD R1",
                SDL_GamepadButton.SDL_GAMEPAD_BUTTON_DPAD_UP => "SD ↑",
                SDL_GamepadButton.SDL_GAMEPAD_BUTTON_DPAD_DOWN => "SD ↓",
                SDL_GamepadButton.SDL_GAMEPAD_BUTTON_DPAD_LEFT => "SD ←",
                SDL_GamepadButton.SDL_GAMEPAD_BUTTON_DPAD_RIGHT => "SD →",
                SDL_GamepadButton.SDL_GAMEPAD_BUTTON_MISC1 => "SD Quick",
                SDL_GamepadButton.SDL_GAMEPAD_BUTTON_LEFT_PADDLE1 => "SD L4",
                SDL_GamepadButton.SDL_GAMEPAD_BUTTON_LEFT_PADDLE2 => "SD L5",
                SDL_GamepadButton.SDL_GAMEPAD_BUTTON_RIGHT_PADDLE1 => "SD R4",
                SDL_GamepadButton.SDL_GAMEPAD_BUTTON_RIGHT_PADDLE2 => "SD R5",
                _ => button.ToString()
            };
        }

        public string GetAxisDisplayName(SDL_GamepadAxis axis, bool positive)
        {
            string prefix = _controllerType switch
            {
                ControllerType.PS3 or ControllerType.PS4 or ControllerType.PS5 => "PS",
                ControllerType.NintendoSwitchPro or ControllerType.NintendoSwitchJoyConLeft or
                ControllerType.NintendoSwitchJoyConRight or ControllerType.NintendoSwitchJoyConPair => "NIN",
                ControllerType.SteamDeck => "SD",
                _ => "XB"
            };

            // Trigger names vary by controller type
            string leftTrigger = _controllerType switch
            {
                ControllerType.PS3 or ControllerType.PS4 or ControllerType.PS5 => "L2",
                ControllerType.NintendoSwitchPro or ControllerType.NintendoSwitchJoyConLeft or
                ControllerType.NintendoSwitchJoyConRight or ControllerType.NintendoSwitchJoyConPair => "ZL",
                ControllerType.SteamDeck => "L2",
                _ => "LT"
            };
            string rightTrigger = _controllerType switch
            {
                ControllerType.PS3 or ControllerType.PS4 or ControllerType.PS5 => "R2",
                ControllerType.NintendoSwitchPro or ControllerType.NintendoSwitchJoyConLeft or
                ControllerType.NintendoSwitchJoyConRight or ControllerType.NintendoSwitchJoyConPair => "ZR",
                ControllerType.SteamDeck => "R2",
                _ => "RT"
            };

            return axis switch
            {
                SDL_GamepadAxis.SDL_GAMEPAD_AXIS_LEFTX => positive ? $"{prefix} L→" : $"{prefix} L←",
                SDL_GamepadAxis.SDL_GAMEPAD_AXIS_LEFTY => positive ? $"{prefix} L↓" : $"{prefix} L↑",
                SDL_GamepadAxis.SDL_GAMEPAD_AXIS_RIGHTX => positive ? $"{prefix} R→" : $"{prefix} R←",
                SDL_GamepadAxis.SDL_GAMEPAD_AXIS_RIGHTY => positive ? $"{prefix} R↓" : $"{prefix} R↑",
                SDL_GamepadAxis.SDL_GAMEPAD_AXIS_LEFT_TRIGGER => $"{prefix} {leftTrigger}",
                SDL_GamepadAxis.SDL_GAMEPAD_AXIS_RIGHT_TRIGGER => $"{prefix} {rightTrigger}",
                _ => axis.ToString()
            };
        }

        #endregion

        #region Polling

        private async Task PollLoop(CancellationToken ct)
        {
            while (!ct.IsCancellationRequested)
            {
                try
                {
                    ProcessEvents();

                    unsafe
                    {
                        if (_gamepad != null)
                        {
                            PollGamepadState();
                        }
                    }

                    await Task.Delay(_pollIntervalMs, ct);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    Logger.LogError("Error in SDL3 gamepad poll loop", ex);
                    await Task.Delay(1000, ct);
                }
            }
        }

        private unsafe void ProcessEvents()
        {
            SDL_Event e;
            while (SDL3.SDL_PollEvent(&e))
            {
                if (e.type == (uint)SDL_EventType.SDL_EVENT_GAMEPAD_ADDED)
                {
                    if (_gamepad == null)
                    {
                        OpenGamepad(e.gdevice.which);
                    }
                }
                else if (e.type == (uint)SDL_EventType.SDL_EVENT_GAMEPAD_REMOVED)
                {
                    if (_gamepad != null)
                    {
                        var removedName = _controllerName;
                        var removedType = _controllerType;

                        SDL3.SDL_CloseGamepad(_gamepad);
                        _gamepad = null;
                        _controllerName = "";
                        _controllerType = ControllerType.Unknown;

                        Logger.LogInfo($"Controller disconnected: {removedName}");
                        ControllerDisconnected?.Invoke(this, new SDL3ControllerEventArgs(removedName, removedType));

                        TryConnectGamepad();
                    }
                }
            }
        }

        private unsafe void PollGamepadState()
        {
            // Poll buttons
            foreach (SDL_GamepadButton button in Enum.GetValues(typeof(SDL_GamepadButton)))
            {
                if (button == SDL_GamepadButton.SDL_GAMEPAD_BUTTON_INVALID ||
                    button == SDL_GamepadButton.SDL_GAMEPAD_BUTTON_COUNT)
                    continue;

                bool isPressed = SDL3.SDL_GetGamepadButton(_gamepad, button);
                bool wasPressed = _buttonStates.TryGetValue(button, out var prev) && prev;

                if (isPressed && !wasPressed)
                {
                    ButtonPressed?.Invoke(this, new SDL3ButtonEventArgs(button, GetButtonDisplayName(button)));
                }
                else if (!isPressed && wasPressed)
                {
                    ButtonReleased?.Invoke(this, new SDL3ButtonEventArgs(button, GetButtonDisplayName(button)));
                }

                _buttonStates[button] = isPressed;
            }

            // Poll triggers
            var leftTrigger = SDL3.SDL_GetGamepadAxis(_gamepad, SDL_GamepadAxis.SDL_GAMEPAD_AXIS_LEFT_TRIGGER);
            var rightTrigger = SDL3.SDL_GetGamepadAxis(_gamepad, SDL_GamepadAxis.SDL_GAMEPAD_AXIS_RIGHT_TRIGGER);

            CheckTrigger(SDL_GamepadAxis.SDL_GAMEPAD_AXIS_LEFT_TRIGGER, leftTrigger);
            CheckTrigger(SDL_GamepadAxis.SDL_GAMEPAD_AXIS_RIGHT_TRIGGER, rightTrigger);

            // Poll thumbsticks for discrete navigation
            var leftX = SDL3.SDL_GetGamepadAxis(_gamepad, SDL_GamepadAxis.SDL_GAMEPAD_AXIS_LEFTX);
            var leftY = SDL3.SDL_GetGamepadAxis(_gamepad, SDL_GamepadAxis.SDL_GAMEPAD_AXIS_LEFTY);
            CheckThumbstickDirection(leftX, leftY, ref _leftStickDirection, true);

            var rightX = SDL3.SDL_GetGamepadAxis(_gamepad, SDL_GamepadAxis.SDL_GAMEPAD_AXIS_RIGHTX);
            var rightY = SDL3.SDL_GetGamepadAxis(_gamepad, SDL_GamepadAxis.SDL_GAMEPAD_AXIS_RIGHTY);
            CheckThumbstickDirection(rightX, rightY, ref _rightStickDirection, false);
        }

        private void CheckTrigger(SDL_GamepadAxis axis, short value)
        {
            bool isPressed = value > TRIGGER_THRESHOLD;
            bool wasPressed = _axisStates.TryGetValue(axis, out var prev) && prev > TRIGGER_THRESHOLD;

            if (isPressed && !wasPressed)
            {
                var displayName = GetAxisDisplayName(axis, true);
                ButtonPressed?.Invoke(this, new SDL3ButtonEventArgs(SDL_GamepadButton.SDL_GAMEPAD_BUTTON_INVALID, displayName, axis));
            }
            else if (!isPressed && wasPressed)
            {
                var displayName = GetAxisDisplayName(axis, true);
                ButtonReleased?.Invoke(this, new SDL3ButtonEventArgs(SDL_GamepadButton.SDL_GAMEPAD_BUTTON_INVALID, displayName, axis));
            }

            _axisStates[axis] = value;
        }

        private void CheckThumbstickDirection(short x, short y, ref int previousDirection, bool isLeft)
        {
            int currentDirection = 0;

            if (y < -AXIS_DEADZONE) currentDirection = 1; // Up
            else if (y > AXIS_DEADZONE) currentDirection = 2; // Down
            else if (x < -AXIS_DEADZONE) currentDirection = 3; // Left
            else if (x > AXIS_DEADZONE) currentDirection = 4; // Right

            if (currentDirection != previousDirection && currentDirection != 0)
            {
                string prefix = _controllerType switch
                {
                    ControllerType.PS3 or ControllerType.PS4 or ControllerType.PS5 => "PS",
                    ControllerType.NintendoSwitchPro => "NIN",
                    ControllerType.SteamDeck => "SD",
                    _ => "XB"
                };

                string stick = isLeft ? "L" : "R";
                string dir = currentDirection switch
                {
                    1 => "↑",
                    2 => "↓",
                    3 => "←",
                    4 => "→",
                    _ => ""
                };

                var displayName = $"{prefix} {stick}{dir}";
                AxisMoved?.Invoke(this, new SDL3AxisEventArgs(currentDirection, displayName, isLeft));

                // Fire compatibility event for left stick
                if (isLeft)
                {
                    LeftThumbstickMoved?.Invoke(this, new ThumbstickEventArgs(currentDirection));
                }
            }

            previousDirection = currentDirection;
        }

        #endregion

        #region IDisposable

        public unsafe void Dispose()
        {
            if (_disposed) return;

            StopPolling();

            if (_gamepad != null)
            {
                SDL3.SDL_CloseGamepad(_gamepad);
                _gamepad = null;
            }

            if (_sdlInitialized)
            {
                SDL3.SDL_Quit();
                _sdlInitialized = false;
            }

            _disposed = true;
        }

        #endregion
    }

    #region Event Args

    public class SDL3ButtonEventArgs : EventArgs
    {
        public SDL_GamepadButton Button { get; }
        public SDL_GamepadAxis Axis { get; }
        public string DisplayName { get; }
        public bool IsTrigger => Axis != SDL_GamepadAxis.SDL_GAMEPAD_AXIS_INVALID;

        public SDL3ButtonEventArgs(SDL_GamepadButton button, string displayName, SDL_GamepadAxis axis = SDL_GamepadAxis.SDL_GAMEPAD_AXIS_INVALID)
        {
            Button = button;
            DisplayName = displayName;
            Axis = axis;
        }
    }

    public class SDL3AxisEventArgs : EventArgs
    {
        public int Direction { get; } // 1=Up, 2=Down, 3=Left, 4=Right
        public string DisplayName { get; }
        public bool IsLeftStick { get; }

        public SDL3AxisEventArgs(int direction, string displayName, bool isLeftStick)
        {
            Direction = direction;
            DisplayName = displayName;
            IsLeftStick = isLeftStick;
        }

        public bool IsUp => Direction == 1;
        public bool IsDown => Direction == 2;
        public bool IsLeft => Direction == 3;
        public bool IsRight => Direction == 4;
    }

    public class SDL3ControllerEventArgs : EventArgs
    {
        public string ControllerName { get; }
        public ControllerType ControllerType { get; }

        public SDL3ControllerEventArgs(string name, ControllerType type)
        {
            ControllerName = name;
            ControllerType = type;
        }
    }

    /// <summary>
    /// Compatibility class for old GamepadManager API
    /// </summary>
    public class ThumbstickEventArgs : EventArgs
    {
        public int Direction { get; } // 1=Up, 2=Down, 3=Left, 4=Right

        public ThumbstickEventArgs(int direction)
        {
            Direction = direction;
        }

        public bool IsUp => Direction == 1;
        public bool IsDown => Direction == 2;
        public bool IsLeft => Direction == 3;
        public bool IsRight => Direction == 4;
    }

    /// <summary>
    /// Compatibility class for old GamepadManager API - wraps SDL3ButtonEventArgs
    /// </summary>
    public class GamepadButtonEventArgs : EventArgs
    {
        public string DisplayName { get; }

        public GamepadButtonEventArgs(string displayName)
        {
            DisplayName = displayName;
        }

        public string GetButtonDisplayName() => DisplayName;
    }

    #endregion

    /// <summary>
    /// Alias for backward compatibility - use SDL3GamepadManager
    /// </summary>
    public class GamepadManager : SDL3GamepadManager
    {
        /// <summary>
        /// Compatibility event that wraps SDL3ButtonEventArgs into GamepadButtonEventArgs
        /// </summary>
        public new event EventHandler<GamepadButtonEventArgs>? ButtonPressed;
        public new event EventHandler<GamepadButtonEventArgs>? ButtonReleased;

        /// <summary>
        /// Compatibility event that wraps SDL3AxisEventArgs into GamepadButtonEventArgs for stick input
        /// </summary>
        public event EventHandler<GamepadButtonEventArgs>? StickMoved;

        public GamepadManager() : base()
        {
            // Subscribe to base events and re-emit as compatibility events
            base.ButtonPressed += (s, e) => ButtonPressed?.Invoke(this, new GamepadButtonEventArgs(e.DisplayName));
            base.ButtonReleased += (s, e) => ButtonReleased?.Invoke(this, new GamepadButtonEventArgs(e.DisplayName));
            base.AxisMoved += (s, e) => StickMoved?.Invoke(this, new GamepadButtonEventArgs(e.DisplayName));
        }
    }
}
