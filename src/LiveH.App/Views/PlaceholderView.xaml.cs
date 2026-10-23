namespace LiveH.App.Views;

public partial class PlaceholderView : ContentView
{
    public PlaceholderView(string title, string moduleName)
    {
        InitializeComponent();
        PageTitle.Text = title;
        ModuleLabel.Text = moduleName;
    }
}
