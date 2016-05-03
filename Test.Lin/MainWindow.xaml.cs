using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace Test.Lin
{
    /// <summary>
    /// MainWindow.xaml에 대한 상호 작용 논리
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();

            DispatcherTimer timer = new DispatcherTimer()
            {
                Interval = new TimeSpan(100 * 10000),
            };
            timer.Tick += Transfer_Tick;
            timer.Start();

            if (_cbDevices.Items.Count > 0)
                _cbDevices.SelectedIndex = 0;
        }

        private void Transfer_Tick(object sender, EventArgs e)
        {
            var now = DateTime.Now.Ticks;

            if (LinManager.IsRxError)
            {
                _rxLamp.Background = (Brush)this.Resources["ramp_error"];
            }
            else
            {
                _rxLamp.Background = LinManager.RxTics > now ? 
                    (Brush)this.Resources["ramp_active"] : (Brush)this.Resources["ramp_idle"];
            }
            if (LinManager.IsTxError)
            {
                _txLamp.Background = (Brush)this.Resources["ramp_error"];
            }
            else
            {
                _txLamp.Background = LinManager.TxTics > now ?
                    (Brush)this.Resources["ramp_active"] : (Brush)this.Resources["ramp_idle"];
            }
        }

        private void refresh_Click(object sender, RoutedEventArgs e)
        {
            Controller.RefreshDevice();
        }

        internal ControllerModel Controller { get { return (ControllerModel)this.DataContext; } }

        private void open_click(object sender, RoutedEventArgs e)
        {
            Controller.LinMgr.WriteCommand(LinManager.DOOR_OPEN);
        }

        private void close_click(object sender, RoutedEventArgs e)
        {
            Controller.LinMgr.WriteCommand(LinManager.DOOR_STOP);
        }

        private void readstate_click(object sender, RoutedEventArgs e)
        {
            Controller.LinMgr.ReadStateLoop(2);
        }

        private void _cbDevices_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            Controller.LinMgr.Device = (LinDevice)_cbDevices.SelectedItem;
        }

        private void _gridLog_Loaded(object sender, RoutedEventArgs e)
        {
            _gridLog.ItemsSource = Controller.LogsData;
        }
    }
}
