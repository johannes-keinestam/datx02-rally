﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using datx02_rally.Graphics;
using datx02_rally.GameLogic;
using datx02_rally.Entities;
using datx02_rally.ModelPresenters;
using Particle3DSample;
using datx02_rally.Particles.WeatherSystems;
using Microsoft.Xna.Framework.Content;
using datx02_rally.Particles.Systems;
using datx02_rally.MapGeneration;
using datx02_rally.EventTrigger;
using datx02_rally.Components;
using Microsoft.Xna.Framework.Input;

namespace datx02_rally.Menus
{
    public enum GamePlayMode { Singleplayer, Multiplayer }
    class GamePlayView : GameStateView
    {
        #region Field

        GamePlayMode mode;

        public bool Paused { get; private set; }
        PauseMenu pauseMenu;
        public SpriteBatch spriteBatch;

        Matrix projectionMatrix;

        #region PostProcess

        RenderTarget2D postProcessTexture;
        Bloom bloom;
        GaussianBlur gaussianBlur;
        MotionBlur motionBlur;
        Matrix previousViewProjection;
        private bool motionBlurEnabled = false;

        #endregion

        List<GameObject> GraphicalObjects = new List<GameObject>(); 

        #region Animals

        Model birdModel;
        datx02_rally.GameLogic.Curve birdCurve;

        #endregion

        #region Lights

        // Model to represent a location of a pointlight
        Model pointLightModel;
        Model spotLightModel;

        List<PointLight> pointLights = new List<PointLight>();
        List<SpotLight> spotLights = new List<SpotLight>();
        DirectionalLight directionalLight;

        #endregion

        #region Level terrain

        int terrainSegmentSize = 50;
        int terrainSegmentsCount = 12;

        // XZ- & Y scaling.
        Vector3 terrainScale = new Vector3(75, 10000, 75);

        float roadWidth = 10; // #Quads
        float roadFalloff = 30; // #Quads

        RaceTrack raceTrack;
        NavMeshVisualizer navMesh;

        //TerrainModel terrain;
        TerrainModel[,] terrainSegments;

        Effect terrainEffect;

        #endregion

        #region Car

        public Car Car { get; private set; }
        Effect carEffect;
        CarShadingSettings carSettings = new CarShadingSettings()
        {
            MaterialReflection = .9f,
            MaterialShininess = 10
        };

        // Used for raycollision test.
        int lastTriangle;

        #endregion

        #region Particle-systems

        List<ParticleSystem> particleSystems = new List<ParticleSystem>();
        ParticleSystem plasmaSystem;
        ParticleSystem redSystem;
        ParticleSystem yellowSystem;

        ParticleSystem greenSystem;
        ParticleSystem airParticles;

        ParticleEmitter[] dustEmitter;
        ParticleSystem[] dustParticles;

        ParticleSystem smokeSystem;
        float smokeTime;

        #endregion

        #region SkyBox

        Model skyBoxModel;
        Effect skyBoxEffect;
        TextureCube cubeMap;

        #endregion

        #region Weather

        ThunderParticleSystem thunderParticleSystem;
        ThunderBoltGenerator thunderBoltGenerator;

        RainParticleSystem rainSystem;

        #endregion

        #region DynamicEnvironment

        RenderTargetCube refCubeMap;

        #endregion

        #region ShadowMap

        RenderTarget2D shadowMap;
        Effect shadowMapEffect;
        Matrix lightView;
        Matrix lightProjection;

        #endregion

        PrelightingRenderer prelightingRenderer;

        ThirdPersonCamera debugCamera;

        #endregion

        #region Initialization

        public GamePlayView(Game game, int? seed, GamePlayMode mode)
            : base(game, GameState.Gameplay)
        {
            game.GetService<ServerClient>().GamePlay = this;
            int usedSeed = seed.HasValue ? seed.Value : 0;
            UniversalRandom.ResetInstance(usedSeed);
            UniversalRandom.ResetInstance(0);
            this.mode = mode;
            this.Paused = false;
        }

        public override void ChangeResolution()
        {
        }

        public override void Initialize()
        {
            // Components
            var components = gameInstance.Components;
            var services = gameInstance.Services;

            var cameraComponent = new CameraComponent(gameInstance);
            components.Add(cameraComponent);
            services.AddService(typeof(CameraComponent), cameraComponent);

            var hudComponent = new HUDComponent(gameInstance);
            components.Add(hudComponent);
            services.AddService(typeof(HUDComponent), hudComponent);
            
            var carControlComponent = new CarControlComponent(gameInstance);
            components.Add(carControlComponent);
            services.AddService(typeof(CarControlComponent), carControlComponent);

            // Particle systems

            plasmaSystem = new PlasmaParticleSystem(gameInstance, content);
            components.Add(plasmaSystem);
            particleSystems.Add(plasmaSystem);

            redSystem = new RedPlasmaParticleSystem(gameInstance, content);
            components.Add(redSystem);
            particleSystems.Add(redSystem);

            yellowSystem = new YellowPlasmaParticleSystem(gameInstance, content);
            components.Add(yellowSystem);
            particleSystems.Add(yellowSystem);

            greenSystem = new GreenParticleSystem(gameInstance, content);
            components.Add(greenSystem);
            particleSystems.Add(greenSystem);

            airParticles = new AirParticleSystem(gameInstance, content);
            components.Add(airParticles);
            particleSystems.Add(airParticles);

            dustParticles = new ParticleSystem[2];
            dustParticles[0] = new SmokePlumeParticleSystem(gameInstance, content);
            dustParticles[1] = new SmokePlumeParticleSystem(gameInstance, content);
            foreach (var dustSystem in dustParticles)
            {
                components.Add(dustSystem);
                particleSystems.Add(dustSystem);
            }

            thunderParticleSystem = new ThunderParticleSystem(gameInstance, content);
            components.Add(thunderParticleSystem);
            particleSystems.Add(thunderParticleSystem);

            rainSystem = new RainParticleSystem(gameInstance, content);
            components.Add(rainSystem);
            particleSystems.Add(rainSystem);

            smokeSystem = new SmokeCloudParticleSystem(gameInstance, content);
            components.Add(smokeSystem);
            particleSystems.Add(smokeSystem);

            pauseMenu = new PauseMenu(gameInstance);
            pauseMenu.ChangeResolution();

            base.Initialize();
        }

