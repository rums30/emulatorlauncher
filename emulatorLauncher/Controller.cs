﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using emulatorLauncher.Tools;

namespace emulatorLauncher
{
    class Controller
    {
        public Controller()
        {
            DeviceIndex = -1;
        }

        public int PlayerIndex { get; set; }
        public int DeviceIndex { get; set; }
        public string Guid { get; set; }
        public string DevicePath { get; set; }
        public string Name { get; set; }
        public int NbButtons { get; set; }
        public int NbHats { get; set; }
        public int NbAxes { get; set; }

        public string GetSdlGuid(SdlVersion version = SdlVersion.SDL2_0_X)
        {
            if (version == SdlVersion.Current)
                return Guid;

            return Guid
                .FromSdlGuidString()
                .ConvertSdlGuid(version)
                .ToSdlGuidString();
        }

        public InputConfig Config { get; set; }

        #region SdlController
        private SdlGameController _sdlController;
        private bool _sdlControllerKnown = false;

        public SdlGameController SdlController
        {
            get
            {
                if (!_sdlControllerKnown)
                {
                    _sdlControllerKnown = true;

                    if (Name != "Keyboard")
                    {
                        if (!string.IsNullOrEmpty(DevicePath))
                            _sdlController = SdlGameController.GetGameControllerByPath(DevicePath);

                        if (_sdlController == null)
                            _sdlController = SdlGameController.GetGameController(Guid.FromSdlGuidString());
                    }
                }

                return _sdlController;
            }
        }
        #endregion

        #region WinmmJoystick
        private WinmmJoystick _winmmJoystick;
        private bool _winmmJoystickKnown = false;

        public WinmmJoystick WinmmJoystick
        {
            get
            {
                if (!_winmmJoystickKnown)
                {
                    _winmmJoystickKnown = true;

                    if (this.Config != null && Name != "Keyboard")
                    {
                        var di = DirectInput;
                        if (di != null)
                        {
                            _winmmJoystick = WinmmJoystick.Controllers.FirstOrDefault(m => m.VendorId == di.VendorId && m.ProductId == di.ProductId && m.DirectInputIndex == di.DeviceIndex);

                            if (_winmmJoystick == null)
                                _winmmJoystick = WinmmJoystick.Controllers.FirstOrDefault(m => m.VendorId == di.VendorId && m.ProductId == di.ProductId);
                        }
                    }
                }

                return _winmmJoystick;
            }
        }
        #endregion

        #region XInput
        private XInputDevice _xInputDevice;
        private bool _xInputDeviceKnown;

        public bool IsXInputDevice
        {
            get
            {
                if (_xInputDevice != null)
                    return true;

                return Name != "Keyboard" && Config != null && XInputDevice.IsXInputDevice(this.Config.DeviceGUID);
            }
        }

        public XInputDevice XInput
        {
            get
            {
                if (_xInputDeviceKnown == false)
                {
                    _xInputDeviceKnown = true;

                    if (Name == "Keyboard" || !IsXInputDevice)
                        return null;

                    var xinputindex = Program.Controllers
                        .OrderBy(c => c.DeviceIndex)
                        .Where(c => c.IsXInputDevice)
                        .ToList()
                        .IndexOf(this);

                    _xInputDevice = new XInputDevice(xinputindex);
                }

                return _xInputDevice;
            }
        }
        #endregion

        #region DirectInputInfo
        private DirectInputInfo _dInputDevice;
        private bool _dInputDeviceKnown;

        public DirectInputInfo DirectInput
        {
            get
            {
                if (!_dInputDeviceKnown)
                {
                    _dInputDeviceKnown = true;

                    if (Config != null && Name != "Keyboard")
                    {                        
                        if (!string.IsNullOrEmpty(this.DevicePath))
                        {
                            _dInputDevice = DirectInputInfo.Controllers.FirstOrDefault(c => this.DevicePath == c.ParentDevice);

                            if (_dInputDevice == null)
                                _dInputDevice = DirectInputInfo.Controllers.FirstOrDefault(c => this.DevicePath == c.DevicePath);
                        }

                        if (_dInputDevice == null)
                            _dInputDevice = DirectInputInfo.Controllers.FirstOrDefault(c => c.TestDirectInputDevice(Config.DeviceGUID));
                    }
                }

                return _dInputDevice;
            }
        }
        #endregion

