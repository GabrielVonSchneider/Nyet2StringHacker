namespace Nyet2Hacker
{
    internal class Constants
    {
        public const int ovlArrayLength = 588;
        public const int ovlArrayBase = 0x1CCFC;
        public const int ovlStringBase = 0x1D810;

        /// <summary>
        /// The string table seems to start at this offset,
        /// so it is considered the minimum.
        /// </summary>
        public const int ovlMinOffset = 0x16;

        //if we somehow go beyond this, we're in trouble:
        public const int ovlMax = 0x20DFC;
    }
}
