using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;

namespace Nyet2Hacker
{
    internal enum FileType
    {
        Ovl,
        Project
    }

    public class LineViewModel : PropertyChangedBase
    {
        private readonly Line line;

        public LineViewModel(Line line)
        {
            this.line = line;
        }

        public string OriginalText
        {
            get => this.line.OriginalText;
        }

        public string TransText
        {
            get => this.line.TransText;
        }

        public bool Done
        {
            get => this.line.Done;
            set => this.Set(v => this.line.Done = v, value, this.line.Done);
        }
    }

    public class MainWindowViewModel : PropertyChangedBase
    {
        private string projectPath;
        private bool dirty;
        private int selectedLine;
        private string workText;
        private ReadOnlyCollection<LineViewModel> lines;


        internal void Load(ProjectFile proj)
        {
            this.Lines = new ReadOnlyCollection<LineViewModel>(
                proj.Lines.Select(l => new LineViewModel(l)).ToList()
            );
        }

        public string ProjectPath
        {
            get => this.projectPath;
            set => this.Set(ref this.projectPath, value);
        }
        public bool Dirty
        {
            get => this.dirty;
            private set => this.Set(ref this.dirty, value);
        }
        public int SelectedLine
        {
            get => this.selectedLine;
            set
            {
                this.Set(
                    value,
                    v => 
                    {
                        this.selectedLine = v;
                        if (v < this.lines.Count && v >= 0)
                        {
                            var selLine = this.lines[v];
                            if (string.IsNullOrEmpty(selLine.TransText))
                            {
                                this.WorkText = selLine.OriginalText;
                            }
                            else
                            {
                                this.WorkText = selLine.TransText;
                            }
                        }
                    },
                    () => this.selectedLine
                );
            }
        }
        public string WorkText
        {
            get => this.workText;
            set => this.Set(ref this.workText, value);
        }
        public ReadOnlyCollection<LineViewModel> Lines
        {
            get => this.lines;
            private set => this.Set(ref this.lines, value);
        }
    }

    public partial class MainWindow : Window
    {
        private const string projectExtension = ".n2h";

        public MainWindow()
        {
            InitializeComponent();
            this.DataContext = new MainWindowViewModel();
        }

        private MainWindowViewModel ViewModel =>
            (MainWindowViewModel)this.DataContext;

        private void WriteError(string error)
        {
            var p = new Paragraph(new Run(error))
            {
                Foreground = new SolidColorBrush(Colors.Red)
            };

            this.output.Document.Blocks.Add(p);
        }

        private void LogError(Exception e, string message)
        {
            string exPlusMessage = message + $"Exception: \"{e}\"";
            void writeLoggingError(Exception le)
            {
                this.WriteError(
                    "An error occured during logging."
                    + Environment.NewLine
                    + "This will make it more difficult to diagnose errors in the future."
                    + Environment.NewLine
                    + "Please make sure that you have access to your %APPDATA% folder."
                    + Environment.NewLine
                    + $"The error was: \"{le}\""
                    + "The ogininal error should follow right away."
                );

                this.WriteError(exPlusMessage);
            }

            string folder = Path.Combine(
                Environment.GetFolderPath(
                    Environment.SpecialFolder.ApplicationData
                ),
                "NyetIIHacker"
            );

            try
            {
                Directory.CreateDirectory(folder);
            }
            catch (Exception le)
            {
                writeLoggingError(le);
                return;
            }

            string filePath = Path.Combine(folder, "log.txt");
            try
            {
                File.AppendAllText(
                    filePath,
                    exPlusMessage
                );
            }
            catch (Exception le)
            {
                writeLoggingError(le);
                return;
            }

            this.WriteError(message);
        }

        private void WriteNeutral(string t)
        {
            this.output.Document.Blocks.Add(new Paragraph(new Run(t)));
        }

        private static bool GetFileType(string filePath, out FileType t)
        {
            t = default;
            if (string.IsNullOrEmpty(filePath))
            {
                return false;
            }

            var ext = Path.GetExtension(filePath);
            switch (ext.ToLower())
            {
                case ".ovl":
                    t = FileType.Ovl;
                    return true;
                case projectExtension:
                    return true;
                default:
                    return false;
            }
        }