        public string ToShortString()
        {
            return Name + ", Device:" + DeviceIndex.ToString() + ", Player:" + PlayerIndex.ToString();
        }

        public override string ToString()
        {
            if (!string.IsNullOrEmpty(DevicePath))
                return Name + " - Device:" + DeviceIndex.ToString() + ", Player:" + PlayerIndex.ToString() + ", Path:" + DevicePath;

            return Name + " - Device:" + DeviceIndex.ToString() + ", Player:" + PlayerIndex.ToString() + ", Guid:" + (Guid.ToString() ?? "null");
        }

        /// <summary>
        /// Translate EmulationStation Input to SDL Input
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
        public Input GetSdlMapping(InputKey key)
        {
            if (Config == null)
                return null;

            Input input = Config[key];
            if (input == null)
                return null;

            if (input.Type == "key")
                return input;

            var ctrl = this.SdlController;
            if (ctrl == null)
                return input;

            int axisValue = 1;

            var mapping = ctrl.Mapping;
            var sdlret = mapping.FirstOrDefault(m => m.Input.Type == input.Type && m.Input.Id == input.Id && m.Input.Value == input.Value);

            if (sdlret == null && input.Type == "axis")
            {
                var invret = mapping.FirstOrDefault(m => m.Input.Type == input.Type && m.Input.Id == input.Id && m.Input.Value == -input.Value);
                if (invret != null)
                {
                    sdlret = invret;
                    axisValue = -1;
                }
            }

            if (sdlret == null)
            {
                if (mapping.All(m => m.Axis == SDL_CONTROLLER_AXIS.INVALID))
                {
                    switch (key)
                    {
                        case InputKey.left:
                            sdlret = mapping.FirstOrDefault(m => m.Input.Type == input.Type && m.Button == SDL_CONTROLLER_BUTTON.DPAD_LEFT);
                            break;
                        case InputKey.right:
                            sdlret = mapping.FirstOrDefault(m => m.Input.Type == input.Type && m.Button == SDL_CONTROLLER_BUTTON.DPAD_RIGHT);
                            break;
                        case InputKey.up:
                            sdlret = mapping.FirstOrDefault(m => m.Input.Type == input.Type && m.Button == SDL_CONTROLLER_BUTTON.DPAD_UP);
                            break;
                        case InputKey.down:
                            sdlret = mapping.FirstOrDefault(m => m.Input.Type == input.Type && m.Button == SDL_CONTROLLER_BUTTON.DPAD_DOWN);
                            break;
                    }
                }

                if (sdlret == null)
                {
                    SimpleLogger.Instance.Warning("[InputConfig] ToSdlCode error can't find <input name=\"" + key.ToString() + "\" type=\"" + input.Type + "\" id=\"" + input.Id + "\" value=\"" + input.Value + "\" /> in SDL2 mapping :\r\n" + ctrl.SdlBinding);
                    return input;
                }
            }

            Input ret = new Input() { Name = input.Name };

            if (sdlret.Button != SDL_CONTROLLER_BUTTON.INVALID)
            {
                ret.Type = "button";
                ret.Id = (int)sdlret.Button;
                ret.Value = 1;
                return ret;
            }

            if (sdlret.Axis != SDL_CONTROLLER_AXIS.INVALID)
            {
                ret.Type = "axis";
                ret.Id = (int)sdlret.Axis;
                ret.Value = axisValue;
                return ret;
            }

            return GetXInputInput(key);
        }

        /// <summary>
        /// Translate EmulationStation Input to XInput Input
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
        public Input GetXInputInput(InputKey key)
        {
            if (Config == null)
                return null;

            Input input = Config[key];
            if (input == null)
                return null;

            if (input.Type == "key")
                return input;

            if (!this.IsXInputDevice)
                return input;

            Input ret = new Input();
            ret.Name = input.Name;
            ret.Type = input.Type;
            ret.Id = input.Id;
            ret.Value = input.Value;

            // Inverstion de start et select
            if (input.Type == "button" && input.Id == 6)
                ret.Id = 7;
            else if (input.Type == "button" && input.Id == 7)
                ret.Id = 6;

            if (input.Type == "axis" && ret.Id == 1 || ret.Id == 3) // up/down axes are inverted
                ret.Value = -ret.Value;

            return ret;
        }

