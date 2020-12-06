using Caliburn.Micro;
using Emgu.CV;
using Emgu.CV.CvEnum;
using Emgu.CV.Structure;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Timers;
using ZXing;
using System.Windows.Media.Imaging;
using System.Net;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Collections.ObjectModel;
using System.Data.SQLite;
using System.Data;
using System.Windows.Input;
using System.Linq;

namespace QRDetection
{
    class MainViewModel:PropertyChangedBase
    {
        private readonly VideoCapture _webcamCapture;

        private readonly BarcodeReader barcode;

        private Timer _captureTimer;

        private Timer _qrStaticReader_Timer;

        private Timer _qrDynamicReader_Timer;

        private Timer _dorStateChange;

        private int dorDelay = 3;

        private const string CheckCode = "sg123#98/!8yush";

        private readonly SQLiteConnection UsersDBConnection = new SQLiteConnection($@"Data Source=|DataDirectory|\DataBase\usersdata.db; Version=3;datetimeformat=CurrentCulture");

        private BitmapImage _webcamimage;
        public BitmapImage WebCamImage
        {
            get => _webcamimage;
            set
            {
                if (_webcamimage != value)
                {
                    _webcamimage = value;
                    NotifyOfPropertyChange(() => WebCamImage);
                }
            }
        }

        private ObservableCollection<UserModel> _dataUsers;
        public ObservableCollection<UserModel> DataUsers
        {
            get => _dataUsers;
            set
            {
                if(_dataUsers != value)
                {
                    _dataUsers = value;
                    NotifyOfPropertyChange(() => DataUsers);
                }
            }
        }

        private Image<Bgr, byte> Frame;

        private string _text;
        public string Text
        {
            get => _text;
            set
            {
                if (_text != value)
                {
                    _text = value;
                    NotifyOfPropertyChange(() => Text);
                }
            }
        }

        private string _usertext;
        public string UserText
        {
            get => _usertext;
            set
            {
                if (_usertext != value)
                {
                    _usertext = value;
                    NotifyOfPropertyChange(() => UserText);
                }
            }
        }

        private bool _webCamState = true;
        public bool WebCamState
        {
            get => _webCamState;
            set
            {
                if (_webCamState != value)
                {
                    _webCamState = value;
                    NotifyOfPropertyChange(() => WebCamState);
                }
            }
        }

        private bool _accessstate;
        public bool AccessState
        {
            get => _accessstate;
            set
            {
                if (_accessstate != value)
                {
                    _accessstate = value;
                    NotifyOfPropertyChange(() => AccessState);
                }
            }
        }

        private bool _systemready = false;
        public bool SystemReady
        {
            get => _systemready;
            set
            {
                if (_systemready != value)
                {
                    _systemready = value;
                    NotifyOfPropertyChange(() => SystemReady);
                }
            }
        }

        private QrTypEnum _qrTyp;

        public MainViewModel()
        {
            barcode = new BarcodeReader();

            _webcamCapture = new VideoCapture();

            _webcamCapture.SetCaptureProperty(CapProp.Buffersize, 3);
            _webcamCapture.SetCaptureProperty(CapProp.FrameHeight, 200);
            _webcamCapture.SetCaptureProperty(CapProp.FrameWidth, 200);

            _captureTimer = new Timer
            {
                Interval = 100,
                AutoReset = true

            };
            _captureTimer.Elapsed += _captureTimer_Elapsed;
            _captureTimer.Start();

            _dorStateChange = new Timer
            {
                Interval = 1000,
                AutoReset = true

            };
            _dorStateChange.Elapsed += _dorStateChange_Elapsed;
            _dorStateChange.Start();
        }

        public ICommand StaticQrDetectCommand
        {
            get
            {
                return new RelayCommand(sender =>
                {
                    _qrStaticReader_Timer = new Timer
                    {
                        Interval = 1000,
                        AutoReset = true
                    };
                    _qrStaticReader_Timer.Elapsed += _qrStaticReader_Timer_Elapsed;
                    _qrStaticReader_Timer.Start();
                    DataUsers = AsyncUserLoad().Result;
                    _qrTyp = QrTypEnum.StaticQr;
                    SystemReady = true;
                });
            }       
        }

        public ICommand DynamicQrDetectCommand
        {
            get
            {
                return new RelayCommand(sender =>
                {
                    _qrDynamicReader_Timer = new Timer
                    {
                        Interval = 1000,
                        AutoReset = true
                    };
                    _qrDynamicReader_Timer.Elapsed += _qrDynamicReader_Timer_Elapsed;
                    _qrDynamicReader_Timer.Start();
                    _qrTyp = QrTypEnum.DynamicQr;
                    SystemReady = true;
                });
            }
        }

