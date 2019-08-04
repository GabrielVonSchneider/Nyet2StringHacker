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
    }

    internal class ProjectFile
    {

        private const int ovlArrayLength = 588;
        private const int ovlArrayBase = 0x1CCFC;
        private const int ovlStringBase = 0x1D810;

        //if we somehow go beyond this, we're in trouble:
        const int ovlMax = 0x20DFC;
        public List<Line> Lines { get; set; }
    }
}
