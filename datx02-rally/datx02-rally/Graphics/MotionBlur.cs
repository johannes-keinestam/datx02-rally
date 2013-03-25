﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework;

namespace datx02_rally.Graphics
{
    public class MotionBlur
    {
        #region Fields and Properties
        private Game1 game;
        private GraphicsDevice device;
        private SpriteBatch spriteBatch;

        private Effect effect;
        private RenderTarget2D motionBlurTexture;

        /// <summary>
        /// The number of samples to blur with
        /// </summary>
        public int NumSamples { get; set; }

        /// <summary>
        /// The scalar to size the blur
        /// </summary>
        public float Size { get; set; }
        #endregion

        public MotionBlur(Game1 game)
        {
            this.game = game;
            this.device = game.GraphicsDevice;
            this.spriteBatch = game.spriteBatch;
            effect = game.Content.Load<Effect>(@"Effects\MotionBlur");

            motionBlurTexture = new RenderTarget2D(device, device.Viewport.Width, device.Viewport.Height,
                false, SurfaceFormat.Color, DepthFormat.Depth24);

            NumSamples = 4;
            Size = 30.0f;
        }

        public Texture2D PerformMotionBlur(Texture2D srcTexture, Texture2D depthTexture, Matrix viewProjectionInverse, Matrix previousViewProjection)
        {
            effect.Parameters["NumSamples"].SetValue(NumSamples);
            effect.Parameters["Size"].SetValue(Size);
            effect.Parameters["DepthTexture"].SetValue(depthTexture);
            effect.Parameters["ViewProjectionInverse"].SetValue(viewProjectionInverse);
            effect.Parameters["PreviousViewProjection"].SetValue(previousViewProjection);

            device.SetRenderTarget(motionBlurTexture);
            spriteBatch.Begin(0, BlendState.Opaque, null, null, null, effect);
            device.Clear(Color.Black);
            spriteBatch.Draw(srcTexture, Vector2.Zero, Color.White);
            spriteBatch.End();

            device.SetRenderTarget(null);

            return (Texture2D)motionBlurTexture;
        }
    }
}