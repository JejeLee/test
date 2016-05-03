using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Test.Lin
{
    // ADOS PID 값들
    public enum PID : byte {
        COMMAND = 0xC1,
        STATE1 = 0xB1,
        STATE2 = 0x32,
        WR_ADDR = 0x73,
        RD_ADDR = 0xB4,
        ADDR_REQUEST = 0xF5
    };

    public delegate void StateReceivedHandler(StateShot aShot);
    public delegate void ParameterReceivedHandler(int aAddr, int aValue);

    public sealed class LinManager
    {
        public LinManager()
        {
            UnderLoopJob = false;
            RefreshHardware();
        }

        public bool WriteCommand(params byte[] aData)
        {
            Log.i("Lin/Command>> {0}", (aData != null && aData.Length > 0) ? aData[0] : 0 );

            return WriteMessage(PID.COMMAND, aData);
        }

        public bool ReadState(out StateShot aShot)
        {
            aShot = null;

            WriteMessage(PID.STATE1);
            Peak.Lin.TLINRcvMsg rmsg; ;
            if (!ReadMessages(out rmsg, 50))
            {
                return false;
            }
            aShot = new StateShot();
            aShot.SetState1(rmsg.Data);
#if TYPE_B
            WriteMessage(PID.STATE2);
            Peak.Lin.TLINRcvMsg rmsg; ;
            if (!ReadMessages(out rmsg, 50))
            {
                return false;
            }
            aShot.SetState1(rmsg.Data);
#endif

            return true;
        }

        public bool ReadStateLoop(int aPeriodMS)
        {
            UnderLoopJob = true;
            
            try
            {
                var watch = new System.Diagnostics.Stopwatch();
                watch.Start();

                while (watch.ElapsedMilliseconds < aPeriodMS && !_stopLoopJob)
                {
                    StateShot shot;
                    if (ReadState(out shot))
                    {
                        InvokeStateReceived(shot);
                    }
                    else
                    {
                        //return false;
                    }
                    //System.Threading.Thread.Sleep(30);
                } 
                watch.Stop();
            }
            finally
            {
                UnderLoopJob = false;
            }
            return true;
        }

        public void ReadStateLoopAsync(int aPeriodMS)
        {
            Task.Run( () => { ReadStateLoop(aPeriodMS); } );
        }

        public bool WriteParameter(int aAddr, int aValue)
        {
            byte high = (byte)((aValue >> 8) & 0xFF);
            byte low = (byte)(aValue & 0xFF);

            Log.i("Lin/Parameter>> {0}={1}", aAddr, aValue);

            if (!WriteMessage(PID.WR_ADDR, high, low))
            {
                return false;
            }

            return true;
        }

        public void WriteParameters(IEnumerable<KeyValuePair<int, int>> aList)
        {
            UnderLoopJob = true;

            try
            {
                foreach (var item in aList)
                {
                    if (_stopLoopJob)
                    {
                        break;
                    }

                    if (!WriteParameter(item.Key, item.Value))
                    {
                        //return false;
                    }
                    //System.Threading.Thread.Sleep(30);
                }
            }
            finally
            {
                UnderLoopJob = false;
            }
        }

        public void WriteParametersAsync(IEnumerable<KeyValuePair<int, int>> aList)
        {
            Task.Run(() => { WriteParameters(aList); });
        }

        public bool ReadParameter(int aAddr)
        {
            if (!WriteMessage(PID.RD_ADDR))
            {
                return false;
            }

            Peak.Lin.TLINRcvMsg rmsg;
            if (!ReadMessages(out rmsg, 200))
            {
                return false;
            }

            int value = (rmsg.Data[1] << 8) | rmsg.Data[2];
            InvokeParameterReceived(rmsg.Data[0], value);

            return true;
        }

        public void ReadParameters(IEnumerable<int> aAddrs)
        {
            UnderLoopJob = true;

            try
            {
                foreach (var item in aAddrs)
                {
                    if (_stopLoopJob)
                    {
                        break;
                    }

                    if (!ReadParameter(item))
                    {
                        //return false;
                    }
                    //System.Threading.Thread.Sleep(30);
                }
            }
            finally
            {
                UnderLoopJob = false;
            }
        }

        public void ReadParametersAsync(IEnumerable<int> aAddrs)
        {
            Task.Run(() => { ReadParameters(aAddrs); });
        }

        //
        // TODO: read parameter, enumerable read/write
        //       read parameter event.

        public int RefreshHardware()
        {
            _devices.Clear();
            this._dev = null;

            // Get the buffer length needed...
            ushort lwCount = 0;
            _err = Peak.Lin.PLinApi.GetAvailableHardware(new ushort[0], 0, out lwCount);
            if (_err != Peak.Lin.TLINError.errOK || lwCount == 0)
                return 0;

            var lwHwHandles = new ushort[lwCount];
            var lwBuffSize = Convert.ToUInt16(lwCount * sizeof(ushort));

            // Get all available LIN hardware.
            _err = Peak.Lin.PLinApi.GetAvailableHardware(lwHwHandles, lwBuffSize, out lwCount);
            if (_err != Peak.Lin.TLINError.errOK || lwCount == 0)
            {
                return 0;
            }

            for (int i = 0; i < lwCount; i++)
            {
                int lnHwType, lnDevNo, lnChannel;
                // Get the handle of the hardware.
                var dev = new LinDevice(lwHwHandles[i]);
                // Read the type of the hardware with the handle lwHw.
                Peak.Lin.PLinApi.GetHardwareParam(dev.HwHandle, Peak.Lin.TLINHardwareParam.hwpType, out lnHwType, 0);
                dev.SetHwType(lnHwType);
                // Read the device number of the hardware with the handle lwHw.
                Peak.Lin.PLinApi.GetHardwareParam(dev.HwHandle, Peak.Lin.TLINHardwareParam.hwpDeviceNumber, out lnDevNo, 0);
                dev.DevNo = lnDevNo;
                // Read the channel number of the hardware with the handle lwHw.
                Peak.Lin.PLinApi.GetHardwareParam(dev.HwHandle, Peak.Lin.TLINHardwareParam.hwpChannelNumber, out lnChannel, 0);
                dev.Channel = lnChannel;

                _devices.Add(dev);
            }

            return _devices.Count();
        }

        public List<LinDevice> _devices = new List<LinDevice>();

        public bool Connect(int aBaudrate = 19200, Peak.Lin.TLINHardwareMode aMode = Peak.Lin.TLINHardwareMode.modMaster)
        {
            if (this.Device == null)
            {
                if (RefreshHardware() <= 0)
                    return false;
            }

            Disconnect();

            m_wBaudrate = aBaudrate;
            m_HwMode = aMode;

           if (m_hClient == 0)
                // Register this application with LIN as client.
                _err = Peak.Lin.PLinApi.RegisterClient(LinManager.ClientName, IntPtr.Zero, out m_hClient);

            int lnMode;
            int lnCurrBaud;
            // The local hardware handle is valid.
            // Get the current mode of the hardware
            Peak.Lin.PLinApi.GetHardwareParam(Device.HwHandle, Peak.Lin.TLINHardwareParam.hwpMode, out lnMode, 0);
            // Get the current baudrate of the hardware
            Peak.Lin.PLinApi.GetHardwareParam(Device.HwHandle, Peak.Lin.TLINHardwareParam.hwpBaudrate, out lnCurrBaud, 0);
            // Try to connect the application client to the
            // hardware with the local handle.
            Device.Connected = false;
            _err = Peak.Lin.PLinApi.ConnectClient(m_hClient, Device.HwHandle);
            if (_err == Peak.Lin.TLINError.errOK)
            {                
                // Get the selected hardware channel
                if (((Peak.Lin.TLINHardwareMode)lnMode == Peak.Lin.TLINHardwareMode.modNone)
                            || m_wBaudrate != lnCurrBaud)
                {
                    // Only if the current hardware is not initialize
                    // try to Intialize the hardware with mode and baudrate
                    _err = Peak.Lin.PLinApi.InitializeHardware(m_hClient, Device.HwHandle, m_HwMode, (ushort)m_wBaudrate);
                }
                if (_err == Peak.Lin.TLINError.errOK)
                {
                    Device.Connected = true;
                    Log.i("Connected - {0}", this.Device);
                    return true;
                }

            }

            Log.e("Connection Error({0}) - {1}", this.Device, GetFormatedError(_err));
            return false;
        }

        private bool Disconnect()
        {
            if (this.IsConnected == false)
                return true;

            // If the application was registered with LIN as client.
            if (Device.HwHandle != 0)
            {
                // The client was connected to a LIN hardware.
                // Before disconnect from the hardware check
                // the connected clients and determine if the
                // hardware configuration have to reset or not.

                // Initialize the locale variables.
                bool lfOtherClient = false;
                bool lfOwnClient = false;
                byte[] lhClients = new byte[255];

                // Get the connected clients from the LIN hardware.
                _err = Peak.Lin.PLinApi.GetHardwareParam(Device.HwHandle, Peak.Lin.TLINHardwareParam.hwpConnectedClients, lhClients, 255);
                if (_err == Peak.Lin.TLINError.errOK)
                {
                    // No errors !
                    // Check all client handles.
                    for (int i = 0; i < lhClients.Length; i++)
                    {
                        // If client handle is invalid
                        if (lhClients[i] == 0)
                            continue;
                        // Set the boolean to true if the handle isn't the
                        // handle of this application.
                        // Even the boolean is set to true it can never
                        // set to false.
                        lfOtherClient = lfOtherClient || (lhClients[i] != m_hClient);
                        // Set the boolean to true if the handle is the
                        // handle of this application.
                        // Even the boolean is set to true it can never
                        // set to false.
                        lfOwnClient = lfOwnClient || (lhClients[i] == m_hClient);
                    }
                }
                // If another application is also connected to
                // the LIN hardware do not reset the configuration.
                if (lfOtherClient == false)
                {
                    // No other application connected !
                    // Reset the configuration of the LIN hardware.
                    Peak.Lin.PLinApi.ResetHardwareConfig(m_hClient, Device.HwHandle);
                }
                // If this application is connected to the hardware
                // then disconnect the client. Otherwise not.
                if (lfOwnClient == true)
                {
                    // Disconnect if the application was connected to a LIN hardware.
                    _err = Peak.Lin.PLinApi.DisconnectClient(m_hClient, Device.HwHandle);
                    if (_err != Peak.Lin.TLINError.errOK)
                    {
                        return false;
                    }
                    Device.Connected = false;
                    Log.i("Disconnected - {0}", this.Device);
                }               
            }

            if (Device.Connected)
                Log.e("Disconnection Error({0}) - {1}", this.Device, GetFormatedError(_err));

            return !Device.Connected;
        }

        public void BlinkDevice(LinDevice aDevice)
        {
            if (aDevice != null)
            {
                Log.i("blink device lamp: dev:{0}, ch:{1}", aDevice.DevNo, aDevice.Channel);
                // makes the corresponding PCAN-USB-Pro's LED blink
                Peak.Lin.PLinApi.IdentifyHardware(aDevice.HwHandle);
            }
        }

        static private string GetFormatedError(Peak.Lin.TLINError error)
        {
            StringBuilder sErrText = new StringBuilder(255);
            // If any error are occured
            // display the error text in a message box.
            // 0x00 = Neutral
            // 0x07 = Language German
            // 0x09 = Language English
            if (Peak.Lin.PLinApi.GetErrorText(error, 0x09, sErrText, 255) != Peak.Lin.TLINError.errOK)
                return string.Format("An error occurred. Error-code's text ({0}) couldn't be retrieved", error);
            return sErrText.ToString();
        }

        public string GetDeviceStatus(LinDevice aDevice)
        {
            Peak.Lin.TLINHardwareStatus lStatus;
            if (aDevice == null)
                return "0(Invalid LIN device)";
            // Retrieves the status of the LIN Bus and outputs its state in the information listView
            _err = Peak.Lin.PLinApi.GetStatus(aDevice.HwHandle, out lStatus);
            if (_err == Peak.Lin.TLINError.errOK)
                switch (lStatus.Status)
                {
                    case Peak.Lin.TLINHardwareState.hwsActive:
                        return "1Bus: Active";
                    case Peak.Lin.TLINHardwareState.hwsAutobaudrate:
                        return "2Hardware: Baudrate Detection";
                    case Peak.Lin.TLINHardwareState.hwsNotInitialized:
                        return "3Hardware: Not Initialized";
                    case Peak.Lin.TLINHardwareState.hwsShortGround:
                        return "4Bus - Line: Shorted Ground";
                    case Peak.Lin.TLINHardwareState.hwsSleep:
                        return "5Bus: Sleep";
                }
            return string.Format("0(Check Error - {0})", GetFormatedError(_err));
        }

        public bool IsConnected { get { return this.Device != null && this.Device.Connected; } }

        public bool IsLastError { get { return _err != Peak.Lin.TLINError.errOK; } }

        public LinDevice Device {
            get {
                return _dev;
            }
            set {
                if (value != _dev)
                {
                    if (_dev != null)
                        Disconnect();

                    _dev = value;
                    if (_dev != null)
                    {
                        BlinkDevice(_dev);
                        Connect();
                    }
                }
            } }
        private LinDevice _dev = null;

        public IEnumerable<LinDevice> Devices { get { return _devices; } }

        static public void StopLoopJob()
        {
            if (UnderLoopJob)
            {
                _stopLoopJob = true;
            }
        }
        private static bool _stopLoopJob = false;
        private static bool _underLoopJob = false;

        static public bool UnderLoopJob {
            get { return _underLoopJob;
            }
            private set {
                _underLoopJob = value;
                if (!_underLoopJob)
                    _stopLoopJob = false;
            }
        }

        static public event StateReceivedHandler StateReceivedEvent;

        static private void InvokeStateReceived(StateShot aShot)
        {
            Log.i("Lin/State<<" + aShot.ToString());
            if (StateReceivedEvent != null)
            {
                StateReceivedEvent(aShot);
            }
        }

        static public event ParameterReceivedHandler ParameterReceivedEvent;

        static private void InvokeParameterReceived(int aAddr,int aValue)
        {
            Log.i("Lin/Parameter<<{0}={1}", aAddr, aValue);
            if (ParameterReceivedEvent != null)
            {
                ParameterReceivedEvent(aAddr, aValue);
            }
        }

        // transmission rx, tx indicator
        public const int TransSurveyTime = 120 * 10000; // 1tic/100 nano sec = 000 ms
        static public long RxTics { get; private set; }
        static public long TxTics { get; private set; }
        static public bool IsRxError { get; private set; }
        static public bool IsTxError { get; private set; }

        public const int DOOR_OPEN = 1;
        public const int DOOR_STOP = 2;

        static Peak.Lin.TLINError Read(byte hClient, out Peak.Lin.TLINRcvMsg aMsg)
        {
            RxTics = DateTime.Now.Ticks + TransSurveyTime;

            Peak.Lin.TLINError err = Peak.Lin.PLinApi.Read(hClient, out aMsg);

            IsRxError = err != Peak.Lin.TLINError.errOK;
            if (IsRxError)
            {
                string emsg = GetFormatedError(err);
                Log.e("Lin Read:" + emsg);
            }
            
            return err;
        }

        static Peak.Lin.TLINError Write(byte hClient, ushort hHw, ref Peak.Lin.TLINMsg aMsg)
        {
            TxTics = DateTime.Now.Ticks + TransSurveyTime;

            Peak.Lin.TLINError err = Peak.Lin.PLinApi.Write(hClient, hHw, ref aMsg);

            IsTxError = err != Peak.Lin.TLINError.errOK;
            if (IsTxError)
            {
                string emsg = GetFormatedError(err); 
                Log.e("Lin Write:" + emsg);
            }

            return err;
        }

        private bool ReadMessages(out Peak.Lin.TLINRcvMsg aMsg, int aTimeout = 50 /* msec */)
        {
            int timeout = aTimeout;
            aMsg = new Peak.Lin.TLINRcvMsg();
            // We read at least one time the queue looking for messages.
            // If a message is found, we look again trying to find more.
            // If the queue is empty or an error occurs, we get out from
            // the dowhile statement.
            //	
            const int sleep = 20;
            var watch = new System.Diagnostics.Stopwatch();
            watch.Start();

            do
            {
                _err = LinManager.Read(m_hClient, out aMsg);
                // If at least one Frame is received by the LinApi.
                // Check if the received frame is a standard type.
                // If it is not a standard type than ignore it.
                if (aMsg.Type == Peak.Lin.TLINMsgType.mstStandard)
                {
                    if (_err == Peak.Lin.TLINError.errOK)
                    {
                        watch.Stop();
                        return true;
                    }
                }

                System.Threading.Thread.Sleep(sleep);

            } while ((_err == Peak.Lin.TLINError.errOK
                    || _err == Peak.Lin.TLINError.errRcvQueueEmpty)
                    && timeout > watch.ElapsedMilliseconds);

            watch.Stop();
            return false;
        }

        private bool WriteMessage(PID aPID, params byte[] aData)
        {
            //byte frameid = (byte)((byte)aPID & Peak.Lin.PLinApi.LIN_MAX_FRAME_ID);
            //Peak.Lin.PLinApi.GetPID(ref frameid);

            Peak.Lin.TLINMsg pMsg = new Peak.Lin.TLINMsg();
            pMsg.Data = new byte[8];
            //pMsg.FrameId = frameid;
            pMsg.FrameId = (byte)aPID;
            pMsg.Direction = Peak.Lin.TLINDirection.dirPublisher;
            pMsg.ChecksumType = Peak.Lin.TLINChecksumType.cstClassic;
            pMsg.Length = (byte)aData.Length;
            // Fill data array
            if (pMsg.Length == 0)
            {
                pMsg.Length = 1;
                pMsg.Data[0] = (byte)0xFF;
            }
            else
            {
                for (int i = 0; i < pMsg.Length; i++)
                {
                    pMsg.Data[i] = aData[i];
                }
            }
            // Check if the hardware is initialize as master
            if (m_HwMode == Peak.Lin.TLINHardwareMode.modMaster)
            {
                // Calculate the checksum contained with the
                // checksum type that set some line before.
                Peak.Lin.PLinApi.CalculateChecksum(ref pMsg);
                // Try to send the LIN frame message with LIN_Write.
                _err = LinManager.Write(m_hClient, Device.HwHandle, ref pMsg);
            }

            return !IsLastError;
        }

        /// <summary>
        /// Client handle
        /// </summary>
        private byte m_hClient = 0;
        /// <summary>
        /// LIN Hardware Modus (Master/Slave)
        /// </summary>
        private Peak.Lin.TLINHardwareMode m_HwMode = Peak.Lin.TLINHardwareMode.modNone;
        /// <summary>
        /// Baudrate Index of Hardware
        /// </summary>
        private int m_wBaudrate = 0;

        Peak.Lin.TLINError _err = Peak.Lin.TLINError.errOK;

        static public readonly string ClientName = "ADOS_LIN";
    }
}
