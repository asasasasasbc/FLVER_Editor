
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

using System.Windows.Forms;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Audio;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Microsoft.Xna.Framework.Media;


namespace MySFformat
{
    class Mono3D : Game
    {
        GraphicsDeviceManager graphics;
        SpriteBatch spriteBatch;
        Texture2D test;
        KeyboardState prevState = new KeyboardState();
        MouseState prevMState = new MouseState();

        public VertexPositionColor[] vertices = new VertexPositionColor[0];

        VertexPositionTexture[] floorVerts;
        BasicEffect effect;

        float mouseX, mouseY;

        float cameraX = 0;
        float cameraY = 4;
        float cameraZ = 2;

        float offsetX = 0;
        float offsetY = 0;
        float offsetZ = 0;

        float centerX = 0;
        float centerY = 0;
        float centerZ = 0;

        public Mono3D()
        {
            Window.Title = "FLVER Viewer by Forsakensilver, press F to refresh";
            Window.AllowUserResizing = true;
            this.IsMouseVisible = true;
            graphics = new GraphicsDeviceManager(this);
            Content.RootDirectory = "Content";
            // string path = @"data\img\27.png";
            //test = Content.Load<Texture2D>(@"data\img\27.png");


        }




        /// <summary>
        /// Allows the game to perform any initialization it needs to before starting to run.
        /// This is where it can query for any required services and load any non-graphic
        /// related content.  Calling base.Initialize will enumerate through any components
        /// and initialize them as well.
        /// </summary>
        protected override void Initialize()
        {
            // TODO: Add your initialization logic here
            floorVerts = new VertexPositionTexture[6];
            floorVerts[0].Position = new Vector3(-20, -20, 0);
            floorVerts[1].Position = new Vector3(-20, 20, 0);
            floorVerts[2].Position = new Vector3(20, -20, 0);
            floorVerts[3].Position = floorVerts[1].Position;
            floorVerts[4].Position = new Vector3(20, 20, 0);
            floorVerts[5].Position = floorVerts[2].Position;

            effect = new BasicEffect(graphics.GraphicsDevice);
            effect.VertexColorEnabled = true;
            base.Initialize();
        }


        /// <summary>
        /// LoadContent will be called once per game and is the place to load
        /// all of your content.
        /// </summary>
        protected override void LoadContent()
        {
            // Create a new SpriteBatch, which can be used to draw textures.
            spriteBatch = new SpriteBatch(GraphicsDevice);


          /*  string path = @"data\img\27.png";

            System.Drawing.Bitmap btt = new System.Drawing.Bitmap(path);
            test = Texture2D.FromStream(this.GraphicsDevice, File.OpenRead(path));
            test = getTextureFromBitmap(btt, this.GraphicsDevice);*/
            // TODO: use this.Content to load your game content here
        }



        //Refer to the code at http://florianblock.blogspot.com/2008/06/copying-dynamically-created-bitmap-to.html
        //Also refer to https://gamedev.stackexchange.com/questions/6440/bitmap-to-texture2d-problem-with-colors
        //Modied by Alan Zhang
        public static Texture2D getTextureFromBitmap(System.Drawing.Bitmap b, GraphicsDevice graphicsDevice)
        {
            Texture2D tx = null;
            using (MemoryStream s = new MemoryStream())
            {
                b.Save(s, System.Drawing.Imaging.ImageFormat.Png);
                s.Seek(0, SeekOrigin.Begin);
                tx = Texture2D.FromStream(graphicsDevice, s);
            }
            return tx;
        }

        /// <summary>
        /// UnloadContent will be called once per game and is the place to unload
        /// game-specific content.
        /// </summary>
        protected override void UnloadContent()
        {
            // TODO: Unload any non ContentManager content here
        }