        protected override void LoadContent()
        {
            // Create a new SpriteBatch, which can be used to draw textures.
            spriteBatch = new SpriteBatch(GraphicsDevice);

            projectionMatrix = Matrix.CreatePerspectiveFieldOfView(MathHelper.PiOver4,
                GraphicsDevice.Viewport.AspectRatio, 0.1f, 50000);

            #region Level terrain generation

            int heightMapSize = terrainSegmentsCount * terrainSegmentSize + 1;
            float halfHeightMapSize = heightMapSize / 2f;
            HeightMap heightmapGenerator = new HeightMap(heightMapSize);
            var heightMap = heightmapGenerator.Generate();

            var roadMap = new float[heightMapSize, heightMapSize];
            raceTrack = new RaceTrack(heightMapSize);

            navMesh = new NavMeshVisualizer(GraphicsDevice, raceTrack.Curve, 1500, roadWidth, terrainScale);

            Vector3 lastPosition = raceTrack.Curve.GetPoint(.01f);

            for (float t = 0; t < 1; t += .0002f)
            {
                var e = raceTrack.Curve.GetPoint(t);

                for (float j = -roadFalloff; j <= roadFalloff; j++)
                {
                    var pos = e + j * Vector3.Normalize(Vector3.Cross(lastPosition - e, Vector3.Up));

                    // Indices
                    int x = (int)(pos.X + halfHeightMapSize),
                        z = (int)(pos.Z + halfHeightMapSize);

                    float height = e.Y;

                    if (Math.Abs(j) <= roadWidth)
                    {
                        heightMap[x, z] = height;
                        roadMap[x, z] = 1;
                    }
                    else
                    {
                        float amount = (Math.Abs(j) - roadWidth) / (roadFalloff - roadWidth);
                        heightMap[x, z] = MathHelper.Lerp(height,
                            heightMap[x, z], amount);
                        roadMap[x, z] = amount / 10f;
                    }
                }
                lastPosition = e;
            }

            heightmapGenerator.Smoothen();
            heightmapGenerator.Perturb(30f);

            terrainEffect = content.Load<Effect>(@"Effects\TerrainShading");
            terrainEffect.Parameters["TextureMap0"].SetValue(content.Load<Texture2D>(@"Terrain\sand"));
            terrainEffect.Parameters["TextureMap1"].SetValue(content.Load<Texture2D>(@"Terrain\grass"));
            terrainEffect.Parameters["TextureMap2"].SetValue(content.Load<Texture2D>(@"Terrain\rock"));
            terrainEffect.Parameters["TextureMap3"].SetValue(content.Load<Texture2D>(@"Terrain\snow"));
            terrainEffect.Parameters["Projection"].SetValue(projectionMatrix);

            // Creates a terrainmodel around Vector3.Zero
            terrainSegments = new TerrainModel[terrainSegmentsCount, terrainSegmentsCount];

            float terrainStart = -.5f * heightMapSize;

            for (int z = 0; z < terrainSegmentsCount; z++)
            {
                for (int x = 0; x < terrainSegmentsCount; x++)
                {
                    terrainSegments[x, z] = new TerrainModel(GraphicsDevice,
                        terrainSegmentSize, terrainSegmentsCount, terrainStart,
                        x * terrainSegmentSize, z * terrainSegmentSize,
                        terrainScale, heightMap, roadMap);
                    terrainSegments[x, z].Effect = terrainEffect;
                }
            }


            //terrain = new TerrainModel(GraphicsDevice, 0, mapSize, 0, mapSize, terrainXZScale, terrainYScale, heightMap, roadMap);
            //terrain.Effect = terrainEffect.Clone();

            #endregion

            #region Car

            Car = MakeCar();
            Player localPlayer = gameInstance.GetService<ServerClient>().LocalPlayer;
            gameInstance.GetService<CarControlComponent>().Cars[localPlayer] = Car;

            #endregion

            #region Lights

            PointLight.LoadMaterial(content);

            // Load model to represent our lightsources
            pointLightModel = content.Load<Model>(@"Models\light");
            spotLightModel = content.Load<Model>(@"Models\Cone");
            //directionalLight = new DirectionalLight(new Vector3(-0.6f, -1.0f, 1.0f), new Vector3(1.0f, 0.8f, 1.0f) * 0.4f, Color.White.ToVector3() * 0.2f);
            directionalLight = new DirectionalLight(
                new Vector3(-1.25f, -2f, 5.0f),
                new Vector3(.1f, .099f, .1f), // new Vector3(1.0f, 0.8f, 1.0f), 
                .6f * Color.White.ToVector3());

            gameInstance.Services.AddService(typeof(DirectionalLight), directionalLight);

            int numlights = 50;
            Vector3 pointLightOffset = new Vector3(0, 250, 0);
            for (int i = 0; i < numlights; i++)
            {
                float t = i / (float)numlights;
                Vector3 point = terrainScale * raceTrack.Curve.GetPoint(t);
                //Vector3 point2 = raceTrack.Curve.GetPoint(t + .01f * (t > .5f ? -1 : 1));
                //var heading = (point2 - point1);
                //var side = triangleSize * roadWidth * Vector3.Normalize(Vector3.Cross(Vector3.Up, heading));
                //point1.Y *= heightScale * triangleSize;

                Random random = UniversalRandom.GetInstance();

                Vector3 color = new Vector3(
                    .6f + .4f * (float)random.NextDouble(),
                    .6f + .4f * (float)random.NextDouble(),
                    .6f + .4f * (float)random.NextDouble());
                pointLights.Add(new PointLight(point + pointLightOffset, color, 450));

            }

            

            Vector3 forward = Vector3.Transform(Vector3.Backward,
                Matrix.CreateRotationY(Car.Rotation));
            
            Vector3 position = Car.Position;

            spotLights.Add(new SpotLight(position + new Vector3(0, 50, 0), forward, Color.White.ToVector3(), 45, 45, 500));
            #endregion

            dustEmitter = new ParticleEmitter[]{
                new ParticleEmitter(dustParticles[0], 150, Car.Position),
                new ParticleEmitter(dustParticles[1], 150, Car.Position)
            };

            #region SkySphere

            skyBoxModel = content.Load<Model>(@"Models/skybox");
            skyBoxEffect = content.Load<Effect>(@"Effects/SkyBox");

            cubeMap = new TextureCube(GraphicsDevice, 2048, false, SurfaceFormat.Color);
            string[] cubemapfaces = { @"SkyBoxes/PurpleSky/skybox_right1", 
                @"SkyBoxes/PurpleSky/skybox_left2", 
                @"SkyBoxes/PurpleSky/skybox_top3", 
                @"SkyBoxes/PurpleSky/skybox_bottom4", 
                @"SkyBoxes/PurpleSky/skybox_front5", 
                @"SkyBoxes/PurpleSky/skybox_back6_2" 
            };

            //cubeMap = new TextureCube(GraphicsDevice, 1024, false, SurfaceFormat.Color);
            //string[] cubemapfaces = { 
            //    @"SkyBoxes/StormyDays/stormydays_ft", 
            //    @"SkyBoxes/StormyDays/stormydays_bk", 
            //    @"SkyBoxes/StormyDays/stormydays_up", 
            //    @"SkyBoxes/StormyDays/stormydays_dn", 
            //    @"SkyBoxes/StormyDays/stormydays_rt", 
            //    @"SkyBoxes/StormyDays/stormydays_lf" 
            //};

            //cubeMap = new TextureCube(GraphicsDevice, 1024, false, SurfaceFormat.Color);
            //string[] cubemapfaces = { 
            //    @"SkyBoxes/Miramar/miramar_ft", 
            //    @"SkyBoxes/Miramar/miramar_bk",
            //    @"SkyBoxes/Miramar/miramar_up", 
            //    @"SkyBoxes/Miramar/miramar_dn", 
            //    @"SkyBoxes/Miramar/miramar_rt",
            //    @"SkyBoxes/Miramar/miramar_lf"
            //};


            for (int i = 0; i < cubemapfaces.Length; i++)
                LoadCubemapFace(cubeMap, cubemapfaces[i], (CubeMapFace)i);

            skyBoxEffect.Parameters["SkyboxTexture"].SetValue(cubeMap);

            foreach (var mesh in skyBoxModel.Meshes)
                foreach (var part in mesh.MeshParts)
                    part.Effect = skyBoxEffect;

            #endregion

            #region Weather

            thunderBoltGenerator = new ThunderBoltGenerator(gameInstance, thunderParticleSystem);
            gameInstance.Components.Add(thunderBoltGenerator);

            #endregion

            #region Foliage

            OakTree.LoadMaterial(content);
            BirchTree.LoadMaterial(content);

            int numTrees = 100;
            for (int i = 0; i < numTrees; i++)
            {
                var t = navMesh.triangles[UniversalRandom.GetInstance().Next(navMesh.triangles.Length)];
                float v = (float)UniversalRandom.GetInstance().NextDouble();
                float u = ((float)UniversalRandom.GetInstance().NextDouble() - .5f);
                if (u < 0)
                    u -= .5f;
                else
                    u += 1.5f;

                var treePos = (t.vertices[0] + u * t.ab + v * t.ac) / terrainScale;

                float X = treePos.X + heightMapSize / 2f,
                    Z = treePos.Z + heightMapSize / 2f;

                float Xlerp = X % 1f,
                    Zlerp = Z % 1f;

                int x0 = (int)X,
                    z0 = (int)Z,
                    x1 = x0 + 1,
                    z1 = z0 + 1;

                float height;
                if (Xlerp + Zlerp > 1)
                {
                    height = MathHelper.Lerp(
                        MathHelper.Lerp(heightMap[x0, z1], heightMap[x1, z1], Xlerp),
                        MathHelper.Lerp(heightMap[x1, z0], heightMap[x1, z1], Zlerp),
                        .5f);
                }
                else
                {
                    height = MathHelper.Lerp(
                        MathHelper.Lerp(heightMap[x0, z0], heightMap[x1, z0], Xlerp),
                        MathHelper.Lerp(heightMap[x0, z0], heightMap[x0, z1], Zlerp),
                        .5f);
                }
                treePos.Y = height - 0.002f;
                
                GameObject tree;
                if (UniversalRandom.GetInstance().Next(0, 2) == 0)
                    tree = new OakTree(gameInstance);
                else
                    tree = new BirchTree(gameInstance);

                tree.Position = terrainScale * treePos;
                tree.Scale = 1 + 5 * (float)UniversalRandom.GetInstance().NextDouble();
                tree.Rotation = new Vector3(0, MathHelper.Lerp(0, MathHelper.Pi * 2, (float)UniversalRandom.GetInstance().NextDouble()), 0);

                GraphicalObjects.Add(tree);

            }

            // {
            //     mushroomGroup = Content.Load<Model>(@"Foliage\MushroomGroup");
            //     ModelMesh mesh = mushroomGroup.Meshes.First<ModelMesh>();
            //     foreach (ModelMeshPart part in mesh.MeshParts)
            //     {
            //         part.Effect = alphaMapEffect.Clone();
            //         part.Effect.Parameters["ColorMap"].SetValue(Content.Load<Texture2D>(@"Foliage\Textures\mushrooms-c"));
            //         part.Effect.Parameters["NormalMap"].SetValue(Content.Load<Texture2D>(@"Foliage\Textures\mushrooms-n"));
            //     }
            // }

            #endregion

            foreach (GameObject obj in GraphicalObjects)
            {
                pointLights.Add(new PointLight(obj.Position + Vector3.Up * 500, new Vector3(0.7f, 0.7f, 0.7f), 450));
            }
            GraphicalObjects.AddRange(pointLights);

            #region Animals

            birdModel = gameInstance.Content.Load<Model>(@"Models\bird");
            birdCurve = new BirdCurve();

            #endregion

            #region Cameras

            var input = gameInstance.GetService<InputComponent>();
            debugCamera = new ThirdPersonCamera(Car, input);
            gameInstance.GetService<CameraComponent>().AddCamera(debugCamera);
            gameInstance.GetService<CameraComponent>().AddCamera(new DebugCamera(new Vector3(0, 200, 100), input));

            #endregion

            #region DynamicEnvironment
            refCubeMap = new RenderTargetCube(this.GraphicsDevice, 256, true, SurfaceFormat.Color, DepthFormat.Depth16);
            carSettings.EnvironmentMap = refCubeMap;
            foreach (TerrainModel model in terrainSegments)
            {
                model.Effect.Parameters["EnvironmentMap"].SetValue(refCubeMap);
            }
            //skyBoxEffect.Parameters["SkyboxTexture"].SetValue(refCubeMap);
            #endregion

            #region PostProcess

            postProcessTexture = new RenderTarget2D(GraphicsDevice,
                GraphicsDevice.Viewport.Width,
                GraphicsDevice.Viewport.Height,
                true, SurfaceFormat.Color, DepthFormat.Depth24);

            gaussianBlur = new GaussianBlur(gameInstance);
            bloom = new Bloom(gameInstance, gaussianBlur);
            motionBlur = new MotionBlur(gameInstance);

            #endregion


            prelightingRenderer = new PrelightingRenderer(GraphicsDevice, content, pointLightModel, spotLightModel);


            #region ShadowMap

            int shadowMapResoulution = 2048; //2048;
            shadowMap = new RenderTarget2D(GraphicsDevice, shadowMapResoulution, shadowMapResoulution, //GraphicsDevice.Viewport.Width, GraphicsDevice.Viewport.Height,
                 true, SurfaceFormat.Color, DepthFormat.Depth24);
            shadowMapEffect = content.Load<Effect>(@"Effects\Shadowmap");

            #endregion

            TriggerManager.GetInstance().CreatePositionTrigger("test", new Vector3(0, 1500, -3200), 3000f, new TimeSpan(0, 0, 5));
            TriggerManager.GetInstance().CreateRectangleTrigger("goalTest", new Vector3(-200, 1500, 2000), new Vector3(1500, 1500, 2000),
                                                                            new Vector3(-200, 1500, 4000), new Vector3(1500, 1500, 4000),
                                                                            new TimeSpan(0, 0, 5));

        }

