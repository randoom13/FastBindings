namespace MauiExample
{
    public partial class MainPage : ContentPage
    {
        int count = 0;

        public MainPage()
        {
            InitializeComponent();
            BindingContext = new MainWindowViewModel();
        }

        private void OnCounterClicked(object sender, EventArgs e)
        {
            var model = BindingContext as MainWindowViewModel;
            model!.Mod.MyProperty = Guid.NewGuid().ToString();
            model!.Color = model!.Color == Colors.Green ? Colors.Red : Colors.Green;
            count++;

            if (count == 1)
                CounterBtn.Text = $"Clicked {count} time";
            else
                CounterBtn.Text = $"Clicked {count} times";

            SemanticScreenReader.Announce(CounterBtn.Text);
        }
    }

}
