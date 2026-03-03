using System;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Media3D;
using HelixToolkit.Wpf.SharpDX;
using SharpDX;

namespace EveOMock
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();

            Random random = new Random();
            this.Title += random.Next().ToString("X");

            var randomColor = System.Windows.Media.Color.FromArgb(255, (byte)random.Next(0, 127), (byte)random.Next(0, 127), (byte)random.Next(0, 127));
            viewPort.BackgroundColor = randomColor;
            
            viewPort.EffectsManager = new DefaultEffectsManager();

            var builder = new MeshBuilder();
            builder.AddBox(new Vector3(0, 0, 0), 1, 1, 1);

            cubeModel.Geometry = builder.ToMeshGeometry3D();

            var allMaterials = PhongMaterials.Materials;
            int index = random.Next(allMaterials.Count);
            var randomMaterial = allMaterials[index];
            cubeModel.Material = randomMaterial;

            CompositionTarget.Rendering += (s, e) =>
            {
                double time = DateTime.Now.Ticks / (double)TimeSpan.TicksPerSecond;
                double angle = (time * 60) % 360;

                cubeModel.Transform = new RotateTransform3D(
                    new AxisAngleRotation3D(new System.Windows.Media.Media3D.Vector3D(0, 1, 0), angle)
                );
            };
        }

        protected override void OnClosed(EventArgs e)
        {
            viewPort.EffectsManager?.Dispose();
            base.OnClosed(e);
        }
    }
}