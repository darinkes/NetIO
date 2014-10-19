using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Net.NetworkInformation;
using System.Windows.Threading;
using OxyPlot;
using OxyPlot.Axes;
using OxyPlot.Series;
using PropertyChanged;

namespace NetIO
{
    [ImplementPropertyChanged]
    class MainWindowViewModel
    {
        public ObservableCollection<string> Interfaces { get; set; }

        public DateTime LastUpdate { get; set; }

        public long Sent { get; set; }

        public long Received { get; set; }

        private long _sentspeed;

        public long TopSentSpeed { get; set; }

        public long TopReceivedSpeed { get; set; }

        public long SentSpeed
        {
            get { return _sentspeed; }
            set
            {
                _sentspeed = value;
                if (_sentspeed > TopSentSpeed)
                    TopSentSpeed = _sentspeed;
            }
        }

        private long _receivedspeed;

        public long ReceivedSpeed
        {
            get { return _receivedspeed; }
            set
            {
                _receivedspeed = value;
                if (_receivedspeed > TopReceivedSpeed)
                    TopReceivedSpeed = _receivedspeed;
            }
        }

        private string _selectedItem;

        public string SelectedItem
        {
            get { return _selectedItem; }
            set
            {
                if (value == _selectedItem)
                    return;

                _selectedItem = value;
                Refresh();
            }
        }

        public PlotModel Model { get; set; }

        private readonly LineSeries _sentseries;

        private readonly LineSeries _receivedSeries;

        private readonly DateTimeAxis _dateTimeAxis;

        private readonly LinearAxis _valueAxis;

        private readonly TimeSpan _maxTimeSpan = TimeSpan.FromSeconds(300);

        private long? _oldsentvalue;
        private long? _oldreceivedvalue;
        private readonly DispatcherTimer _timer;

        private double _oldscale;

        public MainWindowViewModel()
        {
            Interfaces = new ObservableCollection<string>();
            var adapters = NetworkInterface.GetAllNetworkInterfaces();
            foreach (var adapter in adapters)
            {
                Interfaces.Add(adapter.Description);
            }

            SelectedItem = Interfaces.FirstOrDefault();

            Model = new PlotModel
            {
                Title = "Net I/O",
                LegendTitle = "Legend",
                LegendOrientation = LegendOrientation.Horizontal,
                LegendPlacement = LegendPlacement.Outside,
                LegendPosition = LegendPosition.TopRight,
                LegendBackground = OxyColor.FromAColor(200, OxyColors.White),
                LegendBorder = OxyColors.Black
            };

            _dateTimeAxis = new DateTimeAxis
            {
                Position = AxisPosition.Bottom,
                Minimum = DateTimeAxis.ToDouble(DateTime.Now),
                Maximum = Axis.ToDouble(DateTime.Now + _maxTimeSpan),
                IntervalType = DateTimeIntervalType.Seconds,
                MajorGridlineStyle = LineStyle.Solid,
                StringFormat = "HH:mm:ss",
                MajorStep = 1.0 / 24 / 120, // 1/24 = 1 hour, 1/24/120 = 0.5 minutes
                IsZoomEnabled = true,
                MaximumPadding = 0,
                MinimumPadding = 0,
                TickStyle = TickStyle.Outside
            };

            Model.Axes.Add(_dateTimeAxis);

            _valueAxis = new LinearAxis
            { 
                Position = AxisPosition.Left,
                Minimum = 0,
                MajorGridlineStyle = LineStyle.Solid,
                MinorGridlineStyle = LineStyle.Dot,
                Title = "Speed",
                Unit = "B/s",
                TickStyle = TickStyle.Crossing,
            };

            Model.Axes.Add(_valueAxis);

            _sentseries = new LineSeries
            {
                StrokeThickness = 2,
                Color = OxyColors.DarkCyan,
                Title = "Sent"
            };

            Model.Series.Add(_sentseries);

            _receivedSeries = new LineSeries
            {
                StrokeThickness = 2,
                Color = OxyColors.DarkOrange,
                Title = "Received"
            };

            Model.Series.Add(_receivedSeries);

            _timer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(1)
            };
            _timer.Tick += timer_Tick;
            _timer.Start();
        }

