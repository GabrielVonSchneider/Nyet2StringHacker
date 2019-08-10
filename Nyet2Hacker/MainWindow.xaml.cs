using Microsoft.Win32;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using static Nyet2Hacker.Constants;

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

        public int Index => this.line.Index;

        public int Offset => this.line.Offset;
        public int OffsetMod
        {
            get => this.line.OffsetMod;
            internal set => this.Set(
                v =>
                {
                    this.line.OffsetMod = v;
                    this.Notify(nameof(this.EffOffset));
                },
                value,
                this.OffsetMod
            );
        }

        public int EffOffset => this.line.EffOffset;

        internal string GetEffectiveText()
        {
            return this.line.GetEffectiveText();
        }
    }

    public class TestMainWindowViewModel : MainWindowViewModel
    {
        public TestMainWindowViewModel()
        {
            this.Lines = new ReadOnlyCollection<LineViewModel>(
                Enumerable.Repeat<LineViewModel>(null, 200).ToList()
            );

            this.DoneStrings = this.Lines.Count;
            this.TotalStrings = this.Lines.Count;
            this.CompletionPercent = 12.34m;
        }
    }

    public class MainWindowViewModel : PropertyChangedBase
    {
        private string projectPath;
        private bool dirty;
        private int selectedIndex = -1;
        private string workText;
        private ReadOnlyCollection<LineViewModel> lines;
        public DelegateCommand DoneCommand { get; }
        public DelegateCommand PosModCommand { get; }
        public DelegateCommand NegModCommand { get; }
        private bool canCommit;
        private bool loaded;

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
                    this.Dirty = true;
                    this.SelectedLine.Done = !this.SelectedLine.Done;
                }
            });

            this.PosModCommand = new DelegateCommand(false, () =>
            {
                if (this.selectedIndex > 0 &&
                    this.CanPosMod(this.SelectedIndex))
                {
                    this.ModOffset(this.SelectedIndex, +1);
                    this.Validate();
                }
            });

            this.NegModCommand = new DelegateCommand(false, () =>
            {
                if (this.selectedIndex > 0 &&
                    this.CanNegMod(this.SelectedIndex))
                {
                    this.ModOffset(this.SelectedIndex, -1);
                    this.Validate();
                }
            });
        }

        public int MaxLengthAt(int i)
        {
            if (i < 0 || i >= this.lines.Count)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(i),
                    i,
                    "Index out of range."
                );
            }

            if (i == this.lines.Count - 1)
            {
                var lastLine = this.lines[i];
                return lastLine.OriginalText.Length - lastLine.OffsetMod;
            }

            var me = this.lines[i];
            var next = this.lines[i + 1];
            int myOff = me.Offset + this.lines[i].OffsetMod;
            int nextOff = next.Offset + this.lines[i + 1].OffsetMod;
            return nextOff - myOff;
        }

        private bool CanPosMod(int i)
        {
            //just make sure you don't walk over yourself.
            int length = this.lines[i].GetEffectiveText().Length;
            return length > 1 && this.MaxLengthAt(i) > length;
        }

        private bool CanNegMod(int i)
        {
            if (i == 0)
            {
                return false;
            }

            var line = this.lines[i];
            var newOffset = line.Offset + line.OffsetMod - 1;

            //find the previous line.
            LineViewModel prevLine = null;
            int p;
            for (p = i - 1; p >= 0; p--)
            {
                if (this.Lines[p].Offset != line.Offset)
                {
                    prevLine = this.Lines[p];
                    break;
                }
            }

            if (prevLine == null)
            {
                return newOffset >= ovlMinOffset;
            }
            else
            {
                var prevMaxLength = this.MaxLengthAt(p);
                return prevMaxLength > 1
                    && prevMaxLength > prevLine.GetEffectiveText().Length;
            }
        }

        private List<LineViewModel> GetAdjacents(int i, bool includeCenter)
        {
            var adjacents = new List<LineViewModel>();
            var center = this.lines[i];
            for (int a = i + 1; i < this.lines.Count; i++)
            {
                var cand = this.lines[a];
                if (cand.Offset != center.Offset)
                {
                    break;
                }

                adjacents.Add(cand);
            }

            if (includeCenter)
            {
                adjacents.Add(center);
            }

            for (int a = i - 1; a >= 0; a--)
            {
                var cand = this.lines[a];
                if (cand.Offset != center.Offset)
                {
                    break;
                }

                adjacents.Add(cand);
            }

            return adjacents;
        }

        public void ModOffset(int i, int dir)
        {
            if (dir == 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(dir),
                    "Cannot be 0."
                );
            }

            bool can;
            if (dir > 0)
            {
                dir = 1;
                can = this.CanPosMod(i);
            }
            else
            {
                can = this.CanNegMod(i);
                dir = -1;
            }

            if (!can)
            {
                throw new InvalidOperationException(
                    $"Tried to mod offset at i with {dir} when unable to do so."
                );
            }

            var adjacents = this.GetAdjacents(i, includeCenter: true);
            foreach (var adj in adjacents)
            {
                adj.OffsetMod += dir;
            }
        }

        private int currentMaxLength;
        public int CurrentMaxLength
        {
            get => this.currentMaxLength;
            private set => this.Set(ref this.currentMaxLength, value);
        }

        internal void Load(ProjectFile proj, string path)
        {
            this.Lines = new ReadOnlyCollection<LineViewModel>(
                proj.Lines.Select(l => new LineViewModel(l)).ToList()
            );

            this.Loaded = true;
            this.projectPath = path;
            this.CalcCompletion();
        }

        public bool Loaded
        {
            get => this.loaded;
            private set => this.Set(ref this.loaded, value);
        }

        public int TotalStrings
        {
            get => this.totalStrings;
            protected set => this.Set(ref this.totalStrings, value);
        }

        public int DoneStrings
        {
            get => this.doneStrings;
            protected set => this.Set(ref this.doneStrings, value);
        }

        public decimal CompletionPercent
        {
            get => this.completionPercent;
            protected set => this.Set(ref this.completionPercent, value);
        }

        public bool TextTooLong
        {
            get => this.textTooLong;
            private set => this.Set(
                v =>
                {
                    this.textTooLong = value;
                    this.Notify(nameof(this.CanCommit));
                },
                value,
                this.textTooLong
            );
        }

        public bool CanCommit
        {
            get => this.canCommit;
            private set => this.Set(ref this.canCommit, value);
        }

        public bool Commit()
        {
            if (this.TextTooLong || this.SelectedLine is null)
            {
                return false;
            }

            this.Dirty = true;
            var adjacents = this.GetAdjacents(
                this.SelectedIndex,
                includeCenter: true
            );

            foreach (var adj in adjacents)
            {
                if (adj.OriginalText.Length
                    == this.SelectedLine.OriginalText.Length)
                {
                    adj.TransText = this.WorkText;
                }
            }

            this.CalcCompletion();
            this.Validate();
            return true;
        }

        public void CalcCompletion()
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
            int i = this.selectedIndex;
            this.TextTooLong = !(v is null)
                && i > 0 && i <= this.lines.Count
                && this.workText.Length > (
                    this.CurrentMaxLength = this.MaxLengthAt(i)
                );
            this.CanCommit = !(v is null) && !this.textTooLong;

            this.NegModCommand.SetCanExecute(i > 0 && this.CanNegMod(i));
            this.PosModCommand.SetCanExecute(i > 0 && this.CanPosMod(i));
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
            set => this.Set(ref this.dirty, value);
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
            protected set => this.Set(ref this.lines, value);
        }

        public void UnmarkDone()
        {
            foreach (var line in this.Lines)
            {
                line.Done = false;
            }

            this.Dirty = true;
            this.DoneStrings = 0;
            this.CompletionPercent = 0;
        }
    }

    public partial class MainWindow : Window
    {
        private const string projectExtension = ".n2h";

        public MainWindow()
        {
            this.InitializeComponent();
            this.SearchPanel.Visibility = Visibility.Collapsed;
        }

        private MainWindowViewModel ViewModel =>
            (MainWindowViewModel)this.DataContext;

        private class PendProject
        {
            public PendProject(string fileName, ProjectFile proj)
            {
                this.Name = fileName;
                this.File = proj;
            }

            public string Name { get; }
            public ProjectFile File { get; }
        }
        private ProjectFile project;
        private PendProject pendingProject;

        private void WriteLine(string text)
        {
            Paragraph p;
            if (this.Output.Document.Blocks.LastBlock is Paragraph fp)
            {
                p = fp;
            }
            else
            {
                p = new Paragraph();
                this.Output.Document.Blocks.Add(p);
            }

            if (!(p.Inlines.LastInline is LineBreak))
            {
                p.Inlines.Add(new LineBreak());
            }

            p.Inlines.Add(new Run(text));
            this.Output.ScrollToEnd();
        }

        private void WriteError(string error)
        {
            this.WriteP(error, Colors.Red);
        }

        private void WriteP(string text, Color color)
        {
            var p = new Paragraph(new Run(text))
            {
                Foreground = new SolidColorBrush(color)
            };

            this.Output.Document.Blocks.Add(p);
            this.Output.ScrollToEnd();
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

        private bool GetFileType(string filePath, out FileType t)
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
                    return this.pendingProject is null;
                default:
                    return false;
            }
        }

        private void Window_DragEnter(object sender, DragEventArgs e)
        {
            bool ok = false;
            e.Handled = true;
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                var files = (string[])e.Data.GetData(DataFormats.FileDrop);
                if (files.Length > 1)
                {
                    this.WriteError("Only one file at a time is accepted.");
                }
                else if (files.Length == 1)
                {
                    if (this.GetFileType(files[0], out _))
                    {
                        ok = true;
                        e.Effects = DragDropEffects.Copy;
                    }
                }
            }

            if (!ok)
            {
                e.Effects = DragDropEffects.None;
            }
        }

        private void Window_Drop(object sender, DragEventArgs e)
        {
            e.Handled = true;
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                var files = (string[])e.Data.GetData(DataFormats.FileDrop);
                if (files.Length == 1 && this.GetFileType(files[0], out var t))
                {
                    if (this.pendingProject is null)
                    {
                        this.LoadFile(files[0], t);
                    }
                    else
                    {
                        this.LoadOffsets(files[0]);
                    }
                }
            }
        }

        private static int ParseLittleEndInt16(byte[] buffer, int offset)
        {
            return buffer[offset] + (buffer[offset + 1] << 8);
        }

        private static void WriteLittleEndInt16(Stream str, int n)
        {
            str.WriteByte((byte)(n & byte.MaxValue));
            str.WriteByte((byte)((n >> 8) & byte.MaxValue));
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
            }
            catch (Exception e)
            {
                string message = $"Unable to read project from file {fs.Name}.";
                this.LogError(e, message);
                this.WriteError(message);
                proj = default;
                return false;
            }

            if (proj.Lines.Count <= 5)
            {
                return false;
            }

            if (proj.Lines[5].Offset == 0)
            {
                this.WriteP(
                    "This is an old project file without offsets.",
                    Colors.DarkOrange
                );
                this.WriteLine("Please drag the original .ovl on the window, so they can be loaded.");
                this.EnterPrompt(new PendProject(fs.Name, proj));
                return false;
            }

            return true;
        }

        private void Enable(bool enable)
        {
            this.LineList.IsEnabled = enable;
            this.EditPanel.IsEnabled = enable;
            this.SearchPanel.IsEnabled = enable;
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

            this.Load(fileName, projFile);
        }

        private void Load(string fileName, ProjectFile projFile)
        {
            projFile.Lines = projFile.Lines.OrderBy(l => l.Offset).ToList();
            this.ViewModel.Load(projFile, fileName);
            this.project = projFile;

            var gen = this.LineList.ItemContainerGenerator;
            void statusChanged(object sender, EventArgs args)
            {
                if (gen.Status == GeneratorStatus.ContainersGenerated)
                {
                    gen.StatusChanged -= statusChanged;
                    this.LineList.FocusIndex(0);
                }
            };
            if (projFile.Lines.Count > 0)
            {
                gen.StatusChanged += statusChanged;
            }
        }

        private void Commit()
        {
            if (this.ViewModel.Commit())
            {
                int i = this.LineList.SelectedIndex;
                if (i < 0)
                {
                    return;
                }

                this.LineList.FocusIndex(i);
            }
        }

        private void TextBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                this.Commit();
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

        private bool Save(FileType type, bool saveAs = false)
        {
            if (this.project is null)
            {
                return false;
            }
            string path;
            if (this.ViewModel?.ProjectPath is string exPath
                && type == FileType.Project
                && !saveAs)
            {
                path = exPath;
            }
            else
            {
                FileDialog dlg;
                switch (type)
                {
                    case FileType.Ovl:
                        dlg = new OpenFileDialog
                        {
                            Filter = "NYET2.OVL|NYET2.OVL",
                            Title = "Open NYET2.OVL"
                        };
                        break;
                    case FileType.Project:
                        dlg = new SaveFileDialog
                        {
                            Title = "Save Project File",
                            Filter = "Nyet 2 Hacker Files (*.n2h)|*.n2h"
                        };
                        break;
                    default:
                        throw new InvalidOperationException(
                            $"Unknown FileType {type}"
                        );
                }
                this.AllowDrop = false;
                bool ok = dlg.ShowDialog(this) == true;
                this.AllowDrop = true;
                path = dlg.FileName;
                if (!ok)
                {
                    return false;
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
                return false;
            }

            bool saved;
            using (fs)
            {
                switch (type)
                {
                    case FileType.Ovl:
                        saved = this.SaveOvl(fs, this.project);
                        break;
                    default:
                    case FileType.Project:
                        this.SaveProject(fs, this.project);
                        this.ViewModel.ProjectPath = path;
                        saved = true;
                        break;
                }
            }

            if (saved)
            {
                this.ViewModel.Dirty = false;
            }

            return saved;
        }

        private void OvlEof()
        {
            this.WriteError("Encountered end of file while reading .ovl.");
        }

        static readonly Encoding ovlEncoding = Encoding.GetEncoding(437);

        private void LoadOffsets(string filename)
        {
            var pp = this.pendingProject;
            FileStream fs;
            try
            {
                fs = File.OpenRead(filename);
            }
            catch (Exception e)
            {
                string message = $"Unable to open file \"{filename}\" to import offsets.";
                this.LogError(e, message);
                this.WriteError(message);
                return;
            }

            if (fs.Length < ovlMax)
            {
                this.OvlEof();
            }

            int lineCount = pp.File.Lines.Count < ovlArrayLength
                ? pp.File.Lines.Count
                : ovlArrayLength;

            //Calculate a checksum over the table.
            //The length bytes should match.
            int oldChecksum = pp.File.Lines.Sum(l => l.OriginalText.Length);
            int newChecksum = 0;
            fs.Seek(ovlArrayBase, SeekOrigin.Begin);
            var buffer = new byte[2];
            for (int i = 0; i < lineCount; i++)
            {
                newChecksum += fs.ReadByte();
                fs.Position += 1;
                fs.Read(buffer, 0, 2);
                pp.File.Lines[i].Offset = ParseLittleEndInt16(buffer, 0);
            }

            if (!(newChecksum == oldChecksum))
            {
                this.WriteError("The checksums for the .ovl and the .n2h don't match.");
                this.WriteLine("This probably means that the .n2h was generated from a different file.");
                return;
            }

            this.ExitPrompt();
            this.Load(pp.Name, pp.File);
        }

        private bool LoadOvl(FileStream fs, out ProjectFile proj)
        {
            proj = null;
            if (fs.Length < 0x20DFC)
            {
                this.OvlEof();
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
                    this.OvlEof();
                    return false;
                }

                byte strLen = buffer[0];
                int offset = ParseLittleEndInt16(buffer, 2);
                int strLoc = offset + ovlStringBase;
                if (strLoc + strLen > ovlMax)
                {
                    this.OvlEof();
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
                    OriginalText = text,
                    Offset = offset
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
            if (fs.Length < ovlMax)
            {
                this.OvlEof();
                return false;
            }

            foreach (var line in proj.Lines)
            {
                var effText = line.GetEffectiveText();
                var offset = line.EffOffset;

                //Write the length byte:
                fs.Seek(ovlArrayBase + (line.Index * 4), SeekOrigin.Begin);
                fs.WriteByte((byte)effText.Length);

                //Write the offset
                fs.Position += 1;
                WriteLittleEndInt16(fs, offset);

                //And write the string.
                fs.Seek(offset + ovlStringBase, SeekOrigin.Begin);
                var bytes = ovlEncoding.GetBytes(effText);
                fs.Write(bytes, 0, bytes.Length);
            }

            return true;
        }

        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            var mods = e.KeyboardDevice.Modifiers;
            if ((mods & ModifierKeys.Control) > 0)
            {
                if (e.Key == Key.E)
                {
                    this.Save(FileType.Ovl);
                }
                else if (e.Key == Key.S)
                {
                    this.Save(
                        FileType.Project,
                        saveAs: (mods & ModifierKeys.Shift) > 0
                    );
                }
                else if (e.Key == Key.F)
                {
                    this.Search();
                }
                else if (e.Key == Key.OemPlus)
                {
                    if (this.ViewModel.PosModCommand.CanExecute(default))
                    {
                        this.ViewModel.PosModCommand.Execute(default);
                    }
                }
                else if (e.Key == Key.OemMinus)
                {
                    if (this.ViewModel.NegModCommand.CanExecute(default))
                    {

                        this.ViewModel.NegModCommand.Execute(default);
                    }
                }
            }
        }

        private void Search()
        {
            this.SearchPanel.Visibility = Visibility.Visible;
            this.SearchBox.Focus();
            this.SearchBox.SelectAll();
        }

        private void SearchOn(bool up)
        {
            string searchText = this.SearchBox.Text;
            int startIndex = 0;
            if (this.LineList.SelectedIndex >= 0)
            {
                startIndex = this.LineList.SelectedIndex;
            }

            bool match(int index)
            {
                return this.ViewModel.Lines[index].OriginalText?.IndexOf(
                    searchText,
                    StringComparison.CurrentCultureIgnoreCase
                ) >= 0;
            }
            int lineCount = this.LineList.Items.Count;
            int i = startIndex;
            if (up)
            {
                for (i--; i >= 0; i--)
                {
                    if (match(i))
                    {
                        break;
                    }
                }
            }
            else
            {
                for (i++; i < lineCount; i++)
                {
                    if (match(i))
                    {
                        break;
                    }
                }
            }

            if (i >= 0 && i < lineCount)
            {
                this.LineList.SelectedIndex = i;
                this.LineList.ScrollIntoView(this.LineList.Items[i]);
            }
        }

        private void SearchBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                this.SearchOn(
                    up: (e.KeyboardDevice.Modifiers & ModifierKeys.Shift) > 0
                );
            }
            else if (e.Key == Key.Escape)
            {
                this.ExitSearch();
            }
        }

        private void ExitSearch()
        {
            int i = this.LineList.SelectedIndex;
            if (i >= 0 && i < this.LineList.Items.Count)
            {
                this.LineList.FocusIndex(i);
            }
            else if (this.LineList.Items.Count > 0)
            {
                this.LineList.FocusIndex(i);
            }

            this.SearchPanel.Visibility = Visibility.Collapsed;
        }

        private void ExitSearchButton_Click(object sender, RoutedEventArgs e)
        {
            this.ExitSearch();
        }

        private void CommitButton_Click(object sender, RoutedEventArgs e)
        {
            this.Commit();
        }

        private void LineCheckBoxChange(object sender, RoutedEventArgs e)
        {
            this.ViewModel.CalcCompletion();
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            this.Save(FileType.Project);
        }

        protected override void OnClosing(CancelEventArgs e)
        {
            if (this.ViewModel?.Dirty == true)
            {
                var res = MessageBox.Show(
                   this,
                   "There are unsaved changes. Save now?",
                   "Unsaved changes",
                   MessageBoxButton.YesNoCancel,
                   MessageBoxImage.Warning
               );

                e.Cancel = res == MessageBoxResult.Cancel || (
                    res == MessageBoxResult.Yes
                    && !this.Save(FileType.Project)
                );
            }

            base.OnClosing(e);
        }

        private void EnterPrompt(PendProject pendProj)
        {
            this.QuestionPanel.Visibility = Visibility.Visible;
            this.Enable(false);
            this.pendingProject = pendProj;
        }

        private void ExitPrompt()
        {
            this.QuestionPanel.Visibility = Visibility.Collapsed;
            this.Enable(true);
            this.pendingProject = null;
        }

        private void NoButton_Click(object sender, RoutedEventArgs e)
        {
            e.Handled = true;
            this.ExitPrompt();
        }

        private void ExportButton_Click(object sender, RoutedEventArgs e)
        {
            this.Save(FileType.Ovl);
        }

        private void ClearFlags_Click(object sender, RoutedEventArgs e)
        {
            e.Handled = true;
            const string warning = "Warning: This will mark all lines as \"not done\". Proceed?";
            var reply = MessageBox.Show(
                this,
                warning,
                "Clear all done flags",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning
            );

            if (reply == MessageBoxResult.Yes)
            {
                this.ViewModel.UnmarkDone();
            }
        }

        private void SearchButton_Click(object sender, RoutedEventArgs e)
        {
            this.Search();
        }

        private void NextSearchResultButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            e.Handled = true;
            this.SearchOn(up: false);
        }

        private void PreviousSearchResultButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            this.SearchOn(up: true);
        }
    }
}