        public Car MakeCar()
        {
            // Load car effect (10p-light, env-map)
            carEffect = content.Load<Effect>(@"Effects/CarShading");

            Car car = new Car(content.Load<Model>(@"Models/porsche"), 10.4725f);

            foreach (var mesh in car.Model.Meshes)
            {
                if (mesh.Name.StartsWith("wheel"))
                {
                    if (mesh.Name.EndsWith("001") || mesh.Name.EndsWith("002"))
                        mesh.Tag = 2;
                    else
                        mesh.Tag = 1;
                }
                else
                    mesh.Tag = 0;
            }


            // Keep some old settings from imported modeleffect, then replace with carEffect
            foreach (ModelMesh mesh in car.Model.Meshes)
            {
                foreach (ModelMeshPart part in mesh.MeshParts)
                {
                    BasicEffect oldEffect = part.Effect as BasicEffect;
                    if (oldEffect != null)
                    {
                        part.Effect = carEffect.Clone();
                        part.Effect.Parameters["MaterialDiffuse"].SetValue(oldEffect.DiffuseColor);
                        part.Effect.Parameters["MaterialAmbient"].SetValue(oldEffect.DiffuseColor * .5f);
                        part.Effect.Parameters["MaterialSpecular"].SetValue(oldEffect.DiffuseColor * .3f);
                    }
                }
            }
            car.Model.Meshes[0].Effects[1].Parameters["MaterialUnshaded"].SetValue(true);
            car.Model.Meshes[0].Effects[1].Parameters["MaterialAmbient"].SetValue(Color.Red.ToVector3() * 2.0f);
            car.Model.Meshes[0].Effects[2].Parameters["MaterialUnshaded"].SetValue(true);
            car.Model.Meshes[0].Effects[2].Parameters["MaterialAmbient"].SetValue(Color.Red.ToVector3() * 2.0f);

            // Place car at start.
            SetCarAtStart(car);
            car.Update();
            return car;
        }

