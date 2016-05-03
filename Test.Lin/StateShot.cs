using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Test.Lin
{
    public class StateShot
    {
        public StateShot()
        {
            Time = DateTime.Now;
        }

        public void SetBits(byte aValue)
        {
            DoorError = (aValue & 0x01) >  0;
            Test = (aValue & 0x02) > 0;
            DoorRun = (aValue & 0x04) > 0;
            DirectionOpen = (aValue & 0x08) > 0;
            DirectionClose = !DirectionOpen;
            LatchOn = (aValue & 0x10) > 0;
            ReleaseOn = (aValue & 0x20) > 0;
            Clutch = (aValue & 0x40) > 0;
        }

        public void SetState1(byte[] aValue)
        {
            SetBits(aValue[1]);

            this.SpeedM = (aValue[2] << 8) & aValue[3];
            this.SpeedR = (aValue[4] << 8) & aValue[5];
            this.Position = (aValue[6] << 8) & aValue[7];
        }

        public void SetState2(byte[] aValue)
        {
            this.MotorV = (aValue[0] << 8) & aValue[1];
            this.MotorA = (aValue[2] << 8) & aValue[3];
            this.DistanceF = aValue[4];
            this.DistanceR = (aValue[5] << 8) & aValue[6];
        }

        public override string ToString()
        {
            return string.Format("DoorRun:{0}, DirectionOpen:{1}, MotorV:{2}, MotorA:{3}, DoorAngle:{4}", 
                DoorRun, DirectionOpen, MotorV, MotorA, DoorAngle);
        }

        public DateTime Time { get; private set; }
        public bool DoorError { get; set; }
        public bool DoorRun { get; set; }
        public bool DirectionOpen { get; set; }
        public bool DirectionClose { get; set; }
        public bool LatchOn { get; set; }
        public bool ReleaseOn { get; set; }
        public bool Clutch { get; set; }
        public bool Test { get; set; }

        public int SpeedM { get; set; }
        public int SpeedR { get; set; }
        public int DoorAngle { get; set; }
        public int MotorV { get; set; }
        public int MotorA { get; set; }
        public int DistanceF { get; set; }
        public int DistanceR { get; set; }
        public int Position { get; set; }

    }
}
