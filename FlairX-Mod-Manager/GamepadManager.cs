using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace FlairX_Mod_Manager
{
    /// <summary>
    /// Manager for Xbox controller input using XInput API
    /// </summary>
    public class GamepadManager : IDisposable
    {
        #region XInput API

        [DllImport("xinput1_4.dll", EntryPoint = "XInputGetState")]
        private static extern uint XInputGetState(uint dwUserIndex, ref XINPUT_STATE pState);

        [DllImport("xinput1_4.dll", EntryPoint = "XInputSetState")]
        private static extern uint XInputSetState(uint dwUserIndex, ref XINPUT_VIBRATION pVibration);

        [StructLayout(LayoutKind.Sequential)]
        private struct XINPUT_STATE
        {
            public uint dwPacketNumber;
            public XINPUT_GAMEPAD Gamepad;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct XINPUT_GAMEPAD
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
        private struct XINPUT_VIBRATION
        {
            public ushort wLeftMotorSpeed;
            public ushort wRightMotorSpeed;
        }

        #endregion

        #region Button Constants

        [Flags]
        public enum GamepadButtons : ushort
        {
            None = 0,
            DPadUp = 0x0001,
            DPadDown = 0x0002,
            DPadLeft = 0x0004,
            DPadRight = 0x0008,
            Start = 0x0010,
            Back = 0x0020,
            LeftThumb = 0x0040,
            RightThumb = 0x0080,
            LeftShoulder = 0x0100,
            RightShoulder = 0x0200,
            A = 0x1000,
            B = 0x2000,
            X = 0x4000,
            Y = 0x8000
        }

        #endregion

        #region Events

        public event EventHandler<GamepadButtonEventArgs>? ButtonPressed;
        public event EventHandler<GamepadButtonEventArgs>? ButtonReleased;
        public event EventHandler? ControllerConnected;
        public event EventHandler? ControllerDisconnected;

        #endregion

        #region Fields

        private const uint ERROR_SUCCESS = 0;
        private const uint ERROR_DEVICE_NOT_CONNECTED = 1167;
        private const byte TRIGGER_THRESHOLD = 30;
        private const short THUMBSTICK_DEADZONE = 7849;

        private readonly uint _controllerIndex;
        private CancellationTokenSource? _pollCts;
        private Task? _pollTask;
        private bool _disposed;
        private bool _isConnected;
        private ushort _previousButtons;
        private byte _previousLeftTrigger;
        private byte _previousRightTrigger;
        private int _pollIntervalMs = 16; // ~60Hz

        #endregion

        #region Properties

        public bool IsConnected => _isConnected;
        public bool IsPolling => _pollTask != null && !_pollTask.IsCompleted;
        public int PollIntervalMs
        {
            get => _pollIntervalMs;
            set => _pollIntervalMs = Math.Max(1, Math.Min(100, value));
        }

        #endregion

        public GamepadManager(uint controllerIndex = 0)
        {
            _controllerIndex = controllerIndex;
        }

        #region Public Methods

        public void StartPolling()
        {
            if (IsPolling) return;

            _pollCts = new CancellationTokenSource();
            _pollTask = Task.Run(() => PollLoop(_pollCts.Token));
            Logger.LogInfo($"Gamepad polling started for controller {_controllerIndex}");
        }

        public void StopPolling()
        {
            if (!IsPolling) return;

            _pollCts?.Cancel();
            try
            {
                _pollTask?.Wait(500);
            }
            catch (AggregateException) { }
            
            _pollCts?.Dispose();
            _pollCts = null;
            _pollTask = null;
            Logger.LogInfo($"Gamepad polling stopped for controller {_controllerIndex}");
        }

        public bool CheckConnection()
        {
            var state = new XINPUT_STATE();
            var result = XInputGetState(_controllerIndex, ref state);
            return result == ERROR_SUCCESS;
        }

        public void Vibrate(ushort leftMotor, ushort rightMotor, int durationMs = 200)
        {
            if (!_isConnected) return;

            Task.Run(async () =>
            {
                var vibration = new XINPUT_VIBRATION
                {
                    wLeftMotorSpeed = leftMotor,
                    wRightMotorSpeed = rightMotor
                };
                XInputSetState(_controllerIndex, ref vibration);

                await Task.Delay(durationMs);

                vibration.wLeftMotorSpeed = 0;
                vibration.wRightMotorSpeed = 0;
                XInputSetState(_controllerIndex, ref vibration);
            });
        }

        public static string GetButtonName(GamepadButtons button)
        {
            return button switch
            {
                GamepadButtons.DPadUp => "D-Pad Up",
                GamepadButtons.DPadDown => "D-Pad Down",
                GamepadButtons.DPadLeft => "D-Pad Left",
                GamepadButtons.DPadRight => "D-Pad Right",
                GamepadButtons.Start => "Start",
                GamepadButtons.Back => "Back",
                GamepadButtons.LeftThumb => "Left Stick",
                GamepadButtons.RightThumb => "Right Stick",
                GamepadButtons.LeftShoulder => "LB",
                GamepadButtons.RightShoulder => "RB",
                GamepadButtons.A => "A",
                GamepadButtons.B => "B",
                GamepadButtons.X => "X",
                GamepadButtons.Y => "Y",
                _ => button.ToString()
            };
        }

        #endregion

        #region Private Methods

        private async Task PollLoop(CancellationToken ct)
        {
            while (!ct.IsCancellationRequested)
            {
                try
                {
                    var state = new XINPUT_STATE();
                    var result = XInputGetState(_controllerIndex, ref state);

                    if (result == ERROR_SUCCESS)
                    {
                        if (!_isConnected)
                        {
                            _isConnected = true;
                            _previousButtons = 0;
                            _previousLeftTrigger = 0;
                            _previousRightTrigger = 0;
                            ControllerConnected?.Invoke(this, EventArgs.Empty);
                            Logger.LogInfo($"Controller {_controllerIndex} connected");
                        }

                        ProcessInput(state.Gamepad);
                    }
                    else if (result == ERROR_DEVICE_NOT_CONNECTED && _isConnected)
                    {
                        _isConnected = false;
                        ControllerDisconnected?.Invoke(this, EventArgs.Empty);
                        Logger.LogInfo($"Controller {_controllerIndex} disconnected");
                    }

                    await Task.Delay(_pollIntervalMs, ct);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    Logger.LogError("Error in gamepad poll loop", ex);
                    await Task.Delay(1000, ct);
                }
            }
        }

        private void ProcessInput(XINPUT_GAMEPAD gamepad)
        {
            var currentButtons = gamepad.wButtons;

            // Check for newly pressed buttons
            var pressed = (ushort)(currentButtons & ~_previousButtons);
            if (pressed != 0)
            {
                foreach (GamepadButtons button in Enum.GetValues(typeof(GamepadButtons)))
                {
                    if (button != GamepadButtons.None && (pressed & (ushort)button) != 0)
                    {
                        ButtonPressed?.Invoke(this, new GamepadButtonEventArgs(button));
                    }
                }
            }

            // Check for released buttons
            var released = (ushort)(_previousButtons & ~currentButtons);
            if (released != 0)
            {
                foreach (GamepadButtons button in Enum.GetValues(typeof(GamepadButtons)))
                {
                    if (button != GamepadButtons.None && (released & (ushort)button) != 0)
                    {
                        ButtonReleased?.Invoke(this, new GamepadButtonEventArgs(button));
                    }
                }
            }

            // Check triggers as buttons (LT/RT)
            bool leftTriggerPressed = gamepad.bLeftTrigger > TRIGGER_THRESHOLD;
            bool leftTriggerWasPressed = _previousLeftTrigger > TRIGGER_THRESHOLD;
            
            if (leftTriggerPressed && !leftTriggerWasPressed)
            {
                ButtonPressed?.Invoke(this, new GamepadButtonEventArgs(GamepadButtons.None, true, false));
            }
            else if (!leftTriggerPressed && leftTriggerWasPressed)
            {
                ButtonReleased?.Invoke(this, new GamepadButtonEventArgs(GamepadButtons.None, true, false));
            }

            bool rightTriggerPressed = gamepad.bRightTrigger > TRIGGER_THRESHOLD;
            bool rightTriggerWasPressed = _previousRightTrigger > TRIGGER_THRESHOLD;
            
            if (rightTriggerPressed && !rightTriggerWasPressed)
            {
                ButtonPressed?.Invoke(this, new GamepadButtonEventArgs(GamepadButtons.None, false, true));
            }
            else if (!rightTriggerPressed && rightTriggerWasPressed)
            {
                ButtonReleased?.Invoke(this, new GamepadButtonEventArgs(GamepadButtons.None, false, true));
            }

            _previousButtons = currentButtons;
            _previousLeftTrigger = gamepad.bLeftTrigger;
            _previousRightTrigger = gamepad.bRightTrigger;
        }

        #endregion

        #region IDisposable

        public void Dispose()
        {
            if (_disposed) return;
            
            StopPolling();
            _disposed = true;
        }

        #endregion
    }

    public class GamepadButtonEventArgs : EventArgs
    {
        public GamepadManager.GamepadButtons Button { get; }
        public bool IsLeftTrigger { get; }
        public bool IsRightTrigger { get; }

        public GamepadButtonEventArgs(GamepadManager.GamepadButtons button, bool isLeftTrigger = false, bool isRightTrigger = false)
        {
            Button = button;
            IsLeftTrigger = isLeftTrigger;
            IsRightTrigger = isRightTrigger;
        }

        public string GetButtonDisplayName()
        {
            if (IsLeftTrigger) return "LT";
            if (IsRightTrigger) return "RT";
            return GamepadManager.GetButtonName(Button);
        }
    }
}