        private void SetCarAtStart(Car car)
        {
            Vector3 carPosition = raceTrack.Curve.GetPoint(0);
            Vector3 carHeading = (raceTrack.Curve.GetPoint(.001f) - carPosition);
            car.Position = carPosition;
            car.Rotation = (float)Math.Atan2(carHeading.X, carHeading.Z) - (float)Math.Atan2(0, -1);
        }

        /// <summary>
        /// Loads a texture from Content and asign it to the cubemaps face.
        /// </summary>
        /// <param name="cubeMap"></param>
        /// <param name="filepath"></param>
        /// <param name="face"></param>
        private void LoadCubemapFace(TextureCube cubeMap, string filepath, CubeMapFace face)
        {
            Texture2D texture = content.Load<Texture2D>(filepath);
            byte[] data = new byte[texture.Width * texture.Height * 4];
            texture.GetData<byte>(data);
            cubeMap.SetData<byte>(face, data);
        }

        #endregion

        #region Update

        public override GameState UpdateState(GameTime gameTime)
        {
            InputComponent input = gameInstance.GetService<InputComponent>();
            if (input.GetPressed(Input.Exit))
            {
                Paused = !Paused;
            }
            if (!Paused)
            {

                if (input.GetPressed(Input.ChangeController))
                {
                    if (input.CurrentController == Controller.Keyboard)
                    {
                        input.CurrentController = Controller.GamePad;
                        Console.WriteLine("CurrentController equals GamePad");
                    }
                    else
                    {
                        input.CurrentController = Controller.Keyboard;
                        Console.WriteLine("CurrentController equals Keyboard");
                    }
                }

                if (input.GetPressed(Input.Console))
                    gameInstance.GetService<HUDConsoleComponent>().Toggle();

                //Apply changes to car
                Car.Update();

                #region Ray

                bool onTrack = false;

                for (int i = 0; i < navMesh.triangles.Length; i++)
                {
                    var triangle = navMesh.triangles[i];

                    if (CollisionCheck(triangle))
                    {
                        lastTriangle = i;
                        onTrack = true;
                        //break;
                    }
                }

                if (!onTrack)
                {
                    // Project car.Pos on lastTriangle.
                    var t = navMesh.triangles[lastTriangle];

                    float coord = Vector3.Dot(Car.Position - t.vertices[0], t.ac) / t.ac.LengthSquared();
                    bool trans = false;

                    if (coord < 0)
                    {
                        trans = true;
                        lastTriangle += (lastTriangle % 2 == 0 ? -2 : 2);
                    }
                    else if (coord > 1)
                    {
                        trans = true;
                        lastTriangle += (lastTriangle % 2 == 0 ? 2 : -2);
                    }

                    if (lastTriangle < 0)
                        lastTriangle += navMesh.triangles.Length;
                    if (lastTriangle >= navMesh.triangles.Length)
                        lastTriangle -= navMesh.triangles.Length;

                    if (!trans)
                    {
                        Car.Position = coord * t.ac + t.vertices[0];
                    }
                    else if (!CollisionCheck(navMesh.triangles[lastTriangle]))
                    {
                        t = navMesh.triangles[lastTriangle];
                        coord = MathHelper.Clamp(Vector3.Dot(Car.Position - t.vertices[0], t.ac) / t.ac.LengthSquared(), 0, 1);
                        Car.Position = coord * t.ac + t.vertices[0];
                        Car.Normal = t.normal;
                    }
                }

                #endregion

                foreach (GameObject obj in GraphicalObjects)
                {
                    obj.Update(gameTime);
                }

                TriggerManager.GetInstance().Update(gameTime, Car.Position);

            }
            else
            {
                GameState state = pauseMenu.UpdateState(gameTime);
                if (state == GameState.Gameplay)
                    Paused = false;
                else if (state != GameState.PausedGameplay)
                    return state;

                
            }

            // Particles should continue to spawn regardless of the pause state
            for (int x = -3; x < 3; x++)
            {
                for (int z = -3; z < 3; z++)
                {
                    rainSystem.AddParticle(Car.Position + new Vector3(
                        (float)UniversalRandom.GetInstance().NextDouble() * x * 200,
                        500 * (float)UniversalRandom.GetInstance().NextDouble(),
                        (float)UniversalRandom.GetInstance().NextDouble() * z * 200),
                        new Vector3(-1, -1, -1));//Vector3.Down);
                }
            }

            smokeTime += (float)gameTime.ElapsedGameTime.TotalSeconds;
            if (smokeTime > 0.2)
            {
                smokeSystem.AddParticle(Car.Position + Car.Forward * 500 +
                    new Vector3(
                        (-1f + 2 * (float)UniversalRandom.GetInstance().NextDouble()) * 500,
                        500 * (-1f + 2 * (float)UniversalRandom.GetInstance().NextDouble()),
                        (float)UniversalRandom.GetInstance().NextDouble() * 500),
                        Vector3.Up);
                smokeTime = 0;
            }

            //yellowSystem.AddParticle(new Vector3(-200, 1500, 2000), Vector3.Up);
            //redSystem.AddParticle(new Vector3(1500, 1500, 2000), Vector3.Up);
            //plasmaSystem.AddParticle(new Vector3(-200, 1500, 4000), Vector3.Up);
            //greenSystem.AddParticle(new Vector3(1500, 1500, 4000), Vector3.Up);

            //airParticles.AddParticle(Car.Position + Car.Forward * (float)UniversalRandom.GetInstance().NextDouble() * 5000 +
            //    new Vector3(
            //        (-1f + 2 * (float)UniversalRandom.GetInstance().NextDouble()) * 500,
            //        500 * (-1f + 2 * (float)UniversalRandom.GetInstance().NextDouble()),
            //        (float)UniversalRandom.GetInstance().NextDouble() * 500),
            //        Vector3.Up);

            return GameState.Gameplay;
        }