        private void _qrStaticReader_Timer_Elapsed(object senser, ElapsedEventArgs e)
        {
            if (Frame != null)
            {
                Result result = barcode.Decode(Frame.ToBitmap());
                if (result != null)
                {
                    string qrdata = result.ToString();
                    qrdata.Trim();
                    if (DataUsers != null)
                    {
                        if (DataUsers.Any(u=>u.IdentificateCode == qrdata)) 
                        {
                            Text = "доступ разрешен";
                            UserText = $"добро пожаловать {DataUsers.Where(u => u.IdentificateCode == qrdata).First().Login}";
                            AccessState = true;
                        }
                        else 
                        {
                            Text = "доступ запрещен";
                            UserText = $"пользователь не найден";
                            AccessState = false;
                        }
                    }
               
                }
                else
                {
                    Text = "qr-код не найден";
                    UserText = null;
                }
            }
        }

        private void _qrDynamicReader_Timer_Elapsed(object senser, ElapsedEventArgs e)
        {
            if (Frame != null)
            {
                Result result = barcode.Decode(Frame.ToBitmap());
                if (result != null)
                {
                    string url = result.ToString();
                    if (url.Contains("https"))
                    {
                        string qrcode = GetTextFromQrUrl(url);
                        if (qrcode == CheckCode)
                        {
                            Text = "доступ разрешен";
                            AccessState = true;
                        }
                        else
                        {
                            Text = "доступ запрещен";
                            AccessState = false;
                        }
                    }
                    else Text = "bad qr data!";
                }
                else
                {
                    Text = "qr-код не найден";
                }
            }
        }

        private void _captureTimer_Elapsed(object senser, ElapsedEventArgs e)
        {
            if (_webcamCapture.IsOpened)
            {
                WebCamState = false;
                var frame = _webcamCapture.QueryFrame().ToImage<Bgr, byte>();
                if (frame != null)
                {
                    Frame = frame;
                    WebCamImage = BitmapToImageSource(frame.ToBitmap());
                }
            }
            else WebCamState = true;
        }

        private void _dorStateChange_Elapsed(object senser, ElapsedEventArgs e) 
        {
            if (AccessState)
            {
                if (dorDelay == 0)
                {
                    dorDelay = 5;
                    AccessState = false;
                }
                else dorDelay--;
            }
        }
        private string GetTextFromQrUrl(string url)
        {
            HttpWebRequest myHttwebrequest = (HttpWebRequest)HttpWebRequest.Create(url);
            HttpWebResponse myHttpWebresponse = (HttpWebResponse)myHttwebrequest.GetResponse();
            StreamReader strm = new StreamReader(myHttpWebresponse.GetResponseStream());
            string HtmlText = strm.ReadToEnd();
            HtmlText = Regex.Split(HtmlText, "</h1>")[0];
            HtmlText = Regex.Split(HtmlText, "<h1>")[1];
            return HtmlText.Trim();
        }

        private BitmapImage BitmapToImageSource(Bitmap bitmap)
        {
            using (MemoryStream memory = new MemoryStream())
            {
                bitmap.Save(memory, ImageFormat.Bmp);
                memory.Position = 0;
                var bitmapimage = new BitmapImage();
                bitmapimage.BeginInit();
                bitmapimage.StreamSource = memory;
                bitmapimage.CacheOption = BitmapCacheOption.OnLoad;
                bitmapimage.EndInit();
                bitmapimage.Freeze();
                return bitmapimage;
            }
        }

        public Task<ObservableCollection<UserModel>> AsyncUserLoad()
        {
            return Task.Run(() =>
            {
                var dbdata = new ObservableCollection<UserModel>();

                if (UsersDBConnection.State == ConnectionState.Closed)
                {
                    UsersDBConnection.Open();
                }
                if (UsersDBConnection.State == ConnectionState.Open)
                {
                    var read = new SQLiteCommand();
                    read = new SQLiteCommand($"SELECT * FROM 'Users' ", UsersDBConnection);

                    using (SQLiteDataReader reader = read.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            dbdata.Add(new UserModel
                            {
                                Login = reader.GetString(1),
                                IdentificateCode = reader.GetString(2),
                            });
                        }
                    }
                    UsersDBConnection.Close();
                }
                return dbdata;
            });
        }

        public void Closing()
        {
            _captureTimer.Stop();
            _dorStateChange.Stop();
            switch (_qrTyp)
            {
                case QrTypEnum.DynamicQr:
                    _qrDynamicReader_Timer.Stop();
                    break;
                case QrTypEnum.StaticQr:
                    _qrStaticReader_Timer.Stop();
                    break;
            }
            _webcamCapture.Stop();
            _webcamCapture.Dispose();
        }
    }
}
