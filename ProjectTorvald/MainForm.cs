using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.IO;
using System.Diagnostics;

//Extension
using Emgu.CV;
using Emgu.CV.Cuda;
using Emgu.CV.CvEnum;
using Emgu.CV.Structure;
using AForge.Video.DirectShow;
using Emgu.CV.UI;
using Emgu.CV.Face;
using Syncfusion.XlsIO;
using System.Speech.Synthesis;
using Newtonsoft.Json;

namespace ProjectTorvald
{
    public partial class MainForm : Form
    {
        #region Declaration
        Capture grabber = new Capture();
        Image<Bgr, byte> currentFrame;
        Image<Gray, byte> grayFrame;
        FilterInfoCollection filter;
        FaceRecognizer recognizer;
        Rectangle[] faceDetected;
        List<Rectangle> faces = new List<Rectangle>();
        Image<Gray, byte> faceResult, TrainedFace = null;
        List<Image<Gray, byte>> trainingImages = new List<Image<Gray, byte>>();
        List<string> trainingNameLabels = new List<string>();
        List<string> trainingNIMLabels = new List<string>();
        List<int> trainingImagesID = new List<int>();
        List<string> NIMPersons = new List<string>();
        int faceCounter, counterAddFace, counterAbsent = 5, counterScanning;
        string pathDataset = null, pathExcel = null, NIMStored = null, NameStored = null, displayNIM = null;
        private bool recognizing = false, CUDA = false;
        public static bool addFace = false;
        public static string NameLabel, NIMLabel;
        public static SettingsComponent settings;


        //Cascade Declaration
        CudaCascadeClassifier cuda_cascade = new CudaCascadeClassifier("haarcascade_frontalface_default.xml");
        CascadeClassifier cascade = new CascadeClassifier("haarcascade_frontalface_default.xml");
        #endregion


        public MainForm()
        {
            InitializeComponent();
        }


