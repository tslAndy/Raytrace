using System.Runtime.InteropServices;
using UnityEngine;

namespace Rtx
{
    [StructLayout(LayoutKind.Sequential)]
    public struct Bnd
    {
        public Vector3 min,
            max;

        public Bnd(Vector3 min, Vector3 max)
        {
            this.min = min;
            this.max = max;
        }

        public void AddMargin(float margin)
        {
            min -= new Vector3(margin, margin, margin);
            max += new Vector3(margin, margin, margin);
        }

        private bool isCorrect => -1e10f < min.x && min.x < 1e10f;

        public Vector3 size => isCorrect ? max - min : Vector3.zero;
        public Vector3 center => isCorrect ? 0.5f * (min + max) : Vector3.zero;
        public float surface
        {
            get
            {
                if (!isCorrect)
                    return 0.0f;
                Vector3 s = max - min;
                return 2.0f * (s.x * s.z + s.x * s.y + s.y * s.z);
            }
        }

        public static Bnd Zero =>
            new Bnd { min = Vector3.positiveInfinity, max = Vector3.negativeInfinity };

        public static explicit operator Bnd(Bounds bounds) =>
            new Bnd { min = bounds.min, max = bounds.max };

        public static Bnd operator +(Bnd left, Bnd right) =>
            new Bnd
            {
                min = Vector3.Min(left.min, right.min),
                max = Vector3.Max(left.max, right.max),
            };

        public override string ToString()
        {
            return $"{min}    {max}";
        }
    }
}
