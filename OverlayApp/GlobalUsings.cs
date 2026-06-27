// Global using directives to resolve WPF vs WinForms namespace conflicts.
// When UseWindowsForms=true, implicit usings pull in System.Windows.Forms,
// System.Drawing, etc. which conflict with WPF types.

global using WpfApplication = System.Windows.Application;
global using WpfButton = System.Windows.Controls.Button;
global using WpfImage = System.Windows.Controls.Image;
global using WpfBrush = System.Windows.Media.Brush;
global using WpfBrushes = System.Windows.Media.Brushes;
global using WpfColor = System.Windows.Media.Color;
global using WpfPoint = System.Windows.Point;
global using WpfMessageBox = System.Windows.MessageBox;
global using WpfHorizontalAlignment = System.Windows.HorizontalAlignment;
global using WpfFlowDirection = System.Windows.FlowDirection;
global using WpfOpenFileDialog = Microsoft.Win32.OpenFileDialog;