        /// <summary>
        /// Translate EmulationStation to XInput Mapping
        /// </summary>
        /// <param name="key"></param>
        /// <param name="revertAxis"></param>
        /// <returns></returns>
        public XINPUTMAPPING GetXInputMapping(InputKey key, bool revertAxis = false)
        {
            if (Config == null)
                return XINPUTMAPPING.UNKNOWN;

            Input input = Config[key];
            if (input == null)
                return XINPUTMAPPING.UNKNOWN;

            if (input.Type == "key")
                return XINPUTMAPPING.UNKNOWN;

            if (!IsXInputDevice)
                return XINPUTMAPPING.UNKNOWN;

            if (input.Type == "button")
                return (XINPUTMAPPING)input.Id;

            if (input.Type == "hat")
                return (XINPUTMAPPING)(input.Value + 10);

            if (input.Type == "axis")
            {
                switch (input.Id)
                {
                    case 2:
                        if ((!revertAxis && input.Value > 0) || (revertAxis && input.Value < 0))
                            return XINPUTMAPPING.RIGHTANALOG_RIGHT;

                        return XINPUTMAPPING.RIGHTANALOG_LEFT;

                    case 5:
                        return XINPUTMAPPING.RIGHTTRIGGER;

                    case 0:
                        if ((!revertAxis && input.Value > 0) || (revertAxis && input.Value < 0))
                            return XINPUTMAPPING.LEFTANALOG_RIGHT;

                        return XINPUTMAPPING.LEFTANALOG_LEFT;

                    case 1:
                        if ((!revertAxis && input.Value > 0) || (revertAxis && input.Value < 0))
                            return XINPUTMAPPING.LEFTANALOG_DOWN;

                        return XINPUTMAPPING.LEFTANALOG_UP;

                    case 4:
                        return XINPUTMAPPING.LEFTTRIGGER;

                    case 3:
                        if ((!revertAxis && input.Value > 0) || (revertAxis && input.Value < 0))
                            return XINPUTMAPPING.RIGHTANALOG_DOWN;

                        return XINPUTMAPPING.RIGHTANALOG_UP;
                }
            }

            return XINPUTMAPPING.UNKNOWN;
        }

        /// <summary>
        /// Translate EmulationStation input to XInput button flags
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
        public XInputButtonFlags GetXInputButtonFlags(InputKey key)
        {
            XInputButtonFlags result;
            if (Enum.TryParse<XInputButtonFlags>(GetXInputInput(key).ToString(), out result))
                return result;

            return XInputButtonFlags.NONE;
        }

        public Input GetDirectInputMapping(InputKey key)
        {
            if (Config == null)
                return null;

            Input input = Config[key];
            if (input == null)
                return null;

            var guid = this.Guid.FromSdlGuidString();
            if (guid.GetWrappedTechID() == SdlWrappedTechId.HID)
            {
                var dinput = HidToDirectInput.Instance.FromInput(guid, input);
                if (dinput != null)
                    return dinput;
            }

            return input;
        }
    }

    static class InputExtensions
    {
        private static Dictionary<InputKey, InputKey> revertedAxis = new Dictionary<InputKey, InputKey>()
        {
            { InputKey.joystick1right, InputKey.joystick1left },
            { InputKey.joystick1down, InputKey.joystick1up },
            { InputKey.joystick2right, InputKey.joystick2left },
            { InputKey.joystick2down, InputKey.joystick2up },
        };

        public static InputKey GetRevertedAxis(this InputKey key, out bool reverted)
        {
            reverted = false;

            InputKey revertedKey;
            if (revertedAxis.TryGetValue(key, out revertedKey))
            {
                key = revertedKey;
                reverted = true;
            }

            return key;
        }
    }
}