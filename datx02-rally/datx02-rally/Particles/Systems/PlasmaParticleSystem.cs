﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Particle3DSample;
using Microsoft.Xna.Framework.Graphics;

namespace datx02_rally
{
    class PlasmaParticleSystem : Particle3DSample.ParticleSystem
    {
        public PlasmaParticleSystem(Game game, ContentManager content)
            : base(game, content)
        { }

        protected override void InitializeSettings(ParticleSettings settings)
        {
            settings.TextureName = @"Particles/plasma";

            settings.MaxParticles = 10000;

            settings.Duration = TimeSpan.FromSeconds(1.5f);

            settings.MinHorizontalVelocity = 5;
            settings.MaxHorizontalVelocity = 10;

            settings.MinVerticalVelocity = 5;
            settings.MaxVerticalVelocity = 15;

            settings.EndVelocity = 0.75f;

            settings.MinRotateSpeed = -1;
            settings.MaxRotateSpeed = 1;

            settings.MinStartSize = 25;
            settings.MaxStartSize = 25;

            settings.MinEndSize = 25;
            settings.MaxEndSize = 25;

            settings.MinColor = settings.MaxColor = Color.Cyan;

            settings.BlendState = BlendState.Additive;

            this.VisibleChanged += delegate
            {
                Console.WriteLine("Vis: " + this.Visible);
            };

            this.EnabledChanged += delegate
            {
                Console.WriteLine("Enabled: " + this.Enabled);
            };
        }
    }
}