        private bool CollisionCheck(NavMeshVisualizer.NavMeshTriangle triangle)
        {
            var downray = new Ray(Car.Position, Vector3.Down);
            var upray = new Ray(Car.Position, Vector3.Up);

            float? d1 = downray.Intersects(triangle.trianglePlane),
                d2 = upray.Intersects(triangle.trianglePlane);

            if (d1.HasValue || d2.HasValue)
            {
                float d = d1.HasValue ? d1.Value : d2.Value;
                Ray ray = d1.HasValue ? downray : upray;

                var point = ray.Position + d * ray.Direction;

                bool onTriangle = PointInTriangle(triangle.vertices[0],
                    triangle.vertices[1],
                    triangle.vertices[2],
                    point);

                if (onTriangle)
                {
                    Car.Position = point;
                    Car.Normal = triangle.normal;
                }

                return onTriangle;
            }
            return false;
        }

        /// <summary>
        /// Determine whether a point P is inside the triangle ABC. Note, this function
        /// assumes that P is coplanar with the triangle.
        /// </summary>
        /// <returns>True if the point is inside, false if it is not.</returns>
        public static bool PointInTriangle(Vector3 A, Vector3 B, Vector3 C, Vector3 P)
        {
            // Prepare our barycentric variables
            Vector3 u = B - A;
            Vector3 v = C - A;
            Vector3 w = P - A;
            Vector3 vCrossW = Vector3.Cross(v, w);
            Vector3 vCrossU = Vector3.Cross(v, u);

            // Test sign of r
            if (Vector3.Dot(vCrossW, vCrossU) < 0)
                return false;

            Vector3 uCrossW = Vector3.Cross(u, w);
            Vector3 uCrossV = Vector3.Cross(u, v);

            // Test sign of t
            if (Vector3.Dot(uCrossW, uCrossV) < 0)
                return false;

            // At this piont, we know that r and t and both > 0
            float denom = uCrossV.Length();
            float r = vCrossW.Length() / denom;
            float t = uCrossW.Length() / denom;

            return (r <= 1 && t <= 1 && r + t <= 1);
        }

        #endregion

        #region Rendering

