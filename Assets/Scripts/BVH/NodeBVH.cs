using System.Runtime.InteropServices;

namespace Rtx.BVH
{
    [StructLayout(LayoutKind.Sequential)]
    public struct NodeBVH
    {
        public Bnd bnd;

        private int _start,
            _end;

        public NodeBVH(int start, int end, Bnd bnd)
        {
            this._start = start;
            this._end = end;
            this.bnd = bnd;
        }

        public int start
        {
            get => _start;
            set => _start = value;
        }

        public int end
        {
            get => _end;
            set => _end = value;
        }

        public float sah => (end - start) * bnd.surface;

        /*
         * We can safely use start as variable to store child index (for keeping struct size 32 bytes)
         * When node added to stack it goes first as a leaf, containing start and end indices
         * when it gets converted to branch, start and end aren't required anymore
         * because child nodes will contain this information, so we can use start as child index
         * */
        public int childIndex
        {
            get => -_start;
            set
            {
                _start = -value;
                _end = -1;
            }
        }

        public bool isLeaf => _start >= 0;
    }
}
