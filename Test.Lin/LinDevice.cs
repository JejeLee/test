using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Test.Lin
{
    public class LinDevice
    {
        public LinDevice(ushort aHwHandle)
        {
            this.HwHandle = aHwHandle;
            this.Connected = false;
        }

        public ushort HwHandle { get; private set; }
        public string HwType { get { return m_strHwType; } }
        public int DevNo { get; set; }
        public int Channel { get; set; }

        public bool Connected { get; set; }

        public void SetHwType(int aHwType)
        {
            switch (aHwType)
            {
                case Peak.Lin.PLinApi.LIN_HW_TYPE_USB_PRO:
                    m_strHwType = "PCAN-USB Pro";
                    break;
                case Peak.Lin.PLinApi.LIN_HW_TYPE_USB_PRO_FD:
                    m_strHwType = "PCAN - USB Pro FD";
                    break;
                default:
                    m_strHwType = "(Unknown)";
                    break;
            }
        }

        public override string ToString()
        {
            return string.Format("{0} - D:{1} CH:{2}", m_strHwType, DevNo, Channel);
        }

        private string m_strHwType = "(Unknown)";
    }
}