        /// <summary>
        /// Allows the game to run logic such as updating the world,
        /// checking for collisions, gathering input, and playing audio.
        /// </summary>
        /// <param name="gameTime">Provides a snapshot of timing values.</param>
        protected override void Update(GameTime gameTime)
        {

            KeyboardState state = Keyboard.GetState();
            MouseState mState = Mouse.GetState();

            if (GamePad.GetState(PlayerIndex.One).Buttons.Back == Microsoft.Xna.Framework.Input.ButtonState.Pressed || Keyboard.GetState().IsKeyDown(Microsoft.Xna.Framework.Input.Keys.Escape))
                Application.Exit();

            float delta = (float)gameTime.ElapsedGameTime.TotalSeconds;



            //Marine_Yes00.mp3

           

            if (mState.LeftButton == Microsoft.Xna.Framework.Input.ButtonState.Pressed && IsActive)
            {
                float mdx = mState.X - prevMState.X;
                float mdy = mState.Y - prevMState.Y;
                {
                    System.Numerics.Vector3 p = new System.Numerics.Vector3(cameraX, cameraY, cameraZ);
                    System.Numerics.Vector3 p2 = Program.RotatePoint(p, 0, 0, -mdx * 0.01f);
                    cameraX = p2.X;
                    cameraY = p2.Y;
                    cameraZ = p2.Z;
                }
                {
                    System.Numerics.Vector3 p = new System.Numerics.Vector3(cameraX, cameraY, cameraZ);

                    float nX = cameraY;
                    float nY = -cameraX;
                 

                    System.Numerics.Vector3 p2 = Program.RotateLine(p, new System.Numerics.Vector3(0, 0, 0),
                        new System.Numerics.Vector3(nX, nY, 0), mdy * 0.01f);


                    cameraX = p2.X;
                    cameraY = p2.Y;
                    cameraZ = p2.Z;

                }

                

            }

            if (mState.ScrollWheelValue - prevMState.ScrollWheelValue > 0)
            {
                //mouseY -= (50 * delta);
                System.Numerics.Vector3 p = new System.Numerics.Vector3(cameraX, cameraY, cameraZ);



                cameraX = p.X - 1 * (float)(p.X / p.Length());
                cameraY = p.Y - 1 * (float)(p.Y / p.Length());
                cameraZ = p.Z - 1 * (float)(p.Z / p.Length());
            }

            if (mState.ScrollWheelValue - prevMState.ScrollWheelValue < 0)
            {
                System.Numerics.Vector3 p = new System.Numerics.Vector3(cameraX, cameraY, cameraZ);


                cameraX = p.X + 1 * (float)(p.X / p.Length());
                cameraY = p.Y + 1 * (float)(p.Y / p.Length());
                cameraZ = p.Z + 1 * (float)(p.Z / p.Length());
            }

            if (state.IsKeyDown(Microsoft.Xna.Framework.Input.Keys.Right))
            {
                System.Numerics.Vector3 p = new System.Numerics.Vector3(cameraX,cameraY,cameraZ);
                System.Numerics.Vector3 p2 = Program.RotatePoint(p, 0, 0, 5 * delta);
                cameraX = p2.X;
                cameraY = p2.Y;
                cameraZ = p2.Z;
            }

            if (state.IsKeyDown(Microsoft.Xna.Framework.Input.Keys.Left))
            {
                System.Numerics.Vector3 p = new System.Numerics.Vector3(cameraX, cameraY, cameraZ);
                System.Numerics.Vector3 p2 = Program.RotatePoint(p, 0, 0, -5 * delta);
                cameraX = p2.X;
                cameraY = p2.Y;
                cameraZ = p2.Z;
            }

            if (state.IsKeyDown(Microsoft.Xna.Framework.Input.Keys.Up))
            {
                //mouseY -= (50 * delta);
                System.Numerics.Vector3 p = new System.Numerics.Vector3(cameraX, cameraY, cameraZ);


                System.Numerics.Vector3 p2 = Program.RotateLine(p, new System.Numerics.Vector3(0, 0, 0),
                    new System.Numerics.Vector3(cameraY, -cameraX, 0), 3 * delta);
                cameraX = p2.X;
                cameraY = p2.Y;
                cameraZ = p2.Z;
            }

            if (state.IsKeyDown(Microsoft.Xna.Framework.Input.Keys.Down))
            {
                System.Numerics.Vector3 p = new System.Numerics.Vector3(cameraX, cameraY, cameraZ);


                System.Numerics.Vector3 p2 = Program.RotateLine(p, new System.Numerics.Vector3(0, 0, 0),
                    new System.Numerics.Vector3(cameraY, -cameraX, 0), -3 * delta);
                cameraX = p2.X;
                cameraY = p2.Y;
                cameraZ = p2.Z;
            }


            if (state.IsKeyDown(Microsoft.Xna.Framework.Input.Keys.OemComma))
            {
                //mouseY -= (50 * delta);
                System.Numerics.Vector3 p = new System.Numerics.Vector3(cameraX, cameraY, cameraZ);


                
                cameraX = p.X - 3 *delta * (float)(p.X / p.Length());
                cameraY = p.Y - 3 * delta * (float)(p.Y / p.Length());
                cameraZ = p.Z-  3 * delta * (float)(p.Z / p.Length());
            }

            if (state.IsKeyDown(Microsoft.Xna.Framework.Input.Keys.OemPeriod))
            {
                System.Numerics.Vector3 p = new System.Numerics.Vector3(cameraX, cameraY, cameraZ);


                cameraX = p.X + 3 * delta * (float)(p.X / p.Length());
                cameraY = p.Y + 3 * delta * (float)(p.Y / p.Length());
                cameraZ = p.Z + 3 * delta * (float)(p.Z / p.Length());
            }

            if (state.IsKeyDown(Microsoft.Xna.Framework.Input.Keys.NumPad8))
            {
                offsetZ += 3 * delta;

            }

            if (state.IsKeyDown(Microsoft.Xna.Framework.Input.Keys.NumPad2))
            {

                offsetZ -= 3 * delta; ;
            }

            if (state.IsKeyDown(Microsoft.Xna.Framework.Input.Keys.F))
            {

                Program.updateVertices();
            }
            //mouseX = Mouse.GetState().Position.X;
            //mouseY = Mouse.GetState().Position.Y;
            // TODO: Add your update logic here

            prevState = state;
            prevMState = mState;
            base.Update(gameTime);
        }