        public override void Draw(GameTime gameTime)
        {
            Texture2D pauseOverlay = null;
            if (Paused)
                pauseOverlay = pauseMenu.Render();

            gameInstance.GraphicsDevice.Clear(Color.Honeydew);

            skyBoxEffect.Parameters["ElapsedTime"].SetValue((float)gameTime.TotalGameTime.TotalSeconds);

            Matrix view = gameInstance.GetService<CameraComponent>().View;

            #region ShadowMap

            GraphicsDevice.SetRenderTarget(shadowMap);
            GraphicsDevice.Clear(Color.Black);

            float near = 20000, far = 50000; // whereever you are
            float camOffset = near + (far - near) / 2f;
            Vector3 focusPosition = Vector3.Zero; // Car.Position;

            //focusPosition += Vector3.Transform(800 * Vector3.Forward, Car.RotationMatrix);

            //focusPosition /= 100;
            //focusPosition.X = (int)focusPosition.X;
            //focusPosition.Y = (int)focusPosition.Y;
            //focusPosition.Z = (int)focusPosition.Z;
            //focusPosition *= 100;

            lightView =

                //Matrix.CreateTranslation(-focusPosition) *
                //Matrix.Invert(Car.RotationMatrix) * Vector3.Forward.GetRotationMatrix(directionalLight.Direction.XZPlane()) *
                //Matrix.CreateTranslation(focusPosition) *

                Matrix.CreateLookAt(focusPosition - camOffset * directionalLight.Direction, focusPosition, Vector3.Up); // Matrix.CreateLookAt(directionalLight.Position, Car.Position, Vector3.Up);
            lightProjection = Matrix.CreateOrthographic(10000, 10000, near, far); // Matrix.CreatePerspectiveFieldOfView(MathHelper.ToRadians(45), 1, 1000f, 10000f);
            
            //RenderShadowCasters(lightView, lightProjection);
            GraphicsDevice.SetRenderTarget(null);

            #endregion

            prelightingRenderer.Render(view, directionalLight, terrainSegments, terrainSegmentsCount, pointLights, Car, GraphicalObjects);

            if (!GameSettings.Default.PerformanceMode)
                RenderEnvironmentMap(gameTime);

            GraphicsDevice.SetRenderTarget(postProcessTexture);

            // Reset render settings
            GraphicsDevice.BlendState = BlendState.Opaque;
            GraphicsDevice.DepthStencilState = DepthStencilState.Default;
            GraphicsDevice.SamplerStates[0] = SamplerState.LinearWrap;

            GraphicsDevice.Clear(Color.White);
            RenderScene(gameTime, view, projectionMatrix, false);

            GraphicsDevice.SetRenderTarget(null);

            RenderPostProcess();

            RenderPauseMenu(pauseOverlay);

            base.Draw(gameTime);
        }

        public void RenderPauseMenu(Texture2D overlay)
        {
            if (Paused)
            {
                spriteBatch.Begin();
                Rectangle position = new Rectangle(GraphicsDevice.Viewport.Width / 2 - pauseMenu.RenderBounds.Width / 2,
                                                   GraphicsDevice.Viewport.Height / 2 - pauseMenu.RenderBounds.Height / 2,
                                                   pauseMenu.RenderBounds.Width, pauseMenu.RenderBounds.Height);
                spriteBatch.Draw(overlay, position, Color.White);
                spriteBatch.End();
            }
        }

        private void RenderPostProcess()
        {
            // Apply bloom effect
            Texture2D finalTexture = postProcessTexture;

            finalTexture = bloom.PerformBloom(postProcessTexture);

            Matrix view = gameInstance.GetService<CameraComponent>().View;

            if (gameInstance.GetService<InputComponent>().GetKey(Keys.M)) {
                motionBlurEnabled = !motionBlurEnabled;
            }

            if (motionBlurEnabled) {
                Matrix viewProjectionInverse = Matrix.Invert(view * prelightingRenderer.LightProjection);
                finalTexture = motionBlur.PerformMotionBlur(finalTexture, prelightingRenderer.DepthTarget, viewProjectionInverse, previousViewProjection);
                previousViewProjection = view * prelightingRenderer.LightProjection;
            }

            spriteBatch.Begin();
            //spriteBatch.Begin(0, BlendState.Opaque, null, null, null, postEffect);
            //foreach (EffectPass pass in postEffect.CurrentTechnique.Passes)
            //{
            //    pass.Apply();
                spriteBatch.Draw(finalTexture, Vector2.Zero, Color.White);

            //}

            spriteBatch.End();

        }

        //private void RenderShadowCasters(Matrix lightView, Matrix lightProjection)
        //{
        //    Matrix[] transforms = new Matrix[oakTree.Bones.Count];
        //    oakTree.CopyAbsoluteBoneTransformsTo(transforms);

        //    // Only one mesh
        //    var mesh = oakTree.Meshes[0];

        //    // Backup
        //    Effect[] oldEffects = new Effect[2];

        //    // Set shadowmapeffect
        //    for (int i = 0; i < mesh.MeshParts.Count; i++)
        //    {
        //        oldEffects[i] = mesh.MeshParts[i].Effect;
        //        mesh.MeshParts[i].Effect = shadowMapEffect;
        //    }

        //    shadowMapEffect.Parameters["View"].SetValue(lightView);
        //    //BoundingFrustum viewFrustum = new BoundingFrustum(lightView);
        //    shadowMapEffect.Parameters["Projection"].SetValue(lightProjection);
        //    shadowMapEffect.Parameters["AlphaMap"].SetValue(oldEffects[1].Parameters["AlphaMap"].GetValueTexture2D());
        //    shadowMapEffect.Parameters["AlphaEnabled"].SetValue(false);

        //    for (int i = 0; i < treePositions.Length; i++)
        //    {
        //        shadowMapEffect.Parameters["World"].SetValue(transforms[mesh.ParentBone.Index] * treeTransforms[i] *
        //                Matrix.CreateTranslation(treePositions[i]));

        //        for (int p = mesh.MeshParts.Count - 1; p >= 0; p--) // Need reversed draw order!
        //        {
        //            var part = mesh.MeshParts[p];

        //            shadowMapEffect.Parameters["AlphaEnabled"].SetValue(false); //p != 0);

        //            if (p == 0)
        //            {
        //                GraphicsDevice.BlendState = BlendState.AlphaBlend;
        //                //shadowMapEffect.Parameters["AlphaMap"].SetValue(null as Texture2D);
        //            }
        //            else
        //            {
        //                GraphicsDevice.BlendState = BlendState.AlphaBlend;
        //                //shadowMapEffect.Parameters["AlphaMap"].SetValue(oldEffects[1].Parameters["AlphaMap"].GetValueTexture2D());
        //            }

