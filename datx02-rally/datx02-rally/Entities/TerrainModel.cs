﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace datx02_rally
{
    public class TerrainModel
    {
        private GraphicsDevice device;

        private VertexPositionNormalTexture[] vertices;
        private VertexBuffer vertexbuffer;

        private IndexBuffer indexBuffer;

        private BasicEffect effect;

        public Matrix Projection { get { return effect.Projection; } set { effect.Projection = value; } }
        
        public TerrainModel(Vector2 start, Vector2 end, float uvScale, GraphicsDevice device, Texture2D texture, Matrix projection, Matrix world)
        {
        }

        public TerrainModel (GraphicsDevice device, int width, int height, float triangleSize)
        {
            this.device = device;

            effect = new BasicEffect(device);
            effect.EnableDefaultLighting();
            effect.World = Matrix.Identity;

            vertices = new VertexPositionNormalTexture[width * height];

            for (int x = 0; x < width; x++)
            {
                for (int z = 0; z < height; z++)
                {
                    vertices[x * width + z] = new VertexPositionNormalTexture(
                        new Vector3(x * triangleSize, 0, z * triangleSize), 
                        Vector3.Up, 
                        new Vector2(x % 2, z % 2));
                }
            }

            int[] indices = new int[width * height];
            indices[0] = 0;
            indices[1] = width;

            int offset = 0;

            for (int i = 2; i < indices.Length + offset; i++)
            {
                if (i % 3 < 2)
                    indices[i - offset] = indices[i - 2 - offset];
                else
                    indices[i - offset] = 1 + i / 2 + (i % 2 == 0 ? 0 : width);

                if (indices[i - offset] == 2 * width - 1)
                {
                    i += 6;
                    offset += 6;
                }
            }

            indexBuffer = new IndexBuffer(device, typeof(int), width * height, BufferUsage.None);
            indexBuffer.SetData(indices);

            vertexbuffer = new VertexBuffer(device,
                    typeof(VertexPositionNormalTexture), vertices.Length,
                    BufferUsage.None);
            vertexbuffer.SetData(vertices);
        }

        
        public void Draw(Matrix view)
        {
            effect.View = view;
            effect.DiffuseColor = Color.LightBlue.ToVector3();

            foreach (EffectPass pass in effect.CurrentTechnique.Passes)
            {
                pass.Apply();
                device.Indices = indexBuffer;
                device.SetVertexBuffer(vertexbuffer);
                device.DrawPrimitives(PrimitiveType.TriangleList, 0, vertexbuffer.VertexCount);
            }
        }
    }
}