        private Vector3 RotatePoint()
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// This is called when the game should draw itself.
        /// </summary>
        /// <param name="gameTime">Provides a snapshot of timing values.</param>
        protected override void Draw(GameTime gameTime)
        {
            GraphicsDevice.Clear(Color.CornflowerBlue);
            spriteBatch.Begin();
            // TODO: Add your drawing code here
            // Rectangle screenRectangle = new Rectangle((int)mouseX, (int)mouseY, 50, 50);
            //  spriteBatch.Draw(test, screenRectangle, Color.White);
            DrawGround();

            /*var vertices = new VertexPositionColor[4];
             vertices[0].Position = new Vector3(100, 100, 0);
             vertices[0].Color = Color.Black;
             vertices[1].Position = new Vector3(200, 100, 0);
             vertices[1].Color = Color.Red;
             vertices[2].Position = new Vector3(200, 200, 0);
             vertices[2].Color = Color.Black;
             vertices[3].Position = new Vector3(100, 200, 0);
             vertices[3].Color = Color.Red;
             */
            if (vertices.Length > 0)
            {
                GraphicsDevice.DrawUserPrimitives<VertexPositionColor>(PrimitiveType.LineList, vertices, 0, vertices.Length / 2);
            }
           

            spriteBatch.End();
            base.Draw(gameTime);
        }

        void DrawGround()
        {
            // The assignment of effect.View and effect.Projection
            // are nearly identical to the code in the Model drawing code.
            // var cameraPosition = new Vector3(0 + mouseX, 40 + mouseY, 20);
            var cameraPosition = new Vector3(cameraX + offsetX, cameraY + offsetY, cameraZ + offsetZ);
            var cameraLookAtVector =new Vector3(centerX + offsetX,centerY + offsetY,centerZ + offsetZ);
            var cameraUpVector = Vector3.UnitZ;



            effect.View = Matrix.CreateLookAt(
                cameraPosition, cameraLookAtVector, cameraUpVector);
            //effect.VertexColorEnabled = true;
            float aspectRatio =
                graphics.PreferredBackBufferWidth / (float)graphics.PreferredBackBufferHeight;
            float fieldOfView = Microsoft.Xna.Framework.MathHelper.PiOver4;
            float nearClipPlane = 1;
            float farClipPlane = 200;

            effect.Projection = Matrix.CreatePerspectiveFieldOfView(
                fieldOfView, aspectRatio, nearClipPlane, farClipPlane);


           /* foreach (var pass in effect.CurrentTechnique.Passes)
            {
                pass.Apply();

                graphics.GraphicsDevice.DrawUserPrimitives(
                    // We’ll be rendering two trinalges
                    PrimitiveType.TriangleList,
                    // The array of verts that we want to render
                    floorVerts,
                    // The offset, which is 0 since we want to start 
                    // at the beginning of the floorVerts array
                    0,
                    // The number of triangles to draw
                    2);
            }*/


            VertexPositionColor[] lines;
            lines = new VertexPositionColor[6];

            lines[0] = new VertexPositionColor(new Vector3(0, 0, 0), Color.Red);

            lines[1] = new VertexPositionColor(new Vector3(20, 0, 0), Color.Red);

            lines[2] = new VertexPositionColor(new Vector3(0, 0, 0), Color.Blue);

            lines[3] = new VertexPositionColor(new Vector3(0, 10, 0), Color.Blue);

            lines[4] = new VertexPositionColor(new Vector3(0, 0, 0), Color.Yellow);

            lines[5] = new VertexPositionColor(new Vector3(0, 0, 10), Color.Yellow);

            foreach (EffectPass pass in effect.CurrentTechnique.Passes)

            {
                pass.Apply();

                graphics.GraphicsDevice.DrawUserPrimitives(PrimitiveType.LineList, lines, 0, 3);



            }


        }
    }
}