        //            foreach (EffectPass pass in part.Effect.CurrentTechnique.Passes)
        //            {
        //                pass.Apply();
        //                GraphicsDevice.Indices = part.IndexBuffer;
        //                GraphicsDevice.SetVertexBuffer(part.VertexBuffer);
        //                GraphicsDevice.DrawIndexedPrimitives(PrimitiveType.TriangleList,
        //                    part.VertexOffset,
        //                    0,
        //                    part.NumVertices,
        //                    part.StartIndex,
        //                    part.PrimitiveCount);
        //            }
        //        }
        //    }

        //    // Reset effects
        //    for (int i = 0; i < mesh.MeshParts.Count; i++)
        //    {
        //        mesh.MeshParts[i].Effect = oldEffects[i];
        //    }
        //}

        private void RenderEnvironmentMap(GameTime gameTime)
        {
            Matrix viewMatrix;
            for (int i = 0; i < 6; i++)
            {
                CubeMapFace cubeMapFace = (CubeMapFace)i;
                if (cubeMapFace == CubeMapFace.NegativeX)
                    viewMatrix = Matrix.CreateLookAt(Car.Position, Car.Position + Vector3.Left, Vector3.Up);
                else if (cubeMapFace == CubeMapFace.NegativeY)
                    //continue;
                    viewMatrix = Matrix.CreateLookAt(Car.Position, Car.Position + Vector3.Down, Vector3.Forward);
                else if (cubeMapFace == CubeMapFace.PositiveZ)
                    viewMatrix = Matrix.CreateLookAt(Car.Position, Car.Position + Vector3.Forward, Vector3.Up);
                else if (cubeMapFace == CubeMapFace.PositiveX)
                    viewMatrix = Matrix.CreateLookAt(Car.Position, Car.Position + Vector3.Right, Vector3.Up);
                else if (cubeMapFace == CubeMapFace.PositiveY)
                    viewMatrix = Matrix.CreateLookAt(Car.Position, Car.Position + Vector3.Up, Vector3.Backward);
                else if (cubeMapFace == CubeMapFace.NegativeZ)
                    viewMatrix = Matrix.CreateLookAt(Car.Position, Car.Position + Vector3.Backward, Vector3.Up);
                else
                    viewMatrix = Matrix.Identity;

                GraphicsDevice.SetRenderTarget(refCubeMap, cubeMapFace);
                GraphicsDevice.Clear(Color.White);

                //Matrix projection = Matrix.CreatePerspectiveFieldOfView(MathHelper.PiOver4,
                    //1.0f, 100f, 5000f);
                RenderScene(gameTime, viewMatrix, projectionMatrix, true);
            }

            // Default target
            GraphicsDevice.SetRenderTarget(null);
        }

        private void RenderScene(GameTime gameTime, Matrix view, Matrix projection, bool environment)
        {
            BoundingFrustum viewFrustum = new BoundingFrustum(debugCamera.View * projection);

            Matrix[] transforms = new Matrix[Car.Model.Bones.Count];
            Car.Model.CopyAbsoluteBoneTransformsTo(transforms);

            GraphicsDevice.BlendState = BlendState.Opaque;

            #region SkyBox

            skyBoxEffect.Parameters["View"].SetValue(view);
            skyBoxEffect.Parameters["Projection"].SetValue(projection);
            skyBoxModel.Meshes[0].Draw();

            #endregion

            for (int z = 0; z < terrainSegmentsCount; z++)
                for (int x = 0; x < terrainSegmentsCount; x++)
                {
                    var terrain = terrainSegments[x, z];
                    if (viewFrustum.Intersects(terrain.BoundingBox))
                    {
                        if (environment) {
                            Vector3 boxStart = Car.Position;
                            boxStart.Y = -5000;
                            Vector3 boxEnd = boxStart;
                            boxEnd.Y = 5000;
                            boxEnd.X += 50;
                            boxEnd.Z += 50;
                            if (terrain.BoundingBox.Intersects(new BoundingBox(boxStart, boxEnd)))
                                continue;
                        }
                        terrain.Draw(view, projection, gameInstance.GetService<CameraComponent>().Position,
                            directionalLight, lightView, lightProjection, shadowMap);
                    }
                }


            GraphicsDevice.BlendState = BlendState.AlphaBlend;

            // Set view to particlesystems
            foreach (ParticleSystem pSystem in particleSystems)
                pSystem.SetCamera(view, projection);

            #region RenderObjects

            foreach (GameObject obj in GraphicalObjects)
            {
                obj.Draw(view, projection);
            }

            #endregion

            #region Animals

            {
                var t = ((float)gameTime.TotalGameTime.TotalSeconds / 3f) % 1;
                var position = birdCurve.GetPoint(t);
                var heading = Vector3.Normalize(birdCurve.GetPoint(t + (t > .5 ? -.01f : .01f)) - position);
                if (t > .5)
                    heading *= -1;

                birdModel.Draw(Matrix.CreateScale(1) *
                    Vector3.Forward.GetRotationMatrix(heading) *
                    Matrix.CreateTranslation(position),
                    view, projection);
            }

            #endregion


            if (!environment)
            {
                foreach (Car car in gameInstance.GetService<CarControlComponent>().Cars.Values)
                    DrawCar(view, projection, car);
                DrawGhostCar(view, projection, gameTime);
            }

            if (beams == null)
            {
                beamEffect = new BasicEffect(GraphicsDevice);

                int nBeams = 20;
                float distance = 2500, beamLength = 75000;
                float offset = (nBeams / 2f) * distance;

                beams = new VertexPositionColor[2 * nBeams * nBeams];
                for (int x = 0; x < nBeams; x++)
                {
                    for (int z = 0; z < nBeams; z++)
                    {
                        beams[2 * (x + nBeams * z)] = new VertexPositionColor(
                            new Vector3(x * distance - offset, 0,
                            z * distance - offset) - beamLength * directionalLight.Direction, Color.Yellow);
                        beams[2 * (x + nBeams * z) + 1] = new VertexPositionColor(new Vector3(x * distance - offset, 0, 
                            z * distance - offset), Color.Yellow);
                    }
                }
            }

            beamEffect.DiffuseColor = Color.Purple.ToVector3();
            beamEffect.View = view;
            beamEffect.Projection = projection;

            //beamEffect.CurrentTechnique.Passes[0].Apply();
            //GraphicsDevice.DrawUserPrimitives(PrimitiveType.LineList, beams, 0, beams.Length / 2);

        }

