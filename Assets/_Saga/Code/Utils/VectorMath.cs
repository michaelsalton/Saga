using UnityEngine;

namespace _Saga.Code.Utils
{
    public static class VectorMath
    {
        private static Matrix4x4 _isoMatrix = Matrix4x4.Rotate(Quaternion.Euler(0, 45, 0));
        
        public static Vector3 ConvertToIsometric(this Vector3 input)
        {
            return _isoMatrix.MultiplyPoint3x4(input);
        }
    }
}