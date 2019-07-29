using Microsoft.Win32;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
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

    static class Extensions
    {
        public static void FocusIndex(this ListBox self, int i)
        {
            self.SelectedIndex = i;
            var container = self.ItemContainerGenerator.ContainerFromIndex(i);
            if (container is UIElement uiElement && uiElement.Focusable)
            {
                uiElement.Focus();
            }
        }
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
            internal set => this.Set(
                v => this.line.TransText = v,
                value,
                this.line.TransText
            );
        }

        public bool Done
        {
            get => this.line.Done;
            set => this.Set(
                v => this.line.Done = v,
                value,
                this.line.Done
            );
        }
    }

    public class MainWindowViewModel : PropertyChangedBase
    {
        private string projectPath;
        private bool dirty;
        private int selectedIndex;
        private string workText;
        private ReadOnlyCollection<LineViewModel> lines;
        public DelegateCommand DoneCommand { get; }

        private LineViewModel selectedLine;
        private bool textTooLong;

        private int totalStrings;
        private int doneStrings;
        private decimal completionPercent;

        public MainWindowViewModel()
        {
            this.DoneCommand = new DelegateCommand(true, () =>
            {
                if (!(this.SelectedLine is null))
                {
                    this.SelectedLine.Done = !this.SelectedLine.Done;
                }
            });
        }

        internal void Load(ProjectFile proj, string path)
        {
            this.Lines = new ReadOnlyCollection<LineViewModel>(
                proj.Lines.Select(l => new LineViewModel(l)).ToList()
            );

            this.projectPath = path;
            this.CalcCompletion();
        }

        public int TotalStrings
        {
            get => this.totalStrings;
            private set => this.Set(ref this.totalStrings, value);
        }

        public int DoneStrings
        {
            get => this.doneStrings;
            private set => this.Set(ref this.doneStrings, value);
        }

        public decimal CompletionPercent
        {
            get => this.completionPercent;
            private set => this.Set(ref this.completionPercent, value);
        }

        public bool TextTooLong
        {
            get => this.textTooLong;
            private set => this.Set(ref this.textTooLong, value);
        }

        public bool Commit()
        {
            if (this.TextTooLong || this.SelectedLine is null)
            {
                return false;
            }

            this.SelectedLine.TransText = this.WorkText;
            this.CalcCompletion();
            return true;
        }

        private void CalcCompletion()
        {
            this.TotalStrings = this.lines.Count;
            this.DoneStrings = this.lines.Where(l => l.Done).Count();
            if (this.totalStrings == 0)
            {
                this.CompletionPercent = 0;
            }
            else
            {
                this.CompletionPercent = decimal.Round(
                    (decimal)this.doneStrings
                        / this.totalStrings
                        * 100,
                    2
                );
            }
        }

        private void Validate()
        {
            var v = this.selectedLine;
            this.TextTooLong = !(v is null)
                && v.OriginalText?.Length < this.WorkText?.Length;
        }

        public LineViewModel SelectedLine
        {
            get => this.selectedLine;
            private set => this.Set(
                value,
                v =>
                {
                    this.selectedLine = v;
                    this.Validate();
                },
                () => this.selectedLine
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

        public int SelectedIndex
        {
            get => this.selectedIndex;
            set
            {
                this.Set(
                    value,
                    v =>
                    {
                        this.selectedIndex = v;
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
                            this.SelectedLine = selLine;
                        }
                    },
                    () => this.selectedIndex
                );
            }
        }

        public string WorkText
        {
            get => this.workText;
            set => this.Set(
            value,
            v =>
            {
                this.workText = v;
                this.Validate();
            },
            () => this.workText
        );
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
                    t = FileType.Project;
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
                        fileName = null;
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

            this.ViewModel.Load(projFile, fileName);
            this.project = projFile;
            this.LineList.FocusIndex(0);
        }

        private void TextBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                if (this.ViewModel.Commit())
                {
                    int i = this.LineList.SelectedIndex;
                    if (i < 0)
                    {
                        return;
                    }

                    //if (i < this.LineList.Items.Count - 1)
                    //{
                    //    i++;
                    //}

                    this.LineList.FocusIndex(i);
                }
            }
        }

        private void LineList_KeyDown(object sender, KeyEventArgs e)
        {
            int i = this.LineList.SelectedIndex;
            if (e.Key == Key.Enter)
            {
                this.Editor.Focus();
                this.Editor.SelectAll();
            }
            else if (e.Key == Key.J) //Vim delight
            {
                if (i >= 0 && i < this.LineList.Items.Count)
                {
                    i++;
                    this.LineList.FocusIndex(i);
                }
            }
            else if (e.Key == Key.K)
            {
                if (i > 0)
                {
                    i--;
                    this.LineList.FocusIndex(i);
                }
            }
        }

        private void Save(FileType type)
        {
            if (this.project is null)
            {
                return;
            }
            string path;
            if (this.ViewModel?.ProjectPath is string exPath)
            {
                path = exPath;
            }
            else
            {
                var dlg = new SaveFileDialog();
                switch (type)
                {
                    case FileType.Ovl:
                        dlg.Filter = "NYET2.OVL|NYET2.OVL";
                        break;
                    case FileType.Project:
                        dlg.Filter = "Nyet 2 Hacker Files (*.n2h)|*.n2h";
                        break;
                }
                if (dlg.ShowDialog(this) == true)
                {
                    path = dlg.FileName;
                }
                else
                {
                    return;
                }
            }

            FileStream fs;
            try
            {
                fs = File.Open(
                    path,
                    FileMode.OpenOrCreate,
                    FileAccess.ReadWrite
                );
            }
            catch (Exception e)
            {
                string message = $"Failed to open file ${path} for writing.";
                this.LogError(e, message);
                this.WriteError(message);
                return;
            }

            using (fs)
            {
                switch (type)
                {
                    case FileType.Ovl:
                        this.SaveOvl(fs, this.project);
                        break;
                    default:
                    case FileType.Project:
                        this.SaveProject(fs, this.project);
                        break;
                }
            }
        }

        const int ovlArrayLength = 588;
        const int ovlArrayBase = 0x1CCFC;
        const int ovlStringBase = 0x1D810;

        //if we somehow go beyond this, we're in trouble:
        const int ovlMax = 0x20DFC;
        private void ovlEof()
        {
            this.WriteError("Encountered end of file while reading .ovl.");
        }

        static readonly Encoding ovlEncoding = Encoding.GetEncoding(437);
        private ProjectFile project;

        private bool LoadOvl(FileStream fs, out ProjectFile proj)
        {
            proj = null;
            if (fs.Length < 0x20DFC)
            {
                ovlEof();
                return false;
            }

            proj = new ProjectFile
            {
                Lines = new List<Line>(ovlArrayLength)
            };
            var buffer = new byte[256];
            long posForArr = ovlArrayBase;
            for (int i = 0; i < ovlArrayLength; i++)
            {
                fs.Seek(posForArr, SeekOrigin.Begin);
                if (fs.Read(buffer, 0, 4) != 4)
                {
                    ovlEof();
                    return false;
                }

                byte strLen = buffer[0];
                int offset = ParseLittleEndInt16(buffer, 2);
                int strLoc = offset + ovlStringBase;
                if (strLoc + strLen > ovlMax)
                {
                    ovlEof();
                    return false;
                }

                //now read the string.
                posForArr = fs.Position;
                fs.Seek(strLoc, SeekOrigin.Begin);
                fs.Read(buffer, 0, strLen);
                string text;
                try
                {
                    var enc = ovlEncoding;
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

        private void SaveProject(FileStream fs, ProjectFile proj)
        {
            var ser = new JsonSerializer();
            fs.SetLength(0);
            using (var tw = new StreamWriter(fs))
            using (var jw = new JsonTextWriter(tw))
            {
                ser.Serialize(jw, proj);
            }
        }

        private bool SaveOvl(FileStream fs, ProjectFile proj)
        {
            if (fs.Length < 0x20DFC)
            {
                ovlEof();
                return false;
            }

            var buffer = new byte[2];
            foreach (var line in proj.Lines)
            {
                if (!line.Done || line.TransText is null)
                {
                    continue;
                }

                //Write the length byte:
                fs.Seek(ovlArrayBase + (line.Index * 4), SeekOrigin.Begin);
                fs.WriteByte((byte)line.TransText.Length);

                //And write the string.
                fs.Seek(1, SeekOrigin.Current);
                fs.Read(buffer, 0, 2);
                var offset = ParseLittleEndInt16(buffer, 0);
                fs.Seek(offset + ovlStringBase, SeekOrigin.Begin);
                var bytes = ovlEncoding.GetBytes(line.TransText);
                fs.Write(bytes, 0, bytes.Length);
            }

            return true;
        }

        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            if ((e.KeyboardDevice.Modifiers & ModifierKeys.Control) > 0)
            {
                if (e.Key == Key.E)
                {
                    this.Save(FileType.Ovl);
                }
                else if (e.Key == Key.S)
                {
                    this.Save(FileType.Project);
                }
            }
        }
    }
}