        BasicEffect beamEffect;
        VertexPositionColor[] beams;

        private void DrawModel(Model m, Matrix view, Matrix projection, Vector3 position, Matrix transform)
        {
            Matrix[] transforms = new Matrix[m.Bones.Count];
            m.CopyAbsoluteBoneTransformsTo(transforms);

            foreach (ModelMesh mesh in m.Meshes)
            {
                foreach (Effect effect in mesh.Effects)
                {
                    Matrix world = transforms[mesh.ParentBone.Index] * transform *
                        Matrix.CreateTranslation(position);
                    Matrix normalMatrix = Matrix.Invert(Matrix.Transpose(world));

                    //effect.Parameters["NormalMatrix"].SetValue(normalMatrix);
                    effect.Parameters["World"].SetValue(world);
                    effect.Parameters["View"].SetValue(view);
                    effect.Parameters["Projection"].SetValue(projection);

                    //effect.Parameters["DirectionalDirection"].SetValue(directionalLight.Direction);
                    //effect.Parameters["DirectionalDiffuse"].SetValue(directionalLight.Diffuse);
                    //effect.Parameters["DirectionalAmbient"].SetValue(directionalLight.Ambient);
                }
                mesh.Draw();
            }
        }

        #region Car drawing

        private void DrawCar(Matrix view, Matrix projection, Car car)
        {
            carSettings.View = view;
            carSettings.Projection = projection;

            //carSettings.EyePosition = view.Translation;
            carSettings.EyePosition = gameInstance.GetService<CameraComponent>().Position;

            //Vector3 direction = car.Position - carSettings.EyePosition;
            //direction.Y = 0;
            //direction = Vector3.Normalize(direction);

            Vector3[] positions = new Vector3[pointLights.Count];
            Vector3[] diffuses = new Vector3[pointLights.Count];
            float[] ranges = new float[pointLights.Count];
            for (int i = 0; i < 4; i++)
            {
                positions[i] = pointLights[i].Position;
                diffuses[i] = pointLights[i].Diffuse;
                ranges[i] = pointLights[i].Range;
            }

            carSettings.LightPosition = positions;
            carSettings.LightDiffuse = diffuses;
            carSettings.LightRange = ranges;
            carSettings.NumLights = 2;

            carSettings.DirectionalLightDirection = directionalLight.Direction;
            carSettings.DirectionalLightDiffuse = directionalLight.Diffuse;
            carSettings.DirectionalLightAmbient = directionalLight.Ambient;

            foreach (var mesh in car.Model.Meshes) // 5 meshes
            {
                Matrix world = Matrix.Identity;
                // Wheel transformation
                if ((int)mesh.Tag > 0)
                {
                    world *= Matrix.CreateRotationX(car.WheelRotationX);
                    if ((int)mesh.Tag > 1)
                        world *= Matrix.CreateRotationY(car.WheelRotationY);
                }

                // Local modelspace, due to bad .X-file/exporter
                world *= car.Model.Bones[1 + car.Model.Meshes.IndexOf(mesh) * 2].Transform;

                // World
                world *= car.RotationMatrix * car.TranslationMatrix;

                carSettings.World = world;
                carSettings.NormalMatrix = Matrix.Invert(Matrix.Transpose(world));


                foreach (Effect effect in mesh.Effects)
                {// 5 effects for main, 1 for each wheel
                    effect.SetCarShadingParameters(carSettings);

                    effect.Parameters["LightView"].SetValue(lightView);
                    effect.Parameters["LightProjection"].SetValue(lightProjection);
                    effect.Parameters["ShadowMap"].SetValue(shadowMap);
                }

                mesh.Draw();
            }

        }

        float ghostWheelRotation = 0;
        Vector3 ghostPosition;

        private void DrawGhostCar(Matrix view, Matrix projection, GameTime gameTime)
        {
            float t = ((float)gameTime.TotalGameTime.TotalSeconds / 1020f) % 1f;

            foreach (var mesh in Car.Model.Meshes) // 5 meshes
            {
                Matrix world = Matrix.Identity;

                // Wheel transformation
                if ((int)mesh.Tag > 0)
                {
                    world *= Matrix.CreateRotationX(ghostWheelRotation);
                }

                // Local modelspace, due to bad .X-file/exporter
                world *= Car.Model.Bones[1 + Car.Model.Meshes.IndexOf(mesh) * 2].Transform;

                // World
                Vector3 position = raceTrack.Curve.GetPoint(t);
                Vector3 heading = raceTrack.Curve.GetPoint((t + .01f) % 1f) - position;
                heading.Normalize();

                Vector3 normal = Vector3.Up;

                foreach (var triangle in navMesh.triangles)
                {
                    var downray = new Ray(position, Vector3.Down);
                    var upray = new Ray(position, Vector3.Up);

                    float? d1 = downray.Intersects(triangle.trianglePlane),
                        d2 = upray.Intersects(triangle.trianglePlane);

                    if (d1.HasValue || d2.HasValue)
                    {
                        float d = d1.HasValue ? d1.Value : d2.Value;
                        Ray ray = d1.HasValue ? downray : upray;

                        var point = ray.Position + d * ray.Direction;

                        bool onTriangle = PointInTriangle(triangle.vertices[0],
                            triangle.vertices[1],
                            triangle.vertices[2],
                            point);

                        if (onTriangle)
                        {
                            position = point;
                            normal = triangle.normal;
                            break;
                        }
                    }
                }

                ghostWheelRotation -= (position - ghostPosition).Length() / 10.4725f;
                ghostPosition = position;

                world *= Vector3.Forward.GetRotationMatrix(heading) * Vector3.Up.GetRotationMatrix(normal) *
                    Matrix.CreateTranslation(position);

                carSettings.World = world;
                carSettings.NormalMatrix = Matrix.Invert(Matrix.Transpose(world));

                foreach (Effect effect in mesh.Effects) // 5 effects for main, 1 for each wheel
                    effect.SetCarShadingParameters(carSettings);

                mesh.Draw();
            }

        }

        #endregion

        #endregion

    }
}