        private void MainForm_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Control == true && e.KeyCode == Keys.S)
            {
                btnStart.PerformClick();
            }
            else if (e.Control == true && e.KeyCode == Keys.F)
            {
                stpLoadDataset.PerformClick();
            }
            else if (e.Control == true && e.KeyCode == Keys.A)
            {
                stpAddFace.PerformClick();
            }
            else if (e.Control == true && e.KeyCode == Keys.R)
            {
                btnScanning.PerformClick();
            }
            else if (e.Control == true && e.KeyCode == Keys.F1)
            {
                stpHelp.PerformClick();
            }
        }


        private void MainForm_Load(object sender, EventArgs e)
        {
            InitializeJSON();

            CUDA = CudaInvoke.HasCuda;

            if (settings.boolLBPH == true)
            {
                recognizer = new LBPHFaceRecognizer(settings.Radius, settings.Neighbors, settings.GridX, settings.GridY, settings.ThresholdLPBH);
            }
            else if (settings.boolEigen == true)
            {
                recognizer = new EigenFaceRecognizer(settings.numCompEigen, settings.ThresholdEigen);
            }
            else if (settings.boolFisher == true)
            {
                recognizer = new FisherFaceRecognizer(settings.numCompFisher, settings.ThresholdFisher);
            }

            if (!Directory.Exists(Application.StartupPath + "\\report"))
            {
                Directory.CreateDirectory(Application.StartupPath + "\\report");
            }
            if (!Directory.Exists(Application.StartupPath + "\\Face Recognition"))
            {
                Directory.CreateDirectory(Application.StartupPath + "\\Face Recognition");
            }

            //LOAD CAMERA DEVICE
            try
            {
                filter = new FilterInfoCollection(FilterCategory.VideoInputDevice);
                foreach (FilterInfo filterInfo in filter)
                    cbxDevices.Items.Add(filterInfo.Name);
                cbxDevices.SelectedIndex = 0;
            }
            catch
            {
                MessageBox.Show("No Camera Device Detected,\nPlease check your device and re-launch me", "No Device", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
                cbxDevices.Text = "No Camera Device Detected";
                stpLoadDataset.Enabled = false;
                cbxDevices.Enabled = false;
            }
        }


        private void btnStop_Click(object sender, EventArgs e)
        {

        }


        private void Start_Click(object sender, EventArgs e)
        {
            if (faceCounter > 0)
            {
                btnScanning.Enabled = true;
                cbxMeeting.Enabled = true;
            }
            cbxDevices.Enabled = false;
            grabber.QueryFrame();
            Application.Idle += new EventHandler(FrameCapture);
            btnStart.Enabled = false;
            stpAddFace.Enabled = true;
        }


        private void AddFace()
        {
            grayFrame = grabber.QueryFrame().ToImage<Gray, byte>().Resize(pcbCurrentFrame.Width, pcbCurrentFrame.Height, Inter.Cubic);
            faceDetected = cascade.DetectMultiScale(grayFrame, 1.1, 10, new Size(20, 20), Size.Empty);

            foreach (Rectangle face in faceDetected)
            {
                TrainedFace = currentFrame.Copy(face).Convert<Gray, byte>();
                break;
            }

            TrainedFace = faceResult.Resize(100, 100, Inter.Cubic);
            trainingImages.Add(TrainedFace);
            trainingImagesID.Add(faceCounter);
            trainingNameLabels.Add(NameLabel);
            trainingNIMLabels.Add(NIMLabel);

            faceCounter++;
            File.WriteAllText(pathDataset + "facecount.txt", faceCounter.ToString());

            File.WriteAllText(pathDataset + "namelabels.txt", "");
            File.WriteAllText(pathDataset + "nimlabels.txt", "");

            pcbRecognized.Image = TrainedFace;

            for (int l = 0; l < faceCounter; l++)
            {
                trainingImages.ToArray()[l].Save(pathDataset + "\\dataset\\face" + (l + 1) + ".bmp");
                File.AppendAllText(pathDataset + "namelabels.txt", trainingNameLabels.ToArray()[l] + "%");
                File.AppendAllText(pathDataset + "nimlabels.txt", trainingNIMLabels.ToArray()[l] + "%");
            }


            recognizer.Train(trainingImages.ToArray(), trainingImagesID.ToArray());
            recognizer.Save(pathDataset + "trainingdata.xml");

            lblFaceCounter.Text = (faceCounter / 10).ToString();
        }


        private void btnScanning_Click(object sender, EventArgs e)
        {
            counterAbsent = 5;
            if (!cbxMeeting.Text.Any(char.IsDigit))
            {
                MessageBox.Show("Meeting Combo Box Cannot Empty", "Meeting Empty", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
            else if (recognizing == false)
            {
                recognizing = true;
                lblAutoStts.Text = "Automate Status :       ON";
            }
            else if (recognizing == true)
            {
                recognizing = false;
                lblAutoStts.Text = "Automate Status :       OFF";
            }
        }


        private void tmrScanning_Tick(object sender, EventArgs e)
        {
            counterAbsent--;

            if(counterAbsent == -1)
            {
                counterAbsent = 5;
            }
        }


        private void FaceDetector (Rectangle[] facelessVoid)
        {
            foreach (Rectangle face in facelessVoid)
            {
                counterScanning = counterScanning + 1;

                faceResult = currentFrame.Copy(face).Convert<Gray, byte>().Resize(100, 100, Inter.Cubic);

                currentFrame.Draw(new Rectangle(face.Location, face.Size), new Bgr(Color.Red), 2);

                try
                {
                    var result = recognizer.Predict(faceResult.Copy().Resize(100, 100, Inter.Cubic));

                    if (result.Label >= 0)
                    {
                        CvInvoke.PutText(
                        currentFrame,
                        trainingNameLabels.ToArray()[result.Label].Substring(0, trainingNameLabels.ToArray()[result.Label].IndexOf(' ')),
                        new Point(face.X - 2, face.Y - 5),
                        FontFace.HersheyDuplex,
                        0.5,
                        new Bgr(Color.White).MCvScalar);

                        CvInvoke.PutText(
                        currentFrame,
                        trainingNIMLabels.ToArray()[result.Label],
                        new Point(face.X - 2, face.Y - 20),
                        FontFace.HersheyDuplex,
                        0.5,
                        new Bgr(Color.White).MCvScalar);

                        NIMStored = trainingNIMLabels.ToArray()[result.Label];
                        NameStored = trainingNameLabels.ToArray()[result.Label].Substring(0, trainingNameLabels.ToArray()[result.Label].IndexOf(' '));
                    }

                    lblAttendedNIM.Text = NIMStored;
                    lblAttendedName.Text = NameStored;

                    NIMPersons[counterScanning - 1] = NIMStored;
                    NIMPersons.Add("");
                }
                catch
                {
                    continue;
                }
            }
        }


        private void FrameCapture(object sender, EventArgs e)
        {
            NIMPersons.Add("");
            currentFrame = grabber.QueryFrame().ToImage<Bgr, byte>().Resize(pcbCurrentFrame.Width, pcbCurrentFrame.Height, Inter.Cubic);
            grayFrame = currentFrame.Convert<Gray, byte>();

            if (CUDA)
            {
                lblCudaStts.Text = "CUDA Status :             CUDA ON";
                using (CudaImage<Gray, byte> cuda_grayFrame = new CudaImage<Gray, byte>(grayFrame))
                using (GpuMat region = new GpuMat())
                {
                    cuda_cascade.DetectMultiScale(cuda_grayFrame, region);
                    faceDetected = cuda_cascade.Convert(region);
                    faces.AddRange(faceDetected);
                    FaceDetector(faceDetected);
                }
                ScanningCounter();
                AutoAbsent();
            }
            else
            {
                //IF THE USER DON'T HAVE NVIDIA GRAPHIC CARD
                lblCudaStts.Text = "CUDA Status :             CUDA OFF";
                faceDetected = cascade.DetectMultiScale(grayFrame, 1.1, 10, new Size(100, 100), Size.Empty);
                FaceDetector(faceDetected);
                ScanningCounter();
                AutoAbsent();
            }


            pcbCurrentFrame.Image = currentFrame;


            displayNIM = "";
            if (lblNIMOnScreen.Text == "")
            {
                displayNIM = "Scanning";
            }
            NIMPersons.Clear();
        }


        public const int WM_NCLBUTTONDOWN = 0xA1;
        public const int HT_CAPTION = 0x2;

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        public static extern int SendMessage(IntPtr hWnd, int Msg, int wParam, int lParam);
        [System.Runtime.InteropServices.DllImport("user32.dll")]
        public static extern bool ReleaseCapture();

        private void MenuStrip_MouseDown(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                ReleaseCapture();
                SendMessage(Handle, WM_NCLBUTTONDOWN, HT_CAPTION, 0);
            }
        }

        private void stpSettings_Click(object sender, EventArgs e)
        {
            Settings opensettings = new Settings();
            opensettings.ShowDialog();

            if(settings.boolLBPH == true)
            {
                recognizer = new LBPHFaceRecognizer(settings.Radius, settings.Neighbors, settings.GridX, settings.GridY, settings.ThresholdLPBH);
            }
            else if (settings.boolEigen == true)
            {
                recognizer = new EigenFaceRecognizer(settings.numCompEigen, settings.ThresholdEigen);
            }
            else if (settings.boolFisher == true)
            {
                recognizer = new FisherFaceRecognizer(settings.numCompFisher, settings.ThresholdFisher);
            }
        }

        private void stpHelp_Click(object sender, EventArgs e)
        {
            Help help = new Help();
            help.ShowDialog();
        }


        private void stpLoadDataset_Click(object sender, EventArgs e)
        {
            OpenFileDialog openFile = new OpenFileDialog();
            openFile.Title = "Load Dataset";
            openFile.InitialDirectory = Application.StartupPath + "\\report";
            openFile.Filter = "excel file (*.xlsx)|*.xlsx";
            openFile.FilterIndex = 1;
            openFile.RestoreDirectory = true;
            openFile.CheckFileExists = true;
            openFile.CheckPathExists = true;
            if (openFile.ShowDialog() == DialogResult.OK)
            {
                pathExcel = openFile.FileName;
                loadExcelFile();
                pathDataset = Application.StartupPath + "\\Face Recognition\\" + Path.GetFileNameWithoutExtension(openFile.FileName) + "\\";

                if (Directory.Exists(pathDataset))
                {
                    if (!Directory.Exists(pathDataset + "\\dataset"))
                    {
                        Directory.CreateDirectory(pathDataset + "dataset");
                    }
                    if (!File.Exists(pathDataset + "namelabels.txt"))
                    {
                        File.CreateText(pathDataset + "namelabels.txt");
                    }
                    if (!File.Exists(pathDataset + "nimlabels.txt"))
                    {
                        File.CreateText(pathDataset + "nimlabels.txt");
                    }
                    if (!File.Exists(pathDataset + "facecount.txt"))
                    {
                        File.CreateText(pathDataset + "facecount.txt");
                    }
                    if (File.Exists(pathDataset + "trainingdata.xml"))
                    {
                        recognizer.Load(pathDataset + "trainingdata.xml");

                        string namelabels = File.ReadAllText(pathDataset + "namelabels.txt");
                        string nimlabels = File.ReadAllText(pathDataset + "nimlabels.txt");
                        string[] Name = namelabels.Split('%');
                        string[] NIM = nimlabels.Split('%');

                        string[] count = Directory.GetFiles(pathDataset + "dataset", "*", SearchOption.AllDirectories);
                        faceCounter = count.Length;
                        //faceCounter = Convert.ToInt32(File.ReadAllText(pathDataset + "facecount.txt"));


                        for (int l = 0; l < faceCounter; l++)
                        {
                            trainingImages.Add(new Image<Gray, byte>(pathDataset + "dataset\\" + "face" + (l + 1) + ".bmp"));
                            trainingNameLabels.Add(Name[l]);
                            trainingNIMLabels.Add(NIM[l]);
                            trainingImagesID.Add(l);
                        }

                        lblFaceCounter.Text = (faceCounter / 10).ToString();
                        MessageBox.Show("Dataset Loaded,\n" + (faceCounter/10) + " Face Added", "Dataset Loaded", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                    else
                    {
                        MessageBox.Show("Empty Dataset Loaded,\nPlease Add Some Face First", "Dataset Loaded", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        lblFaceCounter.Text = "Empty";
                    }
                }
                else
                {
                    Directory.CreateDirectory(pathDataset);
                    Directory.CreateDirectory(pathDataset + "dataset");
                    File.CreateText(pathDataset + "namelabels.txt");
                    File.CreateText(pathDataset + "nimlabels.txt");
                    File.CreateText(pathDataset + "facecount.txt");
                    lblFaceCounter.Text = "Empty";

                    MessageBox.Show("Empty Dataset Created,\nPlease Train Your Dragon", "Datase Created", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }

                btnStart.Enabled = true;
            }
        }


        private void MainForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            File.WriteAllText(Application.StartupPath + "\\settings\\settings.json", JsonConvert.SerializeObject(settings));
        }


        private void stpAddFace_Click(object sender, EventArgs e)
        {
            AddNewFace add = new AddNewFace();
            add.ShowDialog();

            if (addFace == true)
            {
                counterAddFace = 21;
                tmrAddFace.Enabled = true;
                pcbRecognized.Visible = true;
            }
        }


        private void ScanningCounter()
        {
            counterScanning = 0;

            for (int l = 0; l < faceDetected.Length; l++)
            {
                try
                {
                    displayNIM = displayNIM + NIMPersons[l] + ", ";
                }
                catch
                {
                    continue;
                }
            }

            lblNIMOnScreen.Text = displayNIM;
        }


        private void AutoAbsent()
        {
            if (recognizing == true)
            {
                tmrScanning.Start();
                if (lblNIMOnScreen.Text.Length > 8)
                {
                    tmrScanning.Start();
                    OverWriteExcel(NIMStored);
                    lblAttendanceStatus.Text = counterAbsent.ToString();
                }
                else
                {
                    tmrScanning.Stop();
                    lblAttendanceStatus.Text = "Scanning";
                    lblAttendedNIM.Text = "Scanning";
                    lblAttendedName.Text = "Scanning";
                }
            }
            else if (recognizing == false)
            {
                tmrScanning.Stop();
                lblNIMOnScreen.Text = "Paused";
                lblAttendanceStatus.Text = "Paused";
                lblAttendedNIM.Text = "Paused";
                lblAttendedName.Text = "Paused";
            }
        }


        private void OverWriteExcel(string NIM)
        {
            using (ExcelEngine excelEngine = new ExcelEngine())
            {
                IApplication application = excelEngine.Excel;
                application.DefaultVersion = ExcelVersion.Excel2013;
                IWorkbook workbook = excelEngine.Excel.Workbooks.Open(pathExcel);
                IWorksheet worksheet = workbook.Worksheets[0];
                worksheet.EnableSheetCalculations();

                for (int l = 4; l < ((faceCounter / 10) + 4); l++)
                {
                    if (NIM == worksheet.Range["B" + l.ToString()].Text)
                    {
                        if (counterAbsent == 0)
                        {
                            //string nameSpeech = worksheet.Range["C" + l.ToString()].Text.Substring(0, worksheet.Range["C" + l.ToString()].Text.IndexOf(' '));
                            worksheet.Range[AlphabetToInt((Int32.Parse(cbxMeeting.Text.Substring(0, cbxMeeting.Text.Length-2))) + 2).ToString() + l.ToString()].Number = 1;

                            worksheet.Range["T" + l.ToString()].Formula = "=SUM(D" + l.ToString() + ":S" + l.ToString() + ")";

                            workbook.SaveAs(pathExcel);
                            lblAttendanceStatus.Text = "DONE";
                            string toSpeak = "Scanning Completed";
                            SpeechSynthesizer speech = new SpeechSynthesizer();
                            speech.Speak(toSpeak);
                            speech.Dispose();
                        }
                        else
                        {
                            lblAttendanceStatus.Text = counterAbsent.ToString();
                        }
                    }
                }
            }
        }


        private void tmrAddFace_Tick(object sender, EventArgs e)
        {
            if (counterAddFace > 1)
            {
                counterAddFace -= 1;
                if (counterAddFace % 2 == 0)
                {
                    AddFace();
                }
                else if (counterAddFace == 1)
                {
                    using (ExcelEngine excelEngine = new ExcelEngine())
                    {
                        IApplication application = excelEngine.Excel;
                        application.DefaultVersion = ExcelVersion.Excel2013;
                        IWorkbook workbook = excelEngine.Excel.Workbooks.Open(pathExcel);
                        IWorksheet worksheet = workbook.Worksheets[0];
                        IStyle body = workbook.Styles.Add("Body" + (faceCounter / 10));

                        body.Borders[ExcelBordersIndex.EdgeLeft].LineStyle = ExcelLineStyle.Thin;
                        body.Borders[ExcelBordersIndex.EdgeRight].LineStyle = ExcelLineStyle.Thin;
                        body.Borders[ExcelBordersIndex.EdgeBottom].LineStyle = ExcelLineStyle.Thin;
                        worksheet.Range["B" + ((faceCounter / 10) + 3).ToString() + ":T" + ((faceCounter / 10) + 3).ToString()].CellStyle = body;

                        worksheet.Range["B" + ((faceCounter / 10) + 3).ToString()].Text = NIMLabel;
                        worksheet.Range["C" + ((faceCounter / 10) + 3).ToString()].Text = NameLabel;

                        workbook.SaveAs(pathExcel);
                        workbook.Close();
                        excelEngine.Dispose();
                    }

                    MessageBox.Show("Done, Hope I Can Recognize Him/Her Later", "Face Added", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    tmrAddFace.Enabled = false;
                    btnScanning.Enabled = true;
                    pcbRecognized.Visible = false;
                    cbxMeeting.Enabled = true;
                    addFace = false;
                }
            }
        }


        private void InitializeJSON()
        {
            try
            {
                if(!Directory.Exists(Application.StartupPath + "\\settings"))
                {
                    Directory.CreateDirectory("settings");
                }
                
                settings = JsonConvert.DeserializeObject<SettingsComponent>(File.ReadAllText(Application.StartupPath + "\\settings\\settings.json"));

                if (File.Exists(Application.StartupPath + "\\settings\\settings.json"))
                {
                    MessageBox.Show("Settings Loaded");
                }
            }
            catch (FileNotFoundException ex)
            {
                MessageBox.Show("No Settings Found, Generating One...");
                settings = new SettingsComponent();
                saveSettingsJSON();
            }
            catch (JsonReaderException e)
            {
                MessageBox.Show("Corrupt Settings File");
                File.Delete(Application.StartupPath + "\\settings\\settings.json");
                this.Close();
            }
        }


        private void saveSettingsJSON()
        {
            File.WriteAllText(Application.StartupPath + "\\settings\\settings.json", JsonConvert.SerializeObject(settings));
        }


        static string AlphabetToInt(int index)
        {
            const string letters = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";

            var value = "";

            if (index >= letters.Length)
                value += letters[index / letters.Length - 1];

            value += letters[index % letters.Length];

            return value;
        }


        private void loadExcelFile()
        {
            using (ExcelEngine excelEngine = new ExcelEngine())
            {
                IApplication application = excelEngine.Excel;
                application.DefaultVersion = ExcelVersion.Excel2013;
                IWorkbook workbook = application.Workbooks.Open(pathExcel);
                IWorksheet worksheet = workbook.Worksheets[0];

                if (!workbook.Styles.Contains("Header"))
                {
                    IStyle header = workbook.Styles.Add("Header");
                    header.Font.Bold = true;
                    header.HorizontalAlignment = ExcelHAlign.HAlignCenter;
                    header.Borders[ExcelBordersIndex.EdgeLeft].LineStyle = ExcelLineStyle.Thin;
                    header.Borders[ExcelBordersIndex.EdgeRight].LineStyle = ExcelLineStyle.Thin;
                    header.Borders[ExcelBordersIndex.EdgeTop].LineStyle = ExcelLineStyle.Thin;
                    header.Borders[ExcelBordersIndex.EdgeBottom].LineStyle = ExcelLineStyle.Thin;

                    worksheet.Range["B1"].ColumnWidth = 11;
                    worksheet.Range["C1"].ColumnWidth = 24;
                    worksheet.Range["D1:T1"].ColumnWidth = 13;
                    worksheet.Range["B3:T3"].CellStyle = header;

                    worksheet.Range["B3"].Text = "NIM";
                    worksheet.Range["C3"].Text = "NAMA";
                    worksheet.Range["D3"].Text = "Pertemuan1";
                    worksheet.Range["E3"].Text = "Pertemuan2";
                    worksheet.Range["F3"].Text = "Pertemuan3";
                    worksheet.Range["G3"].Text = "Pertemuan4";
                    worksheet.Range["H3"].Text = "Pertemuan5";
                    worksheet.Range["I3"].Text = "Pertemuan6";
                    worksheet.Range["J3"].Text = "Pertemuan7";
                    worksheet.Range["K3"].Text = "Pertemuan8";
                    worksheet.Range["L3"].Text = "Pertemuan9";
                    worksheet.Range["M3"].Text = "Pertemuan10";
                    worksheet.Range["N3"].Text = "Pertemuan11";
                    worksheet.Range["O3"].Text = "Pertemuan12";
                    worksheet.Range["P3"].Text = "Pertemuan13";
                    worksheet.Range["Q3"].Text = "Pertemuan14";
                    worksheet.Range["R3"].Text = "Pertemuan15";
                    worksheet.Range["S3"].Text = "Pertemuan16";
                    worksheet.Range["T3"].Text = "HADIR";
                }
                workbook.SaveAs(pathExcel);
                workbook.Close();
                excelEngine.Dispose();
            }
        }
    }
}