        void timer_Tick(object sender, EventArgs e)
        {
            if (string.IsNullOrEmpty(SelectedItem))
                return;

            lock (Model.SyncRoot)
            {
                var adapters = NetworkInterface.GetAllNetworkInterfaces();
                foreach (var adapter in adapters)
                {
                    if (adapter.Description != SelectedItem)
                        continue;

                    if (!_oldsentvalue.HasValue || !_oldreceivedvalue.HasValue)
                    {
                        _oldsentvalue = adapter.GetIPv4Statistics().BytesSent;
                        _oldreceivedvalue = adapter.GetIPv4Statistics().BytesReceived;
                        continue;
                    }

                    var cursentvalue = adapter.GetIPv4Statistics().BytesSent;
                    _sentseries.Points.Add(new DataPoint(DateTimeAxis.ToDouble(DateTime.Now), cursentvalue - _oldsentvalue.Value));
                    Sent = cursentvalue;
                    SentSpeed = cursentvalue - _oldsentvalue.Value;
                    _oldsentvalue = cursentvalue;

                    var curreceivedvalue = adapter.GetIPv4Statistics().BytesReceived;
                    _receivedSeries.Points.Add(new DataPoint(DateTimeAxis.ToDouble(DateTime.Now), curreceivedvalue - _oldreceivedvalue.Value));
                    Received = curreceivedvalue;
                    ReceivedSpeed = curreceivedvalue - _oldreceivedvalue.Value;
                    _oldreceivedvalue = curreceivedvalue;

                    if (_dateTimeAxis.Maximum < DateTimeAxis.ToDouble(DateTime.Now))
                    {
                        _dateTimeAxis.Maximum = DateTimeAxis.ToDouble(DateTime.Now);
                        _dateTimeAxis.Minimum = DateTimeAxis.ToDouble(DateTime.Now - _maxTimeSpan);

                        _sentseries.Points.RemoveAll(d => d.X < DateTimeAxis.ToDouble(DateTime.Now - _maxTimeSpan));
                        _receivedSeries.Points.RemoveAll(d => d.X < DateTimeAxis.ToDouble(DateTime.Now - _maxTimeSpan));
                    }

                    SetScale();
                }
            }
            LastUpdate = DateTime.Now;
            Model.InvalidatePlot(true);
        }


        internal void Refresh()
        {
            if (Model == null)
                return;

            lock (Model.SyncRoot)
            {
                if (_timer != null && _timer.IsEnabled)
                    _timer.Stop();
                _sentseries.Points.Clear();
                _receivedSeries.Points.Clear();
                Model.InvalidatePlot(true);
                _oldreceivedvalue = null;
                _oldsentvalue = null;
                TopReceivedSpeed = 0;
                TopSentSpeed = 0;
                Sent = 0;
                Received = 0;
                ReceivedSpeed = 0;
                SentSpeed = 0;
                if (_timer != null && !_timer.IsEnabled)
                    _timer.Start();
            }
        }

        private int RoundOff(double i)
        {
            if (Equals(i, 0.0))
                return 1;
            i += 5;
            var ret = ((int)Math.Round(i / 10.0)) * 10;
            if (Equals(ret, 0))
                ret = 1;
            return ret;
        }

        private void SetScale()
        {
            var maxsent = _sentseries.Points.Max(d => d.Y);
            var maxreceived = _receivedSeries.Points.Max(d => d.Y);
            var maxvalue = maxsent > maxreceived ? maxsent : maxreceived;

            if (maxvalue.Equals(_oldscale))
                return;

            if (_sentseries.Points.Any(d => d.Y >= 1048576) || _receivedSeries.Points.Any(d => d.Y >= 1048576))
            {
                _valueAxis.FormatAsFractions = true;
                _valueAxis.FractionUnit = 1048576;
                //valueAxis.FractionUnitSymbol = " MB/s";
                _valueAxis.Unit = "MB/s";
                _valueAxis.MajorStep = 1048576 * RoundOff(Math.Round(maxvalue / (1048576 * 10)));
            }
            else if (_sentseries.Points.Any(d => d.Y >= 1024) || _receivedSeries.Points.Any(d => d.Y >= 1024))
            {
                _valueAxis.FormatAsFractions = true;
                _valueAxis.FractionUnit = 1024;
                //valueAxis.FractionUnitSymbol = " KB/s";
                _valueAxis.Unit = "KB/s";
                _valueAxis.MajorStep = 1024 * RoundOff(Math.Round(maxvalue / (1024 * 10)));
            }
            else
            {
                _valueAxis.FormatAsFractions = true;
                _valueAxis.FractionUnit = 1;
                //valueAxis.FractionUnitSymbol = " B/s";
                _valueAxis.Unit = "B/s";
                _valueAxis.MajorStep = RoundOff(Math.Round(maxvalue / 10));
            }

            _oldscale = maxvalue;
        }
    }
}
