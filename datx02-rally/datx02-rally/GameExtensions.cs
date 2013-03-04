﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.Xna.Framework;

namespace datx02_rally
{
    /// <summary>
    /// Static class to provide extensionmethods for game management. If you don't know what an extension method is, ask me. 
    /// /Marcus
    /// </summary>
    static class GameExtensions
    {
        public static T GetService<T>(this Game game)
        {
            return (T)game.Services.GetService(typeof(T));
        }

        public static Matrix GetRotationMatrix(this Vector3 source, Vector3 target)
        {
            float dot = Vector3.Dot(source, target);
            if (!float.IsNaN(dot))
            {
                float angle = (float)Math.Acos(dot);
                if (!float.IsNaN(angle))
                {
                    Vector3 cross = Vector3.Cross(source, target);
                    if (cross.LengthSquared() == 0)
                        return Matrix.Identity;
                    cross.Normalize();
                    Matrix rotation = Matrix.CreateFromAxisAngle(cross, angle);
                    return rotation;
                }
            }
            return Matrix.Identity;
        }

        public static Vector3 XZPlane(this Vector3 source)
        {
            return new Vector3(source.X, 0, source.Z);
        }
    }
}