        private void Window_DragEnter(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                var files = (string[])e.Data.GetData(DataFormats.FileDrop);
                if (files.Length > 1)
                {
                    this.WriteError("Only one file at a time is accepted.");
                }
                else if (files.Length == 1)
                {
                    if (GetFileType(files[0], out _))
                    {
                        e.Effects = DragDropEffects.Copy;
                    }
                }
            }
        }

        private void Window_Drop(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                var files = (string[])e.Data.GetData(DataFormats.FileDrop);
                if (files.Length == 1 && GetFileType(files[0], out var t))
                {
                    this.LoadFile(files[0], t);
                }
            }
        }

        private static int ParseLittleEndInt16(byte[] buffer, int offset)
        {
            return buffer[offset] + (buffer[offset + 1] << 8);
        }

        private static string FormatBrackety<T>(IEnumerable<T> self)
        {
            var sb = new StringBuilder("[");
            bool prev = false;
            using (var en = self.GetEnumerator())
            {
                while (true)
                {
                    if (!en.MoveNext())
                    {
                        break;
                    }

                    if (prev)
                    {
                        sb.Append(", ");
                    }
                    sb.Append(en.Current);
                    prev = true;
                }
            }

            sb.Append("]");
            return sb.ToString();
        }

        private bool LoadOvl(FileStream fs, out ProjectFile proj)
        {
            const int arrayLength = 588;
            const int arrayBase = 0x1CCFC;
            const int stringBase = 0x1D810;

            void eof()
            {
                this.WriteError("Encountered end of file while reading .ovl.");
            }

            //if we somehow go beyond this, we're in trouble:
            const int max = 0x20DFC;

            proj = null;
            if (fs.Length < 0x20DFC)
            {
                eof();
                return false;
            }

            proj = new ProjectFile
            {
                Lines = new List<Line>(arrayLength)
            };
            var buffer = new byte[256];
            long posForArr = arrayBase;
            for (int i = 0; i < arrayLength; i++)
            {
                fs.Seek(posForArr, SeekOrigin.Begin);
                if (fs.Read(buffer, 0, 4) != 4)
                {
                    eof();
                    return false;
                }

                byte strLen = buffer[0];
                int offset = ParseLittleEndInt16(buffer, 2);
                int strLoc = offset + stringBase;
                if (strLoc + strLen > max)
                {
                    eof();
                    return false;
                }

                //now read the string.
                posForArr = fs.Position;
                fs.Seek(strLoc, SeekOrigin.Begin);
                fs.Read(buffer, 0, strLen);
                string text;
                try
                {
                    var enc = Encoding.GetEncoding(437);
                    text = enc.GetString(buffer, 0, strLen);
                }
                catch (Exception e)
                {
                    this.LogError(
                        e,
                        $"Falied to decode byte sequence {FormatBrackety(buffer.Take(strLen))} at {strLoc}."
                    );

                    return false;
                }

                proj.Lines.Add(new Line
                {
                    Done = false,
                    Index = i,
                    OriginalText = text
                });
            }

            return true;
        }

        private bool LoadProj(FileStream fs, out ProjectFile proj)
        {
            try
            {
                var s = new JsonSerializer();
                using (var sr = new StreamReader(fs, Encoding.UTF8))
                using (var jsonReader = new JsonTextReader(sr))
                {
                    proj = s.Deserialize<ProjectFile>(jsonReader);
                }

                return true;
            }
            catch (Exception e)
            {
                string message = $"Unable to read project from file {fs.Name}.";
                this.LogError(e, message);
                this.WriteError(message);
                proj = default;
                return false;
            }
        }

        private void LoadFile(string fileName, FileType type)
        {
            FileStream fs;
            try
            {
                fs = File.Open(fileName, FileMode.Open);
            }
            catch (Exception e)
            {
                const string message = "Unable to open the file \"{fileName}\".";
                this.LogError(e, message);
                this.WriteError(message);
                return;
            }

            ProjectFile projFile;
            bool ok;
            using (fs)
            {
                switch (type)
                {
                    case FileType.Ovl:
                        ok = this.LoadOvl(fs, out projFile);
                        break;
                    default:
                    case FileType.Project:
                        ok = this.LoadProj(fs, out projFile);
                        break;
                }
            }

            if (!ok)
            {
                return;
            }

            this.ViewModel.Load(projFile);
        }

        private void TextBox_KeyDown(object sender, KeyEventArgs e)
        {
        }
    }
}
