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

using System.IO;
using Microsoft.ProjectOxford.Face;
using Microsoft.ProjectOxford.Face.Contract;
using System.Globalization;
using Microsoft.Win32;

namespace MSFaceAPITester
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private readonly IFaceServiceClient faceApiConnection = new FaceServiceClient("{subscription key}", "{Face API endpoint URL}");
        private string userGroupId = "friends";
        private List<CreatePersonResult> users = new List<CreatePersonResult>();

        public MainWindow()
        {
            InitializeComponent();
        }

        private async void Window_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                await faceApiConnection.CreatePersonGroupAsync(userGroupId, "My friends");
            }
            catch { }

            users.Add(await faceApiConnection.CreatePersonAsync(userGroupId, "Gašper Kamenšek"));
            users.Add(await faceApiConnection.CreatePersonAsync(userGroupId, "Boštjan Ohnjec"));

            const string user1TrainDatas = @"C:\Temp\GK";
            foreach (string imagePath in Directory.GetFiles(user1TrainDatas, "*.jpg"))
            {
                using (Stream s = File.OpenRead(imagePath))
                {
                    await faceApiConnection.AddPersonFaceAsync(userGroupId, users[0].PersonId, s);
                }
            }

            const string user2TrainDatas = @"C:\Temp\BO";
            foreach (string imagePath in Directory.GetFiles(user2TrainDatas, "*.jpg"))
            {
                using (Stream s = File.OpenRead(imagePath))
                {
                    await faceApiConnection.AddPersonFaceAsync(userGroupId, users[1].PersonId, s);
                }
            }

            await faceApiConnection.TrainPersonGroupAsync(userGroupId);
            TrainingStatus trainingStatus = null;
            while (true)
            {
                trainingStatus = await faceApiConnection.GetPersonGroupTrainingStatusAsync(userGroupId);
                if (trainingStatus.Status != Status.Running)
                    break;

                await Task.Delay(1000);
            }
        }

        private async void btnBrowse_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog openDlg = new OpenFileDialog();
            openDlg.Filter = "JPEG Image(*.jpg)|*.jpg";
            bool? result = openDlg.ShowDialog(this);
            if (!(bool)result)
                return;

            string potDoSlike = openDlg.FileName;
            Uri potDoSlikeURI = new Uri(potDoSlike);

            BitmapImage bmpImage = new BitmapImage();
            bmpImage.BeginInit();
            bmpImage.CacheOption = BitmapCacheOption.None;
            bmpImage.UriSource = potDoSlikeURI;
            bmpImage.EndInit();

            imgPicture.Source = bmpImage;

            Title = "Searching ...";
            var faces = await FaceDetectionProgress(potDoSlike);
            Title = String.Format("Completed. {0} faces founded", faces.Item1.Length);

            if (faces.Item1.Length > 0)
            {
                DrawingVisual visual = new DrawingVisual();
                DrawingContext drawingContext = visual.RenderOpen();
                drawingContext.DrawImage(bmpImage,
                    new Rect(0, 0, bmpImage.Width, bmpImage.Height));
                double dpi = bmpImage.DpiX;
                double resizeFactor = 96 / dpi;

                int indx = -1;
                foreach (var faceFrame in faces.Item1)
                {
                    indx++;

                    drawingContext.DrawRectangle(
                        Brushes.Transparent,
                        new Pen(Brushes.Red, 2),
                        new Rect(
                            faceFrame.Left * resizeFactor,
                            faceFrame.Top * resizeFactor,
                            faceFrame.Width * resizeFactor,
                            faceFrame.Height * resizeFactor
                            )
                    );

                    var faceAttrs = faces.Item2[indx];
                    drawingContext.DrawText(new FormattedText("Name: " + faces.Item3[indx] + "\nAge: " + faceAttrs.Age + "\nGender: " + faceAttrs.Gender, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, new Typeface("Times New Roman"), 30, Brushes.Red), new Point(faceFrame.Left, faceFrame.Top + faceFrame.Height));
                }

                drawingContext.Close();
                RenderTargetBitmap frame = new RenderTargetBitmap(
                    (int)(bmpImage.PixelWidth * resizeFactor),
                    (int)(bmpImage.PixelHeight * resizeFactor),
                    96,
                    96,
                    PixelFormats.Pbgra32);

                frame.Render(visual);
                imgPicture.Source = frame;
            }
        }

        private async Task<Tuple<FaceRectangle[], FaceAttributes[], string[]>> FaceDetectionProgress(string pathToImage)
        {
            try
            {
                using (Stream imgFS = File.OpenRead(pathToImage))
                {
                    var additionalAttrs = new FaceAttributeType[] {
                        FaceAttributeType.Age,
                        FaceAttributeType.Gender
                    };

                    var faces = await faceApiConnection.DetectAsync(imgFS, returnFaceAttributes: additionalAttrs);

                    var facesIds = faces.Select(face => face.FaceId).ToArray();
                    var facesOkvirjis = faces.Select(face => face.FaceRectangle);
                    var facesAttrs = faces.Select(face => face.FaceAttributes);

                    var results = await faceApiConnection.IdentifyAsync(userGroupId, facesIds);
                    List<string> names = new List<string>();
                    foreach (var identifyResult in results)
                    {
                        if (identifyResult.Candidates.Length != 0)
                        {
                            var kandidatId = identifyResult.Candidates[0].PersonId;
                            var person = await faceApiConnection.GetPersonAsync(userGroupId, kandidatId);
                            names.Add(person.Name);
                        }
                        else
                            names.Add("neznan");
                    }

                    return new Tuple<FaceRectangle[], FaceAttributes[], string[]>(facesOkvirjis.ToArray(), facesAttrs.ToArray(), names.ToArray());
                }
            }
            catch (Exception)
            {
                return new Tuple<FaceRectangle[], FaceAttributes[], string[]>(new FaceRectangle[0], new FaceAttributes[0], new string[0]);
            }
        }
    }
}
