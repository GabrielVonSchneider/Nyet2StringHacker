using System.Collections.Generic;

namespace Nyet2Hacker
{
    public class Line
    {
        public int Index { get; set; }
        public string OriginalText { get; set; }
        public string TransText { get; set; }
        public bool Done { get; set; }
        public int Offset { get; set; }
        public int OffsetMod { get; set; }

        public string GetEffectiveText()
        {
            if (string.IsNullOrEmpty(this.TransText))
            {
                return this.OriginalText ?? "";
            }

            return this.TransText;
        }
    }

    internal class ProjectFile
    {
        public List<Line> Lines { get; set; }
    }
}
