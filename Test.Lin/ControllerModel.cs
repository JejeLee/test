using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.ComponentModel;

namespace Test.Lin
{
    internal class ControllerModel : INotifyPropertyChanged
    {
        public ControllerModel()
        {
            Log.LogEvent += LogReceived;

            _mmodel = new ManualModel(this);
            _linmgr = new LinManager();

            _linmgr.RefreshHardware();            
        }

        private void LogReceived(LogData aData)
        {
            _logs.Add(aData);
        }

        public event PropertyChangedEventHandler PropertyChanged;

        private void OnPropertyChanged(String propertyName)
        {
            if (PropertyChanged != null)
            {
                PropertyChanged(this, new PropertyChangedEventArgs(propertyName));
            }
        }

        public void RefreshDevice()
        {
            _linmgr.RefreshHardware();
            OnPropertyChanged("Devices");
        }

        public IEnumerable<LinDevice> Devices { get { return _linmgr.Devices; } }

        public ObservableCollection<LogData> LogsData { get { return _logs; } }

        public ManualModel Manual { get { return _mmodel; } }

        public LinManager LinMgr { get { return _linmgr; } }

        ObservableCollection<LogData> _logs = new ObservableCollection<LogData>();
        ObservableCollection<StateShot> _states = new ObservableCollection<StateShot>();
        ManualModel _mmodel;
        LinManager _linmgr;
    }
}
