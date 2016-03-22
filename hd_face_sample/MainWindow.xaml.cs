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
using Microsoft.Kinect;
using Microsoft.Kinect.Face;
using OpenCvSharp;
using OpenCvSharp.Blob;
using OpenCvSharp.Extensions;

namespace hd_face_marker_tracking
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : System.Windows.Window
    {
        // Step 4. Declare the required objects
        // Provides a Kinect sensor reference.
        private KinectSensor _sensor = null;

        // Acquires body frame data.
        private BodyFrameSource _bodySource = null;

        // Reads body frame data.
        private BodyFrameReader _bodyReader = null;

        // Acquires HD face data.
        private HighDefinitionFaceFrameSource _faceSource = null;

        // Reads HD face data.
        private HighDefinitionFaceFrameReader _faceReader = null;

        // Required to access the face vertices.
        private FaceAlignment _faceAlignment = null;

        // Required to access the face model points.
        private FaceModel _faceModel = null;

        // Used to display 1,000 points on screen.
        private List<Ellipse> _points = new List<Ellipse>();

        // IR + depth frame reader = multisource Frame reader
        private MultiSourceFrameReader multiFrameReader = null; 

        private FrameDescription infraredFrameDescription = null;
        private FrameDescription depthFrameDescription = null;

        // IR, depthフレーム表示用のWritableBitmap
        private WriteableBitmap infraredBitmap = null;
        private WriteableBitmap depthBitmap = null;

        private Int32Rect infraredRect;
        private Int32Rect depthRect;

        private int[] centXY = new int[2];

        // IRフレーム中のblobのindex配列をまとめたlist
        List<int[]> list_arr_index = new List<int[]>();// 配列のlist

        public MainWindow()
        {
            InitializeComponent();
            _sensor = KinectSensor.GetDefault();

            if(_sensor != null)
            {
                // Linten for body data.
                _bodySource = _sensor.BodyFrameSource;
                _bodyReader = _bodySource.OpenReader();
                _bodyReader.FrameArrived += BodyReaderFrameArrived;

                // Listen for HD face data.
                _faceSource = new HighDefinitionFaceFrameSource(_sensor);
                _faceReader = _faceSource.OpenReader();
                _faceReader.FrameArrived += FaceReader_FrameArrived;

                _faceModel = new FaceModel();
                _faceAlignment = new FaceAlignment();

                // ----------------
                // カラーフレームの設定
                // --------------------
                // カラーフレームのリーダーを取得してメンバ変数にセット
                //this.colorFrameReader = _sensor.ColorFrameSource.OpenReader();
                //this.infraredFrameReader = _sensor.InfraredFrameSource.OpenReader();

                // Depth reader
                //this.depthFrameReader = _sensor.DepthFrameSource.OpenReader();

                // multi frame reader
                this.multiFrameReader = _sensor.OpenMultiSourceFrameReader(FrameSourceTypes.Depth |
                                                                           FrameSourceTypes.Infrared);

                // カラーフレームの情報取得用オブジェクト取得
                //colorFrameDescription = _sensor.ColorFrameSource.CreateFrameDescription(ColorImageFormat.Bgra);
                infraredFrameDescription = _sensor.InfraredFrameSource.FrameDescription;

                // Depth Frame description
                depthFrameDescription = _sensor.DepthFrameSource.FrameDescription;
                
                infraredRect = new Int32Rect(0, 0, infraredFrameDescription.Width, infraredFrameDescription.Height);
                depthRect = new Int32Rect(0, 0, depthFrameDescription.Width, depthFrameDescription.Height);

                // カラーフレーム到着(発生)のハンドラーをセット
                //colorFrameReader.FrameArrived += ReaderColorFrameArrived;
                //infraredFrameReader.FrameArrived += ReaderInfraredFrameArrived;

                // Depth frame arrive event handler
                //depthFrameReader.FrameArrived += ReaderDepthFrameArrived;

                // multistream event handler
                multiFrameReader.MultiSourceFrameArrived += ReaderMultiFrameArrived;

                // -----------------------------------------
                // カラーフレームの画面表示用設定
                // -----------------------------------------
                // 表示用のWritableBitmapを作成
                //infraredBitmap = new WriteableBitmap(this.colorFrameDescription.Width,
                //                                           this.colorFrameDescription.Height,
                //                                           96.0, 96.0, PixelFormats.Bgr32, null);
                infraredBitmap = new WriteableBitmap(this.infraredFrameDescription.Width,
                    this.infraredFrameDescription.Height, 96.0, 96.0, PixelFormats.Gray16, null);


                depthBitmap = new WriteableBitmap(this.depthFrameDescription.Width,
                    this.depthFrameDescription.Height, 96.0, 96.0, PixelFormats.Gray16, null);
                // WriteableBitmapをWPFのImageコントローラーのソースに関連付け
                //ColorImage.Source = this.infraredBitmap;

                // start tracking
                _sensor.Open();
            }
        }


        // Step6: Connect a body with a face
        private void BodyReaderFrameArrived(object sender, BodyFrameArrivedEventArgs e)
        {
            using (var frame = e.FrameReference.AcquireFrame())
            {
                if (frame != null)
                {
                    Body[] bodies = new Body[frame.BodyCount];
                    frame.GetAndRefreshBodyData(bodies);

                    Body body = bodies.Where(b => b.IsTracked).FirstOrDefault();

                    if (!_faceSource.IsTrackingIdValid)
                    {
                        if (body != null)
                        {
                            _faceSource.TrackingId = body.TrackingId;
                        }
                    }
                }
            }
        }

        // Step7: Get and update the facial points
        private void FaceReader_FrameArrived(object sender, HighDefinitionFaceFrameArrivedEventArgs e)
        {
            using (var frame = e.FrameReference.AcquireFrame())
            {
                if (frame != null && frame.IsFaceTracked)
                {
                    frame.GetAndRefreshFaceAlignmentResult(_faceAlignment);
                    UpdateFacePoints();
                }
            }
        }

        // Step8: Draw the points on screen

        private void UpdateFacePoints()
        {
            if (_faceModel == null) return;

            var vertices = _faceModel.CalculateVerticesForAlignment(_faceAlignment);

            if (vertices.Count > 0)
            {
                if (_points.Count == 0)
                {
                    for (int index = 0; index < vertices.Count; index++)
                    {
                        Ellipse ellipse = new Ellipse
                        {
                            Width = 2.0,
                            Height = 2.0,
                            Fill = new SolidColorBrush(Colors.Blue)
                        };

                        _points.Add(ellipse);
                    }

                    foreach (Ellipse ellipse in _points)
                    {
                        canvas.Children.Add(ellipse);
                    }
                }

                for (int index =0; index <vertices.Count; index++)
                {
                    CameraSpacePoint vertice = vertices[index];
                    DepthSpacePoint point = _sensor.CoordinateMapper.MapCameraPointToDepthSpace(vertice);

                    if (float.IsInfinity(point.X) || float.IsInfinity(point.Y)) return;

                    Ellipse ellipse = _points[index];

                    Canvas.SetLeft(ellipse, point.X);
                    Canvas.SetTop(ellipse, point.Y);
                }
            }
        }


        // multi frame reader event handler
        private void ReaderMultiFrameArrived(object sender, MultiSourceFrameArrivedEventArgs e)
        {
            // Get a reference to the multi-frame
            var reference = e.FrameReference.AcquireFrame();

            // depth
            using (DepthFrame depthFrame = reference.DepthFrameReference.AcquireFrame())
            {
                string label_coords_blob = "";//label_blobsの文字列
                if (depthFrame != null)
                {
                    FrameDescription depthFrameDescription = depthFrame.FrameDescription;
                    int width = depthFrameDescription.Width;
                    int height = depthFrameDescription.Height;

                    var depthData = new ushort[width * height];
                    depthFrame.CopyFrameDataToArray(depthData);
                    this.depthBitmap.WritePixels(depthRect, depthData, width * (int)depthFrameDescription.BytesPerPixel, 0);

                    // depthData -> camera space data
                    CameraSpacePoint[] cs_points = new CameraSpacePoint[width * height];
                    _sensor.CoordinateMapper.MapDepthFrameToCameraSpace(depthData, cs_points);

                    // calculate camera space coordinates of each marker(blob) // plan: 以下のループは別functionにする
                    float[,] coord_blobs_center = new float[list_arr_index.Count(),3];// blob中心座標の2次元配列
                    //label_sample.Content = list_arr_index.Count().ToString();
                    int i_blob = 0;// blobのindex
                    foreach (int[] arr_index in list_arr_index)
                    {
                        // 各blobのcamera space pointからx, y, z座標を取り出して配列 -> 平均
                        float[] coord_blob_center = new float[3];//blob (反射マーカー)の中心座標を入れる

                        // select camera space points corresponding each blob
                        CameraSpacePoint[] cs_points_blob = new CameraSpacePoint[arr_index.Length];// camera space配列宣言
                        // x,y,z座標のlist
                        List<float> list_x_cs_points_blob = new List<float>(); 
                        List<float> list_y_cs_points_blob = new List<float>();
                        List<float> list_z_cs_points_blob = new List<float>();

                        // x,y,z座標の平均
                        float x_coord_cs_points_blob = 0;
                        float y_coord_cs_points_blob = 0;
                        float z_coord_cs_points_blob = 0;

                        // listの初期化.　念のため
                        list_x_cs_points_blob.Clear();
                        list_y_cs_points_blob.Clear();
                        list_z_cs_points_blob.Clear();

                        // for loop
                        int i_points_blob = 0; // blob内のcs_pointsのindex
                        //int i_coord_blob = 0; // blob内の座標のindex
                        foreach (int i_point in arr_index)
                        {
                            // arr_index: blobのcamera space pointsに対応するindexes
                            // cs_points_blobをまとめる
                            cs_points_blob[i_points_blob] = cs_points[i_point];
                            i_points_blob += 1;
                            // x,y,z座標のlistを求める: infinityを外す
                            if (!Double.IsInfinity(cs_points[i_point].X))
                            {
                                list_x_cs_points_blob.Add(cs_points[i_point].X);
                                list_y_cs_points_blob.Add(cs_points[i_point].Y);
                                list_z_cs_points_blob.Add(cs_points[i_point].Z);
                                // 座標の足し算
                                x_coord_cs_points_blob += cs_points[i_point].X;
                                y_coord_cs_points_blob += cs_points[i_point].Y;
                                z_coord_cs_points_blob += cs_points[i_point].Z;
                            }

                        }
                        // listを配列に変換
                        float[] arr_x_cs_points_blob = list_x_cs_points_blob.ToArray();
                        float[] arr_y_cs_points_blob = list_y_cs_points_blob.ToArray();
                        float[] arr_z_cs_points_blob = list_z_cs_points_blob.ToArray();

                        // cs_points_blobからblobの中心座標を求める ////////////////////

                        // infの割合を求める
                        float ratio_valid_points_blob= (float)arr_x_cs_points_blob.Length/
                            (float)arr_index.Length;// blobの内infinityでなかったpointの割合

                        // infの割合が1割以以上だったら中心座標の計算
                        if (ratio_valid_points_blob>0.0)
                        {
                            // 足し算したものを数で割る
                            x_coord_cs_points_blob = x_coord_cs_points_blob / (float)arr_x_cs_points_blob.Count();
                            y_coord_cs_points_blob = y_coord_cs_points_blob / (float)arr_y_cs_points_blob.Count(); // 分母はどれも同じ
                            z_coord_cs_points_blob = z_coord_cs_points_blob / (float)arr_z_cs_points_blob.Count(); // 分母はどれも同じ
                        }
                        else
                        {
                            x_coord_cs_points_blob = 0;
                            y_coord_cs_points_blob = 0;
                            z_coord_cs_points_blob = 0;
                        }
                        coord_blob_center = new float[]
                        {
                            x_coord_cs_points_blob,
                            y_coord_cs_points_blob,
                            z_coord_cs_points_blob
                        };
                        // 座標coord_blob_centerを二次元配列にまとめる+ label_coordsのstringを生成
                        for (int i_xyz = 0; i_xyz < 3; i_xyz++)
                        {
                            coord_blobs_center[i_blob, i_xyz] = coord_blob_center[i_xyz];

                        }

                        label_coords_blob +=
                            string.Format("X: {0:+000.0;-000.0;+   0.0}, ", coord_blob_center[0] * 100) +
                            string.Format("Y: {0:+000.0;-000.0;+   0.0}, ", coord_blob_center[1] * 100) +
                            string.Format("Z: {0:+000.0;-000.0;+   0.0}\n", coord_blob_center[2] * 100);

                        i_blob += 1;
                    }

                    // coord_blobs_centerを画面に出力
                    label_coords.Content = label_coords_blob;
                }
            }

            // IR
            using (InfraredFrame infraredFrame = reference.InfraredFrameReference.AcquireFrame())
            {
                if (infraredFrame != null)
                {
                    FrameDescription infraredFrameDescription = infraredFrame.FrameDescription;
                    int width = infraredFrameDescription.Width;
                    int height = infraredFrameDescription.Height;

                    //ushort[] infraredData = new ushort[width * height];
                    // http://www.naturalsoftware.jp/entry/2014/07/25/020750
                    var infraredData = new ushort[width * height]; // ushort array
                    infraredFrame.CopyFrameDataToArray(infraredData);
                    this.infraredBitmap.Lock();
                    //this.infraredBitmap = new WriteableBitmap(BitmapSource.Create(width, height, 96, 96,
                    //    PixelFormats.Gray16, null, infraredData, width * (int)infraredFrameDescription.BytesPerPixel));
                    this.infraredBitmap.WritePixels(infraredRect, infraredData, width * (int)infraredFrameDescription.BytesPerPixel, 0);
                    //depthImage.WritePixels(depthRect, depthBuffer, depthStride, 0);// template
                    this.infraredBitmap.Unlock();
                    ColorImage.Source = this.infraredBitmap;

                    // OpenCV: Count blobs and 
                    CountBlobs(this.infraredBitmap);
                }
            }

        }


        //private void CountBlobs(WriteableBitmap writableBitmap)
        private void CountBlobs(WriteableBitmap writeableBitmap)
        {
            Mat imgIR = writeableBitmap.ToMat();// CV_16UC1
            imgIR.ConvertTo(imgIR, MatType.CV_8UC1, 1.0 / 256.0);
            Mat imgIRbin = new Mat(imgIR.Rows, imgIR.Cols, MatType.CV_8UC1);
            Cv2.Threshold(imgIR, imgIRbin, 225, 255, ThresholdTypes.Binary);
            //imgIR.SaveImage("D:/imgIR.png");
            CvBlobs blobs = new CvBlobs(imgIRbin);
            blobs.FilterByArea(30, 2000);
            //label_sample.Content = blobs.Count().ToString();

            // Canvasに追加
            canvas_blob.Children.Clear();
            list_arr_index.Clear();
            //label_sample.Content = blobs.Count.ToString();
            if (blobs.Count>0)
            {                
                foreach (KeyValuePair<int, CvBlob> item in blobs)
                {
                    int labelValue = item.Key;
                    CvBlob blob = item.Value;
                    Rectangle blob_rect = new Rectangle
                    {
                        Width = blob.Rect.Width,
                        Height = blob.Rect.Height,
                        Stroke = Brushes.Red,
                        StrokeThickness = 2
                    };
                    canvas_blob.Children.Add(blob_rect);
                    Canvas.SetLeft(blob_rect, blob.Rect.Left);
                    Canvas.SetTop(blob_rect, blob.Rect.Top);
                }

                // blobsから各blobのindexを取り出す////////////////////
                // blobsからLabelsに変換
                LabelData labelBlobs = blobs.Labels;
                // Labelsを1dデータに変換
                int[] label_blobs_vector = new int[labelBlobs.Rows*labelBlobs.Cols];
                int ii = 0;

                //for (int i_col = 0; i_col< labelBlobs.Cols; i_col++)
                for (int i_row = 0; i_row < labelBlobs.Rows; i_row++)
                {
                    //for (int i_row = 0; i_row<labelBlobs.Rows;i_row++)
                    for (int i_col = 0; i_col < labelBlobs.Cols; i_col++)
                    {
                        label_blobs_vector[ii] = labelBlobs[i_row, i_col];
                        ii += 1;
                    }
                }
                // // Labelsからblob.Valueに一致するindexの配列を作成
                // list_arr_indexに格納する
                // int count_blobs = blobs.Count;
                //label_sample.Content = list_arr_index.Count().ToString();
                foreach (KeyValuePair<int, CvBlob> item in blobs)
                {
                    int count_blobs = blobs.Count();
                    int labelvalue = item.Key;
                    // Labelsからlabelvalueに一致するindex配列を作成
                    int area_blob = item.Value.Area;//
                    int[] arr_idx_label = new int[area_blob];
                    ii = 0;
                    for (int i_lab=0;i_lab<label_blobs_vector.Length;i_lab++)
                    {
                        if (label_blobs_vector[i_lab] == labelvalue)
                        {
                            arr_idx_label[ii] = i_lab;
                            ii += 1;
                        }
                    }
                    //int[] arr_idx_label = label_blobs_vector.FindIndex<int>(label => label == labelvalue);
                    list_arr_index.Add(arr_idx_label);
                }
                //label_sample.Content = list_arr_index.Count().ToString();
                Console.WriteLine("hoge");//ブレイクポイント用
            }



            // おまけ
            // blobs.Label(mm);
            //Mat imgRender = new Mat(mm.Size, MatType.CV_8UC3);
            //imgIR.SaveImage("D:/imgIR.png");
            //label_sample.Content = imgIR.MinMaxIdx();
            //label_sample.Content = blobs.Count.ToString();
        }


    }
}
